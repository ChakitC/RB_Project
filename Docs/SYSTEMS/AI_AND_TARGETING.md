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

