# Prefabs And Authoring

This project relies on prefab-authored context blocks plus runtime reference
resolution. Use this document when creating or editing character prefabs,
pickup prefabs, weapon data, or inspector-facing fields.

## Character Prefab Context

Player, ally, and enemy prefabs should have a context component as the main
reference hub:

- player: `PlayerContext`
- ally: `AllyContext`
- enemy: `EnemyContext`

Common references should be assigned on the context when possible:

- `StateHub`
- `StatsHub`
- `CombatEventBus`
- `WeaponSystem`
- `CharacterAnimBrain`
- `CharacterAnimDriver`
- `CharacterPairOffsetApplier`
- `ThirdPersonAimRigController` (runtime fallback is available)
- `HealthSystem`
- `StaminaSystem`
- `DashSystem`
- `SkillManager`
- `PassiveController`
- `CharacterEquipment`
- `AccessoryLoadout`
- `AITargetInfo`

Runtime code can resolve missing references, but important prefab context fields
should still be bound when authoring stable production prefabs.

### Shared Helper rig contract

`Assets/Prefab/Player/Ally_Helper.prefab` is a shared rig created with the party and hidden between
executions. It is not a `SummonController` object and must remain on the existing Helper rig
architecture. Validate it with **Tools > RB > AI > Validate Helper Rig**. The rig must contain and
bind:

- `AllyContext`
- `CharacterSkillManager`
- `CharacterActiveSkillProgress`
- `CharacterAnimDriver` and `CharacterAnimBrain`

The loaded character's `CharacterStats` supplies the manual command and selected Helper proc
variants at runtime. Keep local authoring references such as animation, model, hitbox, fader, and
teleport-probe fields on the rig; they are not replaceable by context lookup.

Runtime Helper placement uses deterministic candidate rings through
`CharacterPlacementResolver`, including the targeted delivery ring and the fallback ring around
the player. For a targeted skill, the cached animation trajectory and cast-point target impact are
part of that preflight, and the eventual Helper skill playback must enable planar root motion. The
resolver sweeps between animation samples and treats actor-context colliders as actor overlap even
when masks overlap. The Helper is mobile, so a valid NavMesh footprint is required; if no candidate
passes, the summon is rejected before activation or cast rather than using an unvalidated position.

## Character Vertical Motor

`CharacterVerticalMotor` is the single owner of a character's Y axis. Every planar
mover in the project (`PlayerMovementCC`, `DashSystem`, `CharacterKnockbackMotor`,
`AgentMoveDriver`) is deliberately planar-only, so without this component nothing
brings an actor back down once it leaves the ground.

Put it on the same GameObject as the context component and pick the mode that
matches how the actor is moved:

- `Always` — for actors held up only by a `CharacterController`, such as the
  player. Gravity integrates every frame.
- `AgentDriven` — for `NavMeshAgent` actors (ally, enemy, summon). The motor stays
  dormant while the actor is grounded, because the agent already holds it on the
  NavMesh surface, and only takes over once something calls `Launch()`.

Fields to bind:

- `ctx`, `characterController`, `capsuleCollider`, `animBrain`
- `navMeshAgent` and `agentMoveDriver` on `AgentDriven` actors. If a prefab has an
  agent but no `AgentMoveDriver`, the motor suspends the agent directly instead;
  binding the driver is still preferred because its token API reference-counts
  against other systems that suspend the same agent.
- `rootMotionCCDriver` / `rootMotionNavMeshDriver`, so the motor can stand down on
  frames where a root motion clip is writing its own Y. When `CharacterVisualController`
  creates or replaces a model's runtime driver, it explicitly rebinds the active driver to
  `CharacterVerticalMotor` immediately after configuration; the inactive driver reference is
  cleared, so model rebuilds do not depend on a later hierarchy lookup.
- `groundMask` and `collisionMask` — production prefabs use
  `Default | Ground | Ground Y | Terrain`, because scene ground geometry is not
  consistently on the `Ground` layer.

A prefab without the component behaves exactly as it did before the motor existed:
`StateHub.IsGrounded` reports grounded, and nothing applies gravity. The
`Assets/Test/Summoning` fixtures are intentionally left without it.

Do not add a second system that writes Y. Anything that needs to move a character
vertically should call `Launch` / `AddVerticalVelocity`, or hold a gravity-suspend
token via `AcquireGravitySuspendToken` for levitate-style effects.

`KnockbackData.VerticalImpulse` (default `0`) is the authoring hook for launching
knockbacks: leave it at zero for a purely planar knockback, exactly as before.

## Runtime Party Spawn Authoring

Gameplay scenes no longer place or instantiate `PlayerSquad.prefab`. Each scene
must contain exactly one active `PartySpawnPoint`, assigned to
`Assets/Data/Party/DefaultPartySpawnConfig.asset`. The config directly references
the four actor roles and the separate `PlayerUI` prefab:

- `Player`: `Player.prefab`, party index 0
- `PartySlot1`: `Ally_Stryker.prefab`, party index 1
- `PartySlot2`: `Ally_Stryker.prefab`, party index 2
- `Helper`: `Ally_Helper.prefab`, party index 3

The marker transform is the party origin. Each config entry's local position and
rotation are applied relative to it. Do not also place a `PlayerContext`,
`AllyContext`, or `PlayerUIContext` in the same scene; runtime validation treats
that as an ambiguous duplicate setup and refuses to spawn.

`PartySpawnPoint` builds an inactive `PartyRuntimeRoot`, binds party indices,
chain roles, helper management, Behavior Designer's `player` variable, camera
receivers, and Player UI, then activates the completed party atomically. Scene
systems that require the runtime player should implement `IPartySpawnedReceiver`
instead of serializing references into a legacy squad instance.

`Player.prefab` must also contain `PartyFormationController`, referenced by
`PlayerContext.partyFormation`. The authored defaults are a rear triangle at
`(-1.5, 0, -1.8)`, `(1.5, 0, -1.8)`, and `(0, 0, -3.2)`, with a single-file
fallback at Z distances `-1.8`, `-3.2`, and `-4.6`. Keep the movement hysteresis
at 1.25 m start / 0.6 m stop unless the companion agent radius or character
scale changes. Formation slots are assigned from party indices 1-3; do not use
chain actor roles as formation authoring data.

Use **Tools > RB > Party > Create Or Update Runtime Party Setup** to refresh the
default config and migrate the supported gameplay scenes. Use
**Tools > RB > Party > Run Party Spawn Smoke Tests** after changing a party
prefab, the UI prefab, the config, or a gameplay scene. `PlayerSquad.prefab`
remains only as a legacy migration source and is not a runtime dependency.

## Interaction Indicator UI

`Assets/Prefab/User Interface/InteractionIndicator.prefab` is the shared
world-space prompt for every `IInteractable`. `PlayerUI.prefab` owns one
`InteractionIndicatorPresenter`; `PlayerUIRuntimeBinder` binds it to the
runtime Player and the presenter reuses a single indicator instance rather
than adding a Canvas to every interactable.

The indicator appears only for the interactable currently focused by the
Player `Interactor`. Its center label reads the active Input System binding for
the existing `Interract` action, and its action label comes from
`IInteractable.GetPrompt`. Keep prompts short, such as `Heal`, `Refill Ammo`,
`Open Shop`, or `Revive`.

While the Player is aiming, the `Interactor` resolves focus along the
`ThirdPersonAimController` camera ray, so the reticle selects the interactable.
The target must still be inside the Interactor's authored `maxDistance`; camera
aim does not grant long-range interaction, and the aim collision hit prevents
selection through blocking geometry. When the Player is not aiming, focus uses
the existing sphere cast in front of the character. Keep interactable colliders
on a layer included by the Player Interactor's `interactMask`.

For normal objects, the indicator follows the center of the collider hit by the
Interactor. Add `InteractionIndicatorAnchor` to an interactable hierarchy only
when that automatic center is unsuitable, then assign its `Anchor Override`
Transform. The indicator faces the gameplay camera and scales with camera
distance to preserve a consistent apparent size. It uses the `WorldUI` layer,
which must remain rendered by the `WorldUICamera` overlay.

Only `IHoldInteractable` targets fill the radial progress ring. Progress uses
that target's `HoldDuration`, resets immediately when the input is released or
the hold becomes invalid, and pulses once when the completed interaction is
accepted. Instant interactions retain their existing one-press behavior.

Edit colors, type sizes, and ring geometry on the indicator prefab. Transition
and completion-pulse timing are exposed on `InteractionIndicatorView`; distance
scaling and the action name are exposed on `InteractionIndicatorPresenter` in
`PlayerUI.prefab`. Rebuild the generated prefab and restore its PlayerUI binding
with **Tools > RB > UI > Build Interaction Indicator Prefab**.

## Map Room Spawn And Party Warp

`MapRunController` places the player at the entrance selected by
`RoomController.GetPlayerSpawnPoint`. Active companions are then placed
individually in a small formation in front of the player and must successfully
warp onto the spawned room's NavMesh. Their position relative to the initial
`PartyRuntimeRoot` formation before the room transition is intentionally not
preserved.

Every entrance `SpawnPoint` must face into the room and sit near a walkable
NavMesh area. Keep enough clear walkable space in front of it for the player
and companion formation. A room transition fails visibly instead of starting
an encounter with an active companion stranded off NavMesh.

Each room prefab used by `MapRunConfigSO` must carry baked `NavMeshData` on its
`NavMeshSurface`. `MapRunController` loads that baked data with the spawned
prefab and does not rebuild NavMesh at runtime during room transitions.

Room instances are created lazily per `MapNode.Id` and cached until the run
ends. All cached rooms use the same room spawn anchor, so only the current room
may be active. Components inside a room must tolerate repeated
`OnEnable`/`OnDisable` calls and must not duplicate authored content each time a
cached room is revisited.

`RoomController` creates `RuntimeContent/Persistent`, `Encounter`, and
`Temporary` children automatically. Do not add serialized prefab references
for these runtime roots. Normal item drops use `Persistent`, spawned enemies
use `Encounter`, and room-scoped disposable objects may use `Temporary`.

Test Stage combat rooms must author four `enemySpawnPoints` because the current
encounters allow at most four simultaneous enemies. Additional enemies are
created by later waves after the prior wave clears. The Test Stage Boss room
must author one Boss spawn point plus `stageExitSpawnPoint`; keep the exit away
from the Boss spawn so the portal remains usable around the corpse and drops.
The Stage Exit root and its trigger collider must use the `Interactable` layer
so the Player `Interactor` sphere cast can focus it and show the return prompt.

The supported Test Stage enemy prefabs are the four Variant prefabs under
`Assets/Prefab/GameEnemy`. Each must bind its canonical `CharacterStats` asset
and carry `EnemyLevelSystem` on the `EnemyContext` object. Do not put a fixed
combat level on these prefabs: `MapRunController` assigns it from the selected
stage when the encounter spawns the enemy.

Canonical Test Stage stats replace combat numbers only. They must retain the
matching Variant's character prefab, Avatar, Animator Controller, Anim Profile,
behavior subtree, weapon-hand mode, and combat role. An empty `animProfile`
prevents `CharacterAnimBrain` from initializing and leaves the enemy model
without locomotion or combat animation.

Use **Tools > RB Project > Map > Apply Test Stage Content** only when the
canonical Test Stage data, room sockets, enemy stat/weapon/drop-profile
bindings, or Basement board must be regenerated. The authoring tool is
idempotent but deliberately rewrites those owned assets. The Test Stage Heal
room prefab should author one `HealInteractable` station and one
`AmmoRefillInteractable` station, each with an `InteractableLink`, collider, and
the `Interactable` layer.

The Heal room prefab must also carry a `TestStageRecoveryStations` component next to its
`RoomController`. That component is what configures the stations as a one-use 50% party Heal Point
and a reserve-only party Ammo Point, and it creates simple runtime fallbacks only when an authored
station is missing. `RoomController` no longer contains this behaviour: it only drives
`IRoomLifecycleListener` components, so a Heal room prefab without the component silently gets no
recovery stations. **Tools > RB Project > Map > Validate Map Content** reports that as an error for
any Heal room a Test Stage can route through. The authoring tool binds
the existing `RoomDefinition.Heal` into all three Test Stage configs without
rewriting that room asset. Keep the definition enabled, assigned to a prefab,
and at two or more exits; the pre-Boss blue-node validator requires a usable
multi-exit Heal definition.

### Catalog-driven board pages

`StageCatalogBoardPage` fills a page from a `StageCatalogSO` at runtime: it clones a disabled
`StagePlacardButton` template once per catalog stage and binds each clone's run config. Adding a
stage then means editing the catalog asset instead of authoring another placard in the scene.

It is opt-in. A page without the component keeps exactly what was authored into it, which is how
`ExistingMapsPage`, `TestStagePage`, and `BossRushPage` still work today. Use it for new pages; the
existing pages are not migrated automatically, because doing so would hand the tool ownership of
placards it does not own.

### Basement board pages the authoring tool does not own

**Tools > RB Project > Map > Apply Test Stage Content** owns only
`ExistingMapsPage`, `TestStagePage`, the placards it puts inside `TestStagePage`,
and the `PreviousPage` / `NextPage` arrows. Its placard layout has room for
exactly three stages. Stages beyond those three are authored by hand as
additional pages under `MapUI/TestStagePagination`, and the tool neither creates
nor rewrites them.

The tool never destroys `TestStagePagination` and never moves a placard that
already sits inside one of its pages. It reuses the objects it owns instead of
rebuilding them, so re-running it produces no new object ids, no duplicate
components, and no duplicate `onClick` listeners. In the `MobilizBoardPager`
`pages` array it writes index `0` and index `1` only; every other registered page
keeps its identity and its relative order, and `initialPage` is left as authored.
Scene mutations go through the Unity `Undo` API.

Run **Tools > RB Project > Map > Validate Basement Board (Dry Run)** first. It
opens the Basement scene read-only and logs exactly what the apply step would
create, update, adopt, or remove, plus the resulting page order — without writing
anything. `Apply Test Stage Content` logs the same report after it runs.

`BossRushPage` is the first such page. It holds one placard for BOSS RUSH 01 and
is registered as index `2` in the `MobilizBoardPager` `pages` array. To add
another hand-authored page:

1. Create a `RectTransform` child of `TestStagePagination` with anchors
   `(0.12, 0.10)`-`(0.88, 0.86)`, matching `TestStagePage`.
2. Add a placard child with `Image`, `Shadow`, `Button`, `StagePlacardButton`,
   and a TMP label, then assign the `MapRunConfigSO` to the button's `runConfig`
   and wire `Button.onClick` to `StagePlacardButton.EnterStage`.
3. Append the page to `MobilizBoardPager.pages`. Leave `initialPage` at `0` and
   set the new page inactive; the pager toggles visibility on `Awake`.

Do not renumber or reorder the first two pages. Re-running the authoring tool
removes any `StagePlacardButton` inside `TestStagePage` that it does not own, so
hand-authored placards belong on their own page, never inside `TestStagePage`.

When scripting this through the Unity Editor API, load the `MapRunConfigSO`
**after** opening the Basement scene. Opening a scene in `Single` mode unloads
unreferenced assets, so a reference loaded beforehand becomes a destroyed object
and assigning it silently writes `null`.

## Stage Intro Rig Authoring

The MapRun stage intro lives on a shared `StageIntroRig.prefab`, nested under the
Start room prefab (`Assets/Prefab/MAP/Start/Start.DeadEnd.Up.prefab`) so its
markers and camera follow the room instance transform. Create the rig with
**Tools > RB > Map > Create Stage Intro Rig Prefab**; it writes
`Assets/Prefab/MAP/StageIntro/StageIntroRig.prefab` with the contract wired.

Required contents:

| Object | Component | Notes |
| --- | --- | --- |
| Rig root | `StageIntroRig` | discovered with `GetComponentInChildren` under the room instance |
| `Markers/Marker_<Role>` | `StageIntroActorMarker` | exactly one per role: `Player`, `PartySlot1`, `PartySlot2`, `Helper` |
| `CameraAnimationRoot` | `Animator` + `AnimancerComponent` | driven by the Camera Clip |
| `CameraAnimationRoot/IntroCamera` | `CinemachineCamera` | the rig enables it and raises its priority for the intro, then forces it back off |

The rig always switches the intro camera off when the intro ends, rather than
restoring whatever state the prefab shipped. An intro camera left enabled would sit
at the same Cinemachine priority as the gameplay camera, and the brain keeps
whichever activated last — so gameplay would never get its camera back.

The rig also cancels any scale inherited from the room instance (`Start.DeadEnd.Up`
carries a root scale of 1.33), so marker offsets and the camera rig play back at
exactly the size they were blocked out at in Prefab Mode. It still inherits the
room's position and rotation, which it needs: `MapRunController` instantiates rooms
with a per-node yaw, so a rig outside the room hierarchy would face the wrong way.
That is why the rig must stay nested under the room and cannot be its own scene root.

Overhead health bars are world-space and are not covered by
`UIManager.SetHudVisible`, so each actor's `CharacterVisualController` bar is hidden
for the duration of the intro and restored to its previous state afterwards.

Each actor also holds a `CharacterVerticalMotor` gravity-suspend token for the whole
intro. Disabling the `CharacterController` is not enough on its own: the motor falls
back to writing the transform directly when the controller is off, so an actor
parked on a marker would keep sinking through the floor for the length of the shot.
The token is released after the pose is restored.

The Helper is a summon: `AllyHelperManager.Start` deactivates it, and every command
hides it again once the skill finishes, so it would otherwise be missing from its
marker. The intro holds it on screen through
`AllyHelperManager.BeginCinematicAppearance` / `EndCinematicAppearance`. The hold is
a flag the manager checks before hiding, which also settles an ordering hazard:
`MapRunController` runs at the default execution order while `AllyHelperManager` is
at 100, so the intro starts first and the manager's own `Start` would otherwise pull
the helper off screen mid-shot. Positioning deliberately does not go through the
summon path; normal Helper placement now uses deterministic candidate rings and the central
placement resolver, while the intro still owns its exact marker pose.

`Camera Clip` on the rig is the group-shot camera animation and the **master
duration** of the intro. Until an author assigns a real clip the rig fails
validation and the intro is skipped, so the stage still starts normally. Do not
generate placeholder camera motion to satisfy the field.

Opening beat on the rig: the screen goes fully black, the party is placed and
locked while nothing is visible, the rig holds on black for `blackHoldSeconds`
(0.35s), and then the performance and the fade start on the same frame — the
camera clip and the character poses play *underneath* the `fadeInDuration` (0.6s)
fade rather than after it, so the shot is already moving as it is revealed. The
intro's on-screen length equals the camera clip length exactly, because the fade no
longer runs as a separate step before the timeline starts.

Remaining presentation defaults: 0.2s fade out, 8% letterbox per side, 0.75s
hold-to-skip. The screen-space overlay (black fade, letterbox bars, skip label,
hold progress) is built at runtime; nothing has to be assembled by hand.

### Character Intro Poses

The intro pose is `CharacterAnimProfileSO.stageIntroClip` (`Stage Intro` header),
alongside every other clip the profile owns. It is deliberately **per profile, not
per character**: characters that share an anim profile share the intro pose, which
is the same rule as their locomotion, dash, and status poses.

Clips must be **in place** — planar root motion is never applied, and the runtime
state forces `applyRootMotion` off. A clip shorter than the Camera Clip holds its
last frame. An empty field falls back to the profile's locomotion idle blend.
`CharacterAnimBrain` reads the clip through its bound profile, so
`SetAnimProfileOverride` swaps the intro pose along with everything else.

Assignments:

| Anim profile | Stage Intro Clip | Characters using it |
| --- | --- | --- |
| `Roma_AnimProfile` | `Roma_Intro_Sit` | Roma, Milano, Dorothy, Noemi, Roger, Abbygail |
| `Feno_AnimProfile` | `Feno_Intro_Sit` | Feno |
| `Aires_AnimProfile` | `Aires_Intro.Stand` | Aires |

Run **Tools > RB > Map > Assign Stage Intro Clips** to (re)apply them. The tool
exists because these intro FBX files have no `clipAnimations` entry in their meta,
so their AnimationClip is the importer-generated default take and its fileID cannot
be written into an asset by hand — the tool resolves it through the AssetDatabase,
which also makes it safe to re-run after a reimport.

Six of the eight characters share `Roma_AnimProfile`, so they all strike Roma's
pose until they are given their own profile. `Milano_Intro_Stand` therefore has no
profile to live on and is currently unused; the tool reports this rather than
silently dropping it. Give a character its own intro pose by giving it its own
`CharacterAnimProfileSO`.

### Editor Preview

The `StageIntroRig` inspector works in Prefab Mode without entering Play Mode:

- **Preview Roster** gives each of the four roles a **Character** dropdown fed by
  the project's single `CharacterDatabase` (all entries, unfiltered — unlock state
  belongs to a save slot and has nothing to do with blocking a shot) and an
  optional **Intro Clip (Preview)** override;
- **Spawn Preview Party** clones each selected character's
  `CharacterStats.CharacterPrefab` and places it on that role's marker;
- **Clear Preview** removes them;
- **Play / Pause / Stop** and the **Time** slider (`0..cameraClip.length`) sample
  the Camera Clip and each character clip at the same time through Unity's
  Animation Mode;
- **Look Through Intro Camera** / **Frame Now** drive the Scene view camera to
  the intro camera's sampled pose and lens FOV. A `CinemachineCamera` does not
  render on its own without a `CinemachineBrain`, which Prefab Mode has none of,
  so this is how the framing is checked inside Prefab Mode;
- **Preview In Game View (Solo)** is enabled whenever the open scene has a game
  `Camera`. It sets `CinemachineCore.SoloCamera` to the intro camera so the Game
  view renders the real shot at the real aspect ratio while scrubbing.

`GameplayCameraController` only adds its `CinemachineBrain` in `EnsureRuntimeRig`
at runtime, so an Edit Mode scene normally has a camera and no brain. The preview
therefore adds a `HideFlags.DontSaveInEditor` brain to the resolved camera itself.
Switching the toggle off — and clearing the preview, disabling the inspector, or
an assembly reload — destroys that brain, restores the camera's authored pose, and
restores the intro camera's enabled state. Do not save the scene while the toggle
is on. To use it, drag the Start room prefab into a gameplay scene, preview there,
then remove it without saving.

The preview clones the **character prefab**, not the party actor prefab.
`Player.prefab` and `Ally_Stryker.prefab` carry no model at all: their
`modelRoot` is empty until `CharacterVisualController` instantiates
`CharacterStats.CharacterPrefab` into it at runtime. The preview therefore
instantiates that prefab directly and copies `CharacterStats.characterAvatar` onto
its `Animator`, mirroring `ConfigureAnimatorRuntime` so shared clips retarget the
same way they do in game. `modelRoot` sits at local origin on both actor prefabs,
so placing the clone straight on the marker matches runtime placement. Weapons are
not built — that would mean running equipment logic in Edit Mode, and a group shot
does not depend on it.

The intro pose is cut in with a zero fade, not cross-faded. Blending out of the
locomotion mixer interpolates root and hip rotation between two unrelated poses, so
the character visibly swings around into place; the cut happens under the fully
black overlay instead. A `ClipTransition`'s authored fade duration is therefore
ignored for the stage intro — its speed, events, and start time still apply.

Clip resolution per slot: the **Intro Clip (Preview)** override first, then the
character's `CharacterAnimProfileSO.stageIntroClip`, then the locomotion idle — the
same fallback chain as the runtime state, so leaving every override empty previews
exactly what ships. Because `AnimationMode.SampleAnimationClip` applies a clip's
root curves while the runtime state forces root motion off, each actor is snapped
back onto its marker after every sample.

The override is a scratch value and is **not** persisted; only the character
selection is, in `EditorPrefs` keyed by the rig's prefab GUID, so nothing about the
preview reaches the prefab or the build. An override that differs from
`stageIntroClip` is not flagged — committing a pose means editing the character's
`CharacterAnimProfileSO`.

Preview actors are owned by `StageIntroPreviewSession`, not by the inspector, so
selecting and dragging a marker does not destroy them and they follow markers live.
They use `HideFlags.DontSaveInEditor` and are removed by **Clear Preview**, closing
the prefab stage or scene, an assembly reload, and entering Play Mode — that last
one matters because `DontSaveInEditor` objects otherwise survive the edit-to-play
scene reload and would stack four ghost actors on the real party. Sampling runs
through Animation Mode and the scene's dirty flag is restored, so previewing never
dirties the prefab or scene. With no Camera Clip assigned, playback and scrubbing
are disabled but spawning the preview party still works so markers can be blocked
out.

The inspector also lists validation problems: missing or duplicated marker roles,
a missing marker reference, a missing `CinemachineCamera` or camera animation
root, and an empty or zero-length Camera Clip.

## Third-Person Character Calibration

Each `CharacterStats` asset owns a `Third Person/TPS Profile`. Use it to
calibrate the camera without adding character-specific branches to the camera
controller:

- `pivotOffset`: upper-body follow origin
- `shoulderOffset`, `cameraSide`: shoulder framing (`cameraSide = 1` for v1)
- `cameraDistance`, `aimCameraDistance`: free/aim distance
- `freeLookFov`, `shoulderAimFov`: authored FOV relationship
- pitch limits and sensitivity multipliers
- spine/chest/upper-chest pitch weights
- camera collision radius and close-camera fade distances

The default initialized profile is a valid fallback. Player and companion
contexts add the procedural upper-body aim component at runtime, so dynamically
spawned helpers and character model swaps use the same profile. Upper-body aim
uses pitch only while character root rotation owns yaw. It follows the validated
Aim Point during Shoulder Aim, Hip Fire combat alignment, and companion weapon
activity, then blends back when alignment ends or a rotation-locking/full-body
state takes ownership.

Humanoid Animators resolve Spine, Chest, and UpperChest automatically. Every
Generic character visual prefab must add `ThirdPersonAimBoneMap` to the same
GameObject as its Animator and assign:

- `Spine`: the lower torso rotation bone
- `Chest`: the upper torso rotation bone
- `Upper Chest`: optional when the rig has a third torso bone

All assigned transforms must be unique descendants of that Animator. The
current roster maps `spine_01.x` to Spine and `spine_02.x` to Chest, leaving
Upper Chest empty. Missing or invalid Generic mappings fail third-person
authoring validation. At runtime a missing map disables only the visual
upper-body pose; Aim Point resolution and projectile trajectories still work.
Model swaps cause the controller to discard old bone references and resolve the
new visual rig.

Keep the actor's authored `FirePoint` binding and `firePointBoneName` setup
unchanged. If that fire-point bone (the current roster uses root-level
`c_traj`) is not below Spine/Chest/UpperChest, the upper-body controller creates
a runtime-only pivot below it and moves the muzzle origin through the blended
pitch. If the fire point already belongs to the torso hierarchy, no extra pivot
is added. Releasing aim, entering a rotation-locking/full-body state, or
disabling the component restores the pivot to the authored pose. Projectile
direction continues to target the existing Aim Point from the adjusted origin.

`Assets/Prefab/System/CameraHolder.prefab` remains the gameplay camera root.
`GameplayCameraController` creates its Cinemachine camera/brain at runtime so
existing scene instances and cutscene camera animations keep their serialized
references.

Keep `WorldUICamera` in the main camera's URP camera stack with a culling mask
containing only the `WorldUI` layer. Its `WorldUICameraSync.sourceCamera` must
reference the tagged main Camera so the overlay follows the Cinemachine camera
transform and projection. Do not remove this synchronization while the main
Camera excludes `WorldUI`.

Run **Tools > RB Project > Validate Third Person Authoring** after changing the
player prefab, camera prefab, input actions, or Build Settings scenes. The same
validator is available to batch mode through
`ThirdPersonAuthoringValidator.ValidateBatchMode`.

## Axis Rotation Driver

Add `AxisRotationDriver` to the Target GameObject and assign `Source` to the
transform whose rotation should drive it. Pick `Source Axis` / `Target Axis`
from the dropdowns to remap rotation across axes (for example Source Y drives
Target Z). After wiring `Source`, run **Capture Rest Pose** so `Maintain
Offset` preserves the authored pose instead of snapping to an absolute angle;
the rest pose auto-captures the first time `Source` is assigned, but re-run
the button any time the authored pose changes.

`Preview In Edit Mode` drives the target continuously outside Play Mode and
marks the scene dirty while it runs. Leave it on only while authoring the
rig; disable it or save the scene once the setup is confirmed.

Only one `AxisRotationDriver` is allowed per GameObject
(`DisallowMultipleComponent`). For chained rigs, such as a two-stage gear
train, add a second driver to the intermediate GameObject and point the next
driver's `Source` at it — each driver pulls its upstream driver's `Evaluate()`
recursively so the whole chain updates in the same frame.

Prefab boundary: if `Source` lives in a different prefab than the Target, the
reference can only be saved as a scene override, and the captured
`sourceRest`/`targetRest` become per-instance overrides as well.

## Character Skill Loadout Authoring

Author character-default active skills, and any character-owned passive with
its own upgrade tree, on the `CharacterStats` asset under `Skill Loadout`. A
slot represents one command slot and should have a stable `slotId`, hotkey,
default option index, and one or more options. An option's `skillAsset` field
accepts either a `SkillGemDefinition` (active) or a `PassiveDefinition`
(passive) — the asset kind decides the slot's execution mode.

Use explicit `optionId` values when multiple options in the same slot could
reference skills with missing or duplicate `skillId` values. If `optionId` is
empty, runtime save/load uses the skill definition's `skillId`.

`CharacterSkillManager` builds runtime command slots from `ctx.baseStats`.
`CharacterStats` is authoritative for every slot index it defines. Prefab-authored
`autonomousSlots` are still supported only as legacy fallback slots when
`CharacterStats` does not define that index. New character prefabs should leave
active-skill choices in `CharacterStats`. There is no separate `passiveSlots`
field on `CharacterSkillManager` anymore — it was removed (was always
empty in every prefab, so nothing authored is lost).

Helper proc options also require an explicit stable `slotId`, `optionId`, unique Helper proc id,
and a `SkillHelperDef.executionSkill`. The selected option is resolved into a runtime proc entry;
unselected options are authoring data only.

### Authoring A Passive-Kind Slot

- Give it `hotkey: None` — passives are never castable, and the validator
  warns on a passive slot with any other hotkey.
- Put it **last** in `skillSlots`. `CharacterSkillManager` resolves the
  runtime slot index 1:1 with the authored index, so a passive slot before an
  active one would shift every active slot's index; the validator errors on
  this.
- Do not mix an active and a passive option in the same slot — the validator
  errors on it. A slot's options must all be the same kind.
- If the passive is migrating off `CharacterStats.passives` (the flat list),
  clear that list in the same change — `PassiveController` uses configured
  passive slots *instead of* `passives` the moment any slot is passive-kind,
  not in addition to it.
- An `AlwaysOnPassiveDef` option cannot resolve an upgrade tree in Phase 1 (no
  gate mechanism exists for its unconditional modifiers) — the validator
  errors if `upgradeTreeOverride` or the definition's own `upgradeTree` is set
  on one.

See `RB_Project\Docs\SYSTEMS\PASSIVES.md` for the runtime model.

## Node-Centric Ability Authoring Workflow

The normal way to give a skill-tree node a new ability is entirely inside
**Tools > RB > Skills > Active Skill Tree Editor** — no Skill Inspector, no
raw upgrade-id typing:

1. Select the node.
2. In **Gameplay Effects**, click **+ Add Ability**.
3. Pick a payload type from the picker (grouped by category — only
   descriptor-backed types appear; see `Docs/SYSTEMS/SKILL_SYSTEM.md` **Node-
   Centric Ability Authoring** for what makes a type eligible).
4. Fill in the curated fields the descriptor draws. Required asset references
   (a Taunt Status, a Morph model prefab, a Pickup prefab, ...) are never
   pre-filled with a guessed value — leave one empty and **Create Ability**
   stays disabled with the missing requirement shown as an Error.
5. Review the **Gameplay Summary** (plain-language sentence plus detail lines)
   and any Errors/Warnings. A Warning still lets you continue after one
   confirmation; an Error blocks Create until fixed.
6. Click **Create Ability**. The card appears on the node immediately. If this
   was the skill's first additional ability, the skill's existing single
   payload is automatically converted to a composite first, with its data and
   execution settings preserved (nothing is destroyed or reconfigured by
   hand).
7. Save the tree from the toolbar when ready — Save now runs the same unified
   validation (see `Docs/VALIDATION.md`) and stops on any Error.

**Editing** an ability: click **Edit** on its card, change fields, review the
same summary/validation, click **Apply Changes**. **Duplicating**: click
**Duplicate** — this is a one-click action with no wizard step, since there
are no new fields to review; it clones the configuration and mints a new
binding id immediately. **Removing**: click **Remove**, confirm; the payload
is deleted only if no other step still references it, and the node's granted
id is revoked only if nothing else still gates on it.

**Where a status effect is edited.** `+ Add Ability` is the only authoring
entry point in the normal flow, so a status effect is edited wherever it
actually lives:

| Where the status lives | Where you edit it |
| --- | --- |
| Bundled inside a node-owned ability | that ability's card → **Edit** |
| Gated onto an always-active payload | select **no node** → **Always Active Skill Effects** → **Edit** |
| A standalone status ability | `+ Add Ability > Apply Status to Self` |

**Always-active effects.** A step with a blank gate runs unconditionally and is
owned by the skill, not a node, so it is not shown on any node. Deselect the
node to see the skill-level **Always Active Skill Effects** list. Note that a
node's granted id can still gate a *conditional status inside* an always-active
payload — that is a normal, supported pattern (most of `Aires_Skill_3`'s status
effects work this way), and those entries are edited from this section, not
from a node card.

**Advanced/Developer mode.** The node inspector's collapsed **Advanced /
Developer** foldout holds the older **Additional Gated Status Effects** wizard
(kept for creating status assets inline, scope repairs, and duplicate-effectId
checks — see `Docs/SYSTEMS/SKILL_SYSTEM.md`). The ability wizard's own
**Advanced** section, and the tree window's raw **Skill Steps** panel, can show
every serialized field on a payload including ones the curated wizard fields
don't expose. None of these let ownership, embedding, or upgrade-id bindings
change outside `NodeAbilityAuthoringService` — there is no raw `PayloadStep`
creation, no direct `requiredUpgradeId` typing, and no direct
`grantedUpgradeIds` typing in the normal flow.

**Legacy migration (completed).** The project's only direct-gameplay step
(`HealAreaStep`, used by `Aires_Skill_3.asset`) was migrated to
`HealAreaSkillPayloadDef` + `PayloadStep` and the legacy type was then
deleted — see `Docs/VALIDATION.md` **Unified Authoring Validation** for the
retirement gate that was checked before removal. There is nothing left to
migrate; a `HealAreaStep` reference reappearing in an asset means it was
restored from an old revision.

## Active Skill Tree Upgrade-Id Authoring

`SkillUpgradeNodeData.grantedUpgradeIds` marks the behavior flags a tree node grants; steps and
payloads query them at runtime through `SkillCastContext.HasUpgrade`. The ids themselves are never
typed on the tree first — they originate on the payload's own fields (a `SkillEffectStep`'s
`requiredUpgradeId` gate, or a `ConditionalStatusRoute`'s
`conditionalStatuses.applications[].requiredUpgradeId` list, such as on `TauntSkillPayloadDef`,
`ApplyStatusSkillPayloadDef`, or `HealAreaSkillPayloadDef`). The tree only picks from what the payload already
declares — that is the single source of truth for a given id. Node-owned ability ids should be
authored through the **Node-Centric Ability Authoring Workflow** above rather than typed here
directly; this raw picker remains for status-route gates and Advanced/Developer authoring.

### Adding a conditional status route to a new payload or step

Editor tooling (wizard, tree summary, `UpgradeIdUsageScanner`) discovers routes by declaration, so
a new payload/step needs no change to any editor file. Add:

```csharp
[SerializeField]
[LabelText("Conditional Status Effects")]
[SkillStatusRouteTarget(SkillStatusTarget.Self, "My Payload")]
private ConditionalStatusRoute conditionalStatuses = new();
```

Use the string overload when the target depends on authored behavior — the named member must be on
the same type and return a `SkillStatusTarget`:

```csharp
[SkillStatusRouteTarget(nameof(ResolvedConditionalStatusTarget), "Heal Area")]
```

Then wire the three call sites in the owning behavior, which is the part tooling cannot do for you
because only the behavior knows *when* and *on whom* the status should land:

- `route.ApplyUnlocked(context, controller, source, fallbackDuration)` after the behavior has
  resolved its real targets,
- `route.CollectUpgradeIds(ids)` in `CollectUpgradeIds`,
- `route.CollectValidationIssues(issues, "conditionalStatuses")` in `CollectValidationIssues`.

A route with no `[SkillStatusRouteTarget]`, or one pointing at a member that does not exist or does
not return a `SkillStatusTarget`, is a blocking authoring error — it is reported in the wizard and
in the inspector's `Target` line instead of defaulting to `Self`. More than one route per
payload/step is allowed, including several with the same target.

In the Active Skill Tree Editor (or the plain Inspector on a `SkillUpgradeTreeDefinition` asset),
each entry in `Granted Upgrade Ids` is a dropdown listing every id declared by the tree's owning
skill(s) (resolved from `SkillGemDefinition.upgradeTree` / `CharacterSkillLoadoutOption
.upgradeTreeOverride` references), plus a `Custom…` entry that switches that element to free text
for the rest of the session. Use `Custom…` when authoring a tree before its owning skill's payload
exists yet, or when typing an id that hasn't been added to the payload. An authored id that no
longer matches the payload is shown with a warning tint instead of being silently cleared —
`SkillUpgradeTreeValidator` will also flag it as an error once an owning skill is resolvable.

## Direct Serialized References

Direct serialized references are appropriate for:

- local transforms
- hitboxes
- fire points
- model roots
- prefab assets
- databases and config assets
- UI fields
- authoring-only component settings
- component-specific local data that does not belong in shared context

Do not remove local authoring references such as `hitboxTrigger`, `firePoint`,
`modelRoot`, `healthBarPrefab`, database references, or prefab references just
because a context exists.

## Enemy Hit Zone Authoring

Enemy visual prefabs that support part-based direct weapon damage must contain
one `CharacterColliderRefs` component and dedicated hurtbox objects:

- `HitZone_Head`: `SphereCollider`, trigger enabled, layer `Hit`
- `HitZone_Torso`: `BoxCollider`, trigger enabled, layer `Hit`

Parent each hurtbox below the matching animated bone so it follows the rig.
Keep the shapes tight, non-overlapping, and separate from movement,
navigation, attack, and `CharacterPositionCollider` colliders. Do not use an
enemy's main `CapsuleCollider` as a hurtbox.

In `CharacterColliderRefs`, register the exact collider references in
`hitZones` as `Head` and `Torso`. Do not register arms or legs yet; on a
configured enemy, unmapped actor colliders intentionally allow direct weapon
projectiles to pass through.

Bind
`Assets/Data/Combat/Damage/DefaultEnemyHitZoneDamageProfile.asset` to
`HealthSystem.hitZoneDamageProfile` on the enemy base prefab. Player and ally
prefabs remain unconfigured and use legacy whole-character impacts.

See `Docs/SYSTEMS/HEALTH_AND_HIT_ZONES.md` for source eligibility, fallback
behavior, damage order, and the current authored prefab list.

## Overhead Health And Stagger Bar

Character prefabs use `Assets/UI/CharacterOverheadBars.prefab` through the
`CharacterVisualController.healthBarPrefab` field. The controller mounts the
instance on the configured health-bar bone and binds `CharacterOverheadBarsView`
to the actor's `HealthSystem` and optional `StaggerMeter`.

The upper bar shows current HP in green with missing HP in muted red and displays
the owning character's current level as `Lv N`. The label listens to
`LevelSystem` for companions and `EnemyLevelSystem` for enemies. The lower bar
shows current stagger in yellow with unfilled capacity in muted brown.
Characters without a `StaggerMeter` hide the lower bar. Keep
`StaggerMeter.staggerBarPrefab` unassigned when using the combined overhead bar
so a second stagger bar is not created. The same prefab also contains the
ChainReady prompt and binds it to the actor's `StaggerMeter` automatically.
The local player does not instantiate this overhead bar; player HP/stagger
belongs to the HUD. Enemy and companion overhead bars hide when world geometry
blocks line of sight.

## Inventory Window UI

`Assets/UI/Canvas InventoryWindow.prefab` owns `InventoryUI`, and
`Assets/UI/SlotUI.prefab` owns the reusable slot presentation. Keep the grid at
nine columns and four visible rows unless the inventory-window design itself is
being revised. `InventoryUI` calculates square cell size from the viewport, so
do not author fixed slot sizes on instantiated slot objects.

The prefab authors the complete `InventoryScrollView/Viewport/GridRoot`
hierarchy and vertical scrollbar. Adjust the scroll-view margins, viewport
padding, scrollbar width/spacing, and `InventoryUI.slotSpacing` in Prefab Mode;
these values persist and are used by the responsive runtime calculation.
`InventoryUI` still creates the same hierarchy as a defensive fallback for old
or misconfigured prefabs whose serialized references are missing. The scrollbar
and wheel scrolling should appear only when content exceeds four rows.

`SlotUI.prefab` uses a subdued empty background, a gold hover state, and
rarity-colored borders for weapon instances. Keep its icon image set to
preserve aspect ratio and leave pointer raycasts on the slot root rather than on
the icon, amount label, or hover overlay.

## Equipment Upgrade UI

`Assets/Prefab/User Interface/UIWeaponUpgrade.prefab` is the shared weapon upgrade and equipment
dismantle panel. Its root should include `WeaponUpgradeService`,
`AccessoryDismantleService`, and `AccessoryReforgeService`.

Keep the equipment grid under `WeaponListScrollView/Viewport/WeaponListContent`.
The viewport clips extra rows, while the clamped vertical `ScrollRect` supports
mouse-wheel and background dragging. Its auto-hiding scrollbar uses the dark
track and muted-orange handle authored in the prefab. Opening the panel resets
the list to the top; inventory refreshes while it remains open preserve the
current scroll position. Keep slot drag/drop on the slots themselves.

On `UIWeaponUpgrade`, bind:

- `upgradeService`
- `accessoryDismantleService`
- `accessoryReforgeService`
- `upgradeButton`
- `upgradeButtonLabel` (the `TMP_Text` under `upgradeButton`; falls back to
  `GetComponentInChildren<TMP_Text>` if left unassigned)
- `dismantleButton`
- the inventory list, selected slot, detail text, and drag visual references

The inventory list accepts weapon and accessory instances. Weapon selections
can Upgrade or Dismantle as before. Accessory selections repurpose the same
button as **Reforge** (re-rolls the instance's modifier for Gold, drawn from the
Global Modifier Pool) and allow Dismantle only when `EquipmentAssignmentService`
reports that the instance is not equipped by any character. Inventory slots and
the selected-item slot show the assigned character portrait when the instance is
equipped, matching the portrait marker used by `UIEquipment`.

### Accessory Reforge Settings

The Global Modifier Pool lives at
`Assets/Resources/GameSettings/AccessoryReforgeSettings.asset`
(`AccessoryReforgeSettings`, `Game/Accessories/Reforge Settings` in the Create
menu), loaded via `Resources.Load` the same way `InventorySettings` is. To add a
new modifier: create an `AccessoryModifierDefinition` asset
(`Game/Accessories/Modifier`) under `Assets/Data/Items/AccessoryModifiers/`,
fill in `modifierId`/`displayName`/`statModifiers`/`passives`/`weight`, and
register it in the settings asset's `modifierPool`. `requiredAnyTags` /
`excludedTags` on the modifier can restrict which accessories it can roll on by
matching `AccessoryDefinition.tags`; leave both empty to allow it everywhere.

## Menu Bar Save Slots

`Assets/Prefab/User Interface/MenuBar.prefab` includes `MenuBarSaveSlotUI` on
its root Canvas. The component creates the save-slot panel and confirmation
overlay at runtime without requiring external sprites or font assets. Keep it on
the Canvas root so its confirmation overlay can block the full menu.

The panel provides `Save 1` through `Save 3`, highlights the current slot,
marks empty slots, and disables Reset when the current slot has no data. Slot
switches and resets both require confirmation and reload `Basement` through
`SaveManager` without calling `SceneLoaderSystem.LoadBasement`, because that
scene-loader method saves before changing scenes.

## Skill Hitbox Layout Authoring

Prefab-hitbox skills store collider layout data inside their embedded
`PrefabHitboxSkillPayloadDef`. Hitbox layout assets are not part of the supported
authoring workflow.

To edit a layout using scene colliders, add `SetSkillHitBoxData` to an authoring
object, assign the owning `SkillGemDefinition`, and optionally assign a separate
`Source Hitbox Root`. The selected skill must use a prefab-hitbox execution
payload. Use the component controls to create a template, load the inline layout,
save the edited hierarchy back into the skill, and validate group keys and
shape values.

## Barrier Prefab Authoring

`Assets/Prefab/Combat/BarrierRuntime.prefab` is the project-owned barrier
prefab referenced by `BarrierSkillPayloadDef`.

Required structure:

- **Root** — on the `Barrier` physics layer (index 20), carrying:
  - `SphereCollider` with `isTrigger = true`. Its radius is driven at runtime,
    so the authored value only matters in the Scene view.
  - `BarrierRuntime`, with `barrierCollider` and `presentationRoot` assigned.
- **`Presentation`** child — scaled at runtime to match the barrier diameter,
  carrying:
  - `BarrierVfxPresenter` — hit pulse and end animation. It finds its barrier in
    `Awake` via `GetComponentInParent`; call `Bind(BarrierRuntime)` explicitly if
    you construct the hierarchy yourself (Edit Mode never runs `Awake`).
  - `WorldTimeScaledVfx` — keeps the particles on the world clock so the shield
    slows with the barrier under world-slow.
  - The shield VFX (`Shield_gold.prefab`) nested as a **prefab instance**; the
    third-party asset is never edited in place.

When the barrier ends, the gameplay collider is disabled and the runtime root is
destroyed in the same frame. `BarrierVfxPresenter` reparents itself to the scene
root first so the break or fade can finish, then destroys itself — a broken
barrier plays a short scale-up with emission stopped, an expired or anchor-lost
one collapses instead. Nothing is left orphaned, because the presenter owns its
own teardown and the shield instance is its child.

If the prefab is not on the `Barrier` layer, projectiles will pass straight
through it. The Barrier payload's designer descriptor reports this as a blocking
authoring error.

> `Barrier` must stay at layer index 20 with a collision matrix that allows it to
> collide only with `PlayerBullet` and `EnemyBullet`. See
> [Projectile Barriers](SYSTEMS/WEAPON_SYSTEM.md#projectile-barriers).

## Summon Prefab Max HP

When a summon payload has `overrideMaxHealth` enabled, the resolved max HP
reaches the summon as a **flat `StatType.MaxHP` modifier**. The summon prefab's
own base max HP must therefore be authored to `0`, or it will be added on top of
the intended value.

## Skill Charge HUD Authoring

`UI_Manager.prefab` → `PlayerHUD` carries two widget groups, both bound
automatically by `PlayerUIRuntimeBinder`:

- **`SkillChargeHud`** — one `Slot{n}` child per command slot, each with an
  `ActiveSkillChargePresenter`. Set `commandSlotIndex` to the index into
  `CharacterSkillManager.CommandSlots`. Assign `root` to the child `View` object
  (never the presenter's own GameObject, or it would disable itself), plus
  `skillIcon`, `fallbackLabel`, `chargeLabel`, `cooldownFill`, and `readyFlash`.

  Each `View` holds its children in this render order, back to front:

  | Child | Type | Purpose |
  | --- | --- | --- |
  | `SkillIcon` | `Image` | The slot skill's `SkillGemDefinition.icon`. |
  | `CooldownFill` | `Image` | Dark radial overlay for the recharge in flight. |
  | `ReadyFlash` | `Image` | White pulse when the slot becomes usable again. |
  | `FallbackLabel` | `TMP_Text` | `?`, shown when the skill has no icon. |
  | `ChargeLabel` | `TMP_Text` | Charge count, bottom-right. |

  `CooldownFill` must stay **Filled / Radial 360 / Origin Top / clockwise off**
  and must have a sprite assigned — a `Filled` Image with no sprite silently
  falls back to a plain quad and never sweeps. Counter-clockwise is correct here
  and is not a typo: the overlay holds the time still *owed*, so the wedge it
  clears is the one that sweeps clockwise from 12 o'clock. The presenter drives
  the overlay's alpha (`0.35` while a charge is still usable, `0.65` once the
  pool is empty) and hides the object entirely on a full pool.

  Leave the `View` `CanvasGroup` at alpha `1`. The presenter no longer dims it;
  dimming there would multiply with the overlay's own alpha and make a
  half-recharged slot read as unusable.
- **`SkillCastFeedback`** — a `SkillCastFeedbackPresenter` that shows the
  player-facing line from `CastExecutionFailed` (for example
  `"Cannot deploy here"`). Assign `root`, `messageLabel`, and `fadeGroup`.

To add a new presenter of either kind, drop it anywhere under the player UI root
— the binder finds them with `GetComponentsInChildren`, including inactive ones.

## Morph Skill Authoring

Morph / Awakening skills use `MorphSkillPayloadDef` as the embedded execution
payload on a `SkillGemDefinition`. Choose `AnimationOnly`, `ModelOnly`, or
`Both`, then assign the required animation profile and/or morph model prefab for
that mode. Use the optional controller and avatar fields only when the morph
model should not use the current `CharacterStats` controller/avatar.

Model-changing morphs rebuild the character visual through
`CharacterVisualController`, so the morph prefab must be compatible with the
same mounting conventions as the default model. Keep weapon mount bones named
`Weapon.R` / `Weapon.L` or `hand.r` / `hand.l`, and keep the fire point and
health bar target bone available as `c_traj` unless the prefab's controller
fields are configured differently. Missing mount bones will leave weapons, fire
points, or health bars detached or logged as warnings.

Animation-only morphs keep the current model and avatar. Author the replacement
`CharacterAnimProfileSO` with clips that are compatible with that rig.

To buff the caster while morphed, add entries to `Status Effects (while morphed)`:
each is a `StatusEffectDef` plus stack count, applied when the morph activates and
removed when it reverts. Use stat-modifier status effects to raise attack, defense,
or speed; author them as permanent so the morph controls removal, and give each a
dedicated `effectId` so reverting does not strip identically-identified buffs that
came from other sources.

## Combat Timeline Event Authoring

Animancer-driven combat and hitbox events should use `CombatTimelineEventName`
enum values in gameplay data instead of authoring new `StringAsset` references.
The enum values have explicit numeric ids because Unity serializes enum fields
by number.

Current supported event keys include common hitbox events (`HitStart`, `HitEnd`),
pre-cast block events (`PreCastOpen`, `PreCastClose`), the repeatable skill
presentation event (`Vfx`), the camera shake marker (`ShakeCamera`), the
HitLag feedback marker (`HitLag`), and the carried-object release marker
(`DeliveryRelease`, see **Targeted Delivery Authoring**).

`GlobalTimeScaleManager` is the single owner of `Time.timeScale`. Pause
(`UI_Pause`) and HitLag requests are composed through it — pause always wins,
then the strongest (lowest) active HitLag scale, then `1f`. Direct
`Time.timeScale` writes outside this manager should be avoided.
HitLag requests use an `AnimationCurve` envelope (blend 0–1 over normalized
progress) to shape the freeze. The manager has a serialized
`_defaultHitLagShape` (falls back to `Constant(1.0)` = step behavior).
Per-skill override: set `HitLag Shape` on `SkillGemDefinition`; leave empty
to use the manager default.
`TimeSlowManager.WorldTimeScale` is a separate opt-in axis that does not write
`Time.timeScale`. It also uses an `AnimationCurve` envelope for finite-duration
slows (same blend formula as HitLag). The manager has a serialized
`_defaultSlowShape` (falls back to `Constant(1.0)` = step behavior).
`DashSystem` exposes a per-prefab `perfectDashSlowShape` curve. Infinite-duration
slows (cutscene) use the default curve and hold at full depth until manually
reset.
Prefab hitbox skill payloads are sequential-only: every `HitStart` opens the
next configured step and every `HitEnd` closes the currently active step.
Multi-step skill hitboxes should order their payload steps to match the
`HitStart`/`HitEnd` pairs in the Animancer clip.

`ShakeCamera` markers are supported only in the main skill clip. Place one or
more `ShakeCamera` Animancer events in the clip timeline at the desired shake
points. `SkillGemDefinition` auto-detects these markers and arms the binding;
skills without `ShakeCamera` markers produce no missing-marker warning.
`GameplayCameraController` handles the shake using its own Inspector settings — no per-skill
configuration is needed. Camera shake fires for player, field ally, and
summoned helper skills only; enemy skills are not subscribed.

Timeline event authoring is enum-only. New authoring should select enum values
from the inspector dropdown. Do not add `StringAsset` timeline-event fields back
for gameplay hitbox or pre-cast flow, and do not reorder existing enum values;
append new values with explicit numbers.

## Targeted Delivery Authoring

### Delivery prefab

A delivery object (thrown item, food can, dropped supply) is **presentation only**:

- No `Rigidbody`, no `Collider`. `TargetedDeliverySkillPayloadDef` validation reports either as an
  error. The runtime moves the object itself and it must not collide with, push, or be blocked by
  anything on the way to its target.
- Model, renderers, particles, and a trail are all fine.
- Impact VFX and audio are separate optional references on the payload, not children of the
  delivery prefab.

### Launch anchor

The payload picks where the object sits before release:

| Mode | Resolves to | Fallback |
|---|---|---|
| `CastOrigin` | `ISkillUser.CastOrigin` | caster root |
| `ChildPath` | `casterRoot.Find(path)` | cast origin, with a warning |
| `HumanoidBone` | `Animator.GetBoneTransform(bone)` on a humanoid rig | cast origin, with a warning |

A missing anchor is a warning rather than a dropped cast, because character prefabs in this project
are not uniformly shaped.

### `DeliveryRelease` timeline event

The clip must raise `DeliveryRelease` (see **Combat Timeline Event Authoring**) and it must sit
**after** `castPointNormalized`:

- at the cast point the payload spawns the object and parents it to the launch anchor
- at `DeliveryRelease` the object detaches and starts travelling

Authoring the marker at or before the cast point means the object is thrown before it exists. If
the marker never fires, the runtime cleans up the object and logs an authoring warning once - but
the cooldown is already spent, because the cast committed at its cast point.

### Character-owned helper loadout

`CharacterStats` declares what a character is used as, under **Skill Loadout > Party Role**:

| Party Role | Authors | Hidden |
|---|---|---|
| `Stryker` (default) | `Skill Slots` | `Helper Proc Slots`, `Helper Command Slot` |
| `Helper` | `Helper Proc Slots`, `Helper Command Slot` | `Skill Slots` |

`Skill Points Per Level` applies to both roles - a Helper spends the same pool a Stryker does.

A party slot is a shared rig that any character can be loaded into, so the role belongs to the
character asset, not to a prefab. The inspector hides the half that does not apply, and
`CharacterSkillManager` only reads the Helper half from a character whose role is `Helper` -
leftover data in the wrong section never fires, and `SkillUpgradeTreeValidator.ValidateCharacterLoadout`
warns about it.

Author a character's helper assists on its `CharacterStats` asset:

- **Skill Loadout > Helper Proc Slots** (`List<HelperProcLoadoutSlot>`) - one slot per triggered
  assist. Each slot carries `slotId`, `displayName`, `defaultOptionIndex`, and a list of
  `HelperProcLoadoutOption` variants (`optionId`, `displayName`, `helperProc`).
- **Skill Loadout > Helper Command Slot** (`CharacterSkillLoadoutSlot`) - the manual party command,
  authored exactly like a Stryker command slot. Its options must reference a `SkillGemDefinition`;
  a passive here is a validation error, because the party command has to be castable.

`slotId` and `optionId` are save keys: give every slot and option an explicit, stable, unique id.
The runtime namespaces them as `helper:command:<slotId>` and `helper:proc:<slotId>` so a proc slot
can never collide with the command slot or with a Stryker slot.

A Helper proc's Skill Tree comes from `SkillHelperDef.executionSkill.upgradeTree`; there is no
separate tree field on the proc option. A proc option with no `SkillHelperDef`, or whose proc has
no Execution Skill, is not a configurable variant and is dropped before it reaches runtime.

Both halves are read from the runtime helper's `ctx.baseStats` and from nowhere else. There is no
prefab fallback and no second source, and procs are not collected from the other party members'
skill managers.

**An empty slot means "no skill", not "look elsewhere."** A Helper whose command slot has no
configured option has no manual command, the party command is reported as `SkillUnavailable`, and
the Skill screen does not offer a Command tab at all.

**Only the selected variant is equipped.** Authoring three variants in a proc slot arms exactly
one of them; the other two never reach `AllyHelperProcController`.

Do **not** put helper procs in `skillSlots`. A helper proc is never cast by the player: it fires
from a trigger and is performed by the helper actor. A command slot would give the character a
castable hotkey for something they cannot cast.

Do **not** put either on a prefab. `Ally_Helper.prefab` and every party-slot rig are shared
components that any character is loaded into at runtime, so anything serialized there applies to
whoever happens to occupy that slot.

Helper Chain Attack is unchanged and is still its own system: it reads
`CharacterStats.chainAttackSkill` through `FieldAllyMember`, not the fields above.

`CharacterSkillManager.AppendConfiguredHelperChainDefinitions` returns the selected variant of each
of the loaded Helper's `helperProcSlots`. `AllyHelperProcController` does not collect definitions
from the player or other registered party members.

`ActiveSkillScreen` edits both roles. It shows `Active Skills` (Stryker `skillSlots`) or
`Helper Skills` (the Helper command slot followed by the proc slots in authored order), driven by
`SkillLoadoutDescriptorFactory`.

The persistent charge pool still works with a shared prefab because it is keyed by
`SkillGemDefinition` inside the helper's own orchestrator, and the helper GameObject is only ever
`SetActive`-toggled between summons, never destroyed. Charges recharge on timestamps, so a cooldown
keeps running while the helper is hidden.

## Skill Timeline VFX Authoring

Add `SetAnimationVfxData` to the character or an authoring object in the scene
or Prefab Mode. Assign the target `SkillGemDefinition` as `Source Asset` and use
the `main` entry. `Character Root` and `Source VFX Root` are optional: the
component resolves the nearest character context and uses its own transform as
the source container when they are empty.

For cutscene skills, the timeline window uses one authoring component for both
the main skill clip and the cutscene clip. `Load / Sync VFX Data` creates
separate `VFX_Main` and `VFX_Cutscene` containers under the source root, each
with slots for that clip's own `Vfx` markers. `Save VFX Data` writes entries
from `VFX_Main` back to `SkillVfxEvents` and entries from `VFX_Cutscene` back to
`CutsceneDef.cutsceneVfxEvents`. Toggling between Main and Cutscene VFX changes
the active preview/editing container without deleting the other container.

Use this workflow:

1. Add the repeated `Vfx` events at the required times in the Skill Animation
   VFX Timeline.
2. Press `Load / Sync VFX Data`. The tool replaces existing VFX slots and
   entries, creates one `SkillVfxAuthoringSlot` for each timeline VFX cue, and
   reloads saved entries from the assigned asset.
3. Select a slot, add one or more assets to `VFX Prefabs To Add`, then press
   `Add VFX Prefabs`. The tool creates one `SkillVfxAuthoringEntry` per prefab.
4. Move, rotate, or scale each prefab child in the Scene view, then configure
   its anchor, anchor mode, action, and loop settings on its entry component.
5. Preview from the timeline or with `Play All VFX` / `Stop All VFX`.
6. Press `Save VFX Data` in the timeline window or on `SetAnimationVfxData`
   after every authoring change.

Use `Add Empty VFX Entry` on a slot to author `StopLoop` or an entry whose prefab
will be assigned manually. Use `Add VFX Prefabs` on the slot for prefab-backed
entries; the authoring component no longer exposes Skill-specific new-entry
fields.

`Load / Sync VFX Data` replaces `SkillVfxAuthoringSlot` objects and legacy loose
`SkillVfxAuthoringEntry` objects under `Source VFX Root`, groups entries by cue,
creates visible prefab-instance children, and reconstructs their saved placement.
`Clear Authoring Slots` removes the same authoring hierarchy after confirmation.
Other character children, bones, hitboxes, and authored objects are not removed.
The authoring hierarchy records which Skill Definition owns it. Changing the
assigned Skill immediately rebuilds the hierarchy from the new Skill, so prefab
entries from the previous Skill are not reused. Load/Sync is a replace
operation: unsaved placement or entry changes are discarded in favor of asset
data. Use Unity Undo to restore the hierarchy when this was accidental.

`Play All VFX` and `Stop All VFX` use the authored prefab children as placement
sources and play temporary, non-saved scene instances. Each entry supports one
direct prefab-instance child, which supplies both the prefab asset and visual
placement. Its transform is converted to position and rotation relative to the
selected anchor when saving; its scale is stored as a multiplier relative to the
prefab asset. A slot supports multiple VFX by containing multiple entries.

`Custom Child Path` is relative to the character context root. `Humanoid Bone`
requires a valid Humanoid Animator. For Generic rigs, select `Generic Bone`,
select the required bone Transform in the Hierarchy, and press
`Use Selected Bone`. The stored path is relative to the preview Animator root,
not the character context root. The picker rejects the Animator root and objects
outside its hierarchy. Validation reports unresolved paths or bones.

`Anchor Mode` controls attachment after spawning: `World Space` keeps the VFX
at its sampled spawn pose, while `Follow Anchor` moves it with the selected
anchor. Selecting `Generic Bone` defaults to `Follow Anchor`, but it can be
changed to `World Space`. Generic bone follow tracks position and rotation only;
it does not inherit bone scale or scale its local position offset. Other anchor
types retain their existing parenting behavior. Editor preview uses the same
rules. One-shot preview lifetime uses the same Particle System, Trail Renderer,
safety buffer, Animation Clip, and `Extra Life` calculation as runtime. Prefabs
without a Particle System, Trail Renderer, or Animation Clip use `Extra Life`
as their total lifetime instead of adding it to a fallback. `StartLoop` and
`StopLoop` entries must use the same non-empty loop key.
The editor preview tracks active loops by that key. Scrubbing to a point between
Start and Stop keeps the loop active, while scrubbing before Start or after Stop
removes it. A Stop cue can either clear immediately or stop emission and let
remaining particles finish according to its options.
Multiple `StartLoop` entries in one slot may share a key and are treated as one
group; the matching `StopLoop` stops every VFX in that group. A later Start cue
with the same key replaces the previous group. The authoring and runtime systems
do not force `ParticleSystem.Main.loop`; configure looping on the VFX prefab when
continuous particle emission is required.
When animation playback or its runtime request ends, any loop group still active
is removed immediately. Use an explicit `StopLoop` entry when a graceful particle
tail is required before the animation finishes.
Place the same Animancer event named `Vfx` once for every VFX cue. Occurrence 1
uses cue index 0, occurrence 2 uses cue index 1, and so on. Every entry under the
same slot is saved with that slot's cue index and runs at the same marker.

Open `Tools > RB > Animation VFX > Animation Event VFX Timeline` to preview and
edit Skill or Melee animation data against the character resolved by
`SetAnimationVfxData`. The old Skill timeline menu opens the same window.
Assign the component directly or select it in the Hierarchy and press
`Use Selection`. The window reads the Skill Definition and Animator from that
component; it does not keep a separate preview-character reference.

Use Play, Pause, Stop, Loop, Speed, or drag the red playhead to inspect the
animation in the Scene view. The lower timeline separates animation, cast point,
hitbox, VFX, and other Animancer events. Timeline VFX state is sampled from the
playhead instead of being triggered and left to run on editor time. A OneShot is
simulated to `playhead time - marker time` and disappears after its particles
finish. StartLoop/StopLoop groups are reconstructed from preceding markers,
including graceful emission stop and `Extra Life`. Pause freezes both animation
and particles; scrubbing backward or jumping time restarts simulation at that
position; Stop clears all timeline playback instances.

---

## Cutscene Skill Cutscene Scene Objects

These objects must be present in the gameplay scene for Cutscene Skills to work.

### 1. Unity Layer

Create a layer named **`Cutscene`** in **Edit → Project Settings → Tags and Layers**.

### 2. Main Camera

On the main camera's `Camera` component set **Culling Mask** to exclude the
`Cutscene` layer. `CutsceneSkillPresenter` auto-resolves the `GameplayCameraController` component
from `Camera.main` or from a parent holder such as `CameraHolder.prefab`, then
disables/enables it automatically.

During a cutscene, `CutsceneSkillPresenter` also removes the `WorldUI` layer
from every other camera that renders it, including `WorldUICamera`, and restores
each camera's original culling mask when the cutscene ends. Character overhead
health, stagger, and ChainReady displays therefore remain hidden during the
cutscene even when World UI is rendered by a separate URP camera.

### 3. CutsceneCameraRig (new GameObject in scene)

```
CutsceneCameraRig
├── AnimancerComponent          ← plays cameraCutsceneClip
└── CutsceneCamera (child)
    └── Camera component
        - Clear Flags  : Solid Color  (background = black, hides main scene)
        - Culling Mask : Cutscene layer only
        - Depth        : 1  (renders on top of main camera at depth 0)
        → disabled by default
```

### 4. CutsceneCharacter (new GameObject in scene)

```
CutsceneCharacter               ← Layer = Cutscene (set on this object AND all children)
├── SkinnedMeshRenderer         ← shared mesh/material from the character model
└── AnimancerComponent          ← plays characterCutsceneClip
```

Set the layer to `Cutscene` on every object in this hierarchy so the cutscene
camera can see them and the main camera cannot.

### 5. CutsceneSkillPresenter

Add `CutsceneSkillPresenter` to the **player prefab** (or to a persistent
scene GameObject). On the player prefab it resolves `CharacterSkillManager`
and `CharacterAnimBrain` through the prefab's `CharacteContext` automatically.
If the presenter lives on a persistent scene GameObject, assign `_ctx` to the
player's `CharacteContext`. The camera and cutscene-character fields can be left
empty when the scene objects use the documented names and layer setup below.

Optional serialized overrides:

| Field | Assignment |
|-------|-----------|
| `_ctx` | Player's `CharacteContext` when the presenter is not on the player prefab |
| `_mainFollowCamera` | Main camera's `GameplayCameraController` component when auto-resolve should be overridden |
| `_cutsceneCamera` | `CutsceneCamera` Camera component when auto-resolve should be overridden |
| `_cutsceneCamAnimancer` | `CutsceneCameraRig` AnimancerComponent when auto-resolve should be overridden |
| `_cutsceneCharAnimancer` | `CutsceneCharacter` AnimancerComponent when auto-resolve should be overridden |

`_sortingOrder` (default 31999) controls the letterbox overlay depth relative to
other screen effects. Adjust `_barColor` to change the bar/background colour.

Particle previews use temporary, non-saved instances and remain visible without
selecting authored Hierarchy objects. During Play the window advances cached
ParticleSystems incrementally on its 75 FPS tick. Scrubbing rebuilds from the
requested time, and completed instances are deactivated for reuse. Animation is
sampled before VFX so Follow Anchor and Generic Bone previews use the same frame
pose. Cartoon FX Remaster instances register their package editor callback
directly; no CFXR package modification or component-inspector selection is
required. Closing the window, changing source/entry, entering Play Mode, or
compiling removes the sampled instances and unregisters callbacks.

`Play Visual Preview`, `Refresh Visual Preview`, and `Play All VFX` remain manual
realtime previews driven by editor time. Starting a manual preview stops timeline
sampling, and sampling the timeline stops manual previews. The `Manual Preview`
row reports active manual instances, ParticleSystems, CFXR callbacks, and their
update rate. Both modes enable `Effects` and `Particle Systems` in every Scene
View because Unity may otherwise hide normal particle renderers.
Drag a marker to change its normalized time without triggering crossed cues.
Right-click the timeline at the target time and choose an event from the
VFX, Hitbox, Pre-Cast, or Other submenu. Alternatively, choose an enum value
under `Event At Playhead` and press `Add Event`, or select a marker and press
`Remove Selected`.
Missing Animancer `StringAsset` event-name assets are created under
`Assets/Data/CombatTimelineEvents`. `Vfx` is the only event that may be repeated;
other duplicate enum event keys are rejected.

Each `Vfx` marker defines the start time for its matching cue index. Clicking a
VFX marker selects its first matching authoring entry, moves the playhead to that
marker, and samples the cue at its start. Dragging a VFX marker across another
marker reorders their cue indices. Removing a VFX marker also removes entries in
that cue group and shifts later cue indices down.
Stopping or closing the window stops VFX previews and exits Unity Animation Mode
so the sampled character pose is restored.

## Shared Animation VFX Authoring

Use `SetAnimationVfxData` for source-neutral entries such as a
`MeleeComboSO.Step` or `CharacterAnimProfileSO` Dash/Reload animation. Select
the source and entry in the shared timeline, create or sync slots, place prefab
children, then save the VFX data.
Assigning a `MeleeComboSO` on the component selects the first valid step by
default. The component inspector exposes a Step dropdown and the resolved
animation clip, so authoring does not require typing a step GUID manually.

For a `CharacterAnimProfileSO`, select `Dash Forward`, `Dash Backward`, or
`Reload`. These entries store independent embedded `AnimationVfxTrack` data
beside the existing `dashF`, `dashB`, and `reload` transitions. Missing clips
remain visible in the entry list so validation can report them. Dash Left and
Dash Right are not runtime Timeline VFX entries yet.

Use only `SetAnimationVfxData` for Skill, Melee, and Character Profile
authoring. The `GameSetup` scene's former Skill-specific components were
migrated in place, preserving their MonoBehaviour file IDs, source roots, and
existing slot/entry hierarchy.

The hierarchy owner is `(source asset, entry ID)`. Changing a source asset or
entry, including a Melee step or Character Profile Dash/Reload entry, stops
preview and immediately rebuilds from that selection. Selecting null, an
unsupported source, or an entry without Vfx markers clears only VFX slots and
entries below the source root. Maintain step IDs with
`Tools > RB > Animation VFX > Assign Missing Melee Step IDs`.

## Animation Profile Authoring

Character locomotion profiles use `Locomotion Directional Clips` as the primary
Layer 0 locomotion source. Assign at least `Idle`, `Forward`, `Backward`,
`Left`, and `Right`; diagonal clips are optional, but if any diagonal is used all
four diagonal directions should be assigned. The runtime generates the Animancer
directional mixer from these clips.

`Locomotion Param Lerp`, `Snap To 8 Directions`, `Locomotion Fade Duration`, and
`Locomotion Playback Speed` tune that generated mixer.

## Animation Preview Authoring

`SoloRootMotionPreview` supports a single-clip preview and an `Upper Body`
preview. Use `Upper Body` when checking actions such as shooting while the
character continues to play a locomotion clip.

In `Upper Body` mode:

- `Base Clip` should be the locomotion or walk clip.
- `Upper Body Clip` should be the shoot, aim, reload, or other action clip.
- `Upper Body Mask` should usually match the character profile's
  `upperBodyMask` so the action layer does not replace the legs/root.
- `Upper Body Weight` controls how strongly the action layer blends over the
  base clip.
- `Apply Root Motion` can be enabled when the base locomotion clip should move
  the preview transform while scrubbing or playing.
- For Generic clips that contain movement, set the FBX Animation importer's
  `Root Motion Node` to the rig motion root (commonly `root`). Otherwise the
  skeleton can move away from the GameObject root during preview.

Leaving `Upper Body Mask` empty makes the overlay affect the full body, which is
only appropriate for intentional full-body preview checks.

Generic rigs can use pair-specific rotation offsets when the same upper-body
clip needs different corrections for different locomotion clips. Enable
`Apply Pair Bone Offsets`, then create one profile per `Base Clip` plus
`Upper Body Clip` pair. For example, `WalkForward + Shoot` and
`WalkBackward + Shoot` should be separate profiles when they need different
spine or chest correction.

Pair offset profiles store bone paths relative to the Animator root, so they do
not require Humanoid bone mappings. Select generic rig bones in the hierarchy,
use `Add Selected Bones`, then tune each bone's local Euler rotation offset and
weight. The preview restores the previously applied offsets before each
animation sample, then reapplies the active profile after sampling so offsets do
not accumulate frame by frame.

The custom inspector is organized into `Preview Setup`, `Playback`,
`Pair Offset Library`, `Current Working Profile`, and `Advanced` sections. The
raw local profile list is intentionally hidden in `Advanced`; normal tuning
should use `Current Working Profile` so only the active clip pair is edited.

After tuning pair offset values, use `Capture Pose` to record the current
preview bone pose back into `Local Euler Offset`. Use `Save To SO` or
`Save Current Pair` to commit the current profile and write it into the assigned
`PairOffsetProfilesSO` asset. Save also records prefab instance overrides when
applicable and saves the owning scene or asset.

Use `Load Current Pair` to copy the profile matching the current `Base Clip`
plus `Upper Body Clip` from the assigned asset back into the preview component.
Use `Load All` to merge every asset profile back into the preview component. The
`SO Profiles` browser lists each asset element with `Select` and `Load` buttons;
loading one SO element switches the preview to that profile's `Base Clip` and
`Upper Body Clip`. Loading overwrites matching component profiles but does not
delete component profiles that are not present in the asset.

The current profile's bone editor shows each bone offset with its enabled state,
resolved transform, stored bone path, local Euler offset, and weight. Use
`Add Selected Bones`, `Refresh Paths`, and `Remove Missing` to maintain the
generic-rig bone path list.

Pair offset data that should live outside a preview component can be authored in
a `PairOffsetProfilesSO` asset. Create it from `Game > Characters > Pair Offset
Profiles`. The asset stores a runtime `Base Pose` enum, an `Upper Action` enum,
the preview `Base Clip`, the preview `Upper Body Clip`, profile weight, and
per-bone path, local Euler offset, and weight. Runtime animation lookup uses the
enum pair, not clip names or clip references. The clip references are retained
for preview load/save workflows.

Runtime pair offsets are applied by `CharacterPairOffsetApplier`. Add it to
character prefabs that use pair offsets and bind it on the context when possible.
When a character's anim profile has a `PairOffsetProfilesSO` assigned but the
component is missing, `CharacteContext` can add the applier at runtime. Assign
the `PairOffsetProfilesSO` asset on the character's `CharacterAnimProfileSO`
under `Pair Offsets`. At runtime, `CharacterAnimBrain` reports the active
locomotion `Base Pose`, upper-body action (`ShootPulse`, `ShootHold`, or
`Reload`), and action-layer weight. The applier finds the matching enum-keyed SO
profile and applies bone rotations after animation sampling. The applier uses
the `Animator` assigned on the character's `AnimancerComponent` as the skeleton
root, so it follows the active model instance instead of a parent placeholder
Animator. Runtime lookup checks the enum pair first, then falls back to the
preview clip references mapped through `CharacterAnimProfileSO`, so older
clip-keyed `PairOffsetProfilesSO` entries can still work while they are being
migrated to explicit enum keys. While an upper-body action is held, changing
movement direction changes the active locomotion blend. The applier uses the
current locomotion parameter to blend multiple `Base Pose` profiles in the same
frame, so offsets can follow transitions such as `Forward + ShootHold` blending
into `ForwardRight + ShootHold` or `Right + ShootHold`. Author one profile per
locomotion direction that needs a different correction. Profiles with
`Base Pose = None` or `Upper Action = None` are authoring-only and will not be
used by enum lookup, but may still match through clip-reference fallback. Enable
`Show Debug State` on the applier in Play Mode to see whether the active pair is
missing, the profile is missing, or a stored bone path cannot be resolved under
the active Animator root.

`ShootPulse` is forwarded from weapon shot events only for semi-auto firing
mode. Auto and burst weapons should author held-fire behavior through
`ShootHold`/hold loop clips instead of relying on pulse restarts per projectile.
When the action layer is inactive, the first upper-body action plays its state
immediately and fades the action layer in over locomotion. Cross-fades inside
the action layer are reserved for transitions from an already visible action
pose, such as `ShootPulse` into `ShootHold` or reload.

`CharacterAnimProfileSO.reloadBodyMode` also controls movement during reload.
`UpperBody` keeps locomotion available and continues the reload animation,
gameplay routine, and Reload VFX session while Dash plays on the locomotion
layer. `FullBody` blocks normal movement; a successful Dash cancels the reload
before the Dash animation takes ownership. A failed Dash attempt does not
cancel either reload mode.

## Context-Owned References

If the reference is a peer module shared by character systems, prefer resolving
it through context:

- `ctx.StatsHub`
- `ctx.HealthSystem`
- `ctx.WeaponSystem`
- `ctx.CombatEventBus`
- `ctx.SkillManager`
- `ctx.PassiveController`
- `ctx.AccessoryLoadout`
- `ctx.Equipment`

Avoid adding duplicate serialized peer-component fields across modules when the
context can already resolve the component.

`FieldAllyMember` only needs its actor context plus chain-specific authoring
references. Do not bind `HealthSystem`, `AITargetInfo`, `Rigidbody`,
`CharacterController`, `CharacterAnimBrain`, `NavMeshAgent`, `BehaviorTree`, or
`AIAimTargetDriver` directly on the component. Shared modules are resolved
through `CharacteContext`; ally-only modules are resolved through `AllyContext`.

## Player Prefab Expectations

Player prefabs commonly need:

- `PlayerContext`
- movement/input system
- `PlayerInventory`
- `PlayerUIContext`
- party command controller
- ally helper manager
- field ally manager
- chain attack coordinator
- weapon system
- stats, health, stamina, dash, passive, and skill systems

Player-only systems may depend on `PlayerContext`.

Do not author inventory capacity on player or scene prefabs. The shared value is
configured in `Assets\Resources\GameSettings\InventorySettings.asset`; prefab
instances only own inventory runtime data and component references.

## Ally Prefab Expectations

Ally prefabs commonly need:

- `AllyContext`
- `AITargetSensor`
- `NavMeshAgent`
- `AgentMoveDriver`
- weapon or skill systems if the ally can attack
- stats, health, passive, animation, and state systems

When multiple allies share one runtime prefab, assign each character's
Behavior Designer graph through `CharacterStats > AI > Behavior Subtree`.
`CharacterContextPartyLoader` applies the selected Subtree to that ally's
`BehaviorTree` instance before party runtime variable binding. Leave the field
empty only when the prefab-authored behavior should remain as the fallback.

Ally-only systems may depend on `AllyContext` when they need ally-specific
fields such as `AITargetSensor`, `NavMeshAgent`, or `AgentMoveDriver`.

### Taunt authoring (no prefab field)

`AITargetSensor` no longer carries a taunt `StatusEffectDef`. Nothing about taunt
is authored on the prefab: the taunt status is authored on the skill, at
`TauntSkillPayloadDef.tauntStatus`, and `TauntSkillRuntime` applies it to each
target before notifying the sensor. The sensor derives its taunt state from any
active status instance whose Def carries the `Taunt` tag.

The `StatusEffectDef` used as a taunt status must be authored as:

- `tags` contains `Taunt` (`StatusEffectTags.Taunt`) — without it the sensor
  never sees the taunt
- `separatePerSource` **on** — each taunter needs its own instance so expiry can
  fall back to the previous taunter
- `stackMode` = `RefreshDuration` — re-taunting from the same source refreshes
  instead of stacking

`TauntSkillPayloadDef.CollectValidationIssues` reports an error for each of these,
so `SkillPayloadValidationTool` catches a mis-authored Taunt Def before play.

Prefabs that receive taunt still need a `StatusEffectController` (see below) —
that is now the only taunt-related prefab requirement.

`Ally.prefab` has an authored `StatusEffectController` on its root object
(added so status-effect payloads — e.g. `Aires_Skill_3`'s ally-heal branch —
can apply buffs to allies; previously `AITargetSensor` lazily `AddComponent`d
one at runtime when needed, matching the auto-provision pattern already used by
`CharacteContext.ResolveReferences()` for components like `AccessoryLoadout`
and `ThirdPersonAimRigController`). Any other ally-type prefab that needs to
receive status effects (buffs, heals, debuffs) — including taunt — must carry its
own `StatusEffectController` the same way.

## Root Motion Trajectory Authoring

No additional prefab, skill asset, or FBX importer fields are required for
player and party chain-attack root motion placement or Guaranteed Interruption
ally placement. The attack `AnimationClip` remains the source of truth: Unity
must already extract the clip's root motion so `Animator.deltaPosition` and
`Animator.deltaRotation` produce the intended movement during playback.

Targeted Helper delivery skills are the exception because their payload owns a
gameplay stand-off. Author **Targeted Delivery > Caster Placement > Target
Stand-Off At Cast Point** on `TargetedDeliverySkillPayloadDef`; this is the
horizontal Helper-to-target distance at the skill cast point. The runtime
derives the Animation start pose from the sampled root motion and never scales
the clip to fit this value. `AllyHelperManager.minSummonRadius` on the Player
prefab is only for the generic fallback ring around the Player and must not be
used to tune a targeted skill. Milano Skill 3 currently authors this payload
value as `1.2m`.

`SkillGemDefinition > Presentation > Ignore Character Collision During Root
Motion` defaults to enabled. Keep it enabled for jumping, lunging, and other
clips with Y root motion so Player, Enemy, and Ally bodies cannot become ground
or steps. This setting does not disable world collision. Disable it only for a
skill whose root-motion movement is intentionally blocked by character bodies.
No prefab reference is required; the runtime root-motion driver resolves the
character layers and restores the previous collision state when playback ends
or is interrupted.

Ally prefabs using `RootMotionNavMeshDriver` must leave **Zero Y** disabled.
Per-request `RootMotionPlanarOnly` is responsible for suppressing Y on targeted
placement flows such as Guaranteed Interruption. Enabling **Zero Y** on the
prefab suppresses vertical root motion for every Ally skill and is only
appropriate for actors that must remain permanently planar.

Do not assign a motion-bone name, add a trajectory component, bake an asset, or
change the clip to use `c_traj`/`root` for this flow. Runtime code creates and
destroys hidden sampling objects, caches trajectories per clip and Avatar, and
adds the appropriate root-motion driver to the active Animator model. Existing
teleport-profile collision masks, probe collider, and anchor offset are reused.
The serialized NavMesh flag remains compatible with older assets, but Player,
Ally, and Helper placement consumers always require a valid NavMesh footprint.

The chain attack probe collider must continue to represent the actor footprint.
Guaranteed Interruption uses the ally context's character-position collider in
the same way. The current prefab reference is used for start, impact,
full-trajectory, target-overlap, sweep, and NavMesh-footprint validation. A clip
with no extracted XZ displacement or yaw uses the existing teleport behavior.
The central placement boundary preserves this contract and scores the sampled
trajectory without warping its root-motion distance. If all sampled candidates
are obstructed, it selects the candidate with the least measured penetration;
rotation-only animation segments are swept as well, including segments that
translate and rotate together. `CharacterPlacementFootprintUtility` converts
Box, Capsule, and Sphere position colliders for Chain, Targeted Skill, Helper,
and Summon requests; callers use its conservative fallback box when a supported
position collider is unavailable. Summon placement uses a transient registry
reservation while the spawn batch commits, then syncs Physics and releases the
handles; the live summon collider is authoritative for the rest of the summon
lifetime. If a character-position
collider is not available during compatibility migration, the existing profile
clearance box is used as a conservative fallback; authoring a real
`CharacterPositionCollider` is still preferred. Helper chain teleport uses the
same boundary for its side-effect-free placement path, while the legacy
pose-validator callback remains available for helper code that must temporarily
apply a candidate pose.

## Projectile Movement And Lifetime Ownership

A gameplay projectile prefab has **exactly one** component that owns its movement, its lifetime,
and its root activation, and that component is `Projectile`. Nothing else on the root may:

- write the root `Rigidbody` (velocity, constraints, `detectCollisions`, `isKinematic`),
- run its own lifetime timer or coroutine,
- call `SetActive` on the root,
- `Destroy` the instance or return it to `ProjectilePool`.

Two owners on one Rigidbody produce bugs that look like tuning problems: the projectile travels at
the wrong speed, tumbles because a `FreezeRotation` constraint was cleared, or disappears on a timer
that has nothing to do with `ProjectileConfig.lifeTime` while never returning to the pool.

**Vendor movers** — `HS_ProjectileMover` (Hovl Studio) is a second owner and must not sit on a
gameplay projectile root. The vendor script is left untouched on purpose: dozens of demo prefabs
under `Assets/VFX/` rely on it, and editing it would change those. Remove the component from the
gameplay prefab instead.

**Presentation instead** — use `ProjectilePresentationResetter` for particle and light handling
across pool reuse. It restarts the assigned `ParticleSystem`s and restores the assigned `Light`s on
enable, clears them on disable, and does nothing else. Leave both arrays empty to auto-collect every
particle system and light in the hierarchy, or assign them explicitly when the prefab has child VFX
instances that should keep their own behavior (`Projectile 1 bullet Ally` assigns only its trail
particle and its root light, so the authored `Flash`/`Hit` child instances are untouched).

**Validation** — `ProjectileAuthoringValidator` enforces this. Run **Tools > Validation >
Projectile Authoring Report**, or rely on `ProjectileAuthoringValidationTests`. The rule is
reflective, not a blocklist: a root component trips it when it declares its own `FixedUpdate` or
holds a serialized reference to the root's `Rigidbody`/`Collider`. Vendor and demo folders are
excluded from the sweep.

## Enemy Prefab Expectations

Enemy prefabs commonly need:

- `EnemyContext`
- enemy `NavMeshAgent`
- `AgentMoveDriver`
- enemy collider
- `EnemyDropper`
- animator or animation brain/driver
- stats, health, passive, weapon/skill/melee, and state systems as needed

Enemy-only systems may depend on `EnemyContext` when they need enemy-specific
fields.

### ChainAttack Test Target

Use `ChainAttackTestTarget` for a standalone ChainAttack and ChainReady target
without a full enemy prefab:

1. Add `ChainAttackTestTarget` to a GameObject. Unity also adds the required
   `BoxCollider`, kinematic `Rigidbody`, and `StaggerMeter`.
2. Set the inherited `AITargetInfo` Target Identity to `Enemy`.
3. Optionally assign inherited Aim Point and Chain Attack Point transforms.
   When unassigned, the component transform is used.
4. Assign `Assets/UI/CharacterOverheadBars.prefab` to Overhead Bars Prefab.
   The target binds its test HP, stagger meter, and ChainReady prompt to the
   shared UI. Adjust Overhead Bars Offset when the model height differs.
5. In Play Mode, damage the target with stagger payloads or use
   `Force ChainReady`, aim at it, and press **F**.
6. Use `Reset Target` to restore health, targetability, and stagger state.

The target cannot receive lethal damage while ChainReady or a chain execution
is active by default. Disable `preventDeathDuringChainReady` only when testing
chain cancellation caused by target death.

### ChainReady Prompt

`Assets/UI/CharacterOverheadBars.prefab` includes `ChainReadyPromptView` and a
world-space `TMP_Text` prompt. `CharacterOverheadBarsView.Bind` passes the
actor's `StaggerMeter` to it, so enemy variants using the shared overhead prefab
need no additional prompt component. The prompt shows `[F] CHAIN` with a
countdown when the enemy enters ChainReady and hides on exit.

### ChainReady Animation Clip

On the enemy's `CharacterAnimProfileSO` asset, optionally assign a
`chainReady` clip under the StatusEffect section. If no clip is assigned the
enemy freezes in its current locomotion pose during ChainReady with no error.

### Chain Ready Duration

`StaggerProfileSO` has a `chainReadyDuration` field (default 3 s). The
`StaggerMeter` component also has a serialized fallback value. The profile
takes priority when assigned.

### ChainReady Intro Cutscene

The intro cutscene is split across two assets:

**`SkillChainDef`** (per chain):

| Field | Purpose |
|---|---|
| `enableChainReadyIntroCutscene` | Opt-in toggle (default OFF). Existing assets keep current behavior. |

**`CharacterStats`** (per playable character, under **Chain Attack**):

| Field | Purpose |
|---|---|
| `introChainCutscene` | Reference to a **`CutsceneDefSO`** asset (create via *Assets ▸ Create ▸ Game ▸ Cutscene ▸ Cutscene Def*). The SO holds a `CutsceneDef`: assign `characterCutsceneClip` (the character's intro animation), optionally `cameraCutsceneClip`, `worldSlowScale`, fade durations, and `barThickness`. Do not edit `cutsceneVfxEvents` by hand — author it with the **Animation VFX** tool (below). |

**Cutscene VFX authoring:** the intro's VFX uses the same `SetAnimationVfxData`
workflow as skill cutscenes. Drop the `CutsceneDefSO` asset into the **Source
Asset** field, select the **Cutscene VFX** entry, add `Vfx` markers to
`characterCutsceneClip`, then place and **Save VFX Data** — it writes to the
SO's `cutscene.cutsceneVfxEvents`.
The editor preview samples both `characterCutsceneClip` and
`cameraCutsceneClip` when the source is a standalone `CutsceneDefSO`.
Adding the first `Vfx` timeline marker creates its authoring slot immediately,
including when the cutscene previously had no markers.

When both the `SkillChainDef` toggle is on **and** the active character's
`CharacterStats.introChainCutscene` references a `CutsceneDefSO` with a valid clip, pressing F on a
ChainReady target plays a one-shot cinematic intro before the chain's first
step. The intro uses the same stage runtime as skill cutscenes
(`CutsceneSkillPresenter` + `CutsceneDirector`). If the clip is unassigned,
the toggle is off, or the cinematic stage is busy, the chain starts immediately
with no error.

The character clip plays on the **chain locomotion channel** via
`CharacterAnimBrain.TryPlayChainCutscene` — it is presentation-only and never
spawns damage or a skill payload. The chain channel binds the clip's `Vfx`
markers and forwards their cue indices to the active `CutsceneSkillPresenter`
session; a marker at normalized time `0` fires immediately when playback starts.
Each playable character authors their own clip on their
`CharacterStats` asset; shipped assets should leave the clip unassigned until
authored.

## Weapon Authoring

Weapon data should preserve:

- projectile prefab
- firing mode
- fire interval
- reload mode and timing
- ammo limits
- crit, stability, bullet speed, stagger, and cue data
- affix and upgrade compatibility

Author weapon `Stability` as a percentage from `0` to `100`. `0%` keeps full
sway and `100%` removes sway. For passives, accessories, affixes, status
effects, and upgrades, a `Flat` Stability modifier is authored as percentage
points: a value of `10` changes `30%` Stability to `40%`.

`WeaponSystem` is responsible for copying relevant weapon data into public
runtime mirrors and refreshing derived stats.
Runtime code should prefer `WeaponSystem` facade properties and methods over
direct public mirror field access. Use `CurrentAmmo`, `IsMagazineEmpty`,
`IsAiming`, `CurrentFiringMode`, `IsFiringActivity`, `Damage`, `FirePoint`, and
`BindFirePoint` where applicable. Keep mirror fields valid for inspector/debug
compatibility until that compatibility is intentionally retired.

## Passive Authoring

Passive authoring should keep these fields stable:

- passive id/runtime id
- rule id
- trigger event
- origin filter
- cooldown/counter settings
- source id when matching a specific source
- action source ids
- optional event source kind and values

Optional event source rules should define a source id explicitly when designers
need multiple rules to share or distinguish the same source.

## Removed Serialized Fields (Responsibility Reduction)

The following serialized fields were removed from `CharacterAnimBrain`:

- `deactivateOwnerOnSkillExit` — actor lifecycle no longer in Brain; only
  `Player.prefab` had it enabled, but the trigger condition (null skill
  definition) never fired in normal gameplay.
- `statusEffectController` — status policy moved to `CharacterAnimDriver`;
  `StatusEffectController` is now resolved through `CharacteContext.StatusEffects`.

Re-save Player, Ally, and Enemy prefabs after opening Unity to clear stale
serialized data. The prefab YAML values are harmless but will be removed on
next save.

## Status Effect Locomotion Pose

`StatusEffectDef` has a `locomotionPose` field (enum `StatusLocomotionPose`)
under the Gameplay header. It controls which locomotion animation plays when the
status effect is active:

| Value | Behavior |
|-------|----------|
| `Auto` (default) | Derives the pose from structured flags: `pushStunnedState` → Stun, `controlBlocks.Move` → Root. MiniStun and Freeze require explicit assignment. |
| `None` | No locomotion override. |
| `Root` | Rooted in place; aim and rotate still allowed. |
| `MiniStun` | Brief stun animation. |
| `Stun` | Full stun animation. |
| `Freeze` | Frozen animation (highest priority). |
| `ChainReady` | ChainReady pose (plays during the ChainReady window before stagger stun). |

Priority order: Freeze > ChainReady > Stun > MiniStun > Root.

For existing assets that relied on string-token matching (effectId/name/tags),
run `Tools > Status > Migrate Locomotion Pose` once after upgrading. The
migration tool scans all `StatusEffectDef` assets and sets `locomotionPose`
based on the old string heuristic. Assets left as `Auto` use the structured
fallback automatically.

## Basement Character Info Cards

`Assets/Prefab/User Interface/CharactorInfo.prefab` uses `CharacterInfoView` to
display the `PartySlot.Selected` character assigned to each card in the
Basement scene. The view resolves its `PartySlot` from the existing
`FollowWorldToScreenUI.target`, refreshes when the slot selection changes, and
hides the card content while the slot has no selected character.

The bound fields are the character name, `CharacterWeaponType`, combat-role
text, and combat-role icon. `SMG` and `HMG` are formatted as uppercase
abbreviations. Rank remains prefab-authored and is not currently read from
character data.

Combat-role sprites are configured through the `Role Icons` list on
`CharacterInfoView`; change that list when role art or role-to-icon grouping
changes. Keep all six `CharacterCombatRole` entries covered. The Basement
instances must continue to point `FollowWorldToScreenUI.target` at the same
`PartySlot` passed by the `Edid` button to `UILoadLaval.BindSlot`. Each button
also keeps its existing `GameObject.SetActive(true)` call for opening the edit
panel.

`CanvaCharactorInfo` is a HUD layer and must keep sorting order `0`, below the
main `UI` Canvas at order `1`. This lets Shop, Upgrade, Inventory, and future
modal screens cover the cards without adding per-screen hide/show events.
Decorative images and text inside `CharactorInfo.prefab` must keep `Raycast
Target` disabled; only the `Edid` button's target image should receive raycasts.

## Basement Status Panel

`Assets/Prefab/User Interface/UpgradUI.prefab` owns `UILoadLaval`, the character
Status page. It shows **final combined stats** — character base plus level
scaling, equipped weapon, weapon upgrade level, static weapon affixes, equipped
accessories including their reforge modifier, and always-on passives — matching
what `StatsHub` produces in combat. See
`Docs/ARCHITECTURE/CHARACTER_STAT_FORMULA.md` for the shared formula and for the
sources that are deliberately excluded.

`UILoadLaval` has an optional `Affix Database` field. Assign the project's
`WeaponAffixDatabase` asset when the Basement should resolve static weapon affix
bonuses without relying on the database happening to be loaded; when the field is
empty the panel falls back to `WeaponAffixDatabase.GetLoadedAffixById`. Leaving it
unassigned only risks under-reporting affix bonuses, never an error.

Displayed numbers are single values with no breakdown. Keep the existing text
formatting: most stats use `0`, CritRate uses `0.#` plus `%` and is stored as
`0..100`, CritDamage uses `x` plus `0.##` and is stored as a multiplier. Do not
rename `UILoadLaval` or its serialized fields, including the `Enagy` spelling —
the prefab binds them by name.

The panel recalculates immediately when equipment changes. `UIEquipment` raises
`EquipmentChanged` after a successful equip or unequip, and `UILoadLaval`
subscribes to the weapon and accessory `UIEquipment` instances it resolves. Keep
those two `UIEquipment` components as separate instances; the resolver rejects a
shared one.

## Basement Character Shop

`Assets/Prefab/User Interface/Shop/ShopPanel_Starter.prefab` contains
`ShopPanelUI` and `CharacterShopPageUI`. The Basement scene uses this prefab
under the `UIShop` Canvas. At runtime the panel adds persistent `ITEMS` and
`CHARACTERS` tabs, builds the character rows from
`CharacterDatabase.characters`, and keeps the existing item-shop page intact.

To add a playable character to the shop:

1. Add its `CharacterStats` asset to `CharacterDatabase.characters`.
2. Add a matching `CharacterDatabase.unlockEntries` entry.
3. Set `unlockedByDefault`, `goldCost`, and the optional locked message on that
   entry.

The row uses `CharacterStats.icon` and `characterName`, so no character-row
prefab edit is required. Characters without a matching unlock entry still
appear, but they remain locked with `NOT FOR SALE` and emit an authoring warning.
Purchases ask for confirmation, spend gold from the bound `PlayerInventory`,
and save both the unlock and inventory balance to the active save slot.

`Tools > Shop > Create Starter UI Prefabs` preserves this setup when rebuilding
the starter shop prefabs. Keep the `CharacterDatabase` reference on
`CharacterShopPageUI` and the `characterShopPage` reference on `ShopPanelUI`
when creating prefab variants.

## Player Skill Input — Prefab / Input Asset Wiring

Player skill casts (slots 1/2/3) come from the New Input System, not from a
keyboard poll on `CharacterSkillManager` (that component also lives on ally,
enemy, and summon prefabs, so it never reads keys directly — see
`Docs/SYSTEMS/SKILL_SYSTEM.md` § Player Skill Input).

`RB_Project/Assets/Input/Inputmaneger.inputactions`, map `Player`:

| Action | Binding |
|---|---|
| `SkillSlot1` | `<Keyboard>/1` |
| `SkillSlot2` | `<Keyboard>/2` |
| `SkillSlot3` | `<Keyboard>/3` |
| `CallHalper` | `<Keyboard>/tab` (moved off `1` to make room for `SkillSlot1`) |

`Player.prefab`, `PlayerInput` component Events → Player:

| Action | Object | Function |
|---|---|---|
| `SkillSlot1` | `PlayerInputHandler` | `OnSkillSlot1` |
| `SkillSlot2` | `PlayerInputHandler` | `OnSkillSlot2` |
| `SkillSlot3` | `PlayerInputHandler` | `OnSkillSlot3` |

`m_CallState` must be `2` (RuntimeOnly), matching every other entry in the
list. Edit action bindings through the Input Actions editor in Unity, not by
hand-editing the `.inputactions` JSON or the prefab YAML — action ids are
GUIDs that the prefab's `PlayerInput.m_ActionEvents` reference by id, and the
cached `m_ActionName` label shown per entry can go stale (it does not affect
behavior; only the actual GUID binding does — do not trust the label when
reviewing).

`CharacterStats.skillSlots[].hotkey` (the legacy per-character hotkey field on
`ChaDef.*` assets) is no longer read by any runtime code. It exists only as
old data; leave it at `None` on new slots.

## Before Removing A Serialized Field

Check all of these first:

1. Is the field referenced by scenes or prefabs?
2. Is the field saved in player data or migration data?
3. Can runtime code already resolve the same reference through context?
4. Is the field local authoring data rather than a peer module?
5. Will removing it break inspector workflows or old prefab variants?

If any answer is unclear, keep the field and migrate deliberately in a separate
task.

## Guaranteed Interruption Command — Prefab Checklist

### Player Prefab

- Add `InterruptionCommandController` component.
- Set `PlayerContext.interruptionCommand` to the new component (or let
  `ResolveReferences` find it automatically).
- In the Input Asset, add an `InterruptionCommand` action (Button) with
  binding `<Keyboard>/g`. Wire the `PlayerInput` event to
  `PlayerInputHandler.OnInterruptionCommand`.
- Configure `targetSearchMask` and `targetSearchRadius` on the controller.
- Add `PlayerInterruptionController` component.
- Assign `interruptionSkill`: a `CharacterSkillEntry` whose animation clip has
  a `HitStart` timeline event (same requirement as the ally skill).
- Assign `teleportProfile`: a `ChainAttackTeleportProfileDef` asset (e.g.
  `Example.ChainAttack.Teleport.LeftFlank`).
- Set knockback settings (`knockbackDistance`, `knockbackDuration`,
  `knockbackReaction`, `knockbackProgressCurve`) to match the ally values.
- Set `impactTimeoutSeconds` to match the ally value.
- Bind `playerInterruptionController` on `InterruptionCommandController`
  (or let `Awake` resolve it from the same object, parent, or children).
- Set `playerInterruptRange` on `InterruptionCommandController` (default 4 m).
- Set `noWarpStartDistance` on `InterruptionCommandController` (default `0.5`
  m, `Min(0)`). When the executing Player or Ally is already within this XZ
  distance of the resolved start pose, it snaps rotation only (no position
  jump, no hide/fade). `0` disables no-warp and always uses the warp flow.
- Set `noWarpTargetDistance` on `InterruptionCommandController` (default
  `1.5` m, `Min(0)`). Fallback tier used when the actor is not close enough
  to the ideal start pose (above) but is already within this XZ distance of
  the target anchor: the actor attacks in place (root motion disabled, no
  warp) instead of teleporting back to the ideal start. `0` disables this
  fallback.
- Keep `logInterruptionFlow` disabled for normal play. Enable it on the test
  scene instance when diagnosing command target/ally/player selection.

### Ally Prefab (participating allies)

- Requires: `AllyContext`, `FieldAllyMember`, `CharacterSkillManager`,
  `CharacterAnimBrain`, `AIAimTargetDriver`, `NavMeshAgent`, `BehaviorTree`.
- Bind `BehaviorTree` and `AIAimTargetDriver` on `AllyContext`, or allow
  `AllyContext.ResolveReferences()` to resolve them from the actor hierarchy.
- Add `AllyInterruptionController` component.
- Assign `interruptionSkill`: a `CharacterSkillEntry` whose animation clip has
  a `HitStart` timeline event. The interruption runtime explicitly binds this
  event, so the skill may use a payload such as `ProjectileSkillPayloadDef`
  that does not otherwise require timeline events.
- Assign `teleportProfile`: a `ChainAttackTeleportProfileDef` asset.
- Set `motionBoneName` to the animation motion bone used for end-of-skill root
  alignment (`c_traj` by default). The bone must resolve below the active
  Animator, and `CharacterVisualController.ModelRoot` must be a child of the
  ally context root. This field is only for the visible root rebase after the
  interruption skill ends; safe-pose placement samples Unity-extracted root
  motion from the skill clip and does not read this bone.
- `rebaseSettleTimeoutSeconds` is the maximum visible compensation period while
  the Animator blends back to locomotion. The default `0.75` seconds is a hard
  recovery timeout; normal completion occurs earlier when the motion-bone pose
  is stable.
- `ASPHelperDitherFader` is optional. When present through
  `FieldAllyMember.ActorFaderRef`, the ally is hidden before the snap and
  fades in with the interruption animation. The fader is not used at the end of
  the skill; visible root compensation handles the return to locomotion, so the
  end-of-skill rebase also works when no fader is configured.
- Configure knockback settings: `knockbackDistance` > 0, `knockbackDuration` > 0.
- Allies without a configured `AllyInterruptionController` or missing
  skill/profile are simply never selected for interruption.
- Keep `logInterruptionFlow` disabled for normal play. Enable it on the test
  scene instance to trace visual hide, snap, fade-in, skill, impact, fallback,
  and cleanup.

### Enemy / Target Prefab

- Requires: `CharacterSkillManager`, `PreCastBlockController`,
  `CharacterKnockbackMotor`.
- Keep one `PreCastBlockController` per enemy actor. Bind `StaggerMeter` on
  `EnemyContext`, or allow `EnemyContext.ResolveReferences()` to resolve it.
- The blockable skill must have `BlockablePreCast` enabled on its
  `SkillGemDefinition` and correct `PreCastOpen` / `PreCastClose` timeline
  events.
- The skill's cast point must be gated by the cast-moment Animancer event
  (the default pending-cast path). The Pre-Cast Hold mechanism freezes the
  playhead before this event fires.
- Keep `logPreCastFlow` disabled for normal play. Enable it on the test scene
  instance to trace the cast window and block reservation lifecycle.

## Persistent Audio System

Keep `AudioService` on the `AudioSystem` child of
`Assets/Prefab/System/GamePlaySystem.prefab`. The service persists the prefab's
root GameObject so its pooled sources and active music survive scene changes.
Do not place that prefab root beneath a scene-owned parent that is expected to
be destroyed during a transition.

Set `AudioCue.priority` using Unity's convention: lower numbers are more
important. Music and looping cues are protected from pool replacement; use
per-cue `cooldown` and `maxInstances` to control high-frequency one-shot cues.
See `Docs/SYSTEMS/AUDIO_SYSTEM.md` for the complete replacement policy.

## Active Skill Screen

Generate the placeholder uGUI/TMP assets with **Tools > RB > Skills > Build
Active Skill Placeholder Prefabs**. The generated assets are:

- `Assets/Prefab/User Interface/Active Skill/ActiveSkillScreen.prefab`
- pooled Slot Tab, Variant Card, Upgrade Node, and Tree Connection prefabs in
  the same folder
- `Assets/UI/Active Skill/SkillScreenTheme.asset`

The screen uses a 1920 x 1080 Canvas reference with `Scale With Screen Size`
and a 0.5 width/height match. The runtime tree opens fitted to the complete
graph. Scroll the mouse wheel anywhere in the tree viewport to zoom toward the
pointer, from the current fit scale up to an absolute scale of `2.0`. Drag the
empty viewport background with the left mouse button to pan; drags that start on
a node or button remain UI interactions. Pan is hard-clamped to the scaled graph
bounds plus fit padding, and an axis remains centered when its graph bounds are
smaller than the viewport.

`FIT TREE` resets only the current pan and zoom. Hover it to show the mouse
controls. Selecting or unlocking a node preserves the current view, while
opening the screen or changing the selected slot/variant fits the new tree.
Viewport-size changes recompute the fit limit and clamp the preserved view.
The tree canvas intentionally does not use `ScrollRect`. Check the screen
manually at 1280 x 720, 1920 x 1080, and 2560 x 1440. Input is mouse-only.

Author node positions in the Active Skill Tree Editor as they should appear on
screen. `uiPosition` stores the center of each node. The editor uses GraphView
coordinates with positive Y pointing down; the runtime tree converts them to
centered uGUI coordinates with positive Y pointing up. Do not invert node Y
values manually in the tree asset.

Set a node's **Visual Scale** between `1.0` and `2.0` to enlarge it from the
normal 96 x 96 runtime size up to 192 x 192. Scale affects presentation only;
cost, prerequisites, effects, and saved progress do not change. Runtime auto-fit
includes the scaled node bounds. Tree validation warns when node bounds overlap.

Bind a gameplay character with `ActiveSkillScreenController.BindRuntime(ctx)`;
opening the screen enters the Pause UI state and Back restores the previous
state. Bind a lobby character definition with `BindLobby(characterStats)`;
the lobby path reads and writes the same character progress repository without
changing time state. The screen deliberately contains no character roster.
`PlayerUIContext.activeSkillScreen` can hold the scene/prefab reference used by
the caller.

Author the normal Tree on the `SkillGemDefinition` asset under **Active Skill
Tree > Upgrade Tree**. A `CharacterSkillLoadoutOption` automatically uses that
Tree. Set **Upgrade Tree Override** on the Variant only when the same Skill Asset
needs a different graph in that particular loadout option. Leaving both fields
empty produces the screen's `No Active Skill Tree assigned to this variant.`
state. Existing Variant Tree references from the earlier schema migrate to the
override field through `FormerlySerializedAs`.

For participating Player and Companion prefabs, keep one
`CharacterActiveSkillProgress` on the context root. `CharacteContext` resolves
it as the common `ActiveSkillProgress` reference and can add the component at
runtime for backward-compatible prefabs. New or updated prefabs should bind it
explicitly so the dependency is visible to authors.

### Replacing Placeholder Art

Assign frames, backgrounds, state sprites, and colors on `SkillScreenTheme`.
Skill Variant icons continue to come from their `SkillGemDefinition`; node icons
come from `SkillUpgradeNodeData`. Art replacement therefore does not require
code or prefab hierarchy changes. Keep the controller/view component references
intact when restyling the generated prefabs. The builder preserves an existing
theme asset but rebuilds the placeholder prefabs, so do not rerun it after
making manual prefab-only visual edits unless those edits are intentionally
replaceable.

Assign **Important Node Frame** to style nodes whose Visual Scale is above
`1.0`. If it is empty, those nodes fall back to the normal **Node Frame**.

`UpgradUI.prefab` wires `Skill_Tree_Button` to
`UILoadLaval.OpenActiveSkillTree`. The controller instantiates its assigned
`ActiveSkillScreen` prefab once as a standalone overlay Canvas and binds the
currently selected `PartySlot.Selected` character through the lobby session
before opening it. Keep the screen prefab root scale at one and its Canvas
sorting order above the lobby UI; the prefab builder enforces both values.
# Weapon affix behavior assets

Every registered `WeaponAffixDefinition` must reference one root behavior. Use
**Tools > Weapons > Affixes > Migrate And Generate**, then run **Validate (Dry
Run)**. Missing behaviors, duplicate ids, and invalid roll ranges block builds.

# Summon prefab authoring

Use `Assets/Prefab/Player/SummonBase.prefab` as the starting template for a
mobile summon. It is derived from `Ally_Stryker.prefab`, but uses `SummonContext`,
`SummonHealthSystem`, and `SummonedEntityRuntime` instead of Ally party and
persistent-progression components. Duplicate the template before assigning a
summon-specific model, stats, weapons, skills, AI tree, or effects.

A summon prefab requires `SummonContext` on the prefab root, plus `SummonedEntityRuntime`,
`SummonHealthSystem`, `StateHub`, `StatsHub`, `AITargetInfo`, and
`CombatEventBus`. Assign separate Gameplay Root and Presentation Root
references on `SummonedEntityRuntime`. During despawn, gameplay behaviours and
colliders anywhere under the summon root but outside Presentation Root are
disabled; Gameplay Root is also deactivated when it does not contain the runtime.
Presentation receivers can therefore finish VFX or callback work.

Mobile prefabs require `NavMeshAgent` and `AgentMoveDriver`. Stationary prefabs
require `CharacterColliderRefs.CharacterPositionCollider` with a supported
Box, Capsule, or Sphere collider. A stationary prefab with a solid collider must
use a kinematic `Rigidbody` (or no `Rigidbody`) so moving actors cannot push it.
Do not add player/ally/enemy party modules, persistent progression, inventory,
input, or helper-command components.

Use **Tools > RB > Summoning > Validate All Summon Prefabs** after authoring.
The validator also rejects nested summon-skill references that would create
recursive summon trees.

`SummonPlacementResolver` reads the prefab footprint from
`CharacterColliderRefs.CharacterPositionCollider` (or the mobile
`CharacterController`/`NavMeshAgent` footprint), ignores the ground collider
that supplied ground resolution, and delegates clearance scoring to
`CharacterPlacementResolver`. Mobile summons always require a valid NavMesh
footprint; stationary summons retain their authored ground and clearance
rules. The resolver evaluates a deterministic candidate set and can select the
least-overlapping candidate when every candidate is imperfect; it does not
reject solely because the first candidate overlaps an actor.

Summon placement exposes separate layout-orientation and facing controls. Use
caster forward, aim direction, or world axes for offset layout, then choose the
actor facing mode independently when the prefab is spawned. Ground resolution
excludes the collider that supplied the accepted ground hit from the clearance
check, so the default ground and clearance masks can safely overlap.

Runtime spawning stages a clone below an inactive `SummonStagingRoot`, validates
and injects owner/team/attribution before activation, then reparents it under
`SummonWorldRoot`. Keep gameplay modules under the Gameplay Root and presentation
callbacks/VFX under the separate Presentation Root.

During a same-frame summon batch, the transient placement reservation is owned by
the `SummonContext` root (`SummonContext.transform`), not necessarily by the
`SummonedEntityRuntime` component object. This keeps sibling runtime and physical
collider objects under one ownership boundary and prevents the shared resolver
from counting the live collider and its temporary reservation twice.

`Assets/Prefab/Player/MinigunTerret_Summon.prefab` is the stationary Feno turret
variant. It inherits `SummonBase`, nests `MinigunTerret 1` under the
Presentation Root, and uses the inherited stationary footprint collider from
`SummonBase`. The variant disables navigation and mobile AI components.

Targeting and rotation ARE wired up: the `AI System/BehaviorTree` component's
`Subtree` is overridden to `Assets/Scripts/AI/MinigunTerretAI.asset` (a
turret-only tree derived from `AllyAI.asset`, with the navigation/follow
branches removed and `AiRotateToTarget` driving the `Row` bone instead of
`TargetOrbitNavMesh`/`AiShoot`). Because `Row` lives inside the nested
`MinigunTerret_Visual` prefab instance, its `RotateRoot` binding is set as a
`SharedVariable` override directly on the `BehaviorTree` component's data
(not in the shared subtree asset — Unity does not allow reparenting an
existing object across a nested-prefab-instance boundary in Prefab Mode, so
this is the override point instead). `CharacterVisualController.firePointBoneName`
is overridden to `Row` (offset zeroed) so `FirePoint` is reparented onto the
turret bone at runtime and stays aligned with the barrel's rotation.

`CharacterVisualController.buildModelAutomatically` is overridden to `false`
on this prefab. `SummonBase` defaults it to `true`, which tries to build the
visual model at runtime from `baseStats.CharacterPrefab` — this summon has no
such stats entry, so that path silently no-ops (`Awake`/`Start` call it with
`silent: true`) and `_currentModel` never gets set. Since `AttachFirePointToModelBone`
and `CreateHealthBarOnModelBone` both bail out early when `_currentModel` is
null, leaving `buildModelAutomatically` at its inherited `true` value means
`firePointBoneName`/`healthBarBoneName` never take effect and `FirePoint`
stays parented wherever `ThirdPersonAimRigController` first wraps it
(typically `GameplayRoot`, not the turret bone) — the turret visually rotates
but the fire point silently doesn't follow. With it `false`, the controller
instead resolves the already-authored `MinigunTerret_Visual` via
`SetupExistingModel`/`ResolveExistingModelObject`, which is what actually
lets the bone-name overrides take effect. Any other prefab that authors its
visual model directly in the prefab (rather than spawning it from
`baseStats.CharacterPrefab`) needs this same override.

Actual weapon firing (ammo, fire-rate, projectile spawn) remains unimplemented
— only targeting and rotation are authored here.

`Assets/Data/Skills/Feno/Feno.Skill_MinigunTerret.asset` is assigned to Feno's
slot 2. It requests one stationary summon at the caster-forward offset
`(0, 0, 2)`, with a 10-second lifetime, 12-second cooldown, 25 energy cost, and
per-skill cap of one. Keep the payload prefab reference pointed at the variant,
not at `SummonBase` or the source turret prefab.

## Dialogue presentation

Full system reference: `Docs/SYSTEMS/DIALOGUE_SYSTEM.md`. What has to exist in the project and in
scenes:

**Project settings.** Layer 21 is `DialogueActor` and rendering layer 5 is named `Dialogue`. Both are
created by `Tools/Dialogue/Set Up Project Layers`. Every camera except the three stage-slot
`PortraitCamera` components must exclude `DialogueActor` — `CameraHolder.prefab`'s `Camera` and the
`GameSetup` `Main Camera` already do, and `DialoguePresentationScene` strips the bit from any other
camera once per load as a safety net.

**`Assets/Scenes/DialoguePresentation.unity`** is generated by
`Tools/Dialogue/Build DialoguePresentation Scene` and must stay enabled in Build Settings. Hierarchy:

```
DialogueStageRoot          DialogueStage + DialogueDirector, parked at y = -5000
├── Slots/Slot_{Left,Center,Right}   DialogueStageSlot cells at x = -100/0/100
│   ├── ActorAnchor                 actor stays at local origin, facing its camera
│   └── PortraitCamera              one camera + runtime RenderTexture per occupied slot
├── LightRig                          key + rim per slot, plus Fill; disabled between conversations
└── CloneStaging                      MUST stay disabled - clones are stripped here before waking
DialogueCanvas             DialogueUI: Dim -> Vignette -> Actors/{Left,Center,Right} RawImages -> DialogueBox
```

Re-running the builder rebuilds the scene from scratch, so tune UI portrait layout, light values, and
box styling **in the scene**, never by re-running it. Camera distance and position are runtime values
fitted from each actor's current pose bounds; do not use actor transforms for screen composition.

Current authored defaults:

| | Value |
|---|---|
| Slot cells | local x = `-100`, `0`, `100`; actor anchors at each cell origin |
| Slot cameras | FOV `34`; runtime fit padding `1.08` |
| Actor RawImages | equal screen thirds; speaker scale `1`, listener scale `0.94` |
| UI emphasis | speaker y offset `18`, listener tint `(0.78, 0.78, 0.78, 1)`, blend `0.18s` unscaled |
| `DefaultDialogueLightRig` | `listenerIntensityScale 0.55`, `fillIntensity 0.8` |

**Tuning slots in Play Mode does not persist** — the presentation scene is loaded additively at
runtime, so leaving Play Mode discards it. Write the numbers down before stopping, or tune the scene
asset directly in Edit Mode.

**The slot lights are siblings of the slot cells, not children.** Moving a cell leaves its key and
rim light behind; re-derive them as `slotLocalPosition + (0.8, 2.1, -1.6)` for the key and
`+ (-1.1, 2.3, 1.4)` for the rim, which is what the builder does.

The voice `AudioSource` should keep `Ignore Listener Pause` on. The skip prompt's progress `Image` is
`Filled`/`Horizontal` and needs a sprite assigned before it can sweep — a Filled Image with no sprite
never shows a fill.

**Per-character.** One `CharacterDialogueAnimationProfile` per speaking character, `characterId`
matching `CharacterStats.characterId`, an `idlePose` assigned (it is the fallback for every unmapped
pose), and the asset registered in `Assets/Data/Dialogue/DialogueProfileDatabase.asset`.

**Per interact point.** `DialogueTrigger` alongside the existing `InteractableLink`. Play-once
triggers need their sequence's `dialogueId` filled in, or completion cannot be persisted.

**Per NPC that appears on stage.** `DialogueStageActorSource` on the NPC, with a `characterId`
prefixed `npc.` so it cannot collide with a `CharacterStats.characterId`, a `displayName`, and
`modelRoot` left empty (clones the NPC's own GameObject). The trigger's **Stage cast** list picks it
up automatically when it sits on the same object. The NPC still needs a
`CharacterDialogueAnimationProfile` with an idle clip **authored on its own rig** — see the
retargeting warning in `Docs/SYSTEMS/DIALOGUE_SYSTEM.md`. `Abbygail_NPC.prefab` is set up this way as
`npc.abbygail`; its profile `Data/Dialogue/Profiles/DialoguePose.npc.abbygail.asset` is deliberately
empty and waiting for that clip, so she currently stands in bind pose.

**Boot.** `DialoguePresentationSceneLoader` sits on the `GameSetup` `System` object so the
presentation scene is preloaded and re-loaded after every single-mode scene load.
