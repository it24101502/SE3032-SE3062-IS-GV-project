using UnityEngine;

// Put on the Player. Press Q to throw a rock.
public class PlayerThrower : MonoBehaviour
{
    public Transform cameraTransform;     // drag Main Camera here
    public Throwable prefab;              // optional; if empty a small sphere is created at runtime
    public int ammo = 5;
    public float throwForce = 14f;
    public float cooldown = 0.4f;
    public KeyCode key = KeyCode.Q;

    float nextThrow;
    PlayerStats stats;
    CharacterController playerCollider;

    void Start()
    {
        stats = GetComponent<PlayerStats>();
        playerCollider = GetComponent<CharacterController>();
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
    }

    void Update()
    {
        if (stats != null && stats.IsDead) return;
        if (Input.GetKeyDown(key) && ammo > 0 && Time.time >= nextThrow)
        {
            nextThrow = Time.time + cooldown;
            ammo--;
            Throw();
        }
    }

    void Throw()
    {
        GameObject go;
        if (prefab != null)
        {
            go = Instantiate(prefab.gameObject);
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.transform.localScale = Vector3.one * 0.3f;
            go.AddComponent<Rigidbody>();
            go.AddComponent<Throwable>();
        }

        go.transform.position = cameraTransform.position + cameraTransform.forward * 0.8f;

        var rb = go.GetComponent<Rigidbody>();
        rb.mass = 1f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.AddForce(cameraTransform.forward * throwForce, ForceMode.VelocityChange);

        var col = go.GetComponent<Collider>();
        if (col != null && playerCollider != null) Physics.IgnoreCollision(col, playerCollider);

        Destroy(go, 10f);
    }
}
