# AI And Targeting

AI targeting is based on target identity, target state, target scoring, and
memory. Common identity should come from `CharacteContext.TargetIdentity`.

## Main Files

- `Assets\Scripts\AI\Ai Taget And Sensor\IAITargetable.cs`
- `Assets\Scripts\AI\Ai Taget And Sensor\AITargetInfo.cs`
- `Assets\Scripts\AI\Ai Taget And Sensor\AITargetSensor.cs`
- `Assets\Scripts\AI\Ai Taget And Sensor\AITargetingProfileDef.cs`
- `Assets\Scripts\AI\Ally Context Scripts\AllyContext.cs`
- `Assets\Scripts\AI\Ally Context Scripts\AgentMoveDriver.cs`
- `Assets\Scripts\Enemy\EnemyContext.cs`
- `Assets\Scripts\AI\ChainAttack`
- `Assets\Scripts\AI\Helper Proc`

## Target Identity

`AITargetIdentity` values:

- `Auto`
- `Generic`
- `Player`
- `Companion`
- `Enemy`
- `Neutral`

`AITargetInfo` resolves identity in this order:

1. explicit `targetIdentity`, when not `Auto`
2. legacy `targetRole` values for player/companion
3. parent `CharacteContext.TargetIdentity`
4. `Generic`

Common code should use `ctx.TargetIdentity` or `IAITargetable.TargetIdentity`
instead of hard-coded context subtype checks.

`FieldAllyMember.ActorRole` is only a chain registry and sequence-selection
key. Player- or companion-specific behavior must use the member's
`CharacteContext.TargetIdentity`.

## Target Info

`AITargetInfo` implements `IAITargetable` and exposes:

- aim point and chain attack point
- alive state
- targetable state
- team id
- target identity
- combat role
- base target priority
- threat multiplier

When `aimPoint` is not assigned in the Inspector, `AITargetInfo` searches the
character hierarchy for a transform named `spine_02.x` and uses it as the aim
point. If that bone is unavailable, it falls back to the `AITargetInfo`
transform. An explicitly assigned `aimPoint` always takes priority.

It also supports untargetable tokens. Systems can acquire and release tokens
without fighting over one boolean field.

## Target Sensor

`AITargetSensor` scans for target colliders, filters them, scores candidates,
and maintains current/visible/last-seen target state.

Important sensor concepts:

- scan interval
- target layers and obstacle layers — target layers are the only coarse membership
  filter. There is no tag filter: the sensor resolves a candidate's root through
  `CharacteContext`/`AITargetInfo`, and that root is often a child object (the
  `AI System` node) whose tag does not match the character's own tag, so tag
  matching was unreliable by construction.
- field of view
- line of sight
- team filtering
- alive filtering
- grace period and last seen target
- current target stickiness
- retarget interval and lock duration
- threat memory
- taunt target — see **Taunt resolution** below.

## Taunt resolution

Taunt state lives entirely in status effects on the character's own
`StatusEffectController`. The sensor holds **no** taunt `StatusEffectDef` of its own and never
applies the status itself: `TauntSkillPayloadDef.tauntStatus` is authored on the skill, and
`TauntSkillRuntime` applies that spec to each target and then calls
`AITargetSensor.OnTauntApplied(casterRoot)` so the sensor re-resolves, re-registers threat,
force-scans, and clears the aim-target override.

Any `StatusEffectDef` tagged `StatusEffectTags.Taunt` ("Taunt") counts as a taunt — there can be
several Taunt Defs with different VFX or modifiers, and they still compete as one taunt state.
`TauntSkillPayloadDef` validation errors if its Def is missing the tag, has `separatePerSource`
off, or does not use `StackMode.RefreshDuration`.

`UpdateTauntState` re-derives the taunter from scratch on every resolve / cache invalidation:

- **Latest wins.** Each `StatusEffectInstance` carries a monotonic `ApplicationSequence`; the
  tagged instance with the highest sequence wins. Re-applying an existing instance bumps its
  sequence (`MarkReapplied`), so a refresh counts as the newest taunt. List order is never used.
- **Fallback on expiry.** When the newest taunt expires, the next-highest still-active tagged
  instance takes over.
- **Same caster refreshes.** Re-taunting from the same source refreshes that source's instance
  (`StackMode.RefreshDuration`).
- **Different casters keep separate instances** via `separatePerSource`, which is what makes the
  fallback above possible.
- **Dead or unresolvable sources are skipped.** If `instance.Source` was destroyed or does not
  resolve to a valid tracked `CharacteContext`, the sensor moves on to the next instance.

Taunt duration comes from the skill (`fallbackDuration`) unless the application overrides it, so it
ticks in the taunted actor's own **owner-local status clock** (`StatusEffectController.Tick`) rather
than the HitLag-immune `TimeSlowManager.WorldTime` domain the rest of the sensor uses. That clock
follows `WorldDeltaTime` for actors under world slow and `Time.deltaTime` for actors holding a
world-slow exemption, so a taunt on a slowed enemy drains slowly while HitLag and pause freeze it
either way. See **Status duration and tick time semantics** in `SKILL_SYSTEM.md`.

### Who can be taunted

Faction is checked **first**, before the LayerMask, the range check, and the line-of-sight
raycast. `CharacterFactionUtility.AreHostile(caster, target)` is the single shared team rule —
`BarrierFactionUtility` is now a thin wrapper over it, so barriers and taunt cannot drift apart on
who counts as an enemy. `Auto`, `Generic`, `Neutral`, and same-side actors are never taunted, no
matter what the LayerMask includes. The mask stays as a **secondary** filter so an author can still
narrow a taunt further; it is not the faction rule and no serialized relation policy was added.

Then the sensor is asked first: `AITargetSensor.CanAcceptTaunt(casterRoot)` is a non-mutating
preflight of the same checks `OnTauntApplied` performs. If the sensor could never track this
caster, the taunt status is not applied at all — otherwise it would sit on an enemy that keeps
ignoring it. Conditional follow-up statuses (`ConditionalStatusRoute`) only fire when the Taunt
status was applied **and** `OnTauntApplied` accepted the source, so an upgrade cannot pay off a
taunt that never happened.

`TauntSkillRuntime` discovers targets through active `CharacteContext` instances and checks range
from the context transform; physics is used only for the layer filter and the optional
line-of-sight raycast. The caster's own context comes from `SkillCastContext.CasterContext`, and
each target's `StatusEffectController` through `CharacterContextModuleLookup`, so prefabs that keep
the controller on a child branch are handled the same as those that keep it on the root.

`AITargetingProfileDef` can override scoring and policy values. Local sensor
fields are fallback settings when no profile is assigned.

`HasEnemyFromSensor` uses the sensor's retained `CurrentTarget`, not only the
currently visible target. A brief field-of-view or line-of-sight interruption
therefore keeps the behavior tree in combat until the targeting profile's grace
period expires. Invalid, untargetable, or dead targets are still cleared
immediately.

## Ally Context And Movement

`AllyContext` adds:

- `AITargetSensor`
- `NavMeshAgent`
- `AgentMoveDriver`

`AgentMoveDriver` drives stable move animation state from `NavMeshAgent`
movement and writes normalized speed plus local direction into `StateHub`.
`CharacterAnimDriver` forwards that intent to `CharacterAnimBrain` during
`LateUpdate`.

It also supports companion separation from the player by resolving the player
through `CharacteContext.TargetIdentity == Player`.

### Agent Transform Suspension

`NavMeshAgent.updatePosition` makes the agent rewrite the transform from its own
NavMesh-projected position every frame. Any system that needs to drive the actor
directly — root motion, knockback, or an airborne launch — must take the agent off
transform duty first, or its motion is silently erased on the next frame. Syncing
`agent.nextPosition` alone is not sufficient.

`AgentMoveDriver.AcquireAgentTransformSuspendToken()` /
`ReleaseAgentTransformSuspendToken(token)` provide the reference-counted way to do
this. The agent is restored only when the last token is released, which is what
prevents two overlapping owners from restoring each other's cached state and
leaving the agent permanently suspended. `IsAgentTransformSuspended` reports the
current state, and companion separation skips itself while suspended.

`CharacterVerticalMotor` uses these tokens for its airborne window and re-`Warp`s
the agent on landing, since an actor launched over a ledge can come down somewhere
the agent has no valid mesh position for. See
`Docs/PREFABS_AND_AUTHORING.md` for the authoring side.

### Ally Burst Shooting

`Assets\Scripts\AI\Ally TEST Scripts\AiShoot.cs` owns the firing portion of
the ally combat loop. It uses the graph-provided target together with the
ally's `AITargetSensor`; it does not perform another target scan.

Before firing, the task requires the target to remain alive and targetable,
inside the configured planar `minFireRange` / `maxFireRange`, and in line of
sight when `requireLineOfSight` is enabled. When `faceTarget` is enabled, the
ally turns toward the target's `IAITargetable.AimPoint` and does not start the
burst until it is within `aimToleranceDegrees`. The configured `fireDuration`
therefore measures actual firing time rather than including the initial turn.

A blocked firing solution stops held fire immediately. If the solution does
not recover within `fireSolutionLossTimeout`, the task returns failure so the
Behavior Tree can choose a movement or reposition branch. Losing the target
uses `returnSuccessWhenTargetLost`. Normal completion fires for
`fireDuration`, waits for `waitDuration`, then returns success so the combat
tree can evaluate the target and position again.

The task restores the `NavMeshAgent.updateRotation` value and cancels held
fire on every normal or aborted exit. Its timers use world time for actors
whose context has `UsesWorldSlow` enabled.

### Party Formation

`PartyFormationController` on the Player owns the out-of-combat companion
layout. Formation identity comes from `CharacterContextPartyLoader.PartyIndex`,
not `FieldAllyMember.ActorRole`:

| Party index | Triangle slot | Single-file slot |
| ---: | --- | --- |
| 1 | rear-left `(-1.5, 0, -1.8)` | `(0, 0, -1.8)` |
| 2 | rear-right `(1.5, 0, -1.8)` | `(0, 0, -3.2)` |
| 3 | rear-center `(0, 0, -3.2)` | `(0, 0, -4.6)` |

The layout follows the Player's planar movement heading and retains the last
heading while the Player is stationary. It samples the desired slots at 10 Hz.
If the triangle is blocked for 0.5 seconds it collapses into a single file; the
triangle must remain clear for 1.5 seconds before it expands again.

The existing `MoveToPlayerOffsetNavMesh` and
`RandomMoveAroundPlayerNavMesh` Behavior Designer actions use formation
movement when the bound Player has a `PartyFormationController`. Their legacy
behavior remains as a fallback for test actors without a formation controller.
Formation movement starts outside 1.25 m, stops inside 0.6 m, and throttles
destination changes instead of recalculating a path every frame.

Formation movement yields to combat, active skill/root-motion playback,
reserved or busy sequence actors, interruption, knockback, stun, down, and
death. A companion more than 15 m from its slot, or without a complete path for
2 seconds, may warp to a sampled slot only when none of those higher-priority
states is active.

## Enemy Context

`EnemyContext` adds:

- enemy collider
- `AgentMoveDriver`
- `NavMeshAgent`
- `EnemyDropper`
- animator
- forced infinite reserve ammo flag

Enemy world-slow behavior is handled through `ctx.UsesWorldSlow` and the enemy
context's own world-slow application. Common systems should not branch on
`EnemyContext` unless they need enemy-only fields.

## Root Motion Trajectory Placement

Chain Attack and Guaranteed Interruption share placement code under
`Assets\Scripts\AI\TargetedSkillPlacement`. Their execution lifecycles remain
separate. Placement is represented by one immutable
`TargetedSkillPlacementResult` containing the mode, start pose, expected impact
pose, accepted yaw, and failure reason.

The target pose is sampled once before the attack animation starts. The result
is retained for the whole execution; target movement after that point does not
re-snap or re-resolve the actor.

The resolver samples the attack clip through a hidden clone of the active
Animator rig and a manual `PlayableGraph` at approximately 30 Hz. Accumulated
planar `Animator.deltaPosition` and yaw are cached by clip and Avatar.

The existing teleport profile `anchorPositionOffset` defines the actor pose at
the impact point. Chain attacks use the skill cast point. Guaranteed
Interruption uses the skill's `HitStart` Animancer marker when present and
falls back to the skill cast point. The system derives the required animation
start pose from the sampled impact transform, then tests target-relative yaw
candidates using one shared "direct-first ±15° sweep" algorithm
(`TargetedSkillSnapSidePriority`), used identically by legacy teleport,
root-motion placement, Guaranteed Interruption, Chain Attack, and Helper Chain
Attack:

`baseYaw, baseYaw±15, baseYaw±30, ... baseYaw+180`

`baseYaw` is the angle toward the side the actor currently stands on, relative
to the target anchor, when the caller supplies the actor's current position
(Player/Ally interruption do this). Chain Attack and Helper Chain Attack do not
supply an actor position, so `baseYaw` is `0` (the profile's
`anchorPositionOffset` direction) and the sweep proceeds the same way. This
sweep tries the actor-facing side first to reduce warping across the target,
then falls back through the full 360° ring before failing — there is no
separate "configured angles vs. fallback angles" split anymore. The legacy
`probeOrientation` / `allowFallbackToBaseRotation` / `orientationAngles` fields
have been removed from both `ChainAttackTeleportProfileDef` and
`HelperChainAttackSequenceDef`; every profile always sweeps. The legacy
clearance box is still opt-in per profile via `clearanceHalfExtents` /
`obstacleLayers` (exposed through `HasClearanceProbe`).

Each candidate must have clear start and impact poses against the configured
obstacle mask and must keep the sampled full-clip trajectory clear. The
designated target collider may be ignored only for the impact pose and the
sample segment that reaches it; all other obstacles remain blocking throughout
the trajectory. Every sample and sweep between adjacent samples is checked.
When the step requires NavMesh placement, the actor footprint is checked on the
NavMesh across the full trajectory.

Accepted attacks snap to the derived start pose and enable request-scoped
planar root motion. Player movement is applied through `RootMotionCCDriver`;
party and interruption ally movement is applied through
`RootMotionNavMeshDriver`. Both consume XZ translation and yaw only for this
request. The sequence does not call `FaceTarget` after placement, so the
sampled animation reaches the impact pose without a corrective snap. Clips with
no extracted displacement or yaw retain the legacy teleport flow.

Sampling or trajectory-validation errors fail placement explicitly. Legacy
teleport is used only when sampling succeeds and the clip has no meaningful
planar movement or yaw; errors never silently fall back to teleport.

At the cast callback, the runtime compares the actor pose with the sampled
impact pose. A planar error above `0.05m` or yaw error above 2 degrees emits a
warning for clip/Avatar validation.

This flow does not read a motion bone, alter an FBX importer, bake a trajectory
asset, or require new Inspector fields. Helper chain attacks remain on their
existing flow.

## Time Rules

Systems that should respect world slow should use:

- `TimeSlowManager.Instance.WorldTime`
- `TimeSlowManager.Instance.WorldDeltaTime`
- `TimeSlowManager.Instance.WorldTimeScale`

Systems that need to know whether this actor uses world slow should ask:

```csharp
bool usesWorldSlow = ctx == null || ctx.UsesWorldSlow;
```

The player context currently returns `false`; the base context returns `true`.

## Adding AI Behavior

When adding AI behavior:

1. Resolve actor references through `CharacteContext`.
2. Use `AITargetSensor` for target selection instead of duplicating scan logic.
3. Use `AITargetInfo` or `IAITargetable` for target metadata.
4. Add ally-only references to `AllyContext`.
5. Add enemy-only references to `EnemyContext`.
6. Avoid common code branches based only on `PlayerContext`, `AllyContext`, or
   `EnemyContext`.

### Behavior Designer Subtree Variables

Tasks stored in an external Behavior Designer Subtree must share data through
Graph-scoped variables declared by that Subtree. The parent `BehaviorTree`
inherits those variables and may override their initial values per prefab.

Do not bind Subtree task fields directly to GameObject- or Scene-scoped
variables. Those scopes are unavailable while the standalone Subtree asset is
being edited and serialized, so reopening or saving the graph can discard the
binding. Keep only one active `BehaviorTree` component for each authored AI
graph on a GameObject, and remove any `UnknownSharedVariable` entry after its
original variable type or obsolete authoring data has been verified.

### Target Orbit Action

`Assets\Scripts\AI\Movement\TargetOrbitNavMesh.cs` provides the
`TargetOrbitNavMesh` Behavior Designer action for controlled movement around a
shared `GameObject` target. It keeps the actor inside a configured
`minRadius`/`maxRadius` band, probes clockwise or counter-clockwise NavMesh
waypoints, and continuously replans so a moving target remains the orbit
center. `Random` direction is chosen once when the task starts.

The action can optionally face the actor toward the target while strafing.
When facing is disabled, the action leaves `NavMeshAgent.updateRotation`
unchanged. `duration = 0` runs until the tree aborts the action; a positive
duration returns success after that much active actor time. Movement locks and
root motion pause the action and its duration instead of failing it.

Navigation feel is controlled by `repathInterval` and `lookAheadAngle`. Failed
probes retry progressively shorter look-ahead angles without reversing the
configured orbit direction. A failed periodic probe keeps following the current
valid path; the failure timeout starts only when no usable path remains.
`pathFailureTimeout = 0` fails immediately; a positive value allows retries for
that many active seconds.

`moveSpeed = 0` leaves speed ownership with the character movement system. A
positive value acquires a request-scoped speed override from
`AgentMoveDriver`, which remains responsible for applying world slow. The
override and all temporary NavMeshAgent settings are released when the task
ends or is aborted.

### Enemy M_GR_01 Patrol-Orbit-Shoot Tree

`Assets\Scripts\AI\Enemy_M_GR_01_PatrolOrbitShoot.asset` is assigned to the
`BehaviorTree` on `Assets\Prefab\Enemy\Enemy_M_GR_01 Variant.prefab`.
Its repeating selector gives combat higher priority than patrol:

1. `HasEnemyFromSensor` writes the sensor's retained target to the graph-scoped
   `CurrentTarget` variable. Brief line-of-sight interruptions use the targeting
   profile's grace period instead of dropping combat immediately. This
   variant's sensor scans the `Player` and `Ally` layers, so companions are
   valid targets and compete with the player through normal scoring.
2. `TargetOrbitNavMesh` moves into a 5-7 m radial band and orbits for 2.5
   active-world seconds while facing the target. It replans every 0.25 seconds,
   keeps a valid current path when a periodic probe misses, and uses a 3-second
   path-failure timeout so the orbit phase can still finish before firing.
3. `AiShoot` faces `CurrentTarget` horizontally at 720 degrees per second,
   fires for 1.5 seconds, and waits for 0.75 seconds before the combat cycle
   reevaluates. It temporarily disables `NavMeshAgent` automatic rotation and
   restores the previous setting when the task ends.
4. When there is no visible target, `Patrol` selects random NavMesh points
   within 6 m of the locked spawn-area center and waits 1 second at each point.

The selector uses a lower-priority conditional abort, so acquiring a visible
target interrupts patrol immediately. Losing sight keeps the current combat
branch active during the sensor grace period; invalid targets or expired target
memory return the actor to patrol. Movement speed remains owned by the character
movement system because the orbit action's `moveSpeed` is zero.

## Guaranteed Interruption Command

The player can press the Interruption Command input (default G) to interrupt
an enemy that is currently inside a blockable Pre-Cast window. The command
selects either the **player** or a **ready ally** as the executor. Once the
command is accepted, the block is guaranteed regardless of collider or damage
outcome.

### Actor Selection (Decision Tree)

`InterruptionCommandController` picks the executor in this order:

1. Compute XZ distance from player to target anchor.
2. If player is ready (`PlayerInterruptionController.IsReadyForInterruption()`)
   **and** within `playerInterruptRange` (default 4 m):
   - Player placement succeeds → **Player executes**.
   - Player placement fails → fall back to ally selection. If no ally is
     available → `TeleportFailed` (no unsafe warp).
3. Otherwise (player far or not ready):
   - Ally selection succeeds → **Ally executes** (unchanged from before).
   - No ally available **and** player is ready → player warp branch: if
     placement succeeds → **Player executes (warp)**; placement fails →
     `TeleportFailed`.
   - No ally available **and** player not ready → `NoAvailableInterrupter`.

`InterruptionExecutorKind` (Ally / Player) is set on
`InterruptionCommandExecution` so subscribers can distinguish the executor.

### No-Warp Current-Pose Placement

The resolver (`TargetedSkillPlacementResolver.TryResolve`) still computes the
ideal placement first (root-motion or legacy). When the caller passes a
`noWarpStartDistance > 0` together with the actor's current position, the
resolver then checks whether the actor is already within that XZ distance of
the ideal start pose. If so, it re-validates the same
obstacle/sweep/NavMesh/trajectory checks (`ValidateTrajectory`,
`IsProbePoseClear`, `IsProbePoseOnNavMesh`) from the actor's current position
instead of the ideal one. When that check passes, the result carries
`RequiresPositionSnap = false`: the actor keeps its current position (only Y
comes from the current pose) and snaps rotation to the ideal `startRotation`,
so it turns into the attack angle immediately without a positional jump. When
the actor is outside the threshold, or the current-pose check fails (e.g. an
obstacle blocks the trajectory from there), the resolver falls back to the
original ideal placement with `RequiresPositionSnap = true` (the existing warp
flow, including `HideVisualForSnap`/fade).

`InterruptionCommandController` exposes this threshold as
`noWarpStartDistance` (default `0.5`m; `0` disables no-warp and always warps).
It is forwarded to both `PlayerInterruptionController.TryResolvePlacement` and
`AllyInterruptionController.TryResolvePlacement` overloads that accept it.
Ally selection in `TrySelectAlly` prefers any candidate that does not require
a position snap over one that does, even if the warping candidate is closer
to the target; ties within the same warp/no-warp group are broken by distance
as before.

#### No-Warp In-Place (Near Target) Fallback

If the actor does not qualify for the ideal-start no-warp above (e.g. the
skill has a long lunge so the ideal start pose is far away) but is already
within `noWarpTargetDistance` (XZ) of the target anchor, the resolver falls
back to a second no-warp tier: it accepts the actor's current pose as a
**Legacy** placement (`UsesRootMotion == false`, `RequiresPositionSnap ==
false`), facing the anchor, after the same obstacle/NavMesh checks (allowing
overlap with the target itself). Because the result is `Legacy`, the
controllers start the skill with `usePlanarRootMotion: false`, so the actor
plays the attack in place instead of driving root motion forward and
overshooting past the target it is already hugging.

Precedence order (evaluated in this order per branch, root-motion or legacy):
1. Actor within `noWarpStartDistance` of the ideal start pose → no-warp
   root-motion/legacy (full animation trajectory kept).
2. Actor not within (1) but within `noWarpTargetDistance` of the target
   anchor → in-place Legacy fallback (root motion disabled, no warp, no
   fade).
3. Neither → original warp flow (`HideVisualForSnap` → snap to ideal start →
   fade in).

`noWarpTargetDistance = 0` disables the in-place fallback entirely (tier 1
and tier 3 behave exactly as before this addition). Ally selection in
`TrySelectAlly` treats in-place candidates as no-warp candidates (via
`RequiresPositionSnap == false`), so they win over warp candidates using the
same tie-break described above.

Chain Attack (`FieldAllyTransitionController`) does not pass
`preferredActorPosition`/`noWarpStartDistance`/`noWarpTargetDistance` to the
resolver, so it keeps the defaults (`0`) and its behavior is unchanged.

### Flow — Ally Path

1. `InterruptionCommandController` (on the player) searches near
   `PlayerContext.aimTarget` for an enemy with an active blockable pre-cast
   window (`PreCastBlockController.CanBlockActiveCast()`).
2. Scans qualifying allies once. Each candidate resolves one
   `TargetedSkillPlacementResult`; the selected ally and that same result are
   returned together. Placement is not resolved a second time.
3. Reserves the ally (`FieldAllyMember.TryReserve`) and the block
   (`PreCastBlockController.TryReserveBlock`) which acquires a Pre-Cast Hold
   on the enemy animation, freezing the playhead before the cast point.
4. Ally suspends its BehaviorTree and NavMeshAgent, hides through the optional
   `ASPHelperDitherFader`, then applies the selected placement start pose once.
   Root-motion placement uses
   `HitStart` as the sampled impact point and does not call `FaceTarget` after
   the snap; legacy teleport placement still faces the target. The ally then
   locks aim on the target and starts fading in with its interruption skill via
   `CharacterSkillManager.TryStartExternalSkill`, explicitly requiring the
   `HitStart` timeline event even when the skill payload does not normally use
   timeline events. These actor modules are read
   through `AllyContext`; `FieldAllyMember` is used only for shared ally
   reservation and chain coordination.
5. The guarantee is committed only after the ally skill animation request
   starts successfully. Before that boundary, failure restores the ally's
   original pose and autonomy and calls
   `PreCastBlockController.CancelReservedBlock(...)`. This releases the hold
   and reopens the same pre-cast window when the enemy cast is still valid; the
   enemy cast is not cancelled.
6. After guarantee commit, `HitStart`, timeout, interruption, or early
   animation completion can complete the reserved block at most once. Timeout
   and interruption before `HitStart` do not apply knockback.
7. At the ally skill's real `HitStart` timeline event, the reserved block is
   completed through `CompleteReservedBlock`, which calls
   `TryCancelActiveCast(Blocked)`. Force-replace knockback is applied only when
   that block completion succeeds. If the skill clip has a `HitLag` marker and
   the block succeeded, `AllyInterruptionController` fires a one-shot HitLag
   micro-freeze through `GlobalTimeScaleManager`. The marker may be at or after
   `HitStart`; if it arrives before, the freeze is pended and fires on
   block-confirm. Cancelled, failed, or never-impacted executions produce no
   HitLag.
8. Completion and interruption are matched through request-scoped
   `CharacterAnimBrain.PlaybackEvent` signals.
9. After a successful block at `HitStart` and normal ally skill completion, the interruption
   controller rebases the ally context root to the configured motion bone
   (`c_traj` by default) while preserving the visible world pose. During the
   blend back to locomotion, `LateUpdate` compensates the visual root so the
   motion bone remains fixed in world XZ/yaw with no end-of-skill fade-out.
10. When the motion-bone pose stabilizes, the accumulated visual compensation
   is committed into the context root and the visual root returns to its cached
   local pose without moving on screen. Autonomy and the reservation are then
   restored. A timeout commits the current pose as recovery.

All exits converge on idempotent cleanup that restores BehaviorTree,
NavMeshAgent, Rigidbody, CharacterController usage, visibility, aim override,
root-motion policy, and ally reservation. If an interruption fade is stopped by
disabling the actor or fader, the material is restored to fully visible. An
explicit helper/chain fade-out-and-deactivate keeps the material hidden until
the next animation lifecycle begins.

### Flow — Player Path

1. After target is found, `InterruptionCommandController` resolves placement
   through `PlayerInterruptionController.TryResolvePlacement` (same
   `TargetedSkillPlacementResolver` as ally).
2. Reserves the block (`PreCastBlockController.TryReserveBlock`). Player does
   not use `FieldAllyMember` reservation.
3. `PlayerInterruptionController.BeginInterruption` caches the original pose,
   suspends `PlayerMovementCC` so character separation and aim rotation cannot
   displace the sampled root-motion trajectory, optionally hides via
   `ASPHelperDitherFader`, snaps to the start pose, and starts the interruption
   skill via `TryStartExternalSkill` with
   `ignoreResourceCosts: true` and `stampCooldown: false` — the interrupt
   reserves no energy and its reservation refunds the charge even on commit, so
   it does not affect the main skill's cooldown.
4. `RootMotionCCDriver` moves the player via root motion during the skill.
   After completion the player **stays at the end-of-animation position** — no
   snap-back, no visible-root rebase.
5. Guarantee commit, block completion, knockback, HitLag, timeout, and
   fallback logic are identical to the ally path.
6. Cleanup restores visibility and the prior `PlayerMovementCC.enabled` state;
   no BehaviorTree/NavMeshAgent/Rigidbody restoration is needed for the player.

### Reservation Interaction

- **PartyCommand**: `TryGetAllyCommandSlotBlockReason` checks `IsReserved`
  alongside `IsBusy`, so a reserved ally cannot be used for party commands.
- **ChainAttack**: `ChainAttackCoordinator.RunStep` already calls
  `member.TryReserve(runtime)` which fails for a foreign-token reservation.
  No chain code change is needed.
- An ally reserved for interruption cannot be selected for either system until
  the interruption completes or is cancelled.

### Debug Logging

The interruption flow has opt-in Inspector logs on all three participating
controllers:

- `InterruptionCommandController.logInterruptionFlow` logs one command summary
  under `[PreCast.Command]`, including `attemptId`, target scan counts, ally
  scan counts, player readiness/distance, and the final command result.
- `PreCastBlockController.logPreCastFlow` logs the enemy cast, pre-cast window,
  hold reservation, and block lifecycle under `[PreCast.Target]`.
- `AllyInterruptionController.logInterruptionFlow` logs warp, external skill,
  `HitStart`, block completion, visible root rebase settle/commit, fallback,
  and cleanup under `[PreCast.Ally]`.
- `PlayerInterruptionController.logInterruptionFlow` logs snap, external skill,
  `HitStart`, block completion, fallback, and cleanup under `[PreCast.Player]`.

Use `requestId` and `reservationId` to correlate target and ally messages.
Normal success logs are disabled by default to avoid Console noise. Command
failures caused by missing configuration, no available ally, failed safe-pose
placement, or rejected skill start still emit a warning with the ally scan and
safe-pose failure reason, including the last rejected yaw and placement check.

## ChainReady Manual Chain Dispatch

When an enemy's stagger meter fills, it enters **ChainReady** instead of
stunning immediately. During this window the player can aim at the ChainReady
enemy and press **F** (the Interact action) to start a **Manual Chain Attack**
on that explicit target.

### F-key dispatch flow

1. `PlayerInputHandler.OnInterrace` checks for a ChainReady target first via
   `PartyCommandController.TryExecuteChainReadyChainAttack`.
2. `TryExecuteChainReadyChainAttack` resolves the aimed target through
   `ChainAttackTargetingUtility.TryResolveLockedTarget`, then checks its
   `StaggerMeter.IsChainReady`.
3. If no ChainReady target is aimed, `NoReadyTarget` is returned and F falls
   through to normal Interact/Revive.
4. If a ChainReady target is aimed, F is **Consumed** regardless of whether
   the chain actually starts (CP/cooldown/busy blocks still eat the press).
5. On success, `ChainAttackProcController.TryStartChainReadyManualSequence`
   calls `StaggerMeter.BeginChainExecution` to pause the ChainReady timeout.
   If `SkillChainDef.HasChainReadyIntroCutscene` is true, an **intro cutscene**
   plays first (camera, world-slow, letterbox, VFX via `CutsceneSkillPresenter`).
   The chain's first step only starts after the cutscene completes. If the intro
   is interrupted or the target dies mid-cutscene, the chain aborts but the enemy
   still enters stagger. If the feature is off, the clip is unassigned, or the
   `CutsceneDirector` is busy, the chain starts immediately (no cutscene).
6. A second F press during the intro or chain is blocked by
   `meter.IsChainExecutionActive` (set at step 5).

### Auto-proc blocking

`ChainAttackProcController.CanProc` blocks auto-proc chains when
`context.Target` has a `StaggerMeter` in ChainReady state. This reserves
the window for the player's manual F chain.

**Limitation:** `AimTargetOnly` proc chains resolve their target inside
`TryStartSequence` and are not covered by this block.

### Chain completion → Stagger handoff

`ChainAttackCoordinator.SequenceFinished` fires at the single coroutine exit.
`ChainAttackProcController.OnSequenceFinished` listens for the pending
ChainReady target and calls `StaggerMeter.CompleteChainReadyAndEnterStagger`,
which transitions seamlessly from ChainReady lock to Stun lock without
restoring the agent in between.
The safety-hold invariant remains an unconditional error because a cast must
never release while its pre-cast hold reservation is active.

## Where Helper Procs Come From

`AllyHelperProcController` builds its proc list from exactly one source: the runtime helper's own
character asset, reached as

```
AllyHelperManager.HelperSkillManager -> ctx.baseStats.helperProcSlots
```

`Ally_Helper.prefab` and every party-slot rig are shared, so the previous prefab-authored
`helperDefinitions` array and the collection pass over the other party members' skill managers have
both been removed. A proc that is not on the loaded helper character does not exist.

The list is cached and rebuilt only when `AllyHelperManager.HelperLoadoutChanged` fires - the helper
rig being loaded with a different character - or when the helper's `CharacterSkillManager` reference
itself changes. The combat-bus handler runs on every published event, so it must not re-resolve the
list each time.

`FieldAllyManager` registration and the per-member `HealthSystem` subscriptions are unrelated to
this and stay: they are how the threshold trigger finds a recipient.

## Party Health Threshold Helper Proc

`SkillHelperDef.triggerMode` chooses what makes a helper proc fire:

- `CombatEventProc` (default) - the original behaviour: matches a `PassiveEventType` on the combat
  bus, rolls `procChance`, and honours `internalCooldownSeconds`.
- `PartyHealthThreshold` - fires deterministically when an eligible party member's health ratio
  drops to or below `partyHealthThreshold`. **No proc roll**: an assist the player relies on to
  survive must not be a coin flip.

The two modes never overlap. A threshold def is skipped by the combat-bus path entirely, so the
same situation cannot fire it twice.

### Eligibility and selection

- `eligibleRoles` defaults to Player / PartySlot1 / PartySlot2. An empty list falls back to that
  default rather than silently disabling the trigger.
- The Helper role is **never** eligible: it is the actor performing the assist, so it cannot also
  be the recipient.
- Down and dead members are excluded - healing a downed ally would bypass the revive rules.
- Selection is the lowest health ratio at or below the threshold. Ties go to whoever is closer to
  the player, so repeated evaluations of the same situation always pick the same recipient.

### Evaluation is event-driven

`AllyHelperProcController` never polls per frame. It re-evaluates on:

- `HealthSystem.HealthChanged` for every registered `FieldAllyMember`
- `FieldAllyManager.MemberRegistered` / `MemberUnregistered`
- a **one-shot charge wakeup** - after a cast commits, the charge pool already knows exactly when
  the next charge lands (`SkillChargeStatus.NextChargeRemaining`), so the controller sleeps that
  long and re-queries once. It re-queries rather than assuming, because max charges and cooldown
  both come from stats that could have changed while it waited.
- a queue drain that runs **only** while a request is waiting for a busy helper, since there is no
  event for "the helper stopped being busy". A queued request is fully re-validated on release,
  never replayed blindly - if nobody is hurt any more, it is dropped.

### Cooldown ownership

A threshold proc does **not** stamp `internalCooldownSeconds`. Its cooldown is the execution
skill's own charge pool, stamped when the cast transaction commits at its cast point. Two timers
for one assist would put it on two different clocks.

Note that these are genuinely different clocks: `AllyHelperProcController`'s legacy cooldowns run
on `TimeSlowManager.WorldTime`, while `SkillChargeState` runs on `Time.time`. A threshold proc
therefore obeys `Time.time`, matching every other skill cooldown in the game.

`internalCooldownSeconds` is untouched and still authoritative for `CombatEventProc` defs.

### Target lock and helper placement

`AllyHelperManager.TrySummonAllyHelperToTarget` locks the target **before** the helper is even
placed, and never re-resolves it afterwards. An assist that re-picked its recipient halfway through
the animation would heal whoever happened to be worst off at the moment of impact rather than the
one the player watched it fly toward.

Placement sweeps eight bearings on a ring around the target, starting on the far side relative to
the player so the helper does not spawn between the player and whoever they are watching. A single
random sample that landed in a wall would send the helper back to the player even though the target
was perfectly reachable from another angle.

If no bearing produces a NavMesh position, placement falls back to the usual ring around the
player. This is not a failure: the delivery travels to the target regardless of where the helper
stands. Placement is preflight - failing it entirely leaves no cast, no reservation, and above all
no cooldown, because nothing was deployed.

### Metered vs legacy helper casts

`AllyHelperManager.ExecuteHelperSkill` routes a cast through the helper's own
`CharacterSkillManager` only when a `SkillCastCostPolicy` was supplied. That path is the only one
that binds the runtime skill to a persistent per-definition charge pool.

Legacy helper procs and chain-attack steps deliberately keep the old private-orchestrator path with
`ignoreResourceCosts: true`. They have always been free and uncapped, and putting every existing
proc on a cooldown is a separate decision, not a side effect of this feature.

## Summon Targeting

Summons use `SummonContext` with `AITargetIdentity.Companion`, but they do not
participate in party runtime, party formation, pickup collection, or persistent
progression. Their runtime team id is copied from the caster's resolved
`AITargetInfo`, so normal team filtering continues to work without treating a
summon as a party slot.

Mobile summons are warped by `MapRunController` on a committed room transition;
stationary summons despawn at that boundary. Character targeting remains
context-based, while placement clearance uses the summon footprint and explicit
physics overlap checks.
