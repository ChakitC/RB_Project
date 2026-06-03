# Weapon System

`WeaponSystem` is the public runtime facade for weapon equip, fire, reload,
ammo, projectile spawning, weapon visuals, and runtime weapon effects.

Keep `WeaponSystem` as the facade until callers are intentionally migrated.
Public mirror fields still exist for inspector/debug compatibility, but new
code should read weapon state through explicit facade properties and methods.

## Main Files

- `Assets\Scripts\Player\WeaponSystem.cs`
- `Assets\Scripts\Player\StatsHub.cs`
- `Assets\Scripts\Weapons\WeaponInstanceData.cs`
- `Assets\Scripts\Weapons\WeaponAffixRuntimeController.cs`
- `Assets\Scripts\Weapons\WeaponUpgradeRuntimeController.cs`
- `Assets\Scripts\Player\WeaponStatSnapshotBuilder.cs`
- `Assets\Scripts\Player\WeaponAmmoState.cs`
- `Assets\Scripts\Player\WeaponReloadController.cs`
- `Assets\Scripts\Player\WeaponFireController.cs`
- `Assets\Scripts\Player\WeaponRuntimeEffectDispatcher.cs`
- `Assets\Scripts\Player\PlayerInventory.cs`
- `Assets\Scripts\CharacterEquipment.cs`

## Public Facade Surface

External callers currently use `WeaponSystem` for:

- equip/state: `Equip`, `CurrentWeapon`, `CurrentWeaponInstance`,
  `NotifyWeaponInstanceChanged`
- fire/reload: `SetFiring`, `TryShoot`, `TryReload`, `CancelReload`,
  `GetReloadAnimDuration`, `IsReloading`, `IsFiringHeld`,
  `IsFiringActivity`
- ammo: `CurrentAmmo`, `MagazineSize`, `CurrentReserveAmmo`,
  `ReserveAmmoSize`, `IsMagazineEmpty`, `CanRestoreMagazine`,
  `RestoreMagazine`, `CanRestoreReserveAmmo`, `RestoreReserveAmmo`,
  `GrantFreeAmmo`
- aim/visual: `OnAim`, `IsAiming`, `FirePoint`, `BindFirePoint`,
  `RefreshFirePointReference`, public `ctx`
- runtime effects: `SpawnAffixProjectile`

Public mirror fields such as `damage`, `fireRate`, `magazine`,
`maxMagazine`, `reserveAmmo`, `reloadTime`, `critRate`, `critMultiplier`,
`stability`, `bulletSpeed`, and `staggerPower` must remain valid while the
compatibility window is still open. External callers should not add new direct
reads or writes to these fields.

Current migrated callers:

- `StateHub` reads `IsMagazineEmpty` and `IsFiringActivity`.
- `CharacterAnimatorController_2` reads `IsFiringActivity`.
- `PlayerMovementCC` reads `IsAiming` and `IsFiringActivity`.
- `CharacterVisualController` reads `FirePoint` and writes through
  `BindFirePoint`.

## Equip Flow

`CharacterEquipment` and `PlayerInventory` can both lead into weapon equip.
`CharacterEquipment.Equip` updates character equipment state, mirrors the
current weapon into `ctx.currentWeapon`, and calls `WeaponSystem.Equip`.

`WeaponSystem.Equip`:

1. syncs the previous weapon instance state
2. stops active firing/reload activity
3. assigns `currentWeapon` and `currentWeaponInstance`
4. marks derived stats dirty
5. mirrors weapon into `ctx.currentWeapon`
6. copies weapon config values into public mirror fields
7. resolves projectile prefab and fire behavior
8. notifies runtime effect handlers
9. marks `StatsHub` dirty
10. refreshes derived stats
11. initializes ammo state from the weapon instance
12. syncs ammo mirrors and instance state
13. updates ammo UI
14. refreshes weapon visuals
15. plays equip cue when the weapon changed

## Derived Stats

Weapon stats come from `WeaponStatSnapshotBuilder` and `StatsHub`.

`StatsHub` calculates base character stats, weapon stats, level scaling, status
modifiers, passive modifiers, affix modifiers, and upgrade modifiers through
`IStatModifierProvider`.

`StatsHub` keeps a modifier snapshot and input signature. When base stats, level,
current weapon, provider state, or modifier output changes during a cache
refresh or fallback probe, `StatsHub` refreshes its cached values and increments
`StatsHub.Revision`. Reading `StatsHub.Revision` is side-effect free.

`WeaponSystem` subscribes to `StatsHub.StatsDirty` as the normal path and also
observes `StatsHub.Revision`. It uses throttled fallback probes to catch silent
input changes without scanning providers every frame. Weapon derived stats
refresh only when needed:

- before shooting
- before reloading
- during equip
- when a weapon instance changes
- when a caller needs derived ammo/stat limits
- when a throttled fallback probe detects a silent input change

Do not force `WeaponSystem` to recalculate all derived stats or run unthrottled
signature probes every frame.

## Stat Modifier Contract

Any component that contributes runtime modifiers should implement
`IStatModifierProvider` and raise `StatModifiersChanged` when its modifier
output changes. `StatsHub` subscribes to provider events and uses signature
checks as a safety net when a provider forgets to notify. In editor or
development builds, silent signature changes can log a warning so the provider
event can be fixed.

This applies when changes affect weapon-relevant stats such as:

- `Damage`
- `CritRate`
- `CritMultiplier`
- `FireInterval`
- `ReloadTime`
- `Stability`
- `BulletSpeed`
- `MaxMagazine`

This applies to status effects, passive modifiers, weapon affixes, weapon
upgrades, and any custom dynamic modifier provider.

## Shoot Flow

`TryShoot()`:

1. refreshes derived stats if dirty
2. rejects when the actor cannot shoot, weapon is missing, reload is active, or
   the fire interval has not elapsed
3. handles empty magazine behavior and autoload reload
4. checks projectile prefab and fire point
5. sets next fire time
6. consumes ammo unless free ammo is active
7. syncs weapon instance state and ammo UI
8. creates weapon source id, attack id, and passive shot context
9. spawns the projectile
10. plays fire cue
11. reports shot state to `StateHub`
12. publishes `ShotFired` through `CombatEventBus`
13. notifies status effects and weapon runtime effect handlers
14. starts autoload reload when applicable

## Reload Flow

`TryReload()`:

1. refreshes derived stats if dirty
2. rejects when actor state blocks reload
3. stops firing state but preserves held-fire intent
4. rejects when already reloading or magazine is full
5. plays empty cue when reserve ammo is unavailable
6. stops any previous reload routine
7. starts per-bullet or full-magazine reload based on weapon config
8. plays reload cue

`CancelReload()` is responsible for stopping the reload state, animation/audio
side effects, burst routine, and weapon instance sync.

## Ammo And Weapon Instances

`WeaponInstanceData` stores persistent weapon state:

- `instanceId`
- `baseWeaponId`
- rarity
- affixes
- shot counter
- current magazine and reserve ammo
- reserve ammo initialization state
- upgrade state and milestone ids

`WeaponSystem` mirrors runtime ammo into the active instance. If a system changes
weapon instance data externally, call `NotifyWeaponInstanceChanged()`.

## Runtime Effects

Runtime weapon effects should plug into the existing runtime effect handler flow
instead of bypassing `WeaponSystem`.

Existing runtime effect providers include:

- `WeaponAffixRuntimeController`
- `WeaponUpgradeRuntimeController`

They can contribute stat modifiers and react to equip/shot events.

## Extension Rules

- Keep new weapon behavior behind `WeaponSystem` or the existing weapon helper
  classes.
- Keep ammo state free of UI, audio, animation, and coroutine side effects.
- Keep UI, audio, animation, and coroutine behavior owned by `WeaponSystem`.
- Do not add new external reads or writes of `WeaponSystem` public mirror
  fields. Use facade properties such as `CurrentAmmo`, `IsMagazineEmpty`,
  `IsAiming`, `IsFiringActivity`, `Damage`, and `FirePoint`.
- Use `BindFirePoint` instead of assigning `WeaponSystem.firePoint` directly.
- Preserve public mirror fields until prefab/inspector compatibility is
  intentionally retired.
- Raise `IStatModifierProvider.StatModifiersChanged` whenever a provider's stat
  output changes.
- Do not edit generated project files to make a new script compile.
