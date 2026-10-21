# Trap Setter - IS individual agent

**Project:** Outpost 04, eight-room bunker  
**Role:** autonomous trap setter  
**Decision architecture:** Utility AI  
**Theme:** a bunker defence controller that deploys fictional pressure pads on routes the player repeatedly uses.

This implements the trap-setter role only. The adaptive boss, stealth enemy and spawn director remain your teammates' work. The normal scene has no text overlays; F3 enables an optional demonstration panel.

## 1. Completed sketch template

**Job:** Place traps strategically by adapting to the player's recent movement.

**Actions:** Place a pad; remove and relocate an existing pad; wait; forget old route evidence; reset when the demo round restarts.

**Sensed inputs:** Player position; changes between 2 m grid cells; entry count and time of last visit; recent route heat; active trap positions and count; distance to player and spawn; action cooldown; floor and obstacle checks; age and utility of existing traps.

**If the player walks the same path twice:** Increase that area's evidence. Once the player moves at least 4 m away, compare its score with other safe, repeatedly visited positions. Place there only if it wins and reaches the minimum score.

**If I have no traps left to place:** Compare the best new position with the weakest eligible existing trap. Relocate only when the improvement exceeds 0.18. Otherwise wait. The budget means at most three active pads; consumed pads free capacity, subject to cooldowns.

**If a trap already covers the area:** Reject the candidate if it is within 3 m of any active pad. Consider another location or wait.

**If the player changes routes:** Old heat decays. Repeated visits to the new route increase its score, making a later relocation possible.

**If the player stands still:** Do not keep counting visits. A visit means entering a different cell and then returning, with a 1.5-second debounce against boundary jitter.

**If no safe useful location exists:** Wait. The controller never needs to place a trap just because a timer expired.

**Why Utility AI:** Several locations can be valid simultaneously, so comparing their current usefulness is the central problem. The safety gates reject invalid actions; weighted scores choose among the valid ones. A basic timer spawner would not adapt to route history.

## 2. Decision sketch

See `TrapSetter-Decision-Sketch.png` for the visual version.

```text
Observe player position every 0.25 seconds
                    |
Update bounded visit memory on cell re-entry
                    |
Every 1 second: is the action cooldown over?
         no -> WAIT  | yes
                    v
Filter candidates: repeated evidence, safe distance,
floor, no obstacle, no nearby pad, no site cooldown
                    |
Score eligible sites; select the highest utility
                    |
No site or score < 0.50? -> WAIT
                    |
Fewer than 3 pads? -> PLACE highest-scoring pad
                    |
At capacity: find weakest eligible old pad
                    |
New score > old score + 0.18? -> RELOCATE
Otherwise -> WAIT
                    |
Observe again; old evidence decays over time
```

All pads have a **2.5-second amber arming warning**. Their armed behaviour differs:

| Type | Armed colour | Trigger | Effect |
|---|---|---|---|
| Pressure | Red | Within 0.65 m | 25 damage, consumed immediately |
| Shock | Cyan | Within 0.65 m | Half walking speed for 4 seconds; no damage |
| Burst | Purple | Within 0.9 m | Flashes white for 1.2 seconds, then deals 40 damage if still within 1.4 m |

Walking beyond the burst radius before its timer ends avoids damage. A wall or closed door blocks its effect. All traps are consumed once. Shock effects refresh their duration instead of multiplying the slowdown, and movement restores on expiry, respawn or component disable.

Types cycle **Pressure -> Shock -> Burst -> repeat** on each placement, including relocation. Utility AI decides the location and timing, not the type. These gameplay effects and their lifecycle are separate from the decision architecture. Damage values assume the default agent damage setting of 25.

## 3. The scoring model

For an eligible location:

```text
U = 0.65 H + 0.25 R + 0.10 P

heat(now) = savedHeat * 0.5 ^ ((now - lastVisit) / 45)
H = min(1, heat(now) / 4)
R = 0.5 ^ ((now - lastVisit) / 45)
P = clamp(1 - distanceToPlayer / 20, 0, 1)
```

Each accepted entry adds 1 to the decayed heat, capped at 6. The three normalized factors make the score fall in [0, 1]. The 45-second half-life makes old routes progressively less important. Eligibility requires at least two accepted entries and decayed heat of at least 1.25.

- **Heat, weight 0.65:** Repeated travel is the strongest evidence for placing a trap.
- **Recency, weight 0.25:** Prefer recently used routes over abandoned ones.
- **Proximity, weight 0.10:** Modestly prefer relevant nearby positions, after enforcing the 4 m safety exclusion.
- **Minimum score 0.50:** Waiting is a legitimate outcome when evidence is weak.
- **Relocation margin 0.18:** Hysteresis prevents constantly moving traps for tiny score improvements.

Example: H = 0.75, R = 0.80 and P = 0.50 gives U = 0.7375. Another site with H = 0.50, R = 1.00 and P = 0.70 scores 0.645. The first wins, even though the second is more recent and closer. If the weakest existing eligible pad scores 0.40, 0.7375 is enough to justify relocation.

These weights are explainable starting values, **not experimentally proven optimal values**. Heat and recency are partly correlated; the separate recency term deliberately favours fresh evidence. A useful evaluation is to remove that term and compare the results.

## 4. Constraints and edge cases

| Constraint | Behaviour |
|---|---|
| Capacity | Maximum 3 active pads by default |
| Global action cooldown | At least 8 s between placements/relocations |
| Player safety | No placement within 4 m of the player |
| Spawn protection | No placement within 3 m of initial spawn |
| Spacing | No placement within 3 m of an active pad |
| Site cooldown | Used or vacated locations blocked for 30 s |
| Relocation | Old pad must be at least 15 s old and at least 5 m from player |
| Placement surface | Flat ground near y = 0; furniture and blocked sites rejected |
| Idle player | Standing in one cell does not add more entries |
| Large maps | At most 512 remembered cells; oldest evicted first |
| Round reset | Pads, memory and counters cleared |
| Missing player | Agent disables itself with a warning instead of throwing |

The ground check targets this flat bunker. A multi-storey map needs a floor-aware cell key and stored height before reusing the component. The agent is not a moving robot, does not use A*, and does not claim machine learning or reinforcement learning. Its adaptation comes from a decaying movement-history model.

## 5. Open and demonstrate

1. Extract `AbandonedBunker-3TrapTypes.zip`.
2. Add its `AbandonedBunker` folder through Unity Hub and open with Unity 6000.6.4f1.
3. Open `Assets/Bunker/Scenes/AbandonedBunker.unity`, press Play, and click the Game view.
4. Press **F3** to display health, movement state, scores, decisions and trap counts.
5. Walk from the entrance toward the control-room doorway, return toward the entrance, then repeat the route. Do not just stand still.
6. After revisiting cells, move several metres away. Watch for a new amber pad and the logged `Place` decision. The agent waits if there is no eligible candidate.
7. The first pad is red when armed. Return over it: health falls by 25 and the pad disappears. The second placement is cyan: step onto it and compare movement for 4 seconds. The third is purple: enter its trigger radius, then retreat during the white flash to test a successful dodge; on another burst pad, remain nearby to test 40 damage. One-second hit immunity prevents several simultaneous pads from stacking damage instantly.
8. To demonstrate adaptation, use the north cross corridor repeatedly instead. Leave older pads away from you for at least 15 seconds. Relocation depends on the new score beating the old score by 0.18; it is not guaranteed after a fixed number of seconds.
9. At zero health, the demo respawns the player and clears learning. With F3 open, **R** resets manually.
10. Press F3 again to restore the text-free view.

Other controls: WASD movement; mouse look; F flashlight; E storage door; Escape release cursor.

## 6. Code map for the viva

| File | Responsibility | Main place to explain |
|---|---|---|
| `TrapUtility.cs` | Pure memory, scoring and action selection | `Observe`, `HeatAt`, `Score`, `Choose` |
| `TrapSetterAgent.cs` | Unity sensing, candidate filtering, actuation and optional debug panel | `ValidSite`, `Decide`, `Place` |
| `BunkerTrap.cs` | Warning, arming, contact and single-use consumption | `Update` |
| `BunkerPlayerHealth.cs` | Small demo health/respawn receiver | `ApplyDamage`, `ResetRound` |

The scene already contains the agent and health component with their references assigned. Pads are ordinary generated Unity primitives and appear only when the AI decides to place one. No navigation package, external training service or API key is needed.

## 7. Questions you should be able to answer

**Where is the intelligence?** In the history model and repeated comparison of location utilities. Physics rejects impossible locations; the weighted model chooses the useful one.

**Why not an FSM?** An FSM is useful for mutually exclusive modes, such as patrol/chase. This role primarily compares many competing locations, which suits utility scoring. The pad's warning/armed lifecycle does not change the controller's architecture.

**Why not a Behaviour Tree?** A tree can prioritize actions, but a fixed priority does not directly rank many sites by competing factors. It could call a utility scorer, but that adds another layer for this small agent.

**Does it learn?** It adapts using stored experience. It is not a trained neural network and does not learn weights. The weights stay fixed; the route evidence changes.

**What stops a feedback loop?** A count cap, cell-entry debounce, exponential forgetting, minimum spacing, site cooldowns and a relocation margin.

**Can I remove a weight?** Yes. Set its contribution to zero and compare hit rate, placements, relocations and fairness. Re-normalize or retune the threshold if changing the sum of weights.

**How expensive is a decision?** There are at most 512 remembered cells. Each is filtered and scored once per decision; spacing compares against at most 3 pads. With a fixed small capacity, this is approximately linear in remembered cells. Physics cost still needs profiling in Unity with the other agents active.

**What information is unrealistic?** The defence controller has access to the player's position throughout the bunker. This is a deliberate sensor assumption, not simulated line-of-sight perception. The pad's contact check does use a line test to prevent activation through a wall.

## 8. Validation and evaluation

Completed here:

- All runtime and editor C# files compile against installed Unity 6000.6.4f1 assemblies.
- 20 executable pure-C# checks passed. See `Evidence/TrapUtilityChecks.cs` and `Evidence/core-test-results.txt`.
- Scene/prefab YAML, component and asset references, and routes through all eight rooms passed offline checks.

**Not verified here:** Unity import, Play-mode physics, actual trap visuals, performance and four-agent integration. The automated Editor could not connect to its licensing service inside the chat sandbox. Run the demonstration above locally before submitting; compilation is not a substitute for a gameplay test.

Record real results, not invented ones:

| Experiment | Keep fixed | Record |
|---|---|---|
| Repeat one route | Same route, timing and capacity | First placement time, pad locations, activations and actual health changes |
| Switch route | Same first route, then new wing | Time until relocation, old/new scores |
| Idle/boundary test | Stand still, then move near a cell edge | Entry counts and absence of false learning |
| No heat contribution | Same movement trace | Differences in location choice and hits |
| Capacity test | Revisit several routes | Active count never exceeds capacity |
| Group integration | All four agents active | Console errors and frame-time measurements |

## 9. Group handoff and honest progress record

The supplied sketch guide asks the team to coordinate different decision approaches. Confirm Utility AI with the other members, particularly the spawn-director owner, before making this your final choice. A score panel helps explain this agent but does not satisfy the other roles.

The assignment awards individual marks for ongoing version-control work and technical explanation. This package does not create or backdate a five-week commit history. Start from this implementation, understand it, and commit your actual changes and measured improvements as you work. Suggested work stages are memory/scoring, integration, edge cases, playtesting/tuning, and final explanation; these are a plan, not evidence that five weeks of work occurred.

For team integration, use one trap-setter instance, assign the team's player Transform, and adapt `BunkerTrap`'s damage call to the shared health interface. The demo currently adds `BunkerPlayerHealth` when missing; replace that fallback for the final team game to avoid two competing health systems. For shock traps, also replace the direct BunkerWalker speed adjustment with your team movement-speed modifier system. This demo assumes it is the only speed modifier. The other agents can subscribe to `Damaged` and `Respawned`, or use the team's own event system.

Source documents consulted: `AI-agent-sketch-guide.pdf` (Trap Setter template, page 2 and group coordination, page 3) and `Assignmnet_IS.pdf` (role ownership, page 1 and assessment rubric, page 2). No claim is made that this implementation guarantees a particular grade.


## Three-type Play-mode checklist (run locally)

- Pressure: after amber ends, red contact reduces 100 HP to 75 and consumes one pad.
- Shock: cyan contact changes speed from 3 to 1.5 for 4 seconds without health damage; it returns to 3 afterward.
- Shock refresh: another shock refreshes the four-second timer without reducing speed below 1.5.
- Reset: press R with F3 open while slowed; movement returns to normal and all pads clear.
- Burst dodge: trigger purple, back out past 1.4 m during the white flash; no damage occurs.
- Burst hit: remain nearby; default damage is 40 after 1.2 seconds, once only.
- Cover: a solid wall/closed door between pad and player prevents an effect through it.
- Budget: all three types share the same three-active-pad limit. A primed burst cannot be relocated.
- The panel reports activations, not successful damage hits; a dodged burst is still an activation.

The existing 20 automated checks cover route memory and utility decisions, not these Unity physics/time effects. The added trap code compiles; the checklist above still needs Play-mode verification locally.
