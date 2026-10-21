using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.AI;

// =====================================================================
//  StealthEnemyController  -  [IS] Stealth Enemy (Peiris)
//  The BODY of the Stealth Enemy. Connects the pure-logic StealthBrain
//  to Unity: NavMeshAgent movement, attacks, hide-spot search, logging,
//  debug overlay and gizmos.
//
//  Every frame:  1. Perception.Sense()  -> fills the blackboard
//                2. Brain.Tick()        -> FSM picks/runs a state
//                3. The state calls IStealthBody methods (implemented here)
//
//  Setup: put this on the enemy (a Capsule is fine for testing).
//  It adds NavMeshAgent, StealthPerception and StealthHealth for you.
// =====================================================================
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(StealthPerception))]
[RequireComponent(typeof(StealthHealth))]
public class StealthEnemyController : MonoBehaviour, IStealthBody
{
    [Header("Behaviour tuning (FSM thresholds and timers)")]
    public StealthTuning tuning = new StealthTuning();

    [Header("Patrol")]
    [Tooltip("Optional. Empty = wander randomly around the spawn point.")]
    public Transform[] patrolPoints;
    public float wanderRadius = 12f;

    [Header("Bunker markers (placed by the World Builder)")]
    [Tooltip("Use the PatrolPoint / HidePoint markers of the zone below when no patrol points are set.")]
    public bool useBunkerMarkers = true;
    [Tooltip("Zone name on the BunkerAgentMarker (the Acoustic room is built for this agent). Empty = any zone.")]
    public string markerZone = "Acoustic";
    [Tooltip("Only markers this close to the spawn point are used.")]
    public float markerSearchRadius = 40f;

    [Header("Speeds (m/s)")]
    public float walkSpeed = 1.6f;
    public float cautiousSpeed = 2.2f;
    public float runSpeed = 4f;
    public float sprintSpeed = 5.5f;

    [Header("Attack")]
    public float attackDamage = 12f;
    [Tooltip("Damage multiplier when the player is not looking at the enemy.")]
    public float sneakAttackMultiplier = 2f;
    public float attackCooldown = 1.2f;

    [Header("Hiding")]
    public float hideSearchRadius = 10f;
    public int hideSamples = 16;
    [Tooltip("Hide spots closer than this to the player are rejected (too obvious).")]
    public float minHideDistanceFromPlayer = 4f;

    [Header("Stuck recovery")]
    public float stuckCheckInterval = 1f;
    public float stuckMoveThreshold = 0.15f;

    [Header("Death")]
    public float destroyDelay = 4f;

    [Header("Debug")]
    public bool showOverlay = true;
    [Tooltip("Colours the placeholder capsule by state. Turn OFF once the custom model (with its own eye glow) is used.")]
    public bool tintRendererByState = true;
    public bool logToCsv = true;

    // ---- events for other systems (animation, spawn director, visuals) ----
    public static event Action<StealthEnemyController> AnyDied;          // Spawn Director can count kills
    public event Action<StealthStateId, StealthStateId> StateChanged;      // Agent Controller / visuals
    public event Action<bool> Attacked;                                    // bool = sneak attack

    public StealthStateId State { get { return brain != null ? brain.CurrentId : StealthStateId.Patrol; } }
    public float Awareness { get { return brain != null ? brain.Board.Awareness : 0f; } }
    public StealthSpeed CurrentSpeedMode { get; private set; }
    public NavMeshAgent Agent { get { return agent; } }

    // ---- components ----
    NavMeshAgent agent;
    StealthPerception perception;
    StealthHealth health;
    StealthBrain brain;
    Renderer rend;
    MaterialPropertyBlock mpb;

    // ---- movement cache (avoid recalculating the path every frame) ----
    Vector3 lastDestination;
    bool hasDestination;
    Vector3 home;
    int patrolIndex;

    // ---- attack / stuck ----
    float nextAttackTime;
    Vector3 lastStuckCheckPos;
    float nextStuckCheck;
    int stuckCount;
    int stuckRecoveries;

    // ---- designed hide spots from the level ----
    readonly List<Vector3> hidePoints = new List<Vector3>();
    int markerHidesUsed;

    // ---- debug ----
    Vector3 lastHideSpot;
    bool hasHideSpot;
    static StealthEnemyController overlayOwner;     // only one enemy draws the overlay
    GUIStyle style;

    // ---- logging ----
    string sessionId;
    readonly StringBuilder log = new StringBuilder("time,event,state,awareness,distance,canSee,playerSeesMe,noise,reason\n");
    bool logDirty;
    int attacks, sneakAttacks;
    int stateChanges, noisesHeard;
    readonly float[] timeInState = new float[7];
    bool summarySaved;

    // =================================================================
    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        perception = GetComponent<StealthPerception>();
        health = GetComponent<StealthHealth>();
        rend = GetComponentInChildren<Renderer>();
        mpb = new MaterialPropertyBlock();

        brain = new StealthBrain(this, tuning);
        brain.StateChanged += OnBrainStateChanged;
        health.Damaged += OnDamaged;
        health.Died += OnDied;

        sessionId = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
    }

    void Start()
    {
        home = transform.position;
        lastStuckCheckPos = transform.position;
        if (useBunkerMarkers) CollectBunkerMarkers();
        EnsureOnNavMesh();
        ApplyTint(StealthStateId.Patrol);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F10)) SaveLog();

        // Senses first, then decide.
        perception.Sense(brain.Board, Time.deltaTime);
        timeInState[(int)brain.CurrentId] += Time.deltaTime;
        if (brain.Board.HeardNoise) noisesHeard++;

        if (brain.CurrentId == StealthStateId.Dead) return;
        if (!agent.enabled || !agent.isOnNavMesh) { EnsureOnNavMesh(); return; }   // never command an off-mesh agent

        CheckStuck();
        brain.Tick(Time.deltaTime);
    }

    // The World Builder's BunkerAgentMarker objects (PatrolPoint, HidePoint) give this agent a
    // designed patrol route and designed hiding places in its home room.
    void CollectBunkerMarkers()
    {
        var markers = FindObjectsByType<BunkerAgentMarker>(FindObjectsSortMode.None);
        var patrol = new List<BunkerAgentMarker>();
        foreach (BunkerAgentMarker m in markers)
        {
            if (m == null) continue;
            if (!string.IsNullOrEmpty(markerZone) && m.zone != markerZone) continue;
            if ((m.transform.position - home).sqrMagnitude > markerSearchRadius * markerSearchRadius) continue;
            if (m.kind == BunkerAgentMarker.Kind.PatrolPoint) patrol.Add(m);
            else if (m.kind == BunkerAgentMarker.Kind.HidePoint) hidePoints.Add(m.transform.position);
        }
        if ((patrolPoints == null || patrolPoints.Length == 0) && patrol.Count > 0)
        {
            patrol.Sort((a, b) => string.CompareOrdinal(a.name, b.name));    // stable, predictable route order
            patrolPoints = new Transform[patrol.Count];
            for (int i = 0; i < patrol.Count; i++) patrolPoints[i] = patrol[i].transform;
        }
        Debug.Log("[Stealth] " + name + " using " + patrol.Count + " patrol markers and " + hidePoints.Count +
                  " hide markers from zone '" + markerZone + "'");
    }

    // Spawned slightly above/off the floor? Snap onto the closest NavMesh point.
    void EnsureOnNavMesh()
    {
        if (!agent.enabled || agent.isOnNavMesh) return;
        NavMeshHit hit;
        if (NavMesh.SamplePosition(transform.position, out hit, 3f, NavMesh.AllAreas))
            agent.Warp(hit.position);
    }

    // =================================================================
    //  IStealthBody - what the brain can ask the body to do
    // =================================================================
    public Vector3 Position { get { return transform.position; } }
    public Vector3 Forward { get { return transform.forward; } }

    public void MoveTo(Vector3 target, StealthSpeed speed, float stopDistance)
    {
        if (!agent.isOnNavMesh) return;

        agent.speed = SpeedFor(speed);
        agent.stoppingDistance = stopDistance;
        agent.isStopped = false;
        CurrentSpeedMode = speed;

        // Only recalculate the path if the target moved more than 0.5 m (pathfinding is not free).
        if (hasDestination && (target - lastDestination).sqrMagnitude < 0.25f) return;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(target, out hit, 2f, NavMesh.AllAreas) ||
            NavMesh.SamplePosition(target, out hit, 6f, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
            lastDestination = target;
            hasDestination = true;
        }
    }

    public void StopMoving()
    {
        hasDestination = false;
        if (agent.isOnNavMesh)
        {
            agent.ResetPath();
            agent.velocity = Vector3.zero;
        }
    }

    public bool HasArrived()
    {
        if (!agent.isOnNavMesh) return true;
        if (agent.pathPending) return false;
        if (!agent.hasPath) return true;
        return agent.remainingDistance <= agent.stoppingDistance + 0.25f;
    }

    public void FaceTowards(Vector3 worldPoint)
    {
        // While walking, the NavMeshAgent turns the body itself.
        if (agent.velocity.sqrMagnitude > 0.05f) return;
        Vector3 dir = worldPoint - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        Quaternion want = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, want, agent.angularSpeed * Time.deltaTime);
    }

    public bool TryGetPatrolPoint(out Vector3 point)
    {
        // Fixed route if the World Builder placed patrol points...
        if (patrolPoints != null && patrolPoints.Length > 0)
        {
            for (int tries = 0; tries < patrolPoints.Length; tries++)
            {
                Transform p = patrolPoints[patrolIndex];
                patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
                if (p != null) { point = p.position; return true; }
            }
        }
        // ...otherwise wander around where it spawned (works for Spawn Director spawns too).
        return TryGetSearchPoint(home, wanderRadius, out point);
    }

    public bool TryGetSearchPoint(Vector3 around, float radius, out Vector3 point)
    {
        for (int i = 0; i < 10; i++)
        {
            Vector2 c = UnityEngine.Random.insideUnitCircle * radius;
            NavMeshHit hit;
            if (NavMesh.SamplePosition(around + new Vector3(c.x, 0f, c.y), out hit, 2f, NavMesh.AllAreas))
            {
                point = hit.position;
                return true;
            }
        }
        point = transform.position;
        return false;
    }

    // Sample points in a ring around me; keep the ones the player CANNOT see;
    // pick the cheapest: close to me, not right next to the player.
    public bool TryFindHideSpot(out Vector3 spot)
    {
        spot = transform.position;
        hasHideSpot = false;
        if (perception.Player == null) return false;

        Vector3 playerEye = perception.PlayerEyePosition;
        float bestCost = float.MaxValue;
        bool bestIsMarker = false;

        // 1) Designed hide points from the level get first choice (3 m bonus) if the player cannot see them.
        foreach (Vector3 hp in hidePoints)
        {
            NavMeshHit mh;
            if (!NavMesh.SamplePosition(hp, out mh, 1.5f, NavMesh.AllAreas)) continue;
            float dMe = Vector3.Distance(transform.position, mh.position);
            if (dMe > hideSearchRadius * 2f) continue;
            if (Vector3.Distance(mh.position, perception.Player.position) < minHideDistanceFromPlayer) continue;
            if (!perception.IsLineBlocked(playerEye, mh.position + Vector3.up * perception.eyeHeight)) continue;
            float c = dMe - 3f;
            if (c < bestCost) { bestCost = c; spot = mh.position; bestIsMarker = true; }
        }

        // 2) Otherwise sample points in rings around me.

        for (int i = 0; i < hideSamples; i++)
        {
            float angle = i * (360f / hideSamples);
            float r = Mathf.Lerp(3f, hideSearchRadius, (i % 3) / 2f);          // 3 rings: near, mid, far
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            NavMeshHit hit;
            if (!NavMesh.SamplePosition(transform.position + dir * r, out hit, 1.5f, NavMesh.AllAreas)) continue;

            Vector3 candidate = hit.position;
            float toPlayer = Vector3.Distance(candidate, perception.Player.position);
            if (toPlayer < minHideDistanceFromPlayer) continue;
            if (!perception.IsLineBlocked(playerEye, candidate + Vector3.up * perception.eyeHeight)) continue; // player could see it

            float cost = Vector3.Distance(transform.position, candidate)        // short run
                       + Mathf.Max(0f, toPlayer - 12f) * 0.5f;                  // but stay close enough to ambush
            if (cost < bestCost) { bestCost = cost; spot = candidate; bestIsMarker = false; }
        }

        hasHideSpot = bestCost < float.MaxValue;
        lastHideSpot = spot;
        if (hasHideSpot && bestIsMarker) { markerHidesUsed++; AppendRow("HIDE_SPOT", "designed HidePoint marker"); }
        return hasHideSpot;
    }

    public bool IsAttackReady { get { return Time.time >= nextAttackTime; } }

    public void Attack(bool sneakAttack)
    {
        if (!IsAttackReady || perception.PlayerStats == null) return;
        nextAttackTime = Time.time + attackCooldown;

        float dist = Vector3.Distance(transform.position, perception.Player.position);
        if (dist > tuning.attackRange * 1.2f) return;                         // safety: never hit from afar

        float dmg = sneakAttack ? attackDamage * sneakAttackMultiplier : attackDamage;
        perception.PlayerStats.TakeDamage(dmg);

        attacks++;
        if (sneakAttack) sneakAttacks++;
        AppendRow(sneakAttack ? "SNEAK_ATTACK" : "ATTACK", "dmg " + dmg.ToString("F0", CultureInfo.InvariantCulture));
        if (Attacked != null) Attacked(sneakAttack);
    }

    float SpeedFor(StealthSpeed s)
    {
        switch (s)
        {
            case StealthSpeed.Walk: return walkSpeed;
            case StealthSpeed.Cautious: return cautiousSpeed;
            case StealthSpeed.Run: return runSpeed;
            default: return sprintSpeed;
        }
    }

    // =================================================================
    //  Events from the brain and health
    // =================================================================
    void OnBrainStateChanged(StealthStateId from, StealthStateId to, string reason)
    {
        hasDestination = false;                                  // new state -> allow a fresh path
        stateChanges++;
        AppendRow(from + "->" + to, reason);
        Debug.Log("[Stealth] " + name + ": " + from + " -> " + to + " (" + reason + ")  awareness=" +
                  brain.Board.Awareness.ToString("F2", CultureInfo.InvariantCulture));
        ApplyTint(to);
        if (StateChanged != null) StateChanged(from, to);
    }

    void OnDamaged(float amount)
    {
        brain.Board.WasDamaged = true;
        if (perception.Player != null)
        {
            brain.Board.TargetPosition = perception.Player.position;   // "the shot came from over there"
            brain.Board.HasTarget = true;
        }
        AppendRow("DAMAGED", "hp " + health.health.ToString("F0", CultureInfo.InvariantCulture));
    }

    void OnDied()
    {
        brain.Board.IsDead = true;
        brain.Tick(0f);                                          // enter Dead state right now
        perception.SetDeaf(true);
        if (agent.isOnNavMesh) { agent.isStopped = true; agent.ResetPath(); }
        agent.enabled = false;
        if (overlayOwner == this) overlayOwner = null;
        SaveLog();
        SaveSummary("KILLED");
        if (AnyDied != null) AnyDied(this);
        Destroy(gameObject, destroyDelay);
    }

    // =================================================================
    //  Stuck detection: if we should be moving but are not, nudge.
    // =================================================================
    void CheckStuck()
    {
        if (Time.time < nextStuckCheck) return;
        nextStuckCheck = Time.time + stuckCheckInterval;

        float moved = (transform.position - lastStuckCheckPos).magnitude;
        lastStuckCheckPos = transform.position;

        bool shouldMove = agent.hasPath && !agent.pathPending && !agent.isStopped
                          && agent.pathStatus == NavMeshPathStatus.PathComplete
                          && agent.remainingDistance > agent.stoppingDistance + 0.5f;

        stuckCount = (shouldMove && moved < stuckMoveThreshold) ? stuckCount + 1 : 0;
        if (stuckCount < 2) return;

        stuckCount = 0;
        stuckRecoveries++;
        AppendRow("STUCK_RECOVERY", "nudging");
        Vector3 p;
        if (TryGetSearchPoint(transform.position, 3f, out p)) agent.SetDestination(p);
        hasDestination = false;                                  // the state will re-issue its real target
    }

    // =================================================================
    //  Visual feedback for testing (placeholder capsule)
    // =================================================================
    public static Color ColorFor(StealthStateId s)
    {
        switch (s)
        {
            case StealthStateId.Patrol: return new Color(0.2f, 0.8f, 0.3f);
            case StealthStateId.Investigate: return new Color(1f, 0.85f, 0.1f);
            case StealthStateId.Hunting: return new Color(1f, 0.45f, 0f);
            case StealthStateId.Hiding: return new Color(0.2f, 0.4f, 1f);
            case StealthStateId.Attack: return Color.red;
            case StealthStateId.Lost: return new Color(0.6f, 0.2f, 0.9f);
            default: return Color.gray;
        }
    }

    void ApplyTint(StealthStateId s)
    {
        if (!tintRendererByState || rend == null) return;
        // MaterialPropertyBlock: changes colour WITHOUT creating a new material copy per enemy.
        rend.GetPropertyBlock(mpb);
        mpb.SetColor("_BaseColor", ColorFor(s));     // URP Lit
        mpb.SetColor("_Color", ColorFor(s));         // Built-in / fallback
        rend.SetPropertyBlock(mpb);
    }

    // =================================================================
    //  Logging (CSV for the IS report) -> Application.persistentDataPath
    // =================================================================
    static string F(float v) { return v.ToString("F2", CultureInfo.InvariantCulture); }

    void AppendRow(string evt, string reason)
    {
        if (!logToCsv || brain == null) return;
        StealthBlackboard b = brain.Board;
        log.Append(F(Time.timeSinceLevelLoad)).Append(',')
           .Append(evt).Append(',')
           .Append(brain.CurrentId).Append(',')
           .Append(F(b.Awareness)).Append(',')
           .Append(b.DistanceToPlayer < 9999f ? F(b.DistanceToPlayer) : "-").Append(',')
           .Append(b.CanSeePlayer ? 1 : 0).Append(',')
           .Append(b.PlayerSeesMe ? 1 : 0).Append(',')
           .Append(F(perception.LastNoiseStrength)).Append(',')
           .Append((reason ?? "").Replace(',', ';')).Append('\n');
        logDirty = true;
    }

    void SaveLog()
    {
        if (!logToCsv || !logDirty) return;
        try
        {
            string file = "stealth_log_" + sessionId + "_" + GetEntityId().ToString() + ".csv";
            string path = Path.Combine(Application.persistentDataPath, file);
            File.WriteAllText(path, log.ToString());
            logDirty = false;
            Debug.Log("[Stealth] log saved to " + path + "  (attacks " + attacks + ", sneak " + sneakAttacks +
                      ", stuck recoveries " + stuckRecoveries + ")");
        }
        catch (Exception e) { Debug.LogWarning("[Stealth] could not save log: " + e.Message); }
    }

    // One row per enemy per play session in stealth_sessions.csv: the summary table for the IS report.
    void SaveSummary(string result)
    {
        if (!logToCsv || summarySaved || brain == null) return;
        summarySaved = true;
        try
        {
            string path = Path.Combine(Application.persistentDataPath, "stealth_sessions.csv");
            var sb = new StringBuilder();
            if (!File.Exists(path))
                sb.Append("session,enemy,result,duration_s,t_patrol,t_investigate,t_hunting,t_hiding,t_attack,t_lost," +
                          "state_changes,noises_heard,attacks,sneak_attacks,marker_hides,stuck_recoveries\n");
            sb.Append(sessionId).Append(',').Append(GetEntityId().ToString()).Append(',').Append(result).Append(',')
              .Append(F(Time.timeSinceLevelLoad));
            for (int i = 0; i < 6; i++) sb.Append(',').Append(F(timeInState[i]));
            sb.Append(',').Append(stateChanges).Append(',').Append(noisesHeard).Append(',').Append(attacks)
              .Append(',').Append(sneakAttacks).Append(',').Append(markerHidesUsed).Append(',').Append(stuckRecoveries).Append('\n');
            File.AppendAllText(path, sb.ToString());
            Debug.Log("[Stealth] session summary added to " + path);
        }
        catch (Exception e) { Debug.LogWarning("[Stealth] could not save summary: " + e.Message); }
    }

    void OnDestroy()
    {
        SaveLog();
        SaveSummary(perception != null && perception.PlayerStats != null && perception.PlayerStats.IsDead ? "PLAYER_DEAD" : "QUIT");
        if (overlayOwner == this) overlayOwner = null;
    }

    // =================================================================
    //  Debug overlay (top-right) and gizmos (Scene view)
    // =================================================================
    void OnGUI()
    {
        if (!showOverlay || brain == null || brain.CurrentId == StealthStateId.Dead) return;
        if (overlayOwner == null) overlayOwner = this;
        if (overlayOwner != this) return;

        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            style.normal.textColor = Color.white;
        }

        StealthBlackboard b = brain.Board;
        float w = 420f, lh = 24f, x = Screen.width - w - 10f, y = 10f;

        GUI.color = new Color(0f, 0f, 0f, 0.7f);
        GUI.DrawTexture(new Rect(x, y, w, lh * 7 + 16f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        float ly = y + 6f;
        GUI.Label(new Rect(x + 10, ly, w, lh), "Stealth: " + brain.CurrentId + "  (" + CurrentSpeedMode + ")", style); ly += lh;

        // awareness bar with threshold marks
        Rect bar = new Rect(x + 10, ly + 4, w - 20, lh - 8);
        GUI.color = new Color(0.25f, 0.25f, 0.25f);
        GUI.DrawTexture(bar, Texture2D.whiteTexture);
        GUI.color = ColorFor(brain.CurrentId);
        GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * b.Awareness, bar.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
        DrawMark(bar, tuning.investigateThreshold);
        DrawMark(bar, tuning.huntThreshold);
        ly += lh;

        GUI.Label(new Rect(x + 10, ly, w, lh), "Awareness " + b.Awareness.ToString("F2") + "   HP " + Mathf.CeilToInt(health.health), style); ly += lh;
        GUI.Label(new Rect(x + 10, ly, w, lh), "Sees player: " + (b.CanSeePlayer ? "YES" : "no") +
                                              "   Spotted: " + (b.PlayerSeesMe ? "YES" : "no"), style); ly += lh;
        float ago = Time.time - perception.LastNoiseTime;
        string noise = ago < 5f
            ? perception.LastNoiseSource + " " + perception.LastNoiseStrength.ToString("F2") +
              (perception.LastNoiseUsedPath ? " (around walls)" : " (through wall/direct)") + (perception.LastNoiseMasked ? " MASKED" : "")
            : "-";
        GUI.Label(new Rect(x + 10, ly, w, lh), "Last noise: " + noise, style); ly += lh;
        GUI.Label(new Rect(x + 10, ly, w, lh * 2), "Why: " + brain.LastReason, style);
    }

    static void DrawMark(Rect bar, float t)
    {
        GUI.DrawTexture(new Rect(bar.x + bar.width * t - 1f, bar.y - 3f, 2f, bar.height + 6f), Texture2D.whiteTexture);
    }

    void OnDrawGizmosSelected()
    {
        StealthPerception p = perception != null ? perception : GetComponent<StealthPerception>();
        if (p == null) return;
        Vector3 eye = transform.position + Vector3.up * p.eyeHeight;

        // view cone
        Gizmos.color = Color.yellow;
        Vector3 left = Quaternion.Euler(0f, -p.viewAngle * 0.5f, 0f) * transform.forward;
        Vector3 right = Quaternion.Euler(0f, p.viewAngle * 0.5f, 0f) * transform.forward;
        Gizmos.DrawLine(eye, eye + left * p.viewDistance);
        Gizmos.DrawLine(eye, eye + right * p.viewDistance);
        Gizmos.DrawLine(eye + left * p.viewDistance, eye + transform.forward * p.viewDistance);
        Gizmos.DrawLine(eye + right * p.viewDistance, eye + transform.forward * p.viewDistance);

        // reference hearing distance (half loudness)
        Gizmos.color = new Color(0f, 1f, 1f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, p.referenceDistance);

        if (!Application.isPlaying || brain == null) return;

        if (brain.Board.HasTarget)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f);
            Gizmos.DrawLine(eye, brain.Board.TargetPosition);
            Gizmos.DrawWireSphere(brain.Board.TargetPosition, 0.4f);
        }
        if (Time.time - p.LastNoiseTime < 3f)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(p.LastNoisePos, 0.25f + p.LastNoiseStrength);
        }
        if (hasHideSpot)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(lastHideSpot + Vector3.up * 0.5f, Vector3.one);
        }
    }
}
