# Skill System

## Asset Ownership

Each active skill is authored through one visible `SkillGemDefinition` asset.
The skill owns exactly one root `SkillPayloadDef` sub-asset stored in the same
`.asset` file — either a single-effect payload, or a `CompositeSkillPayloadDef`
whose steps wrap any number of additional embedded payload sub-assets (see
**Composite Payloads And Steps** below). Standalone or shared skill payload
assets are not part of the supported authoring workflow.

`SkillGemDefinition` owns:

- identity, tags, description, and icon
- base stats and per-level overrides
- animation, cast timing, and pre-cast configuration
- Animancer-driven timeline VFX events and their local placement data
- the embedded execution payload

`SkillInstance` owns per-character runtime state such as level, support gems,
calculated stats, and cooldown timestamps. Runtime state must not be written to
the definition or its payload.

Skill playback and cancellation commands are issued through
`CharacterAnimDriver`. `CharacterAnimBrain` remains available through
`CharacterAnimDriver.Brain` and `SkillCastContext.AnimBrain` for request-scoped
events, normalized-time queries, timeline payloads, and interruption tracking.

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
- `TauntSkillPayloadDef`
- `CompositeSkillPayloadDef`

Reusable dependencies remain normal asset references. Examples include
projectile prefabs, `ProjectileConfig`, `StatusEffectDef`, audio cues, VFX
prefabs, and pickup prefabs.

### Composite Payloads And Steps

`CompositeSkillPayloadDef` is the payload to use when one skill must do more
than one thing (e.g. taunt **and** heal **and** buff). It holds an authored
`[SerializeReference] List<SkillEffectStep>` and is itself the skill's single
root payload — it uniquely owns `helperFacingMode`, `chainContinueMode`, and
`chainContinueNormalizedTime`, since those fields describe the skill, not any
one effect.

`SkillEffectStep` is a plain `[Serializable]` class (not a `ScriptableObject`),
gated by an optional `requiredUpgradeId` read from `SkillCastContext.HasUpgrade`.
Two step types exist:

- `PayloadStep` wraps any existing `SkillPayloadDef` (embedded as its own
  sub-asset in the same skill file) and executes it unchanged. This is how an
  existing single-effect payload (e.g. `TauntSkillPayloadDef`) gets reused
  inside a composite without being ported to a new type.
- `HealAreaStep` heals and optionally applies `StatusEffectDef`s to either
  `Self` (the caster) or `Allies`. Ally targeting enumerates active
  `CharacteContext` instances, filters to `AITargetIdentity.Player` /
  `Companion`, and checks distance from the caster without querying physics
  colliders. Healing and status references are resolved through each target's
  context and scaled by `FinalSkillStats.healPower`.

Direct `SkillEffectStep` implementations are immediate-only: every enabled
direct step runs when the skill reaches its configured cast point. An effect
that must act at another animation moment is authored as a `SkillPayloadDef`,
wrapped by a `PayloadStep`, and may spawn a request-scoped runtime listener such
as `TauntSkillRuntime`. Payloads declare their required timeline event names;
the skill Inspector treats a missing marker on `skillClip` as a blocking error,
while the runtime warning remains as a final safeguard.

`SkillGemDefinition.TryFindPayload<T>` walks the composite's steps
(`PayloadStep.Payload`, recursively) to find a wrapped payload of a given type;
use it instead of `payload as T` when a system needs to know whether a skill
has, for example, a `ProjectileSkillPayloadDef` anywhere in its execution.

Author a composite through the skill inspector's **Execution Authoring**
section: pick `Composite` as the Execution Type, then use the **Composite Step
Payloads** panel to create/replace/remove each `PayloadStep`'s wrapped payload
(embedded the same way the root payload is) and edit its nested inspector
inline. `HealAreaStep` entries have no payload to author — their fields are
edited directly in the Odin-drawn step list.

Step *structure* — add, reorder, remove, and set `requiredUpgradeId` — can
also be authored from **Tools > RB > Skills > Active Skill Tree Editor**'s
**Skill Steps** toolbar toggle, without leaving the tree window. It edits the
owning skill's composite in place. Each step row is a foldout: collapsed shows
just the step type and its `requiredUpgradeId`; expanded draws every field
`SerializedProperty` exposes on the step (still respecting `[HideInInspector]`
on the legacy `HealAreaStep.ConditionalStatus` fields), so `HealAreaStep`'s own
target/status lists are editable inline. A `PayloadStep`'s wrapped payload is
**not** editable from this panel — it shows a "Select '\<Type\>' Payload"
button that pings the payload sub-asset instead. This is a deliberate scope
limit, not a missing feature: rendering the wrapped payload's own
`Editor.OnInspectorGUI()` inline (directly, or via
`UnityEditor.UIElements.InspectorElement`, which falls back to an internal
`IMGUIContainer` for any editor — Odin's included — that doesn't implement
`CreateInspectorGUI()`) hits a still-open Unity engine bug where a `ScrollView`
resets its scroll offset whenever a sibling/descendant `IMGUIContainer`'s
measured height changes, which happens on essentially every repaint while the
window has focus (confirmed reproducing through at least Unity 6000.0.8f1;
this project is on 6000.0.58f2). Deep payload fields (prefabs, numbers, status
lists) stay in the skill Inspector's existing Composite Step Payloads section.
The panel is only enabled when the open tree resolves to exactly one owner and
that owner is a `SkillGemDefinition` with an embedded `CompositeSkillPayloadDef`
root — a tree shared by multiple skill Variants, a tree owned by a
`PassiveDefinition`, or a skill whose root payload is not yet a composite all
show a disabled explanation instead. Converting a single-payload skill to a
composite is not offered from this panel; do that from the skill inspector
first. The panel tracks its own dirty/save state separate from the tree's,
since it edits a different asset. `SkillPayloadAssetUtility` only performs the
Undo-aware sub-asset mutation and marks the owner dirty; it never calls the
global `AssetDatabase.SaveAssets()`. The invoking Inspector/window owns the
Save or Discard decision, so editing a skill cannot silently save dirty tree or
project assets.

`CompositeSkillPayloadDef.CollectValidationIssues` reports an error when two
wrapped payloads compete for the same stat channel (two
projectile/hitbox payloads, or two `MorphSkillPayloadDef`s), when a
`PayloadStep` has no payload assigned, when a `PayloadStep` wraps another
composite (no nesting), when two steps reference the same child payload, and
when a wrapped payload's
`helperFacingMode`/`chainContinue*` are non-default — copy those values up to
the composite root instead, since the composite is the unique owner. These are
blocking authoring errors, not warnings. The embedded-payload validator allows
any number of `SkillPayloadDef` sub-assets when every one is reachable from the
embedded root and each `PayloadStep` uniquely owns its child. External/shared
children and unreachable embedded payloads (orphans) are errors. Existing
orphans are reported for manual recovery; they are never deleted by validation.
Replacing or removing an embedded Composite root deletes only its reachable
embedded descendants, children first, through the same Undo transaction.

### Conditional Status On Payloads

`ApplyStatusSkillPayloadDef` and `TauntSkillPayloadDef` both support a
`Conditional Status Effects` list in addition to their unconditional one: each
entry pairs a `requiredUpgradeId` with a `StatusEffectDef` (and stack count),
applied only when `SkillCastContext.HasUpgrade(requiredUpgradeId)` is true.
`ApplyStatusSkillPayloadDef` applies conditional entries to the caster;
`TauntSkillPayloadDef` applies them to each taunted enemy. `HealAreaStep` has
the same `conditionalApplications` list for the target it heals. This is how
an Active Skill Tree node changes a skill's *behavior* (see **Active Skill
Loadout and Upgrade Trees** below) rather than only its numbers.

#### Application ownership (StatusEffectDef vs StatusApplicationSpec)

Every authored status application — skill payloads, `HealAreaStep`,
`MorphSkillPayloadDef`, `ApplyStatusOnHitModule`, `PassiveActionDefinition`,
`ApplyStatusPickupEffectDef` — holds a `StatusApplicationSpec` and calls the
`StatusEffectController.ApplyEffect(spec, source, fallbackDuration)` overload.
The `StatusEffectDef`-only overload exists solely for systems that have no
authored application (currently `StatusEffectPickUp`); **skill, passive, pickup
effect, and projectile code must not call it.**

The split of ownership is fixed:

| Owned by `StatusEffectDef` | Owned by `StatusApplicationSpec` |
| --- | --- |
| `effectId`, icon, category, VFX profile | initial `stacks` |
| `stackMode`, `maxStacks`, `separatePerSource` | `modifiers` |
| control blocks, locomotion pose, stunned state | `duration` |
| `tags`, trigger rules | tick damage / heal |
| default values for every channel below | tick interval |

Never express a difference in `stackMode`, `maxStacks`, control behavior, tags,
trigger rules, or presentation from an application — if those must differ, author
a separate `StatusEffectDef`. Two applications sharing a Def are the *same status
identity*; differing magnitude alone is never a reason to clone the asset.

**Duration precedence**, in order:

1. the application's own duration override (including an explicit `0` = permanent)
2. `FinalSkillStats.effectDuration` from the skill / upgrade tree, when `> 0`
3. `StatusEffectDef.duration`

The skill-level value is passed to `ApplyEffect` as `fallbackDuration`; it is
never merged into the serialized spec.

#### Override channels

`StatusApplicationSpec` has four independent override channels — **modifiers**,
**duration**, **tick damage**, and **tick interval** — each with its own
serialized enable flag. Overriding is optional: an entry that is never edited
keeps tracking its `StatusEffectDef`, and existing assets behave identically
until a designer opts in.

Because each channel has an explicit enable flag, `0` is a real value once the
override is on — it is not a "use the default" sentinel:

- duration `0` = permanent
- tick damage `0` = damage/heal disabled for this application
- tick interval `0` = ticking disabled for this application

The modifiers channel additionally supports an **explicit empty override**,
meaning this application intentionally applies no stat modifiers.

The Inspector shows each relevant channel's effective value plus its source
(**From Status Effect Def** or **Application Override**). Duration is always
visible. Tick damage and tick interval stay hidden when the Def has no active
tick and the application has no tick override; **Add Tick Override** reveals and
enables both fields, while **Reset Tick To Status Effect Def** clears the group
and hides it again when the Def still has no tick. While a visible channel tracks
the Def, the displayed value is the Def's. The first edit copies the value the
designer is looking at into the application, turns the enable flag on, and stores
what they typed — without ever mutating the `StatusEffectDef`. Modifiers work the
same way: the first edit, add, remove, or reorder clones the whole list. Each
override has a Reset control that returns it to tracking the Def.
Changing the Effect while any channel is overridden asks whether to reset every
override to the new Def, keep them all, or cancel.

There is no legacy status schema left: the flat `effect`/`stacks` fields, the
`ResolvedSpec()`/`ResolvedStatusSpec()` fallbacks, `HealAreaStep`'s plain
`List<StatusEffectDef>`, and the one-off migration tool have all been removed.
`StatusApplicationSpec` is the only shape an authored application has.

This means the same `AtkDown.asset` can be authored once (identity, icon, VFX,
`stackMode`, `controlBlocks`) and reused by a weak skill (`-10% ATK`) and a strong
one (`-40% ATK`) without cloning the asset. **Debuff modifiers must use
`ModifierOp.Multiply`** (e.g. `0.75` for -25%) — it stacks multiplicatively and
diminishes toward but never reaches 0. `ModifierOp.AddPercent` sums linearly across
sources and can drive a stat negative before the engine's `Mathf.Max(0f, ...)` floor
kicks in.

`StatusEffectDef.separatePerSource` (default `false`) controls whether multiple
actors applying the *same* effect share one instance (existing behavior) or each get
their own instance with independent duration and magnitude, combined automatically
through `StatsHub`'s modifier aggregation. Only enable it on effects where every
applying source is a genuine, distinct actor. **Taunt Defs must enable it** so each
taunter keeps its own instance and expiry can fall back to the previous taunter (see
`Docs/SYSTEMS/AI_AND_TARGETING.md`). **Do not enable it** on morph-granted
effects (`MorphSkillRuntime` removes by definition on revert, which would also strip
other sources' instances), or on pickup-granted effects (the pickup `GameObject`
itself is the "source", not the collecting actor, so per-source keys would defeat
stacking). `StackMode.StrongestOnly` compares applications with the same modifier
shape (same stat + operation) and keeps the stronger one; it never lets a weaker or
shorter reapplication shrink the remaining duration.

`StatusEffectController.ApplyTick` now treats a negative `tickDamage` as a
heal (routed through `HealthSystem.Heal` via `CharacteContext.HealthSystem`)
instead of silently no-op'ing. A regen-style `StatusEffectDef` (positive
`tickInterval`, negative `tickDamage`) heals the effect's target once per
tick.

`SpawnPickupSkillPayloadDef` ground-snaps every spawned pickup after applying
its lateral spread. Configure **Ground Layers** with world-floor layers only;
the default is the project's `Ground` layer. Keeping character layers out of
this mask makes a pickup aimed at a character land on the floor beneath them
instead of spawning on top of their controller.

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

Payload runtimes created when the cast point is reached can only receive named
timeline events that occur after `castPointNormalized`. When such a runtime
subscribes to `SkillTimelineEventRaised` (for example `TauntSkillRuntime` waiting
for `TauntApply`), author the cast point before the required event. A timeline
event placed before or at the cast point may fire before the runtime listener
exists and must not be used to trigger that payload.

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

## Camera Shake Markers

Place one or more `ShakeCamera` Animancer events in the main skill clip to
trigger camera shake at those points. `SkillGemDefinition` scans the clip for
`ShakeCamera` markers and arms the timeline binding automatically — skills
without the marker do not produce missing-marker warnings.

`GameplayCameraController` handles the shake internally using trauma-based Perlin noise on the
Camera child's local pose. Multiple markers in the same clip stack trauma
additively. Shake intensity and decay are configured on `GameplayCameraController`'s Inspector
(see `CAMERA.md`). Decay uses unscaled time so shake speed is stable during
world slow.

`ShakeCamera` markers in the cutscene character clip are not bound. Only the
main skill clip fires `ShakeCamera`. Camera shake fires for player, field ally,
and summoned helper skills. Enemy skills are not subscribed.

## HitLag Markers

Place a `HitLag` Animancer event in the main skill clip to trigger a global
micro-freeze (`Time.timeScale` dip) at that point. `SkillGemDefinition` scans
the clip for `HitLag` markers and arms the timeline binding automatically —
skills without the marker produce no freeze. Per-skill tuning fields on
`SkillGemDefinition` control the freeze: `HitLag Duration` (default `0.06s`,
unscaled), `HitLag Time Scale` (default `0.05`), and the optional
`HitLag Shape` curve. No marker = off.

`HitLag Shape` is an `AnimationCurve` envelope whose X axis is normalized
progress (0 = start, 1 = end) and Y axis is a blend factor (0 = normal speed,
1 = full depth at `HitLag Time Scale`). The effective scale per request is
`Lerp(1, hitLagTimeScale, Clamp01(curve.Evaluate(progress)))`. When the curve
is empty/null the manager falls back to its serialized `_defaultHitLagShape`,
which defaults to `Constant(1.0)` (step behavior identical to pre-curve).

`GlobalTimeScaleManager` owns `Time.timeScale` and composes pause, HitLag,
and the default `1f` scale. Overlapping HitLag requests use the lowest
(strongest) scale until all expire.

`TimeSlowManager.WorldTimeScale` is a separate opt-in axis and is not affected.
It uses the same curve-driven envelope formula for finite-duration world slows
(e.g. perfect-dodge slow in `DashSystem`). Infinite-duration slows (cutscene)
hold at full depth until manually reset.

Currently, HitLag runtime triggering is wired only through the Guaranteed
Interruption flow in `AllyInterruptionController`. The HitLag fires exactly
once per interruption execution, only after the block is confirmed
successfully. If the `HitLag` marker arrives before `HitStart`, it is pended
and fires on block-confirm.

---

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
- every embedded `SkillPayloadDef` sub-asset in the file is referenced by the
  root payload or by a `CompositeSkillPayloadDef` step (no orphaned payloads)
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

`CutsceneSkillPresenter` serves both **skill cutscenes** and the
**ChainReady intro cutscene**. The stage logic (camera, world-slow, letterbox,
visibility override) is source-agnostic; a `CutsceneSource` enum (`Skill`,
`ChainIntro`) ensures that `CastCancelled` only ends a skill-sourced cutscene
and never a chain intro, and vice-versa. The chain intro path uses
`TryBeginChainIntro`/`EndChainIntro` instead of timeline events.

It resolves its owner through `CharacteContext`, then
listens to the resolved `CharacterSkillManager.CastStarted` and
`CharacterAnimBrain.SkillTimelineEventRaised`. It also auto-resolves scene
presentation references from `Camera.main`, `CutsceneCamera`,
`CutsceneCameraRig`, and `CutsceneCharacter` when serialized overrides are left
empty. On `CutsceneSkillStart` it:

- Calls `TimeSlowManager.StartSlow(worldSlowScale, float.MaxValue)` (enemies slow,
  player animation continues normally because `PlayerContext.UsesWorldSlow = false`).
- Adds external `Move`, `Shoot`, and `Rotate` control blocks through `StateHub`
  until the cinematic stage ends or is force-ended. New skill starts are
  rejected by `CutsceneDirector.IsCinematicPlaying`; the active cutscene skill
  remains valid until its cast point.
- Disables `GameplayCameraController` (main follow camera) and enables the cutscene camera.
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

- Before any takeover, `CutsceneSkillPresenter.BeginStage` calls
  `CutsceneDirector.Instance.TryBegin(this)`. The grant is **first-come,
  first-served** — while the stage is `Active` (owned by another presenter) or in
  `Cooldown`, the request is rejected and `BeginStage` returns `false`.
- A **rejected** skill cutscene performs **no** cinematic takeover. A rejected
  ChainReady intro causes the chain to start immediately without any cutscene.
- `EndStage` and `ForceEndStage` call `CutsceneDirector.Instance.End(this)`
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

## Pre-Cast Hold Mechanism

`CharacterAnimDriver` exposes the Pre-Cast Hold command facade used by the
Guaranteed Interruption Command. `CharacterAnimBrain` owns the underlying hold
state that freezes an enemy's skill animation playhead before the cast point,
preventing the cast-moment Animancer event from firing.

- `TryAcquirePreCastHold(requestId, speedMultiplier, safetyMarginNormalized)`
  slows the animation to `speedMultiplier` and clamps `NormalizedTime` to
  `castPoint - safetyMargin`. Once the ceiling is reached, speed is set to 0.
- The hold operates on `AnimancerState.Speed` / `NormalizedTime` (per-clip),
  not `animancer.Graph.Speed`, so world-slow composes multiplicatively.
- `ReleasePreCastHold(handle)` restores the original state speed.
- The hold is automatically cleared when the skill locomotion state exits.

The `SkillCastOrchestrator` cancellation invariants are unchanged — the hold
prevents the payload from releasing by freezing time, not by suppressing
orchestrator logic.

## Two-Phase Reservation Block

`PreCastBlockController` supports a two-phase block for the interruption
command:

1. **`TryReserveBlock`**: acquires a Pre-Cast Hold on the enemy animation,
   closes the pre-cast window/indicator, and returns a `PreCastBlockReservation`.
   While reserved, `CanBlockActiveCast()` returns false (window is closed),
   preventing duplicate commands.
2. **`CompleteReservedBlock`**: releases the hold and calls
   `TryCancelActiveCast(Blocked)` via the shared `DoBlockInternal` core.
   Fires `CastBlocked` once on success. A confirmed block (direct `TryBlockCast`
   or via `CompleteReservedBlock`) consumes the blocked skill's cooldown (see
   `stampCooldown` below), so the enemy cannot immediately retry the same skill.
3. **`CancelReservedBlock`**: releases the hold without cancelling the enemy
   cast. If the same unreleased cast is still active, its pre-cast window and
   presentation are reopened. This path does **not** stamp cooldown, since the
   cast was never actually cancelled.

If the enemy's cast is cancelled externally while reserved, `OnCastCancelled`
releases the hold and clears the reservation (no double-block). If the cast
payload somehow releases while reserved (invariant violation), a
`Debug.LogError` is logged and the reservation is cleaned up.

## External Skill API

`CharacterSkillManager` exposes two methods for systems that start skills on
behalf of a character without going through the loadout/slot path:

- `CanStartExternalSkill(CharacterSkillEntry, ignoreResourceCosts)`: preflight
  check (alive, not blocked by animation, skill can cast). When
  `ignoreResourceCosts` is `true`, the energy/cooldown `CanCast` check is
  skipped (used by Guaranteed Interruption so the player can interrupt without
  sufficient energy).
- `TryStartExternalSkill(CharacterSkillEntry, debugSource, requiredTimelineEvent, usePlanarRootMotion, ignoreResourceCosts, stampCooldown)`:
  delegates to the existing `TryBeginEntryCast` internal method. The timeline
  event and planar root-motion flags are optional. When `ignoreResourceCosts`
  is `true`, the cast bypasses energy checks and shared cooldown. When
  `stampCooldown` is `false`, `SkillInstance` does not write `_lastCastTime`
  after a successful cast, so the skill's personal cooldown is not consumed
  (used by player interruption to avoid affecting the main skill cooldown).
  Both parameters default to the backward-compatible values (`false` / `true`).
  A pending cast cancelled with `SkillCastCancelReason.Blocked` (enemy pre-cast
  block, see Two-Phase Reservation Block above) also stamps cooldown at the
  moment of the block, unless `stampCooldown` is `false` for that request.
  Energy and the skill payload are not spent/executed in this case, since cast
  point was never reached — only the per-instance and shared cooldowns are
  consumed, exactly as if the cast had completed at the block moment. Other
  cancel reasons (`Stunned`, `Staggered`, `AnimationInterrupted`, `Disabled`,
  `CharacterDown`, `CharacterDead`, `InvalidState`) never stamp cooldown.
  Planar translation and yaw are scoped to that animation request and are
  cleared on normal completion, interruption, disable, or destruction.

Each `SkillGemDefinition` also exposes **Ignore Character Collision During Root
Motion** under **Presentation**. It defaults to enabled. While that skill or
chain-skill animation owns root motion, `RootMotionCCDriver` temporarily adds
the `Player`, `Enemy`, and `Ally` layers to the actor
`CharacterController.excludeLayers`. Ground, terrain, and obstacle collision
remain active, so clips may keep their intended Y displacement without using
other character bodies as ground or steps. The collision override is
request-scoped and is released on normal completion, interruption, component
disable, or destruction.

`CharacterAnimBrain.PlaybackEvent` reports request-scoped `Started`,
`CastMoment`, `AdvanceMoment`, `Completed`, and `Interrupted` phases. Systems
that own a specific external request must filter by both playback kind and
request id instead of relying on the legacy parameterless completion event.

## Active Skill Loadout and Upgrade Trees

Each `CharacterStats.skillSlots` entry is a stable Skill Slot. Author a unique,
non-empty `slotId`; each configured `CharacterSkillLoadoutOption` in that slot
is a Skill Variant and needs a unique, non-empty `optionId`. Assign the default
`SkillUpgradeTreeDefinition` on `SkillGemDefinition.upgradeTree`. A Variant can
optionally replace it with `CharacterSkillLoadoutOption.upgradeTreeOverride`;
all runtime and UI paths use `ResolvedUpgradeTree` (override first, then Skill
Asset default). Tree and node IDs are persistent save keys and must not be
renamed after release without a save migration.

`CharacterActiveSkillProgress` is the shared runtime owner and is resolved
through `CharacteContext.ActiveSkillProgress`. Active Skill Points are separate
from Passive Points and are shared by every Slot and Variant on that character.
The default grant is one point for each character level after level 1, controlled
by `CharacterStats.activeSkillPointsPerLevel`. Old saves are caught up once by
the `activeSkillProgressInitialized` flag; the catch-up grant subtracts points
already spent across every tree before topping up, so a save whose flag is
missing or reset does not receive its lifetime grant twice.

When `CharacterContextPartyLoader` assigns a party member's `baseStats`, it
reloads `CharacterActiveSkillProgress` for that character ID. A full progress
reload rebuilds every resolved command slot so field allies receive the same
unlocked upgrade IDs as the corresponding lobby character.

Progress is saved by `{slotId, optionId, treeId}`. Each unlocked node stores its
paid cost. Resetting the current Variant clears its whole tree and refunds those
saved costs, even when the authored costs have since changed. If a Variant is
assigned a different `treeId`, the old paid costs are refunded and the new tree
starts empty. A saved node missing from the current asset — whether because the
node was deleted from the tree or the tree was reassigned — has no gameplay
effect and is pruned from the save with its paid cost refunded the next time
that Variant's progress is read, regardless of whether `treeId` changed. A
prerequisite ID left dangling by a deleted node is an authoring error caught by
**Validate Active Skill Trees**; the surviving nodes that required it are not
cascade-refunded.

All prerequisite IDs on a node use AND semantics. A node may additionally list
`mutuallyExclusiveNodeIds`; `ActiveSkillProgressModel.CanUnlock` rejects it once
any listed node is already unlocked, and the reason string surfaces in the node
detail panel (the node itself renders as its normal Locked state — no special
UI is required). Exclusion is enforced bidirectionally at runtime: `CanUnlock`
also rejects a node if an already-unlocked node lists it as excluded, even if
this node's own list does not list that node back. Authors should still keep
`mutuallyExclusiveNodeIds` reciprocal on both sides — **Validate Active Skill
Trees** errors otherwise — but the runtime does not depend on that being done
correctly. This is how a shared trunk fans out into exclusive branches; use
`CharacterActiveSkillProgress.ResetTree`/the in-UI respec action to switch
branches, since it fully refunds the paid points.

A node may also list `grantedUpgradeIds` — free-form strings such as
`"aires3.self_guard"` with no stat effect of their own. Every id granted by an
unlocked node is aggregated into `SkillUpgradeStatSnapshot.UpgradeIds` and
exposed at cast time as `SkillCastContext.HasUpgrade(id)`. This is the channel
`SkillEffectStep.requiredUpgradeId` and payload conditional-status lists (see
**Composite Payloads And Steps** above) read to gate *behavior*, separate from
the numeric `statModifiers` a node also carries. Use
`"<skillslug>.<feature>"`, lowercase and dot-separated, as the naming
convention.

Clicking a node only selects it and shows requirements plus a before/after stat
preview. Points are spent only after the separate Unlock confirmation. The tree
can change Skill Level and the supported skill stats: Damage, Area Radius,
Projectile Count, Mana Cost, Cast Time, Cooldown, Crit Chance, Stagger Power,
Effect Duration, and Heal Power. `AreaRadius` and `EffectDuration` are single
values for the whole skill — a node that raises either affects every effect
that reads them, not just one. `ProjectileCount` rounds its aggregated
`(base + add) * mul` result to the nearest integer, rounding a `.5` result up
rather than using banker's rounding, so a `x1.5` modifier behaves the same on
both odd and even base counts. `StatType` is serialized by enum index; new
members must always be appended at the end, never inserted. Tree modifiers are
deterministic:

`Skill Level -> (base + sum(tree additions)) * product(tree multipliers) -> Support modifiers -> Character stats`

The upgrade snapshot is attached to `SkillInstance`; it never mutates the source
`SkillGemDefinition` or tree ScriptableObject. Selecting a Variant saves
immediately. In gameplay, `CharacterSkillManager` rebuilds only the affected
slot; lobby editing uses the same `CharacterProgressData` save contract.

### Active Skill Tree Authoring

Open **Tools > RB > Skills > Active Skill Tree Editor** to create/open a tree,
add, duplicate, delete, drag, and connect nodes, edit properties, frame the
graph, validate, and save. Multiple incoming connections become the node's AND
prerequisite list. The runtime data contains only serializable node records and
does not depend on GraphView. A node displays its assigned `icon` Sprite directly
in the graph and refreshes the preview when the Inspector value changes.
`uiPosition` is the node center in both authoring and runtime layouts. Set
`visualScale` from `1.0` to `2.0` to enlarge a node for visual emphasis without
changing its gameplay rules or saved progress. Right-click empty graph space to
add a node; if the owning skill declares an upgrade id no node grants yet, the
context menu offers "Grants \<id\>" entries that create the node pre-wired with
that `grantedUpgradeIds` entry. The **Skill Steps** toolbar toggle switches the
right pane to the composite step panel described in **Composite Payloads And
Steps** above.

Selecting a node shows a **Gameplay Effects** section, in reading order —
gameplay first, authoring tools last: the node's stat modifiers, then
**Unlocked Abilities** listing everything the player actually receives, then
**Additional Gated Status Effects** (the status wizard) at the bottom.
**Unlocked Abilities** shows every site in the owning skill/passive that
reacts to each `grantedUpgradeIds` entry. One id used by several sites lists
all of them. Sites are found by `UpgradeIdUsageScanner`, which walks the
owning asset's `SerializedObject`s — including embedded payload sub-assets,
which a single `SerializedObject` never follows into — looking for
`requiredUpgradeId` (and `upgradeId` inside a `TriggeredPassiveDef`'s
`upgradeOverrides`). Nothing is added to the runtime types for this, so a
newly authored gated field is picked up automatically instead of disappearing
from the report when someone forgets to override a collector.

**Unlocked Abilities** never repeats a conditional status application that an
**Additional Gated Status Effects** card (below) already covers — the two
panels resolve overlapping data (a route application is both a
`requiredUpgradeId` site the scanner finds and a route entry the wizard
collects), and showing it twice just doubles the reading without adding
information. The match is exact — same owning asset, same application's own
property path — never a text comparison, so it cannot misfire on similar
wording. An id whose only usage is a directly-gated status application skips
its **Unlocked Abilities** row entirely; an id that also gates a non-status
behavior (a step, a payload, a passive rule) still lists that remaining
behavior. An id nothing reacts to at all still shows its row with the
"nothing listens for this id" warning, since that is an authoring problem, not
a duplicate. A status embedded in a step's own unconditional list — for
example `HealAreaStep.statusSpecApplications`, gated only by the step's own
`requiredUpgradeId` rather than by an id of its own — is never collected by
**Additional Gated Status Effects**, so it is never a candidate for this
dedup: it stays reported on the **Unlocked Abilities** card for the step's
gate, because editing it means editing the step, not the wizard.

Status applications are summarised with their resolved numbers — `Apply
Aires3_TeaGuard to Self`, then modifier/duration/stack/tick/trigger-rule lines each
marked `From Status Effect Def` or `Override` so it is clear which channel owns
the value (see **Override channels** above). Every status summary names its target
(`Self`, `Allies`, or `Taunted Enemies`); an unknown future application site is
shown as `<unresolved target>` rather than silently omitting the target. Trigger
rules report their actual runtime stack/refresh behavior; the summary does not
infer unimplemented behavior
from a status or node name. A recognized step type can also describe its own
bare gate instead of the generic `Enable <Step>` fallback: a `HealAreaStep`
gate reports its target mode (`Heal Self` or `Heal nearby Allies`), which
`FinalSkillStats` channel each of Heal Power and Area Radius reads from, and
any unconditional status it applies once unlocked, with the same
modifier/duration/stack/tick lines as a conditional application. An
unrecognized step type still falls back to `Enable <Step>` rather than
guessing behavior from its class name. Selecting a node whose granted id gates
one of these sites also shows a **Required Path Preview**: it walks that
node's `requiredNodeIds` back to the tree root, folds every prerequisite plus
the selected node into a `SkillUpgradeStatSnapshot` with the same formula
`SkillInstance.GetFinalStats` uses, and previews the resulting Heal Power
and/or Area Radius against the owning skill's base values. It is labelled
"Required Path Preview", not "Final", because an optional sibling node picked
up later can still change the eventual totals; a shared tree also names the
owning skill next to the preview since base stats can differ per skill. A
value that resolves to 0 or less (or an Allies-mode gate with 0 Area Radius,
or a status application missing its Status Effect Def) shows as a warning on
the card without changing any runtime rule. The location line uses
`Configured In: Taunt Payload (Step N)` so it cannot be mistaken for another
effect. Each site's **Edit** button
opens the Skill Steps panel, expands and highlights the owning step; sites that
live outside a composite step (passive rules, a payload's own fields) select and
ping their source asset instead.

#### Status Effect Wizard

**Gameplay Effects** also contains an **Additional Gated Status Effects**
block, below **Unlocked Abilities**: a `+ Add Additional Status Effect` button
plus one card per conditional status application already gated directly by
one of the node's granted ids. "Additional" distinguishes these from a status
bundled inside an unlocked ability's own step, which the card above already
reports. When no application is directly gated yet, the block reads "No
additional status effect is directly gated by this node. Status effects
bundled with the unlocked ability are shown above." instead of an empty list,
so a designer who only sees a HealArea-style bundled status is not left
wondering why this block is blank. Each card reads top to bottom in the order
a designer needs — what it does, the resolved numbers, then where it comes
from: the resolved application (`Apply MaxHPBuff to Self`), its
modifier/duration/stack/tick lines (the same `From Status Effect Def`/`Override`
lines **Unlocked Abilities** uses, so the two panels never disagree), `Gate:
<upgrade id>`, `Source: <route field label> — Step N` (or `Skill payload` for a
route on the skill's root payload), the status' scope, and three buttons —
**Edit** (reopen the wizard on that application), **Open Source** (select/ping
the payload or composite that stores it), and **Remove**.

The wizard (`ActiveSkillStatusEffectWizardWindow`) is the single window a designer
needs to add a buff from a node. It has four sections:

- **Context** — owning skill (a picker only when the tree is shared by several
  skills), the selected node, and the upgrade gate. The gate is either an id the
  node already grants or a new one; a new id is added to `grantedUpgradeIds`
  as part of the same operation, so the id is never typed twice.
- **Status Identity** — *Use Existing* (any `StatusEffectDef` the skill is allowed
  to use) or *Create New*. **Create New authors a numeric status only** — stat
  modifiers, duration, ticking, stacking. Control blocks, taunt tags, and
  triggered stacking need a hand-authored Def, so those cases must pick an
  existing status.
- **Application** — target, destination, stacks, modifiers, duration, tick. Only
  the channels the designer actually edits get their override flag enabled; the
  rest keep following their runtime source (duration uses the skill/taunt
  fallback before the `StatusEffectDef`; tick damage and tick interval each keep
  an independent override flag; see **Override channels** above).
- **Preview** — every asset and field the commit will write, plus blocking errors
  and warnings. Scope repairs such as **Promote To Global**, **Label As Unique**,
  and **Duplicate As Unique** are staged in the preview and are not executed until
  the commit button. Therefore **Cancel leaves the project untouched**, and a
  commit collapses into a single undo group (including a status asset it created).

All writes go through `ActiveSkillStatusEffectAuthoringService`, never through the
window, and use `SerializedObject`/`SerializedProperty` — the route lists are
private on their payloads/steps and embedded payloads are separate sub-assets, so
no runtime API is widened for authoring.

#### Status Targets And Routes

A destination is one `ConditionalStatusRoute` field
(`Assets/Scripts/Player/Skill/Status/`). The route owns the list of
`ConditionalStatusApplication` (`requiredUpgradeId` + `StatusApplicationSpec`),
applying the unlocked ones through `ApplyUnlocked`; it never discovers actors
itself — the owning behavior resolves the `StatusEffectController`, the source
object and the fallback duration first.

**Who the status lands on is declared next to the field**, with
`[SkillStatusRouteTarget]`, either as a fixed target or as the name of a member
that returns one:

| Target | Declared on |
| --- | --- |
| `Self` | `ApplyStatusSkillPayloadDef.conditionalStatuses`, and `HealAreaStep` when its mode is `Self` |
| `Allies` | `HealAreaStep.conditionalStatuses` when its mode is `Allies` |
| `Taunted Enemies` | `TauntSkillPayloadDef.conditionalStatuses` |

`SkillStatusRouteResolver` walks only the *skill structure* — root payload,
`CompositeSkillPayloadDef` and `PayloadStep` — and finds routes by reflection
(`SkillStatusRouteMetadata`, cached per `Type`). It knows no concrete payload
type, so a new payload or step that carries a `ConditionalStatusRoute` is picked
up by the wizard, the tree summary and `UpgradeIdUsageScanner` without any change
to those files. A route whose target metadata is missing or broken is reported as
a **blocking issue** (`SkillStatusRouteResolutionResult.Issues`) — it never falls
back to `Self`. Each route's `RouteKey` combines the container type, the step
index and the list's property path, so one payload may hold several routes with
the same target without their keys colliding.

A target with no route is disabled in the wizard. **The wizard never creates a
step or payload to make a target available** — inserting a step changes the
skill's execution order, which is a design decision, not a side effect of adding a
buff. When a skill has more than one route for the same target (for example two
Apply Status steps), a **Destination** selector appears, labelled with the step
number. Changing the destination of an existing application moves it: the old
entry is deleted and the new one written in one undo group. Removing an
application deletes only that entry — the `StatusEffectDef` asset is always kept,
because other skills may still use it — and if nothing else in any owner of the
tree (active skill or passive) listens for its gate id, the window offers to drop
the id from the node.

#### Status Effect Scope

A `StatusEffectDef`'s reuse policy is carried by **asset labels**, not by a
serialized field (it is authoring-only data, and a new field would force every
existing status asset to re-serialize):

| Label | Meaning |
| --- | --- |
| `StatusScope.Global` | any skill may use it |
| `StatusScope.Unique` | only the owning skill may use it |
| `StatusOwner.<Skill GUID>` | names that owner |
| *(no label)* | legacy — still usable, reported as a warning |

New statuses are created under `Assets/Data/StatusEffects/Buffs`, `.../Debuffs`,
or `.../Control` when Global, and `Assets/Data/StatusEffects/Unique/<SkillName>`
when Unique. Using a unique status from a skill that does not own it is a
blocking error; the wizard offers **Promote To Global**, **Duplicate As Unique**,
or **Choose Another Status**. Reducing a Global status to Unique is refused while
more than one skill still references it. `effectId` must be unique across the
project.

Labels live in the asset's `.meta` file, which Unity's Undo does not cover — a
scope change alone cannot be undone with Ctrl+Z; press the opposite scope button
to revert it. Everything else the wizard writes (the application, the granted id,
a newly created status asset) is inside the single undo group.

Use **Tools > RB > Skills > Validate Embedded Payloads** before committing
skill assets. It validates the complete root-to-child ownership graph rather
than requiring exactly one sub-asset: every referenced payload must be embedded
in the owning skill, every embedded payload must be reachable, and a child may
belong to only one `PayloadStep`. It also reports missing required payload
timeline markers on the skill clip.

Use **Tools > RB > Skills > Validate Active Skill Trees** before committing
content. Errors include missing/duplicate stable IDs, missing/self/cyclic
prerequisites, invalid cost/level, unsupported stats, unstable loadout IDs, and
an upgrade id the payload declares that no node grants. A node with no effect,
overlapping runtime node bounds, a graph that auto-fits below the readable scale,
a granted id nothing consumes, and the same id granted by two nodes that are not
mutually exclusive are warnings. Visual Scale outside `1.0` to `2.0` is an error.
**Tools > RB > Skills > Run Active Skill Core Smoke Tests**
checks catch-up (including the already-spent deduction), shared points,
prerequisites, Variant isolation, refunds, tree replacement, pruning a node
removed from the asset without a `treeId` change, a failed unlock still
persisting reconciliation, default/override Tree resolution, stat stacking,
granted upgrade-id aggregation, mutually-exclusive rejection (both declared
and one-way), Effect Duration/Heal Power stacking, Projectile Count rounding,
scaled-node layout including the authored-vs-resolved size write-back, frame
fallback, validation, per-issue `NodeId` attribution, grant severities and the
cross-node duplicate rule, `UpgradeIdUsageScanner` resolving both passive rule
sites and the real skill's embedded status applications (including a
`HealAreaStep` gate reading as `Heal nearby Allies` with its unconditional
`Aires3_AllyGuard`/Armor `+20%` reported and the nested conditional
`aires3.ally_regen` application staying a separate, non-duplicated usage), and
`RequiredPathPreviewResolver` folding a multi-level prerequisite chain
(including a `mul: 0` trunk node zeroing every later add, matching the
runtime formula), excluding a sibling node that is not on the required path,
resolving safely against a prerequisite cycle and a dangling prerequisite
id instead of hanging, and **Unlocked Abilities** hiding a granted id whose
only usages are status route applications already shown on a Status Effects
card while keeping a non-status gate (a `HealAreaStep`'s own gate) visible on
the same node.

Use **Tools > RB > Skills > Validate Status Effect Scopes** to check the whole
project's status ⇄ skill relationships: unlabelled (legacy) statuses are warnings,
a unique status used by a non-owner and a duplicated `effectId` are errors.
**Tools > RB > Skills > Run Status Effect Authoring Smoke Tests** covers scope
classification, cross-skill unique rejection, duplicate `effectId`, route
resolution for Self/Allies/Taunted Enemies, multiple destinations on one target,
validate/preview and staged scope repairs writing nothing, creating a status asset
with a new gate id, independent tick-channel override flags, reusing an existing gate id, duplicate-application
blocking, source navigation, moving an application between targets, a destination
whose step was deleted, the shared-tree owner requirement, shared-owner gate usage,
undo/redo cache invalidation, and remove freeing its gate id. It builds and deletes its own assets under a
temporary folder without calling global `AssetDatabase.SaveAssets()`.

## Command Skill Cast Facing

Command-slot skills whose payload uses `FaceDetectedTargetOnCast` rotate the
character root horizontally toward the skill user's current aim direction
immediately before the skill animation starts. Skills using
`KeepCurrentFacing` retain their existing facing. `Aires_Skill_3` enables this
behavior so its animation and world-space VFX align with the current Aim
Target.

## Test Stage Damage And Energy Contract

`SkillGemDefinition.damageCoefficient` scales a skill from the caster's final
character Damage stat without coupling it to weapon damage. Final authored
damage starts from:

`level-resolved base damage + StatsHub.GetSkillBaseDamage() * damageCoefficient`

Upgrade-tree and support modifiers then modify that result. Skill critical
chance comes from the character through `StatsHub`; tuned Test Stage skills use
0 authored base critical chance.

`SkillUserSystem` uses 100 maximum Energy, regenerates 3 Energy per second, and
waits 2 seconds after spending before regeneration resumes. The rule is shared
by player-controlled and AI-controlled party actors. Room travel does not
restore Energy.

Current tuned skills are:

| Skill | Damage formula | Cost | Cooldown | Notes |
| --- | --- | ---: | ---: | --- |
| Roma Ultimate | `100 + Damage * 20` | 60 | 50s | 80 stagger |
| Aires Skill 1 | `100 + Damage * 12` | 25 | 10s | 60 stagger |
| Aires Taunt | 0 | 25 | 15s | radius 10, duration 4s, line of sight required |
| Elite Knockback | `20 + Damage * 0.9` | 0 | 10s | existing hit step remains 2x |
| Boss Heavy Combo | `55 + Damage * 0.55` per hit | 0 | 18s | three existing hits |
| Boss Projectile | `90 + Damage * 1` | 0 | 6s | existing projectile payload |

Boss Skill 2 remains the existing self-buff with zero skill damage and a
25-second cooldown. `Aires.Skill.2.Morph` remains an unequipped placeholder;
do not add gameplay effects or put it back into the default loadout until its
design is complete.
