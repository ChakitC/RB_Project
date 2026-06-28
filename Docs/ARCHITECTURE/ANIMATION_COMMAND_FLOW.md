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

## Context Resolution

Character prefabs must expose both `CharacterAnimDriver` and
`CharacterAnimBrain` through `CharacteContext`. Common systems resolve commands
from `ctx.AnimDriver` and read/event access from `ctx.AnimBrain` or
`ctx.AnimDriver.Brain`.

The Driver emits one warning in Editor or Development builds when a facade
command is called without a resolved Brain.
