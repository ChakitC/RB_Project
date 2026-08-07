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
  declares through `CollectUpgradeIds`. It also warns when the payload declares an id that no
  node in the tree grants (a dead branch), and warns instead of erroring when a tree has no
  owning skill yet, since ids can't be cross-checked in that case.
- `mutuallyExclusiveNodeIds`: blank entries, self-exclusion, missing node ids, a node that both
  requires and excludes the same node, and asymmetric pairs (node A excludes B but B does not
  exclude A back) — the last one is an Error because a one-way lock only misbehaves for players
  who unlock in a specific order.
- The "no gameplay effect" warning no longer fires for a node whose only effect is granting an
  upgrade id (it used to fire on every pure-behavior node once `grantedUpgradeIds` shipped).

Two entry points: `ActiveSkillTreeEditorWindow.ValidateTree()` (open tree, logs to console) and
the project-wide `Tools/RB/Skills/Validate Active Skill Trees` menu. The tree editor window
(`Tools > RB > Skills > Active Skill Tree Editor`) also shows validation issues inline: selecting
a node displays that node's issues as `HelpBox`es below its inspector fields, with a compact count
of remaining issues elsewhere in the tree. This inline pass is cached and only recomputes when the
tree or the selected node's data changes, so it does not re-run every repaint.

`SkillUpgradeTreeValidator.cs` and `ActiveSkillTreeEditorWindow.cs` live under `Editor/`, so
`CheckAssemblyBuild.ps1` does not compile them. Verify changes to either file by opening Unity and
running the validate menu / editor window, not by trusting a green `CheckAssemblyBuild.ps1` run.

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

