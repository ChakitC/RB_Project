# Skill System

## Asset Ownership

Each active skill is authored through one visible `SkillGemDefinition` asset.
The skill owns exactly one `SkillPayloadDef` sub-asset stored in the same `.asset`
file. Standalone or shared skill payload assets are not part of the supported
authoring workflow.

`SkillGemDefinition` owns:

- identity, tags, description, and icon
- base stats and per-level overrides
- animation, cast timing, and pre-cast configuration
- Animancer-driven timeline VFX events and their local placement data
- the embedded execution payload

`SkillInstance` owns per-character runtime state such as level, support gems,
calculated stats, and cooldown timestamps. Runtime state must not be written to
the definition or its payload.

## Character Skill Loadouts

Character default active-skill loadouts are authored on `CharacterStats`.
Each `CharacterSkillLoadoutSlot` defines a stable `slotId`, optional label,
hotkey, default option index, and any number of selectable
`CharacterSkillLoadoutOption` entries. Each option points at a
`SkillGemDefinition`, level, support gems, and optional `optionId`.

`CharacterSkillManager` resolves command slots at runtime from
`ctx.baseStats.skillSlots`. `CharacterStats` is authoritative for every slot it
defines, even when the prefab has a `CharacterSkillManager.autonomousSlots` entry
with `skillAsset` assigned at the same index. Prefab-authored
`autonomousSlots` entries are used only as legacy fallback slots when
`CharacterStats` does not define that index.

Runtime skill switching uses `CharacterSkillManager.TrySelectSkillOption(...)`.
The manager rebuilds the affected `SkillInstance`, cancels any pending cast from
that slot, and persists `{ slotId, optionId }` to the owning character progress
when requested. It never writes the selected option back into the
`CharacterStats` asset.

When `optionId` is empty, runtime lookup falls back to the selected
`SkillGemDefinition.skillId`. Keep option ids unique within one slot so saved
selections can be restored after reload. Invalid saved selections fall back to
the slot's default configured option.

## Creating And Editing Skills

Create skills through `Assets > Create > Game > Skill Gem`. The command creates
a `SkillGemDefinition` with an embedded projectile payload. Select another value
under `Execution Type` to replace it with a different payload type.

The raw payload object reference is intentionally hidden. Use the Execution
Authoring controls on the skill inspector to:

- create a missing payload
- change the execution type
- remove the current execution payload

External payload assets are unsupported. Remove an external reference and create
a new embedded execution payload instead.

Changing execution type deletes the previous embedded payload after user
confirmation. Duplicate skill assets must be checked to ensure the duplicate
references its own embedded payload rather than the source skill's payload.

## Supported Payloads

Current payload implementations are:

- `ProjectileSkillPayloadDef`
- `PrefabHitboxSkillPayloadDef`
- `ApplyStatusSkillPayloadDef`
- `SpawnPickupSkillPayloadDef`
- `MorphSkillPayloadDef`

Reusable dependencies remain normal asset references. Examples include
projectile prefabs, `ProjectileConfig`, `StatusEffectDef`, audio cues, VFX
prefabs, and pickup prefabs.

### Morph / Awakening Payload

`MorphSkillPayloadDef` is a temporary form change: a visual swap plus an optional
self status buff. It never writes to `CharacterStats`/`baseStats`, weapon data,
hitboxes, or scaling. Visuals change through an override layer and stats change
through the `StatusEffect` layer (`StatusEffectController` already feeds `StatsHub`
as an `IStatModifierProvider`). The payload applies at the normal skill cast
moment, defers the actual animator/model swap by one frame, and reverts
automatically after `duration`.

`Change Mode` controls which visual data changes:

- `AnimationOnly`: applies a temporary `CharacterAnimProfileSO` override.
- `ModelOnly`: rebuilds the character model from `Morph Model Prefab`.
- `Both`: changes the model and animation profile together.

Changing the model or animation profile causes `CharacterAnimBrain` to rebind on
the next update. Any active skill animation is intentionally interrupted at that
cast moment and returns to the locomotion state for the new form. If the cast is
interrupted before the deferred apply runs, the runtime host is destroyed without
leaving a morph active. If the character dies or is despawned during morph, the
host shuts down, clears the override, and removes any morph status.

Author morph skills as embedded payloads on the owning `SkillGemDefinition`.
Set a positive `duration`, assign `Morph Anim Profile` for animation-changing
modes, and assign `Morph Model Prefab` for model-changing modes. The optional
controller and avatar fields override the model animator runtime controller and
avatar; when they are empty, the current `CharacterStats` values are used.

Morph can also apply status effects to the caster for the transformed duration.
Fill `Status Effects (while morphed)` with one or more `StatusEffectDef` entries
(each with a stack count). They are applied through `StatusEffectController` when
the morph activates and removed when it reverts (on `duration`, interrupt before
apply, or death). Status effects that carry stat modifiers are how a morph raises
stats such as attack, defense, or speed without touching `baseStats`. The morph
owns the buff lifetime, so author the `StatusEffectDef` as permanent (or with a
duration at least as long as the morph) and give it an `effectId` that does not
collide with normally applied buffs, because `RemoveEffect` matches by definition
reference and then by `effectId`.

## Timeline VFX

Skill-level animation VFX are authored in the scene or Prefab Mode through
`SetAnimationVfxData`, with the `SkillGemDefinition` selected as the source and
entry `main`. The serialized runtime list remains owned by
`SkillGemDefinition`, but it is hidden from the normal skill inspector and is
written through the shared `Save VFX Data` action.

Each authoring entry binds a zero-based VFX cue index to one of these actions:

- `OneShot`: spawn the assigned prefab once
- `StartLoop`: spawn and retain a VFX prefab under a required loop key
- `StopLoop`: stop every active VFX instance stored under the matching loop key

Placement is stored as local position, local Euler rotation, and local scale
relative to the selected anchor. Supported anchors are caster root, cast origin,
aim transform, a child path under the character root, and a Humanoid bone.
Each cue also selects an anchor mode: `WorldSpace` resolves the spawn pose once,
while `FollowAnchor` parents the spawned instance to that anchor after spawning.

`SkillVfxAuthoringSlot` owns one timeline cue index and contains any number of
`SkillVfxAuthoringEntry` children. Each entry owns one VFX action, anchor, loop
settings, and one direct prefab-instance child that supplies the prefab asset and
visual placement. Saving flattens all entries back into `SkillVfxEvent` records;
entries in the same slot receive the same cue index. Runtime does not read
`SetAnimationVfxData`, slots, or authoring entries.
`SetAnimationVfxData` tracks the source asset and entry that own its current
scene hierarchy. Changing the assigned Skill immediately replaces the current
VFX slots and entries with data rebuilt from that Skill. Create/Sync performs
the same replacement every time, preventing stale scene entries from being
saved into another Skill. Unsaved hierarchy edits are discarded by replacement
and can be restored with Unity Undo.

The skill definition collects timeline requirements from both its execution
payload and its VFX entries. Projectile, helper, and chain skills therefore bind
their VFX Animancer events even when the payload itself does not require hitbox
events. `SkillVfxPresenter` maps animation request ids to sessions owned by the
shared `AnimationVfxPresenter`; the shared presenter does not depend on
`SkillGemDefinition`. Loop instances are isolated per session and are stopped
when that request ends or is interrupted.
Request completion and interruption clear remaining loop groups immediately.
Graceful particle completion is reserved for explicit `StopLoop` cues.

Use the same Animancer event name, `Vfx`, at every VFX time in the clip. Runtime
maps occurrences to cue indices in chronological order: the first `Vfx` event is
cue 0, the second is cue 1, and so on. Multiple entries may share one cue index
when several actions should run at the same occurrence. There is no numbered VFX
event-name compatibility path.

The editor timeline at
`Tools > RB > Animation VFX > Animation Event VFX Timeline` uses a scene or
Prefab Mode `SetAnimationVfxData` target. The old Skill menu is an alias to the
same window. It samples the assigned Skill Definition's clip through Unity
Animation Mode, edits the same
Animancer event sequence stored in `skillClip`, and triggers scene VFX previews
when playback crosses VFX markers. Dragging repeated `Vfx` markers across each
other reorders the associated cue groups. Event timing is not duplicated in the
VFX placement data. Scrubbing the timeline playhead across a VFX marker also
previews its cue in either direction; moving an event marker itself does not.
The editor window creates non-saved playback instances and manually advances
their ParticleSystems, so preview rendering does not depend on the authored VFX
GameObject being selected in the Hierarchy. Preview lifetime is based on particle
duration, start delay, start lifetime, and `IsAlive`; it is not inferred from
`isPlaying`, because manual `Simulate` calls leave a ParticleSystem paused.
Cartoon FX Remaster instances additionally register `CFXR_Effect`'s editor
preview update hook without selecting the effect in the Inspector.
The Skill Animation VFX Timeline window owns the only editor update subscription
used by authoring previews. It subscribes while animation playback or VFX work is
active, ticks the shared preview coordinator at 75 FPS, then performs one player
loop request and Scene view repaint. Authoring entries cache their ParticleSystems
and only simulate the latest delta time. The window reports active previews,
ParticleSystems, CFXR callbacks, and measured preview update FPS. Completed preview clones are deactivated and reused by
their authoring entry so repeated timeline crossings do not instantiate the same
prefab every time. One-shot clones have looping disabled without modifying the
source prefab; `StartLoop` clones preserve each ParticleSystem's authored loop
setting. Preview cleanup runs when the window closes, Play Mode
starts, scripts reload, or the Editor quits, including unregistering Cartoon FX
editor callbacks and removing orphaned playback objects.
Preview startup enables the Scene View's Effects and Particle Systems options so
normal playback clones are not filtered by editor view settings.
Editor loop previews are registered as groups by loop key. Multiple `StartLoop`
entries in the same cue and key play together; a later Start cue replaces the
previous group. `StopLoop` stops the whole matching group and optionally simulates
its remaining particles. The system does not enable ParticleSystem looping;
looping behavior must be authored in each prefab. Scrubbing rebuilds loop state from all VFX
cues at or before the playhead; only one-shot cues are replayed when crossing a
marker in either direction.

`PrefabHitboxSkillPayloadDef` owns its hitbox groups and shapes as inline
serialized data. Runtime hitbox execution does not depend on a separate hitbox
layout asset. This keeps layout, step group keys, targeting, anchor, and timeline
configuration inside the same visible skill asset.

## Adding A Payload Type

Add each payload class in its own `.cs` file and inherit `SkillPayloadDef`.
Implement `Execute(SkillCastContext)` and override
`CollectValidationIssues(List<string>)` when the type has required authoring
data. Override timeline and presentation properties only when the execution
requires them.

The editor discovers concrete payload subclasses through Unity `TypeCache`, so
new types appear in `Execution Type` automatically. Do not add a payload enum or
central type switch. Do not add `CreateAssetMenu` to payload classes because
payloads must be created through their owning skill asset.

## Validation

Use `Tools > RB > Skills > Validate Embedded Payloads` to check ownership,
payload count, payload-specific configuration, and prefab-hitbox group keys.
Validation reports errors without modifying assets.

## Validation Contract

A valid skill must satisfy all of the following:

- `payload` is assigned
- the payload is a sub-asset of the same `SkillGemDefinition` asset
- the skill asset contains exactly one `SkillPayloadDef`
- payload-specific required references and timeline configuration are valid
- timeline VFX entries have valid cue indices, required prefabs, anchors, and
  matching loop keys
- prefab-hitbox payloads contain a valid inline layout and every step group key
  resolves to a group in that layout

For C# validation, run only `Assets/Scripts/CheckAssemblyBuild.ps1` as described
in `Docs/VALIDATION.md`.

---

## Cutscene Skill Cutscene

Cutscene Skills add a two-phase presentation on top of the normal skill flow:

**Phase 1 — Cutscene:** a dedicated character animation and camera animation play
while the main scene is hidden. No gameplay effects occur yet.

**Phase 2 — Execution:** the scene returns to normal, the character plays the
attack/buff animation, and the payload fires at `castPointNormalized` as usual.

### Enabling a Skill as Cutscene

Open the `SkillGemDefinition` asset and expand the **Cutscene Skill** foldout:

1. Enable **Is Cutscene Skill**.
2. Assign `characterCutsceneClip` — a `ClipTransition` played on the cutscene
   character rig. Add `Vfx` markers to this clip to drive cutscene VFX spawning
   (see **Authoring Cutscene VFX** below).
3. Assign `cameraCutsceneClip` — the `AnimationClip` played on the cutscene
   camera rig.
4. Tune `worldSlowScale` (default 0.05), `fadeInDuration`, `fadeOutDuration`,
   and `barThickness` as needed.

The foldout is hidden for non-cutscene skills; they require no additional setup.

### Authoring Cutscene VFX

Cutscene VFX are authored via the same `SetAnimationVfxData` tool as regular
skill VFX:

1. In the cutscene scene, add a `SetAnimationVfxData` component (on the
   cutscene character object or any authoring holder).
2. Set **Source Asset** = the `SkillGemDefinition`.
3. Set **Entry** = **"Cutscene VFX"** (only visible when **Is Cutscene Skill**
   is enabled).
4. Set **Character Root** = the cutscene character rig root.
5. Add `Vfx` markers to `characterCutsceneClip` at the desired spawn times, then
   use **Load / Sync VFX Data** to generate authoring slots for each marker.
6. Place VFX prefabs in the slots, position them relative to the cutscene
   character's bones (using the same anchor types as regular skill VFX).
7. Press **Save VFX Data** — data writes to `CutsceneDef.cutsceneVfxEvents`
   inside the `SkillGemDefinition`.

At runtime, `CutsceneSkillPresenter` fires each `Vfx` event via Animancer and
spawns the corresponding prefabs, automatically moving them to the **"Cutscene"**
layer so the cutscene camera can see them.

### Animation Clip Structure

The `skillClip` for an Cutscene Skill must contain these Animancer event markers
in order:

| Marker | Animancer event name |
|--------|----------------------|
| Cutscene start | `CutsceneSkillStart` |
| Cutscene end | `CutsceneSkillEnd` |
| Skill fires | `castPointNormalized` (existing cast-point event) |

`castPointNormalized` must be **after** `CutsceneSkillEnd` so gameplay effects
only trigger once the cutscene has finished.

### Runtime

`CutsceneSkillPresenter` resolves its owner through `CharacteContext`, then
listens to the resolved `CharacterSkillManager.CastStarted` and
`CharacterAnimBrain.SkillTimelineEventRaised`. It also auto-resolves scene
presentation references from `Camera.main`, `CutsceneCamera`,
`CutsceneCameraRig`, and `CutsceneCharacter` when serialized overrides are left
empty. On `CutsceneSkillStart` it:

- Calls `TimeSlowManager.StartSlow(worldSlowScale, float.MaxValue)` (enemies slow,
  player animation continues normally because `PlayerContext.UsesWorldSlow = false`).
- Disables `CameraF` (main follow camera) and enables the cutscene camera.
- Plays `characterCutsceneClip` (`ClipTransition`) and `cameraCutsceneClip`
  (`AnimationClip`) via `AnimancerComponent.Play()` in unscaled time mode.
- Fades in the letterbox overlay (black bars + solid background, unscaled time).
- Hides the global player HUD through `UIManager.Instance`; this is required
  because the HUD is screen-space UI and is not hidden by camera culling masks.
- Binds `Vfx` Animancer event callbacks on the cutscene character state; each
  `Vfx` event spawns the corresponding `cutsceneVfxEvents` cues, using
  `AnimationVfxAnchorResolver` for placement and `SetLayerRecursive` to put them
  on the **"Cutscene"** layer.

On `CutsceneSkillEnd` it reverses all of the above, restores the player HUD, and
re-enables the main camera.
If the cast is cancelled mid-cutscene, `CastCancelled` triggers a fast 0.05 s exit.

### Concurrency Arbitration

The camera, the global `TimeSlowManager`, the camera `cullingMask`, the player
HUD visibility, and the letterbox overlay are shared resources, so two
characters triggering a cutscene-skill at the same time (or back-to-back) would
fight over them. A single `CutsceneDirector` singleton arbitrates one
"cinematic stage" with an
`Idle → Active → Cooldown` state machine:

- Before any takeover, `CutsceneSkillPresenter.StartCutscene` calls
  `CutsceneDirector.Instance.TryBegin(this)`. The grant is **first-come,
  first-served** — while the stage is `Active` (owned by another presenter) or in
  `Cooldown`, the request is rejected and `StartCutscene` returns immediately.
- A **rejected** cutscene performs **no** cinematic takeover (no camera/time/
  overlay/cutscene-VFX). The character still plays its cutscene + main-skill
  animation via `CharacterAnimBrain`, and main-skill VFX still plays — "do the
  moves without the movie."
- `EndCutscene` and `ForceEndCutscene` call `CutsceneDirector.Instance.End(this)`
  to release the stage. `End` is owner-checked and idempotent. Releasing starts a
  cooldown that rejects new cinematics for `cinematicCooldownSeconds` (designer-
  tunable, default 0.35 s) to prevent back-to-back cinematic whiplash.
- The cooldown is measured in **unscaled real-time** (`Time.unscaledTime`) because
  the world is slowed during a cutscene; scaled time would stretch it ~20×.

The director is created lazily on first access, so no scene setup is required. A
designer may drop a `CutsceneDirector` onto the System/bootstrap object to tune
`cinematicCooldownSeconds`; the lazy singleton picks up the placed instance.

### Scene Setup

See `Docs/PREFABS_AND_AUTHORING.md` — **Cutscene Skill Cutscene Scene Objects**
for the required hierarchy, layer, and component wiring.
