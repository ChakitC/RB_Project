# Node-Centric Skill Payload Authoring Plan

Status: Approved implementation plan  
Audience: Follow-up Codex session, Unity programmers, technical designers  
Project: `RB_Project`  
Last updated: 2026-08-11

## 1. Purpose

This document is the implementation handoff for replacing the current technical skill-payload authoring flow with a node-centric designer workflow.

The finished workflow must allow a designer to select a skill-tree node, add and configure one or more existing programmer-defined payload types, review a gameplay summary, validate the result, and save it without opening the raw skill Inspector or manually coordinating upgrade IDs.

This plan does **not** create a no-code gameplay scripting system. Programmers still implement new runtime behavior as `SkillPayloadDef` subclasses. The editor tooling makes those existing behavior types safe and understandable for designers.

## 2. Read This Before Editing

The repository had a dirty working tree when this plan was written. Relevant modified or untracked files included:

- `Assets/Scripts/Editor/ActiveSkill/ActiveSkillTreeEditorWindow.cs`
- `Assets/Scripts/Editor/ActiveSkill/SkillUpgradeTreeValidator.cs`
- `Assets/Scripts/Editor/ActiveSkill/ActiveSkillFeatureSmokeTests.cs`
- `Assets/Scripts/Editor/ActiveSkill/UpgradeIdUsageScanner.cs`
- `Assets/Scripts/Editor/ActiveSkill/UpgradeIdUsage.cs`
- `Assets/Scripts/Player/Skill/Steps/HealAreaStep.cs`
- `Assets/Scripts/Player/Skill/Payloads/ApplyStatusSkillPayloadDef.cs`
- `Assets/Scripts/Player/Skill/Payloads/TauntSkillPayloadDef.cs`
- `Docs/SYSTEMS/SKILL_SYSTEM.md`
- `Docs/PREFABS_AND_AUTHORING.md`
- `Docs/VALIDATION.md`
- several skill and status-effect assets

Before implementation:

1. Run `git status --short` from `RB_Project`.
2. Inspect the diffs of every relevant modified file before touching it.
3. Preserve the existing edits. Do not reset, revert, or replace work that belongs to another task.
4. Reconcile this plan with the current file contents if another session has continued editing them.
5. Keep all changes scoped to node-centric ability authoring, payload descriptors, the `HealAreaStep` migration, validation, tests, and required documentation.

## 3. Confirmed Product Decisions

The following decisions were explicitly approved and should not be reopened without new user direction:

1. Designers may instantiate and configure payload types already implemented by programmers.
2. Designers do not create new runtime behavior types without code.
3. The primary entry point is the selected skill-tree node, not the global `Skill Steps` panel.
4. The node presents a `+ Add Ability` action.
5. If the skill currently has a single root payload, adding an ability automatically converts it to a composite while preserving the existing payload and its data.
6. The editor creates and binds upgrade IDs automatically.
7. Auto-generated IDs are stable after creation. Renaming a node or changing a display label must not silently rename an existing binding ID.
8. Ability creation and editing use a dedicated wizard window.
9. Every designer-facing payload type requires an editor-only descriptor.
10. The descriptor owns designer labels, curated fields, safe defaults, gameplay summaries, and authoring validation.
11. A payload without a valid descriptor is not available in the normal designer picker and is a validation error if found in designer-authored data.
12. One node may own multiple abilities.
13. Each ability card provides `Edit`, `Duplicate`, and `Remove` operations.
14. Removal is reference-safe, transactional, and Undoable.
15. Raw `Skill Steps` and raw `Granted Upgrade Ids` authoring move behind an Advanced/Developer mode.
16. Migration is explicit and project-wide: `Dry Run` first, followed by `Apply Migration` only when blocking issues are resolved.
17. Migration is never triggered automatically on project load.
18. Runtime ownership remains `SkillGemDefinition -> CompositeSkillPayloadDef -> PayloadStep -> SkillPayloadDef`.
19. The node-centric model is an editor presentation and mutation layer; it is not a second runtime database.
20. Steps own orchestration. Payloads own gameplay behavior.
21. Direct gameplay implementations such as `HealAreaStep` must migrate to payload implementations.
22. The new system will not retain a parallel legacy authoring path after migration is verified.
23. Errors block creation and save. Warnings require confirmation. Informational messages do not block work.

## 4. Current Implementation Facts

These facts were verified against the project when the plan was created:

- `SkillGemDefinition` owns one embedded root `SkillPayloadDef`.
- `CompositeSkillPayloadDef` currently owns a `[SerializeReference] List<SkillEffectStep>`.
- `PayloadStep` wraps an embedded `SkillPayloadDef` and inherits `requiredUpgradeId` from `SkillEffectStep`.
- `HealAreaStep` is a direct `SkillEffectStep` that contains gameplay behavior and serialized status configuration.
- `SkillPayloadAssetUtility.GetPayloadTypes()` discovers concrete payload types through Unity `TypeCache`.
- `SkillPayloadAssetUtility.ReplaceWithEmbedded()` replaces and destroys the previous payload graph. It cannot be used as-is for data-preserving conversion to a composite.
- `SkillPayloadAssetUtility.CreateEmbeddedStepPayload()` creates an embedded payload for an existing `PayloadStep`.
- `ActiveSkillTreeEditorWindow` already contains a `Skill Steps` panel and can add a `PayloadStep` with a selected payload type.
- The tree panel intentionally does not render a nested payload Inspector because changing IMGUI height inside the UI Toolkit scroll view causes scroll resets in the project Unity version.
- `UpgradeIdUsageScanner` discovers `requiredUpgradeId` usages and provides data for the node's gameplay-effect summary.
- `SkillUpgradeNodeData.grantedUpgradeIds` is the runtime grant side of a node-to-behavior binding.
- The project currently validates embedded payload ownership and active skill trees through separate tools.

Relevant starting points:

- `Assets/Scripts/Player/Skill/SkillPayloadDef.cs`
- `Assets/Scripts/Player/Skill/SkillGemDefinition.cs`
- `Assets/Scripts/Player/Skill/Payloads/CompositeSkillPayloadDef.cs`
- `Assets/Scripts/Player/Skill/Steps/SkillEffectStep.cs`
- `Assets/Scripts/Player/Skill/Steps/PayloadStep.cs`
- `Assets/Scripts/Player/Skill/Steps/HealAreaStep.cs`
- `Assets/Scripts/Editor/SkillPayloadAssetUtility.cs`
- `Assets/Scripts/Editor/SkillGemDefinitionEditor.cs`
- `Assets/Scripts/Editor/ActiveSkill/ActiveSkillTreeEditorWindow.cs`
- `Assets/Scripts/Editor/ActiveSkill/SkillUpgradeTreeValidator.cs`
- `Assets/Scripts/Editor/ActiveSkill/UpgradeIdUsageScanner.cs`

## 5. Problem Statement

The current flow is technically functional but not designer-friendly:

- Designers must understand `Composite`, `PayloadStep`, and `requiredUpgradeId`.
- Designers must coordinate a step's `requiredUpgradeId` with a node's `grantedUpgradeIds`.
- Deep payload configuration is split between the tree window and the skill Inspector.
- Converting a single-payload skill through the current Inspector replacement flow destroys the old embedded payload graph after confirmation.
- New payload types appear through `TypeCache`, but the editor does not automatically know how to describe their gameplay meaning, required fields, or safe defaults.
- `HealAreaStep` makes the Step/Payload boundary ambiguous because the step itself implements gameplay behavior.
- Generic summaries such as `Enable <Payload>` expose implementation terminology rather than the player-facing result.

## 6. Goals

### 6.1 Primary designer goal

A designer can complete this flow without opening the Skill Inspector:

1. Open `Tools > RB > Skills > Active Skill Tree Editor`.
2. Open a tree with exactly one `SkillGemDefinition` owner.
3. Select a node.
4. Click `+ Add Ability`.
5. Select a programmer-defined ability type.
6. Configure required designer-facing fields in a wizard.
7. Review a gameplay summary and validation results.
8. Click `Create Ability`.
9. See the new ability card on the selected node.
10. Save and validate.
11. Enter Play Mode and observe the ability only after the node is unlocked.

### 6.2 Architecture goals

- Keep the runtime payload architecture data-driven and independent of editor descriptors.
- Make Step and Payload responsibilities unambiguous.
- Use existing serialized runtime data as the source of truth.
- Avoid a mirrored editor-only ability database.
- Preserve existing payload data during automatic single-to-composite conversion.
- Make every asset mutation Undoable and cancellation-safe.
- Make migration explicit, inspectable, and idempotent.
- Make incomplete new payload integrations fail visibly.

## 7. Non-Goals

- No visual scripting system.
- No designer-authored runtime payload classes.
- No nested `CompositeSkillPayloadDef` payloads.
- No external or shared payload assets.
- No change to combat formulas, skill stat aggregation, targeting rules, or save-game node ownership unless required to preserve existing behavior.
- No automatic background migration.
- No runtime dependency on editor descriptors, `UnityEditor`, or editor-only reflection.
- No inline nested payload Inspector inside the tree scroll view.
- No broad redesign of the active skill runtime or skill-tree progression model.

## 8. Target Architecture

```text
SkillUpgradeTreeDefinition
  Node
    grantedUpgradeIds
      stable ability binding id

SkillGemDefinition
  payload: CompositeSkillPayloadDef
    steps
      PayloadStep
        requiredUpgradeId: same stable binding id
        payload: concrete SkillPayloadDef sub-asset

Editor only
  PayloadDesignerDescriptorRegistry
    descriptor for each designer-facing SkillPayloadDef type
```

### 8.1 Responsibility boundary

| Layer | Responsibility | Must not own |
|---|---|---|
| Tree node | Player progression and granting stable ability IDs | Payload implementation or execution configuration |
| Composite payload | Ordered execution of several steps | Payload-specific designer UI |
| Step | Orchestration: order, gate, and future timing/repeat controls | Gameplay effect implementation |
| Payload | Runtime gameplay behavior and serialized gameplay configuration | Tree-node editing UI |
| Descriptor | Editor labels, curated wizard UI, safe defaults, summaries, authoring validation | Runtime execution |

### 8.2 Core invariants

After migration and legacy removal:

1. Every node-authored ability resolves to exactly one `PayloadStep`.
2. Every node-authored `PayloadStep` has exactly one embedded child payload.
3. Every node-authored `PayloadStep.requiredUpgradeId` is non-empty.
4. Exactly one node in the owning tree grants that ID unless a future feature explicitly defines shared grants.
5. A node may grant several IDs because it may own several ability cards.
6. Every child payload is uniquely owned by one `PayloadStep`.
7. Every designer-facing child payload has exactly one valid descriptor.
8. `CompositeSkillPayloadDef` cannot be selected as a child ability payload.
9. Always-active execution may remain as a root single payload until the first additional ability is created.
10. Once a skill is composite, any always-active behavior is represented by a `PayloadStep` with a blank gate, while node-owned abilities use stable non-blank gates.
11. Direct gameplay step types are invalid after migration.

## 9. Editor-Only Descriptor System

### 9.1 Suggested file layout

Create a focused editor-only folder, for example:

```text
Assets/Scripts/Editor/ActiveSkill/AbilityAuthoring/
  IPayloadDesignerDescriptor.cs
  PayloadDesignerDescriptorBase.cs
  PayloadDesignerDescriptorRegistry.cs
  PayloadDesignerContext.cs
  PayloadGameplaySummary.cs
  PayloadAuthoringIssue.cs
  Descriptors/
    ProjectilePayloadDesignerDescriptor.cs
    PrefabHitboxPayloadDesignerDescriptor.cs
    ApplyStatusPayloadDesignerDescriptor.cs
    SpawnPickupPayloadDesignerDescriptor.cs
    MorphPayloadDesignerDescriptor.cs
    TauntPayloadDesignerDescriptor.cs
    HealAreaPayloadDesignerDescriptor.cs
```

Names may be adjusted to match nearby style, but keep runtime and editor code physically separated.

### 9.2 Descriptor contract

The exact API can be refined during implementation. It must support these capabilities:

```csharp
internal interface IPayloadDesignerDescriptor
{
    Type PayloadType { get; }
    string DisplayName { get; }
    string Description { get; }
    string Category { get; }

    void ApplySafeDefaults(SkillPayloadDef payload, PayloadDesignerContext context);
    void DrawWizard(SkillPayloadDef draft, PayloadDesignerContext context);
    PayloadGameplaySummary BuildSummary(SkillPayloadDef payload, PayloadDesignerContext context);
    void CollectAuthoringIssues(
        SkillPayloadDef payload,
        PayloadDesignerContext context,
        List<PayloadAuthoringIssue> issues);
}
```

Do not put this interface in a runtime assembly. Runtime payload classes must compile and execute without it.

### 9.3 Registry behavior

`PayloadDesignerDescriptorRegistry` should:

- discover concrete descriptor types through `TypeCache`;
- instantiate and cache them per editor domain reload;
- index them by exact `PayloadType`;
- report duplicate descriptors as errors;
- report invalid or abstract payload mappings as errors;
- expose only descriptor-backed payloads to the normal wizard;
- exclude `CompositeSkillPayloadDef` from child ability choices;
- expose incomplete payload integrations only in Advanced/Developer diagnostics;
- avoid scanning repeatedly during every tree repaint.

### 9.4 Safe defaults

Every designer-facing descriptor must initialize a newly created draft to a state that is either:

- valid immediately; or
- valid after the designer supplies clearly marked required asset references.

Safe defaults must not fabricate references or silently select arbitrary project assets. If a payload requires a prefab, status definition, configuration asset, animation event, or other author choice, the wizard must show the requirement and keep `Create Ability` disabled until it is resolved.

### 9.5 Gameplay summaries

Summaries must use gameplay language rather than implementation names. Depending on the payload, include:

- gameplay verb;
- target group;
- damage, healing, radius, duration, count, or other primary values;
- whether a value comes from base skill stats, an authored override, or a status definition;
- applied statuses and resolved modifiers;
- important timing or line-of-sight rules;
- required assets or missing configuration;
- warnings for values that make the behavior ineffective.

Do not reflect every serialized field and dump its name/value. The descriptor must translate configuration into gameplay meaning.

## 10. Heal Area Runtime Refactor

### 10.1 Desired result

Create `HealAreaSkillPayloadDef` under `Assets/Scripts/Player/Skill/Payloads/` and move the runtime behavior currently owned by `HealAreaStep` into it.

The new payload must preserve:

- `HealTargetMode`;
- `statusSpecApplications`;
- `conditionalStatuses`;
- self-heal behavior;
- ally discovery through active `CharacteContext` instances;
- `AITargetIdentity.Player` and `Companion` filtering;
- exclusion of the caster from ally mode;
- range checks using `FinalSkillStats.areaRadius`;
- healing through `targetContext.HealthSystem`;
- status application through `targetContext.StatusEffects`;
- heal scaling through `FinalSkillStats.healPower`;
- duration precedence: spec override, then skill effect duration, then status definition;
- conditional upgrade-ID collection and validation behavior.

Do not replace character-context targeting with physics overlap queries.

### 10.2 Migration-safe sequencing

This refactor requires two stages:

#### Stage A: compatibility and migration

- Add `HealAreaSkillPayloadDef`.
- Keep `HealAreaStep` temporarily so Unity can deserialize existing `[SerializeReference]` data.
- Add an explicit translator from the legacy step fields to the new payload fields.
- Add behavior-parity tests before changing assets.
- Implement and run migration.

#### Stage B: legacy retirement

- Verify every project asset has migrated.
- Verify a second dry run reports no legacy steps.
- Add validation that rejects direct gameplay steps.
- Remove or permanently disable the legacy authoring path.
- Delete `HealAreaStep` only after no serialized asset needs its type identity.

Deleting the class before migration would cause Unity to lose the managed-reference type and can make reliable data recovery impossible.

## 11. Stable Ability Binding IDs

### 11.1 Creation

Generate the initial candidate from stable project identifiers:

```text
<skillId>.<nodeId>.<payloadSlug>
```

Normalize it to the project's lowercase dot-separated convention. If the candidate already exists within the owning skill/tree pair, append a deterministic numeric suffix such as `.2`, `.3`, and so on.

### 11.2 Stability

After creation:

- changing a display name does not change the ID;
- renaming a node does not silently change the ID;
- changing payload configuration does not change the ID;
- replacing a payload type does not silently change the ID;
- duplicating an ability generates a new unique ID;
- migration preserves an existing valid matching ID whenever possible.

An Advanced `Regenerate ID` operation may exist, but it must show an impact preview, update all recognized references atomically, and require confirmation.

### 11.3 Binding rules

The authoring service writes the same ID to:

- `SkillUpgradeNodeData.grantedUpgradeIds` on the selected node; and
- `SkillEffectStep.requiredUpgradeId` on the created `PayloadStep`.

The editor must treat this pair as one transaction. Designers do not type either side in the normal flow.

## 12. Atomic Asset Mutation Service

Do not place complex mutation logic directly in GUI callbacks. Add a focused service, or carefully extend `SkillPayloadAssetUtility`, to own transactions.

Suggested operations:

```text
CreateNodeAbility
EditNodeAbility
DuplicateNodeAbility
RemoveNodeAbility
ConvertToCompositePreservingExecution
RegenerateAbilityId
```

### 12.1 Single-payload auto-conversion

`ConvertToCompositePreservingExecution` must not call the existing destructive replacement method as-is.

Required algorithm:

1. Confirm the skill asset is saved and the existing root payload is embedded in the same asset.
2. Validate that the current root graph is structurally supported.
3. Begin one named Undo group.
4. Create an embedded `CompositeSkillPayloadDef`.
5. Create the first `PayloadStep` as an always-active step.
6. Reuse the existing root payload object as the first step's child; do not clone and destroy it unnecessarily.
7. Transfer root-owned execution settings from the old root payload to the new composite:
   - `helperFacingMode`;
   - `chainContinueMode`;
   - `chainContinueNormalizedTime`.
8. Reset those root-owned fields on the child to their required composite-child defaults.
9. Assign the new composite as `skill.payload`.
10. Create the selected node ability as the next `PayloadStep` and embedded child payload.
11. Bind the generated upgrade ID to both the node and step.
12. Mark only the affected skill, composite, payload, and tree dirty.
13. Collapse the Undo group.
14. Do not call global `AssetDatabase.SaveAssets()` from the mutation utility. The window owns Save/Discard.

The three root-owned fields are private on `SkillPayloadDef`; use precise `SerializedObject` access or a narrowly scoped runtime API that does not introduce an editor dependency. Test field transfer explicitly.

### 12.2 Create behavior

- Build and validate a transient draft first.
- Commit only when the designer clicks `Create Ability`.
- Add the child payload as an embedded sub-asset of the owning skill.
- Add one new `PayloadStep` that uniquely owns it.
- Apply the stable binding ID to both sides.
- Leave no orphaned sub-assets if any operation fails.

### 12.3 Edit behavior

Editing must also be cancel-safe:

1. Copy the current payload into a transient draft of the same type.
2. Edit the draft in the wizard.
3. Validate the draft.
4. On confirmation, record Undo on the real payload and copy the draft's serialized values back.
5. On cancel, destroy only the draft.

Do not mutate the real payload continuously while a cancelable wizard is open.

### 12.4 Duplicate behavior

- Clone the selected payload's serialized configuration.
- Apply descriptor-defined duplicate normalization if required.
- Create a new unique embedded payload.
- Create a new step and new stable ID.
- Bind the new ID to the same selected node.
- Never make two steps reference the same child payload object.

### 12.5 Remove behavior

Removal is one Undoable transaction:

1. Resolve the exact node, step, payload, and binding ID.
2. Re-scan references immediately before mutation.
3. If an unknown or external reference uses the ID or payload, stop and show the references.
4. Remove the step from the composite.
5. Destroy its embedded payload only when no composite step references it.
6. Remove the node's granted ID only when no remaining behavior uses it.
7. Mark affected assets dirty.
8. Refresh cards and validation.

## 13. Ability Wizard

### 13.1 Window lifecycle

Create a dedicated `EditorWindow`, for example `ActiveSkillAbilityWizardWindow`.

The wizard receives an immutable request context containing:

- tree;
- owning `SkillGemDefinition`;
- selected node;
- operation mode: Create, Edit, or Duplicate;
- existing ability handle when applicable.

### 13.2 Create flow

1. Resolve that the tree has exactly one skill owner.
2. Resolve that the selected node still belongs to the tree.
3. Show only descriptor-backed payload types.
4. Create a transient `HideAndDontSave` payload draft.
5. Apply safe defaults once.
6. Draw descriptor-curated fields.
7. Recompute summary and validation as values change.
8. Disable `Create Ability` while any error exists.
9. Require confirmation when warnings exist.
10. Commit through the atomic authoring service.
11. Destroy the transient draft in success, cancel, window close, and exception paths.

### 13.3 Wizard layout

Recommended order:

1. Ability type and gameplay description
2. Target and primary effect fields
3. Required asset references
4. Status and secondary effects
5. Timing/presentation fields exposed by the descriptor
6. Gameplay summary
7. Errors and warnings
8. Collapsed `Advanced` section
9. Create/Apply and Cancel actions

Do not expose raw binding IDs in the normal section.

### 13.4 Advanced mode

Advanced mode may expose additional serialized properties or a full payload Inspector in the dedicated wizard, where it does not trigger the tree window's nested scroll-view problem.

Advanced mode must not allow ownership fields, child embedding, or upgrade bindings to be broken without going through the authoring service.

## 14. Node Ability Cards

### 14.1 Placement

In the selected node's `Gameplay Effects` section, use this reading order:

1. Numeric stat modifiers
2. `Abilities Unlocked by This Node`
3. Ability cards
4. `+ Add Ability`
5. Additional gated status-effect authoring, if that separate workflow remains
6. Advanced developer diagnostics

### 14.2 Card resolution

Cards are projections of real runtime data. Resolve them by:

1. reading the node's granted IDs;
2. scanning the owning skill's composite steps;
3. matching exact trimmed `requiredUpgradeId` values;
4. resolving the uniquely owned child payload;
5. resolving its exact descriptor;
6. building the gameplay summary.

Do not store a separate serialized card list on the tree.

### 14.3 Card contents

Each card should show:

- descriptor display name;
- concise gameplay sentence;
- target;
- primary values;
- applied statuses or secondary effects;
- required-path preview where available;
- warnings/errors;
- stable internal ID in Advanced details only;
- `Edit`, `Duplicate`, and `Remove` actions.

### 14.4 Always-active effects

A blank-gated step is not owned by a node. If the UI needs to show it, place it in a skill-level `Always Active Skill Effects` section rather than attaching it to whichever node happens to be selected.

### 14.5 Raw authoring retirement

The normal designer workflow must not expose:

- raw `PayloadStep` creation;
- direct editing of `requiredUpgradeId`;
- direct editing of `grantedUpgradeIds`;
- child payload replacement through unrelated Inspector controls.

Retain raw inspection and repair only behind an explicit Advanced/Developer mode.

## 15. Unified Validation

### 15.1 Severity model

Use a shared authoring issue model:

```text
Error   -> blocks Create and Save
Warning -> Save allowed only after explicit confirmation
Info    -> guidance only
```

### 15.2 Descriptor validation

Validate:

- every designer-facing payload type has exactly one descriptor;
- each descriptor maps to one concrete `SkillPayloadDef` type;
- `CompositeSkillPayloadDef` is not exposed as a child ability;
- safe-default initialization does not throw;
- descriptor summary and validation do not throw on incomplete drafts.

### 15.3 Ability binding validation

Validate:

- node-owned IDs are non-empty and normalized;
- every node-owned ID resolves to exactly one step usage;
- every gated ability step is granted by exactly one appropriate node;
- duplicate IDs are reported;
- unknown references are reported;
- a node with several abilities grants distinct IDs;
- stable IDs are not silently rewritten during ordinary edits.

### 15.4 Payload graph validation

Retain and extend existing rules:

- root payload is embedded;
- every child payload is embedded in the same skill asset;
- every child is reachable from the root;
- no orphaned payloads;
- no child is shared by two steps;
- no nested composite child;
- root-only execution settings live on the composite;
- payload-specific required references and timeline events are valid;
- no direct gameplay step remains after migration.

### 15.5 Save integration

The tree window's Save action must run unified validation before writing:

- errors cancel save and focus the relevant node/card when possible;
- warnings show one consolidated confirmation dialog;
- information is displayed without blocking;
- Save/Discard tracking for the tree and owning skill remains explicit.

Do not silently call global Save from lower-level utilities.

## 16. Migration Tool

### 16.1 Suggested entry points

Add explicit menu commands under `Tools > RB > Skills`, for example:

```text
Migrate Node-Centric Ability Authoring/Dry Run
Migrate Node-Centric Ability Authoring/Apply Migration
```

### 16.2 Scan scope

The migration scans all `SkillGemDefinition` assets and their reachable payload graphs. It also resolves owning upgrade trees and loadout overrides using the same ownership rules as existing active-skill validation.

### 16.3 Dry Run

Dry Run must perform no serialized writes and must report:

- skill asset path;
- owning tree path(s);
- current root payload type;
- direct legacy step count;
- gated step and node binding status;
- proposed `HealAreaStep` conversions;
- proposed ID repairs;
- payloads missing descriptors;
- shared-tree or multiple-owner ambiguity;
- orphaned or external payloads;
- blocking errors;
- warnings and proposed actions.

### 16.4 Apply Migration

Apply only assets that passed preflight. Prefer refusing the entire Apply operation when any blocking error could make a project-wide result ambiguous.

For each legacy `HealAreaStep`:

1. Read its inherited `requiredUpgradeId`.
2. Read `target`.
3. Deep-copy `statusSpecApplications`.
4. Deep-copy `conditionalStatuses` and nested applications.
5. Create an embedded `HealAreaSkillPayloadDef`.
6. Write all copied configuration to it.
7. Replace the managed-reference element with a `PayloadStep` at the same index.
8. Restore the original gate on the new step.
9. Assign the new payload to the new step.
10. Preserve step order.
11. Mark only the affected assets dirty.

For existing valid node/step ID pairs, preserve the ID. Generate or repair only missing, duplicate, or invalid bindings, and report every repair.

### 16.5 Undo and recoverability

- Use explicit Undo groups for each skill or a well-tested project-wide group.
- Do not delete unknown orphaned payloads automatically.
- Do not save automatically from the low-level migration function.
- Provide a clear completion report before the user chooses to save project assets.
- Recommend a version-control checkpoint before Apply.

### 16.6 Idempotence

After a successful Apply:

- a second Dry Run reports zero proposed mutations;
- a second Apply changes no serialized object;
- no duplicate embedded payload is created;
- no ID changes again;
- no asset becomes dirty solely from scanning.

### 16.7 Legacy removal gate

Do not remove `HealAreaStep` or disable legacy deserialization until all are true:

- project-wide Dry Run reports no legacy steps;
- `Validate Embedded Payloads` passes;
- `Validate Active Skill Trees` passes;
- Active Skill smoke tests pass;
- representative migrated skills work in Play Mode;
- a project reopen does not reintroduce or lose serialized data.

## 17. Implementation Phases

### Phase 0: Baseline and collision audit

Tasks:

- inspect current dirty diffs;
- record current validation and smoke-test results;
- identify every existing payload type;
- identify every `SkillEffectStep` subtype;
- inventory skill assets containing `HealAreaStep`;
- inventory single-payload and composite skills;
- inventory shared trees and multiple-owner cases.

Exit criteria:

- no existing edits were overwritten;
- all migration targets are known;
- baseline failures are separated from new failures.

### Phase 1: Descriptor foundation

Tasks:

- add descriptor contract, context, summary, issue type, and registry;
- add registry diagnostics;
- add descriptor coverage tests;
- implement descriptors for existing payloads that can be completed without the Heal Area refactor;
- add safe-default and incomplete-draft tests.

Exit criteria:

- registry deterministically discovers descriptors;
- duplicates and missing mappings are reported;
- normal picker data can be produced without touching runtime assemblies.

### Phase 2: Heal Area payload and parity

Tasks:

- create `HealAreaSkillPayloadDef`;
- move/copy runtime behavior with no semantic changes;
- implement its descriptor;
- keep `HealAreaStep` for migration compatibility;
- add behavior and serialization parity tests.

Exit criteria:

- new payload behavior matches the legacy step;
- conditional and unconditional status behavior matches;
- target resolution still follows `CharacteContext` rules.

### Phase 3: Atomic authoring service

Tasks:

- implement data-preserving single-to-composite conversion;
- implement stable ID generation;
- implement Create/Edit/Duplicate/Remove operations;
- implement reference scans and transaction rollback/cleanup;
- add Undo/Redo and orphan-prevention tests.

Exit criteria:

- cancel creates no persistent object;
- auto-conversion preserves the old payload object and values;
- one Undo returns the complete asset graph to its prior state.

### Phase 4: Wizard

Tasks:

- implement Create/Edit/Duplicate modes;
- implement transient drafts;
- integrate descriptor fields, summary, and issues;
- block errors and confirm warnings;
- ensure window-close cleanup.

Exit criteria:

- all existing designer-facing payload types can be configured through the wizard;
- no normal flow requires the Skill Inspector;
- no nested Inspector is placed inside the tree scroll view.

### Phase 5: Node cards and designer workflow

Tasks:

- add `+ Add Ability` to the selected node;
- resolve and render cards from real node/step/payload data;
- wire Edit/Duplicate/Remove;
- show multiple abilities per node;
- show always-active effects separately;
- move raw authoring behind Advanced/Developer mode.

Exit criteria:

- cards stay correct after save, reopen, Undo, and asset refresh;
- designer operations cannot create one-sided ID bindings.

### Phase 6: Unified validation

Tasks:

- integrate descriptor, binding, graph, tree, and payload issues;
- enforce Error/Warning/Info policy;
- add focus/navigation from an issue to its node, card, step, or payload;
- integrate validation with Save and existing validation menu tools.

Exit criteria:

- invalid assets cannot be saved through the normal tree workflow;
- warnings are consolidated and confirmable;
- existing valid assets remain valid before legacy retirement.

### Phase 7: Migration

Tasks:

- implement project-wide Dry Run;
- implement Apply Migration;
- add detailed report and idempotence checks;
- run Dry Run and resolve blockers;
- create a version-control checkpoint;
- run Apply;
- save explicitly after reviewing the report;
- run a second Dry Run.

Exit criteria:

- no direct legacy gameplay steps remain in assets;
- all binding IDs are valid;
- second Dry Run proposes no changes.

### Phase 8: Legacy retirement

Tasks:

- disable/remove direct gameplay-step authoring;
- remove compatibility-only branches;
- remove `HealAreaStep` only after serialized assets are clean;
- make direct gameplay steps a blocking validation error;
- verify a domain reload and project reopen.

Exit criteria:

- only orchestration step types remain;
- all gameplay effects are payloads;
- no legacy type is required to deserialize project assets.

### Phase 9: Documentation and final validation

Tasks:

- update system architecture and authoring docs;
- update validation docs and smoke-test coverage;
- run C# validation through the approved script;
- run editor validation tools and smoke tests;
- run manual authoring and Play Mode acceptance tests.

Exit criteria:

- documentation matches the final UI and invariants;
- all automated and manual acceptance checks pass.

## 18. Test Plan

### 18.1 Descriptor tests

- every supported payload type has exactly one descriptor;
- duplicate descriptors fail;
- missing descriptors fail;
- composite is excluded;
- safe defaults do not throw;
- summaries work for incomplete drafts;
- required references produce errors rather than fake defaults.

### 18.2 Transaction tests

- Create adds exactly one step and one embedded payload;
- Cancel adds nothing and does not dirty assets;
- Edit cancel preserves the original payload;
- Duplicate creates a unique object and unique ID;
- Remove deletes only the selected owned child;
- unknown references block removal;
- Undo/Redo restores node, step, payload, and ID together;
- exceptions leave no orphaned sub-assets.

### 18.3 Auto-conversion tests

- existing root payload object survives conversion;
- root serialized values survive;
- root-only helper/chain fields transfer to composite;
- child root-only fields reset to allowed defaults;
- new ability is appended after the preserved always-active behavior;
- conversion is one Undo operation;
- invalid external root payload blocks conversion without mutation.

### 18.4 Binding tests

- generated ID is normalized and unique;
- two abilities of the same type on one node receive different IDs;
- node rename does not change existing IDs;
- payload edit does not change IDs;
- duplicate gets a new ID;
- every node ID resolves to one step;
- orphan grants and ungranted steps are errors.

### 18.5 Heal Area migration tests

- target mode preserved;
- gate preserved;
- unconditional status list deep-copied;
- conditional route deep-copied;
- nested status overrides preserved;
- step order preserved;
- runtime self-heal behavior matches;
- runtime ally filtering/range behavior matches;
- duration precedence matches;
- migration rerun is a no-op.

### 18.6 Window tests and smoke coverage

Extend existing smoke coverage carefully because the relevant files may already contain uncommitted work:

- `Assets/Scripts/Editor/ActiveSkill/ActiveSkillFeatureSmokeTests.cs`
- `Assets/Scripts/Editor/ActiveSkill/ActiveSkillStatusEffectAuthoringSmokeTests.cs`
- `Assets/Scripts/Editor/CompositeSkillPayloadEditorTests.cs`

Add a focused test file for node-ability authoring if this prevents unrelated smoke suites from becoming too large.

### 18.7 Manual Unity acceptance matrix

Test at least:

1. Single projectile skill -> add gated Heal Area ability.
2. Existing composite skill -> add a second ability to one node.
3. One node -> two abilities of the same payload type.
4. Edit and cancel.
5. Edit and apply.
6. Duplicate and remove.
7. Undo and Redo each operation.
8. Tree with multiple owners -> authoring blocked with a clear message.
9. Payload missing a descriptor -> unavailable in normal picker and reported in validation.
10. Required prefab/status reference missing -> Create blocked.
11. Warning-only configuration -> confirmation required.
12. Save, close Unity, reopen, and confirm cards/data remain correct.
13. Play Mode with node locked -> gated ability does not run.
14. Play Mode with node unlocked -> gated ability runs.
15. Migrated Heal Area skill -> behavior matches its pre-migration result.

## 19. Validation Commands and Tools

Follow `AGENTS.md` exactly.

### C# compilation

Do not run `dotnet build` directly against Unity `.csproj` files. Run only:

```powershell
powershell -ExecutionPolicy Bypass -File 'P:\Game_RB_Project\RB_Project\Assets\Scripts\CheckAssemblyBuild.ps1'
```

### Unity editor validation

Run:

- `Tools > RB > Skills > Validate Embedded Payloads`
- `Tools > RB > Skills > Validate Active Skill Trees`
- `Tools > RB > Skills > Run Active Skill Core Smoke Tests`
- `Tools > RB > Skills > Run Status Effect Authoring Smoke Tests`
- the new descriptor/ability-authoring smoke tests
- the new migration Dry Run after Apply

Build artifacts must remain outside `Assets`.

## 20. Documentation Updates

Update these documents in the same implementation:

### `Docs/SYSTEMS/SKILL_SYSTEM.md`

- Step versus Payload responsibility;
- node-centric ability ownership model;
- descriptor requirement for new designer-facing payload types;
- stable binding-ID behavior;
- single-to-composite conversion behavior;
- removal of direct gameplay steps;
- validation invariants.

### `Docs/PREFABS_AND_AUTHORING.md`

- exact designer workflow;
- Wizard field ownership;
- Safe Default contract;
- required reference behavior;
- Advanced/Developer mode;
- migration and legacy removal notes.

### `Docs/VALIDATION.md`

- unified severity rules;
- descriptor validation;
- ability-binding validation;
- migration Dry Run/Apply procedure;
- new smoke tests and manual acceptance matrix.

Do not update third-party package documentation.

## 21. Risk Register

### Risk: overwriting current uncommitted work

Mitigation: inspect and preserve diffs before every relevant edit; keep changes surgical.

### Risk: losing legacy managed-reference data

Mitigation: retain `HealAreaStep` until migration has run and all assets have been verified after project reopen.

### Risk: destructive single-to-composite conversion

Mitigation: reuse the existing payload object, transfer root-only settings explicitly, use one Undo group, and test object identity.

### Risk: orphaned embedded payloads

Mitigation: commit only after draft validation; centralize mutations; re-scan ownership before deletion; test exception cleanup.

### Risk: one-sided upgrade-ID bindings

Mitigation: only the authoring service writes IDs in normal mode; unified validation blocks inconsistent pairs.

### Risk: payload types appear without usable designer integration

Mitigation: descriptor-backed picker and mandatory descriptor validation.

### Risk: wizard reintroduces the Unity scroll-reset issue

Mitigation: use a dedicated window and do not embed a nested payload Inspector in the tree window scroll view.

### Risk: migration modifies too many assets unexpectedly

Mitigation: explicit Dry Run, blocking preflight, detailed report, version-control checkpoint, no automatic save, and idempotence tests.

### Risk: shared tree ownership is ambiguous

Mitigation: block node-centric mutation unless the tree resolves to exactly one skill owner; report every owner and require a deliberate architectural resolution.

## 22. Definition of Done

The work is complete only when all statements are true:

- A designer can add an ability from a selected node without opening the Skill Inspector.
- Existing single-payload skill data survives automatic conversion.
- The editor creates and binds stable upgrade IDs automatically.
- One node can own several independent ability cards.
- Create, Edit, Duplicate, Remove, Undo, and Redo preserve a valid embedded payload graph.
- Every normal-picker payload has an editor-only descriptor and safe-default behavior.
- Runtime assemblies do not reference the descriptor system.
- Step owns orchestration and Payload owns gameplay behavior.
- `HealAreaStep` data has migrated to `HealAreaSkillPayloadDef` without behavior loss.
- No legacy direct gameplay step remains in serialized project assets.
- Raw authoring is hidden behind Advanced/Developer mode.
- Errors block Save; warnings require confirmation.
- Migration Dry Run is clean after Apply.
- C# validation, Unity validation tools, smoke tests, project reopen, and representative Play Mode scenarios pass.
- Required documentation is updated.

## 23. Recommended First Action for the Follow-Up Session

Do not start by building the wizard.

Start with Phase 0 and produce a short implementation audit containing:

1. current dirty diffs in all relevant files;
2. the complete payload-type inventory;
3. the complete step-type inventory;
4. all assets containing `HealAreaStep`;
5. all trees with zero, one, or multiple owners;
6. baseline results from the approved C# validator and current smoke tests;
7. any conflict between the current code and this plan.

Then implement the descriptor foundation and Heal Area parity before any destructive migration or legacy removal.
