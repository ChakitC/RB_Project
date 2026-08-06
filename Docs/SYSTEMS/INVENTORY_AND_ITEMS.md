# Inventory And Items

Inventory and equipment are split between player inventory state, character
equipment state, weapon instances, accessory loadouts, pickups, drops, and save
data.

## Main Files

- `Assets\Scripts\Player\PlayerInventory.cs`
- `Assets\Scripts\Inventory`
- `Assets\Resources\GameSettings\InventorySettings.asset`
- `Assets\Scripts\CharacterEquipment.cs`
- `Assets\Scripts\EquipmentAssignmentService.cs`
- `Assets\Scripts\Accessories`
- `Assets\Scripts\Weapons\WeaponInstanceData.cs`
- `Assets\Scripts\Weapons\WeaponInstanceFactory.cs`
- `Assets\Scripts\Pickup`
- `Assets\Scripts\ItemDrop`
- `Assets\Scripts\Shop`
- `Assets\Scripts\System\SaveSystem`

## PlayerInventory

`PlayerInventory` owns:

- inventory slots
- item add/remove behavior
- gold and scrap
- equipped player weapon instance id
- item database references
- weapon database reference
- save/load participation

New inventories start with `100` gold. Prefab and scene-authored
`PlayerInventory` components must keep their serialized `gold` value aligned
with this runtime default so a new or reset save begins with the same amount.

It resolves:

- `CharacteContext`
- `CharacterEquipment`
- `AccessoryLoadout`
- `WeaponSystem`

When inventory adds a non-stackable weapon or accessory, it creates an instance
data object instead of storing only the base definition.

Inventory capacity is owned by the central `InventorySettings` asset under
`Resources/GameSettings`. Player and scene prefabs must not serialize their own
slot count. `PlayerInventory.ConfiguredCapacity` exposes the configured value,
while `PlayerInventory.Capacity` can temporarily be larger when an older save
contains more occupied slots. Overflow items are preserved and compacted back
into the configured capacity as space becomes available.

## Inventory Window UI

`InventoryUI` presents inventory capacity as a nine-column responsive grid. The
cell size is calculated from the available viewport so four complete rows remain
visible without crossing the window boundary. Additional rows stay inside a
vertically clipped `ScrollRect`; its scrollbar hides when every row fits.
The scroll view, viewport, content grid, and scrollbar are authored in
`Assets/UI/Canvas InventoryWindow.prefab`; runtime layout reads their persisted
padding and scrollbar dimensions instead of relying on runtime-only geometry.

Mouse-wheel scrolling, scrollbar dragging, slot drag/drop, clicks, and delayed
tooltips remain supported. While an item is being dragged, holding the pointer
near the viewport's upper or lower edge scrolls the grid with unscaled time so
inventory navigation is unaffected by gameplay world-slow effects.

`InventorySlotUI` keeps empty cells subdued, uses a gold hover treatment, and
shows blue or purple borders for Rare or Epic weapon instances. Item icons must
preserve their aspect ratio. Equipment-owner portrait markers remain available
to equipment and upgrade screens that provide those bindings.

## CharacterEquipment

`CharacterEquipment` is the runtime equipment owner for a character. It tracks:

- current weapon definition
- current weapon instance
- equipped weapon instance id
- owner id
- whether it participates in player inventory save data

It calls `WeaponSystem.Equip` when weapon equipment changes.

Owner ids use:

```text
character:<characterId>
```

Player and companion contexts can participate in shared player inventory save
data. This is controlled by `usePlayerInventorySave` and resolved through
`ctx.TargetIdentity`.

## EquipmentAssignmentService

`EquipmentAssignmentService` centralizes UI/service operations for assigning
equipment:

- detect whether an inventory slot contains a weapon or accessory
- extract instance ids
- equip by owner
- unequip by owner and equipped slot
- find current equipped slot data
- find which owner has an instance assigned

Use this service instead of duplicating assignment logic in UI scripts.

`UIEquipment` treats a right-click on a non-empty equipped slot as an unequip
request. The item remains in inventory. Weapon unequip clears the active
`WeaponSystem` state, while accessory unequip refreshes the loadout so stat and
passive providers stop contributing immediately.

## Weapon Instances

`WeaponInstanceData` stores per-instance weapon state:

- instance id
- base weapon id
- rarity
- main and sub affixes
- shot counter
- current magazine and reserve ammo
- reserve ammo initialization flag
- upgrade level, tier, exp
- unlocked upgrade milestone ids

Weapon instance state must stay synchronized with `WeaponSystem` when ammo,
upgrade, affix, or equip state changes.

## Accessories

Accessory systems live under `Assets\Scripts\Accessories`.

Important concepts:

- `AccessoryDefinition`: base item data
- `AccessoryInstanceData`: per-instance data
- `AccessoryLoadout`: equipped accessory slots on a character
- `AccessoryDismantleService`: validates dismantling and grants scrap
- `AccessoryReforgeSettings`: the Global Modifier Pool and reforge cost formula
- `AccessoryReforgeService`: validates and performs a Reforge
- `AccessoryDisplayNameResolver`: shared "{Modifier} {Accessory}" naming and modifier-effect-summary formatting, used by the tooltip and the Upgrade screen
- accessory modifier definitions and providers

Accessory loadouts should resolve through `CharacteContext.AccessoryLoadout`
when possible.

### Reforge

Accessories have no level system. Instead, the Upgrade screen offers **Reforge**:
spending Gold to re-roll the accessory instance's single `modifierId`. The
modifier pool is one shared **Global Modifier Pool**
(`Assets/Resources/GameSettings/AccessoryReforgeSettings.asset`), not a
per-accessory pool — the old per-`AccessoryDefinition` `modifierPool` field and
`rollModifierOnCreate` flag survive only to resolve legacy modifier ids;
`AccessoryDefinition.GetModifierById` checks the legacy pool first, then falls
back to the Global Pool.

- **Cost**: `ceilTo5(baseBuyPrice / 3)` Gold, computed by
  `AccessoryReforgeSettings.CalculateReforgeCost`.
- **Roll**: never empty, never repeats the accessory's current modifier. New
  accessory instances (drop/shop/`AddItem`) always roll a modifier on creation
  via `AccessoryInstanceFactory.CreateInstance`.
- **Runtime refresh**: a successful Reforge mutates the live inventory
  instance, then `AccessoryLoadout.SyncSceneLoadoutsWithInventoryInstance`
  re-clones the new modifier into any equipped copy in the current scene
  (equipped slots hold `DeepClone()`s, so the inventory mutation alone would
  not otherwise reach them) and refreshes stats/passives.
- **Off-scene owners**: `AccessoryLoadout.SyncPersistedInstanceModifier` patches
  the accessory's `modifierId` directly in `accessories.json` for any owner not
  currently spawned in the scene, before the subsequent
  `SaveInventoryAndAccessories()` call — otherwise that save would re-seed from
  disk and keep the stale modifier for the off-scene owner.
- No confirmation dialog, no audio/VFX, and Reforge Gold is never refunded.

The weapon upgrade UI lists both weapon and accessory instances. Weapons use
the Upgrade button as before. Accessory selections show cost/effect preview and
use the same button for **Reforge** instead; they can also be dismantled when
not assigned to any player or companion loadout. The dismantle reward is `10`
scrap, plus `5` when the instance has a modifier, plus `3` per accessory upgrade
level (`upgradeLevel` is a legacy field, never written for accessories anymore).
These values and the save/assignment rules are serialized on
`AccessoryDismantleService`. A successful dismantle removes the inventory
instance first, grants scrap, and then saves the game.

Accessory stat modifiers can affect stamina capacity and stamina regeneration.
`MaxStamina` is calculated by `StatsHub` and applied by `StaminaSystem` when it
refreshes maximum stamina. `StaminaRegenRate` is also calculated through
`StatsHub`; `StaminaSystem` applies it to the serialized regeneration rate each
frame after the regeneration delay has elapsed.

Accessory `Stability` modifiers use percentage units. A `Flat` value adds
percentage points to the weapon's Stability and the final result is clamped to
`0-100%`. For example, `+10 Flat Stability` changes `30%` to `40%`.

Accessory `MaxReserveAmmo` modifiers are applied by `StatsHub` after the
weapon's base reserve capacity is resolved. Percentage modifiers use whole
percentage values, so `25` means `+25%`. Increasing capacity does not grant free
ammo. When capacity decreases, current reserve ammo is clamped to the new
maximum. Infinite-reserve weapons ignore this stat.

### Starter Mafia Accessories

The early-game mafia accessory set is authored under
`Assets/Data/Items/Accessories_SO/StarterMafia`. Its triggered passives are
under `Assets/Data/Combat/Passives/StarterMafia`.

The set contains Gang Signet Ring, Old Leather Holster, Slow Gold Watch,
Collector's Shades, Pointed Leather Shoes, Under-Suit Vest, Taped Magazine,
Trunk Ammo Box, Casino Lucky Coin, Cleaner's Lighter, Emergency Pager, and
Dashboard Rosary. All twelve items are registered in `ItemDatabase` and the
common accessory drop table.

## Pickups And Drops

Pickup-related scripts live under `Assets\Scripts\Pickup`, and drop logic lives
under `Assets\Scripts\ItemDrop`.

Typical flow:

1. drop manager or enemy dropper spawns pickup prefab
2. pickup visual/motion handles presentation
3. pickup effect definition applies behavior
4. collector context determines whether the collector is eligible
5. inventory, weapon system, health, stamina, or other target system receives
   the effect

During a map run, `ItemDropManager` parents normal `ItemPickup` instances under
the active room's persistent runtime root. An uncollected item is disabled when
the party leaves and becomes available again when that same map node is
revisited. Collected items are destroyed normally and never return. This
persistence is limited to the current in-memory run; starting a new run clears
all cached rooms and their remaining drops.

`SkillPickup` instances are temporary combat objects rather than persistent
room loot and are cleared during room travel.

Pickup collector logic should use `CharacteContext.TargetIdentity` for player
and companion checks.

## Save System

Save data is under `Assets\Scripts\System\SaveSystem`.

Inventory-related data is stored in separate files per save slot:

- `slot_{slot}_inventory.json`: currency, inventory slots, and weapon/accessory
  instance data
- `slot_{slot}_equipment.json`: owner-specific equipped weapon instance ids
- `slot_{slot}_accessories.json`: owner-specific accessory loadouts
- `slot_{slot}_party.json`: current party ids
- `slot_{slot}_character.json`: character level, unlock, skill point, and passive
  progression data
- `slot_{slot}_stage_progress.json`: per-stage run progress

The player-facing save selector exposes three slots (`Save 1` through `Save 3`,
stored internally as slots `0` through `2`). `SaveManager` remembers the latest
selected slot in `PlayerPrefs`. Switching slots does not save the current runtime
state; after confirmation it changes the active slot and reloads `Basement` so
participants cannot retain data from the previous slot.

Resetting a slot deletes every split save file, the legacy aggregate file, and
any temporary copies for the current slot only. In the Editor it also deletes
the corresponding legacy files under `Application.persistentDataPath`, preventing
them from being migrated back after reset. Reset then reloads `Basement` without
performing a save.

`slot_{slot}_game.json` is a legacy aggregate format. When a split file is
missing, `SaveSystem` reads the corresponding section from the legacy file and
writes the new file without deleting the legacy source. This also allows an
interrupted migration to resume one section at a time. New saves do not update
the legacy aggregate file.

Use the system-specific save APIs for frequent changes. Item-shop purchases,
weapon upgrades, and dismantling write inventory only. A Basement character-shop
purchase writes the character unlock to the active slot and then writes the
updated inventory gold. Weapon equip writes inventory plus equipment, while
accessory equip writes inventory plus accessory loadouts. Full saves remain
appropriate for scene transitions and explicit full-save actions.

Inventory and equipment save flows must preserve:

- inventory slot contents
- weapon and accessory instance data
- equipped instance ids
- owner-specific equipment assignments
- explicit empty weapon and accessory slots after unequip
- currency
- migration support for older save shapes

The legacy `PlayerInventoryData.maxSlotCount` field remains readable for JSON
compatibility but is no longer authoritative. New capacity changes come from
`InventorySettings`, and loading a larger legacy inventory must never discard
items beyond the configured slot count.

UI-driven owner assignments can be saved without a live scene
`CharacterEquipment` or `AccessoryLoadout` for that owner, such as Basement
party slots. Direct assignment saves must refresh `SaveManager`'s loaded cache
after writing the file so the next scene load does not reapply stale equipment
data.

Unequip operations must persist an explicit empty assignment for the owner.
This distinguishes a player-selected empty slot from legacy save data that has
no assignment entry and may still use startup default equipment.

Scene saves should not clear an existing weapon assignment just because a
runtime `CharacterEquipment` cannot currently resolve a valid replacement
instance. Preserve the persisted owner entry unless the save path is resolving
a deliberate duplicate assignment.

When loading saved data that already contains `equipment.entries`, those entries
are the authoritative weapon assignment source for each owner. `PlayerInventory`
only resolves the saved instance id into `WeaponInstanceData`; it should not
silently choose the first inventory weapon or let runtime default equipment
replace a missing, blank, duplicate, or unresolved saved assignment. Runtime
startup fallback should only create or select a default weapon when there is no
current slot save data to load.

`CharacterEquipment` owns the active weapon instance assignment for a character
at runtime. `WeaponSystem` may re-run equip during Unity startup, but when it is
given a weapon without an instance it should mirror the matching instance from
`CharacterEquipment` instead of clearing the runtime instance id.

Full game saves should seed from the latest persisted save data before scene
participants write their current state. This keeps equipment and inventory data
owned by unavailable or not-yet-resolved scene participants from being replaced
with empty defaults during scene transitions.

When adding persisted fields, add migration support if old saves may be loaded.

## Test Stage Accessories And Unique Equip

Accessories are duplicate-equipable by default. `AccessoryDefinition.uniqueEquip`
restricts only that exact `itemId` to one assignment in each character's
accessory loadout. The unique items are Feno's Field Pack, RabbitLag, Casino Lucky Coin,
Cleaner's Lighter, Dashboard Rosary, and Emergency Pager. When an old save has
duplicate assignments for one of these items, load keeps the first assignment,
skips later assignments, and leaves every underlying inventory instance intact.

Current prices use a 30% sell value: simple stat pieces cost 150/45,
tradeoff/high-stat pieces cost 200/60, and unique pieces cost 250/75.

| Accessory | Balance effect |
| --- | --- |
| Carrotleaf Heavy Weave Armor | Max HP +5%, Armor +2, Stamina Regen -10% |
| Feno's Field Pack | Reserve +25%; 6% per shot to drop 4 ammo, 2s ICD; unique |
| RabbitCharm | Max Stamina +6%, Stamina Regen +5% |
| RabbitGlove | Stability +3, Reload Time -4% |
| RabbitHat | Max HP +6% |
| RabbitLag | 20% on dash to restore 20% magazine; unique |
| Casino Lucky Coin | Crit +3; on kill Crit +5 for 4s; unique |
| Cleaner's Lighter | On kill Damage +8% for 5s, 8s ICD; unique |
| Collector's Shades | Crit +4, Bullet Speed +8% |
| Dashboard Rosary | Perfect dodge Armor +15% for 4s; unique |
| Emergency Pager | On hit Movement Speed +10% for 2s, 8s ICD; unique |
| Gang Signet Ring | Max HP +4%, Armor +2 |
| Old Leather Holster | Reload Time -8%, Stability +5 |
| Pointed Leather Shoes | Movement Speed +3%, Max Stamina +6% |
| Slow Gold Watch | Reload Time -5%, Max Energy +5% |
| Taped Magazine | Magazine +5%, Reload Time +2% |
| Trunk Ammo Box | Reserve +15% |
| Under-Suit Vest | Armor +5, Movement Speed -2% |

## Test Stage Drops, Shop, And Upgrade Economy

`EnemyDropper` grants currency before and independently from the random-loot
roll. M1 grants 15 gold and 0-1 scrap with 5% loot chance; M2 grants 25 gold and
1 scrap with 10%; Elite grants 50 gold and 2 scrap with 25%; Boss grants 150
gold and 5 scrap with 100%.

Rarity weights are stage-aware:

| Source | Stage 01 C/R/E | Stage 02 C/R/E | Stage 03 C/R/E |
| --- | --- | --- | --- |
| Normal | 90/9/1 | 70/25/5 | 50/40/10 |
| Elite | 70/25/5 | 50/40/10 | 30/50/20 |
| Boss | 55/40/5 | 25/55/20 | 10/45/45 |

Common loot is 80% accessory and 20% complete weapon, Rare is 50/50, and Epic
is always a complete weapon. The gun table contains only the five finished
player weapons.

Each Test Stage Shop node creates five entries once for that room instance and
keeps the same stock when reopened. There is no free refresh, duplicate catalog
items are allowed, and each entry has one unit of stock. Weapon rarity and
upgrade ranges are Stage 01 80/20/0 at +0..+5, Stage 02 45/45/10 at +5..+15,
and Stage 03 20/50/30 at +10..+25. Shop weapons never generate at +30.

Weapon buy price is `300 + rarity premium + 50% of cumulative upgrade gold`,
where Rare adds 150 and Epic adds 350. Weapon sell price is 30% of buy price.

## Extension Rules

- Use instance data for non-stackable equipment.
- Keep equipment owner ids stable.
- Let `CharacterEquipment` call `WeaponSystem.Equip`.
- Call `WeaponSystem.NotifyWeaponInstanceChanged()` after changing the active
  weapon instance externally.
- Use `EquipmentAssignmentService` for UI-driven equip operations.
- Do not remove serialized inventory or equipment fields without checking save
  compatibility and prefab usage.
