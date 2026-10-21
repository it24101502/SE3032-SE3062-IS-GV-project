using UnityEngine;

// Attach to the tracked enemy root so existing gun/melee/rock hits find it.
[DisallowMultipleComponent]
public sealed class EnemyHealth : MonoBehaviour, IDamageable
{
    [Min(1f)] public float maxHealth = 30f;
    [SerializeField] private float health;

    public float Health => health;
    public bool IsDead => health <= 0f;

    private SpawnDirector owner;
    private AgentMovement movement;
    private bool deathHandled;

    private void Awake()
    {
        movement = GetComponent<AgentMovement>();
        health = Mathf.Max(1f, maxHealth);
    }

    public void Initialize(SpawnDirector director)
    {
        owner = director;
    }

    public void TakeDamage(float amount)
    {
        if (deathHandled || !isActiveAndEnabled || amount <= 0f ||
            float.IsNaN(amount) || float.IsInfinity(amount))
            return;

        health = Mathf.Max(0f, health - amount);
        if (health > 0f)
            return;

        deathHandled = true;
        if (movement != null)
            movement.isAlive = false;

        if (owner != null)
            owner.NotifyEnemyDeath(gameObject);

        // Also works for an enemy placed manually without a director.
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

#if UNITY_EDITOR
    [ContextMenu("Tests/Deal Lethal Damage")]
    private void DealLethalDamage()
    {
        if (Application.isPlaying)
            TakeDamage(Mathf.Max(1f, health));
    }
#endif
}
