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

    [Header("Debug overlay")]
    public int overlayFontSize = 24;
    GUIStyle overlayStyle;

    BossBrain brain = new BossBrain();
    Renderer rend;
    NavMeshAgent agent;

    void Start(){
        rend = GetComponent<Renderer>();
        agent = GetComponent<NavMeshAgent>();
        agent.speed = moveSpeed;
        agent.stoppingDistance = 1.5f;
    }

    void Update(){
        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        float distance = toPlayer.magnitude;
        Vector3 dir = toPlayer.normalized;

        BossAction action = brain.Evaluate(playerStats.weapon == WeaponType.Ranged,
                                           playerStats.HealthFraction,
                                           bossHealthComp.Fraction,
                                           distance, Time.deltaTime);

        switch (action){
            case BossAction.Attack:
                GoTo(player.position);
                rend.material.color = Color.red;
                if (distance < 2f) playerStats.health -= 10f * Time.deltaTime;
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
        float boxH = lineH * 5f + 20f;

        GUI.color = new Color(0f, 0f, 0f, 0.7f);
        GUI.DrawTexture(new Rect(10, 10, boxW, boxH), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Label(new Rect(20, 15, boxW, lineH), "Current: " + brain.Current, overlayStyle);
        for (int i = 0; i < 4; i++){
            GUI.Label(new Rect(20, 15 + lineH * (i + 1), boxW, lineH),
                    ((BossAction)i) + ": " + brain.LastScores[i].ToString("F2"),
                    overlayStyle);
        }
    }
}