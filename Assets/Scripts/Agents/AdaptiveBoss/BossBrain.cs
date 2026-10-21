using UnityEngine;

public enum BossAction { Attack, Retreat, Flank, TakeCover }

// Utility AI for the Adaptive Boss.
// Pure logic: no GameObjects, no scene access. BossController feeds it numbers
// and acts on the answer, so it can be unit-tested and explained on its own.
public class BossBrain{
    // ---- Anti-flicker (hysteresis) settings ----
    public float minCommitTime = 1.0f;   // must stay in an action at least this long
    public float switchMargin  = 0.15f;  // a new action must beat the current one by this much

    bool hasChosen = false;
    float timeInCurrent = 0f;

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

        // ---------- Action scores (weighted sums) ----------
        LastScores[(int)BossAction.Attack]    = 0.4f * bossHealth + 0.4f * closeness + 0.2f * playerHurt;
        LastScores[(int)BossAction.Retreat]   = 0.7f * retreatUrgency + 0.3f * closeness;
        LastScores[(int)BossAction.Flank]     = (0.5f * rangedThreat + 0.5f * mediumBand) * courage;
        LastScores[(int)BossAction.TakeCover] = (0.6f * rangedThreat + 0.4f * bossHurt) * coverGate;

        if (hasChosen && Current == BossAction.TakeCover)
            LastScores[(int)BossAction.TakeCover] -= Mathf.Min(0.5f, 0.02f * Mathf.Max(0f, timeInCurrent - 10f));
        
        // ---------- Find the best-scoring action ----------
        int best = 0;
        for (int i = 1; i < 4; i++)
            if (LastScores[i] > LastScores[best]) best = i;

        // First call: take the best action straight away
        if (!hasChosen){
            Current = (BossAction)best;
            hasChosen = true;
            timeInCurrent = 0f;
            return Current;
        }

        // ---------- Anti-flicker rule ----------
        timeInCurrent += deltaTime;
        bool heldLongEnough = timeInCurrent >= minCommitTime;
        bool clearlyBetter  = LastScores[best] > LastScores[(int)Current] + switchMargin;
        if (best != (int)Current && heldLongEnough && clearlyBetter){
            Current = (BossAction)best;
            timeInCurrent = 0f;
        }
        return Current;
    }
}
