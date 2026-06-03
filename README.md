# RB_Project

## Start Here

This is the Unity project under `P:\Game_RB_Project\RB_Project`.

Main gameplay code lives in `Assets\Scripts`. Start with these docs before
changing core systems:

- [Project Overview](Docs/PROJECT_OVERVIEW.md)
- [Validation](Docs/VALIDATION.md)
- [Character Context Architecture](Docs/ARCHITECTURE/CHARACTER_CONTEXT.md)
- [Combat Event Bus](Docs/ARCHITECTURE/COMBAT_EVENT_BUS.md)
- [Weapon System](Docs/SYSTEMS/WEAPON_SYSTEM.md)
- [Passive System](Docs/SYSTEMS/PASSIVES.md)
- [AI And Targeting](Docs/SYSTEMS/AI_AND_TARGETING.md)
- [Inventory And Items](Docs/SYSTEMS/INVENTORY_AND_ITEMS.md)
- [Prefabs And Authoring](Docs/PREFABS_AND_AUTHORING.md)

Existing focused notes:

- [Passive Event Source Architecture](PASSIVE_EVENT_SOURCE_ARCHITECTURE.md)
- [Weapon System Stats Refresh](WEAPON_SYSTEM_STATS_REFRESH.md)

## Local Assembly Build

Use `CheckAssemblyBuild.ps1` as the canonical `Assembly-CSharp` validation command:

```powershell
powershell -ExecutionPolicy Bypass -File 'P:\Game_RB_Project\RB_Project\Assets\Scripts\CheckAssemblyBuild.ps1'
```

- Canonical script: [CheckAssemblyBuild.ps1](/P:/Game_RB_Project/RB_Project/Assets/Scripts/CheckAssemblyBuild.ps1)
- Artifact root: `P:\Game_RB_Project\BuildArtifacts\Assembly-CSharp\`

This avoids creating `_buildobj` and `_buildbin` under `Assets\Scripts`.

Do not run direct Unity `.csproj` build commands for validation. See
[Validation](Docs/VALIDATION.md) for the full project rule.

You can redirect the build artifacts somewhere else:

- One-off: `powershell -ExecutionPolicy Bypass -File 'P:\Game_RB_Project\RB_Project\Assets\Scripts\CheckAssemblyBuild.ps1' -ArtifactRoot 'D:\RB_Project_BuildArtifacts'`
- Persistent for your shell/session: set `RB_ASSEMBLY_BUILD_ARTIFACT_ROOT`

The artifact root must stay outside `Assets\` so Unity does not import the generated assemblies.
