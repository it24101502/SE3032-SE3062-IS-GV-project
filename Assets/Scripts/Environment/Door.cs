using UnityEngine;
using UnityEngine.AI;

public class Door : MonoBehaviour
{
    public Transform player;
    public float interactDistance = 3f;
    public float openAngle = 90f;
    public float openSpeed = 3f;

    [Tooltip("Optional. If empty, one is added to the door panel automatically.")]
    public NavMeshObstacle obstacle;

    bool isOpen;
    Quaternion closedRot, openRot;

    void Start()
    {
        closedRot = transform.rotation;
        openRot = closedRot * Quaternion.Euler(0f, openAngle, 0f);

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
        bool nearby = Vector3.Distance(player.position, transform.position) <= interactDistance;
        if (nearby && Input.GetKeyDown(KeyCode.E)) isOpen = !isOpen;

        if (obstacle != null) obstacle.enabled = !isOpen;

        Quaternion target = isOpen ? openRot : closedRot;
        transform.rotation = Quaternion.Slerp(transform.rotation, target, Time.deltaTime * openSpeed);
    }
}
