using UnityEngine;

public enum BunkerTrapType { Pressure, Shock, Burst }

// Fictional game pads; all begin with an amber warning.
public class BunkerTrap : MonoBehaviour
{
    public BunkerAI.VisitMemory Memory { get; private set; }
    public float PlacedAt { get; private set; }
    public bool IsArmed { get { return Time.time >= PlacedAt + armingTime; } }
    public bool IsPrimed { get { return burstAt > 0; } }
    public BunkerTrapType Kind { get; private set; }
    private TrapSetterAgent owner;
    private Transform target;
    private BunkerPlayerHealth health;
    private Renderer indicator;
    private MaterialPropertyBlock properties;
    private float armingTime, burstAt;
    private bool spent;

    public void Initialize(TrapSetterAgent agent, BunkerAI.VisitMemory cell, Transform player,
        BunkerPlayerHealth receiver, Renderer lamp, float delay, BunkerTrapType kind)
    {
        owner = agent; Memory = cell; target = player; health = receiver;
        indicator = lamp; armingTime = Mathf.Max(1, delay); PlacedAt = Time.time;
        Kind = kind; properties = new MaterialPropertyBlock();
    }

    private bool Within(float radius)
    {
        Vector3 delta = target.position - transform.position;
        if (Mathf.Abs(delta.y) > 1.2f || new Vector2(delta.x, delta.z).sqrMagnitude > radius * radius) return false;
        return !Physics.Linecast(transform.position + Vector3.up * .35f,
            target.position + Vector3.up * .35f, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore)
            || hit.transform.IsChildOf(target);
    }

    void Update()
    {
        if (spent || target == null || owner == null || !owner.isActiveAndEnabled) return;
        Color armed = Kind == BunkerTrapType.Shock ? Color.cyan :
            Kind == BunkerTrapType.Burst ? new Color(.85f, .15f, 1) : new Color(1, .15f, .08f);
        Color color = !IsArmed ? new Color(1, .65f, .05f) : IsPrimed ? Color.white : armed;
        properties.SetColor("_Color", color);
        properties.SetColor("_BaseColor", color);
        properties.SetColor("_EmissionColor", color * (IsPrimed ? 1.5f + Mathf.Sin(Time.time * 30) : .8f));
        indicator.SetPropertyBlock(properties);
        if (IsPrimed)
        {
            if (Time.time >= burstAt) Finish(Within(1.4f));
            return;
        }
        if (!IsArmed || !Within(Kind == BunkerTrapType.Burst ? .9f : .65f)) return;
        if (Kind == BunkerTrapType.Burst)
        {
            burstAt = Time.time + 1.2f;
            Debug.Log("Burst pad primed: move away before the white flash ends.");
        }
        else Finish(true);
    }

    private void Finish(bool inRange)
    {
        spent = true;
        owner.Consumed(this);
        if (health != null && inRange)
        {
            if (Kind == BunkerTrapType.Shock) health.ApplySlow();
            else health.ApplyDamage(Kind == BunkerTrapType.Burst ? owner.damage * 1.6f : owner.damage);
        }
        if (!inRange) Debug.Log("Burst pad avoided: no damage.");
        Destroy(gameObject);
    }
}
