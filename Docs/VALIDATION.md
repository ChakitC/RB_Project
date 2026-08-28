# Validation

Use this document for local C# validation. These rules are project policy.

## Canonical Command

Run C# validation only through:

```powershell
powershell -ExecutionPolicy Bypass -File 'P:\Game_RB_Project\RB_Project\Assets\Scripts\CheckAssemblyBuild.ps1'
```

Do not run `dotnet build` directly against Unity `.csproj` files for gameplay
validation.

## Build Artifact Rule

Do not run any build command that contains either of these paths:

- `Assets\Scripts\_buildbin`
- `Assets\Scripts\_buildobj`

Build artifacts must stay outside `Assets` so Unity does not import generated
assemblies back into the project.

## What The Script Does

`CheckAssemblyBuild.ps1` uses Unity-generated `Assembly-CSharp.csproj` for:

- references
- define symbols
- analyzers
- compiler settings
- project references

It then builds a temporary scanned project outside `Assets`. The scanned
project generates its `Compile` list from real source files under `Assets`.

The source scan is intentionally scoped:

- Include default-assembly `.cs` files under `Assets`.
- Exclude files under folders with `.asmdef`.
- Exclude `Editor` folders.
- Exclude Unity first-pass roots such as `Assets\Plugins`,
  `Assets\Standard Assets`, and `Assets\Pro Standard Assets`.
- Do not include `Packages/**/*.cs` directly.

Package and asmdef code should remain referenced through Unity-generated
project references or assemblies.

## When To Validate

Run the validation command after editing C# source when the change touches:

- gameplay behavior
- public or serialized APIs
- context reference resolution
- stat, passive, weapon, inventory, save, or AI flows
- shared interfaces or data contracts

Markdown-only documentation changes do not require C# validation.

## Helper proc and rig validation

Run **Tools > RB > Skills > Validate Active Skill Trees** after editing a Helper loadout. The pass
reports missing execution skills, missing stable slot/option ids, duplicate Helper proc ids, and
unresolvable proc variants. Run **Tools > RB > AI > Validate Helper Rig** after editing
`Assets/Prefab/Player/Ally_Helper.prefab`; it checks the context, skill progress, skill manager,
animation driver/brain, and their context bindings.

The EditMode smoke tests cover selected proc resolution, variant snapshot/upgrade-id replacement,
legacy character-progress migration (including mixed legacy/current entries without destructive
default overwrites), and idempotent reload. PlayMode coverage is still required for
animation wind-up switching, activation callbacks, party-health queueing, chain attack interruption,
room transitions, and cinematic holds.

## Failure Handling

If validation fails:

1. Fix source-code errors first.
2. Do not edit generated `.csproj` or `.sln` files unless the task explicitly
   requires it.
3. Do not move new classes into unrelated existing files just because Unity has
   not regenerated project files yet.
4. Keep new Unity classes in their intended `.cs` files.
5. If the generated `.csproj` is stale, refresh Unity/reimport/regenerate project
   files instead of changing file ownership.
# Active Skill Tree validation

`SkillUpgradeTreeValidator` (`Assets/Scripts/Editor/ActiveSkill/SkillUpgradeTreeValidator.cs`)
checks `SkillUpgradeTreeDefinition` assets. Beyond blank/duplicate node ids, cost/level ranges,
unsupported `StatType` stat modifiers, prerequisite cycles, and node overlap, it also validates:

- `grantedUpgradeIds`: blank entries, duplicates within one node, and (when the tree has an
  owning `SkillGemDefinition`, resolved via an `AssetDatabase` scan for `upgradeTree`/
  `upgradeTreeOverride` references) ids that don't match anything the owning skill's payload
  declares through `CollectUpgradeIds`. The two directions carry different severities:
  a node granting an id nothing consumes is a **Warning** (the normal state while a tree is
  authored ahead of its payload), while an id the payload declares that no node grants is an
  **Error** (that feature is unreachable in game). A tree with no owning skill yet warns instead
  of erroring, since ids can't be cross-checked in that case.
- The same upgrade id granted by two different nodes is a Warning, because `HasUpgrade` is a set
  membership test and the second node costs a point without changing anything. It is suppressed
  when the two nodes are mutually exclusive — that is how a branch choice offers the same unlock
  down either path.
- `mutuallyExclusiveNodeIds`: blank entries, self-exclusion, missing node ids, a node that both
  requires and excludes the same node, and asymmetric pairs (node A excludes B but B does not
  exclude A back) — the last one is an Error because a one-way lock only misbehaves for players
  who unlock in a specific order.
- The "no gameplay effect" warning no longer fires for a node whose only effect is granting an
  upgrade id (it used to fire on every pure-behavior node once `grantedUpgradeIds` shipped).

Every issue carries a `NodeId` (`null` for tree-level issues). Issues that concern two nodes —
overlap, one-way exclusion, duplicate grants — are emitted once per node with node-specific
wording, so consumers never have to search the message text to work out who an issue belongs to.

Two entry points: `ActiveSkillTreeEditorWindow.ValidateTree()` (open tree, logs to console) and
the project-wide `Tools/RB/Skills/Validate Active Skill Trees` menu. The tree editor window
(`Tools > RB > Skills > Active Skill Tree Editor`) also shows validation issues inline: selecting
a node displays that node's issues as `HelpBox`es below its inspector fields, with a compact count
of remaining issues elsewhere in the tree. Each graph node additionally shows a status badge in its
title bar (`✓` clean, `!` warning, `✕` error) driven by the same `NodeId`. This inline pass is
cached and only recomputes when the tree or the selected node's data changes, so it does not re-run
every repaint.

`SkillUpgradeTreeValidator.cs` and `ActiveSkillTreeEditorWindow.cs` live under `Editor/`, so
`CheckAssemblyBuild.ps1` does not compile them. Verify changes to either file by opening Unity and
running the validate menu / editor window, not by trusting a green `CheckAssemblyBuild.ps1` run.

The tree editor's **Skill Steps** panel edits the owning skill's
`CompositeSkillPayloadDef.steps` directly (add/reorder/remove/`requiredUpgradeId` only; deep
payload fields still go through the skill Inspector). It is gated behind
`SkillUpgradeTreeValidator.FindOwningAssets` resolving to exactly one `SkillGemDefinition` owner
with an embedded composite root, and shares `SkillPayloadAssetUtility.CreateEmbeddedStepPayload` /
`RemoveEmbeddedStepPayload` with the skill's own inspector so a step never ends up unassigned or
orphaned. It tracks its own dirty/save state, separate from the tree asset's — closing the window
or switching trees prompts to save/discard skill step changes independently of tree changes. The
shared payload utility never calls global `AssetDatabase.SaveAssets()`; the invoking window owns
the save boundary, so editing skill steps does not persist unrelated dirty assets.

## Conditional status route regression checks

Conditional status applications live in `ConditionalStatusRoute` fields, and every editor consumer
resolves "who does this land on?" from `[SkillStatusRouteTarget]` via `SkillStatusRouteMetadata`.
`Tools/RB/Skills/Run Status Effect Authoring Smoke Tests` covers the regressions that matter here:

- Routes are discovered from the declaration alone — probe types the resolver has never heard of
  are found, including two routes with the same target on one owner (their route keys must stay
  distinct).
- A behavior-driven target is read from the owning instance, so a `HealAreaSkillPayloadDef` retargeted between
  `Self` and `Allies` moves its route with it.
- A route with no attribute, a target member that does not exist, or one of the wrong type is a
  **blocking** metadata error — never a silent fallback to `Self`.
- `Validate Active Skill Trees` reports those metadata failures as tree-level errors, and route
  resolution also blocks a field whose applications list is not present in its `SerializedObject`
  (for example, a private route field that forgot `[SerializeField]`).
- `UpgradeIdUsageScanner` labels the target from route metadata, so the tree's Gameplay Effects
  summary and the wizard's destination list cannot disagree.
- A recognized step's own bare gate (no sibling `spec`) can describe itself instead of falling back
  to `Enable <Step>`. `HealAreaSkillPayloadDef` is the first case: target mode, the `FinalSkillStats` channel
  behind Heal Power/Area Radius, and any unconditional status it applies. An unrecognized step type
  still falls back to `Enable <Step>` rather than guessing from its class name.
  `RequiredPathPreviewResolver` (`Assets/Scripts/Editor/ActiveSkill/RequiredPathPreviewResolver.cs`)
  backs the node inspector's **Required Path Preview**: it walks `requiredNodeIds` back to the tree
  root (cycle- and missing-id-safe), builds a `SkillUpgradeStatSnapshot` with the runtime formula,
  and calls `SkillInstance.GetFinalStats(null)` so the preview matches gameplay exactly for that one
  path — never labelled "Final", since an optional sibling node can still add more later. `Run
  Active Skill Core Smoke Tests` (see `Docs/SYSTEMS/SKILL_SYSTEM.md`) covers both the scanner
  summary and the resolver's chain aggregation, `mul: 0` zeroing, optional-node exclusion, and
  cycle/missing-prerequisite safety.

The one-off migration from the old per-type `conditionalApplications` schema ran through
`Tools/RB/Skills/Migrate Conditional Status Routes` (with a dry-run counterpart). It copied every
channel field-by-field — upgrade id, effect reference, stacks, modifier override and its list,
duration/tick damage/tick interval overrides and their enable flags — and both the tool and the
legacy fields were deleted once the entry counts matched. No `conditionalApplications` schema
remains in the project; a reappearance means an asset was restored from an old revision.

## Status effect scope validation

`StatusEffectScopeValidator` (`Assets/Scripts/Editor/ActiveSkill/StatusEffectScopeValidator.cs`)
is a separate pass from the tree validator, because the relationships it checks span the whole
project rather than one tree: a unique status leaking into a second skill is invisible from inside
either tree on its own. Run it from `Tools/RB/Skills/Validate Status Effect Scopes`.

- A `StatusEffectDef` with no `StatusScope.*` label is a **Warning** (legacy asset — still usable).
- A `StatusScope.Unique` status referenced by a skill that is not its `StatusOwner.<guid>` is an
  **Error**, as is a `StatusScope.Unique` status with no owner label at all.
- The same `effectId` on two `StatusEffectDef` assets is an **Error**.

Label integrity is checked for every `StatusEffectDef` asset, including statuses that are not
referenced yet, so an orphaned Unique-without-owner or unlabelled legacy asset cannot disappear
from the report merely because no skill uses it.

Usage is discovered by walking every `SkillDefinitionBase` asset *and its sub-assets* for object
references to `StatusEffectDef`, rather than by enumerating known payload fields — a new apply site
is picked up automatically instead of silently escaping the check.

The wizard's own validation (`ActiveSkillStatusEffectAuthoringService.Validate`) runs the same
scope rules plus: a missing owning skill on a shared tree, a blank gate id, a target with no route
in the skill, a destination whose step has been deleted, a duplicate
status + gate id + target + destination, a duplicate `effectId` on a status about to be created,
and a `Multiply` modifier at or below zero (warning). Scope-repair buttons only stage their action
in the request; `Apply` refuses to write while any error is present and performs no writes before
commit, which is what makes **Cancel** safe.

`Tools/RB/Skills/Run Status Effect Authoring Smoke Tests` exercises the flow end to end against
real assets in a temporary folder (`Assets/_StatusAuthoringSmokeTests`, deleted afterwards),
because the interesting cases — asset labels, embedded payload sub-assets, and writing to private
`conditionalApplications` lists — do not exist for in-memory `ScriptableObject`s.

All of these files live under `Editor/`, so `CheckAssemblyBuild.ps1` does not compile them. Verify
changes by running the two menu items above in Unity, not by trusting a green
`CheckAssemblyBuild.ps1` run.

`Tools/RB/Skills/Validate Embedded Payloads` validates a payload ownership graph, not a fixed
sub-asset count. A composite may own its embedded root plus one unique embedded payload per
`PayloadStep`. Null/nested/external/duplicate child references and embedded payloads that are not
reachable from the root are errors. Validation reports existing orphans without deleting them.
Replacing or removing a Composite root deletes its reachable embedded descendants children-first
with Undo support. The same validation pass reports a missing payload-required timeline marker
(for example `TauntApply` or hitbox start/end) as an authoring error; direct `SkillEffectStep`
implementations require no marker because they execute at the cast point.

`CompositeSkillPayloadEditorTests` covers valid multi-payload graphs, orphan/external/duplicate
ownership, recursive replacement/removal and Undo, scoped save behavior, Composite hitbox timeline
authoring, and missing required timeline events. Run it in EditMode after changing Composite
payload authoring or validation.

# Unified Authoring Validation

Node-centric ability authoring (`Assets/Scripts/Editor/ActiveSkill/AbilityAuthoring/`, see
`Docs/SYSTEMS/SKILL_SYSTEM.md` **Node-Centric Ability Authoring**) uses a 3-level severity model —
`PayloadAuthoringSeverity.Error`/`Warning`/`Info` — for descriptor and per-payload authoring issues,
distinct from `SkillUpgradeTreeValidator`'s existing 2-level `SkillUpgradeValidationSeverity`. Error
blocks Create and Save; Warning allows Save only after one explicit confirmation; Info is guidance
only. `NodeCentricPayloadValidator.Validate` bridges the two: an `Error` maps to `Error`, and both
`Warning` and `Info` map to `Warning` — the safe direction, since folding Info into Warning can only
ask for one extra confirmation, never hide something that should have blocked.

## What NodeCentricPayloadValidator checks

Everything here is *in addition to* `SkillUpgradeTreeValidator`'s existing tree/binding rules
(duplicate grants, an id nothing declares, a declared id no node grants — see **Active Skill Tree
validation** above), which already cover most of "every node-owned id resolves to exactly one step
usage." This file adds what that validator cannot see:

- **Registry health** — `PayloadDesignerDescriptorRegistry.GetDiagnostics()` (duplicate descriptors,
  a descriptor mapped to an invalid/abstract type or to `CompositeSkillPayloadDef`) surfaces as
  tree-level errors.
- **Per-payload authoring issues** — for every node-owned `PayloadStep` with a registered descriptor,
  `descriptor.CollectAuthoringIssues` runs and its result is attributed to the exact node that grants
  that step's id (resolved by scanning `grantedUpgradeIds`, not by whichever node happens to be
  selected in the editor).
- **Missing descriptor on a node-owned step** — a payload type with no registered descriptor cannot
  have been authored through the normal wizard; if one is found gated by a real id anyway (Advanced
  mode, or a hand-edited asset), it is a blocking Error.
- **Direct gameplay step** — any `SkillEffectStep` that is not a `PayloadStep` is a blocking Error.
  `PayloadStep` is the only supported orchestration type; a direct-gameplay step type fuses
  orchestration and behavior, which is no longer allowed (the retired `HealAreaStep` was the last
  one — see **Legacy migration** below).
- **Non-normalized granted id** — a Warning, not an Error, since a hand-edited or pre-generator id
  still works at runtime; `AbilityBindingIdGenerator.Normalize` defines the canonical form.
- **Always-active steps are never attributed to a node** — a blank-gated `PayloadStep` is not owned
  by any node (plan section 14.4); its own authoring issues are not surfaced by this validator today
  (no Advanced/skill-level "Always Active Skill Effects" panel exists yet). The Skill Inspector's own
  `SkillGemDefinition`/`CompositeSkillPayloadDef.CollectValidationIssues` still covers it separately.

## Save integration

`ActiveSkillTreeEditorWindow.ComputeUnifiedIssues` merges `SkillUpgradeTreeValidator.Validate` and
`NodeCentricPayloadValidator.Validate` into the one list used by `EnsureIssues` (node badges, inline
issue list), the **Validate** toolbar button, and — new — the **Save** toolbar button.
`ConfirmSaveAgainstValidationIssues` runs before every save: any Error shows a blocking "Cannot Save"
dialog listing them and aborts; any Warning (no Errors) shows one consolidated "Save With Warnings?"
confirmation; a clean tree saves without a prompt. Previously **Save** had no validation gate at all
— this closes that gap.

## Smoke tests

Each menu item is `Tools/RB/Skills/Run <Name> Smoke Tests`, follows the same real-temp-asset pattern
as the Status Effect Authoring smoke tests (own temp folder, deleted on completion, no
`AssetDatabase.SaveAssets()`), and lives under `Editor/`, so `CheckAssemblyBuild.ps1` does not
compile it — verify by running the menu item in Unity, not by trusting a green
`CheckAssemblyBuild.ps1` run:

- **Payload Descriptor** — registry discovery/diagnostics, safe defaults and summary/issue
  generation not throwing on a fresh incomplete draft, and a missing required reference always
  reporting at least one Error.
- **Node Ability Authoring Service** — single-to-composite conversion preserving the original
  payload object and its values, root-owned execution field transfer and child reset, Create's
  auto-convert path, stable-id binding and dedup, a validation-failing draft leaving no side
  effects, Edit committing in place, Duplicate producing a unique object/id, Remove's reference-
  safety guards, and Undo restoring an entire Create in one step.
- **Ability Wizard** — draft construction/safe-defaults for Create, draft population for Edit,
  cleanup on every exit path, and an end-to-end Commit, all driven through reflection since
  `OnGUI`/button clicks cannot be simulated headlessly.
- **Node Ability Cards** — card resolution against a real node/step/payload graph (found, skipped
  when orphaned, deduped), the Duplicate action, and resolution reflecting a removal done through
  the service. (Remove's own confirmation dialog cannot be driven headlessly; its underlying
  mutation is covered by the service test above.)
- **Unified Validation** — a clean ability reporting no issues, a missing required reference
  reported as an Error on the exact granting node, a non-normalized id as a Warning, an
  always-active step never attributed to a node, and the Save gate returning immediately on a
  clean tree (built against an isolated skill/tree, not a shared fixture another test in the same
  run may have deliberately made invalid — see the caution below).

**Caution:** any test that calls a method reachable from `ConfirmSaveAgainstValidationIssues` or
otherwise capable of popping a real `EditorUtility.DisplayDialog` must not run against a fixture a
different test in the same suite has left in an Error state, and must never be exercised through
automated tooling without a human able to dismiss the dialog — a stuck modal blocks the entire Unity
process, not just the test run.

## Legacy migration (completed, tooling removed)

The project's only direct-gameplay step, `HealAreaStep`, was migrated to `HealAreaSkillPayloadDef` +
`PayloadStep` in `Aires_Skill_3.asset` (the only asset that used it) through a one-time
Dry-Run/Apply migration tool, then the tool and the legacy type were both deleted — the same pattern
already used for the earlier Conditional Status Route migration (see **Conditional status route
regression checks** above). Before deletion, every one of these held:

- a project-wide Dry Run reported zero remaining `HealAreaStep` usages
- `Validate Embedded Payloads` and `Validate Active Skill Trees` both passed
- the Active Skill smoke tests passed
- the migrated skill validated cleanly (`SkillUpgradeTreeValidator`: 0 errors;
  `NodeCentricPayloadValidator`: 0 issues; `CompositeSkillPayloadDef.CollectValidationIssues`: 0
  issues) and reloaded from disk with no missing-type warning
- a project-wide text search for `class: HealAreaStep` across `Assets/Data` returned nothing

A `HealAreaStep` reference reappearing in any asset means it was restored from an old revision, not
that migration is still in progress — there is nothing left to migrate, and the type no longer
exists to deserialize into.

# Character animation validation

`CharacterAnimBrainSmokeTests`
(`Assets/Scripts/Editor/Animator/CharacterAnimBrainSmokeTests.cs`) is the
EditMode safety net for `CharacterAnimBrain` playback lifecycle and command
admission. Run it before and after any change to the Brain, its partial state
files, or `CharacterAnimDriver`.

It builds a real Animancer graph — the graph does initialise outside Play Mode —
but **the graph never advances time**. Every assertion is therefore driven by a
cast point of `0` (which the chain poll satisfies on the first tick) or by
invoking the state's own end-of-clip callback the way Animancer would. Coverage:

- skill / utility / chain-cutscene / chain-skill lifecycles and their signal order
- exactly one terminal per request, and none for a caller-requested cancel
- a completion handler starting the next chain in the same frame
- chain playback rejecting every external animation command
- root-motion policy flags for skill playback
- stage intro and hard status handing `applyRootMotion` back on exit
- animator/profile rebind interrupting active playback exactly once
- missing clip / missing profile failing safely with no events
- teardown not replaying terminal events
- the binding fast path: a steady-state tick not re-resolving the hierarchy,
  `InvalidateAnimationBinding()` forcing exactly one full resolve, and an
  Animator or `baseStats.animProfile` swap rebinding on its own
- session lifecycle: two teardown paths racing one request still emitting a
  single terminal, a request never seeing both `Completed` and `Interrupted`, and
  a completed chain being delivered every beat it skipped before its terminal
- root motion: one coherent published policy, the façade never disagreeing with
  it, the policy clearing when playback ends or the Brain is disabled, adapters
  receiving the current policy on registration, and a registered adapter taking
  `Animator.applyRootMotion` over from the Brain

`CharacterAnimationTransitionPolicyTests`
(`Assets/Scripts/Editor/Animator/CharacterAnimationTransitionPolicyTests.cs`)
guards animation priority. Its `ObservedTransitionMatrixMatchesTheAuthoredTable`
test drives a real Brain through every (current mode, requested mode) pair and
compares against a literal table that was captured from the implementation
**before** `CharacterAnimationTransitionPolicy` existed. That table is the
contract, not a restatement of the policy: if a cell changes, gameplay priority
changed. Update the table in the same commit as the policy so the new priority is
the reviewed artefact — never to make a red test go green.

Three cells are asymmetries rather than obvious rules, and are deliberate:

- `FullBodyReload -> Chain` is blocked while `FullBodyReload -> Skill` is allowed.
  A skill calls `StopReloadAction()` first, which clears the reload's exit lock;
  chain playback does not.
- `Skill -> Skill` is blocked but `Skill -> Utility` is allowed. Only skill
  admission consults `IsShootBlockingPlaybackActive`.
- Knockback blocks hard status poses too, not just soft ones.

These files live under `Editor/`, so `CheckAssemblyBuild.ps1` does not compile
them. A green build says nothing about these suites — run them in Unity.

`CheckAssemblyBuild.ps1` also cannot catch a name collision with a `UnityEditor`
type, because it never compiles the editor assembly. A new global-namespace type
shadows a same-named `UnityEditor` type for every editor script that has
`using UnityEditor;` — this is why the transition mode enum is called
`CharacterAnimationMode` and not `AnimationMode`. After adding a top-level type
with a generic name, reimport in Unity and check the Console, not just the build.

Play Mode still owns everything that needs real clip time: skill cast-point
events (they are Animancer events, not polled), the chain playback watchdog,
fade weights, root-motion delta, and prefab wiring. The prefab matrix to exercise
by hand is Player, Ally (NavMesh), an Enemy with a different hierarchy, and a
Summon/turret that uses `inspectorAnimProfile` instead of `baseStats.animProfile`.

## Animation hot-path baseline

`CharacterAnimBrain.Update` calls `TryInitialize()` every frame, which calls
`ResolveReferences()` and `ctx.ResolveReferences()`. Before changing that, record
a baseline so the change can be proved rather than assumed. This is a manual
Unity Profiler pass; no script captures it:

1. Open a MapRun stage and enter Play Mode with the Profiler recording (CPU
   Usage, Hierarchy view, Deep Profile off).
2. Capture at 1, 25, and 100 live actors.
3. Record, per frame: `CharacterAnimBrain.Update`, `TryInitialize`,
   `CharacteContext.ResolveReferences`, `GetComponentInChildren`, and GC Alloc.
4. Repeat the identical capture after the change and compare the same five
   numbers at the same three actor counts.

# Weapon affix validation

Run **Tools > Weapons > Affixes > Validate (Dry Run)** before builds. The build
preprocessor blocks missing behavior assets, duplicate ids, and invalid roll
ranges. `WeaponAffixFrameworkTests` verifies all 27 registered definitions,
endpoint rolls, structured tooltip data, Last Round eligibility, persistent-state
cloning, typed overkill metadata, and the recursion guard.

On 2026-08-03 the focused EditMode suite passed 6/6 and
`CheckAssemblyBuild.ps1` passed with 0 errors. The full EditMode run was blocked
by the pre-existing `PartySpawnUnityTests` scene-open error for
`Assets/Scenes/Map_Play_Pototype/State_1.unity`.

# Summon regression validation

`SummonContractSmokeTests` covers summon/owner attribution across sibling
hierarchies, normal-actor fallback, delayed despawn and presentation isolation,
inactive staging, no-map composite cast semantics, room-transition VFX ownership,
ground/clearance mask overlap, nested oriented-box placement, and horizontal
capsule clearance. Run it with the Unity Test Framework before checking the
Assembly-CSharp build. The focused
PlayMode pass should additionally cover melee kill, weapon projectile kill, DoT
kill after summon destruction, cap eviction with presentation delay, room commit,
rollback, mobile warp failure, and placement/NavMesh failure without consuming a
cap slot.

`CharacterPlacementBaselineTests` captures the pre-migration legacy Chain teleport
contract: the resolver stops at the first accepted candidate and preserves the
authored sweep order (`0`, `+15`, `-15` degrees). This is a baseline only; the
future central resolver may choose a different candidate after its scored result
is explicitly adopted. There is currently no direct EditMode integration coverage
for Chain Attack or Interruption controller placement, so those paths remain a
Phase 3 PlayMode test gap.

`CharacterPlacementResolverTests` covers the Phase 1 core contract without
activating gameplay adapters: wall penetration outranks actor penetration,
disabled planar root motion is evaluated as a static trajectory, the target is
ignored only inside the configured contact window, reservations contribute actor
overlap, required animation fails closed, the trajectory sweep catches a thin
world blocker between samples, a NavMesh start adjustment cannot move the
predicted target impact, Utility-tail/Attack composition keeps one continuous
impact timeline with accumulated yaw, authored order breaks equal scores,
null-safe anchor capture, runtime-policy defaults, and transient summon
reservation deduplication against a physical collider on a sibling of
`SummonedEntityRuntime`.
Run both placement suites in EditMode after the Unity instance holding the project
has been closed or after using the active Editor's Test Runner.

The focused summon regression pass also runs `SummonContractSmokeTests` after
the central summon-clearance delegation. It protects Box/Capsule/Sphere
footprint parity, unsupported-collider fallback, ground-collider exclusion,
nested/rotated footprints, horizontal capsules, least-overlap candidate search,
and the existing owner/runtime contracts. Chain and Interruption controller
scenarios that require live animation playback, utility-tail sequencing, or
PlayMode reservation timing remain integration coverage rather than EditMode
unit coverage.

# Projectile lifecycle validation

Two EditMode suites cover the pooled projectile, plus one Editor menu item.

**`ProjectileLifecycleSmokeTests`** — the runtime contract:

- an acquired projectile stays inactive and its `OnEnable` has not run yet, on reuse **and** on the
  very first `Instantiate`,
- at activation, `OnEnable` observes the final layer, context, direction, depth, split generation,
  and config,
- a despawned projectile goes inactive and is handed back out by the pool,
- reuse as a weapon bullet clears the previous life's AoE, crit, presentation assets, skill source,
  and collision-ignore root,
- world slow scales both travel speed and lifetime accrual (0 = frozen, 0.5 = half),
- a split child is `parent.depth + 1` and `parent.splitGeneration + 1`,
- `SplitOnHitModule` clamps `childCount` and `maxSplitGenerations`,
- a runaway split chain terminates at `Projectile.AbsoluteMaxSplitGeneration`,
- an inherited split budget cannot be widened by a permissive child config, keeps narrowing by
  `min` down the chain, treats `0` as "never split", and survives a spawn that authors no budget,
- `ProjectileSplitGraphAnalyzer` detects a cyclic `childConfig` graph and leaves acyclic ones alone.

**`ProjectileAuthoringValidationTests`** — the authoring contract:

- a second component driving the root Rigidbody is rejected, a presentation-only component is not,
- every gameplay projectile prefab has a single movement/lifetime owner,
- the prefab sweep actually finds prefabs and never reaches vendor folders,
- no shipped split configuration loops back on itself,
- no **new** broken projectile/bullet prefab reference. Known blockers are listed in
  `KnownBrokenProjectileReferences` as a full `path|property|guid` triple, so a second, unrelated
  break in an already-listed prefab is still reported. The test fails both on a new break and on a
  stale entry, so repairing one forces the list to be trimmed.

**Menu item** — **Tools > Validation > Projectile Authoring Report** runs all three authoring
checks and prints the result to the console.

**Known blocker** — `Assets/Prefab/GameEnemy/Enemy_Base.prefab` has `projectilePrefab` pointing at
guid `522002f7fd0905a44ad43b9329339bca`, which no longer exists in the project. Every other
character prefab points at `BulletPlayer_Test_ModulesVer.prefab`. The intended replacement was not
guessed; `WeaponSystem` overwrites the field from `currentWeapon.BulletPrefab` on equip, which is
why the break has not been visible in play.

**Not covered by these suites** — trigger collisions, hit/area VFX, real prefab physics, and
lifetime expiry under a running physics loop remain Play Mode checks. Editor scripts are also not
covered by `CheckAssemblyBuild.ps1`; compile them through a Unity script refresh.

# Map system validation

`CheckAssemblyBuild.ps1` does not compile `Assets/Scripts/Editor`, so map
tooling and map tests are validated by letting Unity compile and then running
the Edit Mode suites in the Test Runner.

Edit Mode suites under `Assets/Scripts/Editor/MapSystem`:

| Suite | Covers |
| --- | --- |
| `MapGeneratorTests` | generator output is a valid graph across seeds |
| `RoomRuntimeContentTests` | the runtime content hierarchy and its clearing rules |
| `MapRunTransitionTests` | the room-transition transaction: commit, rollback, first-entry failure, retry, revisit caching |
| `StageCompletionTests` | the Stage Exit refusal path and the single-commit guarantee |
| `EncounterContentTests` | wave prefab selection over pools with empty slots |
| `BasementBoardPageTests` | Basement board page ownership and the preservation of hand-authored pages |
| `RoomTransitionCleanupTests` | the transition sweep spares party-owned and cached-room content |
| `RoomLifecycleListenerTests` | `IRoomLifecycleListener` drives room-specific behaviour, and only where it applies |
| `MapContentValidationTests` | every run config in the project has no error-level content defect |
| `MapContentValidatorDetectionTests` | each content rule actually fires on broken content |
| `StageProfileTests` | tuning is read from profiles, and a config missing one is rejected |
| `StageProgressSchemaTests` | the save schema version and the Stage Id alias migration |
| `MapGeneratorSweepTests` | 256 seeds per run config, plus an explicit 10,000-seed soak |
| `MapGraphStructureValidatorDetectionTests` | each graph invariant actually fires on a broken graph |
| `StageIntroSmokeTests` | stage intro rig contract |

`MapRunTransitionTests` and `StageCompletionTests` drive `MapRunController`
through `MapRunTestFixture`, which builds an in-memory run config, room
definitions, and room templates, and replaces the party warp with
`MapRunController.PartyWarpOverride`. That seam exists only for tests and is
null during play.

Edit Mode has no `SaveManager` and no `SceneLoaderSystem` singleton, so
`StageCompletionTests` can only cover the refused half of stage completion.
Verify the accepted half in Play Mode: clear the Boss, take the portal once, and
confirm XP and Stage Progress each advance exactly once.

Before touching the Basement board, run **Tools > RB Project > Map > Validate
Basement Board (Dry Run)** and read the report. After applying, re-run
`Apply Test Stage Content` a second time: the scene must show no further diff.

## Map content validation

**Tools > RB Project > Map > Validate Map Content** runs `MapContentValidator`
over every `MapRunConfigSO` in the project. New configs are discovered by asset
type, so a stage added without content cannot slip through unvalidated.

Two severities, and the difference matters:

- **Error** — the run would break, soft-lock, or silently produce wrong content.
  `MapContentValidationTests` fails on any error.
- **Warning** — authoring is degraded but runtime has a working fallback.
  Reported, never fatal.

What it checks, per config:

| Area | Rule | Severity |
| --- | --- | --- |
| Stage identity | two configs share a `Stage Id` | Error |
| Stage identity | `Stage Id` is empty, so the config is not a Test Stage | Warning |
| Config | `MapRunConfigValidator` rejects the config | Error |
| Coverage | a generatable node type has no usable room definition | Error |
| Coverage | a Combat/Elite/Ambush/Trap/Boss type has no usable encounter | Error |
| Encounter | `Boss Encounter` disagrees with `Node Type` | Error |
| Encounter | no waves, or a wave with no usable enemy prefab | Error |
| Encounter | an empty enemy prefab slot inside an otherwise usable wave | Warning |
| Enemy prefab | no `EnemyContext`, no base stats, or no `HealthSystem` | Error |
| Room prefab | no `RoomController` — runtime adds one, but with no authored sockets | Error |
| Room prefab | no `NavMeshSurface`, or a surface with no baked `NavMeshData` | Error |
| Room prefab | a direction in `Exit Mask` has no `RoomExitInteractable` | Error |
| Room prefab | two exit sockets authored in the same direction | Error |
| Room prefab | an exit socket outside the `Exit Mask` | Warning |
| Room prefab | a Combat/Elite/Ambush/Trap/Boss room with no enemy spawn points | Error |
| Room prefab | a masked direction with no entrance spawn point | Warning |
| Room prefab | a Boss room with no Stage Exit spawn point | Warning |
| Room prefab | a Heal room a Test Stage can use, with no `TestStageRecoveryStations` | Error |
| Stage Exit | prefab missing `StageExitInteractable`, `InteractableLink`, a trigger collider, or the `Interactable` layer | Error |

Rooms in this project author their entrance spawn as a `SpawnPoint`-named child
under each exit socket rather than filling `Player Spawn Points By Direction`.
Both satisfy the entrance-spawn rule; the generic `Player Spawn Point` is only
reported when neither exists.

### Generator sweep

`MapGeneratorSweepTests` generates every run config across 256 seeds and runs
both `MapPathValidator` (the runtime gate) and `MapGraphStructureValidator` on
each graph. The structure validator covers what the runtime gate does not:
unique node ids, incoming/outgoing edge symmetry, critical-path endpoints and
adjacency, and the branch-count range.

`MinBranchCount` is a guarantee here, not a preference. `MapGenerator` only logs
a warning when it runs out of branch parents, so a shortfall is caught by the
sweep instead of shipping silently.

`SoakEveryRunConfigAcrossTenThousandSeeds` is marked `Explicit` so it stays out
of the normal run, but it is cheap — about 1.5 s for 50,000 graphs on the
current configs. Run it from the Test Runner after changing the generator, the
room definitions, or the branch limits.

## Stage catalog validation

`StageCatalogValidator` runs as part of **Tools > RB Project > Map > Validate Map Content**.

| Rule | Severity |
| --- | --- |
| a `StageDefinitionSO` with no run config | Error |
| its `Stage Id` disagrees with its run config's `Stage Id` | Error |
| a stage resolves to an empty `Stage Id` | Error |
| two stages share a `Stage Id` | Error |
| a `Legacy Stage Id` is another stage's current id | Error |
| a `Legacy Stage Id` is empty, duplicated, or repeats the current id | Warning |
| a catalog lists an empty slot, or the same stage twice | Error |
| a catalog lists no stages | Warning |

The legacy-id rule is the one worth understanding: a legacy id is adopted on load, so pointing it at
a stage that is still live would silently hand that stage's saved progress to another stage.

## Profiles are required

Every `MapRunConfigSO` must reference a `MapGenerationProfileSO` and a `MapContentPoolSO`, and a
Test Stage must also reference a `StageProgressionProfileSO`. `MapRunConfigValidator` reports a
missing one as an error, so `StartRun` refuses the run and
`MapContentValidationTests` fails the suite.

The one-off migration that moved inline tuning onto profiles has been applied and its tool removed;
the run configs no longer carry inline tuning fields at all. `Tools > RB Project > Map > Apply Test
Stage Content` authors the Test Stage profiles directly, reusing whatever profile a config already
references rather than creating a second one beside it.

## Dialogue authoring

`Tools/Dialogue/Validate Dialogue Authoring` scans every `DialogueSequenceSO`,
`CharacterDialogueAnimationProfileSO`, and `DialogueProfileDatabaseSO` in the project, plus the
`DialogueStage`, `DialogueDirector`, and every `DialogueTrigger` in the open scenes.

| Reported | Why it matters |
|---|---|
| two sequences share a `dialogueId` | they share play-once completion, so finishing one silently locks the other out |
| a play-once trigger whose sequence has no `dialogueId` | completion cannot be persisted, so it replays forever |
| a line spoken by someone outside the cast | that line emphasises nobody |
| two cast entries in the same slot, or more than three entries | the stage has exactly three slots |
| a cast member with no registered pose profile | the actor stands un-posed |
| a profile with no `idlePose` | every unmapped pose leaves the actor un-posed |
| a slot missing its actor anchor, portrait camera, or RawImage; other stage/UI wiring gaps; an active `CloneStaging` root | the slot cannot render, or stripped gameplay components would wake up on the clone |
| `DialoguePresentation` not enabled in Build Settings | the stage cannot load |
| a scene actor with no id or model root, two actors sharing an id, an actor the sequence never stages, or an `npc.` key with no supplying actor | the NPC silently never appears |
| a required (non-optional) cast entry on a party role | the conversation refuses to start whenever that party slot is empty |
| `DialogueLayers.AspFeatureRenderingLayerMask` no longer matching the URP renderer | portraits render flatter than the same character in gameplay |

Run it before committing dialogue content. `CheckAssemblyBuild.ps1` covers the runtime dialogue
scripts but, as always, **not** the editor tooling — the two `Tools/Dialogue/…` menu items have to be
run in Unity to verify.
