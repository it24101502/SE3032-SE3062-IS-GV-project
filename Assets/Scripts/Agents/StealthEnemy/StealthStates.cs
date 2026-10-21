using UnityEngine;

// =====================================================================
//  StealthStates  -  [IS] Stealth Enemy (Peiris)
//  The 7 states of the FSM. Each state:
//    Enter()  - runs once when we switch INTO the state
//    Tick()   - runs every frame, does the behaviour, returns next state
//    Exit()   - runs once when we leave
//
//  Transition summary (A = awareness 0..1):
//    Patrol     -> Investigate  A >= 0.25 (heard / glimpsed something)
//    Patrol     -> Hunting     sees player and A >= 0.7
//    Investigate -> Hunting     A >= 0.7
//    Investigate -> Lost        looked around, nothing, but still uneasy
//    Investigate -> Patrol      A < 0.1, or looked around and calm
//    Hunting    -> Attack      sees player within attack range
//    Hunting    -> Hiding      player LOOKS AT me from far away (hide cooldown ready)
//    Hunting    -> Lost        cannot see player and (A < 0.45 or reached last known spot)
//    Hiding     -> Hunting     player looked away for 1 s / came close / hid too long
//    Hiding     -> Attack      player walked into the ambush
//    Attack     -> Hunting     player out of range
//    Lost       -> Hunting     sees player again, or a loud noise pushes A >= 0.7
//    Lost       -> Patrol      searched all points / timed out (awareness lowered)
//    ANY        -> Hunting     took damage
//    ANY        -> Patrol      player died
//    ANY        -> Dead        health reached 0
// =====================================================================

// ---------------------------------------------------------------------
// PATROL: walk the route (or wander) slowly and quietly.
// ---------------------------------------------------------------------
public class PatrolState : StealthState
{
    public override StealthStateId Id { get { return StealthStateId.Patrol; } }

    bool waiting;
    float waitUntil;

    public override void Enter()
    {
        waiting = false;
        GoToNextPoint();
    }

    void GoToNextPoint()
    {
        Vector3 p;
        if (body.TryGetPatrolPoint(out p)) body.MoveTo(p, StealthSpeed.Walk, 0.3f);
        else body.StopMoving();
    }

    public override StealthStateId Tick(float dt)
    {
        if (bb.CanSeePlayer && bb.Awareness >= t.huntThreshold)
            return brain.Go(StealthStateId.Hunting, "saw the player clearly");
        if (bb.Awareness >= t.investigateThreshold)
            return brain.Go(StealthStateId.Investigate, bb.CanSeePlayer ? "glimpsed something" : "heard a noise");

        if (body.HasArrived())
        {
            if (!waiting) { waiting = true; waitUntil = brain.Clock + t.patrolPause; }
            else if (brain.Clock >= waitUntil) { waiting = false; GoToNextPoint(); }
        }
        return Id;
    }
}

// ---------------------------------------------------------------------
// INVESTIGATE: move carefully to where the noise/glimpse came from,
// then stand there and look left and right.
// ---------------------------------------------------------------------
public class InvestigateState : StealthState
{
    public override StealthStateId Id { get { return StealthStateId.Investigate; } }

    Vector3 point;
    bool lookingAround;
    float lookStart;
    Vector3 lookBaseForward;

    public override void Enter()
    {
        point = bb.HasTarget ? bb.TargetPosition : body.Position;
        lookingAround = false;
        body.MoveTo(point, StealthSpeed.Cautious, 0.5f);
    }

    public override StealthStateId Tick(float dt)
    {
        if (bb.Awareness >= t.huntThreshold)
            return brain.Go(StealthStateId.Hunting, bb.CanSeePlayer ? "suspicion confirmed by sight" : "noise was loud enough to be sure");
        if (bb.Awareness < t.calmThreshold)
            return brain.Go(StealthStateId.Patrol, "calmed down");

        // New information (another noise, or a glimpse) -> re-route to the newer position.
        if ((bb.HeardNoise || bb.CanSeePlayer) && bb.HasTarget && (bb.TargetPosition - point).sqrMagnitude > 1f)
        {
            point = bb.TargetPosition;
            lookingAround = false;
            body.MoveTo(point, StealthSpeed.Cautious, 0.5f);
        }

        if (!lookingAround)
        {
            if (body.HasArrived())
            {
                lookingAround = true;
                lookStart = brain.Clock;
                lookBaseForward = body.Forward;
                body.StopMoving();
            }
        }
        else
        {
            // Sweep the head +-75 degrees like a guard checking a room.
            float angle = Mathf.Sin((brain.Clock - lookStart) * 2f) * 75f;
            body.FaceTowards(body.Position + Quaternion.Euler(0f, angle, 0f) * lookBaseForward);

            if (brain.Clock - lookStart >= t.lookAroundTime)
            {
                if (bb.Awareness >= t.investigateThreshold)
                    return brain.Go(StealthStateId.Lost, "nothing here but still uneasy - searching");
                return brain.Go(StealthStateId.Patrol, "nothing here");
            }
        }
        return Id;
    }
}

// ---------------------------------------------------------------------
// HUNTING: go to where the player is (seen) or probably is (heard).
// Stalker behaviour: creeps up slowly on a player who is standing still
// and facing away, sprints when the player is looking at it, and breaks
// line of sight (Hiding) if spotted from far away.
// ---------------------------------------------------------------------
public class HuntingState : StealthState
{
    public override StealthStateId Id { get { return StealthStateId.Hunting; } }

    public override void Enter()
    {
        if (bb.HasTarget) body.MoveTo(bb.TargetPosition, StealthSpeed.Run, 0.5f);
    }

    public override StealthStateId Tick(float dt)
    {
        if (bb.CanSeePlayer && bb.DistanceToPlayer <= t.attackRange)
            return brain.Go(StealthStateId.Attack, "player in striking range");

        if (bb.CanSeePlayer && bb.PlayerSeesMe && bb.DistanceToPlayer > t.ambushRange && brain.Clock >= brain.HideAllowedAt)
            return brain.Go(StealthStateId.Hiding, "spotted by the player - breaking line of sight");

        if (!bb.CanSeePlayer && bb.Awareness < t.loseThreshold)
            return brain.Go(StealthStateId.Lost, "trail went cold");

        if (!bb.CanSeePlayer && !bb.HasTarget)
            return brain.Go(StealthStateId.Lost, "no idea where the player is");

        Vector3 target = bb.CanSeePlayer ? bb.PlayerPosition : bb.TargetPosition;

        StealthSpeed speed = StealthSpeed.Run;
        if (bb.PlayerSeesMe)
            speed = StealthSpeed.Sprint;                                   // no point being quiet now
        else if (bb.CanSeePlayer && bb.PlayerSpeed < 1.5f && bb.DistanceToPlayer < t.creepRange)
            speed = StealthSpeed.Cautious;                                 // creep up on a still, unaware player

        body.MoveTo(target, speed, bb.CanSeePlayer ? t.attackRange * 0.8f : 0.5f);

        if (!bb.CanSeePlayer && body.HasArrived())
            return brain.Go(StealthStateId.Lost, "reached last known position - player gone");

        return Id;
    }
}

// ---------------------------------------------------------------------
// HIDING: the player has spotted me from far away. Run to the closest
// point the player cannot see, wait until they look away, then come
// back out from a new angle (ambush).
// ---------------------------------------------------------------------
public class HidingState : StealthState
{
    public override StealthStateId Id { get { return StealthStateId.Hiding; } }

    bool hasSpot;
    Vector3 spot;
    float unseenSince;

    public override void Enter()
    {
        hasSpot = body.TryFindHideSpot(out spot);
        if (hasSpot) body.MoveTo(spot, StealthSpeed.Sprint, 0.3f);
        unseenSince = -1f;
    }

    public override void Exit()
    {
        // After coming out of cover it must commit to the hunt for a while
        // (stops an endless hide -> peek -> hide loop).
        brain.HideAllowedAt = brain.Clock + t.hideCooldown;
    }

    public override StealthStateId Tick(float dt)
    {
        if (!hasSpot)
            return brain.Go(StealthStateId.Hunting, "no cover nearby - rushing instead");

        if (bb.CanSeePlayer && bb.DistanceToPlayer <= t.attackRange)
            return brain.Go(StealthStateId.Attack, "player walked into the ambush");

        if ((bb.CanSeePlayer || bb.HeardNoise) && bb.DistanceToPlayer <= t.ambushRange)
            return brain.Go(StealthStateId.Hunting, "player came close - pounce");

        if (timeInState >= t.maxHideTime)
            return brain.Go(StealthStateId.Hunting, "hid long enough");

        if (body.HasArrived())
        {
            if (bb.PlayerSeesMe) unseenSince = -1f;
            else if (unseenSince < 0f) unseenSince = brain.Clock;

            if (unseenSince >= 0f && brain.Clock - unseenSince >= t.unseenBeforeAmbush)
                return brain.Go(StealthStateId.Hunting, "player looked away - coming out");

            if (bb.HasTarget) body.FaceTowards(bb.TargetPosition);       // peek towards the player
        }
        return Id;
    }
}

// ---------------------------------------------------------------------
// ATTACK: strike on a cooldown. A strike while the player is NOT looking
// at me is a sneak attack and does extra damage.
// ---------------------------------------------------------------------
public class AttackState : StealthState
{
    public override StealthStateId Id { get { return StealthStateId.Attack; } }

    public override StealthStateId Tick(float dt)
    {
        // 1.4x range to leave (but 1x to enter) = hysteresis, so it does not flicker at the edge.
        if (bb.DistanceToPlayer > t.attackRange * 1.4f)
            return brain.Go(StealthStateId.Hunting, "player backed off");
        if (!bb.CanSeePlayer && bb.DistanceToPlayer > t.attackRange)
            return brain.Go(StealthStateId.Hunting, "lost contact");

        body.FaceTowards(bb.PlayerPosition);
        body.MoveTo(bb.PlayerPosition, StealthSpeed.Run, t.attackRange * 0.7f);

        if (body.IsAttackReady)
            body.Attack(!bb.PlayerSeesMe);
        return Id;
    }
}

// ---------------------------------------------------------------------
// LOST: go to the last known position, then check a few random points
// around it. New evidence restarts the hunt; nothing found -> patrol.
// ---------------------------------------------------------------------
public class LostState : StealthState
{
    public override StealthStateId Id { get { return StealthStateId.Lost; } }

    Vector3 center;
    int pointsLeft;

    public override void Enter()
    {
        StartSearch(bb.HasTarget ? bb.TargetPosition : body.Position);
    }

    void StartSearch(Vector3 around)
    {
        center = around;
        pointsLeft = t.searchPoints;
        body.MoveTo(center, StealthSpeed.Run, 0.5f);
    }

    public override StealthStateId Tick(float dt)
    {
        // Only NEW evidence restarts the hunt (prevents Hunting <-> Lost ping-pong).
        if (bb.CanSeePlayer && bb.Awareness >= t.investigateThreshold)
            return brain.Go(StealthStateId.Hunting, "found the player again");
        if (bb.HeardNoise && bb.Awareness >= t.huntThreshold)
            return brain.Go(StealthStateId.Hunting, "loud noise while searching");

        if (bb.HeardNoise)
            StartSearch(bb.NoisePosition);                     // quieter noise: search there instead

        if (timeInState >= t.maxSearchTime)
        {
            brain.CalmDown(t.calmThreshold * 0.5f);
            return brain.Go(StealthStateId.Patrol, "search timed out");
        }

        if (body.HasArrived())
        {
            if (pointsLeft <= 0)
            {
                brain.CalmDown(t.investigateThreshold * 0.5f);
                return brain.Go(StealthStateId.Patrol, "searched the area - nothing found");
            }
            Vector3 p;
            if (body.TryGetSearchPoint(center, t.searchRadius, out p))
                body.MoveTo(p, StealthSpeed.Cautious, 0.4f);
            pointsLeft--;
        }
        return Id;
    }
}

// ---------------------------------------------------------------------
// DEAD: do nothing. (The controller handles the body.)
// ---------------------------------------------------------------------
public class DeadState : StealthState
{
    public override StealthStateId Id { get { return StealthStateId.Dead; } }
    public override void Enter() { body.StopMoving(); }
    public override StealthStateId Tick(float dt) { return Id; }
}
