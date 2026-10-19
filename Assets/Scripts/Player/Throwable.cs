using System;
using UnityEngine;

// Goes on the thrown object (added automatically if you use no prefab).
public class Throwable : MonoBehaviour
{
    public float damage = 15f;
    public float minImpactSpeed = 4f;     // soft taps do nothing
    public float loudness = 1f;
    [Tooltip("Prints what each rock hit to the Console. Turn off when everything works.")]
    public bool debugLog = true;

    // Other agents (e.g. the Stealth Enemy's sound detection) can subscribe:
    //   Throwable.OnImpact += (position, noise) => { ... };
    public static event Action<Vector3, float> OnImpact;

    bool hasDealtDamage;

    void OnCollisionEnter(Collision c)
    {
        float speed = c.relativeVelocity.magnitude;
        if (speed < minImpactSpeed)
        {
            if (debugLog) Debug.Log("[Rock] touched " + c.collider.name + " too slowly (" + speed.ToString("F1") + " < " + minImpactSpeed + "), no damage");
            return;
        }

        OnImpact?.Invoke(transform.position, loudness * speed);

        if (hasDealtDamage) return;
        if (c.collider.GetComponentInParent<PlayerStats>() != null) return;   // bounce-back never hurts the thrower

        var target = c.collider.GetComponentInParent<IDamageable>();
        if (debugLog)
            Debug.Log("[Rock] hit " + c.collider.name + " at speed " + speed.ToString("F1") +
                      (target != null ? " -> damage " + damage : " -> NOTHING on it can take damage"));

        if (target != null)
        {
            target.TakeDamage(damage);
            hasDealtDamage = true;
        }
    }
}