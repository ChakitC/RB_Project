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

Reusable dependencies remain normal asset references. Examples include
projectile prefabs, `ProjectileConfig`, `StatusEffectDef`, audio cues, VFX
prefabs, and pickup prefabs.

## Timeline VFX

Skill-level animation VFX are authored in the scene or Prefab Mode through
`SetSkillVfxData`, which now inherits shared `SetAnimationVfxData` authoring.
The serialized runtime list remains owned by
`SkillGemDefinition`, but it is hidden from the normal skill inspector and is
written by the authoring component's `Save VFX To Skill` action.

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
`SetSkillVfxData`, slots, or authoring entries.
`SetSkillVfxData` also tracks the Skill Definition that owns its current scene
hierarchy. VFX authoring commands rebuild slots from the assigned Skill when that
owner changes, preventing scene entries from being saved into another Skill.

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
same window, and existing `SetSkillVfxData` components require no migration. It samples the
assigned Skill Definition's clip through Unity Animation Mode, edits the same
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
