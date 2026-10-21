using System;
using System.Collections.Generic;
using UnityEngine;

// =====================================================================
//  StealthBrain  -  [IS] Stealth Enemy (Peiris)
//  Architecture: Finite State Machine (FSM) driven by a continuous
//  "awareness" value (0..1) that is fed by hearing and sight.
//
//  This file is PURE LOGIC. It never touches GameObjects, NavMesh,
//  physics or rendering. It reads facts from a StealthBlackboard and
//  gives orders through the IStealthBody interface. The MonoBehaviour
//  StealthEnemyController implements IStealthBody, so the same brain
//  could drive a different body (or a unit test) without changes.
//
//      StealthPerception --writes--> StealthBlackboard --read by--> StealthBrain
//      StealthBrain --orders--> IStealthBody (StealthEnemyController -> NavMeshAgent)
// =====================================================================

public enum StealthStateId { Patrol, Investigate, Hunting, Hiding, Attack, Lost, Dead }

// How fast the body should move. The controller maps these to real speeds.
public enum StealthSpeed { Walk, Cautious, Run, Sprint }

// All the numbers that shape the behaviour, editable in the Inspector.
[Serializable]
public class StealthTuning
{
    [Header("Awareness thresholds (0..1)")]
    [Tooltip("At or above this the enemy goes to check out what it heard/glimpsed.")]
    public float investigateThreshold = 0.25f;
    [Tooltip("At or above this the enemy is sure the player is there and hunts.")]
    public float huntThreshold = 0.7f;
    [Tooltip("Hysteresis: a hunting enemy only gives up when awareness drops BELOW this (lower than huntThreshold so it does not flicker).")]
    public float loseThreshold = 0.45f;
    [Tooltip("Below this the enemy calms down and goes back to patrolling.")]
    public float calmThreshold = 0.1f;

    [Header("Distances (m)")]
    public float attackRange = 1.8f;
    [Tooltip("If the player spots the enemy closer than this, it attacks instead of hiding.")]
    public float ambushRange = 5f;
    [Tooltip("When the player is facing away and closer than this, the enemy slows down to creep up silently.")]
    public float creepRange = 8f;
    public float searchRadius = 6f;

    [Header("Timers (s)")]
    public float minStateTime = 0.4f;
    public float lookAroundTime = 3f;
    public float patrolPause = 1.5f;
    public float maxHideTime = 6f;
    [Tooltip("After hiding once the enemy must wait this long before hiding again (stops a hide/peek loop).")]
    public float hideCooldown = 6f;
    [Tooltip("How long the player must look away before a hidden enemy comes out.")]
    public float unseenBeforeAmbush = 1f;
    public float maxSearchTime = 20f;

    [Header("Search")]
    public int searchPoints = 4;
}

// What the brain knows right now. Written by StealthPerception / the controller.
public class StealthBlackboard
{
    public float Awareness;                 // 0 = calm, 1 = certain the player is here
    public bool CanSeePlayer;
    public bool PlayerSeesMe;               // player is looking at me with clear line of sight
    public bool PlayerAlive = true;
    public bool HasPlayer;                  // a player exists in the scene
    public float DistanceToPlayer = float.MaxValue;
    public Vector3 PlayerPosition;
    public float PlayerSpeed;               // horizontal speed (m/s); still players are easier to sneak up on

    // Best guess of where the player is: where I last SAW them, or where I last HEARD a noise.
    public Vector3 TargetPosition;
    public bool HasTarget;

    // One-frame events (cleared by the brain after every tick)
    public bool HeardNoise;
    public Vector3 NoisePosition;
    public float NoiseStrength;             // perceived strength after distance/walls
    public bool WasDamaged;

    public bool IsDead;
}

// Everything the brain is allowed to ask the body to do.
public interface IStealthBody
{
    Vector3 Position { get; }
    Vector3 Forward { get; }
    void MoveTo(Vector3 target, StealthSpeed speed, float stopDistance);
    void StopMoving();
    bool HasArrived();
    void FaceTowards(Vector3 worldPoint);
    bool TryGetPatrolPoint(out Vector3 point);
    bool TryGetSearchPoint(Vector3 around, float radius, out Vector3 point);
    bool TryFindHideSpot(out Vector3 spot);
    bool IsAttackReady { get; }
    void Attack(bool sneakAttack);
}

// Base class for every state. Each state decides ONLY its own outgoing transitions.
public abstract class StealthState
{
    protected StealthBrain brain;
    protected StealthBlackboard bb { get { return brain.Board; } }
    protected IStealthBody body { get { return brain.Body; } }
    protected StealthTuning t { get { return brain.Tuning; } }
    protected float timeInState { get { return brain.TimeInState; } }

    public abstract StealthStateId Id { get; }
    public void Bind(StealthBrain owner) { brain = owner; }

    public virtual void Enter() { }
    public virtual void Exit() { }

    // Run one frame of behaviour. Return the id of the state to be in next
    // (return Id to stay in this state).
    public abstract StealthStateId Tick(float dt);
}

public class StealthBrain
{
    public readonly StealthBlackboard Board = new StealthBlackboard();
    public readonly IStealthBody Body;
    public readonly StealthTuning Tuning;

    public StealthStateId CurrentId { get { return current.Id; } }
    public float TimeInState { get; private set; }
    public float Clock { get; private set; }                // brain's own time, so it is testable
    public float HideAllowedAt;                             // shared by Hunting/Hiding for the cooldown
    public string LastReason = "start";

    // (from, to, reason) - the controller uses this for logging, colours and animation.
    public event Action<StealthStateId, StealthStateId, string> StateChanged;

    readonly Dictionary<StealthStateId, StealthState> states = new Dictionary<StealthStateId, StealthState>();
    StealthState current;

    public StealthBrain(IStealthBody body, StealthTuning tuning)
    {
        Body = body;
        Tuning = tuning ?? new StealthTuning();

        Register(new PatrolState());
        Register(new InvestigateState());
        Register(new HuntingState());
        Register(new HidingState());
        Register(new AttackState());
        Register(new LostState());
        Register(new DeadState());

        current = states[StealthStateId.Patrol];
        current.Enter();
    }

    void Register(StealthState s)
    {
        s.Bind(this);
        states[s.Id] = s;
    }

    public void Tick(float dt)
    {
        Clock += dt;
        TimeInState += dt;

        // 1) "Any state" transitions that override whatever the state wants.
        StealthStateId next;
        string reason;
        if (!GlobalTransition(out next, out reason))
        {
            // 2) Otherwise the current state decides.
            next = current.Tick(dt);
            reason = LastReason;
        }

        if (next != current.Id)
        {
            // Anti-flicker: ordinary transitions must wait minStateTime.
            // Urgent ones (death, attack, being shot) happen immediately.
            bool urgent = next == StealthStateId.Dead || next == StealthStateId.Attack || Board.WasDamaged;
            if (urgent || TimeInState >= Tuning.minStateTime)
                ChangeState(next, reason);
        }

        // One-frame events have now been used.
        Board.HeardNoise = false;
        Board.WasDamaged = false;
    }

    bool GlobalTransition(out StealthStateId next, out string reason)
    {
        next = current.Id;
        reason = "";
        StealthStateId id = current.Id;

        if (Board.IsDead)
        {
            if (id == StealthStateId.Dead) return false;
            next = StealthStateId.Dead; reason = "killed";
            return true;
        }
        if (id == StealthStateId.Dead) return false;

        // Player died: stop hunting, forget, go back to patrol.
        if (!Board.PlayerAlive && id != StealthStateId.Patrol)
        {
            Board.Awareness = 0f;
            next = StealthStateId.Patrol; reason = "player dead";
            return true;
        }

        // Got hurt: I now know exactly where the player is -> hunt (unless already fighting).
        if (Board.WasDamaged && id != StealthStateId.Attack && id != StealthStateId.Hunting)
        {
            Board.Awareness = 1f;
            next = StealthStateId.Hunting; reason = "took damage";
            return true;
        }
        return false;
    }

    // Called by a state when it wants to explain WHY it is switching (shown in the log/overlay).
    public StealthStateId Go(StealthStateId to, string reason)
    {
        LastReason = reason;
        return to;
    }

    void ChangeState(StealthStateId to, string reason)
    {
        StealthStateId from = current.Id;
        current.Exit();
        current = states[to];
        TimeInState = 0f;
        current.Enter();
        if (StateChanged != null) StateChanged(from, to, reason);
    }

    // Lower awareness on purpose (used when a search ends with nothing found).
    public void CalmDown(float maxAwareness)
    {
        Board.Awareness = Mathf.Min(Board.Awareness, maxAwareness);
    }
}
