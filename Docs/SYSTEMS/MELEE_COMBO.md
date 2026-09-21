# Melee Combo

## Step Data

`SkillGemDefinition` owns optional Combo data (`comboEnabled`, `comboSteps`).
Each `SkillComboStep` owns an `executionSkill` reference, its chain window,
buffer-expiry rule, and stable editor entry GUID. The referenced
`SkillGemDefinition` owns the clip, duration (`baseCastTime`, zero means native
clip duration), impact settings, and timeline VFX. Its embedded
`PrefabHitboxSkillPayloadDef` owns an inline `SkillHitboxLayoutData`, shared by
the same runtime used for ordinary Skills.
The retired `MeleeComboSO` assets, type, and hidden Animation Profile fields have
been removed. Runtime reads `meleeSkill`, `lightMeleeSkill`, and `heavyMeleeSkill`. Existing leaf
Skill assets keep their GUIDs, payloads and shared references. No animation or
damage data is copied into the combo root. Nested combos are invalid. A basic
attack slot also accepts a standalone Skill (one step without repeat).

Use **Combo > Assign Missing Step IDs** on the root Skill after adding or
duplicating steps. Reordering retains existing IDs. The basic-attack sequencer
owns input buffering; this migration does not introduce paid multi-step active
casts. Ordinary active-skill entry points reject a combo root before spending
resources. Assign the root to a Basic Attack slot; standalone active Skills
keep their existing cost and playback policy.

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
Each HitStart opens a new hit cache and attack/chain identity. Skill voice and
blockable pre-cast remain separate. Defensive Block now opts in through the
current step's inline Skill Defensive Block settings, including its optional windup. The same
request-scoped hitbox binding and timed approach accept Basic Melee playback.
An interrupted request clears the combo buffer; continuation keeps the normal
combo rules. Steps with Can Be Blocked disabled do not inherit a previous step's window.

## Migration And Authoring

Migration and legacy cleanup are complete. Seven root Skills live under
`Assets/Data/Combat/ComboSkills`; their eight execution Skills remain under
`Assets/Data/Combat/MeleeSkills` with the original GUIDs and embedded payloads.
Four Animation Profiles use the new Basic Attack slots. Create and edit a
`SkillGemDefinition` with Combo enabled for new combos; the one-time migration
menus and old timeline adapter have been removed.

The cleanup backup is outside Assets at
`../.codex-temp/melee-legacy-cleanup-20260920`, including retired assets/scripts,
their meta files, affected profiles, SHA-256 manifests, and the old-to-new map
in `../BuildArtifacts/melee-legacy-cleanup.txt`. It is an archive, not an asset
folder to import into the current project. Cleanup verified each step's execution
reference, ID, chain window and buffer rule before removing the old asset.

Edit the root Skill's Combo list for ordering/buffering, then select a Step in
the Timeline for animation, Hitbox, VFX and Block. `SkillComboVfxTimelineSource`
routes edits to the selected leaf Skill and Chain Window edits to the root.
Chain edits reject stale source snapshots, use Undo, and save only the root;
VFX and Block saves target only the selected leaf asset. Child Skills are hidden
from the main picker when reachable through a combo Entry.
Use `SetSkillHitBoxData` for both Basic Melee and Skills. Groups select Payload,
CasterRoot, or AnimatorRoot space plus a relative anchor path. AnimatorRoot uses
the current visual model Animator via `ctx.Visual.ModelAnimator`, falling back
to `ctx.AnimBrain.BoundAnimator` for actors without a visual module. This avoids
serializing the actor wrapper path when a prefab also has a placeholder Animator.
`MeleeController.targetMask` retains the actor-specific basic-attack layer filter;
ordinary Skills use their payload mask. The `Use Caster Melee Hitboxes` switch
and context hitbox reference no longer exist.

The completed character-hitbox migration copied shapes/anchors into execution
Skills and preserved actor masks. Its import-only `MeleeHitboxTrigger` schema
and migration tool are now retired, after checking for remaining serialized
references. The earlier geometry backup remains outside Assets under
`../.codex-temp/unified-hitbox-assets`.

The eight existing skills now have layouts: five preserve Rector/GR04 geometry
(including the Rector heavy fallback), and three generic Roma/Milano steps use
an approved starter Box centered at `(0, 1, 1)`, size `(1.2, 1.6, 1.5)` in
CasterRoot space. These starter dimensions need animation/balance tuning.

All seven existing combos (eight steps) were migrated. The missing clip from the
retired `Assets/Character/GRS_02/Rector_MeleeComboSO.asset` was replaced with Rector's
`Rector_HavyAttack` as an explicitly approved placeholder; its original hit
windows and impact values remain intact. Review that character's animation
timing when its intended clip becomes available.
