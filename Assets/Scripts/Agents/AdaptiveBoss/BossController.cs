using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.AI;

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

    [Header("Debug overlay")]
    public int overlayFontSize = 24;
    GUIStyle overlayStyle;

    BossBrain brain = new BossBrain();
    Renderer rend;
    NavMeshAgent agent;

    // Decision log for the IS report (CSV written to Application.persistentDataPath)
    StringBuilder log = new StringBuilder("time,event,weapon,playerHP,bossHP,distance,attack,retreat,flank,cover\n");
    bool hasLogged, finished;
    BossAction lastLogged;

    void Start(){
        rend = GetComponent<Renderer>();
        agent = GetComponent<NavMeshAgent>();
        agent.speed = moveSpeed;
        agent.stoppingDistance = 1.5f;
    }

    void Update(){
        if (Input.GetKeyDown(KeyCode.F9)) SaveLog();

        // End states
        if (bossHealthComp.IsDead){ Finish("BOSS_DEAD", Color.gray); return; }
        if (playerStats.IsDead){ Finish("PLAYER_DEAD", rend.material.color); return; }

        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        float distance = toPlayer.magnitude;
        Vector3 dir = toPlayer.normalized;

        BossAction action = brain.Evaluate(playerStats.weapon == WeaponType.Ranged,
                                           playerStats.HealthFraction,
                                           bossHealthComp.Fraction,
                                           distance, Time.deltaTime);
        LogIfChanged(action, distance);

        switch (action){
            case BossAction.Attack:
                GoTo(player.position);
                rend.material.color = Color.red;
                if (distance < attackRange) playerStats.TakeDamage(attackDamagePerSecond * Time.deltaTime);
                break;
            case BossAction.Retreat:
                GoTo(transform.position - dir * 6f);
                rend.material.color = Color.yellow;
                break;
            case BossAction.Flank:
                Vector3 sideways = Vector3.Cross(Vector3.up, dir);
                GoTo(player.position + sideways * 8f);
                rend.material.color = Color.magenta;
                break;
            case BossAction.TakeCover:
                GoTo(NearestCover());
                rend.material.color = Color.blue;
                break;
        }
    }

    void Finish(string label, Color colour){
        if (finished) return;
        finished = true;
        agent.isStopped = true;
        agent.ResetPath();
        rend.material.color = colour;
        AppendRow(label, brain.Current, 0f);
        SaveLog();
    }

    // Send the agent to the closest valid point on the NavMesh
    void GoTo(Vector3 target){
        if (NavMesh.SamplePosition(target, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            agent.SetDestination(hit.position);
    }

    Vector3 NearestCover(){
        Vector3 best = transform.position;
        float bestDist = float.MaxValue;
        foreach (Transform c in coverPoints){
            float d = Vector3.Distance(transform.position, c.position);
            if (d < bestDist){ bestDist = d; best = c.position; }
        }
        return best;
    }

    // ---------- logging ----------
    static string F(float v){ return v.ToString("F2", CultureInfo.InvariantCulture); }

    void LogIfChanged(BossAction action, float distance){
        if (hasLogged && action == lastLogged) return;
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
           .Append(F(brain.LastScores[3])).Append('\n');
    }

    void SaveLog(){
        try{
            string path = Path.Combine(Application.persistentDataPath, "boss_log.csv");
            File.WriteAllText(path, log.ToString());
            Debug.Log("[Boss] log saved to " + path);
        } catch (System.Exception e){ Debug.LogWarning("Could not save boss log: " + e.Message); }
    }

    void OnDestroy(){ if (!finished && hasLogged) SaveLog(); }

    // Shows cover points as green spheres in the Scene view
    void OnDrawGizmos(){
        if (coverPoints == null) return;
        Gizmos.color = Color.green;
        foreach (Transform c in coverPoints)
            if (c != null) Gizmos.DrawWireSphere(c.position, 0.7f);
    }

    // Debug overlay: shows all four live scores
    void OnGUI(){
        if (overlayStyle == null)
        {
            overlayStyle = new GUIStyle(GUI.skin.label);
            overlayStyle.fontStyle = FontStyle.Bold;
            overlayStyle.normal.textColor = Color.white;
        }
        overlayStyle.fontSize = overlayFontSize;

        float lineH = overlayFontSize * 1.4f;
        float boxW = overlayFontSize * 12f;
        float boxH = lineH * 7f + 20f;

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
    }
}
