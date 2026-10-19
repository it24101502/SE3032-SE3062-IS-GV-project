# GV Agent Controller - Setup and Handover

Owner: Premarathna P A I B - IT24101844
Module: SE3032 Graphics and Visualization

## Implemented
- Waypoint movement using Transform arrays.
- World-space movement using Vector3 arrays.
- Smooth horizontal facing rotation.
- Movement-speed-driven Idle/Move Animator transitions.
- Pause/resume, alive state and optional path looping.
- Reusable EnemyAgent prefab.
- Editor position-array demonstration.

## Files
- Assets/Scripts/Movement/AgentMovement.cs
- Assets/Prefabs/Agents/EnemyAgent.prefab
- Assets/Scenes/TestScenes/AgentControllerTest.unity
- Assets/Animations/BodyVisual.controller
- Assets/Animations/AgentIdle.anim
- Assets/Animations/AgentMove.anim

## Path Input
For scene waypoint objects:
agentMovement.SetPath(waypointTransforms);

For calculated world-space positions:
agentMovement.SetPositionPath(worldPositions);

Each accepted path resets the waypoint index without teleporting
the agent. The input array is copied.
Transform targets remain live references.
Vector3 targets are fixed position snapshots.
Null or empty input clears the selected path.
Invalid input is rejected while preserving the previous path.

Positions must describe the agent root position.
For a root centered 1 unit above the floor, ground-level path
coordinates need the appropriate vertical offset before assignment.
The consumer does not calculate paths or avoid obstacles.

## Animation Setup
Assign an Animator with a float parameter named Speed.
The controller uses actual per-frame movement speed.
Disable Apply Root Motion because AgentMovement controls translation.
Keep animation on visual children so root movement stays independent.

Current clips animate a temporary capsule visual.
They are not humanoid walk/run animations.

## Final Character Setup - Pending Asset
- Obtain the team's final enemy model and any rig/animation assets.
- Inspect the model hierarchy and animation bindings.
- Set its scale, ground alignment and forward direction.
- Assign compatible clips/controller and Animator.
- Remove or disable duplicate colliders on visual children.
- Retest movement, rotation, Idle/Move and pause/resume.
- Do not assume capsule animation bindings fit the replacement model.

## Prototype Checks
Developer reported passing:
- Repeated waypoint looping.
- Smooth facing rotation.
- Movement animation during movement.
- Movement and move animation stop when movement is disabled.
- Movement resumes when enabled.
- Movement stops when the agent is not alive.

The position-array demonstration was invoked successfully;
the supplied Console screenshot showed no warnings or errors.

## Design Choices
- MoveTowards limits each movement step to speed multiplied by time.
- RotateTowards limits turning to degrees per second.
- Animator reference and parameter hash are cached.
- Visual animations are separated from root movement.
- A reusable prefab supports consistent agent setup.

No performance improvement measurements have been recorded.

## Remaining Final Checks
- Final character visual and animation setup.
- Actual IS-calculated path connection.
- Movement and collision behaviour in the game environment.
- Updated architecture sketch.
- Group demo video contribution and individual viva preparation.
