using UnityEngine;

public enum BossAction { Attack, Retreat, Flank, TakeCover }

// Pure logic: no GameObjects, exactly as in the design document.
public class BossBrain{
    public float minCommitTime = 1.0f;
    public float switchMargin = 0.15f;
    bool hasChosen = false;

    public float[] LastScores = new float[4];   // for the debug overlay
    public BossAction Current = BossAction.Attack;
    float timeInCurrent = 0f;

    public BossAction Evaluate(bool playerHasRanged, float playerHealth,
                               float bossHealth, float distance, float deltaTime){
        // Considerations
        float closeness = 1f - Mathf.Clamp01(distance / 20f);
        float mediumBand = 1f - Mathf.Clamp01(Mathf.Abs(distance - 10f) / 10f);
        float bossHurt = 1f - bossHealth;
        float retreatUrgency = bossHurt * bossHurt;
        float playerHurt = 1f - playerHealth;
        float rangedThreat = playerHasRanged ? 0.9f : 0.1f;

        // Action scores (weights from section 5)
        LastScores[(int)BossAction.Attack]    = 0.4f * bossHealth + 0.4f * closeness + 0.2f * playerHurt;
        LastScores[(int)BossAction.Retreat]   = 0.7f * retreatUrgency + 0.3f * closeness;
        LastScores[(int)BossAction.Flank]     = 0.5f * rangedThreat + 0.5f * mediumBand;
        LastScores[(int)BossAction.TakeCover] = 0.6f * rangedThreat + 0.4f * bossHurt;

        // Find the best action
        int best = 0;
        for (int i = 1; i < 4; i++)
            if (LastScores[i] > LastScores[best]) best = i;

        // First call: just take the best action straight away
        if (!hasChosen){
            Current = (BossAction)best;
            hasChosen = true;
            timeInCurrent = 0f;
            return Current;
        }

        // Anti-flicker rule (section 6)
        timeInCurrent += deltaTime;
        bool heldLongEnough = timeInCurrent >= minCommitTime;
        bool clearlyBetter = LastScores[best] > LastScores[(int)Current] + switchMargin;
        if (best != (int)Current && heldLongEnough && clearlyBetter){
            Current = (BossAction)best;
            timeInCurrent = 0f;
        }
        return Current;
    }
}
