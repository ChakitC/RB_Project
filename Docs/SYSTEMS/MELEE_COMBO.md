# Melee Combo

## Step Data

Each `MeleeComboSO.Step` owns an `executionSkill` reference, its chain window,
buffer-expiry rule, and stable editor entry GUID. The referenced
`SkillGemDefinition` owns the clip, duration (`baseCastTime`, zero means native
clip duration), impact settings, and timeline VFX. Its embedded
`PrefabHitboxSkillPayloadDef` owns an inline `SkillHitboxLayoutData`, shared by
the same runtime used for ordinary Skills.
Legacy clip/impact/VFX fields remain hidden for migration only; runtime does not
read them. The GUID distinguishes steps that share the same clip.

Run `Tools > RB > Animation VFX > Assign Missing Melee Step IDs` after creating,
duplicating, or merging steps. It changes only empty or duplicate IDs.

## Timeline And VFX

Melee clips support repeatable `HitStart` and `HitEnd` pairs plus shared `Vfx`
markers. The shared timeline exposes Hitbox events and an editable Chain Window;
range values are clamped and ordered in `0..1` before the owning struct is
assigned back into the list.

`Vfx` occurrences are numbered chronologically from zero. Multiple actions may
target one occurrence, and an empty marker is valid.

## Runtime Lifecycle

`MeleeController` owns `MeleeComboSession` and sends each selected step through
`CharacterSkillManager.TryStartMeleeStep`. Playback uses `Locomotion_Skill` and
request-scoped Skill timeline events. `SkillHitboxSequenceRuntime` is the sole
owner of collider creation, contact sampling, damage, hit caches, and hit/kill
events. Each controller caches one runtime/layout per payload. Repeating a
previously used step reuses its collider objects. Groups bind directly to their
configured anchors so animated bones and nonuniform scale remain part of the
actual transform hierarchy. The runtime explicitly owns cleanup of those groups
even when they are outside its host hierarchy. Missing/destroyed bones cannot
leave an active hit window; the next execution resolves anchors again.

Player input uses `StateHub.RequestMeleePress`: the first press starts the combo,
and subsequent presses feed its input buffer. AI's `RequestOnMelee` remains a
start-only command and does not restart an active combo. Changing Light/Heavy
mid-combo is ignored. The last step repeats only when its chain window permits
it. Old request callbacks cannot advance a new step.

Every advance, completion, interruption, or disable closes the current hitbox
window and VFX session. The shared executor still reports the semantic animation
mode and playback kind as Melee, preserving state/transition rules.

## Basic Attack Rules

`SkillExecutionKind.BasicMelee` is a command context, independent of
`SkillTag.Melee`. An Active skill tagged Melee remains a normal paid skill.
Basic attacks do not reserve, resize, consume, or recharge the shared charge
pool and do not spend energy. They retain Melee admission/reload/fire-intent
rules, live character/weapon damage and critical stats at hit time, the legacy
stagger fallback (half skill-base damage), and `CombatSourceKind.Melee`.
Each HitStart opens a new hit cache and attack/chain identity. Skill voice,
blockable pre-cast, and defensive-block windup are not enabled by this reuse.

## Migration And Authoring

Use **Tools > RB > Melee > Preview Skill Migration**, then **Migrate All Combos
To Skills** in Edit Mode. The tool creates one visible skill per combo-step
GUID under `Assets/Data/Combat/MeleeSkills`, with its own embedded payload.
Existing execution skills are reused without overwriting designer changes.
Newly converted steps require the character-hitbox migration below or a layout
authored with `SetSkillHitBoxData` before they can execute.
Original combo files are backed up outside Assets in the workspace's
`.codex-temp/melee-skill-baseline/Assets` folder. `ValidateAll()` checks migrated
references, embedded payload ownership, required markers, and VFX.

Edit the combo for ordering/buffering/chain windows. Edit the referenced skill
for animation, duration, impact, and VFX. The Melee timeline source redirects
clip/VFX edits to that skill while preserving the combo's chain-window lane.
Use `SetSkillHitBoxData` for both Basic Melee and Skills. Groups select Payload,
CasterRoot, or AnimatorRoot space plus a relative anchor path. AnimatorRoot uses
the current visual model Animator via `ctx.Visual.ModelAnimator`, falling back
to `ctx.AnimBrain.BoundAnimator` for actors without a visual module. This avoids
serializing the actor wrapper path when a prefab also has a placeholder Animator.
`MeleeController.targetMask` retains the actor-specific basic-attack layer filter;
ordinary Skills use their payload mask. The `Use Caster Melee Hitboxes` switch
and context hitbox reference no longer exist.

`Tools > RB > Melee > Migrate Character Hitboxes To Skill Layouts` copies legacy
shapes/anchors into the execution skills, preserves actor masks, then removes
old components/colliders from prefabs. Rerunning it keeps existing layouts.
`MeleeHitboxTrigger` remains only as an import schema for this editor tool, with
no runtime logic and no remaining scene/prefab instances. Original files are
backed up outside Assets under `.codex-temp/unified-hitbox-assets`.

The eight existing skills now have layouts: five preserve Rector/GR04 geometry
(including the Rector heavy fallback), and three generic Roma/Milano steps use
an approved starter Box centered at `(0, 1, 1)`, size `(1.2, 1.6, 1.5)` in
CasterRoot space. These starter dimensions need animation/balance tuning.

All seven existing combos (eight steps) were migrated. The missing clip on
`Assets/Character/GRS_02/Rector_MeleeComboSO.asset` was replaced with Rector's
`Rector_HavyAttack` as an explicitly approved placeholder; its original hit
windows and impact values remain intact. Review that character's animation
timing when its intended clip becomes available.
