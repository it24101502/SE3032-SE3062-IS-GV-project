# Spawn Director: MainLevel integration

Owner: Premarathna P A I B — IT24101844

Status: integration code prepared; Unity compilation, setup validation and gameplay acceptance tests must be run on the target computer. Historical standalone results are not integrated gameplay results.

## Install and configure

1. Save intentional scene changes and close Unity Editor.
2. Extract the update ZIP into Downloads/AgentController-Integration-Update.
3. Run Install-Integration.ps1 from PowerShell. It checks the feature branch and source hashes, backs up replaced files in Downloads, and copies only this update's files. It does not commit, push, merge, or modify any other branch.
4. Open AgentControllerGit with Unity 6000.6.4f1. Wait for script compilation. Any red Console errors must be resolved before continuing.
5. Open Assets/Scenes/MainScenes/MainLevel.unity.
6. Run Agent Controller > Set Up Spawn Director in MainLevel.
7. Press Ctrl+S. Run Agent Controller > Validate MainLevel Spawn Setup. Read its route/clearance counts. At least one candidate must be reachable and clear; inspect any warnings. Validation does not certify camera visibility, damage or gameplay.
8. Run the acceptance checks below in Play mode, then stop Play before saving intended configuration.

The setup uses the existing Player, its Camera, GameFlow and exactly four BunkerAgentMarker components of kind SpawnCandidate. It leaves their transforms, bunker geometry, existing EnemyAgent prefab, Animator, PlayerStats, PlayerThrower and AgentMovement unchanged. It creates EnemyAgentLight and EnemyAgentHeavy prefab variants, then adds/configures one SpawnDirector and one bridge in MainLevel. Running setup again reuses the variants and director; it reassigns the listed integration references and profiles.

## Runtime connections

- SpawnDirectorPlayerBridge runs before director updates. It sends PlayerStats.HealthFraction and Clamp01(PlayerThrower.ammo / rockAmmoCapacity) through UpdatePlayerState. The capacity is fixed, initially set from the configured starting supply. It represents throwable rocks, not gun ammunition; ranged shots currently consume no ammunition.
- Missing player/thrower references pause the director. Player death, disabled player, a false bridge Gameplay Active flag, timeScale zero and GameFlow boss death also stop new spawning. For other game menus, explicitly update the bridge Gameplay Active flag.
- EnemyHealth implements the existing IDamageable interface. Existing gun, melee and rock hit detection can damage it. A lethal hit notifies its owning director once, stops movement, disables the root and destroys it. No inactive corpse accumulates.
- AgentNavMeshChase requests a complete NavMesh route every 0.5 seconds and passes the corners to AgentMovement.SetPositionPath. It skips the starting corner and adds a 1.05-unit capsule-root height offset. AgentMovement continues to own movement, smooth rotation and the Animator Speed value. No second NavMeshAgent drives the same transform.
- Chase clears its supplied route when the player is unavailable, gameplay ends, controls disable movement, or a route is invalid. It stops within 1.5 horizontal units of the target. A moving player triggers periodic replanning. No attack behaviour is added by this movement adapter.

## Spawn placement and profiles

MainLevel markers are at floor height. Align Spawn Root To NavMesh samples that floor and raises the capsule root by 1.05. The original test scenes retain their old behaviour because this option defaults to false.

The clearance mask includes Default (the bunker walls and props) plus SpawnObstacle. The 2-unit capsule is raised clear of the floor, so floor colliders can remain Default. Player and thrown-rock layers are excluded from the clearance mask. All old distance, spacing, viewport, complete-route and short-retry filters remain in effect.

| Profile | Health | Movement speed | Preferred pressure | Peak only |
|---|---:|---:|---:|---|
| Light | 30 | 2 | 0.3 | No |
| Heavy | 60 | 1.4 | 0.8 | Yes |

Both variants inherit the capsule visual and Idle/Move Animator. Distinct health/speed are implemented; final artwork and combat behaviours are separate work.

## MainLevel acceptance checklist

Record actual observations; these tests have not been run in the remote editing environment.

| Check | Procedure | Expected observation |
|---|---|---|
| Compile/setup | Import update, run setup, save scene, run validation | No red compile errors; four references; at least one clear complete route |
| Real health input | In Play, select Player and change health from 100 to 50 | Director Health01 becomes 0.5; target stress contribution rises by 0.25, then smooths |
| Real rock supply | Press Q once with a capacity/supply of 5 | HUD rocks 4, director Ammo01 0.8; ammo stress contribution is 0.04 |
| Chase/animation | Allow a spawn, move player around a navigable corner | Clone follows a complete path, stays above floor and plays Move; it idles near player |
| Ranged damage | Aim at a Light clone, use ranged weapon (2), hit three times with default damage 10 | Health 30 -> 20 -> 10 -> 0; clone is removed |
| Rock/melee damage | Hit a fresh clone with a sufficiently fast rock or melee attack | Existing IDamageable hit detection reduces clone health |
| Death/count/replacement | Reach cap 3, kill one clone during an eligible phase | Count drops once; replacement can spawn after its interval when a valid point exists |
| Player death | Set Player health to 0 in Play | Bridge sends Player Alive false; new spawns and chase stop |
| Gameplay gate | Untick Gameplay Active on the bridge, then restore | Director and chase stop, then resume when eligible |
| Boss victory | Defeat boss or set BossHealth health to 0 in Play | GameFlow victory and bridge disable spawning/chase |
| Invalid route | Move player well outside NavMesh temporarily | No new unreachable spawn; chase clears invalid route after its next refresh; no exceptions |
| Profiles | Observe BuildUp and a low-stress Peak | Light and Heavy names appear; inspect distinct health and speeds |
| Old GV prototype | Open AgentControllerTest and run its existing tests | Waypoints, position arrays, pause/resume, Animator and completion event still work |

A full cap does not imply replacement during Relax, player death, victory, an invalid route, blocked/visible points or insufficient distance. Those gates intentionally prevent spawning. Do not weaken them to make a test pass. For a focused health/movement check only, temporarily untick Reject Visible Points during Play; restore the intended production value before saving.

## Limitations and validation scope

This update has C# syntax parsing, existing-method preservation and asset/marker checks performed outside Unity. No Unity Editor/runtime or Unity assembly compiler is available in that environment. The setup action performs real NavMesh/physics validation when run in Unity. Actual damage, navigation and spawn results must be recorded above on the target computer.

The director counts only its own enemies. Viewport visibility is point-based and ignores wall occlusion/full bounds. The transform follower has no crowd avoidance or physics collision response; routes rely on a correctly baked/carved NavMesh, and rapidly moving obstacles can invalidate them between refreshes. The new enemies chase but do not attack. Other agents, final model binding, performance measurement and balance tuning require gameplay testing.

## Commit in the feature branch

Review git status and diffs first. Commit the GV path producer separately from IS lifecycle/setup work. Include every new script's .meta. Keep unrelated settings/logs out of these commits.

```powershell
git add Assets/Scripts/Movement/AgentNavMeshChase.cs Assets/Scripts/Movement/AgentNavMeshChase.cs.meta
git commit -m "[GV] Feed NavMesh chase paths into existing agent movement"

git add Assets/Scripts/Agents/SpawnDirector.cs Assets/Scripts/Agents/SpawnDirectorPlayerBridge.cs Assets/Scripts/Agents/SpawnDirectorPlayerBridge.cs.meta Assets/Scripts/Agents/EnemyHealth.cs Assets/Scripts/Agents/EnemyHealth.cs.meta Assets/Editor.meta Assets/Editor/SpawnDirectorIntegrationSetup.cs Assets/Editor/SpawnDirectorIntegrationSetup.cs.meta Assets/Prefabs/Agents/EnemyAgentLight.prefab Assets/Prefabs/Agents/EnemyAgentLight.prefab.meta Assets/Prefabs/Agents/EnemyAgentHeavy.prefab Assets/Prefabs/Agents/EnemyAgentHeavy.prefab.meta Assets/Scenes/MainScenes/MainLevel.unity Docs/SpawnDirector-MainLevel-Integration.md Docs/SpawnDirector-Report.md
git diff --cached --check
git diff --cached --stat
git commit -m "[IS] Connect real player state and damageable enemies in MainLevel"
git push origin feature/agent-controller-premarathna
```

Run these only after setup generates the prefab variants and the tests pass. The first GV commit depends on the already existing SpawnDirector type; the second IS commit wires this producer into gameplay. No main/develop/fix branch is pushed.
