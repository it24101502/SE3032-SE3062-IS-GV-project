using System;
using UnityEngine;

// Replace this small demo receiver with your team's health component if needed.
public class BunkerPlayerHealth : MonoBehaviour
{
    public float maximumHealth = 100;
    public float Health { get; private set; }
    public event Action<float> Damaged;
    public event Action Respawned;
    private Vector3 spawn;
    private Quaternion facing;
    private float immuneUntil;
    private BunkerWalker walker;
    private float normalSpeed;
    private float slowedUntil;
    public bool IsSlowed { get { return Time.time < slowedUntil; } }

    void Awake()
    {
        spawn = transform.position; facing = transform.rotation;
        Health = maximumHealth; immuneUntil = Time.time + 2;
        walker = GetComponent<BunkerWalker>();
        if (walker != null) normalSpeed = walker.walkSpeed;
    }

    void Update()
    {
        if (slowedUntil > 0 && Time.time >= slowedUntil) ClearSlow();
    }

    public void ApplySlow()
    {
        if (Time.time < immuneUntil || walker == null) return;
        if (slowedUntil > 0 && !IsSlowed) ClearSlow();
        if (!IsSlowed) normalSpeed = walker.walkSpeed;
        slowedUntil = Time.time + 4;
        walker.walkSpeed = normalSpeed * .5f;
        Debug.Log("Shock pad: movement slowed for 4 seconds.");
    }

    private void ClearSlow()
    {
        if (slowedUntil > 0 && walker != null) walker.walkSpeed = normalSpeed;
        slowedUntil = 0;
    }

    void OnDisable() { ClearSlow(); }

    public void ApplyDamage(float amount)
    {
        if (amount <= 0 || Time.time < immuneUntil) return;
        Health = Mathf.Max(0, Health - amount);
        immuneUntil = Time.time + 1;
        if (Damaged != null) Damaged(amount);
        Debug.Log("Trap hit: health " + Health + "/" + maximumHealth);
        if (Health <= 0) ResetRound();
    }

    public void ResetRound()
    {
        ClearSlow();
        CharacterController controller = GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;
        transform.SetPositionAndRotation(spawn, facing);
        if (controller != null) controller.enabled = true;
        Health = maximumHealth; immuneUntil = Time.time + 2;
        if (Respawned != null) Respawned();
    }
}
