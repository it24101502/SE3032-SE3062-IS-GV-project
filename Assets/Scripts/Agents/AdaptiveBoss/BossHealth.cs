using UnityEngine;

public class BossHealth : MonoBehaviour
{
    public float maxHealth = 100f;
    public float health = 100f;
    public float Fraction => health / maxHealth;
    public void TakeDamage(float amount) { health = Mathf.Max(0f, health - amount); }
}