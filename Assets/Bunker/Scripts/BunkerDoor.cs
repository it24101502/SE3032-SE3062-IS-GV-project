using UnityEngine;

// Attach to a hinge object; the door panel is its child.
public class BunkerDoor : MonoBehaviour
{
    public float openAngle = -100f;
    public float speed = 100f;
    public bool isOpen = true;
    private Quaternion closedRotation;
    private float angle;

    void Awake()
    {
        closedRotation = transform.localRotation;
        angle = isOpen ? openAngle : 0f;
        transform.localRotation = closedRotation * Quaternion.Euler(0, angle, 0);
    }

    public void Toggle() { isOpen = !isOpen; }

    void Update()
    {
        angle = Mathf.MoveTowards(angle, isOpen ? openAngle : 0f, speed * Time.deltaTime);
        transform.localRotation = closedRotation * Quaternion.Euler(0, angle, 0);
    }
}
