using System;
using UnityEngine;

// =====================================================================
//  StealthHealth  -  [IS] Stealth Enemy (Peiris)
//  Implements the team's shared IDamageable interface, so the player's
//  gun/melee (PlayerStats) and thrown rocks (Throwable) can hurt it
//  without knowing anything about the Stealth Enemy.
// =====================================================================
public class StealthHealth : MonoBehaviour, IDamageable
{
    public float maxHealth = 60f;
    public float health = 60f;

    public event Action<float> Damaged;    // amount
    public event Action Died;

    public bool IsDead { get { return health <= 0f; } }
    public float Fraction { get { return Mathf.Clamp01(health / maxHealth); } }

    void Awake()
    {
        health = maxHealth;
    }

    public void TakeDamage(float amount)
    {
        if (IsDead || amount <= 0f) return;
        health = Mathf.Max(0f, health - amount);
        if (Damaged != null) Damaged(amount);
        if (IsDead && Died != null) Died();
    }
}
