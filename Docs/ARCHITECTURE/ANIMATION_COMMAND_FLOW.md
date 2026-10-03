# Animation Command Flow

## Animancer package

The project embeds Animancer **8.4.0** at `Packages/com.kybernetik.animancer`.
It was imported from `origin/v-asset`, commit
`d7510a2bc13649a9a6352e744109e1573ac74683`, on 2026-09-30, replacing 8.2.3.
The package includes its Runtime, Editor, Art, and optional `Samples~` files.

That source snapshot retained three obsolete 8.2.3 files. The import removes
`AnimancerEventParameterTypes.cs` (duplicate event parameter classes),
`TransitionLibraryEditorData.cs` (replaced by the new editor data classes),
and `Read Me v8.2.3.asset`, together with their metadata. No project scene or
asset references their old GUIDs. Other package files retain the source snapshot.
Project Animancer settings and other package versions remain as configured on
the current branch.

## Presentation transitions

`CharacterAnimationPresentation` owns layer masks, action fades and deferred
destruction of temporary animation states. `CharacterAnimBrain.Transitions.cs` is
the public facade and playback factory. Clip states use `AnimationPlayback`;
continuous mixers and sampled Block/struggle poses use Presentation directly.
`CharacterAnimationTransitionPolicy` separates locomotion admission from
`AllowsAction` for Shoot, Reload and CommanBlock. Each state retains clip and lock checks.

The configured locomotion/action indices keep their existing meanings. The Brain
reserves `max(locomotionLayerIndex, actionLayerIndex) + 1` for full-body actions,
above the upper-body layer. Do not use that layer for other systems. Masks stay
fixed for a binding: the profile's upper-body mask and a Brain-owned full-body
mask. CommanBlock uses the latter. Completion/interruption fades its layer without
changing either mask; shooting/reloading can start underneath during the fade.

Existing profile/transition durations remain in use, including explicit Dash/Block
durations and Stage Intro's intentional cut. CommanBlock uses its authored End Time.
Presentation restores authored transition events on every replay before Timeline
attaches the new callbacks. This preserves End Time after cleanup has detached the
previous event sequence; Animancer only initializes that sequence when creating a state.
Gameplay events, locks and root-motion ownership end at the state transition;
visual fading does not extend them. Block's sampled pose stays frozen on exit.
Temporary enemy struggle states remain frozen and event-free until both weight and
target weight are zero, then are destroyed. Replaying a state through the central
entry point cancels its old cleanup. Disable clears pending cleanup and action
weights; rebind/destruction also release the owned mask.

`TryGetPairOffsetContributions` reports each upper-body state's action and weight,
multiplied by its layer weight and attenuated by full-body action coverage. The
outgoing locomotion pose description remains available during exclusive takeover
so corrections fade with the old action. Single-action query APIs remain available
for compatibility; the runtime offset applier uses the per-state contributions.

## Playback flow

Character animation playback uses one command path:

```text
Gameplay system -> StateHub or controller -> CharacterAnimDriver -> CharacterAnimBrain
```

## Internal modules (2026-09-30)

Brain owns these ordinary C# objects; no additional prefab components are needed.

| Module | Responsibility |
| --- | --- |
| `CharacterAnimationBinding` | Resolve context/Animancer, cache Animator/profile/graph and publish a generation-stamped reference snapshot. |
| `CharacterAnimationPresentation` | Clip playback, fixed masks, layer fades, actual offset contributions and temporary pose retirement. |
| `PlaybackSessionCoordinator` | Own Skill, Utility, Chain and SpecialReaction channels, terminal-once close and the CommanBlock control lease. |
| `AnimationTimelineBinding` | Attach/release event sequences, guard callbacks with playback identity, create presenters and clean up animation VFX sessions. |
| `PlaybackSignalDispatcher` | Defer immutable notifications until a command/transition has settled; preserve order during reentrant dispatch. |
| `AnimationPlayback` | One clip slot: authored events, timing overrides, callback identity and clip VFX cleanup. |
| `SingleClipActionState` / `SingleClipLocomotionState` | Shared entry/exit lifecycle for simple states, with hooks for action rules. |
| `AnimationMixerInput` | Shared directional snapping and exponential smoothing; each mixer retains its own tuning. |

Brain retains its public API/events, FSM selection and state-specific orchestration.
Block phases, Skill cutscene/approach/struggle and Chain cast/advance/watchdog rules
remain in their states. Root-motion adapters still own actor movement and restore
their movement/navigation resources.

`GetDiagnosticSnapshot()` is internal and available through the existing reflection
test/tool pattern. It returns state/mode, binding and session generations, layer
weights, pending signals/temporary cleanup, callback leases and animation VFX leases.

## Adding a simple clip state

Use the matching single-clip base and provide the existing profile transition.
The slot clones the authored sequence on every play, applies optional timing
settings, binds VFX/OnEnd and rejects stale callbacks. For example, a new
locomotion state can use this pattern (the transition is supplied by its caller):

```csharp
private sealed class Locomotion_Example : SingleClipLocomotionState
{
    private readonly ClipTransition clip;
    public Locomotion_Example(CharacterAnimBrain owner, ClipTransition clip) : base(owner)
        => this.clip = clip;
    public override bool CanEnterState => clip != null && clip.IsValid;
    protected override void EnterPlayback()
        => playback.Play(new AnimationPlaybackOptions { Transition = clip }, onEnd: endCallback);
    protected override void OnPlaybackEnd()
        => owner.TrySetLocomotionState(owner.IsDowned ? owner.crawlState : owner.locomotion);
}
```

Override `ExitPlayback` for gameplay/root-motion cleanup and request closure.
The base stops clip callbacks/VFX first. SessionCoordinator closes the request;
SignalDispatcher publishes its terminal after the transition scope ends. The
slot itself never emits a terminal or destroys an outgoing pose.

Options are runtime data, not serialized authoring. Omitted timing overrides
preserve transition settings and existing action-layer fades. `Duration` converts
clip length to speed; `NormalizedStart`, `Speed` and `FadeDuration` are explicit
overrides. `HoldLastFrame` serves Dead, StageIntro and Knockback. Set
`InvokeAuthoredEnd` when the state previously invoked authored end callbacks
(Skill, Utility and Chain); simple states retain their previous replacement behavior.
Use `configureEvents` for cast/advance/timeline rules rather than constructing a
second sequence or assigning `OnEnd`. Authored multicast callbacks check identity
between subscribers, and exceptions do not drop the remaining valid subscribers.

Each slot owns a generation and a Timeline lease in addition to binding/session
identity. An old slot's `Stop` cannot detach a newer slot's callbacks when Animancer
reuses the same state. `Stop` and `EndVfx` are repeatable. `EndVfx` lets Reload stop
its effects before the state exits. Shared transition assets never hold actor callbacks.

Utility, SpecialReaction, Reload, CommanBlock, ShootPulse, Dead, StageIntro, Dash
and Knockback use the shared bases. Skill and Chain retain their custom state
lifecycle and use slots only for clip playback. Skill cutscene/main are sequential
phases of one slot; request VFX remain owned by Timeline across the phase change.
Its concurrent temporary struggle pose stays with Presentation retirement. Crawl
uses a slot for its intro and keeps its continuous mixer. Block, Live and ShootHold
keep their existing continuous or manually sampled lifecycle.

No Transition Library, new prefab component or asset migration is required.
`ReloadAnimationPlayback` was removed; command movement/rotation tokens now live
directly in SessionCoordinator. Private forwarding wrappers were removed while
public APIs and the wrapper that records pair-offset metadata remain.

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

Flow: `StateHub.RequestMeleePress(type)` → `MeleeController` selects a step →
`CharacterSkillManager.TryStartMeleeStep` → Driver → the shared Skill playback
channel. `SkillExecutionKind.BasicMelee` keeps the observable animation mode and
playback kind as Melee. `Locomotion_Skill` binds the skill's hit/VFX timeline and
the combo's chain-window callbacks; there is no separate Melee playback state.
The controller handles chain windows and terminal playback signals, closes the
previous execution, and requests the next skill. Callbacks capture request IDs
so a previous step cannot release, advance, or terminate the next step.

`CharacterAnimDriver.Brain` exposes the resolved Brain for read and event access.
The Driver command facade mirrors the Brain command signatures so request ids,
timing, root-motion flags, and return values remain unchanged.

## StateHub Intent

The Driver pushes continuous intent in `Update` at execution order **1000**,
after normal movement writers. Brain runs its FSM/mixers once in `Update` at
**1100**. Both attributes and script `.meta` overrides use those values:

- `MoveSpeed01`
- `MoveDirLocal`
- fire-hold context

Movement systems write speed and local direction to `StateHub`; they do not
write locomotion properties on Brain directly. A movement writer added later must
run before order 1000 to meet the same-frame contract. Mixers keep their previous
smoothing and use `ctx.UsesWorldSlow` through the Brain's animation clock.

`CharacterPairOffsetApplier` restores the preceding offset in one Animancer
pre-update hook, before new animation poses are evaluated. It records an evaluation
generation and applies offsets once in `LateUpdate` at order 10000, before Aim/Axis
drivers. Render frames with no evaluation retain the last corrected pose; manual
evaluations, including a paused graph, advance the generation. Rebinding and disable
detach the hook and clear cached bones. An internal binding notification also
reconnects the hook before callers manually evaluate a newly bound graph.

`StateHub.LifeStateChanged` is the primary animation source for down, revive,
and dead transitions. Driver subscribes to `HealthSystem` life events only when
no `StateHub` is available, so a transition is never handled by both sources.
Other audio and gameplay subscribers continue to use `HealthSystem` events.

## Internal Playback Channels

`PlaybackSessionCoordinator` owns the internal `PlaybackChannel` instances for
skill, utility, chain and special reaction playback. Each channel wraps a
`PlaybackRequestState` with a `PlaybackKind` tag and shared lifecycle helpers.
Callback tokens contain channel identity, request ID, playback generation and
binding generation. Reusing an ID or rebinding the same Animator cannot revive a
previous callback. Timeline end handlers check again after authored callbacks,
which may cancel or start another playback. Accepted replacement of the same
channel exits the old state before arming the new request.

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
  frame and the same call stack when the new current state permits it. Death and
  cinematic takeovers are already in place when interruption callbacks run.
- Queue order is preserved, so the observable event order is unchanged.
- Each request produces at most one terminal (`Completed` or `Interrupted`).
  This is enforced by `PlaybackRequestState`'s status machine
  (`Idle -> Started -> CastReleased -> Completed | Interrupted`), not by each
  call site recomputing it, so two teardown paths racing the same request in one
  frame still produce exactly one event.
- A completed request has always seen its cast moment, and a completed chain has
  always seen its advance moment: any beat the clip did not reach is delivered,
  in clip order, before the terminal.
- Explicit `CancelSkillCastRequest`, `CancelUtilityCastRequest` and
  `CancelChainPlaybackRequest` each send one `Interrupted` for the matching open
  request, after the old state exits and its resources are released. Repeated or
  mismatched cancellation does nothing. Requestless legacy playback keeps its
  separate completion behavior.
- Canonical, legacy, timeline/VFX, special-reaction and root-motion notifications
  invoke subscribers separately. Exceptions are logged; later subscribers and
  queued notifications still run. Notification payloads retain the original
  request identity even when a subscriber starts another playback.
- Disable/destruction reject reentrant playback while draining cleanup. Re-enable
  invalidates the binding and permits initialization again.

Command scopes include session close, resource release and state entry.
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
`CanExitState` is `false` during gameplay; binding teardown can force it out
and release its callback lease.

Soft status poses do not take exclusive ownership and therefore have nothing to
restore. Disabling the Brain releases exclusive locomotion as part of teardown,
so a disabled-then-re-enabled character is never parked in a state whose request
has already been cleared.

Teardown must first release every state that can *refuse* to exit — the chain
before `AllowChainStateExit`, and either reload state inside its `_lockExit`
window — or the transition silently fails and the state stays parked. Binding
teardown uses an internal unbound state which does not evaluate a destroyed model.
Re-enable rebuilds the binding, even if its Animator/profile references are unchanged.

## Context Resolution

Character prefabs must expose both `CharacterAnimDriver` and
`CharacterAnimBrain` through `CharacteContext`. Common systems resolve commands
from `ctx.AnimDriver` and read/event access from `ctx.AnimBrain` or
`ctx.AnimDriver.Brain`.

## Animation Binding

`CharacterAnimationBinding` caches context, Animancer, its graph, Animator and
profile, and supplies a `CharacterAnimationBindingSnapshot` to presentation and
timeline modules. Its per-frame initialization check is reference
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

Rebind exits the old states and releases callback/VFX/presentation resources before
building replacements. Open requests close once; their terminal notifications drain
after the new binding is ready. Explicit invalidation increments the generation even
if references are unchanged. Destroying a model releases the old session while
waiting for its replacement. If fallback finds multiple Animancer components, bind
the intended one explicitly on Brain; it reports the ambiguity instead of choosing
the first child.

The Driver emits one warning in Editor or Development builds when a facade
command is called without a resolved Brain.

## Special Point Reaction

The Special Point Mini Stun is a dedicated one-shot playback
(`CharacterAnimBrain.Locomotion_SpecialReaction`), not an extension of
`Locomotion_StatusEffect`. Status locomotion is intent-driven: it holds a pose for
as long as the intent says so, runs with `usesRootMotion: false`, and has no
notion of a clip finishing. The reaction needs request identity, complete root
motion, a terminal callback, and a watchdog, and widening the status state to
cover both would have changed every generic Mini Stun in the game.

Commands go through the Driver like every other playback:

```text
SpecialShootPointController
    -> CharacterAnimDriver.TryPlaySpecialReaction(requestId, missingClipFallbackSeconds)
    -> CharacterAnimBrain.TryPlaySpecialReaction
    -> Locomotion_SpecialReaction
    -> SpecialReactionCompleted(requestId) / SpecialReactionInterrupted(requestId, reason)
```

### Priority

`CharacterAnimationMode.SpecialReaction` and
`CharacterAnimationTransitionReason.SpecialReactionOverride` place the reaction in
the table as:

```text
Death / Down > Cutscene > active Chain Attack > Special Point Mini Stun > every other reaction
```

The reaction starts over locomotion, crawl, dash, reload, melee, skill, utility,
knockback, and both status modes. It is refused over `Dead`, `StageIntro`, a
downed character, and — through `AllowsExternalCommand`, which does not admit
`SpecialReactionOverride` — an active chain. Once it owns locomotion only
`LifeStateOverride` and `CinematicOverride` may cut it short; `StatusOverride` and
`ExternalControlLoss` deliberately may not, because the reaction *is* the stagger
reaction. `SpecialPointReactionPrioritySmokeTests` asserts the whole table.

`TryPlaySpecialReaction` returns `false` only for those refusals. A **missing clip
is not a refusal**: the state holds the gameplay lock for the profile's
`missingClipFallbackSeconds`, logs one warning, and then completes normally, so the
ChainReady handoff still happens.

### Root motion

The reaction is the first playback to need full animation translation — Y
included — *and* animation yaw. `RootMotionPolicy.WithShape` previously coupled
yaw to `planarOnly`, so a four-argument overload now authors `planarOnly`,
`applyYaw`, `ignoreCharacterCollision`, and `environmentSafe` independently. The
two-argument overload still couples yaw to planar motion for existing skill and
chain playback.

Both root-motion adapters submit translation to `ctx.MovementMotor`.
`RootMotionNavMeshDriver` requests collision sliding on every playback, including Skill
and Chain Attack. When `ctx.cc` exists the motor uses `CharacterController.Move`.
The NavMesh adapter holds a motor-owned suspension token. This also covers allies
whose animated model body is a trigger. An enabled controller can live on the
context root or below it; resolved movement is applied to the whole actor.

Without a controller, the motor sweeps the solid `CharacterPositionCollider`
against `environmentCollisionMask`. It slides the remaining translation along
blocking surfaces and sweeps each segment again, so downward animation can move
along the floor without crossing walls. Missing/disabled/trigger-only bodies
stop translation with a warning instead of applying unchecked movement. A
disabled controller also stops translation. Trigger volumes do not block sweeps.

`IgnoreCharacterCollision` excludes the Player, Enemy, and Ally layers only.
Controller exclusions already owned by other systems are preserved and restored
after the move. Put blocking world geometry on world layers with solid colliders;
controller movement uses the controller's collision settings, while body sweeps
use the driver's mask. Model rebuilds copy that mask, padding, and recovery radius.

`RootMotionPolicy.EnvironmentSafe` now only opts into recovery to a nearby valid
NavMesh position at the end of playback. Collision safety does not depend on this
flag. `ResyncAgent` alone cannot recover an actor that is no longer on the mesh.
Exit recovery waits when another movement owner still holds the transform; that
owner retains responsibility for its landing/recovery.

Adapters report full-Y ownership to the motor whenever their policy changes.
`CharacterVerticalMotor` reads this shared state: planar clips permit gravity,
while full-Y clips suppress it. Disable, destruction and model rebind release the
adapter's ownership and navigation token. The motor restores the original agent
flags only after the relevant remaining owners have released their tokens.

`RootMotionCollisionTests` covers ordinary playback against thin walls, controller
bodies on the root and children, trigger model bodies, floor and corner sliding,
character exclusions, dense hit-zone colliders, and settings copied on rebind.

The flag is latched on every *active* policy publish rather than read once at
entry: the Brain publishes `Active` from `EnterExclusiveLocomotion` and the shape
flags a moment later, so the first publish of a playback always still carries the
previous shape.

### Handoff to ChainReady

`CompleteNow` leaves the state *before* the completion signal is raised. If the
signal were raised from inside the state, the transition policy would still see
`SpecialReaction` owning locomotion and refuse the ChainReady pose. Because the
whole handoff runs in one call stack — completion → `EndSpecialPointReactionHold`
→ `EnterChainReady` — no frame passes in which AI or NavMesh resumes between the
two states. `StaggerMeter` is the single owner of that suspension across both.

See `Docs/SYSTEMS/SPECIAL_SHOOT_POINTS.md` for the round and stagger contract.

## Defensive Block

Production timed approach keeps the original attacker's Skill request playing.
`AnimDriver.TryBeginSkillApproach(requestId, endNormalized, duration)` validates that
request, hands root motion to the gameplay motor, and fits the remaining moving
charge segment into the duration with positive playback speed. Ordinary cast-point
and timeline events remain active so costs settle normally; the attack adapter
suppresses the bound damage execution separately. The state restores its previous
playback speed on exit. No held pose or additional attacker animation state is used.
For a Continue Skill Block window, `TryEndSkillApproach(requestId)` restores the
saved playback speed and root-motion policy without ending the Skill or replaying
its timeline. The gameplay motor releases its navigation/control scope first.
An interrupted/expired request cannot restore root motion onto a different skill.
Movement/outcome/cancellation belong to `DefensiveBlockAttack` and its approach
motor; Brain only owns playback. Global pause and actor time still affect both.

`CharacterAnimationMode.Block` runs Begin, held-pose Loop, Impact and Exit under
one request. AnimDriver owns `TryBeginBlock`, `TryBlockImpact` and `EndBlock`;
Brain exposes `BlockPhase` and terminal `PlaybackKind.Block` events. Gameplay
contact decisions belong to the interruption controller, not the animation.
The optional charge preparation path in `DefensiveBlockAttack` acquires a zero-speed
`SkillPreCastHoldHandle` through AnimDriver at CastStarted, before the first clip
advance. It releases that handle after `SkillDefensiveBlockSettings.windupSeconds`
in the caster's time domain, or on reset/invalidated playback. The normalized-zero
command window stays available while payload and charge motion wait. The defender's
Block request is separate and continues raising its guard during this preparation.
Production Rector sets this duration to zero to preserve continuous skill playback;
the experimental 0.4 s held pose is no longer enabled.
After companion warp arrival (or immediately on accepted Player self guard), a frontal contact may transition Begin directly to Impact,
skipping Loop. `TryBlockImpact` accepts Begin or Loop for the matching request;
it rejects other phases and repeated impacts. Arrival and hit geometry remain
controller-owned gates; this does not grant general damage immunity.
Block excludes ordinary commands and yields to life/cinematic/control-loss
transitions. Its recoil motor owns displacement, with animation root motion off.
`BlockAnimationProfile.beginStartNormalized` selects the first Begin frame; playback
interpolates from there to `guardPoseNormalized`, then holds that pose for Loop.
The production Aires profile uses 0.55 to 0.65 of `Aires_Block`: the earlier portion
contains a jump, and holding the old 0.35 frame left the model's feet airborne even
while the actor root was correctly grounded. Keep both endpoints within a grounded
guard segment when replacing the clip; disabling root motion does not remove bone animation.
Player fallback shares this Block request and profile; it does not run the legacy
Player interruption skill or execute an Aires payload. The Player prefab's default
GuardSetting supplies the existing generic Block clips when its character has no
override. This is prototype clip reuse, not newly authored character-specific guard
animation. Receiver control/reservation and cleanup remain controller responsibilities.
See [Rector Defensive Block Test](../SYSTEMS/DEFENSIVE_BLOCK_TEST.md).

Production skill and character profiles opt in to the same flow used by the test
scene. The party transient-execution scope owns AI/movement suspension without
invincibility or collision exclusion. The controller emits Accepted/Arrived/Impacted/
Finished events; the production Cinemachine extension owns camera presentation.

## StateHub owner notifications

StateHub projections are read-only. WeaponSystem publishes RuntimeStateChanged
when an operation commits (including coroutine steps); StateHub forwards reload
start once from the owner transition. MeleeController, DashSystem and
CharacterKnockbackMotor publish activity changes. HealthSystem publishes its
explicit LifeStateChanged, which StateHub forwards alongside Died. Existing
AnimDriver/AnimBrain request and playback flows are retained.

Debug logging and on-screen rendering now live in StateHubDebugView. StateHub
retains legacy serialized debug fields for migration, without rendering them.
