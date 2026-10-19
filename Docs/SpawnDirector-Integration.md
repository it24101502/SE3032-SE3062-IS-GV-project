# Spawn Director - Integration and Prototype Tests

Owner: Premarathna P A I B - IT24101844
Module: SE3062 Intelligent Systems

## Files
- Assets/Scripts/Agents/SpawnDirector.cs
- Assets/Scenes/TestScenes/SpawnDirectorTest.unity
- Assets/Prefabs/Agents/EnemyAgent.prefab

## Decision process
1. Read player health, ammunition and nearby owned enemies.
2. Calculate stress:
   0.5 * health deficit + 0.2 * ammo deficit
   + 0.3 * normalized nearby enemy count.
3. Smooth stress over time.
4. Switch between BuildUp, Peak and Relax.
5. Pause spawning during Relax, inactive gameplay, player death
   or when the active enemy limit is reached.
6. Select an eligible enemy profile closest to target pressure.
7. Reject blocked, too-close, camera-visible or unreachable points.
8. Score remaining points using NavMesh path distance (70%)
   and spacing from owned enemies (30%).
9. Spawn one enemy at the highest-scoring valid point.

## Player integration
The player/game-state controller must call:

director.UpdatePlayerState(
    normalizedHealth,
    normalizedAmmo,
    playerIsAlive,
    gameplayIsActive
);

Health and ammo must be normalized to 0..1.
Call regularly as state changes, including death and game pause.
Assign the actual player Transform and gameplay Camera.

## Enemy integration
Assign actual enemy prefab assets to Enemy Types.
Keep Enemy Prefab assigned as the default prefab.
Configure preferred pressure and Peak Only for each profile.

The director currently tracks enemies that it creates.
Destroyed or inactive enemies are removed from its count.
If death leaves an active corpse, disable or destroy its tracked
root object, or add explicit death notification during integration.
AgentMovement.isAlive alone does not remove it from the count.

Enemy movement and combat must be configured separately.

## Level setup
Provide a baked NavMesh and valid spawn-point Transforms.
Current prototype spawn roots are at Y = 1.
Spawn clearance assumes a centered upright capsule:
radius 0.5, height 2.

Assign solid blocking walls/props to SpawnObstacle.
Select that layer in Spawn Blocking Layers.
Keep Ground out of that blocking mask.

## Manual prototype results
Reported passed by the developer in SpawnDirectorTest:
- Active enemy limit of 3.
- No replacement spawn while player is dead.
- No replacement spawn while gameplay is inactive.
- Spawning resumes during an eligible phase.
- High stress enters Relax.
- Recovery returns to BuildUp.
- Blocking cube prevents spawning at the isolated test point.
- Disabling that cube permits spawning.
- Light and Heavy profile selection.

## Current limitations
- Real player health/ammo integration is pending.
- Both enemy profiles currently use the same capsule prefab.
- Actual enemy combat and death integration are pending.
- Nearby counting includes only director-owned active enemies.
- Visibility checks the spawn point against the camera viewport;
  wall occlusion and full enemy bounds are not evaluated.
- Integrated bunker testing and difficulty tuning are pending.
