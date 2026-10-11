# SE3032-SE3062-IS-GV-project

Interactive 3D Shooting Game — joint project for **SE3032 (Graphics & Visualization)** and **SE3062 (Intelligent Systems)**. One codebase, one Git repo, two rubrics.

## Branch owner
**Suwasthikka S** — IT24101502

## Role
- **GV (Systems Engineer):** Player interaction physics — doors, barricades, throwables.
- **IS (Adaptive Boss):** Boss agent that changes tactics depending on the player's weapon or health.

## Engine
Unity 6 (`6000.6.3f1`), Universal Render Pipeline, Unity AI Navigation (NavMesh).

## Architecture (IS) — Adaptive Boss

**Architecture type:** Utility AI. Every decision cycle the boss scores four actions from 0 to 1 and picks the best one.

| File | Responsibility |
|------|----------------|
| `Assets/Scripts/Agents/AdaptiveBoss/BossBrain.cs` | Pure decision logic (no GameObjects): considerations → action scores → anti-flicker rule |
| `Assets/Scripts/Agents/AdaptiveBoss/BossController.cs` | Perception, NavMesh movement, cover line-of-sight check, stuck recovery, damage, CSV logging, debug overlay |
| `Assets/Scripts/Agents/AdaptiveBoss/BossHealth.cs` | Boss health (`IDamageable`) |

**Actions:** `Attack`, `Retreat`, `Flank`, `TakeCover`

**Considerations (all normalised 0–1):**
- `closeness` – how near the player is
- `mediumBand` – peaks when the player is about 10 m away
- `bossHurt` / `retreatUrgency` (= bossHurt²) – how wounded the boss is
- `playerHurt` – how wounded the player is
- `rangedThreat` – 0.9 if the player holds the ranged weapon, 0.1 if melee (this is how the boss adapts to the player's weapon)
- `courage` – a wounded boss is less willing to flank
- `coverGate` – cover is much less attractive when no cover point is hidden from the player

**Action scores:**
```
Attack    = 0.4*bossHealth + 0.4*closeness + 0.2*playerHurt
Retreat   = 0.7*retreatUrgency + 0.3*closeness
Flank     = (0.5*rangedThreat + 0.5*mediumBand) * courage
TakeCover = (0.6*rangedThreat + 0.4*bossHurt) * coverGate
```

**Anti-flicker rule:** the boss must stay in an action for at least `minCommitTime` (1.0 s), and a new action must beat the current one by `switchMargin` (0.15).

**Robustness features:**
- Cover points are only used if a ray from the player's eyes is blocked by real geometry (line-of-sight check).
- Stuck detection: if the boss should be moving but is not, it drops its path and walks to a random nearby NavMesh point.
- Retreat and flank targets fall back to shorter or opposite directions when a wall or the arena edge is in the way.
- Closed doors and intact barricades carve the NavMesh, so the boss paths around them (or waits at a door it cannot pass).

## Controls
| Key | Action |
|-----|--------|
| W A S D / Mouse | Move / look |
| Shift / Space | Sprint / jump |
| 1 / 2 | Melee / ranged weapon |
| Left mouse | Attack |
| Q | Throw rock |
| E | Open / close door |
| F9 | Save boss log now |
| R | Restart after win / lose |

## Boss logs
Written to `Application.persistentDataPath` (on Windows: `C:\Users\<name>\AppData\LocalLow\DefaultCompany\My project\`):
- `boss_log_<time>.csv` — one row per decision change (scores, distance, HP, player weapon)
- `boss_sessions.csv` — one summary row per play session (result, time spent in each action, switches, stuck recoveries)

## Status
- [x] Repo cloned, branch created
- [x] AI architecture sketch (paper)
- [x] Basic player movement + one interaction (Week 2)
- [x] Minimal Adaptive Boss logic in test scene (Week 2)
- [x] Boss: line-of-sight cover, stuck recovery, session logging (Week 3)
- [x] README architecture section
- [ ] Boss stress-testing and play-session logs collected
- [ ] IS report submitted
- [ ] Viva rehearsal
- [ ] Integration into the shared main scene with the team
- [ ] GV: lighting, textures, optimisation notes, 3-min demo video

## Deadlines
- IS: agent code + report + viva — **17 Oct 2026**
- GV: source code + 3-min demo video — **21 Oct 2026**

## Commit convention
Tag commits by module for clarity:
```
[GV] ...   → physics / interaction work
[IS] ...   → Adaptive Boss agent work
```
