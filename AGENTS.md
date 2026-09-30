# Repository Guidelines

These rules supplement [the parent AGENTS.md](../AGENTS.md). Read it if it is
not already in context; it owns build/test policy, MCP usage, character rules,
and documentation requirements.

## Project Structure

- Use the Unity version in `ProjectSettings/ProjectVersion.txt`.
- Gameplay code is under `Assets/Scripts/`, organized by feature. Scenes,
  prefabs, and data belong in `Assets/Scenes`, `Assets/Prefab`, and `Assets/Data`;
  settings and package declarations are in `ProjectSettings/` and `Packages/`.
- Before changing a core system, read `Docs/PROJECT_OVERVIEW.md` and the relevant
  `Docs/SYSTEMS/` or `Docs/ARCHITECTURE/` page.
- Keep `.meta` files with their assets. Do not commit generated `Library/`,
  `Temp/`, `Logs/`, build outputs, or unrelated Unity-generated changes.

## Coding Style

Use four-space indentation and braces on new lines, following nearby conventions.
Use `PascalCase` for types, methods, properties, and public APIs; `camelCase` for
parameters, locals, and serialized private fields. Prefer `[SerializeField] private`
to mutable public fields. Match each primary Unity type's filename to its type.
Preserve serialized field names unless a migration is included.

## Adding Tests

Place new Unity Test Framework coverage in a dedicated `Tests` assembly and
name fixtures `FeatureNameTests`. Follow the parent build and validation rules.

## Commits And Pull Requests

Use short, imperative, scoped commit messages and keep commits focused.
PRs should describe behavior changes, list validation, link the relevant issue
when one exists, and include screenshots or captures for visible scene, UI,
animation, or VFX changes.
