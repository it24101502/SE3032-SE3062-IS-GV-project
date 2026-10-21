using UnityEngine;

public enum BossAction { Attack, Retreat, Flank, TakeCover }

// Utility AI for the Adaptive Boss.
// Pure logic: no GameObjects, no scene access. BossController feeds it numbers
// and acts on the answer, so it can be unit-tested and explained on its own.
public class BossBrain{
    // ---- Anti-flicker (hysteresis) settings ----
    public float minCommitTime = 1.0f;   // must stay in an action at least this long
    public float switchMargin  = 0.15f;  // a new action must beat the current one by this much

    // ---- Anti-stalemate: the boss gets tired of circling / hiding ----
    public float fatigueStart    = 6f;    // seconds in Flank/TakeCover before it starts losing appeal
    public float fatigueRate     = 0.05f; // score lost per extra second
    public float fatigueMax      = 0.8f;  // largest penalty
    public float fatigueRecovery = 0.7f;  // fatigue drained per second spent doing something else

    // ---- Aggression: the boss charges now and then, and commits to it ----
    public float pressureDelay    = 10f;  // seconds without attacking before the urge to attack starts
    public float pressureRamp     = 6f;   // seconds for the urge to reach full strength
    public float pressureMax      = 0.5f; // largest Attack bonus (scaled down when the boss is hurt)
    public float attackBurstTime  = 6f;   // once charging, stay in Attack at least this long...
    public float burstMinBossHealth = 0.15f; // ...unless the boss is below this health fraction

    // ---- Desperation: wounded boss breaks out of hiding after a long stalemate ----
    public float desperationDelay = 30f;  // seconds without attacking before even a wounded boss gets desperate
    public float desperationRamp  = 15f;
    public float desperationBonus = 0.6f;
    bool desperateCharge = false;

    bool hasChosen = false;
    float timeInCurrent = 0f;
    float timeSinceAttack = 0f;
    readonly float[] fatigue = new float[4];   // only Flank and TakeCover use it

    public float[] LastScores = new float[4];   // for the debug overlay and CSV log
    public BossAction Current = BossAction.Attack;

    // coverAvailability: 1 = at least one cover point is hidden from the player right now,
    //                    0 = nowhere to hide. Supplied by BossController (line-of-sight check).
    public BossAction Evaluate(bool playerHasRanged, float playerHealth,
                               float bossHealth, float distance, float deltaTime,
                               float coverAvailability = 1f){
        // ---------- Considerations (each normalised to 0..1) ----------
        float closeness       = 1f - Mathf.Clamp01(distance / 20f);                         // 1 = touching, 0 = 20m+ away
        float mediumBand      = 1f - Mathf.Clamp01(Mathf.Abs(distance - 10f) / 10f);        // peaks at 10m
        float bossHurt        = 1f - bossHealth;
        float retreatUrgency  = bossHurt * bossHurt;                                        // grows fast when badly hurt
        float playerHurt      = 1f - playerHealth;
        float rangedThreat    = playerHasRanged ? 0.9f : 0.1f;                              // reacts to the player's weapon
        float courage         = 1f - 0.6f * bossHurt;                                       // a wounded boss is less willing to flank
        float coverGate       = 0.25f + 0.75f * Mathf.Clamp01(coverAvailability);           // no hiding place -> cover is much less attractive

        // How much a wounded boss still wants to fight (1 = healthy, 0.4 = nearly dead)
        float resolve = 0.4f + 0.6f * Mathf.Clamp01(bossHealth);
        // Urge to attack grows the longer the boss has gone without attacking
        float pressure = Mathf.Clamp01((timeSinceAttack - pressureDelay) / Mathf.Max(0.01f, pressureRamp));

        // ---------- Action scores (weighted sums) ----------
        LastScores[(int)BossAction.Attack]    = 0.4f * bossHealth + 0.4f * closeness + 0.2f * playerHurt
                                                + pressureMax * resolve * pressure 
                                                + desperationBonus * Mathf.Clamp01((timeSinceAttack - desperationDelay) / Mathf.Max(0.01f, desperationRamp));
        LastScores[(int)BossAction.Retreat]   = 0.7f * retreatUrgency + 0.3f * closeness;
        LastScores[(int)BossAction.Flank]     = (0.5f * rangedThreat + 0.5f * mediumBand) * courage
                                                - FatiguePenalty(BossAction.Flank);
        LastScores[(int)BossAction.TakeCover] = (0.6f * rangedThreat + 0.4f * bossHurt) * coverGate
                                                - FatiguePenalty(BossAction.TakeCover);

        for (int i = 0; i < 4; i++)
            if (LastScores[i] < 0f) LastScores[i] = 0f;

        // ---------- Find the best-scoring action ----------
        int best = 0;
        for (int i = 1; i < 4; i++)
            if (LastScores[i] > LastScores[best]) best = i;

        // First call: take the best action straight away
        if (!hasChosen){
            Current = (BossAction)best;
            hasChosen = true;
            timeInCurrent = 0f;
            TrackTime(deltaTime);
            return Current;
        }

        // ---------- Anti-flicker rule ----------
        timeInCurrent += deltaTime;
        bool heldLongEnough = timeInCurrent >= minCommitTime;
        bool clearlyBetter  = LastScores[best] > LastScores[(int)Current] + switchMargin;

        // Once the boss has started a charge it follows through (unless it is nearly dead)
                float lockTime = desperateCharge ? attackBurstTime * 2f : attackBurstTime;
        bool chargeLocked = Current == BossAction.Attack
                            && timeInCurrent < lockTime
                            && (bossHealth > burstMinBossHealth || desperateCharge);

        if (best != (int)Current && heldLongEnough && clearlyBetter && !chargeLocked){
            if ((BossAction)best == BossAction.Attack)
                desperateCharge = timeSinceAttack > desperationDelay;   // a last-ditch rush stays committed even at low HP
            Current = (BossAction)best;
            timeInCurrent = 0f;
        }

        TrackTime(deltaTime);
        return Current;
    }

    // Penalty grows after fatigueStart seconds of Flank/TakeCover and drains slowly afterwards
    float FatiguePenalty(BossAction a){
        return Mathf.Min(fatigueMax, fatigueRate * Mathf.Max(0f, fatigue[(int)a] - fatigueStart));
    }

    void TrackTime(float dt){
        timeSinceAttack = Current == BossAction.Attack ? 0f : timeSinceAttack + dt;
        UpdateFatigue(BossAction.Flank, dt);
        UpdateFatigue(BossAction.TakeCover, dt);
    }

    void UpdateFatigue(BossAction a, float dt){
        int i = (int)a;
        if (Current == a) fatigue[i] += dt;
        else fatigue[i] = Mathf.Max(0f, fatigue[i] - fatigueRecovery * dt);
    }
}