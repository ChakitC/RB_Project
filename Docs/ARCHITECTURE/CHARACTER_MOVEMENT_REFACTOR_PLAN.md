# Character movement consolidation

Status: implemented after approval, 2026-09-29. Both migration phases are integrated.
The required C# build passed with 93 existing warnings and no errors. Unity passed
16 root-motion collision tests, 17 movement integration tests (including four
production actor prefabs), the existing nested-knockback regression, and one
Play Mode lifecycle matrix containing 18 scenarios. The later legacy removal
migrated nine prefabs. See `Docs/VALIDATION.md` for scope and rerun instructions.

Follow-up cleanup: context-owned explicit motor creation, read-only lookup and
status queries, bound two-argument movement calls, separate collision methods,
and removal of unused vertical-motor reference searches. The approved legacy
removal deletes four unused vertical-motor serialized fields, both runtime-driver
binding methods, and per-call body/root movement and placement overloads. All
callers and fixtures use the bound APIs. Context-free authored motors remain supported.

## Objective

Give player, ally and enemy movement one collision-aware displacement entry
point, then give overlapping movement behaviours one owner of the NavMeshAgent
state snapshot. Keep timing, curves, animation policy, combat state transitions,
AI path selection and gravity calculations in their existing modules.

## Current integration points

| Existing module | Move into the shared motor | Keep in the existing module |
| --- | --- | --- |
| `RootMotionCCDriver` | Controller displacement and root-motion Y ownership | Animator callback, policy subscription, yaw, existing collision-ignore lifecycle |
| `RootMotionNavMeshDriver` | Controller/body displacement, collision slide, agent suspension and synchronization | Animator callback, policy subscription, yaw, recovery timing and serialized settings |
| `PlayerMovementCC` | Walking and overlap-separation displacement | Input, speed ramp, facing, gameplay admission |
| `DashSystem` | Dash displacement | Duration, speed, invincibility, perfect dodge and combat events |
| `CharacterKnockbackMotor` | Displacement, agent suspension and synchronization | Curves, reactions, interruption, stopping rules and AI destination policy |
| `CharacterVerticalMotor` | Vertical displacement, agent suspension and validated landing placement | Gravity, launch velocity, ground probes, landing admission and timers |
| `AgentMoveDriver` | Separation displacement and suspension token implementation | Navigation speed, move animation parameters, destinations and separation timing |
| `DefensiveBlockApproachMotor` | Validated approach displacement and agent suspension | Ground/path checks, duration, control blocks and outcome |

## Phase 1: shared displacement

1. Add `CharacterMovementMotor` in its own C# file. Resolve it through
   `CharacteContext.MovementMotor` and `ResolveReferences()`.
2. Preserve existing component types and serialized fields. Existing collision
   masks, padding, trigger policy and authored body overrides are passed to the
   motor; do not replace them with new defaults across every behaviour.
3. Prefer `ctx.cc` for displacement. Move through `CharacterController.Move` and
   return the actual applied displacement. Support a controller below the context
   without moving only the child model.
4. Otherwise use the context's authored body collider and
   `CharacterBodySweepUtility`. Root motion slides; knockback and vertical motion
   retain their existing stop-at-hit semantics. Preserve the existing knockback
   NavMesh-agent-shape fallback where it is already supported.
5. Do not convert a missing/disabled collision body into unchecked transform
   displacement. Report missing authoring once.
6. Migrate both root-motion drivers together with their regression tests first;
   compile and test that slice before migrating walk, dash, knockback and gravity.
7. Keep explicit teleports/validated placement separate from ordinary movement.
   A warp API must not become a shortcut around collision checks for normal moves.

Runtime component creation requires explicit coverage: one motor per actor,
no recursion through `ResolveReferences()`, no model-owned motor destroyed on a
model rebuild, and no automatic persistent prefab edits during inspection.

## Phase 2: NavMesh handoff and axis ownership

1. A motor-owned token API replaces independent snapshots of `isStopped`,
   `updatePosition` and `updateRotation`. Capture the original values only on the
   first suspension, and restore them only after the last relevant token ends.
2. Distinguish stopping navigation from taking ownership of position or rotation.
   A separation pause should not acquire axes it does not use.
3. Root motion, knockback, airborne motion and defensive approach each retain and
   release their own token. Keep `AgentMoveDriver`'s existing public token methods
   as forwarding methods so external callers do not break.
4. Support disable/re-enable, model rebind and release while off the NavMesh.
   Restore the captured agent, including when references change. Pending restores
   must not overwrite a newer owner or strand the agent after pooling.
5. Keep knockback's existing request to resume AI autonomy, but defer its agent
   flag changes until other movement owners have released their tokens.
6. Root-motion adapters report whether they own Y. Gravity yields only while an
   active adapter owns vertical translation; planar root motion still permits
   gravity. Disabling/replacing a driver must release its Y ownership.
7. Preserve existing animation/combat admission and interruption ordering. Do not
   introduce a new global priority table without characterizing that behaviour.

Normal NavMesh path following continues to use the agent. This refactor does not
replace pathfinding, migrate every teleport, or merge every gameplay behaviour
into a single class.

## Validation required before completion

- Run `Assets/Scripts/CheckAssemblyBuild.ps1` after each integrated code slice.
- Keep the 14 root-motion collision cases passing: thin walls, nested bodies,
  trigger model bodies with controllers, floor contact, corners, ignored character
  layers, dense self colliders, free translation, disabled controllers and rebind.
- Add movement equivalence checks for walk, dash, knockback and gravity, including
  actual distance returned when blocked and existing collision-mask overrides.
- Test overlapping root-motion/airborne/knockback suspension tokens released in
  different orders; assert original agent settings are restored only at the end.
- Test originally stopped agents, disabled agents, off-mesh release, actor pooling,
  model replacement, repeated configuration and stale token release.
- Test full-Y root motion versus planar root motion with gravity, and release on
  adapter disable/destruction.
- Exercise representative Player, Ally, helper/stryker and Enemy layouts in Unity.
- Inspect and handle any Save dialog before starting or polling Unity tests.
  Do not discard unsaved user changes or interpret a blocked dialog as a test result.

## Documentation to update with implementation

- `Docs/ARCHITECTURE/CHARACTER_CONTEXT.md`: motor resolution and shared references.
- `Docs/ARCHITECTURE/ANIMATION_COMMAND_FLOW.md`: adapters and movement/Y ownership.
- `Docs/SYSTEMS/AI_AND_TARGETING.md`: NavMesh suspension and recovery ownership.
- `Docs/PREFABS_AND_AUTHORING.md`: motor setup and existing-prefab migration.
- `Docs/VALIDATION.md`: new regression checks and how to run them.

## Main risks

Incorrect handoff can freeze navigation or restore it during another movement.
Changing collision settings can alter dash/knockback distance or character
separation. Incorrect Y ownership can suppress gravity or apply vertical movement
twice. The staged migration and lifecycle tests above are completion requirements.
