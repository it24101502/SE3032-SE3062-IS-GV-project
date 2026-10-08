using UnityEngine;

public class BossHealth : MonoBehaviour, IDamageable
{
    public float maxHealth = 100f;
    public float health = 100f;
    public float Fraction => Mathf.Clamp01(health / maxHealth);
    public bool IsDead => health <= 0f;
    public void TakeDamage(float amount) { health = Mathf.Max(0f, health - amount); }
}
