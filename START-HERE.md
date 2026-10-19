# Expanded Abandoned Bunker

Open this project with **Unity 6000.6.3f1**.

1. Extract the ZIP to a new folder, if using the packaged download.
2. In Unity Hub, add the `AbandonedBunkerExpanded` folder (the one containing Assets, Packages and ProjectSettings).
3. Open `Assets/Bunker/Scenes/AbandonedBunkerExpanded.unity`.
4. Press Play. Move with WASD, look with the mouse, toggle the flashlight with F and use the original Storage door with E. Escape releases the mouse.
5. Walk to the original Control Room. The new left exit leads to Ventilation; the new right exit leads to Maintenance. Both return to the rear service corridor, which connects to Generator Hall and Service Staging.

The original `AbandonedBunker.unity` scene remains unchanged. The expanded scene is first in this copy's build scene list. Your original project and ZIP on D: were not edited.

## Added spaces

| Space | Floor dimensions | Purpose |
|---|---|---|
| Acoustic / ventilation | 10 x 10 m | Equipment and baffles interrupt sightlines; sound, hiding and patrol markers support the Stealth Enemy. |
| Maintenance workshop | 12 x 12 m | Central machinery creates a shortcut and a longer bypass; route samples, six trap candidates and exclusion markers support De Silva's Trap Setter. |
| Rear service corridor | 51 x 4 m, plus return links | Connects both wings into a loop and provides access to the rear rooms. |
| Generator hall | 16 x 18 m; 5 m ceiling | Two generator banks, side routes, cover and a retreat position support the Adaptive Boss. |
| Service staging | 8 x 8 m | An offset screen shields the staging pocket; two candidate spawn positions support the Spawn Director. |

All geometry is editable in the Unity Hierarchy under `07 - EXPANSION`. The original room objects remain in their existing groups. In the expansion scene only, the Control Room's side walls have openings and its electrical cabinet has moved 1.1 m toward the rear wall to clear the east opening.

## Agent handoff

`07 - EXPANSION / 05 - Agent integration markers` contains labelled objects with the `BunkerAgentMarker` component. Select one to see its marker radius in the Scene view. Fields identify marker kind, room and route.

- Trap Setter: sample Shortcut and Bypass markers, score TrapCandidate positions, and enforce NoTrapZone exclusions in the agent code.
- Stealth Enemy: connect SoundSource, HidePoint and PatrolPoint markers to the team's perception and sound systems.
- Adaptive Boss: use the generator approach, flank and retreat markers with the chosen navigation system.
- Spawn Director: check player distance, visibility, reachability and enemy limits before using any SpawnCandidate. The corridor points are candidates, not guaranteed hidden locations.

These are environment hooks only. **AI behaviours, trap placement, damage, sound events, objectives and navigation baking are not implemented.** The fuse housing and machinery are visual props. The original entrance remains sealed.

## Validation

The project was compiled and the scene generated with Unity 6000.6.3f1. Physics capsule checks used the existing player's 0.29 m radius, sampling paths every 0.2 m. Checks cover the original approach, both new Control exits, Acoustic return, both Workshop routes, rear corridor, generator centre and flanks, and staging access. Agent standing markers were also checked against colliders. See the adjacent Validation.txt in the delivery bundle.

Rendered previews were visually reviewed. The top view hides ceilings and temporarily disables fog for clarity; gameplay lighting and ceilings remain enabled in the saved scene. These checks do not replace a team playtest or test AI navigation that has not yet been added.

Every pre-existing file under Assets was compared with the supplied archive and remained byte-for-byte identical. The new scene, marker script, expansion materials, text shader and editor builder are additions. ProjectVersion is set to 6000.6.3f1.

## Editing safely

The expanded scene is already built; no generation command is needed. Continue editing that scene and keep the original as a baseline. Keep .meta files alongside their assets when moving or committing them.

Avoid the old `Bunker / Rebuild scene (replaces current scene)` menu item: it overwrites the baseline scene and regenerates shared assets. The supplied `BunkerExpansionBuilder.Build` is a batch reproduction tool; running it again resets the expansion scene from the baseline and replaces its expansion materials. It is not a tool for updating a manually edited expansion.
