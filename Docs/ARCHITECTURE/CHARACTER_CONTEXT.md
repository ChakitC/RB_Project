# Character Context Architecture

`CharacteContext` is the shared base reference hub for character actors. The
class name is intentionally spelled `CharacteContext`; keep using that name
until there is a dedicated rename task.

## Main Types

- `CharacteContext`: shared base for player, ally, and enemy characters.
- `PlayerContext`: player-only references such as input, inventory, helper
  manager, field ally manager, chain attack coordinator, party command, and UI.
- `AllyContext`: ally-only references such as `AITargetSensor`, `NavMeshAgent`,
  and `AgentMoveDriver`.
- `EnemyContext`: enemy-only references such as enemy `NavMeshAgent`, collider,
  dropper, animator, and forced infinite reserve ammo setting.
- `CharactorContext.cs`: legacy alias only. Do not add new logic there.

## Base Context Responsibilities

`CharacteContext` owns references that can be shared by multiple character
types, including:

- core state and stat systems: `StateHub`, `StatsHub`, `CombatEventBus`
- movement bodies: `Rigidbody`, `CharacterController`
- common modules: `TargetInfo`, `CharacterEquipment`, `AccessoryLoadout`,
  `WeaponSystem`, `CharacterAnimBrain`, `CharacterAnimDriver`,
  `CharacterPairOffsetApplier`, `MeleeController`
- visual and collider support: `CharacterContextPartyLoader`,
  `CharacterVisualController`, `UIManager`, `CharacterColliderRefs`
- gameplay modules: `LevelSystem`, `HealthSystem`, `StaminaSystem`,
  `DashSystem`, `CharacterKnockbackMotor`, `PassiveController`,
  `PlayerPassiveProgress`, `SkillUserSystem`, `Interactor`,
  `CharacterSkillManager`

Common systems should primarily depend on `CharacteContext` and read peer
modules through `ctx`.

## Identity And Time

Use context properties instead of subtype checks for common behavior:

- `ctx.TargetIdentity` identifies player, companion, enemy, neutral, or generic
  AI target identity.
- `ctx.UsesWorldSlow` controls whether systems should use world-slow time.
- `ctx.ForceInfiniteReserveAmmo` allows subtype-specific ammo policy without
  hard-coding enemy checks in common weapon logic.

Do not use `ctx is PlayerContext` in common code just to branch identity or
world-slow behavior.

## Reference Resolution Contract

`ResolveReferences()` is the main binding point.

Rules:

- Shared references belong in `CharacteContext.ResolveReferences()`.
- Subtype-only references belong in that subtype override.
- Every subtype override must call `base.ResolveReferences()` first.
- Modules should use `ctx` as the primary entry point.
- Prefer `ctx.StatsHub`, `ctx.HealthSystem`, `ctx.WeaponSystem`,
  `ctx.CombatEventBus`, and similar context-owned references over duplicated
  serialized peer references.

`ResolveActorComponent()` resolves in this order:

1. keep the current reference if assigned
2. component on the same object
3. child component when allowed
4. parent component

## Adding A Shared Module

When adding a component/reference used by multiple character types:

1. Add a field to `CharacteContext`.
2. Bind it in `CharacteContext.ResolveReferences()`.
3. Update prefab context blocks when useful.
4. Read the component through `ctx` in runtime systems.
5. Avoid adding duplicate serialized references to every module unless the field
   is local authoring data.

## Adding A Subtype Module

When adding a player-, ally-, or enemy-only component:

1. Add it to `PlayerContext`, `AllyContext`, or `EnemyContext`.
2. Bind it in that subtype's `ResolveReferences()` override.
3. Call `base.ResolveReferences()` first.
4. Keep common systems typed against `CharacteContext` unless they truly need
   subtype-specific fields.

## Good Patterns

Use this style in common systems:

```csharp
ctx?.ResolveReferences();
var statsHub = ctx != null ? ctx.StatsHub : null;
var combatEventBus = ctx != null ? ctx.CombatEventBus : null;
```

Use subtype references only when required:

```csharp
if (ctx is AllyContext allyContext)
{
    AITargetSensor sensor = allyContext.AITargetSensor;
}
```

## Avoid

- Declaring another `public class PlayerContext`.
- Adding new logic to `CharactorContext.cs`.
- Branching common identity behavior with `ctx is PlayerContext`.
- Removing serialized prefab-facing fields without confirming runtime resolution
  and prefab usage.
- Moving newly created Unity classes into unrelated files to work around stale
  `.csproj` generation.
