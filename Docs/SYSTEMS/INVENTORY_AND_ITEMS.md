# Inventory And Items

Inventory and equipment are split between player inventory state, character
equipment state, weapon instances, accessory loadouts, pickups, drops, and save
data.

## Main Files

- `Assets\Scripts\Player\PlayerInventory.cs`
- `Assets\Scripts\Inventory`
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
- find current equipped slot data
- find which owner has an instance assigned

Use this service instead of duplicating assignment logic in UI scripts.

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
- accessory modifier definitions and providers

Accessory loadouts should resolve through `CharacteContext.AccessoryLoadout`
when possible.

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

Inventory and equipment save flows must preserve:

- inventory slot contents
- weapon and accessory instance data
- equipped instance ids
- owner-specific equipment assignments
- currency
- migration support for older save shapes

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

