using UnityEngine;

public class SpawnDirector : MonoBehaviour
{
    public enum DirectorPhase
    {
        BuildUp,
        Peak,
        Relax
    }

    [Header("Prototype Inputs - Set Manually")]
    public bool gameplayActive = true;
    public bool playerAlive = true;

    [Range(0f, 1f)] public float health01 = 1f;
    [Range(0f, 1f)] public float ammo01 = 1f;
    [Min(0)] public int nearbyEnemies = 0;
    [Min(1)] public int enemyCountForMaxStress = 5;

    [Header("Stress Settings")]
    [Min(0.01f)] public float smoothingSpeed = 2f;
    [Range(0f, 1f)] public float highStressThreshold = 0.75f;
    [Range(0f, 1f)] public float recoveryThreshold = 0.35f;

    [Header("Phase Durations - Seconds")]
    [Min(1f)] public float buildUpDuration = 20f;
    [Min(1f)] public float peakDuration = 10f;
    [Min(1f)] public float minimumRelaxDuration = 8f;

    [Header("Runtime Status - Observe During Play")]
    [SerializeField] private float targetStress;
    [SerializeField] private float smoothedStress;
    [SerializeField] private float phaseTimer;
    [SerializeField] private DirectorPhase currentPhase;

    public DirectorPhase CurrentPhase => currentPhase;
    public float Stress => smoothedStress;

    private void Start()
    {
        currentPhase = DirectorPhase.BuildUp;
        phaseTimer = 0f;
        targetStress = CalculateStress();
        smoothedStress = targetStress;
    }

    private void Update()
    {
        if (!gameplayActive || !playerAlive)
            return;

        targetStress = CalculateStress();

        float blend = 1f - Mathf.Exp(
            -Mathf.Max(0.01f, smoothingSpeed) * Time.deltaTime
        );

        smoothedStress = Mathf.Lerp(
            smoothedStress,
            targetStress,
            blend
        );

        phaseTimer += Time.deltaTime;

        if (currentPhase != DirectorPhase.Relax &&
            smoothedStress >= highStressThreshold)
        {
            ChangePhase(DirectorPhase.Relax);
            return;
        }

        switch (currentPhase)
        {
            case DirectorPhase.BuildUp:
                if (phaseTimer >= buildUpDuration)
                    ChangePhase(DirectorPhase.Peak);
                break;

            case DirectorPhase.Peak:
                if (phaseTimer >= peakDuration)
                    ChangePhase(DirectorPhase.Relax);
                break;

            case DirectorPhase.Relax:
                if (phaseTimer >= minimumRelaxDuration &&
                    smoothedStress <= recoveryThreshold)
                {
                    ChangePhase(DirectorPhase.BuildUp);
                }
                break;
        }
    }

    private float CalculateStress()
    {
        float healthStress = 1f - Mathf.Clamp01(health01);
        float ammoStress = 1f - Mathf.Clamp01(ammo01);

        float enemyStress = Mathf.Clamp01(
            nearbyEnemies / (float)Mathf.Max(1, enemyCountForMaxStress)
        );

        return Mathf.Clamp01(
            healthStress * 0.5f +
            ammoStress * 0.2f +
            enemyStress * 0.3f
        );
    }

    private void ChangePhase(DirectorPhase nextPhase)
    {
        currentPhase = nextPhase;
        phaseTimer = 0f;

        Debug.Log(
            $"[SpawnDirector] Phase: {currentPhase}, Stress: {smoothedStress:F2}",
            this
        );
    }
}