# SE3032-SE3062-IS-GV-project

Interactive 3D Shooting Game – joint project for **SE3032 (Graphics & Visualization)**
and **SE3062 (Intelligent Systems)**. One codebase, one Git repo, two rubrics.

Open with **Unity 6000.6.4f1** (URP). Main scene: `Assets/Scenes/MainScenes/MainLevel.unity`.

## Team Members & Roles

### De Silva D S P S N – IT24102190
- **GV (World Builder):** level layout, lighting, texturing, NavMesh baking.
- **IS (Trap Setter):** places mines/traps strategically, learning player movement patterns.

### Premarathna P A I B – IT24101844
- **GV (Agent Controller):** turns path arrays into smooth movement/animation.
- **IS (Spawn Director):** controls enemy spawns dynamically to balance difficulty.

### Peiris M P V P – IT24100245
- **GV (Core Developer):** custom 3D modelling (2+ models built from scratch).
- **IS (Stealth Enemy):** sneaks, hides and hunts using sound detection.

### Suwasthikka S – IT24101502
- **GV (Systems Engineer):** player physics (doors, barricades, throwables).
- **IS (Adaptive Boss):** changes tactics based on the player's weapon and health.

## IS Agents
| Agent | Owner | Architecture | Script location |
|---|---|---|---|
| Adaptive Boss | Suwasthikka | Utility AI | `Assets/Scripts/Agents/AdaptiveBoss` |
| Spawn Director | Premarathna | Phase machine + scoring | `Assets/Scripts/Agents/SpawnDirector.cs` |
| Trap Setter | De Silva | TBD | TBD |
| Stealth Enemy | Peiris | TBD | TBD |

## Status
- [x] Repo, `develop` branch, URP project setup
- [x] Boss, Spawn Director, player physics (doors, barricades, rocks)
- [x] Bunker level geometry
- [ ] NavMesh baked on MainLevel
- [ ] Trap Setter
- [ ] Stealth Enemy
- [ ] Custom Blender models imported
- [ ] Agent animation / movement on NavMesh
- [ ] IS report, viva prep
- [ ] 3-minute demo video

## Deadlines
- IS: agent code + report + viva – **21 Oct 2026** (extended by the professor)
- GV: source code + 3-minute demo video – **21 Oct 2026**

## Commit convention
- `[GV]` level design / lighting / NavMesh / movement / animation / modelling
- `[IS]` Trap Setter / Spawn Director / Stealth Enemy / Boss agent work
- `[SETUP]` packages, project settings, repo housekeeping