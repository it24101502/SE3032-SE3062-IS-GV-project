using UnityEngine;

// Environment integration points only. These do not implement agent decisions.
public sealed class BunkerAgentMarker : MonoBehaviour
{
    public enum Kind { RouteSample, TrapCandidate, SoundSource, HidePoint, PatrolPoint, BossCover, BossRetreat, SpawnCandidate, NoTrapZone }
    public Kind kind;
    public string zone;
    public string route;
    public float radius = 0.5f;

    void OnDrawGizmosSelected()
    {
        Gizmos.color = kind == Kind.NoTrapZone ? Color.red : Color.cyan;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.15f, radius);
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up);
    }
}
