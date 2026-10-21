using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.AI;

// Connects BossBrain (decisions) to the game world (NavMesh movement, damage, logging).
public class BossController : MonoBehaviour{
    [Header("References")]
    public Transform player;
    public PlayerStats playerStats;
    public BossHealth bossHealthComp;
    public Transform[] coverPoints;

    [Header("Movement")]
    public float moveSpeed = 3f;

    [Header("Combat")]
    public float attackRange = 2f;
    public float attackDamagePerSecond = 10f;

    [Header("Cover (line-of-sight check)")]
    [Tooltip("Height of the player's eyes above the player's pivot (camera is at 0.6 in this project).")]
    public float playerEyeHeight = 0.6f;
    [Tooltip("Height above a cover point's ground position used for the hidden check.")]
    public float coverCheckHeight = 1f;
    public float coverRefreshInterval = 0.25f;

    [Header("Stuck recovery")]
    public float stuckCheckInterval = 1f;
    public float stuckMoveThreshold = 0.15f;
    public int stuckChecksBeforeRecovery = 2;
    public float recoveryDuration = 1f;

    [Header("Debug overlay")]
    public int overlayFontSize = 24;
    GUIStyle overlayStyle;

    bool showOverlay = true;   // F4 hides/shows the boss overlay

    BossBrain brain = new BossBrain();
    Renderer rend;
    NavMeshAgent agent;

    // ---- cover state ----
    readonly RaycastHit[] losHits = new RaycastHit[16];
    Transform currentCover;
    bool coverAvailable;     // at least one cover point is hidden from the player
    bool bossHidden;         // the boss itself is currently hidden from the player
    float nextCoverRefresh;

    // ---- flank / retreat helpers ----
    float flankSide = 1f;
    static readonly float[] retreatDistances = { 6f, 4f, 2f };

    // ---- stuck recovery ----
    Vector3 lastCheckPos;
    float nextStuckCheck;
    int stuckCount;
    float recoverUntil;
    int stuckRecoveries;
    float lastDistance;
    float elapsed;           // fight time, kept so the summary is right even when Play mode is stopped

    // ---- logging (CSV files go to Application.persistentDataPath) ----
    string sessionId;
    StringBuilder log = new StringBuilder("time,event,weapon,playerHP,bossHP,distance,attack,retreat,flank,cover,coverAvail\n");
    bool hasLogged, finished, summarySaved;
    BossAction lastLogged;
    readonly float[] timeInAction = new float[4];
    int actionSwitches, weaponSwitches;
    bool hasWeapon;
    WeaponType lastWeapon;

    void Start(){
        rend = GetComponent<Renderer>();
        agent = GetComponent<NavMeshAgent>();
        agent.speed = moveSpeed;
        agent.stoppingDistance = 1.5f;
        lastCheckPos = transform.position;
        sessionId = System.DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
    }

    void Update(){
        elapsed = Time.timeSinceLevelLoad;
        if (Input.GetKeyDown(KeyCode.F9)) SaveLog();

        if (Input.GetKeyDown(KeyCode.F4)) showOverlay = !showOverlay;

        // ---- End states ----
        if (bossHealthComp.IsDead){ Finish("BOSS_DEAD", Color.gray); return; }
        if (playerStats.IsDead){ Finish("PLAYER_DEAD", rend.material.color); return; }
        if (!agent.isOnNavMesh) return;     // safety: never command an agent that is off the NavMesh

        // ---- Perception ----
        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        float distance = toPlayer.magnitude;
        Vector3 dir = toPlayer.normalized;
        lastDistance = distance;

        TrackPlayerWeapon(distance);
        RefreshCover();

        // ---- Decision ----
        BossAction action = brain.Evaluate(playerStats.weapon == WeaponType.Ranged,
                                           playerStats.HealthFraction,
                                           bossHealthComp.Fraction,
                                           distance, Time.deltaTime,
                                           coverAvailable ? 1f : 0f);
        timeInAction[(int)action] += Time.deltaTime;
        LogIfChanged(action, distance);

        // ---- Safety net: detect and fix a boss that is stuck ----
        CheckStuck(action, distance);
        if (Time.time < recoverUntil) return;   // let the recovery move finish

        // ---- Act ----
        switch (action){
            case BossAction.Attack:
                GoTo(player.position);
                rend.material.color = Color.red;
                if (distance < attackRange) playerStats.TakeDamage(attackDamagePerSecond * Time.deltaTime);
                break;

            case BossAction.Retreat:
                RetreatFrom(dir);
                rend.material.color = Color.yellow;
                break;

            case BossAction.Flank:
                Vector3 sideways = Vector3.Cross(Vector3.up, dir);
                if (!GoTo(player.position + sideways * flankSide * 8f)){
                    flankSide = -flankSide;                                    // that side is blocked, try the other
                    GoTo(player.position + sideways * flankSide * 8f);
                }
                rend.material.color = Color.magenta;
                break;

            case BossAction.TakeCover:
                if (currentCover != null) GoTo(currentCover.position);
                else RetreatFrom(dir);                                         // nowhere to hide: back away instead
                rend.material.color = Color.blue;
                break;
        }
    }

    // =====================================================================
    //  Movement helpers
    // =====================================================================

    // Send the agent to the closest valid NavMesh point. Returns false if there is none nearby.
    bool GoTo(Vector3 target, float sampleRadius = 3f){
        if (NavMesh.SamplePosition(target, out NavMeshHit hit, sampleRadius, NavMesh.AllAreas)){
            agent.SetDestination(hit.position);
            return true;
        }
        return false;
    }

    // Back away from the player. If a wall or the arena edge is in the way, try shorter
    // distances, then slide sideways so the boss never freezes against a wall.
    void RetreatFrom(Vector3 dirToPlayer){
        foreach (float d in retreatDistances)
            if (GoTo(transform.position - dirToPlayer * d, 1.5f)) return;
        GoTo(transform.position + Vector3.Cross(Vector3.up, dirToPlayer) * flankSide * 4f, 1.5f);
    }

    // =====================================================================
    //  Cover: choose a cover point that is really hidden from the player
    // =====================================================================

    void RefreshCover(){
        if (Time.time < nextCoverRefresh) return;
        nextCoverRefresh = Time.time + coverRefreshInterval;

        bossHidden = IsHiddenFromPlayer(transform.position);

        // Keep the current cover while it still hides us (stops the boss changing its mind every frame)
        if (currentCover != null && IsHiddenFromPlayer(currentCover.position + Vector3.up * coverCheckHeight)){
            coverAvailable = true;
            return;
        }
        currentCover = FindHiddenCover();
        coverAvailable = currentCover != null;
    }

    Transform FindHiddenCover(){
        Transform best = null;
        float bestSqr = float.MaxValue;
        foreach (Transform c in coverPoints){
            if (c == null) continue;
            if (!IsHiddenFromPlayer(c.position + Vector3.up * coverCheckHeight)) continue;
            float sqr = (c.position - transform.position).sqrMagnitude;
            if (sqr < bestSqr){ bestSqr = sqr; best = c; }
        }
        return best;
    }

    // True if something solid (wall, barricade, door...) blocks the line from the player's eyes to 'target'.
    bool IsHiddenFromPlayer(Vector3 target){
        Vector3 from = player.position + Vector3.up * playerEyeHeight;
        Vector3 delta = target - from;
        float len = delta.magnitude;
        if (len < 0.01f) return false;

        int n = Physics.RaycastNonAlloc(from, delta / len, losHits, len, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++){
            Transform t = losHits[i].collider.transform;
            if (t.IsChildOf(transform) || t.IsChildOf(player)) continue;   // ignore the boss and the player
            if (t.GetComponentInParent<Throwable>() != null) continue;     // ignore flying rocks
            return true;
        }
        return false;
    }

    // =====================================================================
    //  Stuck detection
    // =====================================================================

    void CheckStuck(BossAction action, float distance){
        if (Time.time < nextStuckCheck) return;
        nextStuckCheck = Time.time + stuckCheckInterval;

        float moved = (transform.position - lastCheckPos).magnitude;
        lastCheckPos = transform.position;

        bool attackingInRange = action == BossAction.Attack && distance < attackRange;
        bool partialPath = agent.pathStatus == NavMeshPathStatus.PathPartial;   // target unreachable (e.g. closed door): waiting is fine
        bool shouldBeMoving = !agent.pathPending && agent.hasPath && !attackingInRange && !partialPath
                              && agent.remainingDistance > agent.stoppingDistance + 0.5f;

        if (shouldBeMoving && moved < stuckMoveThreshold) stuckCount++;
        else stuckCount = 0;

        if (stuckCount >= stuckChecksBeforeRecovery){
            stuckCount = 0;
            Recover();
        }
    }

    // Wiggle free: drop the old path and walk to a random nearby NavMesh point.
    void Recover(){
        stuckRecoveries++;
        AppendRow("STUCK_RECOVERY", brain.Current, lastDistance);
        agent.ResetPath();
        for (int i = 0; i < 8; i++){
            Vector2 c = Random.insideUnitCircle * 6f;
            Vector3 p = transform.position + new Vector3(c.x, 0f, c.y);
            if (NavMesh.SamplePosition(p, out NavMeshHit hit, 2f, NavMesh.AllAreas)){
                agent.SetDestination(hit.position);
                break;
            }
        }
        recoverUntil = Time.time + recoveryDuration;
    }

    // =====================================================================
    //  End of fight
    // =====================================================================

    void Finish(string label, Color colour){
        if (finished) return;
        finished = true;
        if (agent.isOnNavMesh){
            agent.isStopped = true;
            agent.ResetPath();
        }
        rend.material.color = colour;
        AppendRow(label, brain.Current, 0f);
        SaveLog();
        SaveSummary(label);
    }

    // =====================================================================
    //  Logging (decision log for the IS report)
    // =====================================================================

    static string F(float v){ return v.ToString("F2", CultureInfo.InvariantCulture); }

    void TrackPlayerWeapon(float distance){
        if (!hasWeapon){ lastWeapon = playerStats.weapon; hasWeapon = true; return; }
        if (playerStats.weapon != lastWeapon){
            lastWeapon = playerStats.weapon;
            weaponSwitches++;
            AppendRow("PLAYER_SWITCH->" + lastWeapon, brain.Current, distance);
        }
    }

    void LogIfChanged(BossAction action, float distance){
        if (hasLogged && action == lastLogged) return;
        if (hasLogged) actionSwitches++;
        if (action == BossAction.Flank) flankSide = Random.value < 0.5f ? -1f : 1f;   // vary the side each time
        hasLogged = true;
        lastLogged = action;
        AppendRow("->" + action, action, distance);
        Debug.Log("[Boss] switched to " + action + " | weapon=" + playerStats.weapon +
                  " playerHP=" + F(playerStats.health) + " bossHP=" + F(bossHealthComp.health));
    }

    void AppendRow(string label, BossAction action, float distance){
        log.Append(F(Time.timeSinceLevelLoad)).Append(',')
           .Append(label).Append(',')
           .Append(playerStats.weapon).Append(',')
           .Append(F(playerStats.health)).Append(',')
           .Append(F(bossHealthComp.health)).Append(',')
           .Append(F(distance)).Append(',')
           .Append(F(brain.LastScores[0])).Append(',')
           .Append(F(brain.LastScores[1])).Append(',')
           .Append(F(brain.LastScores[2])).Append(',')
           .Append(F(brain.LastScores[3])).Append(',')
           .Append(coverAvailable ? "1" : "0").Append('\n');
    }

    // One file per play session: boss_log_<time>.csv (every decision)
    void SaveLog(){
        try{
            string path = Path.Combine(Application.persistentDataPath, "boss_log_" + sessionId + ".csv");
            File.WriteAllText(path, log.ToString());
            Debug.Log("[Boss] log saved to " + path);
        } catch (System.Exception e){ Debug.LogWarning("Could not save boss log: " + e.Message); }
    }

    // One row per session appended to boss_sessions.csv (summary table for the report)
    void SaveSummary(string result){
        if (summarySaved) return;
        summarySaved = true;
        try{
            string path = Path.Combine(Application.persistentDataPath, "boss_sessions.csv");
            var sb = new StringBuilder();
            if (!File.Exists(path))
                sb.Append("session,result,duration_s,boss_hp_end,player_hp_end,action_switches,time_attack,time_retreat,time_flank,time_cover,player_weapon_switches,stuck_recoveries\n");
            sb.Append(sessionId).Append(',')
              .Append(result).Append(',')
              .Append(F(elapsed)).Append(',')
              .Append(F(bossHealthComp.health)).Append(',')
              .Append(F(playerStats.health)).Append(',')
              .Append(actionSwitches).Append(',')
              .Append(F(timeInAction[(int)BossAction.Attack])).Append(',')
              .Append(F(timeInAction[(int)BossAction.Retreat])).Append(',')
              .Append(F(timeInAction[(int)BossAction.Flank])).Append(',')
              .Append(F(timeInAction[(int)BossAction.TakeCover])).Append(',')
              .Append(weaponSwitches).Append(',')
              .Append(stuckRecoveries).Append('\n');
            File.AppendAllText(path, sb.ToString());
            Debug.Log("[Boss] session summary saved to " + path);
        } catch (System.Exception e){ Debug.LogWarning("Could not save session summary: " + e.Message); }
    }

    void OnDestroy(){
        if (!finished && hasLogged){
            SaveLog();
            SaveSummary("QUIT");
        }
    }

    // Shows cover points as green spheres in the Scene view
    void OnDrawGizmos(){
        if (coverPoints == null) return;
        Gizmos.color = Color.green;
        foreach (Transform c in coverPoints)
            if (c != null) Gizmos.DrawWireSphere(c.position, 0.7f);
        if (currentCover != null){
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, currentCover.position);
        }
    }

    // Debug overlay: shows all four live scores
    void OnGUI(){
        if (!showOverlay) return;

        if (overlayStyle == null)
        {
            overlayStyle = new GUIStyle(GUI.skin.label);
            overlayStyle.fontStyle = FontStyle.Bold;
            overlayStyle.normal.textColor = Color.white;
        }
        overlayStyle.fontSize = overlayFontSize;

        float lineH = overlayFontSize * 1.4f;
        float boxW = overlayFontSize * 16f;
        float boxH = lineH * 8f + 20f;

        GUI.color = new Color(0f, 0f, 0f, 0.7f);
        GUI.DrawTexture(new Rect(10, 10, boxW, boxH), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Label(new Rect(20, 15, boxW, lineH), "Current: " + brain.Current, overlayStyle);
        for (int i = 0; i < 4; i++){
            GUI.Label(new Rect(20, 15 + lineH * (i + 1), boxW, lineH),
                    ((BossAction)i) + ": " + brain.LastScores[i].ToString("F2"),
                    overlayStyle);
        }
        GUI.Label(new Rect(20, 15 + lineH * 5, boxW, lineH),
                "Boss HP: " + Mathf.CeilToInt(bossHealthComp.health), overlayStyle);
        GUI.Label(new Rect(20, 15 + lineH * 6, boxW, lineH),
                "Player: " + playerStats.weapon, overlayStyle);
        GUI.Label(new Rect(20, 15 + lineH * 7, boxW, lineH),
                "Cover: " + (bossHidden ? "hidden" : "exposed") + (coverAvailable ? " (cover free)" : " (no cover)"),
                overlayStyle);
    }
}