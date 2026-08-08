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
- `Combat/Passives`: `AlwaysOnPassiveDef`, `TriggeredPassiveDef`, `CustomPassiveDef`, `PassiveCustomBehavior`
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
- `Skills/<Character>`: `SkillGemDefinition` and `SkillUpgradeTreeDefinition` assets
  for that character's skills, each `SkillGemDefinition` with its `SkillPayloadDef`
  embedded as a sub-asset
- `Skills/Enemies/<Character>`: `SkillGemDefinition` assets for enemy/ally NPC skills
- `Skills/_Test`: test/helper skill assets still referenced by test scenes or prefabs
- `Weapons/Definitions`: `GunConfig`
- `Weapons/Affixes`: `WeaponAffixDefinition`
- `Weapons/Databases`: `WeaponDatabase`, `WeaponAffixDatabase`
- `Weapons/Upgrade`: `WeaponUpgradeCurve`
- `Legacy/TestAbility`: old test ability SO assets (`AbilityDef`, `TargetingDef`, `EffectDef`)

## Status Effect Authoring

`Combat/StatusEffects/Definitions` (`StatusEffectDef`) assets are identity only —
`effectId`, icon, VFX, `stackMode`, `controlBlocks`, `triggerRules`. Their
`modifiers` / `duration` / `tickDamage` are fallback values, not the only place a
skill/passive/projectile can author a status's numbers. See "Magnitude at apply
site" in `RB_Project/Docs/SYSTEMS/SKILL_SYSTEM.md` before creating a new
`StatusEffectDef` that differs from an existing one only by its numbers — override
the numbers at the apply site instead of cloning the asset.

## Current Upgrade Data

- `Weapons/Upgrade/WeaponUpgradeCurve.Default.asset` is the default upgrade curve
  used by `WeaponUpgradeService` and the weapon upgrade UI prefab.
