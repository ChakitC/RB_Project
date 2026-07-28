# Repository Guidelines

## Project Structure & Module Organization

This is a Unity 6 project (editor `6000.0.58f2`). Gameplay C# lives in
`Assets/Scripts/`, grouped by feature, such as `AI/`, `Inventory/`, `Passives/`,
`Weapons/`, and `UIScripts/`. Scenes, prefabs, data, and art belong in their
matching `Assets/Scenes`, `Assets/Prefab`, `Assets/Data`, and content folders.
Project-wide Unity settings are under `ProjectSettings/`; package declarations
are under `Packages/`. Read `Docs/PROJECT_OVERVIEW.md` and the relevant
`Docs/SYSTEMS/` or `Docs/ARCHITECTURE/` page before changing a core system.

Do not hand-edit generated `.csproj` or `.sln` files. Keep Unity `.meta` files
with their assets, and avoid committing generated `Library/`, `Temp/`, `Logs/`,
or build-output directories.

## Build, Test, and Development Commands

- `powershell -ExecutionPolicy Bypass -File .\BuildAssembly.ps1` — canonical
  compile validation for the default `Assembly-CSharp` sources.
- `powershell -ExecutionPolicy Bypass -File .\BuildAssembly.ps1 -ArtifactRoot D:\RB_Build`
  — runs the same check with artifacts outside the repository.
- Open the project with Unity `6000.0.58f2` — run scenes and validate serialized
  references, prefabs, animation, and gameplay behavior.

Never use `dotnet build` directly on Unity-generated project files. Build
artifacts must remain outside `Assets/`; see `Docs/VALIDATION.md`.

## Coding Style & Naming Conventions

Use C# with four-space indentation and braces on new lines. Follow nearby code
when a subsystem has an established pattern. Use `PascalCase` for types,
methods, properties, and public APIs; use `camelCase` for parameters, locals,
and serialized private fields. Prefer `[SerializeField] private` over exposing
mutable fields. Keep one primary Unity type per file and match the filename to
the type (for example, `WeaponController.cs`). Preserve stable serialized field
names unless a migration is included.

## Testing Guidelines

No repository-wide automated test suite is currently established. For every C#
change, run `BuildAssembly.ps1`; then exercise the affected flow in Unity.
Check the relevant scenes and prefabs for missing references and review the
Console for errors. If adding Unity Test Framework coverage, place tests in a
dedicated `Tests` assembly and name fixtures `FeatureNameTests`.

## Commit & Pull Request Guidelines

Recent history uses short, imperative update summaries such as
`Update_SkillTree`; prefer clearer scoped messages, for example
`Fix weapon stat refresh after equipment change`. Keep commits focused and do
not include unrelated Unity-generated churn. Pull requests should describe the
behavioral change, list validation performed, link the relevant issue, and add
screenshots or a short capture for visible scene, UI, animation, or VFX changes.
