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
- accessory modifier definitions and providers

Accessory loadouts should resolve through `CharacteContext.AccessoryLoadout`
when possible.

The weapon upgrade UI lists both weapon and accessory instances. Accessories
cannot be upgraded there, but they can be dismantled when they are not assigned
to any player or companion loadout. The default reward is `10` scrap, plus `5`
when the instance has a modifier, plus `3` per accessory upgrade level. These
values and the save/assignment rules are serialized on
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

`slot_{slot}_game.json` is a legacy aggregate format. When a split file is
missing, `SaveSystem` reads the corresponding section from the legacy file and
writes the new file without deleting the legacy source. This also allows an
interrupted migration to resume one section at a time. New saves do not update
the legacy aggregate file.

Use the system-specific save APIs for frequent changes. Shop purchases, weapon
upgrades, and dismantling write inventory only. Weapon equip writes inventory
plus equipment, while accessory equip writes inventory plus accessory loadouts.
Full saves remain appropriate for scene transitions and explicit full-save
actions.

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

## Extension Rules

- Use instance data for non-stackable equipment.
- Keep equipment owner ids stable.
- Let `CharacterEquipment` call `WeaponSystem.Equip`.
- Call `WeaponSystem.NotifyWeaponInstanceChanged()` after changing the active
  weapon instance externally.
- Use `EquipmentAssignmentService` for UI-driven equip operations.
- Do not remove serialized inventory or equipment fields without checking save
  compatibility and prefab usage.
