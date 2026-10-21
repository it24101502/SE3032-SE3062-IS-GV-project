using UnityEngine;
using UnityEngine.AI;

// =====================================================================
//  StealthPerception  -  [IS] Stealth Enemy (Peiris)
//  The SENSES of the Stealth Enemy. Turns the world into numbers on the
//  StealthBlackboard. Decides nothing - the StealthBrain does that.
//
//  HEARING (the unique part):
//    Sound reaches the enemy by the LOUDEST of two routes:
//      a) around the walls  - distance = NavMesh path length
//      b) through the walls - straight-line distance, but muffled
//    perceived = loudness / (1 + (d / referenceDistance)^2)
//    (inverse-square falloff, the +1 stops it blowing up at d = 0)
//    So a gunshot in the next room is heard; a footstep behind a thick
//    wall at the end of a long corridor is not.
//
//  SIGHT:
//    view cone + line-of-sight raycast. Seeing the player raises
//    awareness gradually (a stealth "detection meter"), faster when the
//    player is close, in the centre of the view and moving.
//
//  MEMORY:
//    awareness slowly decays when nothing has been sensed for a while.
// =====================================================================
public class StealthPerception : MonoBehaviour
{
    [Header("Vision")]
    public float viewDistance = 18f;
    [Range(10f, 360f)] public float viewAngle = 110f;
    [Tooltip("Height of the eyes above the pivot.")]
    public float eyeHeight = 0.8f;
    [Tooltip("Awareness gained per second when the player is close, centred and moving.")]
    public float sightGainPerSecond = 1.2f;
    [Tooltip("Closer than this the enemy senses the player even behind it (if not behind a wall).")]
    public float closeSenseRange = 1.5f;

    [Header("Hearing")]
    [Tooltip("Sounds further away than this (straight line) are ignored completely.")]
    public float hearingRange = 35f;
    [Tooltip("Distance at which a sound is perceived at half its loudness.")]
    public float referenceDistance = 6f;
    [Tooltip("How much of a sound gets THROUGH a wall (0 = none, 1 = walls do nothing).")]
    [Range(0f, 1f)] public float wallMuffle = 0.3f;
    [Tooltip("Perceived sounds weaker than this are ignored.")]
    public float minAudible = 0.04f;
    [Tooltip("Awareness gained per unit of perceived sound.")]
    public float hearingGain = 1f;

    [Header("Sound masking (machinery noise)")]
    [Tooltip("Noises made within this distance of a SoundSource marker (e.g. the Acoustic room fan) are partly drowned out.")]
    public float maskRadius = 4f;
    [Range(0f, 1f)] public float maskFactor = 0.4f;

    [Header("Memory")]
    public float decayPerSecond = 0.06f;
    [Tooltip("Awareness does not decay until this long after the last thing sensed.")]
    public float decayDelay = 2f;

    [Header("Player's view (to know if I have been spotted)")]
    public float playerViewAngle = 70f;
    public float playerEyeHeight = 0.6f;     // matches the camera height used by the boss

    [Header("Line of sight")]
    [Tooltip("Layers that block sight and muffle sound (walls, doors...).")]
    public LayerMask occluders = ~0;

    // ---- read by the controller (overlay, gizmos, logging) ----
    public Transform Player { get; private set; }
    public PlayerStats PlayerStats { get; private set; }
    public Vector3 LastNoisePos { get; private set; }
    public float LastNoiseStrength { get; private set; }
    public float LastNoiseTime { get; private set; }
    public NoiseSource LastNoiseSource { get; private set; }
    public bool LastNoiseUsedPath { get; private set; }
    public bool LastNoiseMasked { get; private set; }

    Transform playerCam;
    Vector3 lastPlayerPos;
    bool hasLastPlayerPos;
    float lastStimulusTime = -100f;
    float nextPlayerSearch;
    bool deaf;

    // noise collected between two Sense() calls - keep only the loudest
    bool pendingNoise;
    Vector3 pendingNoisePos;
    float pendingNoiseStrength;
    NoiseSource pendingNoiseSource;
    bool pendingUsedPath;
    bool pendingMasked;
    readonly System.Collections.Generic.List<Vector3> soundSources = new System.Collections.Generic.List<Vector3>();

    readonly RaycastHit[] hits = new RaycastHit[16];
    NavMeshPath path;

    void Awake()
    {
        path = new NavMeshPath();
    }

    void Start()
    {
        // Machinery noise sources placed by the World Builder (e.g. "Fan noise source" in the Acoustic room).
        foreach (BunkerAgentMarker m in FindObjectsByType<BunkerAgentMarker>(FindObjectsSortMode.None))
            if (m != null && m.kind == BunkerAgentMarker.Kind.SoundSource) soundSources.Add(m.transform.position);
    }

    bool IsMasked(Vector3 p)
    {
        foreach (Vector3 s in soundSources)
            if ((s - p).sqrMagnitude <= maskRadius * maskRadius) return true;
        return false;
    }

    void OnEnable()  { NoiseSystem.NoiseEmitted += OnNoise; }
    void OnDisable() { NoiseSystem.NoiseEmitted -= OnNoise; }

    public void SetDeaf(bool value) { deaf = value; }

    public Vector3 EyePosition { get { return transform.position + Vector3.up * eyeHeight; } }

    public Vector3 PlayerEyePosition
    {
        get { return Player != null ? Player.position + Vector3.up * playerEyeHeight : Vector3.zero; }
    }

    // =================================================================
    //  Called once per frame by StealthEnemyController, BEFORE the brain.
    // =================================================================
    public void Sense(StealthBlackboard bb, float dt)
    {
        FindPlayerIfNeeded();

        bb.HasPlayer = Player != null;
        bb.PlayerAlive = PlayerStats != null && !PlayerStats.IsDead;
        bb.CanSeePlayer = false;
        bb.PlayerSeesMe = false;
        bool sensedSomething = false;

        if (Player != null && dt > 0f)
        {
            bb.PlayerPosition = Player.position;
            bb.DistanceToPlayer = Vector3.Distance(transform.position, Player.position);

            Vector3 flat = Player.position - lastPlayerPos; flat.y = 0f;
            bb.PlayerSpeed = hasLastPlayerPos ? flat.magnitude / dt : 0f;
            lastPlayerPos = Player.position;
            hasLastPlayerPos = true;

            if (bb.PlayerAlive)
            {
                bool lineClear = !IsLineBlocked(EyePosition, PlayerEyePosition);
                float visibility = lineClear ? Visibility(bb.PlayerSpeed) : 0f;

                if (visibility > 0f)
                {
                    bb.CanSeePlayer = true;
                    bb.Awareness += sightGainPerSecond * visibility * dt;
                    bb.TargetPosition = Player.position;
                    bb.HasTarget = true;
                    sensedSomething = true;
                }
                bb.PlayerSeesMe = lineClear && PlayerIsLookingAtMe();
            }
        }
        else
        {
            bb.DistanceToPlayer = float.MaxValue;
        }

        // ---- hearing: apply the loudest noise heard since last frame ----
        if (pendingNoise)
        {
            bb.HeardNoise = true;
            bb.NoisePosition = pendingNoisePos;
            bb.NoiseStrength = pendingNoiseStrength;
            bb.Awareness += pendingNoiseStrength * hearingGain;
            if (!bb.CanSeePlayer)                    // sight is more reliable than sound
            {
                bb.TargetPosition = pendingNoisePos;
                bb.HasTarget = true;
            }
            LastNoisePos = pendingNoisePos;
            LastNoiseStrength = pendingNoiseStrength;
            LastNoiseSource = pendingNoiseSource;
            LastNoiseUsedPath = pendingUsedPath;
            LastNoiseMasked = pendingMasked;
            LastNoiseTime = Time.time;
            sensedSomething = true;
            pendingNoise = false;
            pendingNoiseStrength = 0f;
        }

        // ---- memory: slowly forget ----
        if (sensedSomething) lastStimulusTime = Time.time;
        else if (Time.time - lastStimulusTime > decayDelay) bb.Awareness -= decayPerSecond * dt;

        // Very close and not behind a wall: you can feel someone breathing down your neck.
        if (bb.CanSeePlayer && bb.DistanceToPlayer <= closeSenseRange)
            bb.Awareness = Mathf.Max(bb.Awareness, 1f);

        bb.Awareness = Mathf.Clamp01(bb.Awareness);
    }

    // 0 = cannot see, otherwise how strongly the player stands out (0..1)
    float Visibility(float playerSpeed)
    {
        Vector3 toPlayer = PlayerEyePosition - EyePosition;
        float dist = toPlayer.magnitude;
        if (dist > viewDistance) return 0f;

        float angle = Vector3.Angle(transform.forward, toPlayer);
        bool close = dist <= closeSenseRange;
        if (angle > viewAngle * 0.5f && !close) return 0f;

        float distanceFactor = Mathf.Max(0.1f, 1f - dist / viewDistance);           // near = easy to see
        float centreFactor   = close ? 1f : Mathf.Lerp(1f, 0.4f, angle / (viewAngle * 0.5f)); // edge of view = harder
        float motionFactor   = 0.5f + 0.5f * Mathf.Clamp01(playerSpeed / 5f);       // still = harder to notice
        return distanceFactor * centreFactor * motionFactor;
    }

    bool PlayerIsLookingAtMe()
    {
        if (playerCam == null) return false;
        Vector3 toMe = (transform.position + Vector3.up * eyeHeight) - playerCam.position;
        return Vector3.Angle(playerCam.forward, toMe) <= playerViewAngle * 0.5f;
    }

    // =================================================================
    //  HEARING - called by NoiseSystem whenever anything makes a sound
    // =================================================================
    void OnNoise(NoiseEvent e)
    {
        if (deaf || !isActiveAndEnabled) return;

        Vector3 ears = EyePosition;
        float straight = Vector3.Distance(e.Position, ears);
        if (straight > hearingRange) return;                          // cheap early-out

        // Route 1: straight through walls (muffled if something is in the way)
        float through = Falloff(e.Loudness, straight);
        if (IsLineBlocked(e.Position + Vector3.up * 0.1f, ears)) through *= wallMuffle;

        // Route 2: around the walls along walkable space (NavMesh path length)
        float around = 0f;
        float pathLength;
        if (TryPathLength(e.Position, transform.position, out pathLength))
            around = Falloff(e.Loudness, pathLength);

        bool usedPath = around > through;
        float perceived = Mathf.Max(through, around);
        bool masked = IsMasked(e.Position);
        if (masked) perceived *= maskFactor;                       // fan noise drowns it out
        if (perceived < minAudible) return;

        if (!pendingNoise || perceived > pendingNoiseStrength)
        {
            pendingNoise = true;
            pendingNoisePos = e.Position;
            pendingNoiseStrength = perceived;
            pendingNoiseSource = e.Source;
            pendingUsedPath = usedPath;
            pendingMasked = masked;
        }
    }

    float Falloff(float loudness, float distance)
    {
        float k = distance / referenceDistance;
        return loudness / (1f + k * k);
    }

    // Total length of the NavMesh path between two points. False if there is no complete path
    // (e.g. a closed door carves the NavMesh) - then only the "through the wall" route counts.
    bool TryPathLength(Vector3 from, Vector3 to, out float length)
    {
        length = 0f;
        NavMeshHit a, b;
        if (!NavMesh.SamplePosition(from, out a, 2f, NavMesh.AllAreas)) return false;
        if (!NavMesh.SamplePosition(to, out b, 2f, NavMesh.AllAreas)) return false;
        if (!NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path)) return false;
        if (path.status != NavMeshPathStatus.PathComplete) return false;

        Vector3[] corners = path.corners;
        for (int i = 1; i < corners.Length; i++) length += Vector3.Distance(corners[i - 1], corners[i]);
        return true;
    }

    // =================================================================
    //  LINE OF SIGHT - true if a solid object is between the two points
    // =================================================================
    public bool IsLineBlocked(Vector3 from, Vector3 to)
    {
        Vector3 delta = to - from;
        float len = delta.magnitude;
        if (len < 0.01f) return false;

        int n = Physics.RaycastNonAlloc(from, delta / len, hits, len, occluders, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            Collider c = hits[i].collider;
            Transform tr = c.transform;
            if (tr.IsChildOf(transform)) continue;                                   // myself
            if (Player != null && tr.IsChildOf(Player)) continue;                    // the player
            if (c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic) continue; // rocks, debris
            if (c.GetComponentInParent<NavMeshAgent>() != null) continue;            // other agents
            return true;
        }
        return false;
    }

    void FindPlayerIfNeeded()
    {
        if (Player != null && PlayerStats != null && playerCam != null) return;
        if (Time.time < nextPlayerSearch) return;
        nextPlayerSearch = Time.time + 1f;                       // do not search every frame

        if (PlayerStats == null)
        {
            PlayerStats = FindFirstObjectByType<PlayerStats>();
            Player = PlayerStats != null ? PlayerStats.transform : null;
            hasLastPlayerPos = false;
        }
        if (Camera.main != null) playerCam = Camera.main.transform;
    }
}
