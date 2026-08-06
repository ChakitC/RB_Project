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
the `Interactable` layer. `RoomController` configures them as a one-use 50%
party Heal Point and a reserve-only party Ammo Point; simple runtime fallbacks
are created only when an authored station is missing. The authoring tool binds
the existing `RoomDefinition.Heal` into all three Test Stage configs without
rewriting that room asset. Keep the definition enabled, assigned to a prefab,
and at two or more exits; the pre-Boss blue-node validator requires a usable
multi-exit Heal definition.

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

## Character Skill Loadout Authoring

Author character-default active skills on the `CharacterStats` asset under
`Skill Loadout`. A slot represents one command slot and should have a stable
`slotId`, hotkey, default option index, and one or more options. An option points
at the actual `SkillGemDefinition`, level, and support gems to use when selected.

Use explicit `optionId` values when multiple options in the same slot could
reference skills with missing or duplicate `skillId` values. If `optionId` is
empty, runtime save/load uses the skill definition's `skillId`.

`CharacterSkillManager` builds runtime command slots from `ctx.baseStats`.
`CharacterStats` is authoritative for every slot index it defines. Prefab-authored
`autonomousSlots` are still supported only as legacy fallback slots when
`CharacterStats` does not define that index. New character prefabs should leave
active-skill choices in `CharacterStats`.

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
presentation event (`Vfx`), the camera shake marker (`ShakeCamera`), and the
HitLag feedback marker (`HitLag`).

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

### AITargetSensor.tauntedEffectDef (required)

`AITargetSensor` requires a `[Header("Taunt")] tauntedEffectDef` reference
(`StatusEffectDef`) to apply taunt. Without it, `ApplyTaunt` no-ops and logs a
warning. Assign the `Taunted` status effect asset
(`Assets/Scripts/StatusEffects/Taunted.asset`) on every prefab that has an
`AITargetSensor`:

- `Assets/Character/Mons/Enemy_Base Variant.prefab`
- `Assets/Prefab/GameEnemy/Enemy_Base.prefab`
- `Assets/Prefab/AI Ally/Ally.prefab`
- `Assets/Prefab/Player/Ally_Helper.prefab`
- `Assets/Prefab/Player/Ally_Stryker.prefab`

`Ally.prefab` has no `StatusEffectController`; `AITargetSensor` lazily
`AddComponent`s one at runtime when needed, matching the auto-provision
pattern already used by `CharacteContext.ResolveReferences()` for components
like `AccessoryLoadout` and `ThirdPersonAimRigController`.

## Root Motion Trajectory Authoring

No additional prefab, skill asset, or FBX importer fields are required for
player and party chain-attack root motion placement or Guaranteed Interruption
ally placement. The attack `AnimationClip` remains the source of truth: Unity
must already extract the clip's root motion so `Animator.deltaPosition` and
`Animator.deltaRotation` produce the intended movement during playback.

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
teleport-profile collision masks, probe collider, anchor offset, and optional
NavMesh requirement are reused.

The chain attack probe collider must continue to represent the actor footprint.
Guaranteed Interruption uses the ally context's character-position collider in
the same way. The current prefab reference is used for start, impact,
full-trajectory, target-overlap, sweep, and NavMesh-footprint validation. A clip
with no extracted XZ displacement or yaw uses the existing teleport behavior.
Helper chain attacks are not changed by this feature.

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
