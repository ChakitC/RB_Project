# ScriptableObject Data Layout

Use this folder for project-authored ScriptableObject assets. Keep third-party,
plugin, and package-owned data inside their original package folders.

When moving existing assets, move them through Unity's Project window or move the
`.asset` file together with its `.meta` file so references keep the same GUID.

## Folder Map

- `AI/Targeting`: `AITargetingProfileDef`
- `AI/HelperProc`: `SkillHelperDef`, `HelperChainAttackSequenceDef`
- `Audio`: `AudioCue`
- `Characters/Databases`: `CharacterDatabase`
- `Characters/Stats`: `CharacterStats`
- `Characters/Animation`: `CharacterAnimProfileSO`
- `ChainAttack/Sequences`: `ChainAttackSequenceDef`
- `ChainAttack/SkillChains`: `SkillChainDef`
- `ChainAttack/TeleportProfiles`: `ChainAttackTeleportProfileDef`
- `Combat/Cover`: `CoverConfig`
- `Combat/MeleeCombos`: `MeleeComboSO`
- `Combat/Passives`: `AlwaysOnPassiveDef`, `TriggeredPassiveDef`, `CustomPassiveDef`, `PassiveTreeDefinition`, `PassiveCustomBehavior`
- `Combat/Projectiles/Configs`: `ProjectileConfig`
- `Combat/Projectiles/Modules`: `ProjectileModule` assets such as pierce, split, arc, curve, grenade, status-on-hit
- `Combat/Stagger`: `StaggerProfileSO`
- `Combat/StatusEffects/Definitions`: `StatusEffectDef`
- `Combat/StatusEffects/VFX`: `StatusEffectVfxProfile`
- `Drops`: `DropTable`, `EnemyDropProfile`, `GunDropTable`, `RarityTable`
- `Items/Definitions`: `ItemDefinition`
- `Items/Databases`: `ItemDatabase`
- `Pickups/Effects`: `PickupEffectDef` assets
- `Progression/Levels`: `LevelTableSO`
- `Shops/Catalogs`: `ShopCatalog`
- `Skills/Definitions`: `SkillGemDefinition`
- `Skills/Payloads`: `SkillPayloadDef` assets
- `Skills/Hitboxes`: `SkillHitBoxData`
- `Skills/SupportGems`: `SupportGemDefinition`
- `Weapons/Definitions`: `GunConfig`
- `Weapons/Affixes`: `WeaponAffixDefinition`
- `Weapons/Databases`: `WeaponDatabase`, `WeaponAffixDatabase`
- `Weapons/Upgrade`: `WeaponUpgradeCurve`
- `Legacy/TestAbility`: old test ability SO assets (`AbilityDef`, `TargetingDef`, `EffectDef`)

## Current Upgrade Data

- `Weapons/Upgrade/WeaponUpgradeCurve.Default.asset` is the default upgrade curve
  used by `WeaponUpgradeService` and the weapon upgrade UI prefab.
