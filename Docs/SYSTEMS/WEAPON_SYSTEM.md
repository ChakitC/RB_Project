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
  `CurrentFiringMode`, `IsFiringActivity`
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
- `CharacterAnimDriver` reads `CurrentFiringMode` to play shot pulse animation
  only for semi-auto shots.
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

`MaxReserveAmmo` is resolved after `MaxMagazine`, because weapons without an
explicit reserve limit derive their capacity from magazine size. The final
reserve capacity is included in `WeaponStatSnapshot`. Increasing the limit does
not add ammo; decreasing it clamps the current reserve count. Infinite-reserve
weapons bypass reserve-capacity modifiers.

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

### Test Stage Weapon Baseline

The five complete player/drop weapons use this baseline. Critical identity
comes from the character, so every weapon has 0 added critical chance and a
neutral `1x` critical multiplier.

| Weapon | Damage | Interval | Magazine / Reserve | Reload | Stability | Stagger | Bullet Speed |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Roma SMG `SMG_RB01` | 12 | 0.10s | 30 / 240 | 1.6s | 45 | 8 | 50 |
| Aires rifle `SMG_RB02` | 14 | 0.10s | 36 / 288 | 1.4s | 65 | 10 | 55 |
| Feno HMG `HMG.RB.01` | 19 | 0.15s | 100 / 200 | 3.0s | 35 | 18 | 45 |
| Milano sniper `SR_RB_01` | 180 | 0.70s | 5 / 40 | 0.35 + 0.45/round + 0.25s | 80 | 35 | 100 |
| General rifle `SMG_GR01` | 18 | 0.12s | 30 / 240 | 1.8s | 55 | 10 | 50 |

All five have base buy/sell prices of 300/90. Direct weapon headshots use the
shared 1.5x head hit-zone multiplier. `Enemy_SMG_GR01` is not in item, shop, or
drop databases and must remain separate from the player rifle.

### Test Stage Upgrade Curve

Rarity caps remain Common +10, Rare +20, and Epic +30. Each upgrade level adds
0.2% Damage, reduces Fire Interval by 0.1%, reduces all reload durations by
0.2%, and adds 0.5 Stability. Levels +10, +20, and +30 each add 5% magazine
capacity. Reload modifiers apply to full-magazine reload and to sniper start,
per-round insert, and end delays.

Milestones are:

- +10: 3% chance for one ammo-free extra projectile.
- +20: every 20th shot fires one ammo-free extra projectile.
- +30: completing a reload reduces Fire Interval by 8% for 2 seconds.

The gold cost to reach target level `L` is `20 + 5 * (L - 1)`. Scrap cost is 1
for +1 through +10, 2 for +11 through +20, and 3 for +21 through +30. Cumulative
costs are +5 150/5, +10 425/10, +15 825/20, +20 1,350/30, +25 2,000/45, and
+30 2,775/60 (gold/scrap).

### Test Stage Weapon Affix Pool

Weapon affixes use stable, namespaced ids. The test-stage pool intentionally
supports firearm weapon types only: Sniper, Shotgun, Pistol, Rifle, Smg, and
Hmg. Melee and Spirit require their own affix pools.

Sub-affixes are permanent stat modifiers while the weapon is equipped:

| Id | Display Name | Modifier | Roll | Weight | Weapon Types |
| --- | --- | --- | ---: | ---: | --- |
| `weapon.sub.damage.v1` | Sharpened Rounds | Damage, AddPercent | +2% to +4% | 1.0 | All firearms |
| `weapon.sub.crit_rate.v1` | Calibrated Sights | CritRate, Flat | +1 to +3 | 1.0 | All firearms |
| `weapon.sub.fire_interval.v1` | Fleet Trigger | FireInterval, AddPercent | -6% to -3% | 1.0 | All firearms |
| `weapon.sub.reload_time.v1` | Rapid Loader | ReloadTime, AddPercent | -9% to -5% | 1.0 | All firearms |
| `weapon.sub.bullet_speed.v1` | Velocity Bore | BulletSpeed, AddPercent | +10% to +18% | 0.85 | All firearms |
| `weapon.sub.max_magazine.v1` | Extended Magazine | MaxMagazine, AddPercent | +8% to +12% | 0.85 | Rifle, Smg, Hmg |
| `weapon.sub.crit_multiplier.v1` | Precision Bore | CritMultiplier, Flat | +0.08 to +0.15 | 1.0 | All firearms |
| `weapon.sub.stability.v1` | Balanced Frame | Stability, Flat | +6 to +10 | 0.85 | All firearms |
| `weapon.sub.max_reserve_ammo.v1` | Ammo Harness | MaxReserveAmmo, AddPercent | +15% to +25% | 0.65 | All firearms |

Main-affixes add conditional weapon behavior:

| Id | Display Name | Behavior | Weight | Weapon Types |
| --- | --- | --- | ---: | --- |
| `weapon.main.reload_stability.v1` | Steady Reload | Reload grants +10 to +20 Stability for 3 seconds | 1.0 | Pistol, Rifle, Smg, Hmg |
| `weapon.main.reload_damage.v1` | Combat Loading | Reload grants +8% to +12% Damage for 3 seconds | 1.0 | All firearms |
| `weapon.main.reload_crit_rate.v1` | Deadeye Chamber | Reload grants +6 to +10 CritRate for 3 seconds | 1.0 | Sniper, Shotgun, Pistol, Rifle |
| `weapon.main.echo_chamber.v1` | Echo Chamber | Every 10th shot fires an ammo-free projectile at 70% damage | 1.0 | Rifle, Smg, Hmg |
| `weapon.main.breach_chamber.v1` | Breach Chamber | Every 4th shot fires an ammo-free projectile at 45% damage and 90% speed with 1.75x stagger and a 0.75-unit, 0.12-second MiniStun knockback | 1.0 | Sniper, Shotgun, Pistol |

Echo Chamber and Breach Chamber use a deterministic `procChance` of 1. Echo
falls back to the equipped weapon's projectile config and prefab. Breach uses
`WeaponAffix.BreachProjectile`, falls back to the equipped weapon's projectile
prefab, and does not add explosion modules or separate VFX.

Different affix ids may intentionally modify the same stat. A reload main-affix
can therefore combine with a permanent sub-affix for the same stat. Exact affix
ids remain unique on a weapon instance.

Known asset-only limitations:

- Timed reload buffs can remain active for their remaining duration after the
  actor swaps weapons.
- The affix shot counter persists on the weapon instance across reloads and
  weapon swaps.
- MaxMagazine and MaxReserveAmmo affixes increase capacity without granting
  the additional ammo immediately.
- The namespaced affix ids replace the earlier test ids. Existing test saves
  using `Damge.1`, `Crit.1`, or `Stabiity.1` must be reset; there is no runtime
  migration for those ids.

### Stability Percentage

`Stability` is authored and exposed as a percentage from `0` to `100`:

- `0%` keeps the weapon's full authored sway speed and sway angle.
- `50%` halves both sway speed and sway angle.
- `100%` removes weapon sway completely.

The runtime sway multiplier is `1 - (Stability / 100)`. Stability also scales
return speed from `1x` at `0%` to `2x` at `100%`. This stat controls weapon
sway only; recoil, projectile spread, and other accuracy behavior remain
separate systems.

Final Stability is clamped to `0-100%` after all modifiers. A `Flat` Stability
modifier adds percentage points, so `30 Stability + 10 Flat = 40%`. Author
these values as whole percentages, not `0-1` normalized values.

### Damage Range Falloff

Projectile damage is finalized through `DamageCalculator`, which applies range
falloff before critical and armor modifiers. Current falloff profiles are:

| Weapon Type | Free Range | Damage Drop After Free Range |
| --- | ---: | ---: |
| Sniper | 120 units | none |
| Shotgun | 12 units | 2% base damage per unit |
| Pistol | 25 units | 0.5% base damage per unit |
| Rifle | 40 units | 0.3% base damage per unit |
| SMG | 3 units | 8% base damage per unit |
| HMG | 30 units | 0.4% base damage per unit |
| Melee | infinite | none |

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

## Projectile Pooling

Projectiles are managed through `ProjectilePool` (singleton, `DontDestroyOnLoad`), which reuses instances instead of calling `Instantiate`/`Destroy` on every shot.

**Spawn paths** — all three routes go through the pool:

- Normal fire: `WeaponProjectileSpawner.Spawn` → `ProjectilePool.Instance.Get(prefab, pos, rot)`
- Skill projectiles: `ProjectileSkillPayloadDef.Execute` → `ProjectilePool.Instance.Get(prefab, pos, rot)`
- Child/split projectiles: `Projectile.SpawnChild` → `ProjectilePool.Instance.Get(prefab, pos, rot)`

**Despawn** — `Projectile.Despawn` routes to `ProjectilePool.Return`, which reparents to the pool transform and calls `SetActive(false)`. If the projectile has no pool reference (spawned before pool existed), it falls back to `Destroy`.

**Cap**: 128 idle instances per prefab by default. Configured via the `ProjectilePool` component's `maxPerPool` serialized field. Instances above the cap are destroyed instead of pooled.

**Layer reset on reuse** — `PrepareForSpawn` calls `ProjectileLayerUtility.InheritLayer` to reset the hierarchy to the prefab's authored layer before the caller applies `ApplyForContext` or `ApplyForSkillUser`. This prevents stale layers when an instance is reused for a different bullet faction.

**Ball/trail VFX** — routed through `VfxSpawner.SpawnLoopingVfx` / `StopLoopingVfx`, sharing the existing `VfxPool`. VFX is detached from the projectile before it is returned to the pool, so trails do not teleport or linger on reused instances.

**Spawn flash VFX** — assign `spawnFlashPrefab` on `Projectile` to play a one-shot muzzle flash at the spawn position. `PrepareForSpawn` calls `VfxSpawner.SpawnVfx(spawnFlashPrefab, pos, rot)` immediately after each pool reuse, covering all spawn paths (normal fire, skill, child/split). Leave the field null if the projectile has no flash effect.

**Hovl Studio `HS_ProjectileMover` compatibility** — prefabs that carry `HS_ProjectileMover` on a child VFX object must have their `Flash` Inspector field set to a Project asset prefab (not a scene-hierarchy child instance). `HS_ProjectileMover` now routes flash through `VfxSpawner.SpawnVfx` on every `OnEnable`, and `OnDisable` stops all coroutines so the lifetime timer does not outlive pool tenure. The pool-aware `notDestroy` path is selected automatically when `ProjectilePool.Instance` is present.

**Debug VFX kill-switch** — untick `Spawn Vfx` on the `VfxSpawner` Inspector to suppress all VFX and damage numbers without affecting gameplay (projectiles, hitboxes, and damage calculations still run normally). Useful for isolating perf/pooling issues caused by VFX prefabs.

**Prewarm** — call `ProjectilePool.Instance.Prewarm(prefab, count)` to fill the pool before a scene starts. Not wired to any automatic trigger (phase 2).

## Projectile Barriers

A `BarrierRuntime` (see [Barrier Payload](SKILL_SYSTEM.md#barrier-payload)) can
swallow hostile projectiles before they reach whatever is behind it.

**Physics setup** — barriers live on the `Barrier` layer (index 20). The
collision matrix allows `Barrier` to collide **only** with `PlayerBullet` and
`EnemyBullet`. A barrier prefab that is not on this layer will never be hit; the
Barrier payload's designer descriptor reports this as an authoring error.

**Blocking rules** — `ProjectileBarrierGate` is the single decision point. All
three projectile paths call it, and it runs **before** wall handling, area
damage, damage application, and module callbacks:

- `Projectile` (modern pooled path)
- `Bullet` (legacy weapon path)
- `SkillProjectile` (legacy skill path)

A shot is blocked only when all of these hold:

1. The projectile implements `IBarrierBlockableProjectile`.
2. Its source actor is hostile to the barrier's owner. Friendly fire passes
   through; an unknown or unresolvable faction also passes through, with a
   development-build warning.
3. It is travelling **inward from outside**. A projectile whose spawn position
   was inside the barrier — including the protected turret's own fire — always
   leaves freely, as does anything already moving outward.

**Damage charged to the barrier** is the shot's damage after range falloff,
crit, and damage multipliers, but **before** the target's armor and hit-zone
scaling.

**A blocked shot is consumed silently.** It despawns without running `OnHit`,
status application, explosions, split/chain spawns, or gameplay hit VFX. The
shot that breaks a barrier is absorbed in full — there is no damage overflow
onto whatever was behind it.

**Fast projectiles** — `Projectile` sweeps for barriers *before* its wall sweep,
so a projectile fast enough to tunnel past the trigger in one physics step still
gets caught by the barrier rather than only by the geometry behind it.

Hitscan and beam weapons are out of scope; they do not currently consult the
gate.

## Upgrade-Gated Status On Hit

`UpgradeGatedStatusOnHitModule` is a `ProjectileModule` that applies a
`StatusApplicationSpec` on hit, but only when the **firing summon** carries a
given `requiredUpgradeId` (via `SummonedEntityRuntime.HasUpgrade`).

It is deliberately narrow:

- It runs on the direct-damage hook alone. Area bursts are excluded via
  `Projectile.AreaExploded`, chained/split descendants via `ctx.depth > 0`, and
  damage-over-time never reaches a projectile hook at all. The debuff therefore
  tracks aimed shots that landed, not blast radius.
- The status is credited to the summon's **owner**, not the summon, so
  attribution and owner-side scaling stay with the character who deployed it.
- A blank `requiredUpgradeId` applies unconditionally. A projectile with no
  `SummonedEntityRuntime` on its source actor never applies it.

`Feno_MinigunTerret_ArmorShred` (`Assets/Data/StatusEffects/`) is the Part B
status: Armor `-15%`, 3s, max 1 stack, `RefreshDuration`,
`separatePerSource = false`.

> The module is **not yet assigned to any projectile**. The Minigun Terret has no
> Behavior Tree or firing behavior in this milestone, so Part B ships as data and
> a runtime contract with no in-game shooting effect until the turret can fire.

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

`CharacterAnimDriver` listens to `StateHub.ShotFired`, but forwards shot pulse
animation to `CharacterAnimBrain` only when `WeaponSystem.CurrentFiringMode` is
`Semi`. Auto and burst weapons should use held-fire animation behavior instead
of restarting `ShootPulse` every projectile.

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

Reload movement policy comes from the actor's `CharacterAnimProfileSO`.
`UpperBody` reload allows movement and remains active while Dash plays on the
locomotion layer. `FullBody` reload blocks normal movement; Dash is still
allowed, but `CancelReload()` runs only after the Dash attempt succeeds. Failed
Dash attempts leave the reload routine, animation, and Timeline VFX session
active.

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
# Affix runtime pipeline

Shots capture ammo-before/after, consumption, last-round state, and weapon
instance id before spawn. Equipped behavior assets own plain C# runtimes with
stat, pre-shot, pre-damage, combat-event, and timer hooks. Persistent counters
live in versioned records on `WeaponInstanceData`.
