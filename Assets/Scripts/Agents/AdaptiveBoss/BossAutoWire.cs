using System.Collections.Generic;
using UnityEngine;

// NEW FILE. Put it on the Boss next to BossController.
// It fills in the scene references automatically, so the Boss prefab can be dropped
// into any scene (for example the integrated level) without re-assigning anything.
// BossController and BossBrain are not modified.
[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(BossController))]
public class BossAutoWire : MonoBehaviour
{
    [Tooltip("Name of the empty object whose children are the cover points.")]
    public string coverParentName = "CoverPoints";

    void Awake()
    {
        var boss = GetComponent<BossController>();

        if (boss.playerStats == null) boss.playerStats = FindFirstObjectByType<PlayerStats>();
        if (boss.player == null && boss.playerStats != null) boss.player = boss.playerStats.transform;
        if (boss.bossHealthComp == null) boss.bossHealthComp = GetComponent<BossHealth>();

        if (boss.coverPoints == null || boss.coverPoints.Length == 0)
        {
            var list = new List<Transform>();
            GameObject parent = GameObject.Find(coverParentName);
            if (parent != null)
                foreach (Transform t in parent.transform) list.Add(t);
            else
                Debug.LogWarning("[BossAutoWire] No object called '" + coverParentName + "' found. The boss will have no cover points.");
            boss.coverPoints = list.ToArray();
        }

        if (boss.player == null || boss.playerStats == null)
            Debug.LogWarning("[BossAutoWire] No PlayerStats found in the scene. The boss cannot see the player.");
    }
}
