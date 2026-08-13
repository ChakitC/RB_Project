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
