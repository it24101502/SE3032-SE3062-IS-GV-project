using System.Collections.Generic;
using UnityEngine;
using BunkerAI;

// An autonomous bunker defence controller. The intelligence chooses WHERE and
// WHEN to place pads; it is intentionally not a walking enemy with pathfinding.
// Pure scoring and memory are in TrapUtility.cs; this class senses/acts in Unity.
public class TrapSetterAgent : MonoBehaviour
{
    public Transform player;
    [Range(1, 6)] public int capacity = 3;
    public float decisionInterval = 1;
    public float placementCooldown = 8;
    public float safePlayerDistance = 4;
    public float minimumTrapSpacing = 3;
    public float armingDelay = 2.5f;
    public float damage = 25;
    public bool showDebug;

    private readonly PathMemory memory = new PathMemory();
    private readonly List<BunkerTrap> traps = new List<BunkerTrap>();
    private BunkerPlayerHealth health;
    private Vector3 spawn;
    private Material baseMaterial, lampMaterial;
    private float nextSample, nextDecision, nextAction;
    private UtilityBreakdown lastScore;
    private string decision = "Wait: learning the player's route";
    private string candidate = "none";
    private int placementCount, relocationCount, hitCount;

    void Start()
    {
        if (player == null)
        {
            BunkerWalker walker = FindAnyObjectByType<BunkerWalker>();
            if (walker != null) player = walker.transform;
        }
        if (player == null) { Debug.LogWarning("Trap setter needs a player Transform."); enabled = false; return; }
        health = player.GetComponent<BunkerPlayerHealth>();
        if (health == null) health = player.gameObject.AddComponent<BunkerPlayerHealth>();
        health.Respawned += ResetLearning;
        spawn = player.position;
        baseMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")); baseMaterial.color = new Color(.18f, .23f, .19f);
        lampMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")); lampMaterial.EnableKeyword("_EMISSION");
        ResetLearning();
    }

    void Update()
    {
        if (player == null) return;
        if (Input.GetKeyDown(KeyCode.F3)) showDebug = !showDebug;
        if (showDebug && Input.GetKeyDown(KeyCode.R)) { health.ResetRound(); return; }
        if (Time.time >= nextSample)
        {
            memory.Observe(player.position.x, player.position.z, Time.time);
            nextSample = Time.time + .25f;
        }
        if (Time.time >= nextDecision)
        {
            nextDecision = Time.time + Mathf.Max(.25f, decisionInterval);
            Decide();
        }
    }

    private float FlatDistance(Vector3 a, Vector3 b)
    { return Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z)); }

    private bool ValidSite(VisitMemory cell, out Vector3 site)
    {
        site = new Vector3(cell.X, 0, cell.Z);
        if (FlatDistance(site, player.position) < Mathf.Max(3, safePlayerDistance)
            || FlatDistance(site, spawn) < 3) return false;
        // Only observed walking locations are considered, never unvisited rooms.
        if (!Physics.Raycast(site + Vector3.up * 2, Vector3.down, out RaycastHit floor, 3,
            ~0, QueryTriggerInteraction.Ignore) || floor.normal.y < .9f) return false;
        if (floor.transform.IsChildOf(player) || floor.collider.bounds.size.y > .5f || Mathf.Abs(floor.point.y) > .25f) return false;
        site.y = floor.point.y;
        // A raised check sphere avoids the floor and rejects walls/furniture.
        if (Physics.CheckSphere(site + Vector3.up * .42f, .36f, ~0, QueryTriggerInteraction.Ignore)) return false;
        foreach (BunkerTrap trap in traps)
            if (FlatDistance(trap.transform.position, site) < Mathf.Max(1.5f, minimumTrapSpacing)) return false;
        return true;
    }

    private void Decide()
    {
        traps.RemoveAll(t => t == null);
        if (Time.time < nextAction) { decision = "Wait: placement cooldown"; return; }
        VisitMemory best = null; Vector3 bestSite = Vector3.zero;
        lastScore = new UtilityBreakdown();
        double bestValue = -1;
        foreach (VisitMemory cell in memory.Cells.Values)
        {
            if (!TrapUtility.HasEvidence(memory, cell, Time.time) || !ValidSite(cell, out Vector3 site)) continue;
            UtilityBreakdown score = TrapUtility.Score(memory, cell, Time.time, FlatDistance(site, player.position));
            if (score.Total > bestValue) { best = cell; bestSite = site; bestValue = score.Total; lastScore = score; }
        }
        candidate = best == null ? "none" : best.Key.ToString();
        BunkerTrap weakest = null; double weakestValue = double.PositiveInfinity;
        foreach (BunkerTrap trap in traps)
        {
            // Never move a pad while the player is approaching or during arming.
            if (trap.IsPrimed || Time.time - trap.PlacedAt < 15 || FlatDistance(trap.transform.position, player.position) < 5) continue;
            double value = TrapUtility.Score(memory, trap.Memory, Time.time, FlatDistance(trap.transform.position, player.position)).Total;
            if (value < weakestValue) { weakest = trap; weakestValue = value; }
        }
        TrapAction action = TrapUtility.Choose(bestValue, traps.Count, capacity, Time.time >= nextAction, weakestValue);
        if (action == TrapAction.Wait)
        {
            decision = best == null ? "Wait: need a safe, repeated route" : "Wait: score too low or no worthwhile relocation";
            return;
        }
        if (action == TrapAction.Relocate)
        {
            weakest.Memory.BlockedUntil = Time.time + 30;
            traps.Remove(weakest); Destroy(weakest.gameObject); relocationCount++;
        }
        Place(best, bestSite); placementCount++;
        nextAction = Time.time + Mathf.Max(2, placementCooldown);
        decision = action + " at " + best.Key + " | score " + bestValue.ToString("F2");
        Debug.Log("Trap setter: " + decision);
    }

    private void Place(VisitMemory cell, Vector3 site)
    {
        // Utility chooses position/timing; types cycle for a predictable demo.
        BunkerTrapType kind = (BunkerTrapType)(placementCount % 3);
        GameObject root = new GameObject("Adaptive " + kind + " pad");
        root.transform.SetParent(transform); root.transform.position = site;
        GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disc.name = "Pressure pad base"; disc.transform.SetParent(root.transform, false);
        disc.transform.localPosition = Vector3.up * .045f; disc.transform.localScale = new Vector3(.7f, .045f, .7f);
        // Purely visual; the player cannot get stuck on a pad.
        Collider discCollider = disc.GetComponent<Collider>(); discCollider.enabled = false; Destroy(discCollider);
        disc.GetComponent<Renderer>().sharedMaterial = baseMaterial;
        GameObject lamp = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        lamp.name = kind + " indicator"; lamp.transform.SetParent(root.transform, false);
        lamp.transform.localPosition = Vector3.up * .10f; lamp.transform.localScale = new Vector3(.22f, .015f, .22f);
        Collider lampCollider = lamp.GetComponent<Collider>(); lampCollider.enabled = false; Destroy(lampCollider);
        Renderer indicator = lamp.GetComponent<Renderer>(); indicator.sharedMaterial = lampMaterial;
        BunkerTrap trap = root.AddComponent<BunkerTrap>();
        trap.Initialize(this, cell, player, health, indicator, armingDelay, kind);
        traps.Add(trap);
    }

    public void Consumed(BunkerTrap trap)
    {
        trap.Memory.BlockedUntil = Time.time + 30;
        traps.Remove(trap); hitCount++;
    }

    public void ResetLearning()
    {
        foreach (BunkerTrap trap in traps) if (trap != null) Destroy(trap.gameObject);
        traps.Clear(); memory.Clear();
        nextSample = Time.time; nextDecision = Time.time + 1; nextAction = Time.time + 3;
        placementCount = relocationCount = hitCount = 0;
        decision = "Wait: learning the player's route"; candidate = "none"; lastScore = new UtilityBreakdown();
    }

    void OnGUI()
    {
        if (!showDebug || player == null) return;
        GUILayout.BeginArea(new Rect(16, 16, 520, 285), GUI.skin.box);
        GUILayout.Label("TRAP SETTER / Utility AI / F3 hides this panel");
        GUILayout.Label("Health: " + (health != null ? health.Health.ToString("F0") : "n/a") + " | R resets this demo round");
        GUILayout.Label("Observed cells: " + memory.Cells.Count + " | Active traps: " + traps.Count + "/" + capacity);
        GUILayout.Label("Placements: " + placementCount + " | Relocations: " + relocationCount + " | Activations: " + hitCount);
        GUILayout.Label("Candidate: " + candidate + " | Score: " + lastScore.Total.ToString("F2"));
        GUILayout.Label("Heat " + lastScore.Heat.ToString("F2") + " x .65 + Recency " + lastScore.Recency.ToString("F2")
            + " x .25 + Proximity " + lastScore.Proximity.ToString("F2") + " x .10");
        GUILayout.Label(decision);
        GUILayout.Label("Amber = arming | Red = pressure | Cyan = shock | Purple = burst");
        GUILayout.Label("White flash = burst countdown: move away! Next type: " + (BunkerTrapType)(placementCount % 3));
        GUILayout.Label("Pressure: " + damage + " damage | Burst: " + damage * 1.6f + " damage | Shock: 4s slow");
        GUILayout.Label("Player movement: " + (health != null && health.IsSlowed ? "SLOWED" : "normal"));
        GUILayout.EndArea();
    }

    void OnDestroy()
    {
        if (health != null) health.Respawned -= ResetLearning;
        if (baseMaterial != null) Destroy(baseMaterial);
        if (lampMaterial != null) Destroy(lampMaterial);
    }
}
