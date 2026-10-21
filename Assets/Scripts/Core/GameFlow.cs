using UnityEngine;
using UnityEngine.SceneManagement;

// Put this on an empty GameObject called "GameFlow".
// Shows a small HUD, win/lose text, and lets you press R to restart.
public class GameFlow : MonoBehaviour
{
    public PlayerStats player;
    public BossHealth boss;
    public PlayerThrower thrower;

    GUIStyle hud, big;

    void Start()
    {
        if (player == null)  player  = FindFirstObjectByType<PlayerStats>();
        if (boss == null)    boss    = FindFirstObjectByType<BossHealth>();
        if (thrower == null) thrower = FindFirstObjectByType<PlayerThrower>();
    }

    void Update()
    {
        bool over = (player != null && player.IsDead) || (boss != null && boss.IsDead);
        // The scene must be in File > Build Profiles > Scene List for this to work
        if (over && Input.GetKeyDown(KeyCode.R))
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    void OnGUI()
    {
        if (hud == null)
        {
            hud = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
            hud.normal.textColor = Color.white;
            big = new GUIStyle(GUI.skin.label) { fontSize = 48, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            big.normal.textColor = Color.white;
        }

        string line = "";
        if (player != null)  line += "HP " + Mathf.CeilToInt(player.health) + "   Weapon: " + player.weapon + " (1 melee / 2 ranged)";
        if (thrower != null) line += "   Rocks: " + thrower.ammo + " (Q)";
        line += "   E: door   F9: save boss log";
        GUI.Label(new Rect(10, Screen.height - 40, Screen.width - 20, 30), line, hud);

        // crosshair
        GUI.Label(new Rect(Screen.width / 2f - 6, Screen.height / 2f - 14, 30, 30), "+", hud);

        if (player != null && player.IsDead)
            GUI.Label(new Rect(0, 0, Screen.width, Screen.height), "YOU DIED\nPress R to restart", big);
        else if (boss != null && boss.IsDead)
            GUI.Label(new Rect(0, 0, Screen.width, Screen.height), "BOSS DEFEATED\nPress R to restart", big);
    }
}
