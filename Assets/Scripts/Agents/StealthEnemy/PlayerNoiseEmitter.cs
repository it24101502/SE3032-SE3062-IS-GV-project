using UnityEngine;

// =====================================================================
//  PlayerNoiseEmitter  -  [IS] Stealth Enemy (Peiris)
//  Put this on the Player. It turns what the player does into noises on
//  the NoiseSystem so the Stealth Enemy can hear them:
//     footsteps (one per 'stepDistance' metres walked)
//        sneak (hold Left Ctrl) = almost silent, but slower
//        walk                   = quiet
//        sprint (Shift)         = loud
//     ranged shot  = very loud      melee swing = quiet
//  It only READS the Systems Engineer's PlayerStats / PlayerMovement,
//  so their scripts do not need to change.
// =====================================================================
[RequireComponent(typeof(CharacterController))]
public class PlayerNoiseEmitter : MonoBehaviour
{
    [Header("Footsteps")]
    public float stepDistance = 1.8f;
    public float sneakLoudness = 0.05f;
    public float walkLoudness = 0.35f;
    public float sprintLoudness = 0.9f;
    [Tooltip("Horizontal speed above which a step counts as sprinting.")]
    public float sprintSpeedThreshold = 6.5f;

    [Header("Sneaking")]
    public KeyCode sneakKey = KeyCode.LeftControl;
    [Tooltip("Slow the player down while sneaking (multiplies PlayerMovement.speed).")]
    public bool sneakSlowsPlayer = true;
    [Range(0.1f, 1f)] public float sneakSpeedMultiplier = 0.45f;

    [Header("Weapons")]
    public float gunshotLoudness = 3f;
    public float meleeLoudness = 0.5f;

    public bool IsSneaking { get; private set; }

    CharacterController cc;
    PlayerStats stats;
    PlayerMovement movement;
    float baseSpeed;
    float walked;
    float nextShotNoise;

    void Start()
    {
        cc = GetComponent<CharacterController>();
        stats = GetComponent<PlayerStats>();
        movement = GetComponent<PlayerMovement>();
        if (movement != null) baseSpeed = movement.speed;
    }

    void Update()
    {
        if (stats != null && stats.IsDead) return;

        // ---- sneaking ----
        IsSneaking = Input.GetKey(sneakKey);
        if (movement != null && sneakSlowsPlayer)
            movement.speed = IsSneaking ? baseSpeed * sneakSpeedMultiplier : baseSpeed;

        // ---- footsteps: one noise every stepDistance metres on the ground ----
        Vector3 v = cc.velocity;
        v.y = 0f;
        float speed = v.magnitude;
        if (cc.isGrounded && speed > 0.3f)
        {
            walked += speed * Time.deltaTime;
            if (walked >= stepDistance)
            {
                walked = 0f;
                float loud = IsSneaking ? sneakLoudness : (speed > sprintSpeedThreshold ? sprintLoudness : walkLoudness);
                NoiseSystem.Emit(transform.position, loud, NoiseSource.Footstep);
            }
        }

        // ---- weapons (same button and cooldowns as PlayerStats) ----
        if (stats != null && Input.GetMouseButtonDown(0) && Time.time >= nextShotNoise)
        {
            if (stats.weapon == WeaponType.Ranged)
            {
                nextShotNoise = Time.time + stats.rangedCooldown;
                NoiseSystem.Emit(transform.position, gunshotLoudness, NoiseSource.Gunshot);
            }
            else
            {
                nextShotNoise = Time.time + stats.meleeCooldown;
                NoiseSystem.Emit(transform.position, meleeLoudness, NoiseSource.Melee);
            }
        }
    }

    void OnDisable()
    {
        if (movement != null && sneakSlowsPlayer && baseSpeed > 0f) movement.speed = baseSpeed;   // never leave the player slowed
    }
}
