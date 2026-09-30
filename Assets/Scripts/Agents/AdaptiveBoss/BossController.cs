using UnityEngine;

public class BossController : MonoBehaviour{
    [Header("Test inputs: change these while the game is running")]
    public Transform player;
    public bool playerHasRanged = true;
    [Range(0f, 1f)] public float playerHealth = 0.8f;
    [Range(0f, 1f)] public float bossHealth = 0.3f;

    [Header("Movement")]
    public float moveSpeed = 3f;

    [Header("Debug overlay")]
    public int overlayFontSize = 24;
    GUIStyle overlayStyle;

    BossBrain brain = new BossBrain();
    Renderer rend;

    void Start(){
        rend = GetComponent<Renderer>();
    }

    void Update(){
        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;
        float distance = toPlayer.magnitude;
        Vector3 dir = toPlayer.normalized;

        BossAction action = brain.Evaluate(playerHasRanged, playerHealth,
                                           bossHealth, distance, Time.deltaTime);

        switch (action){
            case BossAction.Attack:
                transform.position += dir * moveSpeed * Time.deltaTime;
                rend.material.color = Color.red;
                break;
            case BossAction.Retreat:
                transform.position -= dir * moveSpeed * Time.deltaTime;
                rend.material.color = Color.yellow;
                break;
            case BossAction.Flank:
                Vector3 sideways = Vector3.Cross(Vector3.up, dir);   // circle around the player
                transform.position += sideways * moveSpeed * Time.deltaTime;
                rend.material.color = Color.magenta;
                break;
            case BossAction.TakeCover:
                // Placeholder: stand still. Real cover points come later.
                rend.material.color = Color.blue;
                break;
        }
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

        // Dark semi-transparent background so the text is readable on any scene
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