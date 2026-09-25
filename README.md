# SE3032-SE3062-IS-GV-project

**Interactive 3D Shooting Game** — a joint project graded twice, across two modules:

- **SE3032 — Graphics & Visualization** (20% of final grade): visual fidelity, 3D modeling, environment design, physical interaction mechanics.
- **SE3062 — Intelligent Systems** (20% of final grade): AI, pathfinding logic, and dynamic decision-making for 4 autonomous agents.

Same codebase, same Git repo, two rubrics.

## Overview
A playable 3D shooting game where the player navigates an environment and engages with 4 AI-controlled agents/enemies. Engine: **Unity** (built-in NavMesh support matches the GV requirement directly).

## Team & Roles

| Student | Reg No | GV Role | IS Agent |
|---|---|---|---|
| De Silva D S P S N | IT24102190 | World Builder — level layout, lighting, texturing, NavMesh bake | Trap Setter — places mines/traps strategically, learns player movement patterns |
| Suwasthikka S | IT24101502 | Systems Engineer — player physics (doors, barricades, throwables) | Adaptive Boss — changes tactics depending on player's weapon or health |
| Peiris M P V P | IT24100245 | Core Developer — custom 3D models (2+, built from scratch) | Stealth Enemy — sneaks around, hides, hunts using sound detection |
| Premarathna P A I B | IT24101844 | Agent Controller — turns path arrays into smooth movement/animation | Spawn Director — controls enemy spawns dynamically to balance difficulty |

## Branches
Each student works on their own branch, covering **both** their GV and IS responsibilities (grading distinguishes module by file path, not branch):

- `feature/world-builder-desilva`
- `feature/systems-engineer-suwasthikka`
- `feature/core-developer-peiris`
- `feature/agent-controller-premarathna`

Merge into `main` via Pull Request at agreed sync points — not ad hoc.

## Folder structure
```
Assets/
├── Scripts/
│   ├── Player/          ← Systems Engineer
│   ├── Agents/
│   │   ├── TrapSetter/
│   │   ├── AdaptiveBoss/
│   │   ├── StealthEnemy/
│   │   └── SpawnDirector/
│   ├── Environment/      ← World Builder
│   └── Core/              ← shared utilities
├── Models/
├── Materials/
├── Textures/
├── Prefabs/
├── Scenes/
│   ├── MainScenes/
│   └── TestScenes/
├── Animations/
└── Audio/
```

## AI architecture options (one per agent, no duplicates)
- A* / weighted Dijkstra
- Finite State Machine (FSM)
- Behaviour Tree
- GOAP (Goal-Oriented Action Planning)
- Utility AI

Using a different architecture per agent supports the IS rubric's "Creative Agent Design & Originality" marks.

## Deadlines

| Module | Deliverable | Deadline |
|---|---|---|
| SE3062 (IS) | Agent code + report + viva readiness | **Sat, 17 Oct 2026** |
| SE3032 (GV) | Source code + 3-minute demo video | **Wed, 21 Oct 2026** |

## Commit convention
Tag commits by module so either grader can filter history cleanly:
```
[GV] ...   → visuals, physics, models, environment
[IS] ...   → agent logic
```

## Practical rules
- Commit often, descriptive messages — both modules grade individual Git history.
- Keep AI logic decoupled from visuals — expose clean methods (e.g. `GetNextMove()`) rather than tangling agent logic with animation/physics code.
- Fixed weekly sync day — agree this in Week 1 and stick to it.
