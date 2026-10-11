using System;
using UnityEngine;

// Goes on the thrown object (added automatically if you use no prefab).
public class Throwable : MonoBehaviour
{
    public float damage = 15f;
    public float minImpactSpeed = 4f;     // soft taps do nothing
    public float loudness = 1f;

    // Other agents (e.g. the Stealth Enemy's sound detection) can subscribe:
    //   Throwable.OnImpact += (position, noise) => { ... };
    public static event Action<Vector3, float> OnImpact;

    bool hasDealtDamage;

    void OnCollisionEnter(Collision c)
    {
        float speed = c.relativeVelocity.magnitude;
        if (speed < minImpactSpeed) return;

        OnImpact?.Invoke(transform.position, loudness * speed);

        if (hasDealtDamage) return;
        if (c.collider.GetComponentInParent<PlayerStats>() != null) return;   // bounce-back never hurts the thrower

        var target = c.collider.GetComponentInParent<IDamageable>();
        if (target != null)
        {
            target.TakeDamage(damage);
            hasDealtDamage = true;
        }
    }
}
