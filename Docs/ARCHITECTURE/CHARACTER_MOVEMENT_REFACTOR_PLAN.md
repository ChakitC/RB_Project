# Character movement consolidation

## Held Crouch and momentum Slide (2026-10-04)

`PlayerMovementCC.Crouch.cs` and `.Slide.cs` own crouch and slide. The production
Player action map binds `Crouch` to `<Keyboard>/ctrl` (either Ctrl), routed through
the Player prefab's `OnCrouch` UnityEvent. Hold while slow to crouch; press grounded
above 1.15 times base walk speed to slide. Ctrl can also interrupt an active grounded
Dash immediately, using its actual planar displacement velocity; it need not wait
for Sprint. One slide per press; airborne/blocked presses never queue a later slide.

Entry captures actual planar locomotion displacement before canceling Sprint.
Gravity, separation, teleports and other motion owners do not contribute; Dash is
an explicit handoff through `DashSystem.TryGetPlanarVelocity`. Camera
rotation alone cannot steer. Camera-relative WASD supplies perpendicular steering
and opposing braking; diagonal input is clamped and steering never adds speed.
Defaults: lateral acceleration 0.8 times base speed per second, 45 degrees/second
maximum turn, friction 0.75 times base speed per second, opposing brake 1.0 times
base speed per second, exit at 0.6 times base speed. Full Sprint slides for about
1.3 seconds on flat ground without braking.

Slide spends stamina continuously: actual planar distance returned by
`ctx.MovementMotor.Move` each frame multiplied by the current character's
`CharacterStats.slideStaminaPerUnit` (default 2 stamina/world unit; 0 is free).
There is no entry fee. Vertical motion, separation, requested-but-blocked distance,
walking/crouching and release blending do not count. The final requested step is
limited by affordable distance. Empty stamina ends Slide in crouch while Ctrl is
held; paid Slide cannot start without stamina or its context-bound StaminaSystem.
Holding Ctrl through regeneration does not restart Slide. StaminaSystem pauses
regeneration during Slide and retains its normal delay after each spend. Small
per-frame costs are retained instead of discarded by the previous value tolerance.

Aim/fire keep Slide without walking speed penalties. Release blends back to walk
over 0.15 seconds. Slowdown/wall contact becomes crouch while held. Crouch speed is
0.45 times walk; height is 60%, clamped to capsule diameter, preserving foot height.
Standing requires clear space and retries later when blocked. Restore only a shape
still owned by crouch. Model transforms and hit zones are not resized.

Dash requires standing clearance; failure preserves Slide. Holding Ctrl through
successful Dash ends in crouch, never Sprint. Reload, melee, skill, Block, locks,
stun, knockback, down/death and exclusive motion cancel Slide. UI/pause, disable
and focus loss clear momentum and require release before a new slide. Leaving
ground ends Slide; the vertical motor remains authoritative. No Jump, slope
acceleration or slide invulnerability is added. Sprint still ignores
WASD: held A/D starts steering only after the initial Slide velocity capture.

Dash-to-Slide cancels the Dash coroutine, pending Sprint and dash invulnerability
after capturing its collision-constrained velocity. It does not charge another Dash
fee or refund the accepted Dash; subsequent Slide distance has its own cost.
A stationary/blocked or stale Dash velocity cannot
start Slide. The animation Brain leaves Dash playback as soon as Slide intent arrives,
so the old dash clip duration cannot hide the Slide animation or later restart Sprint.

Optional in-place `crouchIdle`, `crouchMove` and `slide` transitions use layer 0
without root motion. Missing clips use ordinary locomotion. Roma now uses its
looping `Roma_Slide` clip at speed 1 and 0.1-second fade; crouch idle/move still use
fallback until authored. Visual/hit-zone pose matching remains an authoring task.

## Held Dash to Sprint (2026-10-03)

`PlayerMovementCC` owns Sprint in its `PlayerMovementCC.Sprint.cs` partial.
Press Shift to dash immediately; keep it held through successful completion to
sprint initially along the captured Dash direction. Releasing Shift restores directional
walking immediately and removes the speed bonus over `sprintReleaseSeconds`.
Camera yaw now steers Sprint (2026-10-04). The movement direction smoothly turns
toward the camera's planar forward using `sprintTurnSmoothTime` (0.25 seconds)
and `sprintMaxTurnSpeed` (360 degrees/second). Facing shares this smoothed heading
so the body and travel turn together. Movement keys do not steer Sprint. Camera
pitch does not add vertical movement; a missing/vertical camera or rotation lock
retains the previous direction. Turn velocity resets on cancellation and entry.

Defaults are 1.6 times the actor's walk speed, 0.2 seconds to accelerate, and
0.12 seconds to remove the bonus. Sprint spends no extra stamina and grants no
invincibility. Dash retains its stamina, i-frame, perfect-dodge and passive rules.
`DashCompleted(Vector3)` fires only on natural completion; `DashCanceled` clears
pending Sprint on interruption/disable. Failed dashes do not arm Sprint.

Combat input, movement suspension, UI/pause, stun, knockback, down/death, disable,
focus loss, and exclusive playback cancel the pending/active sprint. Holding the
same press cannot restart an interrupted sprint. `PlayerInputHandler` forwards
both performed and canceled Dash callbacks; disable cancels Sprint and requires
a release before the next press. A release while disabled is reconciled on enable.

Sprint uses `ctx.MovementMotor.Move`; gravity stays with CharacterVerticalMotor.
A capsule sweep clips travel before walls, including oblique contact, instead of
letting controller wall sliding steer the run. Walkable normals and steps remain
available. The motor's actual displacement also detects a blocked run.
The actor clock follows `ctx.UsesWorldSlow`. Root/nested controller layouts use
the same context bindings as walking and dash.

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

## Passive stamina notifications (2026-10-04)

StaminaSystem.Spend(float) is retained. Spend(float, reason) additionally identifies
a successful payment; ResourceSpent reports only the clamped amount actually paid
and a unique FactId. Gradual Slide payments therefore accumulate their real total.
Failed/zero/invalid payments, regen, restoration and max changes do not emit Spent.

Restore(amount) returns the actual restored quantity and preserves the existing
regen delay. OnStaminaChanged still informs HUD and dynamic passive modifiers.
A passive conversion may change Max; the existing ratio-preserving policy remains.

DashSystem.ExecutionId identifies one Dash execution. PerfectDodge metadata carries
that identity, so multiple threats avoided by the same Dash can grant stamina once.
No extra movement polling is introduced for Stamina/Momentum recipes.
