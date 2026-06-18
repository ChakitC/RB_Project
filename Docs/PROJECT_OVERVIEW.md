# Project Overview

This Unity project is organized around character-owned gameplay systems. Player,
ally, and enemy objects use a context component as the main reference hub, while
combat systems publish events through a local `CombatEventBus`.

## Project Roots

- Unity project: `P:\Game_RB_Project\RB_Project`
- Main gameplay scripts: `Assets\Scripts`
- Data assets: `Assets\Data` and script-specific data folders
- Scenes: `Assets\Scenes`
- Prefabs: `Assets\Prefab`
- Local validation script: `Assets\Scripts\CheckAssemblyBuild.ps1`

Generated files, Unity project files, build artifacts, `Library`, `Temp`, and
`.csproj` files should not be edited unless the task explicitly requires it.

## Core Gameplay Shape

The runtime architecture is centered on these parts:

- `CharacteContext` is the shared base reference hub for character actors.
- `PlayerContext`, `AllyContext`, and `EnemyContext` extend the shared context
  only for subtype-specific references.
- `StatsHub` calculates character and weapon stats from base stats plus runtime
  modifier providers.
- `WeaponSystem` is the weapon facade for equip, fire, reload, ammo, projectile,
  and runtime weapon-effect behavior.
- `CombatEventBus` publishes combat and passive events for the owning character.
- `PassiveController` consumes combat events, applies passive modifiers, applies
  status effects, and optionally enables passive event source components.
- `AITargetInfo` and `AITargetSensor` handle target identity, targeting state,
  scoring, and target memory.
- `PlayerInventory`, `CharacterEquipment`, and `AccessoryLoadout` own inventory
  slots, equipment assignment, weapon instances, and accessory instances.

## Important Script Areas

- `Assets\Scripts\CharacteContext.cs`: shared character reference hub.
- `Assets\Scripts\Player`: player-specific systems, inventory, stats, weapon,
  level, movement, stamina, dash, and UI-facing systems.
- `Assets\Scripts\AI`: ally AI, target sensing, chain attack, helper logic, and
  NavMesh movement support.
- `Assets\Scripts\Enemy`: enemy context and enemy-only systems.
- `Assets\Scripts\Passives`: passive definitions, event bus, event context,
  optional event sources, and stat modifier contracts.
- `Assets\Scripts\Weapons`: weapon instances, affixes, upgrades, runtime weapon
  effects, projectile spawning helpers, and weapon stat snapshots.
- `Assets\Scripts\StatusEffects`: status-effect runtime behavior.
- `Assets\Scripts\Pickup`, `Inventory`, `ItemDrop`, and `Shop`: item pickup,
  inventory, drop, and shop flows.
- `Assets\Scripts\System\SaveSystem`: save data, save manager, and migration.

## System Connection Summary

Most gameplay code should enter through the character context:

1. A character prefab has `CharacteContext` or one subtype.
2. `ResolveReferences()` binds common modules such as `StatsHub`,
   `CombatEventBus`, `WeaponSystem`, `HealthSystem`, and `SkillManager`.
3. Subtype contexts bind only subtype-specific modules.
4. Systems read peers through `ctx` when the reference is already context-owned.
5. Combat systems publish events to `ctx.CombatEventBus`.
6. Passive and stat systems listen, update runtime modifiers, and notify
   `StatsHub` through modifier change events when needed.

For details, read:

- `Docs\GAMEPLAY_OVERVIEW.md`
- `Docs\ARCHITECTURE\CHARACTER_CONTEXT.md`
- `Docs\ARCHITECTURE\COMBAT_EVENT_BUS.md`
- `Docs\SYSTEMS\WEAPON_SYSTEM.md`
- `Docs\SYSTEMS\PASSIVES.md`
- `Docs\SYSTEMS\AI_AND_TARGETING.md`
- `Docs\SYSTEMS\INVENTORY_AND_ITEMS.md`
- `Docs\PREFABS_AND_AUTHORING.md`
