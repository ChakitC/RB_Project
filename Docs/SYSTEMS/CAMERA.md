# Camera System

## Overview

Gameplay uses an over-the-shoulder third-person camera built on Cinemachine 3.
The project keeps ownership of input, state, aiming, profiles, UI suppression,
and cinematic handoff.

| Component | Responsibility |
|-----------|----------------|
| `GameplayCameraController` | Free Look / Shoulder Aim state, yaw/pitch, FOV, cursor ownership, recoil, Cinemachine setup, and cutscene handoff |
| `CinemachineThirdPersonFollow` | Shoulder geometry, damping, camera distance, and obstacle push-in |
| `CinemachineThirdPersonAim` | Stable center-screen orientation |
| `ThirdPersonAimController` | Camera Aim Point and muzzle-obstruction validation |
| `ThirdPersonAimRigController` | Player/companion visual upper-body pitch toward the Aim Point |
| `ThirdPersonAimBoneMap` | Generic-rig Spine, Chest, and optional UpperChest bindings |
| `ThirdPersonCharacterProfile` | Per-character pivot, shoulder, distance, FOV, aim-rig, and fade calibration |
| `ThirdPersonOcclusionFader` | Local-player close-camera fade |
| `ThirdPersonReticleView` | Dynamic spread, Shoulder Aim contraction, hit marker, and muzzle-blocked marker |

`GameplayCameraController` remains on `CameraHolder` so existing
`CutsceneSkillPresenter` references remain valid. Cinemachine runtime components
are created beneath that holder and the existing camera receives a
`CinemachineBrain`.

## Camera States

### Free Look

- Mouse delta orbits the camera.
- The camera never auto-recenters.
- WASD movement is camera-relative.
- The character faces movement direction.

### Shoulder Aim

- Hold right mouse button.
- The v1 camera stays on the right shoulder.
- Character yaw follows the camera's planar forward.
- Camera distance and FOV blend to their aiming values.
- Camera impulse strength is reduced.

### Hip Fire

Hip fire is allowed. A fired shot starts a short combat-alignment window so the
character turns toward camera yaw while spread and reticle bloom communicate
the lower precision.

## Aim Point and Muzzle Trajectory

`ThirdPersonAimController` casts from the center of `Camera.main` and writes the
validated result to `PlayerContext.aimTarget`. Projectile skills use the full
3D direction from their cast origin to this point.

Weapon projectiles do not spawn with the camera ray direction directly. The
weapon resolves a direction from `WeaponSystem.FirePoint` to the Aim Point,
applies the current spread cone, and stores that explicit direction in
`WeaponShotContext` and `WeaponProjectileSpawnContext`. Nearby walls therefore
block a shot even when the camera can see around them.

Player and companion colliders are ignored by friendly projectiles. Wall
collision remains active through `ProjectileLayerUtility`.

## Upper-body Aim

`ThirdPersonAimRigController` applies visual pitch only. Character root rotation
continues to own yaw, so the procedural pose does not double the horizontal
turn performed by player movement or AI facing.

The player follows the validated Aim Point during Shoulder Aim and the brief
Hip Fire combat-alignment window. Companions follow their AI Aim Point while
their weapon is aiming or firing. Releasing combat alignment blends the last
valid pitch back to the animation pose instead of resolving a new direction
toward world origin.

Pitch is clamped by `maximumUpperBodyPitch` (default `55` degrees) and
distributed across only the resolved Spine, Chest, and UpperChest bones.
It is applied after `CharacterPairOffsetApplier`, so authored locomotion/action
pair corrections remain part of the final pose.
When the model's configured fire-point bone is outside that torso hierarchy,
the controller creates a runtime pivot and carries `WeaponSystem.FirePoint`
through the same blended pitch around the resolved chest. Projectile direction
is still resolved independently from the resulting muzzle position toward the
existing Aim Point, including spread and muzzle-obstruction handling.
Rotation-locking states and exclusive full-body animation playback suppress the
procedural pose and blend it out. `maximumUpperBodyYaw` remains serialized for
compatibility but is not used by the pitch-only controller.

Humanoid Animators resolve the three bones through `HumanBodyBones`. Generic
visual prefabs must place `ThirdPersonAimBoneMap` on the Animator GameObject and
bind their rig transforms. A missing Generic map disables only the visual pose
and emits one warning; gameplay aiming and projectile direction are unchanged.
The bindings are resolved again whenever a character model is swapped.

## Collision and Occlusion

`CinemachineThirdPersonFollow.AvoidObstacles` pushes the camera inward for world
geometry. The `Ally` layer is excluded from only this follow-obstacle filter, so
companions do not push the camera toward the player; the Aim Point collision
filter remains unchanged. The old world-geometry cutout component is disabled
at runtime; levels are not modified for the camera.

When pushed very close, the local character fades using the ASP `_Dithering`
property. Every active character whose target identity is `Companion` fades
fully while its collider overlaps a `0.3`-meter capsule between the gameplay
camera and player pivot. All overlapping companions fade together, including
field allies and runtime helpers, without disabling their AI, colliders,
targeting, or other gameplay. The fade releases when they leave the capsule or
gameplay camera control hands off to a cutscene. Close-camera, companion
occlusion, and animation fades remain visual concerns; disabling the camera
fader releases only the camera-owned fade.

## Camera Recoil and Impulses

Gun data supplies hip/aim spread, movement penalty, bloom, recovery, and camera
kick. Stability reduces spread and recoil. `ShakeCamera` animation markers
still subscribe from the player, field allies, and helper, but now generate
short Cinemachine impulses. Shoulder Aim applies a lower impulse multiplier.

## UI and Cursor Ownership

Gameplay locks and hides the cursor. It unlocks and suppresses camera input
while inventory, passive tree, active-skill screen, pause menu, or cinematic
skill playback is active.

The pause menu creates a camera settings panel for horizontal sensitivity,
vertical sensitivity, invert Y, and FOV. Values use independent `PlayerPrefs`
keys, so old save data remains valid and missing values receive defaults.

World-space character UI uses the `WorldUI` layer and is rendered by the
`WorldUICamera` overlay in `CameraHolder`. `WorldUICameraSync` copies the main
camera transform and projection after Cinemachine updates so overhead health,
stagger, and ChainReady displays use the same framing in free look, Shoulder
Aim, and animated camera states.

## Cinematic Handoff

`CutsceneSkillPresenter` disables `GameplayCameraController` as before. The
controller disables its Cinemachine camera and brain, allowing the existing
camera-holder Animancer clip to drive the real camera. On re-enable, Cinemachine
invalidates its previous state and restores the preserved TPS yaw/pitch without
an isometric snap.

## Character Authoring

Every `CharacterStats` asset contains a `Third Person/TPS Profile`. The default
profile is valid when no character-specific calibration is supplied. Tune:

- pivot and shoulder offsets
- free/aim distance
- free/aim FOV
- pitch limits and sensitivity multipliers
- spine/chest/upper-chest pitch weights
- collision radius and fade distances

`CharacteContext.ResolveReferences` creates `ThirdPersonAimRigController` for
player and companion identities at runtime, including dynamically spawned
helpers and swapped character models. Generic character visual prefabs must
also author `ThirdPersonAimBoneMap`; the third-person authoring validator checks
every character registered in `CharacterDatabase`.

## Select Screen

`Assets/Scripts/SelectCharactor/CameraManager.cs` continues to switch its own
Cinemachine cameras for character/map selection. It is separate from gameplay
TPS state.
