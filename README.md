# RB_Project

## Local Assembly Build

Use `BuildAssembly.ps1` from the project root for local `Assembly-CSharp` verification.

- Script: [BuildAssembly.ps1](/P:/Game_RB_Project/RB_Project/BuildAssembly.ps1)
- Implementation: [CheckAssemblyBuild.ps1](/P:/Game_RB_Project/RB_Project/Assets/Scripts/CheckAssemblyBuild.ps1)
- Artifact root: `P:\Game_RB_Project\BuildArtifacts\Assembly-CSharp\`

This avoids creating `_buildobj` and `_buildbin` under `Assets\Scripts`.

You can redirect the build artifacts somewhere else:

- One-off: `.\BuildAssembly.ps1 -ArtifactRoot 'D:\RB_Project_BuildArtifacts'`
- Persistent for your shell/session: set `RB_ASSEMBLY_BUILD_ARTIFACT_ROOT`

The artifact root must stay outside `Assets\` so Unity does not import the generated assemblies.
