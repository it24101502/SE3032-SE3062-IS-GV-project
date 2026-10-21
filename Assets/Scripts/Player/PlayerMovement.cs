using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    public Transform cameraTransform;
    public float speed = 5f;
    public float sprintMultiplier = 1.6f;
    public float jumpHeight = 1.2f;
    public float mouseSensitivity = 2f;
    public float gravity = -20f;

    CharacterController cc;
    PlayerStats stats;
    float pitch;
    float verticalVelocity;

    private float slowUntil;
    public float slowFactor = 0.5f;
    public bool IsSlowed => Time.time < slowUntil;
    public void ApplySlow(float seconds) { slowUntil = Time.time + seconds; }

    void Start()
    {
        cc = GetComponent<CharacterController>();
        stats = GetComponent<PlayerStats>();
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) Cursor.lockState = CursorLockMode.None;
        if (stats != null && stats.IsDead) return;              // no control when dead

        // Look around
        transform.Rotate(0f, Input.GetAxis("Mouse X") * mouseSensitivity, 0f);
        pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * mouseSensitivity, -80f, 80f);
        cameraTransform.localEulerAngles = new Vector3(pitch, 0f, 0f);

        // Move with WASD (+ Shift to sprint, and apply temporary slow factor if active)
        Vector3 move = transform.right * Input.GetAxis("Horizontal")
                     + transform.forward * Input.GetAxis("Vertical");
        if (move.sqrMagnitude > 1f) move.Normalize();
        
        float currentSpeed = speed * (Input.GetKey(KeyCode.LeftShift) ? sprintMultiplier : 1f);
        if (IsSlowed) currentSpeed *= slowFactor;

        // Gravity + jump
        if (cc.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
        if (cc.isGrounded && Input.GetButtonDown("Jump"))
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        verticalVelocity += gravity * Time.deltaTime;

        cc.Move((move * currentSpeed + Vector3.up * verticalVelocity) * Time.deltaTime);
    }
}