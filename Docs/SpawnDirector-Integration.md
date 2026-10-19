\# Spawn Director — Integration and Prototype Tests



\*\*Owner:\*\* Premarathna P A I B — IT24101844  

\*\*Module:\*\* SE3062 — Intelligent Systems  

\*\*Status:\*\* Standalone prototype tested; game integration pending.



\## Files



\- `Assets/Scripts/Agents/SpawnDirector.cs`

\- `Assets/Scenes/TestScenes/SpawnDirectorTest.unity`

\- `Assets/Prefabs/Agents/EnemyAgent.prefab`



\## Architecture



The Spawn Director combines a Finite State Machine with utility-based

enemy and spawn-point selection.



\- \*\*BuildUp:\*\* Introduces enemies at the configured BuildUp interval.

\- \*\*Peak:\*\* Uses the configured Peak interval and permits Peak-only profiles.

\- \*\*Relax:\*\* Suspends new spawns to provide a recovery period.



High stress can trigger Relax early. Recovery requires both the minimum

Relax duration and stress below the recovery threshold.



\## Decision Process



1\. Remove destroyed or inactive enemies from tracking.

2\. Count nearby director-owned enemies when automatic counting is enabled.

3\. Stop spawning if gameplay is inactive or the player is dead.

4\. Calculate and smooth player stress.

5\. Update the BuildUp, Peak or Relax phase.

6\. Stop spawning during Relax or when the active enemy limit is reached.

7\. Calculate the spawn interval and wait until it elapses.

8\. Calculate target pressure and select an eligible enemy profile.

9\. Reject blocked, too-close, camera-visible or unreachable spawn points.

10\. Score valid points using path-distance fit and enemy spacing.

11\. Spawn one enemy at the highest-scoring point.

12\. Update tracking and repeat.



If no eligible enemy profile or valid spawn point exists, no enemy is

spawned. The director retries after the next spawn interval.



\## Stress Calculation



Inputs are normalized to the range `0..1`.



```text

HealthStress = 1 - Health01

AmmoStress   = 1 - Ammo01

EnemyStress  = Clamp01(NearbyEnemies / EnemyCountForMaxStress)



TargetStress = Clamp01(

&#x20;   0.5 \* HealthStress

&#x20; + 0.2 \* AmmoStress

&#x20; + 0.3 \* EnemyStress

)

```



Stress is smoothed using a frame-time-based exponential blend:



```text

Blend = 1 - exp(-SmoothingSpeed \* DeltaTime)



SmoothedStress = Lerp(

&#x20;   SmoothedStress,

&#x20;   TargetStress,

&#x20;   Blend

)

```



The weights and thresholds are prototype tuning values. They have not

yet been validated through integrated gameplay balancing.



\## Spawn Timing and Enemy Selection



```text

BaseInterval = PeakSpawnInterval during Peak;

&#x20;              BuildUpSpawnInterval otherwise.



CurrentSpawnInterval = Max(

&#x20;   0.1,

&#x20;   BaseInterval \* (1 + SmoothedStress)

)

```



Higher stress increases the interval between spawns. Relax prevents

spawning entirely.



Enemy profile selection uses:



```text

PhasePressure = 0.8 during Peak; 0.35 otherwise.



TargetPressure = PhasePressure \* (1 - SmoothedStress)



EnemyScore = 1 - Abs(TargetPressure - PreferredPressure)

```



The director selects the highest-scoring eligible profile.



\- Profiles without a prefab are skipped.

\- Peak-only profiles are eligible only during Peak.

\- `Enemy Prefab` is used when `Enemy Types` is empty.

\- Configured enemy types can spawn without a default prefab.

\- If configured profiles exist but none are eligible, spawning is skipped.



\## Spawn-Point Selection



A candidate must pass the following checks:



\- Minimum horizontal distance from the player.

\- Camera viewport rejection, when enabled.

\- Complete NavMesh path to the player.

\- Capsule clearance against the configured blocking layers.

\- Minimum spacing from tracked enemies.



Valid candidates are scored as follows:



```text

PreferredDistance = Max(

&#x20;   MinimumPlayerDistance,

&#x20;   PreferredPlayerDistance

)



DistanceScore = 1 / (

&#x20;   1 + Abs(PathDistance - PreferredDistance)

)



SpacingScore = Clamp01(

&#x20;   NearestEnemyDistance / Max(0.1, PreferredDistance)

)



SpawnScore = 0.7 \* DistanceScore + 0.3 \* SpacingScore

```



When no tracked enemies exist, `SpacingScore` is `1`.



The highest-scoring point is selected. Equal scores retain the first

candidate encountered.



Unity NavMesh APIs provide path calculation. This prototype does not

implement a custom A\* algorithm.



\## Player Integration



The actual player or game-state controller must call:



```csharp

director.UpdatePlayerState(

&#x20;   normalizedHealth,

&#x20;   normalizedAmmo,

&#x20;   playerIsAlive,

&#x20;   gameplayIsActive

);

```



Integration requirements:



\- Supply health and ammunition normalized to `0..1`.

\- Update the director as player and game state change.

\- Include player death and inactive gameplay.

\- Assign the actual player Transform and gameplay Camera.



This API exists, but an actual player-state caller is not yet connected.



\## Enemy Integration



Assign actual enemy prefab assets to `Enemy Types`.



For each profile, configure:



\- Type Name

\- Prefab

\- Preferred Pressure

\- Peak Only



On enemy death, the enemy system should call:



```csharp

director.NotifyEnemyDeath(enemyRoot);

```



Pass the root GameObject created by this director, not a visual child.



The method:



\- Ignores null or untracked objects.

\- Removes the owned enemy from tracking.

\- Updates the active enemy count.

\- Disables the enemy root.



Nearby enemy counting refreshes on the next Update.



Destroyed or inactive enemies are also removed during normal tracking

updates. Setting `AgentMovement.isAlive` to false alone does not notify

the director.



Connecting the notification to an actual enemy death script is pending.

Enemy movement, combat and corpse presentation require separate setup.



\## Level Setup



\- Provide a baked NavMesh connecting spawn areas to the player.

\- Assign valid spawn-point Transforms.

\- Keep spawn positions aligned with the enemy prefab's root convention.

\- Current prototype spawn roots use `Y = 1` above a floor at `Y = 0`.

\- Current clearance assumes an upright capsule centered on the spawn root:

&#x20; radius `0.5`, height `2`.

\- Adjust clearance dimensions when replacing the prototype prefab.

\- Assign solid blocking wall and prop colliders to `SpawnObstacle`.

\- Select that layer in `Spawn Blocking Layers`.

\- Keep Ground outside the blocking mask.

\- Trigger colliders are ignored by the clearance check.



With viewport rejection enabled, all points inside the assigned camera's

viewport are rejected. If every point is visible, no enemies will spawn.



\## Saved Prototype Settings



| Setting | Value |

|---|---|

| Gameplay Active | Enabled |

| Player Alive | Enabled |

| Health 01 | 1 |

| Ammo 01 | 1 |

| Automatic Enemy Count | Enabled |

| Nearby Enemy Radius | 10 |

| Enemy Count for Max Stress | 5 |

| Smoothing Speed | 2 |

| High Stress Threshold | 0.75 |

| Recovery Threshold | 0.35 |

| BuildUp Duration | 20 seconds |

| Peak Duration | 10 seconds |

| Minimum Relax Duration | 8 seconds |

| Max Active Enemies | 3 |

| BuildUp Spawn Interval | 4 seconds |

| Peak Spawn Interval | 2 seconds |

| Minimum Player Distance | 3 |

| Preferred Player Distance | 6 |

| Minimum Enemy Spacing | 1.5 |

| Reject Visible Points | Enabled |

| Spawn Blocking Layers | SpawnObstacle |

| Spawn Radius | 0.5 |

| Spawn Height | 2 |



\## Manual Prototype Test Results



The following tests were reported passed by the developer in

`SpawnDirectorTest`. These are manual prototype checks, not automated

tests or proof of integrated gameplay.



| Test | Observed Result | Status |

|---|---|---|

| Enemy limit | Active enemies stop at 3 | Passed |

| Player death | No replacement spawns while Player Alive is disabled | Passed |

| Inactive gameplay | No replacement spawns while Gameplay Active is disabled | Passed |

| Resume | Spawning resumes during an eligible phase | Passed |

| High stress | Director enters Relax | Passed |

| Recovery | Director returns to BuildUp after recovery conditions are met | Passed |

| Blocked spawn point | Active blocking cube prevents spawning at the isolated point | Passed |

| Unblocked spawn point | Disabling the cube permits spawning | Passed |

| Enemy profile selection | Light and Heavy profiles are selected | Passed |

| No default prefab | Configured enemy types replace a deleted enemy while Enemy Prefab is None | Passed |



`NotifyEnemyDeath()` has been added, but its connection to an actual

enemy death system has not yet been tested.



\## Current Limitations



\- Real player health and ammunition integration is pending.

\- Light and Heavy profiles currently use the same capsule prefab;

&#x20; distinct combat behaviours have not been demonstrated.

\- Actual enemy combat and death integration are pending.

\- Nearby counting includes only director-owned active enemies.

\- Visibility checks the spawn-point position against the camera viewport.

&#x20; Wall occlusion and full enemy bounds are not evaluated.

\- Clearance dimensions are configured manually.

\- Integrated bunker testing, simultaneous operation with the other

&#x20; agents, performance measurement and difficulty tuning are pending.



\## Integration Acceptance Checks



Before claiming the integrated system is complete:



\- Confirm actual health and ammunition changes affect stress.

\- Confirm player death and inactive gameplay stop new spawns.

\- Confirm actual enemy death updates the director's count.

\- Confirm distinct enemy prefabs behave correctly after spawning.

\- Verify spawn clearance and reachability in the bunker.

\- Test the director alongside the other three agents.

\- Check the Console for errors and measure gameplay performance.

