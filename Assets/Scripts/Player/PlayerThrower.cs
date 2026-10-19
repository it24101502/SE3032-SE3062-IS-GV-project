using UnityEngine;

// Put on the Player (it needs the Player's CharacterController and PlayerStats). Press Q to throw a rock.
public class PlayerThrower : MonoBehaviour
{
    public Transform cameraTransform;     // found automatically (Main Camera) if empty
    public Throwable prefab;              // optional; if empty a small bouncy sphere is created at runtime
    public int ammo = 5;
    public float throwForce = 14f;
    public float cooldown = 0.4f;
    public KeyCode key = KeyCode.Q;
    [Tooltip("Optional layer name for thrown rocks (create it in Project Settings > Tags and Layers).")]
    public string throwableLayer = "Throwable";

    float nextThrow;
    PlayerStats stats;
    CharacterController playerCollider;
    int throwableLayerIndex = -1;
    static PhysicsMaterial rockMaterial;      // shared by every rock, created once

    void Start()
    {
        stats = GetComponent<PlayerStats>();
        playerCollider = GetComponent<CharacterController>();
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
        throwableLayerIndex = LayerMask.NameToLayer(throwableLayer);   // -1 if the layer does not exist

        if (stats == null || playerCollider == null)
            Debug.LogWarning("[PlayerThrower] Put this script on the Player object (it needs PlayerStats and CharacterController).");
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

    static PhysicsMaterial GetRockMaterial()
    {
        if (rockMaterial == null)
        {
            rockMaterial = new PhysicsMaterial("Rock");
            rockMaterial.bounciness = 0.35f;
            rockMaterial.dynamicFriction = 0.5f;
            rockMaterial.staticFriction = 0.5f;
        }
        return rockMaterial;
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
            go.GetComponent<Collider>().sharedMaterial = GetRockMaterial();
        }

        if (throwableLayerIndex >= 0) go.layer = throwableLayerIndex;
        go.transform.position = cameraTransform.position + cameraTransform.forward * 0.8f;

        var rb = go.GetComponent<Rigidbody>();
        rb.mass = 1f;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;   // fast rocks must not tunnel through thin walls
        rb.AddForce(cameraTransform.forward * throwForce, ForceMode.VelocityChange);

        var col = go.GetComponent<Collider>();
        if (col != null && playerCollider != null) Physics.IgnoreCollision(col, playerCollider);

        Destroy(go, 10f);
    }
}
