using UnityEngine;

// A small, dependency-free first person controller. Distances are in metres.
[RequireComponent(typeof(CharacterController))]
public class BunkerWalker : MonoBehaviour
{
    public Camera viewCamera;
    public Light flashlight;
    public float walkSpeed = 3f;
    public float mouseSensitivity = 1.8f;
    private CharacterController controller;
    private float pitch;
    private float verticalSpeed;
    private BunkerDoor nearbyDoor;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        if (Cursor.lockState != CursorLockMode.Locked) return;

        transform.Rotate(0f, Input.GetAxis("Mouse X") * mouseSensitivity, 0f);
        pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * mouseSensitivity, -80f, 80f);
        viewCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        Vector3 input = Vector3.ClampMagnitude(new Vector3(Input.GetAxisRaw("Horizontal"), 0, Input.GetAxisRaw("Vertical")), 1f);
        Vector3 movement = transform.TransformDirection(input) * walkSpeed;
        if (controller.isGrounded && verticalSpeed < 0f) verticalSpeed = -2f;
        verticalSpeed += Physics.gravity.y * Time.deltaTime;
        movement.y = verticalSpeed;
        controller.Move(movement * Time.deltaTime);

        if (Input.GetKeyDown(KeyCode.F)) flashlight.enabled = !flashlight.enabled;
        nearbyDoor = null;
        if (Physics.Raycast(viewCamera.transform.position, viewCamera.transform.forward, out RaycastHit hit, 2.7f))
            nearbyDoor = hit.collider.GetComponentInParent<BunkerDoor>();
        if (nearbyDoor != null && Input.GetKeyDown(KeyCode.E)) nearbyDoor.Toggle();
    }

    void OnDisable()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
