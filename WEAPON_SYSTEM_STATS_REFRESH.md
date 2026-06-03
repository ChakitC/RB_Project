# WeaponSystem Stats Refresh

## Summary

`WeaponSystem` does not recalculate derived weapon stats every frame. It keeps
public mirror fields fresh through dirty events, side-effect-free
`StatsHub.Revision` checks, and limited fallback signature probes.

Current flow:

1. `StatsHub` collects all `IStatModifierProvider` output into one modifier
   snapshot when the cache is refreshed.
2. `StatsHub` computes an input signature from base stats, level, current
   weapon, provider identity/active state, and all runtime modifiers.
3. If the signature changes during a cache refresh or fallback probe, `StatsHub`
   recalculates cached stats and increments `StatsHub.Revision`.
4. `StatsHub.Revision` is a cheap read with no cache refresh side effects.
5. Providers raise `IStatModifierProvider.StatModifiersChanged` as the normal
   path. `StatsHub` subscribes to those events and calls `MarkDirty()`.
6. `WeaponSystem` observes `StatsHub.StatsDirty` and `StatsHub.Revision`. If the
   derived stats are dirty or the revision changed, weapon derived stats are
   refreshed before use.

This means a dynamic provider that forgets to call `StatsHub.MarkDirty()` should
not create permanently stale stats. Weapon actions force a fallback probe before
important work, and idle `WeaponSystem.Update()` probes are throttled. In editor
or development builds, silent signature changes can log a warning so the missing
provider event can be fixed.

## Provider Contract

Any component implementing `IStatModifierProvider` must expose:

```csharp
public event Action StatModifiersChanged;

public void AppendStatModifiers(List<RuntimeStatModifier> buffer)
{
    // Append the provider's current stat modifiers.
}
```

When the provider knows its output changed, it should invoke
`StatModifiersChanged`. This keeps stat consumers responsive without waiting for
the signature probe path.

`AppendStatModifiers()` should be side-effect free. It may be called for cache
rebuilds and signature probes, so it should only append current data.

## Stats That Affect Weapons

Weapon-derived stats depend on these `StatType` values:

- `Damage`
- `CritRate`
- `CritMultiplier`
- `FireInterval`
- `ReloadTime`
- `Stability`
- `BulletSpeed`
- `MaxMagazine`

`StatsHub` also caches character-facing values such as `Armor`, `MoveSpeed`,
`MaxHP`, `MaxStamina`, and `MaxEnergy`.

## Existing Providers

These provider components currently raise `StatModifiersChanged`:

- `StatusEffectController`
- `PassiveController`
- `AccessoryLoadout`
- `WeaponAffixRuntimeController`
- `WeaponUpgradeRuntimeController`

`StatsHub.RebuildModifierProviders()` scans child components, subscribes to
provider events, and marks the cache dirty when the provider hierarchy changes.

## WeaponSystem Refresh Points

`WeaponSystem` refreshes derived stats only when needed:

- before shooting
- before reloading
- during equip
- when a weapon instance changes
- when ammo/stat limits are queried
- when `StatsHub.Revision` differs from the observed revision
- when the throttled fallback probe detects a silent input change

Do not restore unconditional `RefreshDerivedStats()` calls or unthrottled
signature probes every frame.

## Dynamic Provider Guidance

For new dynamic providers:

1. Implement `IStatModifierProvider`.
2. Keep `AppendStatModifiers()` deterministic for the current provider state.
3. Invoke `StatModifiersChanged` when internal state changes modifier output.
4. Avoid modifiers that depend directly on `Time.time` without a discrete state
   change. If a value truly must change continuously, use a local calculation at
   the consumer instead of forcing global stat cache churn.

## Related Files

- `Assets/Scripts/Player/StatsHub.cs`
- `Assets/Scripts/Player/WeaponSystem.cs`
- `Assets/Scripts/Passives/IStatModifierProvider.cs`
- `Assets/Scripts/StatusEffects/StatusEffectController.cs`
- `Assets/Scripts/Passives/PassiveController.cs`
- `Assets/Scripts/Accessories/AccessoryLoadout.cs`
- `Assets/Scripts/Weapons/WeaponAffixRuntimeController.cs`
- `Assets/Scripts/Weapons/WeaponUpgradeRuntimeController.cs`
- `PASSIVE_EVENT_SOURCE_ARCHITECTURE.md`
