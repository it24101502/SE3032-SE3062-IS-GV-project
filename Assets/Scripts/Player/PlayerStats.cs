using UnityEngine;

public enum WeaponType { Melee, Ranged }

public class PlayerStats : MonoBehaviour, IDamageable
{
    public float maxHealth = 100f;
    public float health = 100f;
    public WeaponType weapon = WeaponType.Ranged;

    [Header("Ranged (key 2)")]
    public float rangedDamage = 10f;
    public float rangedRange = 50f;
    public float rangedCooldown = 0.25f;

    [Header("Melee (key 1)")]
    public float meleeDamage = 25f;
    public float meleeRange = 2.5f;
    public float meleeCooldown = 0.6f;

    [Header("Hit detection")]
    [Tooltip("Layers that shots and melee swings can hit. Untick Debris, Throwable and Player.")]
    public LayerMask hitMask = ~0;

    public float HealthFraction => Mathf.Clamp01(health / maxHealth);   // 0 to 1, what BossBrain expects
    public bool IsDead => health <= 0f;

    float nextAttackTime;
    Transform cam;

    void Start()
    {
        cam = Camera.main != null ? Camera.main.transform : null;
    }

    public void TakeDamage(float amount)
    {
        health = Mathf.Max(0f, health - amount);
    }

    void Update()
    {
        if (IsDead || cam == null) return;

        if (Input.GetKeyDown(KeyCode.Alpha1)) weapon = WeaponType.Melee;
        if (Input.GetKeyDown(KeyCode.Alpha2)) weapon = WeaponType.Ranged;

        if (Input.GetMouseButtonDown(0) && Time.time >= nextAttackTime)
        {
            if (weapon == WeaponType.Ranged)
            {
                nextAttackTime = Time.time + rangedCooldown;
                Attack(rangedRange, 0f, rangedDamage);          // thin ray
            }
            else
            {
                nextAttackTime = Time.time + meleeCooldown;
                Attack(meleeRange, 0.5f, meleeDamage);          // fat sphere = forgiving melee swing
            }
        }
    }

    void Attack(float range, float radius, float damage)
    {
        RaycastHit hit;
        bool didHit = radius > 0f
            ? Physics.SphereCast(cam.position, radius, cam.forward, out hit, range, hitMask, QueryTriggerInteraction.Ignore)
            : Physics.Raycast(cam.position, cam.forward, out hit, range, hitMask, QueryTriggerInteraction.Ignore);

        if (!didHit) return;
        if (hit.collider.transform.IsChildOf(transform)) return;   // never hit ourselves

        var target = hit.collider.GetComponentInParent<IDamageable>();
        if (target != null) target.TakeDamage(damage);
    }
}
