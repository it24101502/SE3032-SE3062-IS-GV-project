using UnityEngine;
using UnityEngine.AI;

// Put on a Cube (scale it into a wall). Can be shot, hit in melee or hit with thrown rocks.
// While standing it carves a hole in the NavMesh so the boss paths around it.
// When destroyed it breaks into physics debris and the NavMesh opens up again.
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(NavMeshObstacle))]
public class Barricade : MonoBehaviour, IDamageable
{
    public float maxHealth = 60f;
    public int debrisCount = 8;
    public float debrisSize = 0.35f;
    public float debrisLifetime = 5f;
    public Color damagedColor = new Color(0.35f, 0.1f, 0.1f);
    [Tooltip("Optional layer name for debris (create it in Project Settings > Tags and Layers).")]
    public string debrisLayer = "Debris";

    float health;
    bool broken;
    Collider col;
    Renderer rend;
    NavMeshObstacle obstacle;
    Color baseColor;
    int debrisLayerIndex = -1;

    void Start()
    {
        health = maxHealth;
        col = GetComponent<Collider>();
        rend = GetComponent<Renderer>();
        obstacle = GetComponent<NavMeshObstacle>();
        debrisLayerIndex = LayerMask.NameToLayer(debrisLayer);   // -1 if the layer does not exist

        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.center = Vector3.zero;
        obstacle.size = Vector3.one;           // follows the cube's scale
        obstacle.carving = true;
        obstacle.carveOnlyStationary = true;

        if (rend != null) baseColor = rend.material.color;
    }

    public void TakeDamage(float amount)
    {
        if (broken) return;
        health -= amount;
        if (rend != null) rend.material.color = Color.Lerp(damagedColor, baseColor, Mathf.Clamp01(health / maxHealth));
        if (health <= 0f) Break();
    }

    void Break()
    {
        broken = true;
        Bounds b = col.bounds;

        for (int i = 0; i < debrisCount; i++)
        {
            var piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
            piece.transform.position = new Vector3(
                Random.Range(b.min.x, b.max.x),
                Random.Range(b.min.y, b.max.y),
                Random.Range(b.min.z, b.max.z));
            piece.transform.localScale = Vector3.one * debrisSize;
            piece.transform.rotation = Random.rotation;
            if (debrisLayerIndex >= 0) piece.layer = debrisLayerIndex;
            if (rend != null) piece.GetComponent<Renderer>().sharedMaterial = rend.sharedMaterial;

            var rb = piece.AddComponent<Rigidbody>();
            rb.AddExplosionForce(250f, b.center, 4f);
            Destroy(piece, debrisLifetime);       // debris is removed after a few seconds to keep physics cheap
        }

        obstacle.enabled = false;
        col.enabled = false;
        if (rend != null) rend.enabled = false;
        Destroy(gameObject, 0.1f);
    }
}
