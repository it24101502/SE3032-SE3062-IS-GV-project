using UnityEngine;

// =====================================================================
//  StealthDroneVisuals  -  [GV] Core Developer (Peiris)
//  Visual feedback for the custom "Stalker Drone" model.
//    * The eye lens glows in the colour of the AI state
//      (green patrol, yellow investigate, orange hunting, red attack...)
//      and gets brighter as awareness rises.
//    * A small point light in the eye casts that colour on the walls.
//    * The model hovers (gentle bob) and tilts into its movement.
//
//  It only LISTENS to StealthEnemyController - the AI code knows nothing
//  about this script (AI logic decoupled from visuals).
//
//  Hierarchy expected:
//    StealthEnemy            (NavMeshAgent, StealthEnemyController, collider)
//      └─ Visual             (empty, this script can live on the root)
//           └─ SM_StalkerDrone (the imported FBX)
//  'visualRoot' = the "Visual" child, so bobbing never moves the NavMeshAgent.
// =====================================================================
public class StealthDroneVisuals : MonoBehaviour
{
    [Header("References")]
    public StealthEnemyController controller;
    [Tooltip("Renderer of the eye/lens part. Its material must have Emission ticked.")]
    public Renderer eyeRenderer;
    [Tooltip("Material slot index of the eye material on that renderer.")]
    public int eyeMaterialIndex = 0;
    public Light eyeLight;
    [Tooltip("Child that holds the model. Bobbing/tilting is applied here, not on the agent.")]
    public Transform visualRoot;

    [Header("Glow")]
    public float minIntensity = 1.5f;
    public float maxIntensity = 6f;
    public float colourBlendSpeed = 6f;

    [Header("Hover")]
    public float bobHeight = 0.08f;
    public float bobSpeed = 2.2f;
    public float maxTilt = 12f;          // degrees
    public float tiltSmoothing = 5f;
    [Tooltip("How far the model drops when the drone is destroyed (its hover height).")]
    public float deadDrop = 0.7f;

    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    MaterialPropertyBlock mpb;
    Color currentColour;
    Vector3 baseLocalPos;
    Quaternion baseLocalRot;
    Vector3 lastPos;
    Vector3 smoothedVelocity;

    void Awake()
    {
        if (controller == null) controller = GetComponentInParent<StealthEnemyController>();
        mpb = new MaterialPropertyBlock();
        if (visualRoot != null)
        {
            baseLocalPos = visualRoot.localPosition;
            baseLocalRot = visualRoot.localRotation;
        }
        lastPos = transform.position;
        currentColour = StealthEnemyController.ColorFor(StealthStateId.Patrol);
    }

    void Start()
    {
        if (controller != null) controller.tintRendererByState = false;   // the model uses the eye glow instead
    }

    void LateUpdate()
    {
        if (controller == null) return;
        float dt = Time.deltaTime;

        // ---- eye colour + brightness ----
        Color target = StealthEnemyController.ColorFor(controller.State);
        currentColour = Color.Lerp(currentColour, target, 1f - Mathf.Exp(-colourBlendSpeed * dt));
        float intensity = Mathf.Lerp(minIntensity, maxIntensity, controller.Awareness);
        if (controller.State == StealthStateId.Dead) intensity = 0f;

        if (eyeRenderer != null)
        {
            // MaterialPropertyBlock = per-object colour without duplicating the material (keeps batching)
            eyeRenderer.GetPropertyBlock(mpb, eyeMaterialIndex);
            mpb.SetColor(EmissionId, currentColour * intensity);
            eyeRenderer.SetPropertyBlock(mpb, eyeMaterialIndex);
        }
        if (eyeLight != null)
        {
            eyeLight.color = currentColour;
            eyeLight.intensity = intensity * 0.5f;
            eyeLight.enabled = intensity > 0f;
        }

        // ---- hover bob + tilt into movement ----
        if (visualRoot == null || dt <= 0f) return;
        Vector3 velocity = (transform.position - lastPos) / dt;
        lastPos = transform.position;
        smoothedVelocity = Vector3.Lerp(smoothedVelocity, velocity, 1f - Mathf.Exp(-tiltSmoothing * dt));

        bool dead = controller.State == StealthStateId.Dead;
        float bob = dead ? 0f : Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        visualRoot.localPosition = baseLocalPos + Vector3.up * (dead ? -deadDrop : bob);

        // Lean forward when moving (local space), like a drone pitching into its flight direction.
        Vector3 local = transform.InverseTransformDirection(smoothedVelocity);
        float pitch = Mathf.Clamp(local.z * 2.5f, -maxTilt, maxTilt);
        float roll = Mathf.Clamp(-local.x * 2.5f, -maxTilt, maxTilt);
        if (dead) { pitch = 35f; roll = 20f; }                            // crashed drone
        visualRoot.localRotation = baseLocalRot * Quaternion.Euler(pitch, 0f, roll);
    }
}
