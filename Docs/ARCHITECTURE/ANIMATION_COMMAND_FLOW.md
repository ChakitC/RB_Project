# Animation Command Flow

Character animation playback uses one command path:

```text
Gameplay system -> StateHub or controller -> CharacterAnimDriver -> CharacterAnimBrain
```

## Ownership

- `StateHub` owns gameplay state and continuous animation intent.
- `CharacterAnimDriver` is the only component that issues playback or mutation
  commands to `CharacterAnimBrain`.
- `CharacterAnimBrain` remains the playback engine and observable event source.
- Other systems may read Brain state, sample normalized time, and subscribe to
  Brain events. They must route playback and mutation commands through Driver.

## Status Locomotion

`CharacterAnimDriver` owns status locomotion intent resolution. It subscribes to
`StatusEffectController.EffectsChanged` and holds external/stagger reaction state.
`StatusLocomotionIntentResolver` (pure class) merges active effects, external
reactions, and stagger reactions by priority into a single intent pose.
Driver sends the resolved intent to Brain via `SetStatusLocomotionIntent`.
Brain still owns clip-availability fallback and reconciles the intent after
exclusive states (knockback, skill) end.

External callers (`StaggerMeter`, `CharacterKnockbackMotor`) continue to call
`Driver.SetExternalStatusLocomotion` / `Driver.SetStaggerStatusLocomotion` with
unchanged signatures.

## Melee Combo

`MeleeController` owns melee combo policy. It holds a `MeleeComboSession` (pure
class) that tracks the active combo, step index, input buffer, chain window, and
repeat logic. `MeleeType` is a top-level enum (no longer nested in Brain).

Flow: `MeleeController.PressMelee(type)` → session decides step →
`Brain.TryStartMeleePlayback` / `Brain.AdvanceMeleeStep`. Brain plays the clip
and emits `MeleeChainWindowOpened`, `MeleeChainWindowClosed`,
`MeleeStepCompleted`. MeleeController receives these events, asks the session,
and calls Brain to advance or complete. All callbacks are synchronous
(same-frame, no re-entrancy).

`CharacterAnimDriver.Brain` exposes the resolved Brain for read and event access.
The Driver command facade mirrors the Brain command signatures so request ids,
timing, root-motion flags, and return values remain unchanged.

## StateHub Intent

The Driver pushes continuous intent to Brain during `LateUpdate`:

- `MoveSpeed01`
- `MoveDirLocal`
- fire-hold context

Movement systems write speed and local direction to `StateHub`; they do not
write locomotion properties on Brain directly.

`StateHub.LifeStateChanged` is the primary animation source for down, revive,
and dead transitions. Driver subscribes to `HealthSystem` life events only when
no `StateHub` is available, so a transition is never handled by both sources.
Other audio and gameplay subscribers continue to use `HealthSystem` events.

## Internal Playback Channels

`CharacterAnimBrain` uses an internal `PlaybackChannel` to manage request state
for skill, utility, and chain playback subsystems. Each channel wraps a
`PlaybackRequestState` with a `PlaybackKind` tag and shared lifecycle helpers.
The public event surface (`SkillCastMomentReached`, `SkillCompleted`,
`SkillCastInterrupted`, `ChainCastMomentReached`, `ChainPlaybackCompleted`,
`ChainPlaybackInterrupted`, `PlaybackEvent`) is unchanged.

## Transition Priority

`CharacterAnimationTransitionPolicy` is the single place that decides whether one
animation mode may take locomotion from another. It is pure and stateless, so it
is directly table-testable, and it speaks in `CharacterAnimationMode` +
`CharacterAnimationTransitionReason`.

It owns **admission only**. Whether a state has a valid clip, and whether a state
is currently refusing to exit (a full-body reload inside its locked window, the
chain before `AllowChainStateExit`), stays with the state that owns that data.
What a caller observes is therefore `policy AND state checks` — a `true` from the
policy only means no priority rule objects.

The Brain exposes `CurrentAnimationMode` and routes every start command through
it. Commands that stop or cancel rather than start a mode use
`AllowsExternalCommand`, which is the chain-ownership rule on its own.

Rules, in the order they are applied:

1. Death is absorbing — nothing else begins once the death pose owns locomotion.
2. Chain playback yields only to `LifeStateOverride`, `CinematicOverride`, or
   `ExternalControlLoss`. A status effect does not qualify: a stun cannot cut a
   chain attack short.
3. One chain at a time.
4. A skill will not start on top of a skill, utility, or chain. Utility carries
   no such restriction, which is what lets a chain warp-out interrupt a skill.
5. A downed character refuses skill, utility, chain, full-body reload, and
   knockback. Dash and melee never carried a downed check.
6. Knockback needs a character that is still standing.
7. A hard status pose (stun, mini-stun, freeze, chain-ready) overrides anything
   except knockback; a soft pose only decorates an otherwise idle character.

Changing gameplay priority means changing this policy **and** the table in
`CharacterAnimationTransitionPolicyTests`, which was captured from the
implementation that predates the policy. See `Docs/VALIDATION.md`.

## Root Motion Ownership

`RootMotionPolicy` is an immutable value — `Active`, `PlanarOnly`, `ApplyYaw`,
`IgnoreCharacterCollision` — published by the Brain as one unit through a single
mutation point. Publishing the four flags together is the point: `Active` used to
be set by `EnterExclusiveLocomotion` while the shape flags were set by the
per-playback helpers, so an adapter could see root motion active while the shape
still described the previous playback.

The Brain **declares**; an adapter **applies**:

- `RootMotionCCDriver` for CharacterController actors
- `RootMotionNavMeshDriver` for NavMesh actors

Adapters call `RegisterRootMotionAdapter` and receive the current policy
immediately, so one attached mid-playback is not left a frame behind. While at
least one adapter is registered, only the adapter writes
`Animator.applyRootMotion`, moves the transform, and drives NavMeshAgent
`isStopped`/`updatePosition`/`updateRotation` and the collision-ignore token.

An adapter must release what it took **before** it stops owning it. That applies
to three moments, and all three are the same rule:

- **Unregistering** (`OnDisable`/`OnDestroy`) — restore the agent flags and the
  collision override first, or the actor stays frozen. A model rebuild disables
  the old driver exactly this way.
- **Rebinding** (`Configure`) — release the *previous* agent or CharacterController
  before pointing at the new one, then re-enter if the policy is still active.
  Rebinding first would strand the old one with nobody left to restore it.
- **Re-entering** — an adapter must never cache over its own overrides. Entering
  root motion twice without an intervening exit would capture
  `isStopped`/`updatePosition`/`updateRotation` as they were *left by the driver*
  and treat them as the agent's own settings, so the eventual restore re-applies
  them instead of undoing them. `RootMotionNavMeshDriver` guards this with
  `_hasCachedAgentState` so call order cannot break it.

When the last adapter unregisters, the Brain re-asserts the declared policy onto
the Animator instead of inheriting whatever the departing adapter left.

With **no** adapter registered the Brain writes `Animator.applyRootMotion`
itself, so Unity's built-in root motion still moves the Animator's transform.
That fallback is what characters with no adapter — summons, turrets, and any
prefab without a `CharacterVisualController` to attach one — have always used.

`RootMotionActive`, `RootMotionPlanarOnly`, `RootMotionYawActive`, and
`RootMotionIgnoresCharacterCollision` remain as a read-only façade over the
policy for existing consumers (movement, formation, targeting, interruption, the
vertical motor). They cannot disagree with the policy because they read from it.

## Playback Signal Dispatch

`PlaybackEvent` is the canonical stream. Every legacy per-subsystem event is
fanned out from it in one place — `RaisePlaybackSignal` in
`CharacterAnimBrain.PlaybackSignals.cs` — so a new phase or kind is wired once.
The mapping is derived from `(PlaybackKind, PlaybackPhase)`; call sites only
raise `EmitPlaybackSignal`.

Signals raised while the locomotion state machine is mid-transition are queued
and flushed once the outermost transition has settled. This matters because
Animancer's `StateMachine.ForceSetState` calls `OnExitState` **before** it
reassigns `CurrentState`: a terminal event raised from inside `OnExitState`
would otherwise reach handlers while `IsChainPlaybackActive` /
`IsSkillPlaybackActive` still reported the finished playback as running, and any
handler that reacted by starting the next playback was rejected.

Guarantees:

- A handler for `ChainPlaybackCompleted` / `SkillCompleted` /
  `SkillCastInterrupted` may start the next playback immediately, in the same
  frame and the same call stack. No caller needs to defer itself by a frame.
- Queue order is preserved, so the observable event order is unchanged.
- Each request produces at most one terminal (`Completed` or `Interrupted`).
  This is enforced by `PlaybackRequestState`'s status machine
  (`Idle -> Started -> CastReleased -> Completed | Interrupted`), not by each
  call site recomputing it, so two teardown paths racing the same request in one
  frame still produce exactly one event.
- A completed request has always seen its cast moment, and a completed chain has
  always seen its advance moment: any beat the clip did not reach is delivered,
  in clip order, before the terminal.
- A request cancelled through `CancelSkillCastRequest` /
  `CancelUtilityCastRequest` produces no terminal event: the caller asked for
  the cancel and is not notified of an interruption it caused itself.
  `CancelChainPlaybackRequest` does raise `Interrupted`, because the chain
  sequence runner is a different owner from the caller.

All locomotion transitions go through `TrySetLocomotionState`,
`TryResetLocomotionState`, and `ForceSetLocomotionState`, which own that
transition scope. Do not call `locomotionSM.TrySetState` and friends directly.

## Exclusive Locomotion Ownership

Skill, utility, chain, melee, full-body reload, knockback, stage intro, and hard
status poses take exclusive ownership of locomotion through
`EnterExclusiveLocomotion`, which returns the previous
`Animator.applyRootMotion`. **Every `EnterExclusiveLocomotion` must be paired
with an `ExitExclusiveLocomotion(previous)` in the state's `OnExitState`**, or
root motion stays disabled for the rest of the run — a NavMesh root-motion
driver never gets it back. `Locomotion_Dead` is the one exception: its
`CanExitState` is `false`, so it never exits.

Soft status poses do not take exclusive ownership and therefore have nothing to
restore. Disabling the Brain releases exclusive locomotion as part of teardown,
so a disabled-then-re-enabled character is never parked in a state whose request
has already been cleared.

Teardown must first release every state that can *refuse* to exit — the chain
before `AllowChainStateExit`, and either reload state inside its `_lockExit`
window — or the transition silently fails and the state stays parked. Re-enabling
does not rebuild the states when the binding is unchanged, so a state left stuck
that way stays stuck for the rest of the run.

## Context Resolution

Character prefabs must expose both `CharacterAnimDriver` and
`CharacterAnimBrain` through `CharacteContext`. Common systems resolve commands
from `ctx.AnimDriver` and read/event access from `ctx.AnimBrain` or
`ctx.AnimDriver.Brain`.

## Animation Binding

`CharacterAnimBrain` binds once to an `Animator` + `CharacterAnimProfileSO` pair
and then caches that binding. Its per-frame initialization check is reference
comparisons only — no `GetComponent` walk and no `ctx.ResolveReferences()` — so
the steady-state cost does not scale with how many context references a prefab
leaves unassigned. (An unassigned reference makes `ResolveActorComponent` run a
full self/children/parent search *every* time it is asked, which is why this was
worth moving off the hot path.)

The full resolve runs only when:

- the Brain has not initialized yet (so a prefab whose references arrive late
  keeps retrying every frame until it succeeds),
- `animancer.Animator` no longer matches the bound `Animator` — a rebuilt model,
- the resolved profile no longer matches the bound profile — a Morph override
  through `SetAnimProfileOverride`, or a swapped `baseStats.animProfile`,
- the `AnimancerComponent` or bound `Animator` was destroyed,
- `OnEnable`, or
- something calls `InvalidateAnimationBinding()` explicitly.

`CharacterVisualController.ConfigureAnimatorRuntime` calls
`ctx.AnimDriver.InvalidateAnimationBinding()` after repointing the rig. The Brain
would notice a changed `Animator` instance on its own; the explicit call also
covers the same `Animator` coming back with a new controller or avatar.

A rebind interrupts any active skill/utility request exactly once before it
rebuilds the states, so a Morph mid-cast cannot leave a request stranded.

The Driver emits one warning in Editor or Development builds when a facade
command is called without a resolved Brain.
