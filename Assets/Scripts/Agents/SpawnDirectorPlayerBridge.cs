using UnityEngine;

// Feeds real gameplay state into the director before its Update runs.
[DefaultExecutionOrder(-200)]
[DisallowMultipleComponent]
[RequireComponent(typeof(SpawnDirector))]
public sealed class SpawnDirectorPlayerBridge : MonoBehaviour
{
    public PlayerStats playerStats;
    public PlayerThrower playerThrower;
    public GameFlow gameFlow;

    [Tooltip("Capacity of the throwable-rock supply, not gun ammunition.")]
    [Min(1)] public int rockAmmoCapacity = 5;
    [Tooltip("Set false from a menu or game-state controller to pause the director.")]
    public bool gameplayActive = true;

    private SpawnDirector director;

    private void Awake()
    {
        director = GetComponent<SpawnDirector>();
        if (playerThrower == null && playerStats != null)
            playerThrower = playerStats.GetComponent<PlayerThrower>();

        if (playerStats == null || playerThrower == null)
            Debug.LogWarning("[SpawnDirectorPlayerBridge] Assign PlayerStats and PlayerThrower. Spawning is paused until both are available.", this);

        FeedState();
    }

    private void Update()
    {
        FeedState();
    }

    private void FeedState()
    {
        if (director == null)
            return;

        if (playerStats == null || playerThrower == null)
        {
            director.UpdatePlayerState(0f, 0f, false, false);
            return;
        }

        bool alive = !playerStats.IsDead;
        bool bossDefeated = gameFlow != null &&
            gameFlow.boss != null && gameFlow.boss.IsDead;
        bool active = gameplayActive && Time.timeScale > 0f &&
            playerStats.isActiveAndEnabled && !bossDefeated;

        float health = playerStats.maxHealth > 0f
            ? playerStats.HealthFraction : 0f;
        float ammo = Mathf.Clamp01(
            playerThrower.ammo / (float)Mathf.Max(1, rockAmmoCapacity));

        director.UpdatePlayerState(health, ammo, alive, active);
    }

    private void OnDisable()
    {
        if (director != null)
            director.UpdatePlayerState(director.health01, director.ammo01,
                director.playerAlive, false);
    }
}
