# Prefabs And Authoring

## Character Menu Mind layout (2026-10-10)

`Assets/Spain/UI/Character Menu Mind/CharacterMenuMind.prefab` is the uGUI layout
assembled from the 16 supplied PSD layer exports. It is nested in
`Assets/Prefab/User Interface/UpgradUI.prefab`, which retains the existing
`UILoadLaval` entry point and inactive legacy children. The Basement scene uses
this integrated host for every character. Its overlay Canvas uses a 1920x1080
reference resolution and Expand scaling. The centered Artboard has fixed
1920x1080 dimensions; `BasementCharacterMenuScreenController.LateUpdate` scales
the whole board to fit its parent so sprites and TMP text scale together.
Keep its AspectRatioFitter disabled and both anchors at (0.5, 0.5).
All PNGs retain their full-canvas positions; do not use Set Native Size on the
individual layers. Sprite imports use Single mode, no mipmaps, and uncompressed
2048 maximum resolution to preserve the typography and transparent edges.

`Artboard/EquipButton`, `GunButton`, and `SkillButton` contain bounded transparent
hit targets, Button components, and explicit up/down navigation. The host binds
EQUIP to Accessory, GUN to weapon equipment and SKILL to Active Skill Tree through
the existing `UILoadLaval` actions. Each Label uses a UI Mask to clip its
Halftone child to the letter shapes, restoring the PSD clipping effect. Decorative
images do not receive raycasts. CharacterStatus and CharacterLogo hold the right
panel layers. Name, Level, XP, eight stats, Gold and UPLEVEL use existing character
and inventory data. BACK closes the status session. The center remains transparent
over the Basement environment; use the scene EventSystem, not an embedded one.
The layout CanvasGroup starts hidden and the host controller reveals it after
the camera transition. Bind owner, camera manager, selection, group, artboard,
definition, background roots and all four letterbox bars on the host controller.

`CharacterMenu.asset` uses `Aires_CharacterMenuCamera.asset`, baked from
`Assets/Character/Aires/Aires_CharacterMenu_Camera.fbx`, path
`Camera CharacterStatus Menu`. Register the scene `CharacterMenuCamera` in
`CameraManager.menuCameras`. Edit each character's `BasementAnimationProfileSO`
under **Menus > CharacterMenu**: Animation owns the status clip/fade/speed/start
time and Camera references that character's `<Name>_CharacterMenuCamera.asset`.
All eight production profiles have this entry and separate camera assets, initially
copied from the existing shot. Adjusting one character's camera does not alter the
others. The menu definition's Default Camera remains a fallback for missing overrides.
Aires uses looping `Aires_CharacterMenu_Idle`; other characters retain their previous
status transitions. The old `inMenuIdle` field is hidden but retained for serialized
compatibility with legacy status hosts. The integrated screen exclusively uses
its Menus entry. `AgentScripts/CharacterMenuProfileMigration.cs` migrates missing
entries/cameras without replacing existing ones. Null material slots in
`AiresSelect.prefab` were restored from
the corresponding source renderers without replacing its animation/weapon overrides.

Status is the parent presentation. Accessory/GunMenu return directly to its camera
and pose; Skill Tree temporarily hides/disables the status UI within that session.
Only closing Status restores formation, background UI and selection. See
`VALIDATION.md`, Character Menu Mind integration, for automated and runtime results.
The return animation uses the character profile's Idle Fade Duration. Roma, Roger
and Milano now use 0.25 seconds, matching the other production profiles; their
previous 0/near-zero values visibly snapped on menu exit. This setting also applies
when returning to Idle from Pickup. Pickup fade settings are unchanged.

## Basement menu cameras and animation (2026-10-10)

Menu camera blends track the selected character (2026-10-11). CameraManager's
`Menu Blend Look At Offset` selects a character-local framing point, default (0, 1.1, 0).
It uses presentation-root position/rotation and ignores imported scale. Runtime-only
`BasementMenuBlendTarget` extensions supply this reference to formation and visited
menu cameras. Preset endpoints remain unchanged; generated camera states represent
authored roll as Dutch so Cinemachine preserves tilt while blending around the target.
The extension caches authored orientation separately: Cinemachine writes the upright
RawOrientation back to its Transform each frame. Reading roll from that output would
erase the outgoing camera's tilt and cause a jump at blend start. CameraManager updates
the cached pose for the active camera and restores it when removing the extension.
Extensions are removed on session close/interruption. No manual aim component is needed.

`AiresSelect.prefab` overrides the inherited
`SubmachineGun_02_Right_EditorPreview` GameObject to inactive. This placement preview
comes from `Assets/Prefab/Charactor/Aires.prefab`; keep it disabled in the Basement
variant because `BasementWeaponPresentation` mounts the equipped weapon at runtime.
Enabling both produces two overlapping guns under `Weapon.R`.

Accessory and GunMenu now use `BasementMenuDefinitionSO` assets in
`Assets/Data/Animation/Basement/Menus`. Each definition owns its default camera,
EaseInOut blend duration, final UI reveal duration and other-character visibility.
The defaults remain 0.8s / 0.5s / 0.45s. Screen-local legacy fade fields are retained
for serialized compatibility and hidden from the Inspector; edit the menu definition
for current timing. Both screen CanvasScalers use Expand with a 1920x1080 reference
and a fitted 16:9 artboard, keeping authored pixel offsets aligned on wide/tall displays.
Letterbox bars render above the artboard and intercept hits outside the visible area.

`CameraManager.menuCameras` binds each definition to a scene CinemachineCamera.
The same object owns `BasementMenuPresentationController`, which handles one menu
session, backgrounds, selection/drag locking, letterbox and ally material restoration.
Screen controllers implement `IBasementMenuView` and retain inventory operations.
Do not add an Animator/Animancer or weapon spawner to a menu UI prefab.

### Blender camera workflow

1. Author the character origin and static camera in the same Blender coordinate
   system. Export the camera as FBX; preserve the imported Unity model hierarchy.
2. Create **Game > Basement > Menu Camera Preset**, assign Source FBX and select
   the Camera path. Multiple cameras require an explicit selection.
3. Press **Bake Camera** in Edit Mode. The preset stores camera position/rotation
   relative to the imported model root, including intermediate parent transforms,
   plus Cinemachine lens settings (physical sensor, gate fit, shift, clipping and FOV).
   Reimporting the FBX does not overwrite the baked preset; press Bake again deliberately.
4. Assign the preset to the menu default or a character's menu entry. No reference
   FBX camera is instantiated/enabled during play. Inspect the resulting shot at 16:9.

`Aires_GunMenuCamera.asset` is baked from
`Assets/Spain/UI/Gunmenu/Aires_camera_Gunmenumene.fbx`, camera path `Camera Gun Menu`:
position (-0.399025, 2.098622, 1.953272), Euler (13.439820, 189.280000, 0.000014),
26mm lens, 36x24 sensor, Horizontal gate fit, zero shift, near/far 0.1/1000.
Its imported root has identity rotation and scale. Avoid applying Blender/FBX axis
or scale conversion a second time. The runtime pose uses the stable
`PartySlot.PresentationRoot`, never an animated bone.

### Per-character menu animation

Use the existing `CharacterStats.basementAnimationProfile`. Its `menus` array
contains a menu definition, a `ClipTransition` and an optional camera override.
Use one entry per menu. A missing entry/camera uses the menu default camera;
a missing clip preserves the current pose without restarting it. Duplicate menu
entries are reported and resolve to defaults instead of selecting an arbitrary one.
The original Aires profile has a GunMenu camera override as the first authoring example.

### Current character profiles (2026-10-10)

Milano menu assets updated (2026-10-12): `Milano_BasementAnimation.asset` uses
the clips in `Assets/Character/Milano`: `Milano_CharacterMenu_Idle`,
`Milano_EquipMenu_Idle` and `Milano_GunMenu_Idle` for CharacterMenu, Accessory
and GunMenu respectively. All three have Loop Time enabled; legacy `inMenuIdle`
also uses Milano's CharacterMenu clip. Existing transition timing and formation
Idle/Pickup remain. The individual `Milano_CharacterMenuCamera`,
`Milano_EquipMenuCamera` and `Milano_GunMenuCamera` presets are baked from
the CharacterStatus, Equip and CharacterGun FBX cameras in Milano's `Menu Camera`
folder. The gun source filename intentionally retains `Menuu.fbx`. These assignments
supersede Milano's borrowed Aires clips below. Re-bake after source camera changes.

Visual compatibility remains unresolved: the supplied menu FBXs have 164 face
renderer bones, including 13 `cc_fs_999c93b5a182_*` bones absent from the current
151-bone `Milano.fbx`/MilanoSelect model. Runtime screenshots show blank eyes in
CharacterMenu and missing/deformed face geometry in Equip/Gun. The references are
configured, but this content is not visually accepted; export animations against
the production rig or deliberately migrate the model and its skin bindings together.

Feno menu assets updated (2026-10-11): `Feno_BasementAnimation.asset` now uses
`Assets/Character/Feno/Feno_CharacterMenu_Idle.fbx`, `Feno_AccessoryMenu_Idle.fbx`
and `Feno_GunMenu_Idle.fbx` for CharacterMenu, Accessory and GunMenu respectively.
All three imported clips have Loop Time enabled; the legacy `inMenuIdle` also uses
Feno's CharacterMenu clip. Existing transition timing and formation Idle/Pickup remain.
The three `Menus/Feno_*MenuCamera.asset` presets are baked from the matching FBX
cameras in `Assets/Character/Feno/Menu Camera` (CharacterStatus, Accessory,
CharacterGun). These assignments supersede the borrowed Aires menu clips below
for Feno. Re-bake the individual preset after changing its source camera.

The shared trial has ended. All eight production CharacterStats again reference
their own `<Character>_BasementAnimation.asset`. Original formation idle/pickup
and Animator paths are preserved. Aires additionally keeps the supplied Inmenu,
Accessory and GunMenu transitions and cameras in `Aires_BasementAnimation.asset`.
The other seven profiles now use Aires clips only for previously empty Inmenu,
Accessory and GunMenu transitions. Their existing formation Idle/Pickup and
camera overrides are preserved; newly added menu entries use the menu's default
camera. These are separately editable transitions in each character's own profile,
not a shared profile reference or automatic fallback link to Aires. The known
cross-rig face/weapon limitations below still apply to these borrowed menu clips.
`Shared_Aires_MenuTest.asset` remains available but is
not assigned to production characters.

The retained shared test asset contains the following configuration:
Formation Idle uses `Aires_Formation_Idle`; the separate `inMenuIdle` transition
uses `Aires_Inmenu_Idle` only while the character information panel is open.
Accessory uses `Aires_AccessoryMenu_Idle`; GunMenu uses
the supplied `Aires_GunMenu_Idle`. These three FBX clips have Loop Time enabled.
Transitions use 0.25s fade, speed 1 and start time 0. Pickup retains
`Aires_Formation_Pickup` from the cloned Aires profile. Menu entries reference their
existing default camera presets. Each character keeps its own `basementAnimatorPath`.

This was a temporary content configuration, not automatic Generic-rig retargeting.
Main body paths overlap, but clothing/face paths differ: the Accessory clip has
211 Transform paths, with 71-198 matching paths across these models. Runtime shots
show Milano's face becoming malformed and some weapon grips not aligning. Use this
profile for menu/transition tests, not final character-art acceptance. To restore
individual content, assign the preserved `<Character>_BasementAnimation.asset`
to that character's `basementAnimationProfile`; no prefab changes are required.
The `Assets/Tests/DefensiveBlock/Aires.Test.asset` fixture keeps its previous profile.

The state flow is **Formation Idle <-> Pickup**, **Formation Idle -> character
information (In menu) -> Accessory/GunMenu**. Back from either submenu restores
In menu; closing the information panel restores the current formation/pickup state.
`UILoadLaval` owns that parent session through `SetInCharacterMenu`, including slot
rebinding, preview rebuild and disable cleanup. Submenus hide its Canvas without
disabling its GameObject, so the parent pose remains the return destination.
A profile without `inMenuIdle` falls back to its formation/pickup pose.

Assign the authored AnimationClip and its Fade Duration, Speed and Start Time.
Looping/non-looping behavior follows the clip; non-looping states hold naturally.
Menu playback temporarily uses unscaled time. Selection commands during the menu
update the desired picked state without replacing its pose. Closing crossfades back
to that selected/idle state and restores the prior update mode. Mounted weapon changes
continue through `BasementWeaponPresentation` without restarting the menu animation.

For a new screen, create a definition/preset, bind a CinemachineCamera in the manager,
implement `IBasementMenuView`, then call `TryOpen(definition, slot, view)` and `Close()`.
The central controller exposes `CanInteract`, `IsTransitioning` and `Owns(view)`.
Only call Close for the owned view. Active Skill and combat presentation are unchanged.

## Basement character animation

`Assets/Prefab/CharactorSelect/BasementCharacterPresentation.prefab` owns the
shared `BasementCharacterPresentation`, `BasementAnimationController`,
`AnimancerComponent` and `BasementWeaponPresentation`, with a `ModelRoot` child. Both `SlotPoint.prefab`'s
`PartySlotVisualPreview.presentationPrefab` and `CharactorSlotUI.prefab`'s
`SpawnOnDragOutButton.presentationPrefab` reference this same asset. Each slot or
drag preview creates its own instance, so picking one cannot affect another.
The wrappers delegate model creation, animation binding and teardown to this owner.

`BasementCharacterPresentation` is the common entry point: `Build`, `Clear`,
`SetPicked`, `ResetToIdle`, `ShowWeapon` and `ClearWeapon`. Slot and drag wrappers
expose their owned `Presentation`; `CharacterSelectable` receives that instance
through `BindPresentation` and never searches for an animation controller.
Selection/outline and party saving remain with their existing input/slot modules.
Definition sync reads the bound presentation's `CurrentDef`, not arbitrary child
holders (which may belong to outgoing models or decorations).

`PartySlotVisualPreview` and `CharacterDragWeaponPreview` resolve equipment data
using their save slot/inventory context, then pass the selected `GunConfig` to
`ShowWeapon`. Only `BasementWeaponPresentation` creates and destroys mounted objects.
Configure fallback hand names and offsets on that component in the shared prefab;
per-weapon `GunConfig` mount overrides still take precedence. Repeated refreshes
reuse the mounted objects; changing models clears weapons before destroying the
old skeleton. The legacy drag `SetAnimator` API is retained for compatibility;
the presentation's live Animator is the authoritative binding.

Each playable `CharacterStats` holds `CharacterPrefabBasement` (model only),
`basementAnimationProfile`, and `basementAnimatorPath` (exact path relative to that
model; empty means its root). The eight model prefabs contain no Basement controller
or Animancer component. Their separate `BasementAnimationProfileSO` assets live in
`Assets/Data/Animation/Basement`.
Assign Idle and Pickup transitions there; Fade Duration is the time to enter
that pose. Initial poses play immediately: slots start in Idle, roster previews
start in Pickup without creating or evaluating Idle first. Clip loop settings are preserved;
a non-looping clip holds its last frame until the selection changes.
Both transitions use an explicit Start Time of `0x` to restart on entry, including
when reversing an unfinished blend. Repeated `SetPicked` commands do not replay.
`ClipTransition` owns Fade Duration, Speed and Start Time; the controller uses
`Play(transition)` and only overrides fade to zero for the initial pose or explicit Idle reset. If an
author changes Start Time, reset uses that authored initial pose too.

Non-looping clips hold their final pose naturally while their state time continues
to advance. Do not add an end callback, clamp time or set speed to zero just to
hold Pickup. This follows the developer's [Playing Animations](https://kybernetik.com.au/animancer/docs/manual/playing/)
and [End Events](https://kybernetik.com.au/animancer/docs/manual/events/end/) guidance.

Bind the shared controller's Animancer explicitly; leave its Animator and profile
empty on the shared asset. `Build` binds them from the selected data and resolves
only the exact Animator path, never the first Animator in the hierarchy.
The target must match the clip's relative paths. Aires, Feno, Milano and Roger animate
their named model child; the other four animate the prefab root. Each prefab has
one enabled Animator with an Avatar, no Runtime Animator Controller, and root
motion disabled. Outer Animator components on nested layouts remain disabled as
Basement-only overrides. Do not apply these overrides to shared model prefabs.

`CharacterSelectable.SetPicked` and drag preview selection call the presentation,
which forwards animation commands to its controller.
`Build(selected)` retains its existing API and starts new slot models in Idle;
the new `Build(selected, startPicked)` overload lets roster previews start directly
in Pickup. The matching animation `Bind` overload sets the initial selection state.
Refreshing an already-built definition is a no-op: it preserves model, state,
playback time, blend weights, menu ownership and drag sway. Changing definitions
still clears the old model/graph. Call `ResetToIdle` explicitly when a reset is wanted.
Wrappers expose the bound Animator for weapon mounts; they must not call
Animator.Rebind or set Mecanim parameters. The legacy
`CharacterSelectable.animator` and `pickedBoolName` fields remain for serialized/API
compatibility; the latter no longer controls animation. A missing animation binding
does not cancel selection or its outline. Ambiguous preview controllers are reported
instead of choosing the first child. Combat Brain/Driver/Profile assets are independent.

Model replacement/clear calls `Unbind` before destroying the old model, destroying
its Animancer graph/output. The shared owner survives
model swaps. `SlotPoint.prefab` no longer contains the old authored Roma model.
Slot refresh and dropping never scan/delete children by name or by the presence
of `CharacterDefHolder`/`CharacterSelectable`; unrelated children are preserved.
The public `ClearTemporarySelectObjects` compatibility entry points are now no-ops.
The existing slot `modelRoot`
remains `PartySlot.PresentationRoot` for the Accessory camera; do not replace that
anchor with the animated skeleton. Re-enabling a bound presentation resets to Idle.

Original Basement Controllers are retained as migration references. Dorothy's
profile takes its clips from its Basement Controller, correcting the former empty
Battle Controller assignment. Existing per-character blend durations are preserved.

Menu entry takes animation ownership before cancelling formation dragging, so
selection cleanup cannot play Idle between Pickup and the menu. Child-menu entry
and return call `SwitchMenu(entry)` directly, preserving unscaled playback and the
original update-mode restoration state. A missing destination clip retains the
current pose. `ExitMenu` is reserved for leaving the menu session and then restores
the current formation/selection pose. Existing transition fade durations still apply.

### Drag sway and model sizes

The shared `BasementCharacterPresentation.prefab` owns `BasementDragSway` and a
`SwayRoot` above `ModelRoot`. Keep slot roots, labels and menu-camera anchors outside
that sway root. The model and its mounted weapons rotate together without editing
animation bones or enabling root motion/physics.

Each of the eight Basement model prefabs has `BasementDragRig` on its root. Bind
Head, Left Foot and Right Foot explicitly to bones in that model; these are Generic
rigs, not Humanoid bindings. Grip Offset uses model-local axes and fractions of the
initial head-to-feet length. Do not include hats, render bounds or weapons in size
measurement. For a new model, add this component and bind all three bones. A missing
or foreign binding warns once per model build and leaves ordinary dragging usable
without sway.

`Build` captures the authored body vector before evaluating either initial animation,
so Pickup previews do not need a hidden Idle evaluation. `BeginDrag` measures
its world length including parent/model scale and freezes spring frequency for that
gesture. Animation crouching cannot change the weight. The live animated head is
the pivot: each LateUpdate removes the previous sway and compensates the new rotation
so the grip stays at its unswayed animated position, including scaled parents.

Shared Inspector defaults: Sensitivity **15 degrees per viewport-height/second**,
Max Angle **20 degrees**, Frequency **2 Hz**, Damping Ratio **0.6**, Reference Height
**1.29451823** (Roma at normal scale). Effective frequency is
`clamp(frequency * sqrt(referenceHeight / bodyHeight), 1.4, 2.8)` Hz. Larger models
therefore swing more slowly. Only horizontal pointer speed drives the spring; the
rotation axis follows the drag camera's forward direction. Integration uses unscaled
time and substeps no longer than 1/120 second. Focus changes and frames exceeding
0.1 second discard the pointer delta to avoid a resume impulse.

Presentation exposes `BeginDrag(Camera, Vector2)`, `UpdateDrag(Vector2)` and
`EndDrag()`. Formation input forwards through its bound `CharacterSelectable`;
roster input forwards through `CharacterDragVisualPreview.Presentation`, beginning
only after placing the spawned preview. No-event frames still damp toward zero.
Release, cancellation, entering menus, clearing/replacing models and disabling restore
the original pivot pose and erase spring velocity. Selection-only `SetPicked(true)`
does not start sway. Placement and party-save rules remain owned by existing inputs.

## Character passive UI

Assign a prefab with a `PassiveHudView` root to `PassiveDefinition.hudPrefab`.
Leave it empty for passives without a custom HUD. Never assign a scene instance.
`GameplayHUD/PassiveHUD` contains the shared `PassiveHudHost` and horizontal layout.
Select this direct child and edit its RectTransform position/anchors to place the
passive window. Runtime views spawn beneath it, one per grant. It stays independent
of HP panel movement. Re-running the builder preserves an existing root-level
host's authored position; older nested hosts migrate without moving on screen.

Roma uses `Assets/Character/Roma/Passive/UI/RomaPassiveHUD.prefab`, already assigned
on the production Stamina/Momentum definition. Open this prefab to edit TMP text,
colors and `PassiveGaugeArc` widths/segments. Keep its `momentumVariable` binding
equal to the recipe variable ID. UI references are local authoring fields; owner
Stamina and PassiveController resolve through CharacteContext at binding time.

`Tools > RB > Passives > Create Roma HUD` creates missing assets/host and assigns
the production reference. Re-running preserves existing authored prefab visuals.
Play with Roma selected to test actual spending, Perfect Dash and a normal attack.
For Combat Surge, spend 100 total Stamina (refilling between payments if needed),
then fire a successful normal shot. The existing Momentum ring shows charge,
READY, then a five-second countdown. `surge-active` is the recipe's timer binding;
the serialized `nextAttackText` reference now displays the Surge state. Keep this
local binding intact. The three other passive effects still use their existing UI.
Use the dedicated PassiveComposition test assemblies for automated checks.

## Player Sprint and Roma Run

PlayerMovementCC exposes `sprintSpeedMultiplier` (1.6),
`sprintAccelerationSeconds` (0.2), and `sprintReleaseSeconds` (0.12). Existing
Player prefabs use these defaults; no new component is required. Keep the
PlayerContext movement binding and the Dash action's performed/canceled events.
Shift is the authored keyboard Dash binding. Tap to dash; hold through completion
to sprint; release to resume ordinary directional movement.

During Sprint, rotate the camera to steer. `sprintTurnSmoothTime` defaults to
0.25 seconds (larger values respond more slowly), and `sprintMaxTurnSpeed` defaults
to 360 degrees/second. The smoothed heading controls both travel and body yaw;
camera pitch and movement keys do not steer the run. Dash retains its initial
straight path. The same Roma_Run animation continues while turning.

`Assets/Data/Animation/Roma/Roma_AnimProfile.asset` assigns its `sprint` transition
to `Assets/Animation/Ch_Roma_Anim/Roma_Run.fbx` / `Roma_Run`, fade 0.12 seconds.
The model clip imports with Loop Time/Loop Pose and root rotation/XZ/height locks.
Sprint displacement is driven by gameplay code. The clip is optional on other
profiles. With a sprint clip assigned, all facing directions keep that single
clip until Sprint ends; the directional movement mixer resumes afterward.

## AI Ctx authoring

For every owner-consuming custom Task, bind `context` to a Graph variable named
`Ctx`, type CharacteContext, default null. Insert `InitializeAIContext` before the
old behavior. Under a root Repeater use Sequence -> initializer -> old behavior.
Bind all nested Stacked Tasks too. Nested Subtree references must explicitly override
the child's Graph Ctx with the parent's Ctx using the native override list.

Shared sensor, movement driver and aim driver references now live on CharacteContext.
Existing serialized sensor/driver names and bindings are retained. Assign explicit
bindings when multiple candidates belong to one actor. Missing navigation is valid
for a stationary actor; the shooting branch still needs sensor, StateHub and weapon.
Formation Tasks receive their authored InCombat binding, and Ally contexts supply
InterruptionController. Enemy-only SpecialShootPoints stays on EnemyContext.

Run Tools > RB > AI > Validate Owner Context after editing. It checks Ctx type/scope,
all consumer bindings and initialization ordering. Migrate Owner Context is an
explicit one-time command for existing Subtrees/inline trees; it does not run on
import. Reopen/save/reopen to confirm node count, GUIDs and bindings remain intact.

## P0 shooting AI example

`Assets/Prefab/GameEnemy/Enemy_M_GR_01 Variant.prefab` keeps its existing Behavior
Tree reference to `Assets/Scripts/AI/Enemy_M_GR_01_PatrolOrbitShoot.asset` and that
asset's GUID. Existing Ally and boss behavior settings are preserved during Ctx migration. Context-bound Sensor, StateHub,
WeaponSystem, NavigationAgent, MovementMotor and optional FieldAllyMember are the
reference hubs; peers may live on root or nested actor children.

The Subtree declares Graph-scoped `Ctx`, `CurrentTarget`, `InCombat` and
`CombatState` (`SharedVariable<AICombatState>`). Bind every P0 task's
`combatState` to that variable; bind target consumers to `CurrentTarget`.
`CanAct` initializes per-tree/per-actor runtime data. Never reuse a mutable state
value across actors or reset it on each Repeater cycle.

Priorities are CanAct -> Recovery -> Combat -> Investigate/Search -> Patrol/Wait.
The visible Combat sequence is range 5-7 m -> orbit 2.5 s -> CanShootTarget ->
AiShoot 1.5 s -> rest 0.75 s. Its alternative finds/moves to a firing point using
the perceived aim position. `CanShootTarget.allowReloadCycle=true` and
`checkAimAngle=false` let AiShoot perform aiming and bounded reloads.

Bind `combatState` on the example's `TargetOrbitNavMesh` and `Patrol` to activate
their P0 watchdog; leave it unbound on existing graphs. Keep orbit `moveSpeed=0`
and `repathInterval=0.25`. Search defaults are observation age 10 s, shared budget
8 s, radius 3 m and 3 points; Recovery allows 2 attempts and waits 0.5 s after
exhaustion. Configure Both aborts at autonomy and Combat boundaries, Self on
visible orbit, and priority interruption of Search. Do not configure ammo or
temporary shoot readiness as a reload-cancelling conditional abort.

The explicit **Tools > RB > AI > Apply P0 to M_GR_01** command rebuilds only this
example's Subtree using native serialization. It does not run on import. Reopen
the asset after authoring and inspect Graph variable bindings and prefab Subgraph.
See `SYSTEMS/AI_AND_TARGETING.md` for contracts and `VALIDATION.md` for results.

## Animation presentation layers

CharacterAnimBrain retains the serialized locomotion/action indices (normally 0/1)
and reserves the next index above both for full-body actions (normally 2). Other
systems must not use that reserved layer. UpperBodyMask stays on the upper-body
action layer; CommanBlock plays on the separate full-body layer. No extra prefab
component or per-animation list is required. `actionFadeIn`/`actionFadeOut` apply
to both action layers, while locomotion transitions retain their own fade settings.
Set CommanBlock's End Time to the desired completion point: its movement/rotation
lock ends there and the remaining pose fades out. Pair Offset corrections blend
per upper-body state and are suppressed by full-body action coverage.

Keep Driver/Brain execution orders at **1000/1100**; movement writers must run
before Driver.Update. Pair offsets restore before Animancer evaluates and apply
once afterward, including manual evaluation. New internal animation modules do
not require Inspector bindings. Keep `CharacteContext.AnimDriver`/`AnimBrain` and
the Brain's explicit Animancer reference correct for root and nested model layouts.
After rebuilding a rig, invalidate the animation binding through Driver. Explicit
invalidation also covers a changed avatar/controller on the same Animator.
Multiple fallback Animancer candidates produce a warning and require an explicit
reference. Existing clips, profiles, fade values and authored End Times remain in use;
no Transition Library or asset migration is required.

This project relies on prefab-authored context blocks plus runtime reference
resolution. Use this document when creating or editing character prefabs,
pickup prefabs, weapon data, or inspector-facing fields.

## Spacebar Block HUD

`Assets/Spain/UI/Block HUD/SpacebarBlockHUD.prefab` combines the seven source
sprites in that folder as centered uGUI Image children. Place the prefab under
an existing Canvas and resize or scale the root RectTransform as a single unit.
The source images share a 1769 x 1075 canvas and must keep their full image size
and center alignment. Their hierarchy order controls the artwork: both `Base`
layers sit behind the three `Power Gauge` layers, then `Text`, with `Text_2`
on top. All images have Raycast Target disabled because this is display art.

## Ammo HUD

`Assets/Spain/UI/Ammo HUD/AmmoHUD.prefab` combines the seven transparent source
sprites on their shared 976 x 598 canvas. Place it under an existing Canvas.
`CurrentAmmoText` and `ReserveAmmoText` are TextMeshPro placeholders set to
`999`; bind these two fields to the current and reserve ammo counts in the HUD
controller when wiring gameplay. Keep the image children center-aligned at
their native size so the orange frame, black panels, and bullet layers match.
All visual elements have Raycast Target disabled.

## HP and Stamina Bar

`Assets/Spain/UI/HpStamina HUD/HpStaminaBar.prefab` combines seven source
sprites on their shared 745 x 1002 canvas. Place it under an existing Canvas
and scale the root RectTransform as one unit. The stamina artwork sits behind
the HP artwork; the HP frame overlays its orange fill. `CurrentHPText` and
`MaxHPText` are TextMeshPro placeholders set to `999` and should be bound to
the character's live HP values when gameplay is connected. The HP and stamina
fill sprites currently show the reference image at full fill; they do not
change with gameplay until a HUD controller drives them. All visual elements
have Raycast Target disabled.

## Ally Status HUD

`Assets/Spain/UI/AllyStatus HUD/AllyStatusHUD.prefab` combines eight source
sprites on their shared 1196 x 755 canvas. Place it under an existing Canvas.
`PortraitMask/PortraitIcon` is the replaceable character icon slot. It uses
`Assets/Spain/Charactor_Icon/Aires_Icon.png` as the initial sprite; assign
another character sprite to `PortraitIcon` when showing a different ally.
Keep `PortraitIcon` inside `PortraitMask` so the image stays within the angled
black portrait area. The HP and stamina bars show the reference artwork at
full fill until gameplay code drives their progress. All images have Raycast
Target disabled.

## Combo HUD

`Assets/Spain/UI/FollowUp_Hud/ComboHUD.prefab` layers `Combo_Color` behind
`Combo_BG` on their shared 1920 x 1080 canvas. Place it under an existing
Canvas. The large `E` input prompt is part of the background sprite.
`Assets/Spain/UI/FollowUp_Hud/ComboAllyIconCombo.prefab` is the separate ally icon
panel: `Combo_AllyIconCombo_Color` sits behind `Combo_AllyIconCombo_Base` on
their shared 585 x 668 canvas. `PortraitMask/PortraitIcon` clips the character
image inside the angled black panel, with `Aires_Icon` assigned initially.
Change the sprite on `PortraitIcon` for another ally, and keep it under
`PortraitMask` so the panel border remains visible.
All images in both prefabs have Raycast Target disabled.

`Assets/Spain/UI/FollowUp_Hud/ComboHUDCombined.prefab` is the composed layout
shown in the reference image. It nests one `ComboHUD` and three
`ComboAllyIconCombo` instances named `AllyIconSmall`, `AllyIconMedium`, and
`AllyIconLarge`. Each instance overrides its `PortraitMask/PortraitIcon`
sprite to `Roma_Icon` and can be changed independently. The source prefabs
remain reusable on their own; edit the combined prefab when authoring the
three-icon composition. Each ally card includes a `Remaining` TMP label for
the live opportunity countdown.

## Skill HUD

`Assets/Spain/UI/SkillHud/SkillHUD.prefab` combines the six source sprites on
their shared 953 x 480 canvas. Place the single prefab under an existing
Canvas. `BG_Skill` is the common base image. `UnavailableState` is active by
default and overlays the dark progress artwork and gray `SKILL` text.
`ReadyState` contains the orange progress artwork and white text with orange
accents. Activate exactly one of these state groups to show whether the skill
is ready. The prefab contains visuals only; the HUD controller must toggle
the groups when the skill state changes. Keep the images center-aligned at
their native size. All images have Raycast Target disabled.

## Gameplay HUD

`Assets/Spain/UI/GameplayHUD.prefab` is the full-screen composition from the
HUD layout reference. Place it under a Canvas that uses a 1920 x 1080
reference resolution with `Scale With Screen Size`. Its root stretches to
the Canvas; the child elements use screen-edge anchors so their positions
follow the corresponding corners when the viewport changes.

The left side contains three `AllyStatusHUD` instances (`AllyTop`,
`AllyMiddle`, `AllyBottom`) and `PlayerHpStamina`. The top and bottom ally
portraits initially use Aires, and the middle uses Dorothy. The right side
contains `FollowUpDisplay`, `AmmoDisplay`, and a circular
`MinimapPlaceholder`. The lower right follows the reference stack: three
dark cards (`SkillCardBack`, `SkillCardMiddle`, `SkillCardFront`) sit behind
one orange `SkillReady` instance of `SkillHUD`. The cards use
`SkillCardFrame.png` and `MilanoSkillSilhouette.png`, a monochrome derivative
of the existing `MilanoSkill_2_Icon` artwork. `ReadySkillIllustration` uses
the same silhouette in white and is clipped by the ready HUD base. Replace
these image sprites when showing other character skills. Four small images
form the center crosshair. All new images have Raycast Target disabled.

`PlayerUI.prefab` and the active `PlayerUI_PartyHud.prefab` assign this prefab
to `PlayerUIRuntimeBinder`. When the party spawns, the binder adds `GameplayHUD` under the existing `UI_Manager`
Canvas and binds `GameplayHudPresenter` to the spawned player. The presenter
shows player and party HP, stamina, portraits, magazine and reserve ammo,
slot 0 skill readiness, and the combo display while an offer is available.
The player's diagonal HP and stamina artwork uses `SlantedBarFillEffect` at
runtime so depletion follows the angle of each painted bar. Keep those two
fill images as `Simple` images and preserve their original sprite alignment.
The three ally panels use PartySlot1, PartySlot2, and the active Helper in that
order. Missing actors hide their panels. The legacy player vital, ammo, HP,
stamina, and charge visuals, along with the older PartyHUD status and skill
panels, are hidden to avoid duplicate readouts. On `PlayerUI_PartyHud`, the
binder redirects `PartyHudPresenter` to the new `FollowUpDisplay` and hides both
the old `PartyHUD/FollowUpOffers` and legacy `PlayerHUD/PartyFollowUpHud` views.
The existing `PartyHudInputRouter` remains active: E consumes the first valid
offer, ordered by earliest expiry then offer ID, matching the displayed cards
from `AllyIconSmall` (beside E) through Medium to Large. Portraits use the
offer owner's current character icon; `Remaining` uses the controller's
pause-aware clock. Only eligible offers with charges show a card; empty cards
and an empty display are hidden. Do not independently toggle `FollowUpDisplay`
from another presenter. Keep each card's `PortraitMask/PortraitIcon` Image
and `Remaining` TMP label intact. An incomplete replacement retains the old
view. UI prefabs without `PartyHudPresenter` retain their legacy Combo UI.
Command, energy, cast feedback, and fullscreen effects retain their existing
controllers.
The circular minimap remains placeholder art until a map source is authored.
The runtime instance inherits the current `UI_Manager` CanvasScaler, which
uses Constant Pixel Size, so check this composition at each target resolution.

`GameplayHudAimLayout` reads the bound player's `WeaponSystem.IsAiming` and active
Cooperative Block session each frame. While aiming or in Block Struggling/Success,
only Player Status (`PlayerHpStamina`) moves, to
bottom-left Canvas coordinates (380, 240), beside the ally column and left
of the character. It keeps its authored size and rotation. The ally panels,
skill cards, ammo, Combo, and reticle retain their authored transforms.
Transitions take 0.18 seconds using unscaled time, including when reversing
Aim mid-transition. It restores the captured authored position and anchors when
neither Aim nor Block Struggling/Success is active, or when the layout is disabled,
without accumulating offsets on rebind. Ending Block while still aiming keeps the Aim position.
The component's inspector fields tune the Aim layout; positions are in Canvas
units. `GameplayHudPresenter.Bind` adds the component if it is missing. Keep
`PlayerHpStamina` directly under `GameplayHUD`, and author its normal pose
outside Play mode. Validate the Player Status position and unchanged surrounding
panels at 1280 x 720 and 1920 x 1080 after changing these settings.

## Character Prefab Context

### Player system groups

`Assets/Prefab/Player/Player.prefab` has six direct children under
`------------ Player_System ------------`:

| Group | Responsibilities |
| --- | --- |
| `GamePlayStats_System` | Health, Stamina, Level, CombatEventBus, StatsHub, StateHub/debug view, StatusEffects and Passives |
| `Movement_System` | Movement, Dash, Knockback, VerticalMotor, PlayerInput/handler and interaction |
| `Combat_System` | Weapon/affix/upgrade, Melee, SkillManager/SkillUser/progression, aim/targeting, interruption and defensive block/cue |
| `CharacterVisual_System` | Visual/model binding, visibility, status/cutscene presentation, Animancer, animation Brain/Driver, pair offsets and aim rig |
| `Character_Equipment` | Equipment, accessories and inventory |
| `Party_System` | Character loader, formation, commands, helper, field allies, chains and follow-up controller/executor |

The root retains Transform, CharacterController, Rigidbody, Animator,
PlayerContext, AITargetInfo, AudioListener and CharacterMovementMotor.
Aim Target and PlayerVisual remain actor anchors; FirePoint belongs to the weapon model. The root Animator is retained;
this organization change does not remove or retarget animation components.

Keep group transforms at local identity. Important peer references stay explicitly
bound on PlayerContext, including `inputHandler` and `interruptionController`.
PlayerInput and its handler share Movement_System; preserve all 18 authored
UnityEvent callbacks. StateHubDebugView shares StateHub's object; the FollowUp
controller/executor share Party_System; the animation Brain shares Animancer's
object. CharacterVisualController.ModelRoot still points to PlayerVisual.

Resolve pickups, reticle, party interruption and chain input suspension through
the actor context rather than assuming these modules are on the root. StatsHub
discovers modifier providers across its owning actor, including the sibling
Combat_System and Character_Equipment groups, while excluding other actors.

Grouping validation: `PlayerPrefabGrouping.Tests` (Edit Mode) and
`PlayerPrefabGrouping.PlayMode.Tests` (Play Mode). Also run the existing
StateHub locomotion and UnifiedWeaponPassives production-player regressions.

### Ally and Enemy system groups

`Assets/Prefab/Player/Ally_Helper.prefab`, `Ally_Stryker.prefab` and
`Assets/Prefab/GameEnemy/Enemy_Base.prefab` use six direct groups under
`------------ Ally_System ------------` or `------------ Enemy_System ------------`.
The four GameEnemy variants inherit these groups from Enemy_Base.

Variant EnemyLevelSystem and the existing SpecialShootPointController modules
remain added-component overrides on the inherited stats/combat hosts. Their
component file IDs and original property overrides are retained. Unity's native
component-move command rejects restructuring these prefab instances; the one-time
migration is archived outside Assets, with its serialized-data audit. Keep the
variants linked to Enemy_Base when editing their systems.

| Group | Responsibilities |
| --- | --- |
| `GamePlayStats_System` | Health/EnemyHealth, Stamina, Level, CombatEventBus, StatsHub, StateHub/debug view, StatusEffects and Passives; Enemy also owns StaggerMeter, EnemyDropper and variant EnemyLevelSystem |
| `Movement_System` | AgentMoveDriver, RootMotionNavMeshDriver, Dash, Knockback and VerticalMotor; Ally also owns Interactor/InteractableLink |
| `Combat_System` | Weapon/affix/upgrade, SkillManager/SkillUser/progression, Melee, DefensiveBlock, PreCastBlock and variant SpecialShootPointController |
| `CharacterVisual_System` | Visual/model binding, animation Brain/Driver, Animancer, pair offsets, aim rig, visibility and status/cutscene presentation |
| `Character_Equipment` | Equipment and existing accessories |
| `AI_System` | BehaviorTree, GameObjectSharedVariables and their authoring layers, target sensor and aim driver; Ally also owns the party loader, FieldAllyMember, ChainSkillUserProxy and interruption |

Move only modules already authored on that prefab. Ally retains eight root
components: Transform, AllyContext, NavMeshAgent, CharacterController, Rigidbody,
Animator, CharacterMovementMotor and AITargetInfo. Enemy retains the corresponding
seven without CharacterController. Keep model roots, HelperAim, HitTaget,
colliders and other positional anchors at their authored transforms; system groups
use local identity. `AI Ally/Ally.prefab` and `Ally_Base.prefab` are separate layouts.

Bind important systems through the existing context. Agent movement and Stagger
read sibling state/animation systems through that owner; Helper uses
`AllyContext.BehaviorTree`, and EnemyLevelSystem notifies its owner's StatsHub even
while inactive. EnemyDropper uses the context position and enemy level. Keep AI
graph data, task GUIDs, Ctx bindings and the shared-variable layer chain intact;
an inherited layer plus its variant override layer is intentional.

The production DefensiveBlock command reuses context-bound guards/attacks and adds
missing ones in Combat_System. TestStage authoring reads the context-bound Weapon,
Dropper and EnemyLevelSystem, creating a missing enemy level in GamePlayStats_System.
Do not add copies to the actor root after grouping.

Grouping regression assemblies: `AllyEnemyPrefabGrouping.Tests` and
`AllyEnemyPrefabGrouping.PlayMode.Tests`. Run AI targeting, movement, party commands,
DefensiveBlock, SpecialShootPoint and shared Player regressions as well.
See `VALIDATION.md` for completed runs and the remaining unrelated party/HUD
authoring failures. Enemy_Base still uses the original Mons_GR01_MaleData model
definition, which instantiates a complete M_GR_01 actor beneath VisualModel;
the grouping migration preserves this existing model choice.

### Player Crouch / Slide

PlayerInput uses `Inputmaneger.inputactions`, `InvokeUnityEvents`, and a Crouch
event bound to `PlayerInputHandler.OnCrouch`. `<Keyboard>/ctrl` accepts either Ctrl.
Retain this event on derived player prefabs. PlayerMovementCC exposes Crouch and
Momentum Slide Inspector settings; its runtime capsule snapshot uses `ctx.cc` and
preserves authored foot placement. Do not resize model/hit-zone colliders to
compensate for missing animation. Assign optional in-place looping `crouchIdle`,
`crouchMove`, and `slide` transitions on CharacterAnimProfileSO, or leave empty for
ordinary locomotion fallback. Verify upper-body aim/fire masks with final clips;
Roma uses `Assets/Animation/Ch_Roma_Anim/Roma_Slide.fbx` for Slide (looping,
speed 1, fade 0.1 seconds). Crouch idle/move still use fallback until authored.
Ctrl may be pressed during grounded Dash to hand its current momentum to Slide;
waiting for Dash to finish or holding Shift until Sprint is no longer necessary.
Keep the animation profile's Reload Body Mode at **UpperBody** (as on Roma) to
reload while sliding. Reload and Slide can start in either order without canceling
each other. FullBody Reload retains its authored exclusive movement lock.

Set each CharacterStats asset's **Base Stats > Slide Stamina / Unit**
(`slideStaminaPerUnit`) independently. Default is 2 stamina per world unit of actual
planar Slide movement; 0 disables the cost. For example, 5 units costs 10 stamina
at rate 2, applied gradually each movement frame. The value comes from the actor's
current `ctx.baseStats`, not from the Player prefab or DashSetting. Ensure its
context resolves StaminaSystem. Paid Slide stops at empty stamina; regeneration
pauses during Slide and resumes after the usual spending delay. No extra entry fee.

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

### AI target sensor authoring

Each sensor must be on or below its owning `CharacteContext`. Use one sensor per
actor, or an explicit Ally/Summon context binding; an ambiguous lookup returns no
sensor. Bind health, state, TargetInfo and StatusEffects on the context. Keep local
sensor-origin, forward-reference and aim-point Transform bindings.

Target masks now apply to the **context GameObject layer**, not hitbox/body layers.
Enemy sensors normally include Player and Ally; Ally/Summon sensors include Enemy.
The shared faction rule also requires opposing identities, so broad masks do not
make props or friendly actors eligible. Colliderless actors remain discoverable.

The ten existing sensor prefabs use obstruction mask
`Default | Ground | Ground Y | Terrain` (151553) and ignore triggers. A mask of zero
with LOS enabled is an authoring issue. The sight query ignores the owning actor
and accepts the target's own colliders. Check authored aim height against actual
cover geometry; an assigned aim point remains authoritative.

Run **Tools > RB > AI > Validate Target Sensors** to inspect prefabs and loaded
scenes for missing ownership, empty sight masks and target masks excluding opposing
actor layers. `maxHits` and `tauntScoreBonus` remain serialized for compatibility,
but no longer limit discovery or override Taunt precedence respectively.

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
variants at runtime. Keep local authoring references such as animation, model, hitbox, visibility, and
teleport-probe fields on the rig; they are not replaceable by context lookup.

Runtime Helper placement uses deterministic candidate rings through
`CharacterPlacementResolver`, including the targeted delivery ring and the fallback ring around
the player. For a targeted skill, the cached animation trajectory and cast-point target impact are
part of that preflight, and the eventual Helper skill playback must enable planar root motion. The
resolver sweeps between animation samples and treats actor-context colliders as actor overlap even
when masks overlap. The Helper is mobile, so a valid NavMesh footprint is required; if no candidate
passes, the summon is rejected before activation or cast rather than using an unvalidated position.

## Character Visibility Authoring

Any character that is hidden, faded, or teleported by gameplay — the Helper, field
allies in a chain attack, an interrupting ally or the Player — needs
`CharacterVisibilityController` **on the character root**, next to its context, and
a visual model that can actually be dithered.

On the character root:

- `CharacterVisibilityController`. Leave its `characterVfx` field empty:
  `CharacterVisualController` rebinds it to the live model on every build, form
  override, and form restore, so a value serialized here is stale the moment the
  model changes. `ctx.Visibility` resolves it self/parent/child like every other
  context reference.
- Timing is authored per character: appear/disappear duration and curve, and
  `useUnscaledTime` (on by default, so fades still run during hit-lag, world slow,
  and dialogue pauses).
- `disableShadowsWhileHidden` is off by default. Turn it on only if a fully hidden
  character is still seen casting a shadow — the dither runs in the shadow pass, so
  it should not be needed.

### Face shadow (SDF) authoring

A character's **face mesh only** takes the `ZLZ_Std_Char_Face` preset; every other mesh takes
`ZLZ_Std_Char_Body`. The Face preset turns on face mode and reads `_FaceTex`, the SDF that decides
which half of the face is in shadow for a given light angle.

**`_FaceTex` must be two-channel.** `ZLZ_FaceShadow.hlsl` samples `.r` when the main light is on the
head's left and `.g` when it is on the right, and ZLZ's own `T_FaceShadow` satisfies
`G(x) == R(width-1-x)` exactly. A single-channel SDF — which is what ASP used, because ASP mirrored
it in the shader — leaves `.g` at 0, so the whole face goes to shadow the moment the light crosses to
one side. Convert one to the other by writing **R = horizontally mirrored source, G = source**
(`Assets/Character/Roma/Roma_FaceSDF_ZLZ.png` is the worked example).

Import the SDF **sRGB off** and **uncompressed**: the texel is an angle threshold compared against a
linear 0–1 value, and block compression bands the shadow terminator.

**Check the face UV layout before choosing where the SDF comes from.** ZLZ's documented workflow is
to reuse the shipped `T_FaceShadow` on every character and align it with the UV controls — that
assumes a face UV spanning the full 0–1 range, which is what their VRoid demo character has. Three of
this project's characters (**Roma, Dorothy, Milano**) instead use a *mirrored* face UV: one island
covering half the U range serves both halves of the face (measured — 215 of 225 mirrored vertex pairs
on Roma share a UV). The shared texture cannot be fitted to those with a single uniform scale plus an
XY offset, and the whole face renders shadowed at every setting. Those characters need their own
two-channel SDF baked against their own UV. The remaining characters span the full U range and can
take the shared-texture route.

Where a character does have its own baked SDF, it already matches its UV, so `_FaceShadowUVScale`,
`_FaceShadowUVOffsetX/Y`, and `_FlipUvFace` normally stay at their defaults.

To align a shared texture, switch `_DebugUvFace` on. It renders `float3(step(0.5, FaceUV), 0)`, so a
correctly aligned face shows **four quadrants meeting at the middle of the face** — green top-left,
yellow top-right, black bottom-left, red bottom-right. The face UV transform is
`FaceUV = (uv + offset - 0.5) * scale + 0.5`, so the quadrant cross sits wherever `uv = 0.5 - offset`:
**scale does not move the cross** (it pivots on it), it only controls how far the SDF spreads over the
face. Set the offsets first to centre the cross, then set scale. Switch the debug back off before
committing.

A character whose face is a single flat colour in this view has a mirrored face UV and cannot be
aligned at all — see above.

Face shadow needs `ZLZ_HeadDirectionBinder` with calibrated axes; without it the shader has no head
direction and the shadow never moves.

On the character **visual** prefab (under `ModelRoot`):

- `ZLZ_CharacterVFX` with **Dither enabled** and a valid `ZLZ_DitherSettings`
  asset. Dither disabled makes `SetInstant` a silent no-op: gameplay believes the
  character is hidden while it is still fully drawn. The controller logs one
  warning for this, not one per frame.
- `ZLZ_HeadDirectionBinder` with its head bone assigned and forward/right axes
  calibrated (Character Dashboard → Auto Detect). Every rig in this project names the
  head bone `head.x`; do not pick up a `HitZone_Head` / `Head.HitZone` collider by mistake.
- `ZLZ_CharacterDashboard` — the authoring panel that owns `headBone` (it syncs the value
  down to the binder in `OnValidate`) and hosts the Smooth Normal Bake, Selection Outline,
  Contact Shadow, and Tone Mapping setup sections. It is editor-only: no `Awake`, `Start`,
  or `Update`, so it costs nothing at runtime. Every prefab under `Prefab/Charactor` has one.
- ZLZ materials on every renderer that should take part; a renderer whose material
  has no `_DitherAlpha` is skipped silently.

The controller never disables the actor. Sequence owners subscribe to `Disappeared`
and do the `SetActive(false)` themselves — that is where helper protection rollback
and chain-attack cleanup hang off. Nothing carries a `deactivateTarget` any more.

### Wired prefabs

| prefab | controller sits on | appear | disappear |
|---|---|---:|---:|
| `Prefab/Player/Player.prefab` | root, same object as `PlayerContext` | 0.18s | 0.18s |
| `Prefab/Player/Ally_Stryker.prefab` | root, same object as `AllyContext` | 0.18s | 0.18s |
| `Prefab/Player/Ally_Helper.prefab` | root, same object as `AllyContext` | **0.40s** | **0.40s** |
| `Prefab/Player/SummonBase.prefab` | root, same object as `SummonContext` | 0.18s | 0.18s |

The Helper's 0.40s is deliberate — it preserves the fade speed it had before the ZLZ
migration; the other three use the controller's 0.18s default.

### Weapons fade with the body

A mounted weapon is part of the character as far as visibility is concerned, so **every weapon
material a character can hold must use a shader that has `_DitherAlpha`** — in practice
`ZLZ/AnimeToon/Character`. `Toon/TC_CustomToonOutline` does not have it, so a weapon on that shader
stays fully solid while its owner fades out. The materials behind every `GunConfig.WeaponPrefab`
(`SubmachineGun_01/02`, `HeavyMachineGun_01`, `Sniper_01`, `Pistol_01`, `M_SMG_03`) are on the ZLZ
shader for exactly this reason; keep new ones there too.

`ZLZ_CharacterVFX` caches its renderer list, so a weapon mounted *after* the model was bound is
invisible to the dither until the list is rebuilt. `CharacterVisualController.BuildModelFromWeaponDef`
therefore calls `ctx.Visibility.RefreshVisualRenderers()` once after both hands are settled — on
every path, including the ones that only destroy a weapon or fail to find a hand bone. The order that
matters is:

```
build / bind character model → mount weapons → refresh the ZLZ renderer list → re-apply the alpha
```

`RefreshVisualRenderers()` re-applies the current alpha in the same call because
`ZLZ_CharacterVFX.RefreshRenderers()` rebuilds its material instances and resets the dither to
visible — without that, swapping a weapon while the character is hidden pops it on screen for a
frame. Call it only when the weapon hierarchy actually changes, never per frame.

`CharacteContext.Visibility` and `FieldAllyMember.actorVisibility` are **bound in the prefab**
even though both resolve at runtime as a fallback. The serialized reference makes the prefab
checkable by eye and stops the wiring depending on a hierarchy search — the visual stack lives
under a `CharacterVisual_System` child while the controller sits on the root, so a one-direction
lookup is not something to rely on.

## Character Rendering Pipeline

ZLZ shaders now reside in `Assets/ZLZ/AnimeShader/Shaders`, alongside the shared
`Assets/ZLZ/Common` folder. The remaining legacy `Shaders` folder was moved with
its `.meta` file after closing Unity because the Editor move returned Access denied.
All 39 contained files and the folder metadata were verified unchanged. This layout
resolves the character shader's relative include of `Common/Shaders/ZLZ_Lighting.hlsl`.

On Unity 6000.0, the ZLZ material Inspector uses `MaterialProperty.flags` and
`MaterialProperty.PropFlags.HideInInspector`. The package's `propertyFlags` branch
does not compile on 6000.0.58f2; retain this compatibility fix when updating ZLZ.

`ZLZ_SelectionController.RefreshRenderers()` is a project integration method restored
after the package update. It re-scans child renderers, including inactive children.
`CharacterSelectable` calls it before updating selection so swapped models and weapon
previews attached after `Awake` participate in the outline. Preserve this method when
updating ZLZ; the stock package may not provide it.

The local Forward+ patch from commit `95b3834e` was reverted in the character shader
and `ZLZ_MainLighting.hlsl`. Both files now use their pre-patch versions. Additional
lighting uses the original per-object loop; the project's custom Forward+ support
is no longer present. Backups of the patched files are outside Assets in
`P:/Game_RB_Project/ZLZ_Restore_Backup_20260915`.

Character shading is ZLZ (`ZLZ/AnimeToon/Character`). The render pipeline assets are
**project-owned** — never the copies inside `Assets/Plugins/ASP`, which are read-only:

| what | asset |
|---|---|
| Quality level `PC` | `Assets/Settings/PC_RPAsset.asset` |
| Graphics Settings default | `Assets/Settings/PC_RPAsset.asset` |
| PC renderer | `Assets/Settings/PC_Renderer.asset` |
| Quality level `Mobile` | `Assets/Settings/Mobile_RPAsset.asset` |

Renderer features on `PC_Renderer`: `ZLZ_AnimeToneMappingFeature` and `ZLZ Hull Outline` are on;
`ZLZ Character Contact Shadow`, `ZLZ Screen Space Outline`, and `ZLZ Selection Outline` are present
but off, and `ScreenSpaceAmbientOcclusion` is off.

**Character renderers must claim rendering layer bit 0 only.** `ZLZ_SelectionOutlineFeature` filters
on bits **1 (Character), 2 (Enemy), 3 (Item)**, and `ZLZ_SelectionController` is what sets those bits
when something is actually selected. A renderer authored with `renderingLayerMask = 4294967295` (all
32 bits) matches all three types permanently, so the outline is drawn on it constantly no matter what
the controller does. Every prefab under `Prefab/Charactor`, `Prefab/EnemyVisual`, and `Prefab/Player`
has been normalised to bit 0; keep new character prefabs that way.

Bit 0 is what every gameplay light uses, and the dialogue stage's bit 5 is applied to clones at
runtime by `DialogueActorCloneFactory`, so nothing else depends on those extra bits. The Hull Outline
feature does not filter by rendering layer, so narrowing the mask does not affect it.

Note that the Character Dashboard's **Setup Selection Outline** button switches the renderer feature
on for the whole project, not just that character.

**Only one tone-mapping feature may ever be active.** Running ASP Tone Mapping and
`ZLZ_AnimeToneMappingFeature` on the same renderer crushes every ZLZ character to near-black with
neon-saturated highlights — it looks like broken materials, but it is the double tone-map.

Turning on `ZLZ Character Contact Shadow` or `ZLZ Screen Space Outline` means setting their
`casterLayers` / `characterLayers` masks to the character Unity layer. Both filter by Unity layer,
which is also why dialogue clones stay on layer 0 — see **Dialogue presentation**, and
`DialogueAuthoringValidator` reports it if either mask stops covering that layer.

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

- `ctx`, `characterController`, `colliderRefs`. `characterController`
  is optional and stays empty on actors that have none (all Enemy prefabs).
  `colliderRefs` is normally resolved from `ctx` and rebound whenever
  `CharacterVisualController` swaps the model.
- `capsuleCollider` is an **optional explicit override** for the body shape. Leave
  it empty unless an actor genuinely needs a body different from
  `CharacterColliderRefs.CharacterPositionCollider`. It is never auto-filled by a
  hierarchy search any more, because that returned a hit-zone capsule on several
  characters.
- `navMeshAgent` on `AgentDriven` actors resolves through `ctx.NavigationAgent`.
  Suspension uses `ctx.MovementMotor` with or without an `AgentMoveDriver`.
- Gravity reads root-motion Y ownership from `ctx.MovementMotor`. The vertical
  motor's old `animBrain`, `agentMoveDriver`, `rootMotionCCDriver` and
  `rootMotionNavMeshDriver` fields and `BindRuntimeRootMotionDriver` methods have
  been removed. Root-motion adapters register Y ownership directly with the
  shared motor. The nine prefabs containing these fields were migrated through
  Unity; there are no remaining overrides for these field names in project assets.
- `groundMask` and `collisionMask` — production prefabs use
  `Default | Ground | Ground Y | Terrain`, because scene ground geometry is not
  consistently on the `Ground` layer.

A prefab without the component behaves exactly as it did before the motor existed:
`StateHub.IsGrounded` reports grounded, and nothing applies gravity. The
`Assets/Test/Summoning` fixtures are intentionally left without it.

Do not add a second system that writes Y. Anything that needs to move a character
vertically should call `Launch` / `AddVerticalVelocity`, or hold a gravity-suspend
token via `AcquireGravitySuspendToken` for levitate-style effects.

### Dash Must Keep World Collision

`DashSetting_Player.dashIFrameExclude` must leave `Default`, `Ground`, `Ground Y`,
and `Terrain` out of the excluded layers. `DashSystem` applies this mask to both
the Rigidbody and CharacterController while dashing, and the vertical motor
continues applying gravity. Excluding a floor's layer lets the character sink
through it even when the ground probe still detects it. The defensive-block test
arena's `Arena Floor`, for example, uses `Default`.

The player asset excludes mask `34814` (previously `34815`, which also excluded
`Default`). Damage invulnerability still comes from the health invincibility token.
When checking dash authoring, test a full dash on each supported floor layer and
confirm that the character stays supported while character pass-through remains
available.

`KnockbackData.VerticalImpulse` (default `0`) is the authoring hook for launching
knockbacks: leave it at zero for a purely planar knockback, exactly as before.

### Displacement Without A CharacterController

`CharacterMovementMotor` applies the shared displacement rule for walking, dash,
root motion, knockback, gravity, companion separation and defensive approach:

```text
enabled CharacterController -> CharacterController.Move()
no CharacterController      -> position-collider sweep + actor Transform move
disabled CharacterController -> no displacement
```

The no-controller path is used by every Enemy. It resolves the actor body from
`ctx.ColliderRefs.CharacterPositionCollider` and probes it through the shared
`CharacterBodySweepUtility`, which supports `CapsuleCollider`, `BoxCollider`, and
`SphereCollider`, honours the collider's scale/centre/rotation, ignores only
colliders owned by the actor context (not siblings under a shared room root), and
allocates nothing per frame. The motor then moves
only by the safe displacement and syncs `NavMeshAgent.nextPosition`.

Knockback retains its existing NavMesh-agent-cylinder fallback. Other missing-body
movement stops and warns once; gravity no longer falls back to an unswept move.
Root motion slides at collisions; knockback and gravity stop at the hit distance.
Keep each behaviour's existing mask, padding and trigger settings. Controller
movement continues to use the controller's collision settings.

The context creates one `CharacterMovementMotor` through `EnsureMovementMotor()`
at runtime on its own object.
Existing prefabs need no mass migration. It may also be authored on the context
object; do not put it on a replaceable model. Ordinary context inspection in Edit
Mode and `CharacterMovementMotor.Resolve` do not create components. Standalone
setups without a context must author their motor explicitly and bind it during
initialization. `Move(delta, settings)` and `PlaceOnNavMesh(position)` use that
binding; per-call body/root overloads were removed. Placement is reserved for positions already
validated by landing/recovery logic, not ordinary walking or animation motion.

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

## NPC Station Presentation

Add `NpcPresentationTarget` to the same NPC object that owns its
`ShopInteractable` (or another station interactable). The component authors the
camera relative to the real NPC in the map; it does not move, rotate, or clone
the NPC. Use `Framing Root` only when the interactable transform is not a stable
camera reference.

The Abbygail shop defaults are calibrated for the station layout shown in Game
View:

- camera local offset `(0, 1.55, 5.8)`
- look local offset `(-1.55, 1.35, 0)` and FOV `34`
- NPC framed on the left, approximately head-to-knee
- existing Shop UI anchored on the right at `72%` Safe Area width with a
  `24`-pixel margin

Keep the target on all three station prefabs:

- `Assets/Prefab/Station/AbbyGail_Shop_Tire_1.prefab`
- `Assets/Prefab/Station/AbbyGail_Shop_Tire_2.prefab`
- `Assets/Prefab/Station/AbbyGail_Shop_Tire_3.prefab`

To inspect the composition without entering Play Mode, select the NPC object
with `NpcPresentationTarget` and click **Open Camera Preview** in its Inspector.
The preview window uses a real Unity Camera and a 16:9 render texture with the
authored position, rotation, and FOV. A dark guide marks the runtime Shop UI
area, and inspector changes update the render live. Closing the window destroys
the temporary preview camera and does not modify the scene or prefab.

Runtime presentation resets the active Cinemachine brain while the screen is
black, so it cuts directly to the authored pose instead of exposing an in-flight
camera blend. Its lens also overrides physical-camera projection with standard
perspective projection, matching the Camera Preview even when the gameplay
camera uses a physical sensor or gate fit.

`ShopInteractable` allows presentation when no map run is active, when the run
has no current room, or when `MapRunController.CurrentRoom.RoomCleared` is true.
An uncleared active room shows `Clear enemies first` and does not open the Shop.
Keep purchase/catalog/inventory behavior in `ShopPanelUI`; presentation owns
only the transition, camera, temporary layout, control lock, and restoration.

## Map Room Spawn And Party Warp

คู่มือภาษาไทยสำหรับสร้างห้องใหม่ตั้งแต่เลือก base prefab จนลง Content Pool:
[วิธี setup Map Room Prefab](SYSTEMS/MAP_PREFAB_SETUP.md).

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

### Base Map Room Topologies

`Assets/Prefab/MAP/Base_Map` contains the 15 neutral topology prefabs used as
the starting point for new map rooms: four dead ends, two straights, four turns,
four T-junctions, and one cross. They are authoring assets only and are not
automatically registered in a `MapRunConfigSO`; create or update the appropriate
`RoomDefinitionSO` only after a room's gameplay type and encounter content are
chosen.

The Start role uses `Base_Map/Start/RoomPrefab.Base.Start.Up.prefab`. It is a
Prefab Variant of `Base_Map/DeadEnd/RoomPrefab.Base.DeadEnd.Up.prefab`, rather
than a copied sixteenth topology, so it inherits the one exit and all runtime
references. It has no baked NavMeshData; bake the finished room Variant. Create the styled Start room as a
Variant of this Start base. Runtime rotation supplies the other three Start
orientations; do not add extra exits to this base.

Every base prefab has exactly the exit sockets matching its topology, a bound
`InteractableLink` and trigger collider on the `Interactable` layer for each
exit, and an `EntranceSpawnPoint` facing inwards at every exit. Its
`RoomController` has those exit and spawn references explicitly bound. Do not
remove or rename these runtime objects when decorating the room.

Base prefabs intentionally do not contain a `Navigation` child or baked
`NavMeshData`: each room's final walkable shape is determined by its art and
collision. Add environment art under the empty `Visual` child, then author a
`Navigation`/`NavMeshSurface` on the finished room Variant and bake its data
there before registering that room in `MapRunConfigSO`. To regenerate and
verify the topology set, run **Tools > RB Project > Map > Generate Base Room
Prefabs**. To regenerate or verify the Start role base, use **Tools > RB
Project > Map > Generate Start Base Prefab** or **Validate Start Base Prefab**.

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
and bind `EnemyLevelSystem` through `EnemyContext` (GamePlayStats_System on grouped
prefabs). Do not put a fixed
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


The Sewer stage Start room is
`Assets/Prefab/MAP/Sewer/PrefabMapaGame/Sewer_Start.prefab`. It includes the
configured rig copied from `ProtoTypeMap/Start/Start.DeadEnd.Up.prefab`, with the
original `Intro_Camera_ProtoType.fbx` camera clip and all four role markers. The
rig is offset upward by 0.08 units to match the Sewer floor. Keep the
`CinemachineCamera` component enabled, and its GameObject inactive in the saved
prefab. The rig activates the GameObject during the intro; it does not enable a
disabled camera component. Replacing a Start room requires preserving this
rig; without it, `MapRunController` deliberately skips the intro. Intro playback
is attempted once per fresh run, not when revisiting Start. Saved marker paths
and physical clearance were checked at all four room rotations; the actual rig camera methods and Animancer evaluation were also checked at
0, 1, 2, and 3.5 seconds, confirming camera motion and deactivation on cleanup.
Full cinematic playback in a fresh run still requires Play Mode verification.

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

Put `WeaponMuzzleBinding` on each weapon prefab root, with its FirePoint at the
barrel tip and local +Z pointing out. CharacterVisualController binds the mounted
model's muzzle after weapon/model changes (right hand for BothHands; left for
LeftHand). The integrated turret binds its visual model directly. Do not author
a FirePoint on the actor root or set a fire-point bone/offset on the visual controller.
The upper-body rig changes the pose, never the muzzle hierarchy.

Bind `CharacteContext.WeaponAim` to the actor's ThirdPersonAimController or
AIAimTargetDriver. The weapon source must belong to that actor. Use
`Tools > RB > Weapons > Validate Weapon Muzzles` after authoring new weapons.

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

### Player-commanded Ally/Helper placement

On each `SkillGemDefinition`, open **Gameplay > Party Command Placement**:

- **Placement Mode**: `Player Target`, `Around Player`, or `In Front Of Player`.
- **No Target Placement** appears for `Player Target`: choose the per-skill
  fallback `Around Player` or `In Front Of Player`.
- **Target Distance** retains the existing stand-off setting for targeted casts;
  a payload's authored placement distance takes priority.
- **Player Distance** sets the maximum radius around the Player or the forward
  distance, including fallback placement. Default is 3 m.

Existing assets default to `Player Target` with `In Front Of Player` fallback.
Choose `Around Player` for support skills that should stay near the Player and
`In Front Of Player` for skills that should always use the Player's facing. Those
two modes ignore selected enemies. Placement does not assign a healing recipient
or change damage/buff targeting: configure the payload's recipient rules separately.
See [Skill System](SYSTEMS/SKILL_SYSTEM.md#player-directed-ally-and-helper-skills)
for in-range checks, fade timing, collision validation and helper lifecycle.

### Loadout slots

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
`hitZones` as `Head` and `Torso`. Register limb colliders as `Torso` when they
should receive normal body damage; leave a collider unmapped only when direct
weapon projectiles are intentionally meant to pass through it.

Author visual-model hurtboxes on the source visual prefab, not as Scene or Enemy
Variant additions. `CharacterVisualController` rebuilds the model from that
source at runtime. `Rector.prefab`, for example, owns its arm and leg triggers
and maps all nine of them to `Torso`.

Assign the enemy's main body/position collider to
`CharacterPositionCollider`. `EnemyHealth.Die()` disables the position collider
and every registered Hit Zone through this component.

## Enemy Body Collider

Enemy prefabs have **no `CharacterController`**. The authored
`CharacterColliderRefs.CharacterPositionCollider` is the Enemy's only body shape:
it is what knockback and vertical motion sweep against, what
`RootMotionNavMeshDriver.PushOverlappingCharacters` pushes with, and what the
interruption controllers probe.

Every Enemy visual prefab owns a `CharacterPosition` child directly under the
model root — not under an animated bone, or the body would swim with the rig:

| Visual prefab | Used by | Capsule |
|---|---|---|
| `Assets/Prefab/Charactor/Rector.prefab` | `Enemy_B_GR_01 Variant` | r `1.13`, h `4.06`, y `2.01` |
| `Assets/Prefab/Charactor/M_GR04.prefab` | `Enemy_E_GR_01 Variant` | r `0.86`, h `2.59`, y `1.26` |
| `Assets/Prefab/EnemyVisual/GR_NM01.prefab` | `Enemy_M_GR_01 Variant` | r `0.5`, h `2`, y `1` |
| `Assets/Prefab/EnemyVisual/GR_NM02.prefab` | `Enemy_M_GR_02 Variant` | r `0.5`, h `2`, y `1` |

Each capsule reproduces the `CharacterController` it replaced, so the physical
footprint is unchanged. Requirements for any new Enemy:

- exactly one resolved `CharacterColliderRefs` on the actor;
- `CharacterPositionCollider` non-null, **enabled**, **non-trigger**, and on the
  `Enemy` layer (the layer the removed controller used — `Enemy` differs from
  `Default` only in ignoring `EnemyBullet`);
- shape is `CapsuleCollider`, `BoxCollider`, or `SphereCollider`; nothing else is
  sweepable;
- it is **not** registered in `hitZones`, and Head/Torso stay trigger colliders on
  the `Hit` layer;
- it is authored inside the visual model prefab, because
  `CharacterVisualController.BindCurrentModelReferences()` rebinds
  `ctx.ColliderRefs` to the model's own component on every rebuild. A Variant
  override that repoints it survives only while `buildModelAutomatically` is off.

Do not reuse a melee hitbox, a bone collider, or a Hit Zone as the position
collider, and do not add a `CharacterController` back to an Enemy prefab.

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

## Basement Accessory screen

`Assets/Prefab/User Interface/BasementAccessoryScreen.prefab` is the runtime
screen assembled from `Assets/Spain/UI/CharacterMenuAssosry/Slor All/Slor All.prefab`.
The original artwork stays unchanged. The screen owns its Overlay Canvas,
GraphicRaycaster, CanvasGroup, BACK button, five `BasementAccessorySlotView`
bindings, accessory picker, and four letterbox bars. Keep decorative Images'
raycast targets disabled; only the five localized hit targets receive clicks.
Bind the cards in numeric Slot 1–5 order, not the source prefab's sibling order.

Each `BasementAccessorySlotView.categoryColorImages` explicitly binds the three
Images ending in `Color` under its artwork `Slot N` root (description/arrow accent,
icon background and dotted frame). Some child names have copied slot numbers;
use the owning `Slot N` hierarchy, not those embedded numbers. The Inspector
palette defaults to Offensive `#EB7E11`, Defensive `#87A63E`, Utility `#4FB5D8`;
empty/unknown uses white. Keep non-Color artwork, icons and text out of this array.
`iconBackground` identifies the solid icon frame (`BG_Icon_Color`). Its
`iconFrameLighten` defaults to 0.55, blending the category color toward white.
The dotted `Icon Fame_Color` and description/arrow accent keep the base color.

`BasementAccessoryReferenceFrame` keeps a fixed 1920×1080 design rectangle and
uniformly fits it to its parent. Do not add a LayoutGroup/AspectRatioFitter to
this rectangle or stretch the full-canvas art independently. Description masks
bound the rotated text, and the card view truncates long names with an ellipsis.
Keep the picker ScrollRect's Viewport, Content, grid and ContentSizeFitter wired.
`BasementAccessoryPickerSlot.prefab` is a SlotUI variant with its tooltip font
set to the screen's Sarabun font; ordinary inventory slots retain their defaults.

The Basement scene overrides the controller's `cameraManager`,
`characterSelection`, and `backgroundRoots` (UI, CanvaCharactorInfo, MapUI).
These are existing GameObjects, not newly added components on prefab instances.
All descendant Canvas and GraphicRaycaster enabled states are cached/restored.
`UILoadLaval` binds `accessoryScreen` and `accessoryScreenButton`; the new button
calls `OpenAccessoryScreen`. Keep the original Equipment button's callback.

`CameraManager` binds the scene's `AccessoryCamera` CinemachineCamera and existing
MainCamera CinemachineBrain. The reference FBX is authoring-only: its root-local
camera position `(0.682654, 2.017028, 2.782889)` and Euler rotation
`(18.879890, 170.777700, 11.420010)` are baked into the manager. The virtual camera
copies the physical lens (36×24 sensor, 24 mm focal length / 53.1301° vertical
sensor FOV, Horizontal gate fit, zero lens shift). Its transform is computed
relative to `PartySlot.PresentationRoot`, exposed by `PartySlotVisualPreview`.
The FBX marker's import-axis rotation/100× scale is not an actor transform.
No reference FBX Camera is instantiated during play.

`BasementAccessoryBlends.asset` supplies 0.8-second EaseInOut blends to/from
AccessoryCamera; the existing default 2-second stage-selection blend stays intact.
Keep the manager's `accessoryBlendDuration` matched to that asset. The controller's
`fadeInDuration` is the final 0.5-second portion of the actual blend: its CanvasGroup
eases from transparent to opaque while the camera settles. Clicks stay blocked
until both finish. `GetAccessoryRevealProgress` exposes that shared timing.
The manager updates the camera target before Cinemachine's LateUpdate and uses
unscaled time for this menu. It temporarily matches the character-selection lens
to the reference's physical Horizontal gate fit, preserving the original vertical
FOV. This avoids Cinemachine's discrete perspective/physical switch at 50%.
Do not change the reference gate fit without reviewing that conversion.
The output viewport interpolates into the fitted 16:9 rectangle instead of snapping.
Closing restores the original source lens, output projection and brain timing.

`otherCharactersFadeDuration` on the screen defaults to 0.45 seconds for fading
unselected PartySlots out/in. `BasementAccessoryRendererFade` temporarily instances
their materials: ZLZ character materials use `_DitherAlpha`, and URP Lit exceptions
use alpha blending. It never edits shared material assets or adds runtime property
blocks (which can interfere with character lighting). Materials using another
shader log an authoring warning and are hidden only at fade completion; add fade
support before using such a shader on a Basement preview or its equipment.

`Assets/Fonts/Sarabun/Sarabun-Regular SDF.asset` bakes ASCII, Thai and punctuation
into one static 1024² atlas (72-point sampling, 8-pixel padding). Its source font
and OFL license are retained alongside it, from the
[Google Fonts Sarabun distribution](https://github.com/google/fonts/tree/main/ofl/sarabun).

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

For a migrated Player/Ally Stryker, author `skillSlots` in any order but assign
stable IDs and semantic kinds:

- one `slotId: active` with `Slot Kind: Active`
- one `slotId: ultimate` with `Slot Kind: Ultimate`
- zero or more separate slots with `Slot Kind: Passive`

Active/Ultimate options must reference castable `SkillGemDefinition` assets;
Passive options must reference `PassiveDefinition`. Do not use list position as
the contract. Run **Tools > RB > Validate Player Ally Skill Loadouts** after
editing these assets. The validator reports legacy slots, duplicate IDs, missing
configured assets, mixed Passive/cast options, and missing or duplicate
Active/Ultimate slots.

Do not author a third Player/Ally cast fallback on
`CharacterSkillManager.autonomousSlots`. Migrated two-slot characters reject
cast input 2 even if a stale prefab entry exists. Enemy, Summon, Chain Attack,
Party Follow-Up, and Helper data are separate systems and must not be moved into an
Ultimate slot by inference.

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

The Basement screen may change the selected option and inspect/upgrade every
tree. In a map run, selection calls return false and do not fall back to writing
the save; tree inspection and upgrades remain available. The run lock stores
only selected IDs, not `SkillInstance` objects or upgrade snapshots.

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
6. Press `Save All Changes` in the timeline window (or `Save VFX Data` on `SetAnimationVfxData` for VFX only)
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
When playback/request ends (including interruption), each StartLoop's `Session End Mode`
controls remaining instances. `Immediate` is the serialized default; `FinishParticles`
stops emission, detaches at the current world pose and lets the pool-owned tail finish.
Use an explicit `StopLoop` for a tail beginning at a particular marker. StopLoop's
own `Allow Particles To Finish` and `Extra Life` still take precedence at that marker.
FinishParticles requires particles; mesh/shader fades need their own end animation.
The entry's `Preview Session End` button previews this policy; non-looping main
animation playback also applies it when it reaches the clip end, and the cutscene
section applies it before handing off to the main clip. Looping the same preview
entry restarts its single preview instance. Closing/stopping
preview explicitly still clears previews.
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

### VFX lifetime and pooling authoring

OneShot entries expose `Lifetime Mode`: `Auto` preserves the existing estimate
(maximum particle/trail lifetime versus animation clip length, plus Extra Life).
`Explicit` uses exactly `Explicit Lifetime` seconds and ignores estimated duration,
minimum lifetime and Extra Life. Use Explicit for mesh/shader effects or a deliberate
cutoff. Explicit retains authored particle looping until that cutoff; Auto disables
particle loops for one-shot playback. Both runtime and preview use the same calculation and deadline. Validation
reports nonpositive/nonfinite explicit durations and Auto entries with no lifetime
source or positive Extra Life. Existing serialized entries default to Auto/Immediate.

Runtime VFX instances are rented inactive, reset/configured, then activated.
FinishParticles animation loops use a detached follower so disabling/destroying the
owner cannot interrupt their tail; non-Generic anchors still track position, rotation
and changes in scale. Status loops and graceful pre-cast loops use the same approach. Do not
schedule `Destroy` on a pooled instance. Retain `VfxLease` for delayed cleanup; an old
lease cannot stop a later rental of the same object. `SpawnVfxEnabled` gates new
spawns (and damage numbers), not already active effects or gameplay execution.

The pool owns ParticleSystem stop actions and Cartoon FX clear behavior on clones;
source prefabs stay unchanged. Custom scripts that modify material/property blocks,
timers or subscriptions implement `IPooledVfxLifecycle`. Rent/return callbacks run
inactive; restore only state owned by that script. The pool resets child poses,
active flags, layers, root scale, particle loops, trails, follow anchors and its clock.
It cannot undo arbitrary work in unrelated custom scripts. VfxRigSpawnPlayer reuses
and releases its owned AnimatorOverrideController instead of allocating one per rent.

Graceful tails keep their clock selection after detaching. Their deadline is
estimated from base particle/trail speeds so world slow is applied only once. Pool scene-unload cleanup
uses the originating scene, including detached tails; pool disable also returns active
rentals. The default inactive capacity is 20 per prefab, not a concurrent-spawn limit.

## Shared Animation VFX Authoring

Use `SetAnimationVfxData` for source-neutral entries such as a
`SkillGemDefinition` Combo Step or `CharacterAnimProfileSO` Dash/Reload animation. Select
the source and entry in the shared timeline, create or sync slots, place prefab
children, then save the VFX data.
Assigning a Combo Skill on the component selects the first valid step by
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

For sliding while shooting, preview the
slide clip with the shooting clip and save a profile with `Base Pose = Slide`
and the matching `Upper Action`. Runtime selects `Slide` when a valid slide
clip is active and maps it to `CharacterAnimProfileSO.slide.Clip`. Existing
clip-keyed slide profiles also work through the clip-reference fallback; they
do not need to be recreated. Without a valid slide clip, locomotion keeps its
normal directional offset selection.

### Previewing offset fades

`SoloRootMotionPreview > Transition Preview` accepts optional `From Base Clip`
and `From Upper Clip`. The normal Base/Upper clips are the destinations. Move
`Base Fade To Current` and `Upper Fade To Current` from 0 to 1 independently to
inspect a transition; clearing a source clip fixes that channel at the destination.
The time scrubber samples all participating clips at the same time in seconds.
Each pair uses its local working profile if present, otherwise its saved SO
profile. Missing/disabled pairs contribute zero correction. `Upper Body Weight`
also scales the correction, using the same rotation evaluator as runtime.

During a transition, Capture Pose does nothing and Save keeps authored values
without baking the blended pose into one profile. Set both fade weights to 1
before capturing a destination pose. Existing SO entries remain valid. New
runtime Base Pose keys include Slide, Sprint, CrouchIdle and CrouchMove.
`Show Debug State` on the runtime applier lists evaluated base/action weights.
Runtime fades include every still-weighted state and actual mixer child weights;
offsets are not switched immediately when movement intent changes.

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
actual evaluated base/action clip references, so older
clip-keyed `PairOffsetProfilesSO` entries can still work while they are being
migrated to explicit enum keys. While an upper-body action is held, changing
movement direction changes the active locomotion blend. The applier uses the
evaluated mixer child weights to blend multiple `Base Pose` profiles in the same
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
- `PlayerTargetingController` on the same actor root, bound to `PlayerContext.Targeting`
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

### Player Soft-target Authoring

`Assets/Prefab/Player/Player.prefab` owns one `PlayerTargetingController`. Keep its owner bound to
the prefab's `PlayerContext`. Starting values are 30 m range, 0.15 acquire radius, 0.18 release
radius, 0.015 switch advantage, 0.10 s switch delay, and all layers as LOS obstacles; tune these on
the controller only after representative 16:9 and ultrawide playtests.

`PlayerTargetIndicatorView` follows the runtime-overlay convention used by
`ThirdPersonReticleView`: `GameplayCameraController` creates one persistent Screen Space Overlay
instance. There is no marker prefab to place and it receives no raycasts. Its arrow uses live cached
character bounds for the overhead point; `AITargetInfo.AimPoint` remains the separate score/aim
point. Character prefabs should therefore keep their normal `CharacterController`, non-trigger
body collider, or renderers available for height resolution.

The former `PlayerInputHandler.meleeSearchRadius`/`meleeTargetMask`,
`InterruptionCommandController.targetSearchMask`/`targetObstacleMask`/`targetSearchRadius`, and
Chain/Helper sequence aim-search radius, trigger, LOS, obstacle, and ChainReady-preference fields
have been removed. Acquisition belongs to `PlayerTargetingController`; consumer-specific action
eligibility remains where it belongs (`meleeSearchDistance = 4 m`, interruption range/window,
sequence target layers). Do not re-add the old fields as fallback selectors.

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

No additional prefab or FBX importer setup is required for
player and party chain-attack root motion placement or Guaranteed Interruption
ally placement. The attack `AnimationClip` remains the source of truth: Unity
must already extract the clip's root motion so `Animator.deltaPosition` and
`Animator.deltaRotation` produce the intended movement during playback.

Targeted Helper delivery skills are the exception because their payload owns a
gameplay stand-off. Author **Targeted Delivery > Caster Placement > Target
Stand-Off At Placement Point** on `TargetedDeliverySkillPayloadDef`; this is the
horizontal Helper-to-target distance at the skill placement point. The existing
serialized field/API keeps the `AtCastPoint` suffix for compatibility. The runtime
derives the Animation start pose from the sampled root motion and never scales
the clip to fit this value. `AllyHelperManager.minSummonRadius` on the Player
prefab is only for the generic fallback ring around the Player and must not be
used to tune a targeted skill. Milano Skill 3 currently authors this payload
value as `1.2m`.

In **SkillGemDefinition > Presentation > Target Placement**, select **Placement
Timing** to align a root-motion attack independently of when its payload starts:

- **Legacy** (default): preserves Cast Point alignment for Field Ally Chain and
  targeted Helper; Interruption keeps HitStart with Cast Point fallback.
- **HitStart**: align with the first HitStart marker. If absent, the inspector's
  **Placement Point / HitStart Fallback** value is used independently of Cast Point.
- **Custom**: align at **Placement Point / HitStart Fallback** (normalized clip time).

The **Animation Event VFX Timeline > Animation / VFX** view also has a separate
**Placement Point** row for the main skill. Scrub to the contact pose (A), then
press **Set At Playhead > Placement Point**, drag its marker, or right-click the
row and choose **Set Placement Point Here**. Each action selects **Custom** timing
and saves the skill asset; Undo/Redo restores the mode and time together. Cast
Point and HitStart are unchanged. The marker label shows Custom, HitStart,
HitStart Fallback, or Legacy. Legacy displays the Cast Point used by Field Ally
Chain/targeted Helper; Interruption still follows HitStart when available.
Use the Inspector's **Placement Timing** to switch back to HitStart or Legacy.
Point editing is disabled in Play Mode and the cutscene portion of the preview;
cutscene-only and basic-melee Combo entries do not expose Placement Point.

For **Party Follow-Up**, choose **Align Placement Point To Event Target** in the
execution profile's Placement Policy to use this marker when finding the warp
start S. In this mode **Desired Range** means the actor-root distance from the
target at A, and **Local Offset** offsets A. The existing Place Near Event Target
mode still measures the initial warp distance and does not use the marker.
`Test.Aires.FollowUp` uses the dedicated `AiresFollowUpExecution` profile with
this mode enabled and Desired Range 2m. Adjust that range for the skill's reach;
Placement Point supplies timing, not the blade/contact offset. Missing trajectory
data or a blocked trajectory prevents the Combo from starting.

This Party Follow-Up mode preserves the clip's vertical root motion for jumps.
Desired Range aligns the horizontal contact pose; the root height at that beat
comes from the authored Y curve relative to the grounded takeoff. It does not
force an airborne contact beat down to the target's ground height. The body must
clear ceilings and have supported takeoff/landing positions. Existing planar
Chain/Helper sampling remains unchanged.

For a slash that starts its payload at 0.20 and reaches the target at 0.45, keep
Cast Point at 0.20 and choose Custom placement 0.45 (or HitStart if that marker is
the desired pose). Keep HitStart/HitEnd authored for the actual damage window.
The placement offset describes the actor root at that time, not the blade tip.
Payload listeners must still exist before their required timeline markers fire;
moving Placement Point does not create or move those markers. No existing skill
assets are migrated automatically. The separate static Helper Chain teleport does
not use trajectory alignment.

`SkillGemDefinition > Presentation > Ignore Character Collision During Root
Motion` defaults to enabled. Keep it enabled for jumping, lunging, and other
clips with Y root motion so Player, Enemy, and Ally bodies cannot become ground
or steps. This setting does not disable world collision. Disable it only for a
skill whose root-motion movement is intentionally blocked by character bodies.
No prefab reference is required; the runtime root-motion driver resolves the
character layers and restores the previous collision state when playback ends
or is interrupted.

Ally and Enemy prefabs using `RootMotionNavMeshDriver` must leave **Zero Y** disabled.
Per-request `RootMotionPlanarOnly` is responsible for suppressing Y on targeted
placement flows such as Guaranteed Interruption. Enabling **Zero Y** on the
prefab suppresses vertical root motion for every Ally or Enemy skill and is only
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
projectile spread and `100%` removes spread. For passives, accessories, affixes, status
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

## Party Follow-Up — Prefab / Input / HUD Wiring

Add `PartyFollowUpSkillExecutor` and `PartyFollowUpOpportunityController` to the
player/context hierarchy. Assign the controller's Executor and
`PartyFollowUpFeatureFlags` fields, then bind both components on `PlayerContext` or
leave them discoverable within the same actor hierarchy. `PartyRuntimeBinder`
binds the controller after the party and Helper are ready and before
`PlayerUIRuntimeBinder` runs.

The current authoring is complete on `Assets/Prefab/Player/Player.prefab`: both
components are on the Player root, both `PlayerContext` references are bound,
and the controller references
`Assets/Data/PartyFollowUp/PartyFollowUpFeatureFlags.asset`. Keep `runtimeEnabled`
and `phase4aPublishersEnabled` off until the focused Play Mode rollout checks
pass.

Each participating field ally's `CharacterStats > Party Follow-Up Skill` must point
to a dedicated `PartyFollowUpSkillDef`. Its execution skill must not also appear in
normal battle slots, Helper slots, or the legacy Chain Attack field. Validate
the assets with **Tools > Validation > Validate Party Follow-Up Skills**.

For the combat HUD, add `PartyFollowUpHudPresenter` and assign a
`PartyFollowUpSlotView` for `PartySlot1` and `PartySlot2`. Bind the slot's root,
icon, radial expiry fill, fallback label, and optional disabled-state label.
The existing `PlayerUIRuntimeBinder` finds and binds presenters beneath the UI
root.

The concrete HUD subtree is
`PlayerUI/UI_Manager/PlayerHUD/PartyFollowUpHud` in
`Assets/Prefab/User Interface/PlayerUI.prefab`. Its two slot components stay
active for binding while their `View` children begin inactive and are toggled
by `PartyFollowUpSlotView`. The two views are stacked at the right-center edge and
contain the opportunity icon, radial expiry overlay, combo name, `FOLLOW-UP READY`
tag, and input glyph. Keep decorative Images non-raycastable because the popup
is an input prompt, not a mouse button.

`PlayerVitalHud/Ally1VitalHud/FollowUpCooldown` and
`PlayerVitalHud/Ally2VitalHud/FollowUpCooldown` are also bound by the corresponding
slot views. Each indicator sits above the ally HP bar and contains the combo
icon, a horizontal charge-recovery fill, and a `READY`/seconds label. Do not
drive this fill from a local timer: `PartyFollowUpSlotView` reads
`CharacterSkillManager.TryGetPartyFollowUpChargeStatus` through the bound combo
controller so the HUD and execution use the same charge pool.

`Assets/Input/Inputmaneger.inputactions` map `Player` contains:

| Action | Keyboard | Gamepad | PlayerInput callback |
|---|---|---|---|
| `PartyFollowUpSlot1` | `4` | D-pad Left | `OnPartyFollowUpSlot1` |
| `PartyFollowUpSlot2` | `5` | D-pad Right | `OnPartyFollowUpSlot2` |

The `PlayerInput` Unity Events on `Player.prefab` are already wired to those
callbacks. A reserved `OnPartyFollowUpHelper` callback exists for later Helper
support. Keep the input bindings and HUD glyphs synchronized, and edit the
asset through Unity's Input Actions editor rather than hand-editing JSON.

Default-off vertical-slice content is stored under
`Assets/Data/PartyFollowUp/Test/`: Feno's Entered Break offer chains into Aires's
`FollowUpSkillCommitted` follow-up through the shared
`FieldAllyFollowUpExecution.asset` profile. Run the Party Follow-Up validator after
changing either definition or its owning `CharacterStats` asset.

Full data, runtime, and cleanup contracts are documented in
[`Docs/SYSTEMS/PARTY_FOLLOW_UP.md`](SYSTEMS/PARTY_FOLLOW_UP.md).

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
- In `Assets/Input/Inputmaneger.inputactions`, use the `Player/Block` action
  (Button) bound to `<Keyboard>/space`. Wire the `PlayerInput` event to
  `PlayerInputHandler.OnInterruptionCommand`.
- `Player/Dash` is bound to `<Keyboard>/shift` (left or right Shift).
- The command reads the already committed `PlayerContext.Targeting` actor. Configure soft-target
  acquisition on `PlayerTargetingController`; this command keeps only its own range, placement,
  block-window, and executor rules and never searches a second enemy.
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
- `CharacterVisibilityController` is optional. When present through
  `FieldAllyMember.VisibilityRef`, the ally is concealed before the snap and
  fades in with the interruption animation. It is not used at the end of the
  skill; visible root compensation handles the return to locomotion, so the
  end-of-skill rebase also works when no visibility controller is configured.
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
this is the override point instead). The turret visual root now owns a
`WeaponMuzzleBinding` targeting a FirePoint child of the authored FirePoint.Bone
under Row/Terret. Its corrected child orientation points +Z down the barrel;
the imported bone's orientation remains unchanged.

`CharacterVisualController.buildModelAutomatically` is false on this prefab.
It must bind the already-authored visual through SetupExistingModel, including
its integrated muzzle and health bar. It does not instantiate a hand-held gun.
Other prefabs with a preplaced model need the same model-build setting.

The turret WeaponSystem now supports the shared aim/muzzle launch path. Its
authored behavior tree still determines whether it requests weapon fire; this
migration does not add a new firing task to a rotation-only tree.

`Assets/Data/Skills/Feno/Feno.Skill_MinigunTerret.asset` is assigned to Feno's
slot 2. It requests one stationary summon at the caster-forward offset
`(0, 0, 2)`, with a 10-second lifetime, 12-second cooldown, 25 energy cost, and
per-skill cap of one. Keep the payload prefab reference pointed at the variant,
not at `SummonBase` or the source turret prefab.

## Dialogue presentation

Full system reference: `Docs/SYSTEMS/DIALOGUE_SYSTEM.md`. What has to exist in the project and in
scenes:

**Project settings.** Rendering layer 5 is named `Dialogue`, maintained by
`Tools/Dialogue/Set Up Project Layers`. There is **no dialogue Unity layer**: clones sit on layer 0
like every other character, because ZLZ's character render features filter by Unity layer and a
clone on its own layer renders without the contact shadow and screen-space outline the same
character has in gameplay. Keeping
clones out of the gameplay view is done by parking the stage far below the level instead, so
**that distance is load-bearing** — do not move the stage closer.

**`Assets/Scenes/DialoguePresentation.unity`** is generated by
`Tools/Dialogue/Build DialoguePresentation Scene` and must stay enabled in Build Settings. Hierarchy:

```
DialogueStageRoot          DialogueStage + DialogueDirector, parked at y = -20000
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

## Special Shoot Point Authoring

Opt-in and enemy-only. An enemy participates when it carries a
`SpecialShootPointController` with a profile and enough usable anchors; anything
less makes the Behavior Tree trigger report `Failure` rather than run a degraded
round. Prove the APIs first — do not start from a specific enemy prefab.

### 1. Profile asset

`Create → RB → Enemy → Special Shoot Point Profile` produces a
`SpecialShootPointProfileSO`. Suggested home:
`Assets/Data/Combat/SpecialShootPoint/`.

| Field | Default | Notes |
|---|---:|---|
| `telegraphDuration` | `0.6` | Points visible, colliders disabled |
| `activeDuration` | `4.0` | The shared HUD countdown |
| `cooldown` | `8.0` | From challenge resolution, not from the end of the Mini Stun |
| `lastSecondWarningThreshold` | `1.0` | Must not exceed `activeDuration` |
| `defaultPointCount` / `maxPointCount` | `2` / `4` | A BT override is clamped to the maximum and the usable anchor count |
| `pointHealthPercentOfMaxHp` | `3` | Percent of the enemy's current Max HP, shared by every point in a round |
| `pointHealthMin` / `pointHealthMax` | tuning | Clamps on the resolved per-point HP |
| `staggerRewardPercentOfMaxStagger` | `25` | Percent of the target's Max Stagger |
| `missingClipFallbackSeconds` | `0.6` | Gameplay lock held when the Mini Stun clip is missing |
| `runtimePointPrefab` | — | **Required.** Must carry `SpecialShootPointInstance` |
| `pointColliderLayer` | `3` (`Hit`) | Applied to the pooled point and its collider |
| `pointHitRedirectDistance` | `1.0` | How far past an outer hit-zone impact a shot may still be credited to a point on its trajectory. Keep below the enemy's body thickness |
| `breakVfxPrefab` / `timeoutVfxPrefab` | — | Success and failure **must** read differently |
| `resolveFadeSeconds` | `0.25` | Fade before the point returns to the pool |
| `pointHitSfx` / `pointBreakSfx` | — | Optional |
| `debugLogging` | off | Logs every direct hit on an enemy running a round. Turn on to diagnose "my shots are not landing"; leave off in builds |

### 2. Runtime point prefab

One small prefab, pooled and reused. Never instantiated per hit or per round.

- Root carries `SpecialShootPointInstance`.
- A **`SphereCollider`**, `isTrigger = true`. Its radius is overwritten per round
  from the selected anchor, and the component enables and disables it as the round
  phase changes — leave it disabled in the prefab.
- A presentation root (assigned to `presentationRoot`) that is uniformly scaled by
  the anchor's `vfxScale`.
- A `Renderer` for the ring/crack (`ringRenderer`). The component drives
  `_Fill` (remaining HP, 1→0), `_Flash` (hit and last-second warning), and
  `_Alpha` (resolve fade) through a `MaterialPropertyBlock`. Property names are
  serialized fields, so any shader that exposes equivalents works; a property the
  shader does not have is simply ignored.
- Nothing that renders through the enemy or the environment: there is no
  through-model/X-ray point in this feature.

### 3. Enemy prefab and anchors

Add `SpecialShootPointController` to the object that owns `EnemyContext`, then:

- assign `profile`,
- leave `ctx` empty to auto-resolve, or bind the `EnemyContext` directly,
- optionally assign `poolRoot` (defaults to the controller's own transform).

**Anchors go on the visual model prefab, not on the controller.** Add a
`SpecialShootPointAnchorSet` next to `CharacterColliderRefs` on the visual prefab
and author the list there. Enemy models are rebuilt from that source at runtime
(`CharacterVisualController.BindCurrentModelReferences`), so a bone Transform
serialized on the context root points at the destroyed model instance after the
first rebuild — exactly the reason `CharacterColliderRefs` lives on the model too.
The controller resolves the set through its context and re-resolves it whenever
the model is swapped; a round whose points are destroyed by a rebuild cancels
itself rather than stranding an uncompletable challenge.

The controller keeps a local `anchors` fallback list for actors whose model is
never rebuilt, such as turrets. The authoring validator warns when it is used on
anything else.

Each anchor entry:

| Field | Notes |
|---|---|
| `anchor` | The bone or transform the point rides. Required |
| `enabled` | Uncheck to keep the entry but drop it from the shuffle bag |
| `localPosition` / `localEulerAngles` | Offset from the bone |
| `colliderRadius` | World radius of this point's hit collider |
| `vfxScale` | Uniform scale for the point's presentation root |
| `hitZone` | `Torso` or `Head`. A head anchor takes the normal Headshot multiplier |

`localPosition` is a **visibility** setting, not a hit setting: nudge the point out until it reads
on the model. Whether a shot can land is handled by the trajectory redirect (see
`Docs/SYSTEMS/SPECIAL_SHOOT_POINTS.md`), so a point that sits inside the enemy's coarse body hitbox
is still hittable. Placement is an art call — a solver cannot tell where the silhouette looks right.

Author at least as many enabled anchors as `defaultPointCount`, and preferably
several more — the shuffle bag only produces variety when there is a surplus.
Do **not** place live point objects or colliders under candidate bones: anchors
are data, and the pooled runtime points are reparented onto them at run time.

`EnemyContext.SpecialShootPoints` resolves the controller automatically in
`ResolveReferences()`. Leaving it `null` on every other enemy is the normal case
and is not an error.

### 4. Animation

The reaction reuses the profile's existing **`CharacterAnimProfileSO.miniStune`**
clip; there are no Light/Heavy variants in v1. The clip should carry real root
motion — the reaction applies full animation translation *and* yaw. A missing or
invalid clip is not fatal: the lock is held for `missingClipFallbackSeconds`, one
warning is logged, and the round still completes and hands off to ChainReady.

For enemies driven by `RootMotionNavMeshDriver`, check its **Environment-Safe Root
Motion** block: `environmentCollisionMask` (geometry the reaction may not pass
through), `environmentCollisionPadding`, and `navMeshRecoverySearchRadius`. These
are consulted only while a playback asks for environment safety, so ordinary skill
and chain clips are untouched. The actor must have an authored
`CharacterColliderRefs.CharacterPositionCollider` for the sweep to have a body.

### 5. HUD

Two optional presenters, both bound through the controller's static round events
so a single instance serves whichever enemy opens a round:

- `SpecialShootPointHudView` — `root` (shown only during a round), `countdownFill`
  (a **Filled** `Image`), optional `countdownLabel` and `pointsRemainingLabel`, and
  the normal/warning fill colours.
- `SpecialShootPointOffscreenIndicator` — `markerRoot` (a screen-space canvas
  `RectTransform`), `markerPrefab`, optional `worldCamera` (falls back to
  `Camera.main`), and `edgePadding`. Markers appear **only** for points outside the
  camera frame; an in-frame occluded point deliberately gets none.

### 6. Validate

`Tools → RB → Validate Special Shoot Point Authoring` with the prefab or scene
object selected reports a missing profile or point prefab, a point prefab with no
`SphereCollider`, too few usable anchors for the configured count, duplicate
anchor transforms, and inconsistent profile clamps.

## Sewer Toon Waterfall and Ripple Shaders

The URP unlit sewer-water effects live under `Assets/Shaders/Sewer_Toon/`:

- `Waterfall.shader` generates animated Voronoi water streaks, noisy bottom foam,
  separately adjustable HDR `Base Water Color`, `Ripple Color`, and
  `Bottom Foam Color`, plus an `Erosion` cutoff. The base and ripple colours are
  selected by the ripple mask rather than added together, so changing one does
  not tint the other region. Enable `Invert Ripples` to reverse that mask for
  colour, erosion, and vertex displacement together. Apply
  `Assets/Materials/Sewer_Toon/Waterfall.mat` to a vertical waterfall mesh whose
  UV G axis runs from bottom (`0`) to top (`1`). Duplicate the mesh and raise
  `Erosion` and HDR brightness when a separate foreground foam layer is needed.
  Use `Vertex Offset Amount` (about `0.05`-`0.1`) on that foam material to prevent
  z-fighting, and lower `Vertical Cell Scale` when the source mesh needs longer
  vertical streaks. `Bottom Fade Start`, `Bottom Fade End`, and `Bottom Fade Power`
  fade the waterfall by UV G; the mesh bottom must map to the lower G values.
  Keep `Cull Mode` at Back to hide the rear surface. Enable `Depth Write` on the
  main layer: the shader treats that layer as a fully opaque water sheet and does
  not let `Erosion` or the bottom fade expose geometry behind it. Disable
  `Depth Write` on the offset foam layer so it retains the transparent animated
  cutout and avoids self-overlap. The preview scene contains this setup as
  `WaterFall` plus `WaterFall_Foam`.
- `ScrollRipples.shader` scrolls an external ripple texture, fades it with an
  edge mask, adds animated Voronoi distortion, and breaks the rings up with
  dissolve. Apply `Assets/Materials/Sewer_Toon/Scroll Ripples.mat` to a horizontal
  quad or particle. `Ripple Intensity` compensates for dim generated textures and
  their mip levels. In the preview scene, `WaterFall_BaseRipples` is parented to
  the active `WaterFall` and uses a looping Particle System rendered with Unity's
  built-in Quad mesh, expanding Size over Lifetime, Color over Lifetime alpha
  for fade-in/fade-out, and the ripple material. Keep the emitter rotated flat
  against the water, Renderer `Render Mode` at `Mesh`, and Main `Scaling Mode`
  at `Hierarchy`. The ripple then follows the emitter and prefab hierarchy's
  Transform Scale instead of a billboard's screen-size clamp. GPU instancing is
  disabled for this material setup.
  Keep the emitter slightly above the water surface to avoid z-fighting, and
  re-check its world position if the scene contains duplicated waterfall objects.

Starter textures are provided at `Assets/Textures/Sewer_Toon/Ripple_Rings.asset`
and `Ripple_EdgeMask.asset`. They can be replaced per material with authored
textures while keeping the same shader controls. The ripple shader and the
waterfall's foam layer are transparent; the depth-writing waterfall layer emits
opaque alpha to hide geometry behind it. Both shaders intentionally contain no
shadow-caster pass.
## Skill Loadout UI authoring (2026-09-12)

For the separate reference-layout combat HUD, see [Separate Party HUD](SYSTEMS/PARTY_HUD.md).
`DefaultPartySpawnConfig.asset` uses `PlayerUI_PartyHud.prefab` in game. The original PlayerUI prefab remains available as a separate asset.

- Continue using `Assets/Prefab/User Interface/Active Skill/ActiveSkillScreen.prefab`.
  `UILoadLaval.OpenActiveSkillTree` keeps its existing prefab reference.
- `SlotTabs/Viewport/Content` holds Active, Ultimate, Passive, and Combo type cards in
  one full-width bottom scroll region. The left panel selects variants for the
  chosen type; the right panel shows that variant's upgrade tree.
  `PassiveSlots` stays inactive for serialized compatibility; do not add an
  Upgrades category. Keep `loadoutLockText` bound on the controller.
- Assign `CharacterStats.partyFollowUpSkill` for the Combo card and its dedicated
  `PartyFollowUpSkillDef.upgradeTree` for upgrade nodes. Combo is a fixed authored
  skill, not a selectable cast slot. Its tree uses shared Skill Points with
  progress isolated by `party-combo` / `comboId`; no additional HUD cast slot is needed.
- The existing `ActiveSkillSlotTab.prefab` retains its Button, Label and Selected
  objects, with `skillIcon`, `skillName`, and `description` references added.
  These show the equipped option; clicking a tab opens that slot's options/tree.
- `PlayerUI.prefab/UI_Manager/PlayerHUD/SkillChargeHud`: Slot0 has `slotKind=Active`,
  Slot1 has `slotKind=Ultimate`; Slot2 is inactive. Keep its serialized objects for
  compatibility. The Horizontal Layout Group width is 156 (72 + 12 + 72).
- Assign HUD `inputActions`, `inputBindingLabel`, `slotKindLabel`, and
  `readinessGroup`. Decorative graphics/text do not receive raycasts.
- Loadout Canvas retains Scale With Screen Size at 1920×1080. It needs the gameplay
  bootstrap's EventSystem and its own GraphicRaycaster. Basement scene itself
  does not author an EventSystem; enter through the existing bootstrap flow.
- `GameSetup.unity` keeps the configured `System/EventSystem` (InputSystemUIInputModule
  with Point/Click actions). Its unconfigured root-level duplicate is inactive.
- Keep the standalone root `TimeSlowManager` inactive in `GameSetup.unity`.
  The shared `System` object already owns the active TimeSlowManager. Enabling both
  makes singleton duplicate cleanup depend on Awake order and can destroy the
  entire `System` object, including SaveManager and the configured EventSystem.
  Do not restore bootstrap root activation from an older scene snapshot without
  validating the GameSetup-to-Basement transition.
- Missing skill icons use text; do not assign temporary artwork or invent an
  Aires/Roma skill mapping. Unmapped legacy slots remain in authored data but
  are not exposed as extra Upgrades tabs.

## Defensive Block production and test scene

For Player + Ally defense choose `Skill > Defensive Block > Mode = Cooperative` and enable
`cooperativeEnabled` on **both** actor profiles. This opt-in is false by default. Use a melee/dash
hitbox payload and bind at least one step to the Block window; the first bound step supplies the
failure hit. The command cannot become ready until that payload execution exists. Cooperative
ends the entire request; `Continue Skill` is invalid. Configure the approach and `cooperative`
meter fields in the Inspector or the Timeline's draft settings popup, then Save.

Both actors need the existing context, `FieldAllyMember`, `DefensiveBlockController`, animation,
placement body, health and movement setup. Cooperating actors must have a safe NavMesh-backed
position beside the Player. Optional `CharacterAnimProfileSO.block.cooperativeStruggleClip` supplies
a Player/Ally struggle loop; leave it empty to use the held guard pose. No new animation asset is required.

For the enemy, assign **Skill > Defensive Block > Cooperative > Enemy Struggle Clip**
and **Enemy Struggle Fade Seconds** (default 0.06). The Timeline's **Block > Timed
Approach Settings...** popup exposes the same fields through draft/Save/Revert/Undo.
Use an in-place, non-Legacy clip with positive length and a finite, nonnegative fade.
An empty clip holds the attack impact pose. A configured clip loops only during the
struggle, pauses with gameplay, and stops when the session resolves or cancels.
The loop does not execute skill payload/events or advance the original attack timeline.
This is per-skill enemy presentation; defender clips remain in `CharacterAnimProfileSO.block`.

The production `GameplayHUD` nests `Assets/Spain/UI/Block HUD/SpacebarBlockHUD.prefab`
on the right (right-center anchor, offset `(-550, -140)`, scale `0.52`). Its
`CooperativeBlockHud` is bound by `GameplayHudPresenter`; `Ensure` first searches the
Player's `playerUIContext` so it reuses this view without creating the fallback overlay.
It appears only while Struggling. `Power Gauge_2` uses `SlantedBarFillEffect` to clip the
authored gold bar to the session meter without stretching the artwork. Accepted presses
swap `Base_1` for `Base_animation` and enable `Text_animation` (sprite `Text_2`) for
0.12 seconds, then restart the idle blink cycle. Keep `Text` active underneath the
text overlay so its exclamation mark remains visible; exclude `Text` from `restingArtwork`.
While Struggling, the base swap and text overlay also blink automatically every
`blinkInterval` (default 0.3 seconds). Blinking uses unscaled time, freezes during pause,
and resets when the session ends or the view is unbound. Images do not
intercept clicks, and the Block HUD is excluded from the right-panel fade.

For a custom or fallback HUD, add `CooperativeBlockHud` below the Player and bind `group` (CanvasGroup), `meterFill`
(uGUI Image), and `prompt`, `timeLabel`, `helperLabel` (TMP). The fill must be left-pivoted because
the component scales its local X. Bind the HUD to the Player's interruption controller through
`Bind`; the runtime discovers an existing child HUD or creates its default overlay on first use.
Keep Block wired to `PlayerInputHandler.OnInterruptionCommand`, including canceled callbacks, so
held input cannot repeatedly add meter. The prompt reads the action's actual binding display string.
The session owns one camera shot; do not start a second guard camera from its Accepted events.
Cooperative uses the Player guard profile's normal Block camera during approach. At contact,
**Cooperative Struggle Camera** blends to a closer oblique rear shot with the actors on the
left: local position `(1.9, 2.2, -3.4)`, Euler `(10, -12, 8)`, FOV `55`, blend `0.2` seconds.
These settings are snapshotted at entry and held through the success reaction before returning.
It centers on Player/Ally and backs away from the authored camera offset only enough to fit
both defenders, with screen-edge padding and the output camera's aspect ratio. Enemy distance
does not affect framing. The existing wall avoidance can shorten this distance in tight spaces.

`GameplayHudBlockVisibility`, installed by `GameplayHudPresenter`, fades `AmmoDisplay`,
`FollowUpDisplay`, the four `SkillReady Ally` roots and optional `MinimapPlaceholder` over
0.18 unscaled seconds during struggle/success. It restores their original CanvasGroup alpha
and input flags on exit, cancel or unbind. Keep these as direct GameplayHUD children.
Left-side status/party panels and the separate Block prompt remain visible; hidden presenters
stay active so cooldowns and combo expiration continue updating.

Tune **Cooperative Camera Shake** on the Player's `DefensiveBlockActorProfile`:
`cooperativeImpactShakeDegrees` (1.8), `cooperativeImpactShakeSeconds` (0.22), and
`cooperativeImpactShakeFrequency` (22 Hz) control the one-time contact kick;
`cooperativeStruggleShakeDegrees` (0.12) and `cooperativeStruggleShakeFrequency` (9 Hz)
control the gentle continuous shake. Set either Degrees value to zero to disable that
effect. Values are copied when the shot begins; edits apply to the next Block.
The contact kick starts when the pair and enemy reach Struggling, not on the initiating
button press. Both effects pause with gameplay and stop on resolution/cancellation.
Tune **Cooperative Success Camera Shake** on the same Player guard profile for the
separate kick when the pair wins: `cooperativeSuccessShakeDegrees` (1.8 degrees),
`cooperativeSuccessShakeSeconds` (0.2 real seconds), and
`cooperativeSuccessShakeFrequency` (20 Hz). Zero degrees disables it. The kick pauses
with gameplay and clears on shot cleanup; failure/cancellation does not trigger it.
The same initial contact also plays **Impact Presentation > Impact Vfx** from the Player
guard profile, once at the midpoint of Player/Ally guard contact centers. **Impact Vfx Lifetime**
controls its normal lifetime; session cleanup removes it earlier if necessary. Use the existing
effect slot (or leave it empty to omit VFX). Success feedback still plays when the pair wins.
**Impact Cue** now also plays once at initial Cooperative contact, alongside the VFX and
camera kick. It uses the same `AudioCue` as success feedback (the fixture uses `BlockImpact`).
Edit that cue for the clip, volume and pitch; an empty cue disables this sound.
Cooperative screen wind reuses the HUD's existing `perfectDodgeScreenEffectPrefab` reference;
no prefab changes are needed. Its owned `BeginScreenWind` API returns a `PlayerScreenEffectHandle`
which the caller must advance and dispose. Cooperative Block does this with its own pause-aware
clock and ends the wind immediately on resolution/cancellation. Unlike the autonomous screen
effect APIs, it works with an inactive HUD and does not extend the requested duration to match
the prefab's particle lifetime. Other callers' effects are independent.
Keep **Screen Effect Size Multiplier** near `1.15` (115% of the camera frustum size,
not a percentage input). `PlayerUI` and `PlayerUI_PartyHud` use this value. Their old
`110.8` override expanded the quad beyond the screen so the wind's edge pattern was
outside the viewport even though its particle existed directly in front of the camera.
Both Player UI prefabs use **Screen Effect Opacity Multiplier** `0.8` (previously `0.4`)
to make the wind more visible. This shared setting also applies to other screen effects
created by the controller.

The test fixture generator **Tools/RB/Defensive Block/Add Cooperative Test Skill** adds the
Rector copy and opt-in Player/Aires profiles under `Assets/Tests/DefensiveBlock/Cooperative`.
The existing test-scene skill picker selects this mode without modifying production assets.

Production bindings use the original Rector Skill 1 and Aires character definition.
Tune `Assets/Data/Skills/Enemies/Rector/Rector_Skill_1.asset > Defensive Block` for range, window, hitbox steps
and Rector knockback. Its **Threat prediction** fields configure pre-hitbox lane
half-width (1.5 m), forward reach (2.3 m) and estimated charge speed (8 m/s).
Defensive Block selects incoming attacks automatically without aiming; actual
hitbox geometry and observed speed replace the estimates when available. Tune
`GuardSetting.asset` for placement, guard, recoil and warp fade;
`CharacterStats.animProfile > Block` for clips and phases. Companions need an opted-in
character definition. `Player.prefab > DefensiveBlockController.defaultSettings`
references the same GuardSetting as a fallback when Player's character definition
does not supply one. Player and Aires have identical impact results for this prototype.
Player guards in place; only companions use landing placement and warp fades.

**Skill > Defensive Block > Mode** selects **Contact** (physical hitbox interception)
or **Timed Approach** (safe forward movement to a timed impact). Rector Heavy uses
Contact; Rector Skill 1 uses Timed Approach with **0.22 seconds** and **1.6 m Stand
Off**. The latter measures from the guard root along its forward direction to
Rector's final root. Duration is attack-owned and uses the attacker's actor clock.
In the Timeline use **Block > Mode** and **Block > Timed Approach Settings...**;
changes remain draft data until **Save All**. Contact ignores those movement values.
Both the landing and the enemy approach path must be safe. Short approaches that
would require moving Rector backwards are unavailable. Keep Windup Seconds at zero.
No new clip is required: the skill keeps playing with translation owned by the motor.

**GuardSetting > Impact presentation > Impact Cue** references
`Assets/Data/DefensiveBlock/BlockImpact.asset` (AudioCue), using
`Assets/AudioResult/Sfx/BattleSystem/RB_Project_Block_SFX.mp3`.
It plays once on confirmed Block success for either receiver, not when input is
accepted. Tune the cue's Base Volume for this effect; it also follows the Sfx mix.
The authored cue is a global 2D one-shot so the Block camera does not attenuate it.
Clear Impact Cue to disable the sound without changing guard behavior.

**GuardSetting > Ready signal > Ready Cue** references
`Assets/Data/DefensiveBlock/BlockOpen.asset`, a global 2D Sfx one-shot using
`RB_Project_BlockOpen_SFX.mp3`. It sounds when an actionable flare appears on
screen, not for dim unavailable threats. Adjust Base Volume on that AudioCue or
clear Ready Cue to disable it. The Player's guard settings supply this cue even
when a companion will receive the Block.

`Player.prefab > InterruptionCommandController.defensiveBlockEnabled` gates the
feature. `DefensiveBlockReadyCue` on that prefab controls light entry/exit (0.18/0.12 s),
size, offset, brightness and `unavailableBrightness` (0.25). Unavailable incoming
attacks retain a dim flare; actionable commands show the bright flare. There is no
key prompt or overlay Canvas/TMP label. The flare prefab/material/shader are in the same
production data folder. Warp fade defaults are 0.04 s out / 0.08 s in on GuardSetting.
Begin may transition directly to Impact after arrival; fade-in is not immunity.
Rector preparation is authored separately on
`Rector_Skill_1.asset > Defensive Block > Windup Seconds` (**0: disabled**).
The experimental 0.4 s value held the skill pose and was rejected for breaking
animation continuity. Keep it zero for continuous playback. If explicitly enabled,
it delays every cast in the caster's time domain. Keep the command window start at zero to allow
the ready flare and early guard input during preparation. The two charge hitbox
steps remain enabled; removing the first would leave real early damage unblocked.
The Block section in `Aires_AnimProfile` uses `Begin Start Normalized = 0.55` and
`Guard Pose Normalized = 0.65`; earlier frames in `Aires_Block` contain a jump.
For replacement clips, choose a grounded interval rather than changing actor Y to
compensate for animated bones. Landing surfaces must have solid colliders included
in GuardSetting's world layers within 0.2 m of the NavMesh landing point; character
colliders and triggers are excluded as support. This is checked again after fade-out.

Defender recoil is authored on the incoming **Skill asset > Defensive Block >
Defender Recoil (Player and Ally)**. **Slide Distance** and **Slide Seconds** apply
to both defenders, including self guard and Cooperative Block. Defaults are 1.5 m
and 0.35 s; zero distance disables movement. Values are captured when each guard
begins. The old Actor Profile recoil fields have been removed.

Other defender settings are authored on `GuardSetting.asset`
(`DefensiveBlockActorProfile`), referenced by `ChaDef.Aires > Defensive Block` and
the Player controller's `defaultSettings`. A character-specific profile takes priority.
Controller tuning copies are hidden to avoid editing values that the profile
overwrites. The SO includes **Global HitLag** duration (0.06 real seconds), time
scale (0.1), optional blend curve, guard center height and impact VFX lifetime.
Duration zero disables HitLag. Tune before Play Mode, or respawn to reload an
edited profile. The old serialized fields remain for prefab/API compatibility.

On `GuardSetting.asset`, tune **Camera**: enabled, local position, Euler, FOV,
entry 0.16 s, hold 0.12 s and exit 0.4 s. Each shot snapshots the SO at entry;
these fields can be tuned for the next shot without respawning. The production
Cinemachine extension still handles collision, priority and return to live follow.
The camera controller's old settings are hidden compatibility fields.
For Contact and Timed Approach, tune **Normal Block Camera Shake** on that same
actor profile: `normalImpactShakeDegrees` = 1.2, `normalImpactShakeSeconds` = 0.15,
and `normalImpactShakeFrequency` = 20 Hz. Set degrees to zero to disable the kick.
It fires once at successful contact, pauses with the game, and is removed when
the shot ends. Camera must be enabled. Cooperative shake settings stay separate.
`Configure Production Assets` reuses the profile bound to Aires even if renamed;
only an unbound character gets a new `GuardSetting.asset`.

Open `Assets/Tests/DefensiveBlock/RectorDefensiveBlock.unity`; C / Space / R are charge /
Block / reset. Shift is Dash. The harness uses `PartySpawnPoint`, `DefaultPartySpawnConfig`, real
prefabs/UI/input and the production skill. `definitionOverrides` on PartySpawnPoint
supplies the scene roster by party index without modifying saves. Auto-block and
pause-automatic-combat are test controls; disabling the latter resets into live AI.

`Tools > RB > Defensive Block > Create or Upgrade Test Scene` preserves the arena
and updates bindings in place. `Configure Production Assets` sets up the production
profiles/prefabs. Historical copied test assets are no longer runtime dependencies.
See [Defensive Block](SYSTEMS/DEFENSIVE_BLOCK_TEST.md) for lifecycle and validation.

## Perfect Dash success sound

`Assets/Data/Combat/DashSetting_Player.asset > Perfect Dodge > Perfect Dash Cue`
references `Assets/Data/Combat/PerfectDash.asset` (AudioCue), using
`Assets/AudioResult/Sfx/BattleSystem/RB_Project_PrefectDash_SFX.mp3`.
`DashSystem` plays it once per confirmed perfect dodge, alongside its existing
feedback, including when a custom PerfectDodgeHandler handles the screen effect.
Ordinary dash does not play it. `playsPerfectDodgeFeedback = false` suppresses it
for actors configured without player feedback; a null cue disables only this sound.
The cue is a global 2D Sfx one-shot. Tune Base Volume on the cue or the Sfx mix.
## Shared Melee Skill Authoring

- Each combo step references an execution `SkillGemDefinition`. Edit animation,
  impact, VFX and Hitbox Layout on that skill; ordering, chain windows and input
  buffering remain on the combo. A Melee tag alone does not make a skill free.
- Both Basic Melee and Skills use `SetSkillHitBoxData`. Create a source template
  or load the selected skill's layout, adjust its Collider shapes, then Save
  Layout From Source. Box, Capsule and Sphere use the same path.
- Each `SkillHitboxGroup` stores Anchor Space and Anchor Path. Payload preserves
  the existing skill anchor/offset behavior. CasterRoot uses actor-local space.
  AnimatorRoot resolves from the actual model Animator, with paths such as
  `root/c_traj/Weapon.L`; omit actor-wrapper names and `(Clone)` model names.
- Load Layout validates all anchors before replacing the preview. Bone groups
  are parented directly to the bone and retained in the tool's Attached Groups
  list, even outside Source Hitbox Root. Keep the tool on the same character.
  Save converts collider poses back into each group's anchor space and preserves
  anchor metadata. Missing anchors abort without overwriting the skill.
- `CharacterVisualController.ModelAnimator` identifies the live visual model,
  including prefabs with a second placeholder Animator on the actor root.
  Runtime rebinds cached groups at each execution after character/model changes.
- Keep the Basic Melee target mask on `MeleeController`; normal Skills use their
  payload mask. Migration preserved existing actor masks. The obsolete
  `MeleeHitboxTrigger` schema and one-time migration menus have been removed.
  Author new combos as SkillGemDefinition assets and assign the profile's
  `meleeSkill`, `lightMeleeSkill`, or `heavyMeleeSkill` slot. Legacy combo assets
  and profile fields were archived outside Assets and removed after reference checks.
- Roma/Milano's generic combos now have starter front-facing Box layouts. Their
  current dimensions are placeholders for tuning through the same authoring tool.


## Hitbox mode in Animation/VFX Timeline (2026-09-19)

In the **Animation / VFX** tab, timeline rows follow the selected source's
capabilities and saved data. Required source lanes remain visible even before
their markers are authored. **VFX** appears when cues or VFX markers exist;
**Block Window** appears when the skill has a profile or an enabled editor draft.
**Other Events** appears only for existing events without a dedicated lane,
including legacy/unknown names so they can still be inspected and removed.
Combined cutscene playback includes its reference animation's events and cues
when deciding which rows to show. Migrated melee steps without hitbox payloads
hide the Hitbox row unless it already contains events; Chain Window remains.
Right-click **Animation** or empty timeline space to add events or enable Block
for a supported main skill. The relevant optional row appears automatically;
row-specific menus and marker dragging continue to work as before.

Open `Tools > RB > Animation VFX > Edit Hitboxes`, or press **Open Hitbox
Timeline** on `SetSkillHitBoxData`. Both open the existing Animation Event VFX
Timeline with **Hitbox** selected. Keep the old Load/Save Layout buttons for
collider-based import/export workflows.

1. Right-click a character (or one of its bones) in the Hierarchy and choose
   **Edit Hitboxes**. You can also right-click a character prefab in the Project,
   or drag a scene character/prefab into the window's **Character** field and press
   **Use Selection** to use the currently selected character. Prefab assets open
   in Prefab Mode automatically. Setup reuses an existing `SetAnimationVfxData`
   belonging to that character, or adds one with Undo; it never saves the scene.
   Choose **Light / Heavy / Skills**, then an **Attack**. A fresh setup selects the
   first available attack; an existing source selection is preserved. Combo steps
   show their step number and animation name instead of execution-skill filenames.
   Skill choices include configured loadout variants, helper/chain/party attacks
   and serialized enemy skills; selecting one does not equip or cast it.
   Use **Refresh** after changing setup, or open **Setup...** to assign a source
   asset/entry manually and inspect the resolved Animator. **Source Asset** opens
   a searchable picker grouped by supported type: Skills and Combo Skills in Hitbox
   mode; Animation/VFX also includes Animation Profile and Cutscene. Search by name
   or asset path. Skills referenced by a selectable combo step are hidden from this
   list: choose the combo, then choose its step in **Entry**. Standalone skills
   remain visible; this does not delete or change any skill asset. A skill with no
   selectable owning step remains visible. Drag a supported asset onto the field,
   use **Ping** to locate it, or choose **None (clear source)** to clear it.
   Unsupported drops are rejected; source changes keep the existing draft prompt.
   Old MeleeComboSO assets were removed after migration. Skill filtering is by
   type, so an unconfigured source may still need a
   hitbox payload in the Skill Designer. The normal view keeps
   character and attack selection in two compact rows. A container with several characters
   is rejected rather than selecting an arbitrary member.
2. Select the embedded payload in the left **HITBOXES** browser, then a group and
   one of its shapes. Use **+ Group / + Shape**; the group's **...** menu duplicates
   or deletes it. The selected shape's inspector has Duplicate/Delete actions.
   Composite payloads have separate layouts. Payload composition remains owned by
   the Skill Designer; this mode adds, duplicates, removes and renames groups
   and shapes within each existing hitbox payload.
3. Choose Box, Capsule or Sphere. Edit transform, collider center, size, radius,
   height and direction in the right **SHAPE** inspector. **Scale & collider center**
   holds the less frequently changed fields. Use **Move / Rotate / Size / Scale** handles
   in Scene View. New shapes use the existing layout defaults (Capsule, radius
   0.5, height 1, Y axis, unit scale).
4. Under **ATTACHMENT > Follow**, choose Payload, CasterRoot or AnimatorRoot.
   Click **Bone** to open **Bone Picker**. Its body diagram uses balanced display
   proportions while every node still selects the original transform. Fingers
   (including their descendants) are hidden from both diagram and list. Weapon
   attachments are detached buttons at the sides and never affect body framing.
   Their anchors belong to the diagram: panning, zooming and orbiting move them
   with the body. They are clipped to the canvas instead of pinned to window edges.
   **Model pose** shows actual body coordinates; rigs with unrecognized body names
   fall back to model coordinates. The schematic supports common spine/limb names.
   Click a joint, segment or weapon button, or search/select a name in the left list.
   Twist bones are drawn in detached parallel lanes beside their limb, with both
   endpoints offset by 30 screen points per lane. Rendering and picking use the
   same separated geometry at every zoom; extra twist branches get their own lane.
   If several joints/segments overlap under the pointer, clicking opens a chooser
   with each bone's name and hierarchy path. This never automatically applies the
   first candidate, even on double-click; choose the bone, then Select Bone.
   The selected bone draws on top, and name labels avoid covering each other.
   Confirm with **Select Bone** or double-click; Cancel/Escape leaves the draft alone.
   Right-drag orbits, middle-drag pans and the wheel zooms. Front/Side/Top and Frame
   reset the view; Names toggles labels. **All transforms** includes sockets and
   unweighted transforms; the default shows skin bones, their ancestors and the
   current attachment. Models without skin bones show their full hierarchy.
   The picker closes when its source/model becomes invalid, on assembly reload or
   when entering Play Mode. It creates no preview model or animation player.
   You can also drag one GameObject or Transform from the character's Hierarchy
   directly onto **Bone**. It must be under the current Follow root; foreign
   characters, prefab assets, multiple objects and paths resolving to a different
   same-named bone are rejected. Dropping preserves the shapes' world pose and
   supports Undo/Redo; only the relative path is saved when you press Save All Changes.
   AnimatorRoot follows `CharacteContext.Visual.ModelAnimator` and the shared
   runtime fallback, including prefabs with a placeholder Animator on the actor.
   Changing anchors preserves the shape's world pose using Unity TRS conversion;
   rotated non-uniform scale can imply shear, which a Unity Transform cannot
   represent exactly. Inspect the result before saving in that case.
5. Scrub or use the shared Play/Pause, Loop and Speed controls. Active groups are
   green, the selected group yellow, other groups assigned to the selected Hit cyan,
   and inactive groups gray. Clicking a Hit selects its first assigned group. **Solo** and **Frame**
   help isolate geometry. Hitbox mode previews the main attack
   clip; its VFX use the same existing preview player and playhead.
6. The timing section stays below the independently scrolling browser/inspector.
   Drag the ruler or empty timeline to scrub continuously. Drag cyan/orange edges
   to resize, or drag the body of a hit bar to move both endpoints while preserving
   duration. Clip boundaries clamp the movement. Click a hit to show only its
   **Start (s) / End (s)**, duration and **Groups** selector. Each bar labels its Hit
   and groups; an orange underline warns about missing group assignments.
   Right-click empty space for **Add Hit Here** at the clicked time. Right-click a
   bar for **Duplicate Hit / Delete Hit / Assign Groups**. Duplicate preserves the
   groups, damage, knockback and remaining step settings separately for every
   affected payload, then selects the copy. It finds the next free gap (wrapping
   to earlier space if needed); invalid timing or insufficient space disables it.
   Delete/Backspace removes the selected Hit only while the timeline has keyboard
   focus and no text field is being edited. Shared timing edits/deletion affect
   all connected payloads; the affected names appear below the selected Hit.
   **Damage × / Override Knockback / Distance / Duration** edit only the selected
   payload's Hit. **More settings** shows hit policy, cache reset and knockback
   curve/reaction/interruption at the top of the right inspector. With Override
   Knockback off, this Hit emits no knockback data (matching the existing runtime).
   **+ Hit at Playhead** adds a window and assigns the currently selected group for
   this payload. **Shared timing affects** lists payloads sharing the edited event.
   New shared windows require group
   assignments in every affected payload; switch payloads to complete them. Switching
   payloads retains the corresponding selected Hit when they share an endpoint.
   Stable window IDs keep damage, knockback and other step settings attached when
   windows move past one another. **Repair markers** reveals a repair list in the
   right inspector for incomplete/overlapping imported timelines (normalized values).
7. Press **Save All Changes** below the source/attack selector to commit, **Revert** to reload Hitbox data, or Undo/Redo to change
   the draft. Geometry and hit events remain in an Editor-only draft until Save.
   Navigation and window closing offer Save/Discard/Cancel when the draft is dirty.
   Source changes made elsewhere block Save; use Revert to reload before editing.

Save validates duplicate/empty groups, missing bones, invalid shape dimensions or
scale, missing step references and incomplete/overlapping windows. It writes only
layout/step fields and the owned hit events in the selected Skill asset, preserving
other events and their callbacks. It uses `SaveAssetIfDirty`, never project-wide
`SaveAssets`. The skill and its hitbox payloads must already be saved together;
missing event-name assets must be created through the existing event authoring tool.
The shared **Save All Changes** bar appears in both Animation / VFX and Hitbox.
It replaces the window's Save VFX Data and Save Hitboxes buttons, and saves pending
VFX hierarchy edits, Hitbox drafts and Block drafts for the selected Skill/Step.
All pending sections are validated before the first write; errors identify VFX,
Hitbox or Block, and stale/foreign drafts prevent saving. **Saved / Unsaved changes**
includes hierarchy VFX edits. An entry whose VFX hierarchy has not been loaded
keeps its saved cues. **Load / Sync VFX Data** reloads only the selected entry's VFX;
it does not discard Hitbox or Block drafts. Other timeline edits, including Chain
Window dragging, continue to save on mouse release. Combo roots/other Steps and
unrelated dirty assets are not flushed by Save All. The Block context-menu save
and the component's VFX-only save remain available.

As Unity saves an entire asset file, other pending edits already in that same Skill
file are included; separate dirty assets are not flushed.

Scene geometry uses editor Handles only: it creates no attack collider or damage
runtime. Backward scrubbing and loops reconstruct active groups from the playhead
(start inclusive, end exclusive). Stop, changing characters/models, assembly reload
and entering Play Mode release the existing animation/VFX preview and restore the
sampled pose. Editing is disabled in Play Mode. Dirty drafts survive assembly reload.
No mesh fitting, damage simulation or automatic balance changes are performed.


Standalone animation models are also supported by **Edit Hitboxes**, including the
Rector model in GameSetup without a gameplay context. Setup resolves the model's
Animator and finds Character Stats through exact prefab references, falling back
to the same Avatar. Attacks from matching configurations are combined and duplicate
source/entry pairs are removed. Use **Setup... > Character Stats** to select one configuration
or supply stats for a model with no match. This adds only the authoring component,
not a gameplay context. The Hierarchy menu uses Unity's priority-10 registration
so the command is included in the right-click menu as well as GameObject.

### Defensive Block timing in the Animation / VFX view

`Assets/Prefab/GameEnemy/Enemy_Base.prefab` owns one enabled
`DefensiveBlockAttack` in Combat_System and binds the Context's shared
`DefensiveBlockAttack` reference. Enemy variants inherit this component, including
Rector and GR04; do not add another copy on each variant. Rector's former local
component has been moved to the Base. This supplies attack-side runtime support;
each Skill still needs its own saved **Can Be Blocked** setting and Block Windows.

Animation/VFX preview applies clip root displacement relative to the actor's
placed position and orientation. Rotating a character in the scene rotates its
preview movement too; scrubbing remains relative to the same starting pose.
The secondary camera clip retains its authored motion.

In Main Skill, right-click **Block > Add Block Window Here** for a Skill with Block disabled, then edit its timeline row below VFX. Drag **Block Open / Block Close** or
set either endpoint from the right-click menu. Use **Block > Save Block** or
**Block > Revert Block** in that same menu. There is no Block panel above the
timeline. Right-click within a window to target it and use **Block > Hitbox Steps**,
**On Success**, or **Remove Window**. Step numbers are zero-based; checked steps
belong to the clicked window. A gap offers Add instead of editing a different
window. Context menus in other rows contain only that row's actions: VFX, Hitbox,
Cast Point, range endpoints, or Other Events. Add another
window at a playhead in a gap, or use the context menu; Remove keeps at least one.
Choose **Continue Skill** to suppress only that window's hitboxes and continue the
enemy animation without enemy knockback, or **Interrupt Skill** for the original
skill cancel/knockback. Defender Impact/recoil and success feedback apply to both.
Windows must be ordered, non-overlapping and use distinct zero-based Hitbox Steps.
The suggested step on Add must be checked against the payload's actual timeline.
An empty `SkillDefensiveBlockSettings.windows` list retains the legacy single-window behavior;
existing Skills are not automatically retimed. Leave time for guard recovery and
safe placement between windows. Multiple payload executions sharing local step
indices still require separate authoring/integration; this adapter owns one bound
hitbox sequence per cast, as before.
The row shows an asterisk for unsaved changes. The draft supports Undo/Redo, and changing source or
closing the window prompts for pending edits. Each Skill stores its own inline
**Defensive Block** settings. Add enables **Can Be Blocked** only when saved; no
sub-asset is created. **Block > Disable Block** retains the authored windows and
settings. The Skill Inspector edits non-timing parameters directly; changes there
cause a conflict for any open Timeline draft, which must be reverted before saving.
Duplicating the Skill copies independent settings. Defender profiles remain on
Character Stats and continue to own animation, guard geometry, recoil and feedback.
See [Defensive Block authoring](SYSTEMS/DEFENSIVE_BLOCK_TEST.md#editing-block-timing-in-animationvfx-timeline)
for timing semantics, conflict handling, and the distinction from Pre-Cast events.

### Timeline preview keyboard and mouse controls

Focus Animation Event VFX Timeline, then tap **Space** to toggle Play/Pause at the
current playhead. Hold **Space** and move the mouse left to scrub backward or
right to scrub forward, without clicking. Tiny pointer jitter during a tap is ignored.
The movement is relative to the
current playhead (no jump to the clicked marker), clamps at the clip ends, and
pauses playback. Releasing Space leaves the preview paused. Space repeat
and release after a drag do not toggle playback. Escape cancels the gesture.
These controls work in Animation / VFX and Hitbox modes; the combined Animation /
VFX ruler can scrub backward from the main clip into its preceding cutscene.
Text/numeric editing, other focused windows and Unity Play Mode do not consume
these shortcuts. Space-drag takes priority over event/Hitbox/Block marker edits;
it changes only preview time and never saves authoring data.

### Optional Block Impact authoring

For Timed Approach or Cooperative, each `SkillDefensiveBlockSettings.windows` entry
has an optional **Use Impact Marker** toggle and **Impact Normalized** time. Leave
it off for existing approach timing. When on, use `Close <= Impact < 1`, before the
next window and the animation's authored end. Contact does not use this marker.
The Timeline can create the single-window data automatically when saving a marker.

In Animation/VFX Timeline use **Block > Impact > Window N > Set Impact Here**, drag
the pink upper handle, or edit the selected window in **Timed Approach Settings**.
Save Block/Save All persists the draft; Revert discards it; edits support Undo/Redo.
Removing the marker returns that window to default timing. No prefab, new animation
clip, or HUD binding is needed. See `SYSTEMS/DEFENSIVE_BLOCK_TEST.md` for runtime
semantics and the test harness validation command.

### Block animation ownership

Block animation is inline in `CharacterAnimProfileSO.block` (`BlockAnimationSettings`).
Open the character's `CharacterStats.animProfile` and expand **Block** to edit Begin,
Impact, optional Cooperative Struggle, sampled normalized ranges, phase durations
and blend time. `CharacterAnimBrain` resolves its normal character/override profile;
the controller reads this same source for readiness and starts playback through
`CharacterAnimDriver.TryBeginBlock(requestId)`. Each playback snapshots the settings.

`DefensiveBlockActorProfile` owns gameplay, placement, recoil movement and presentation
settings. Its `IsConfigured` checks those settings; guard readiness separately needs
valid clips in the character Animation Profile. Player's default guard settings do
not supply animation. Give new playable characters their own Block data as well.

Existing Aires, Roma, Feno and Milano animation profiles were migrated with the
previous clips and timing. Their inline settings can now be edited independently.
The hidden legacy `DefensiveBlockActorProfile.animation`, controller field and old
`BlockAnimationProfile` assets are retained for serialization/migration compatibility;
normal Block gameplay does not read them. The old explicit driver overload remains
for source compatibility. Do not author new animation there.

**Tools > RB > Defensive Block > Migrate Animation To Character Profiles** copies
legacy data into empty character profiles, supports Undo, preserves existing authored
Block data and rejects conflicting character bindings. It also migrates the four
existing player profiles from Player.prefab's old fallback. It is safe to rerun.

## StateHub debug migration and pause ownership

Run **Tools > RB > StateHub > Migrate Debug Views** to add StateHubDebugView and
copy debugInInspector, logTransitions, showOnScreen and screenPos from legacy
StateHub fields. Source prefabs are processed before variants; scene overrides
are copied to the corresponding view. Repeating the migration is a no-op when
legacy values are unchanged, and preserves later edits to the view. Unsaved
scenes are skipped and listed. Legacy fields remain serialized until migration
and reference checks are complete. Character context bindings are checked during
migration. Unity may normalize serialized defaults/order while saving fixtures.

Author debug options on StateHubDebugView after migration. Runtime-generated rigs
receive a view during context preparation if none exists. The view displays
states, blocked-action reasons and control/UI token holders.

UI states no longer pause time as an enter/exit side effect. A gameplay screen
owns both a StateHub UI-state token and a GlobalTimeScaleManager pause token.
ActiveSkillScreenController releases its tokens on close, disable, destruction
or rebinding; closing one screen cannot release another screen or Dialogue's hold.

## Composed passive authoring (2026-10-04)

Keep PassiveController, StatsHub, StaminaSystem, HealthSystem and CombatEventBus
bound through the actor's CharacteContext. Modules may be on the root or nested;
StatsHub includes the context-bound PassiveController even outside its own subtree.
Do not add Momentum or passive-specific counter fields/components to characters.

Edit PassiveDefinition.recipe after migration. Legacy Triggered/Custom fields are
retained migration input and no longer drive gameplay. Recipes/fragments are
ScriptableObjects with SerializeReference steps; retain rule/step/invocation IDs
when reordering and bind shared variables explicitly. New providers should return
PassiveGrant with stable source/installation IDs.

Use Tools > RB > Passives > Dry Run Recipe Migration before Migrate Recipes.
Migration is repeatable and retains definition GUIDs, upgrade trees and references.
No scene/prefab replacement is required. Optional movement event sources are
created only on demand; author at most one source of each kind per actor.

The RomaStaminaMomentum asset under Assets/Tests/PassiveComposition/Fixtures is
test/example data only. It has not been assigned to production Roma. Full managed
reference authoring UI and node graph are outside this change; use typed recipe
data and the example factory for authoring/validation.

### Roma Stamina / Momentum installation

ChaDef.Roma now has a passive slot after its Ultimate, with stable slot ID
`roma.passive.stamina-momentum`. It selects the production
`Assets/Character/Roma/Passive/Passive.Roma_StaminaMomentum.asset` by default.
The shared Player prefab resolves this through CharacterSkillManager; no passive
is hard-coded into the Player prefab or added to other character definitions.
The production asset owns its recipe and four fragments independently of Tests.

The production definition references `Roma_StaminaMomentum_UpgradeTree.asset` in
the same folder. `RomaCombatSurgeAuthoring.Install()` installs the base recipe and
all nine upgrade nodes together; repeated installation preserves IDs/GUIDs and
does not duplicate nodes or strip upgrade rules. It reapplies default tuning.
The tree uses three columns: Dodge left, continuous fire middle, low Stamina right.
Each column progresses from two 1-point nodes to a 2-point major node; minor scale
is 1.0 and major scale 1.5. Only prerequisites within that column create links.
The existing Tree view displays progression bottom-to-top. Nodes use the same
`Assets/Spain/Skill_Icon/Global` sprite library as the Aires and Feno trees.
In prerequisite order, Dodge uses Skill_15600/4050/5100, continuous fire uses
Skill_1090/5040/1000, and low Stamina uses Skill_4020/13260/2080. The authoring
helper preserves this mapping when reinstalling defaults.

Use the existing purchase/save/reset flow and shared skill points. No Player
prefab fields or extra HUD gauges are required. The existing node drawer displays
the selected node description, fits passive text within its description area,
and resolves prerequisite labels to node display names. Non-passive description
font settings are restored when switching back to an active skill.

### Weapon passive authoring (2026-10-05)

Keep the existing WeaponAffixRuntimeController component and serialized database
reference on weapon-bearing prefabs. It now supplies grants to the context-bound
PassiveController; no per-affix MonoBehaviour is needed. WeaponAffixDefinition
references a ComposedPassiveDef/recipe and owns rollSpec, tooltip and eligibility.
The optional passive hudPrefab uses the existing GameplayHUD/PassiveHUD host;
weapon affixes do not require a HUD prefab. Roma's HUD and placement are unchanged.

Older actors such as AI Ally/Ally.prefab can omit the shared passive module. The
equipment adapter creates it when an actual affix grant requires it; combat/weapon
phase recipes also install a missing CombatEventBus through the owner context.
No extra module is installed for an empty weapon, and static-only grants do not
need a combat bus. Prefer explicit context bindings in newly authored prefabs.

Use WeaponPassiveRecipeMigration.MigrateAll(true) for a dry run, then false to
migrate. Existing asset GUIDs, names, roll values, feedback profiles and external
references are preserved. Legacy rootBehavior fields remain for reference and
migration compatibility but cannot construct gameplay runtimes. New formulas use
recipe parameters, bindings and steps. Never put affix counters on CharacteContext.

### Skill origins formerly bound to the actor FirePoint

These SkillUserSystem bindings now enable `useWeaponMuzzleForCastOrigin` and clear
the old transform reference. CastOrigin reads the currently equipped muzzle through
context, so swapping weapons cannot leave a stale skill origin. With no weapon
muzzle, non-weapon skills use their authored castOrigin or actor root. Explicit
skill-only cast origins remain supported. AI aimTransform references that used
the old muzzle now use the driver's aim target/runtime override. Skill trajectory
modules and targeting policies are unchanged.

## Stat modifier scope authoring

Choose `Global` for whole-character bonuses and `WeaponOnly` for the gun's own
contribution. Flat uses stat units, AddPercent 20 means +20%, Multiply 1.2 means
x1.2. Three Multiply stacks in one group give x1.6; different groups multiply.
Damage/Crit upgrades and migrated weapon affix stat bonuses are WeaponOnly;
HP/movement/resource bonuses remain Global. Do not use WeaponOnly for stats that
are not consumed by the weapon stat pipeline.

Passive entries, dynamic recipe steps, accessory stats, status modifier entries
and upgrade entries expose scope. The existing status application drawer and
status authoring wizard preserve scope when copying a definition into an override.
An application override is independent of its definition after editing.

Migration menus: `Tools > RB > Stats > Preview Scope Migration` and `Apply Scope
Migration`. Apply is schema-versioned/idempotent and uses native asset serialization.
`Tools > RB > Stats > Validate Modifier Scopes` reports unsupported scope/stat
pairs across accessory, status, upgrade, recipe and fragment assets/subassets.
Changed affix graphs are isolated from existing references; do not delete their
original subassets as part of migration. Existing saves keep weapon IDs, rolls and
upgrade levels. This change requires no character prefab reference cleanup.

## Rendering after ASP removal (2026-10-08)

ASP is retired. Do not add ASPCharacterPanel, ASP renderer features, volume
components, or ASPSmoothModel importer markers. ZLZ remains the character shader;
PC/Mobile renderer features remain ZLZ plus the existing PC SSAO/MK Toon features.

- Nine remaining ASP/Character materials were migrated in place to
  ZLZ/AnimeToon/Character, retaining material GUIDs, base textures/colors, UV scale
  and offset, culling and alpha cutoff. ASP-specific shading effects are not an
  exact visual match; final art tuning is required on these legacy materials.
- ASP panels were removed from 16 prefabs (17 components), and six ASP renderer
  features plus five ASP volume overrides were removed. An orphaned missing
  ASPScreenSpaceLensFlareFeature subasset was also removed.
- Shared URP settings now live in Assets/Settings/ProjectURP with unchanged GUIDs.
  BasementSkybox.mat is in Assets/Skyboxes; the referenced sky HDR, default ramp
  and diamond matcap are preserved under Assets/Textures/RetainedArt.
- Assets/ASP contains authored ramp textures, not the retired plugin. These are
  retained as art assets. Existing smooth-reference FBX files are retained; the
  obsolete ASP userData markers were cleared on 18 model importers.
- Assets/Plugins/ASP and its separate ASPLocalizationData asset were removed.
  Model importing no longer runs the old ASP smooth-normal postprocessor.

## Character accessory slot artwork

`Assets/Spain/UI/CharacterMenuAssosry/Slor All/Slor All.prefab` contains the five
accessory slot illustrations as 20 separate uGUI Images plus five dummy item Images. The root Canvas uses
Screen Space Overlay and a 1920x1080 Scale With Screen Size reference (match 0.5).
All source PNGs retain their full-canvas transparent padding: keep each Image
stretched to its slot group, with zero offsets and no extra rotation.

The PSD top-to-bottom group order is reversed for Unity drawing: Slot 4, Slot 2,
Slot 3, Slot 1, Slot 5. Within each group, draw Disclipstion_Color first, then
BG_Disclipstion, BG_Icon_Color, and Icon Fame_Color. Original asset spelling is
preserved. These Images are decorative and have raycastTarget disabled. This
prefab contains no scene background, character, BACK button, or
interaction logic. Instantiate the prefab to display the complete artwork;
its groups and individual layers remain editable. A Unity camera preview was
checked at 1920x1080; gameplay integration has not been exercised.

Each slot includes a decorative `DummyItemIcon` between its icon background and
frame, using existing sprites from `Assets/Spain/Accessory_Icon/Chibi`. Slots
1-5 show RabbitLag, RabbitHat, CasinoLuckyCoin, FenosFieldPack, and GangSignetRing.
These are static preview assignments, not runtime inventory bindings. Replace
the Image sprite or disable the child to change the dummy. Icon anchors preserve
the reference positions; individual sizes and rotations match the tilted frames.

## Gun menu artwork

`Assets/Spain/UI/Gunmenu/GunMenu.prefab` is a standalone uGUI Canvas using Screen
Space Overlay and a 1920x1080 Scale With Screen Size reference (match 0.5).
The Artboard uses Fit In Parent at 16:9, preserving the artwork on wider or
narrower screens. Instantiate it as a scene root, with one active EventSystem
and a compatible input module in the host scene.

The 12 separate artwork Images retain their original full-canvas transparent
padding. Unity back-to-front group order is Guninventory, GunStatus, Slot_Gun,
Information Button, Back. Within each group the PSD order is reversed. Preserve
zero offsets, full stretch, and no added rotation on artwork Images. The source
PNGs are imported as single Full Rect sprites without mipmaps or compression.
Back/Black.png was copied from the matching Gunmenu PSD export to complete the
reference. The scene/character background and hidden PSD layer are excluded.

Back/HitArea and Information Button/HitArea contain Buttons with bounded,
normalized transparent raycast Images. Decorative artwork does not intercept
pointer input. Button targetGraphics reference their respective artwork Images;
onClick events remain unwired. Dummy_GunItem and Dummy GunIn slot Icon are static
preview artwork, and the scroll illustrations do not implement scrolling or
inventory selection. Gameplay integration is not part of this visual prefab.

Validation: reopened the saved prefab, checked all 12 sprite references and
missing scripts, rendered Unity camera previews at 1920x1080, 2560x1080, and
1440x1080, and checked button bounds at each aspect ratio. No new Console errors
were reported. No project C# sources were changed, no gameplay tests were run,
and Play Mode integration remains unverified.
## Basement GunMenu screen

The Basement character status panel's existing EQUIPMENT button still calls
`UILoadLaval.OpenWeaponEquipment`. Its new `gunMenuScreen` scene reference routes
that action to `BasementGunMenuScreenController`; do not add a second entry button.
Callers without the new reference retain the legacy equipment panel. The
ACCESSORY entry and its picker remain separate.

`Assets/Prefab/User Interface/BasementGunMenuScreen.prefab` is a variant of the
GunMenu artwork prefab. It adds a hidden CanvasGroup, controller and four black
letterbox bars. Keep the original 16:9 Artboard and all 12 sprite layers. Back's
localized HitArea calls the controller's `Back`; Information is reserved and inactive. The live inventory grid, assignment flow and scrolling are
described below in Live weapons in Basement GunMenu.

The Basement instance binds `cameraManager`, `characterSelection`, and the same
backgroundRoots as the accessory screen (UI, CanvaCharactorInfo, MapUI). Opening
uses the selected PartySlot, cancels dragging, caches/disables background Canvas
and GraphicRaycaster components, and fades the other party members over 0.45s.
The controller reuses BasementAccessoryRendererFade without editing shared
materials. Back/Escape works after the opening transition completes; close or
teardown restores the original enabled states, selection and camera settings.
The selected character keeps its existing Basement animation and weapon model.

`GunMenuCamera` is a CinemachineCamera in the Basement scene, using the existing
output camera and brain. `Aires_camera_Gunmenumene.fbx` is an authoring reference
only. Its root-local camera offset is (-0.399025, 2.098622, 1.953272), with Euler
(13.439820, 189.280000, 0.000014); apply these relative to PartySlot.PresentationRoot
without the marker's FBX import-axis rotation or 100x scale. The physical lens is
26 mm, 36x24 sensor, 49.55028-degree sensor FOV, Horizontal gate fit and zero shift.
The existing BasementAccessoryBlends asset adds 0.8s EaseInOut rules to/from
GunMenuCamera. The default 2s map blend and existing accessory rules are retained.

CameraManager owns one presentation session at a time. BeginGunMenuView and
EndGunMenuView use the same internal transition/lens/viewport restoration as the
unchanged accessory API. GunMenuViewport and GetGunMenuRevealProgress expose the
camera's fitted 16:9 viewport and the final 0.5s UI reveal. Menus cannot overlap
one another or open from the map view. Timing uses unscaled time. Closing during
a reveal cancels it; missing/disabled targets and scene teardown restore state.

Validation entry points: BasementGunMenu.Tests and BasementGunMenu.PlayMode.Tests,
plus BasementAccessory.Tests and BasementAccessory.PlayMode.Tests for regression.
The GunMenu fixture uses temporary objects and a backed-up test save slot for equipment checks.
### Live weapons in Basement GunMenu

BasementGunMenuScreen's weapons reference now points to BasementGunMenuWeapons.
UILoadLaval passes its BasementContext inventory when opening the selected party
slot and listens to EquipmentChanged to recalculate the status panel. The
standalone source GunMenu art prefab remains the original layered reference;
the Basement variant disables only its dummy inventory, dummy equipped icon and
painted scrollbar, replacing them with interactive children.

WeaponScroll uses a rotated, masked uGUI ScrollRect with four 204px card columns,
10px gaps and a matching scrollbar. BasementGunMenuWeaponCard is the reusable
cell prefab. The view reuses cells across refreshes/reopens, binds individual
weapon instance IDs, with ownership restrictions and a gold equipped border. Clicking a
free weapon calls EquipmentAssignmentService.TryEquip with the selected
character's owner ID. The service enforces existing ownership/save rules; this
screen never transfers another character's weapon implicitly. Duplicate clicks
on the current instance do not equip or save again.

After successful equip the view refreshes PartySlot.RefreshSelectedWeaponVisual,
the equipped icon and weapon-only stats from WeaponStatPreview, then raises
EquipmentChanged. Only the weapon model changes; character animation and the
reference camera stay intact. Inventory slot/reset and equipped weapon events
queue refreshes, and bindings are removed on close/disable. The click handler
revalidates the selected owner and CanInteract gate before every operation.

Hovering a list card or the equipped slot shows a white dashed outline immediately
and InventorySlotTooltipUI after 0.55 unscaled seconds, with that weapon's upgrades
and affixes. Leaving or dragging dismisses it. Information has no callback and is
inactive, reserved for a future screen. Back/Escape first dismisses visible details,
then closes the GunMenu. An empty inventory or unavailable inventory has an explicit
message; missing icons retain the card hit area. No unequip command or
new save schema is introduced by this screen.

GunMenu list reference refinement (2026-10-10): weapon card art now stretches to
200x200 inside the existing 204x204 card with a 2px selection border. Paper, Name
and State child objects are retained for binding but hidden to match the image-only
reference. Equipment ownership/button gating is unchanged. WeaponScroll starts at
(1216,-340) in the existing rotated artboard; the four-column grid and fade remain.

GunMenu bottom clipping correction (2026-10-10): WeaponScroll viewport and
LiveScrollbar height are 640 rather than 750, ending inside the lower black frame.
The existing Mask clips both rendering and pointer hits. Content height is retained
so all inventory rows remain reachable by scrolling.

GunMenu hover and drag (2026-10-10): BasementGunMenuWeaponHover binds card/slot data
and owns the delayed tooltip and highlight lifecycle. HoverDashes uses a single
non-raycasting BasementGunMenuDashedOutline mesh under the existing mask.
BasementGunMenuWeaponCard routes left-button drag events to the view, which creates
a non-raycasting drag image on the root Canvas. BasementGunMenuWeaponDropSlot
on EquippedWeapon accepts that screen's active drag; it uses the same Equip path
as a click. List cards also accept drops to swap inventory positions. Drop outside these targets, close, disable and source removal cancel the drag.
All inventory weapons can be dragged for sorting, including equipped weapons. Equipping another owner's weapon remains blocked.
The legacy InventorySlotUI reference on EquippedWeapon is retained as an anchor but disabled
to avoid duplicate pointer handlers. Its transparent Image is the hover/drop hit
area, including when the slot has no equipped icon. Information's persistent
listeners are empty and its hit Image is not a raycast target.

GunMenu drag/owner visual parity (2026-10-10): BasementGunMenuWeaponCard now has a
CanvasGroup; active drag fades its source card to alpha 0.35 and restores alpha 1
on cancellation, successful drop, refresh/reuse or close. DraggedWeapon follows
pointer coordinates with a (20,-20) Canvas-local offset, matching DragItemUI.
Each card's 40x40 EquippedCharacterIcon is anchored bottom-right and has raycasts
disabled. It is hidden for unassigned or unresolved owners. The screen's serialized
characterDatabase points to the same CharacterDatabase asset used by existing
character UI; character owner IDs resolve via CharacterEquipment parsing, the
bound selected character first, then database.GetById (including off-party owners).
HoverDashes remains the final sibling. Equip ownership restrictions, click-to-equip,
tooltip delay, Information placeholder and the image-only diagonal grid remain.

GunMenu list reordering (2026-10-10): cards implement IDropHandler and route the
active drag to BasementGunMenuWeapons.DropWeaponOnCard. Weapon instance IDs map
the filtered grid back to actual inventory indices before PlayerInventory.MoveOrSwap;
empty slots and other item types do not shift the mapping. Successful swaps save
inventory order through SaveManager.SaveInventoryOnly and refresh card bindings.
Both equipped and unassigned weapons can be reordered, while ownership, equipped
instance, character presentation and EquipmentChanged remain untouched. Portraits
and the gold equipped border follow the instance. Self drops do nothing. An owned
card's Button remains non-interactable for equip clicks; its drag/drop handlers
remain enabled. No prefab or save-schema migration is required.

## Supply preview in Test Supply (2026-10-15)

Open `Assets/Scenes/Test Supply.unity` and enter Play Mode. The scene uses its
existing Neria model, `SoloAnimation` supply clip, and
`Character/Camera Supply/Camera Supply` camera. Its authored lens and pose are
retained; the unused `Main Camera` Camera component is disabled. The original
scene geometry and character setup remain in place. No additional preview scene
or Build Settings entry is created.

`BasementSupplyPreviewCameraFrame` on the Supply camera fits its viewport to the
same centered 16:9 area as the UI. This preserves character framing on ultrawide
and taller displays without changing the authored camera pose or lens.

`Assets/Prefab/User Interface/BasementSupplyScreen.prefab` owns the overlay Canvas,
1920x1080 centered artboard, outside letterbox bars, launch button and SupplyScreen.
CanvasScaler uses Expand. The root artboard mask clips the rotated artwork;
the item viewport has its own rotated Mask. The grid uses seven 172px cells,
8px gaps and 200px bottom padding so the final row can scroll fully into view
at the left edge of the sloping screen. Decorative graphics do not receive
raycasts. `BasementSupplyItemCard.prefab` owns each selectable card.

The item selection uses the supplied `Selectio Item Frame.png` red dashed art.
Its `SupplyItemSelection` sprite rect is (1135,365,200,201), in bottom-left
source-pixel coordinates. The selection Image cancels the baked 8.1-degree angle
locally and fits the 172px card; the rotated grid supplies the final screen angle.
It is maskable and does not receive raycasts. `AgentScripts/BasementSupplySelectionSetup.cs`
updates the card prefab and cached cards in both Screen and Room prefabs. Preserve
the source PNG and update all three prefabs when changing this decoration.

The `BasementSupplyScreenController` on SupplyScreen references the existing
ItemDatabase asset in the Inspector. It reads `items` in database order, skips
null and repeated references, and displays ItemDefinition icon, displayName,
description and baseBuyPrice. The three tabs are All, Weapons and Accessories.
Each open/category switch resets to the first item and top of the list. Long
descriptions have a separate vertical ScrollRect; missing artwork and empty
categories show placeholders. BACK/Escape closes the screen; OPEN SUPPLY opens
it again. The public `Open()`, `Close()`, `SetCategory(int)` (0/1/2) and
`SelectItem(ItemDefinition)` methods only change preview UI state.

Category tabs now use the supplied `Category Icon Item Type`, `Category Icon
Dummy`, `Category Icon Dummy 2` and `Category Selection Frame` sprites, without
generated color panels, labels or procedural category outlines. Orange is All,
blue is Weapons, grey is Accessories. The selected tab expands outward and its
white frame stays visible until another category is selected. Other tabs retract
behind the paper while retaining an exposed clickable strip. The controller's
`categoryTabs`, `categoryExpandedPositions` and `categoryPaper` bindings control
position, scale and draw order. Artwork retains its baked angle; hit areas follow
the same angle. `AgentScripts/BasementSupplyCategorySetup.cs` records the prefab
upgrade; apply it after the original one-time setup if rebuilding from scratch.

BUY is deliberately non-interactable and has no purchase listener. This preview
uses `BasementSupplyBuyHover` on BuyDisabled to show its red raster outline only
while hovered; leaving the button or closing/reopening the screen hides it.
The outline itself does not receive raycasts. This preview
does not bind PlayerInventory, ShopService or ShopPanelUI, roll weapon instances,
or write saves. Base prices are labelled as such, not live shop offers. Production
Basement shop and character-unlock flows are unchanged.

Artwork comes from `Assets/Spain/UI/Basement Supply UI`, imported as transparent
sprites without mipmaps or NPOT resizing (single layers except the cropped item
selection sprite). Raster layers retain their
original canvas coordinates and baked angle. A cream UI Image supplies the
missing paper base; BACK uses Novecento TMP, while live information uses Sarabun.
The flattened Dummy Item images are references only, not interactive content.

`AgentScripts/BasementSupplySetup.cs` records the one-time MCP authoring process.
It refuses to overwrite an existing Supply Preview UI root: edit the existing
prefab for further changes instead of rebuilding the scene hierarchy.

## Supply Room in production Basement (2026-10-15)

Start through `Assets/Scenes/GameSetup.unity`, then click SHOP in Basement.
`BasementContext.SupplyRoom` now routes that button to a read-only Supply room
visit. Unbound contexts retain the original ShopPanelUI route. BUY remains disabled;
no purchase, inventory-instance generation or save migration is introduced.

`Assets/Prefab/User Interface/BasementSupplyRoom.prefab` captures the user's current
Test Supply room, including Neria/SoloAnimation, camera, walls, floor, props and
the nested Supply UI prefab. Its Basement instance is translated to approximately
(91.51, 1.71, 9.68), leaving a 20-unit X gap beyond existing renderer bounds.
Internal transforms and the camera lens are retained. The separate Test Supply
scene remains editable. No new Build Settings entry, Directional Light,
EventSystem or AudioListener is added.

`Supply Room Controller` stays active outside the initially inactive room.
Its `BasementSupplyRoomController` binds the Basement context, room, screen,
Supply camera, existing output cameras, background canvases and full-screen
fade CanvasGroup. Fade covers all aspect-ratio bars and UI at sorting order 32767.
Defaults are 0.25 seconds to black, one frame at the switched view, and 0.30
seconds back, using unscaled time. Scene UI's root CanvasScaler now uses Expand
at 1920x1080 so the existing SHOP button also remains reachable at 1280x720.

The controller exposes Open(), Close(), IsOpen, IsTransitioning and Phase
(Closed/Opening/Open/Closing). Repeated transition commands are ignored; map
selection, character presentation and NPC presentation reject entry. It snapshots
camera/canvas enabled states, raycasters, character selection input and EventSystem
navigation/selection. At full black it switches output cameras and opens Supply.
BACK/Escape reverse the transition and restore those snapshots. Disabling the
controller in any phase also restores input/view and clears the black overlay.

Supply Screen's RequestClose()/CloseRequested event routes BACK and Escape to
the room controller. Open()/Close() retain immediate UI behavior for the controller
and standalone previews. With no CloseRequested subscriber, RequestClose() closes
the preview directly. The Basement instance disables the preview OPEN SUPPLY
launcher; do not enable that launcher in production. Keep the room controller
outside the room/UI hierarchy so it can finish transitions while those are hidden.

`AgentScripts/BasementSupplyRoomSetup.cs` records the one-time authoring operation
and refuses an existing room. Subsequent edits should target the existing prefab
and bindings rather than rebuilding. This production integration supersedes the
earlier preview-only note that Basement's SHOP route was unchanged.
