using UnityEngine;
using UnityEngine.AI;

// Put on the door hinge (pivot). The door panel must be a child of this object.
public class Door : MonoBehaviour
{
    [Tooltip("Found automatically if left empty.")]
    public Transform player;
    public float interactDistance = 3f;
    public float openAngle = 90f;
    public float openSpeed = 3f;
    public KeyCode interactKey = KeyCode.E;

    [Header("Optional sounds")]
    public AudioClip openClip;
    public AudioClip closeClip;

    [Tooltip("Optional. If empty, one is added to the door panel automatically.")]
    public NavMeshObstacle obstacle;

    bool isOpen, nearby;
    Quaternion closedRot, openRot;
    float interactSqr;
    GUIStyle promptStyle;

    void Start()
    {
        closedRot = transform.rotation;
        openRot = closedRot * Quaternion.Euler(0f, openAngle, 0f);
        interactSqr = interactDistance * interactDistance;

        if (player == null)
        {
            var stats = FindFirstObjectByType<PlayerStats>();
            if (stats != null) player = stats.transform;
        }

        // A closed door blocks the boss on the NavMesh; an open door does not.
        if (obstacle == null)
        {
            var panel = GetComponentInChildren<Collider>();
            if (panel != null)
            {
                obstacle = panel.gameObject.AddComponent<NavMeshObstacle>();
                obstacle.shape = NavMeshObstacleShape.Box;
                obstacle.center = Vector3.zero;
                obstacle.size = Vector3.one;
                obstacle.carving = true;
                obstacle.carveOnlyStationary = true;
            }
        }
    }

    void Update()
    {
        if (player == null) return;

        // sqrMagnitude avoids a square root every frame
        nearby = (player.position - transform.position).sqrMagnitude <= interactSqr;
        if (nearby && Input.GetKeyDown(interactKey))
        {
            isOpen = !isOpen;
            AudioClip clip = isOpen ? openClip : closeClip;
            if (clip != null) AudioSource.PlayClipAtPoint(clip, transform.position);
        }

        if (obstacle != null) obstacle.enabled = !isOpen;

        // Only rotate while the door is still moving
        Quaternion target = isOpen ? openRot : closedRot;
        if (Quaternion.Angle(transform.rotation, target) > 0.05f)
            transform.rotation = Quaternion.Slerp(transform.rotation, target, Time.deltaTime * openSpeed);
        else
            transform.rotation = target;
    }

    void OnGUI()
    {
        if (!nearby) return;
        if (promptStyle == null)
        {
            promptStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            promptStyle.normal.textColor = Color.white;
        }
        string text = "Press " + interactKey + " to " + (isOpen ? "close" : "open") + " the door";
        GUI.Label(new Rect(0, Screen.height * 0.62f, Screen.width, 40), text, promptStyle);
    }
}
