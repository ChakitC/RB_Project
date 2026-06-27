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

## Context Resolution

Character prefabs must expose both `CharacterAnimDriver` and
`CharacterAnimBrain` through `CharacteContext`. Common systems resolve commands
from `ctx.AnimDriver` and read/event access from `ctx.AnimBrain` or
`ctx.AnimDriver.Brain`.

The Driver emits one warning in Editor or Development builds when a facade
command is called without a resolved Brain.
