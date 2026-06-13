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
- prefab-hitbox payloads contain a valid inline layout and every step group key
  resolves to a group in that layout

For C# validation, run only `Assets/Scripts/CheckAssemblyBuild.ps1` as described
in `Docs/VALIDATION.md`.
