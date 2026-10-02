using UnityEngine;

public class Door : MonoBehaviour
{
    public Transform player;
    public float interactDistance = 3f;
    public float openAngle = 90f;
    public float openSpeed = 3f;

    bool isOpen;
    Quaternion closedRot, openRot;

    void Start()
    {
        closedRot = transform.rotation;
        openRot = closedRot * Quaternion.Euler(0f, openAngle, 0f);
    }

    void Update()
    {
        bool nearby = Vector3.Distance(player.position, transform.position) <= interactDistance;
        if (nearby && Input.GetKeyDown(KeyCode.E)) isOpen = !isOpen;

        Quaternion target = isOpen ? openRot : closedRot;
        transform.rotation = Quaternion.Slerp(transform.rotation, target, Time.deltaTime * openSpeed);
    }
}