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

It also supports untargetable tokens. Systems can acquire and release tokens
without fighting over one boolean field.

## Target Sensor

`AITargetSensor` scans for target colliders, filters them, scores candidates,
and maintains current/visible/last-seen target state.

Important sensor concepts:

- scan interval
- target layers and obstacle layers
- field of view
- line of sight
- team filtering
- alive filtering
- grace period and last seen target
- current target stickiness
- retarget interval and lock duration
- threat memory
- taunt target

`AITargetingProfileDef` can override scoring and policy values. Local sensor
fields are fallback settings when no profile is assigned.

## Ally Context And Movement

`AllyContext` adds:

- `AITargetSensor`
- `NavMeshAgent`
- `AgentMoveDriver`

`AgentMoveDriver` drives stable move animation state from `NavMeshAgent`
movement and writes normalized movement into `StateHub` and `CharacterAnimBrain`.

It also supports companion separation from the player by resolving the player
through `CharacteContext.TargetIdentity == Player`.

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

## Guaranteed Interruption Command

The player can press the Interruption Command input (default G) to order a
ready ally to interrupt an enemy that is currently inside a blockable Pre-Cast
window. Once the command is accepted, the block is guaranteed regardless of
collider or damage outcome.

### Flow

1. `InterruptionCommandController` (on the player) searches near
   `PlayerContext.aimTarget` for an enemy with an active blockable pre-cast
   window (`PreCastBlockController.CanBlockActiveCast()`).
2. Selects the nearest qualifying ally via `AllyInterruptionController.IsReadyForInterruption()`.
   Ally must be alive, not busy, not reserved, have a configured interruption
   skill, and a resolvable safe teleport pose.
3. Reserves the ally (`FieldAllyMember.TryReserve`) and the block
   (`PreCastBlockController.TryReserveBlock`) which acquires a Pre-Cast Hold
   on the enemy animation, freezing the playhead before the cast point.
4. Ally suspends its BehaviorTree and NavMeshAgent, warps to the safe pose,
   locks aim on the target, and starts its interruption skill via
   `CharacterSkillManager.TryStartExternalSkill`. These actor modules are read
   through `AllyContext`; `FieldAllyMember` is used only for shared ally
   reservation and chain coordination.
5. At the ally skill's `HitStart` timeline event, the reserved block is
   completed (`CompleteReservedBlock` → `TryCancelActiveCast(Blocked)`), and
   a force-replace knockback is applied to the target.
6. After the ally skill animation finishes, autonomy is restored and the
   reservation is released.

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
  scan counts, and the final command result.
- `PreCastBlockController.logPreCastFlow` logs the enemy cast, pre-cast window,
  hold reservation, and block lifecycle under `[PreCast.Target]`.
- `AllyInterruptionController.logInterruptionFlow` logs warp, external skill,
  `HitStart`, block completion, fallback, and cleanup under `[PreCast.Ally]`.

Use `requestId` and `reservationId` to correlate target and ally messages.
Normal logs are disabled by default to avoid Console noise. The safety-hold
invariant remains an unconditional error because a cast must never release
while its pre-cast hold reservation is active.
