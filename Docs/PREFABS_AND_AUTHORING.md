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

## Equipment Upgrade UI

`Assets/UI/UIWeaponUpgrade.prefab` is the shared weapon upgrade and equipment
dismantle panel. Its root should include both `WeaponUpgradeService` and
`AccessoryDismantleService`.

On `UIWeaponUpgrade`, bind:

- `upgradeService`
- `accessoryDismantleService`
- `upgradeButton`
- `dismantleButton`
- the inventory list, selected slot, detail text, and drag visual references

The inventory list accepts weapon and accessory instances. Weapon selections
can upgrade or dismantle. Accessory selections disable Upgrade and allow
Dismantle only when `EquipmentAssignmentService` reports that the instance is
not equipped by any character. Inventory slots and the selected-item slot show
the assigned character portrait when the instance is equipped, matching the
portrait marker used by `UIEquipment`.

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

## Combat Timeline Event Authoring

Animancer-driven combat and hitbox events should use `CombatTimelineEventName`
enum values in gameplay data instead of authoring new `StringAsset` references.
The enum values have explicit numeric ids because Unity serializes enum fields
by number.

Current supported event keys include common hitbox events (`HitStart`, `HitEnd`),
pre-cast block events (`PreCastOpen`, `PreCastClose`), and the repeatable skill
presentation event (`Vfx`). Prefab hitbox skill
payloads are sequential-only: every `HitStart` opens the next configured step
and every `HitEnd` closes the currently active step. Multi-step skill hitboxes
should order their payload steps to match the `HitStart`/`HitEnd` pairs in the
Animancer clip.

Timeline event authoring is enum-only. New authoring should select enum values
from the inspector dropdown. Do not add `StringAsset` timeline-event fields back
for gameplay hitbox or pre-cast flow, and do not reorder existing enum values;
append new values with explicit numbers.

## Skill Timeline VFX Authoring

Add `SetSkillVfxData` to the character or an authoring object in the scene or
Prefab Mode. Assign the target `SkillGemDefinition`. `Character Root` and
`Source VFX Root` are optional: the component resolves the nearest character
context and uses its own transform as the source container when they are empty.

Use this workflow:

1. Add the repeated `Vfx` events at the required times in the Skill Animation
   VFX Timeline.
2. Press `Create / Sync VFX Slots`. The tool creates one
   `SkillVfxAuthoringSlot` for each timeline VFX cue and migrates legacy entries
   into the matching slot.
3. Select a slot, add one or more assets to `VFX Prefabs To Add`, then press
   `Add VFX Prefabs`. The tool creates one `SkillVfxAuthoringEntry` per prefab.
4. Move, rotate, or scale each prefab child in the Scene view, then configure
   its anchor, anchor mode, action, and loop settings on its entry component.
5. Preview from the timeline or with `Play All VFX` / `Stop All VFX`.
6. Press `Save VFX Data` in the timeline window or `Save VFX To Skill` on
   `SetSkillVfxData` after every authoring change.

Use `Add Empty VFX Entry` on a slot to author `StopLoop` or an entry whose prefab
will be assigned manually. `New Entry Settings` and `Create Authoring Entry`
remain available for advanced cases and place the new entry in the selected cue
index's slot.

`Load VFX From Skill` replaces `SkillVfxAuthoringSlot` objects and legacy loose
`SkillVfxAuthoringEntry` objects under `Source VFX Root`, groups entries by cue,
creates visible prefab-instance children, and reconstructs their saved placement.
`Clear Authoring Slots` removes the same authoring hierarchy after confirmation.
Other character children, bones, hitboxes, and authored objects are not removed.
The authoring hierarchy records which Skill Definition owns it. Changing the
assigned Skill before adding, syncing, or saving VFX rebuilds the hierarchy from
the newly assigned Skill, so prefab entries from the previous Skill are not reused.

`Play All VFX` and `Stop All VFX` use the authored prefab children as placement
sources and play temporary, non-saved scene instances. Each entry supports one
direct prefab-instance child, which supplies both the prefab asset and visual
placement. Its transform is converted to position and rotation relative to the
selected anchor when saving; its scale is stored as a multiplier relative to the
prefab asset. A slot supports multiple VFX by containing multiple entries.

`Custom Child Path` is relative to the character context root. `Humanoid Bone`
requires a valid Humanoid Animator. Validation reports unresolved paths or
bones. `Anchor Mode` controls attachment after spawning: `World Space` keeps the
VFX at its spawn pose, while `Follow Anchor` moves it with the selected caster
root, cast origin, aim transform, custom child, or Humanoid bone. Editor preview
uses the same attachment behavior. `StartLoop` and `StopLoop` entries must use
the same non-empty loop key.
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
hitbox, VFX, and other Animancer events. Scrubbing the playhead across a `Vfx`
marker previews that cue in both directions. Particle previews are simulated on
temporary playback instances and remain visible without selecting the authored
Hierarchy objects. The timeline advances cached ParticleSystems incrementally,
rather than relying on Unity's selection-driven Particle Effect panel. Completed
preview instances are deactivated and reused when the same cue plays again, which
avoids repeated prefab instantiation and hierarchy scans while scrubbing or
looping playback. Cartoon FX Remaster playback instances
also register their package editor-update hook directly, so shader position and
light animation updates do not require opening the CFXR component inspector.
Changing Hierarchy selection or timeline-window focus does not stop particle
simulation. Preview updates are coordinated by one shared
75 FPS tick owned by the Skill Animation VFX Timeline window. Authoring entries
never subscribe to `EditorApplication.update` themselves. The window subscribes
only while animation playback or a VFX preview is active, advances cached
ParticleSystems incrementally, and performs one player-loop request and Scene
view repaint per tick. The Preview Runtime row shows active preview, cached
ParticleSystem, registered CFXR callback, and actual update-rate counts. Closing the
window, entering Play Mode, compiling scripts, or quitting the Editor removes
preview objects and unregisters Cartoon FX editor callbacks. Starting a preview also enables `Effects` and
`Particle Systems` in every Scene View because Unity otherwise filters normal
particle renderers while its selection-driven Particle Effect preview remains
visible.
Drag a marker to change its normalized time without triggering crossed cues.
Right-click the timeline at the target time and choose an event from the
VFX, Hitbox, Pre-Cast, or Other submenu. Alternatively, choose an enum value
under `Event At Playhead` and press `Add Event`, or select a marker and press
`Remove Selected`.
Missing Animancer `StringAsset` event-name assets are created under
`Assets/Data/CombatTimelineEvents`. `Vfx` is the only event that may be repeated;
other duplicate enum event keys are rejected.

Crossing a `Vfx` marker during playback triggers entries with the matching cue
index. Clicking a VFX marker selects its first matching authoring entry. Dragging
a VFX marker across another marker reorders their cue indices. Removing a VFX
marker also removes entries in that cue group and shifts later cue indices down.
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

Keep existing `SetSkillVfxData` components on scenes and prefabs. They inherit
the shared authoring behavior while preserving the old component GUID, Skill
reference, hierarchy slots, entries, and buttons. V2 requires no prefab or scene
conversion.

The hierarchy owner is `(source asset, entry ID)`. Changing a Melee step stops
preview and rebuilds from that step. Maintain step IDs with
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

Ally-only systems may depend on `AllyContext` when they need ally-specific
fields such as `AITargetSensor`, `NavMeshAgent`, or `AgentMoveDriver`.

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

## Before Removing A Serialized Field

Check all of these first:

1. Is the field referenced by scenes or prefabs?
2. Is the field saved in player data or migration data?
3. Can runtime code already resolve the same reference through context?
4. Is the field local authoring data rather than a peer module?
5. Will removing it break inspector workflows or old prefab variants?

If any answer is unclear, keep the field and migrate deliberately in a separate
task.
