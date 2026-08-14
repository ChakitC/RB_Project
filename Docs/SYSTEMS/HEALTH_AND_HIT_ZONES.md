# Health And Hit Zones

## Ownership

`HealthSystem` remains the single owner of a character's HP. Hit Zones identify
the body region struck; they do not create separate health pools.

The target owns its multiplier data through
`HitZoneDamageProfileSO hitZoneDamageProfile` on `HealthSystem`. The shared
enemy profile is:

`Assets/Data/Combat/Damage/DefaultEnemyHitZoneDamageProfile.asset`

Current values:

| Hit Zone | Multiplier |
|---|---:|
| `Torso` | `1.0` |
| `Head` | `1.5` |

Missing entries and missing profiles resolve to `1.0`.

## Eligible Damage

Hit Zone resolution is opt-in through `ProjectileContext.useHitZones`.
`WeaponProjectileSpawner` enables it for direct weapon projectiles. Child and
split projectiles inherit the same context.

The following damage remains Hit Zone-neutral:

- skill projectiles
- melee hits
- built-in and module-driven area damage
- status-effect ticks
- legacy `Bullet` damage

An area projectile may require a mapped hurtbox to register its direct impact
on a configured target, but every target damaged by the resulting area query
receives `CharacterHitZone.None`.

## Damage Order

For a direct weapon-projectile hit:

1. `DamageCalculator` resolves the existing range, critical-hit, and armor
   calculation.
2. `HealthSystem` applies the target's Hit Zone multiplier.
3. `EnemyHealth` applies the active stagger damage-taken multiplier.
4. Active chain-execution non-lethal protection caps the resolved damage when
   applicable. ChainReady alone does not prevent lethal damage.
5. The shared HP value is reduced.

This makes Head damage multiplicative with the existing finalized projectile
damage while preserving chain protection.

## Collider Resolution

`CharacterColliderRefs` owns the serialized Hit Zone-to-`Collider` mappings.
Mappings use exact collider references. `CharacterPositionCollider` must never
be registered as a Hit Zone.

When a target has at least one valid Hit Zone mapping, an opted-in weapon
projectile accepts only mapped colliders. Unmapped actor colliders—including
arms and legs in the current version—are ignored before impact VFX, module hit
notification, or despawn, so the projectile passes through them.

Backward-compatible fallback:

- no `CharacterColliderRefs`, or no valid mappings: use legacy impact behavior
  and `1.0` damage
- valid mappings but no damage profile: mapped zones still receive damage at
  `1.0`

## Events And API

`DamageContext.HitZone` carries the resolved `CharacterHitZone`. Its default is
`None`, and `DamageContext.WithDamage` preserves it.

`HealthSystem.HeadshotTaken(float appliedDamage, GameObject attacker)` fires
only when a Head hit actually removes HP. It includes lethal hits and does not
fire for invincible, prevented, zero-damage, or Torso hits. No UI, audio, or
passive consumer is connected in the current version.

When adding future zones such as arms or legs, append new explicit enum values
to `CharacterHitZone`; do not reorder existing values because Unity serializes
the enum numerically.

## Current Enemy Authoring

The following visual prefabs contain dedicated `HitZone_Head` and
`HitZone_Torso` trigger colliders on the `Hit` layer:

- `Assets/Prefab/Mons/GR_NM01.prefab`
- `Assets/Prefab/Mons/GR_NM02.prefab`
- `Assets/Prefab/Mons/M_GR_03.prefab`
- `Assets/Prefab/Charactor/GRS_02.prefab`
- `Assets/Prefab/Charactor/M_GR04.prefab`
- `Assets/Prefab/Charactor/Rector.prefab`

`Assets/Prefab/Enemy/Enemy_Base.prefab` binds the shared enemy profile, so its
variants inherit the target-side multiplier data.

The legacy `Assets/Character/Mons/Enemy_Base 1.prefab` is not Hit Zone-authored.
Unity currently refuses to save it because it already contains a Missing Script.
It continues to use the backward-compatible `1.0` damage path until that prefab
is repaired in a separate task.

## Validation

After changing Hit Zone code or prefab mappings:

1. Run `Assets/Scripts/CheckAssemblyBuild.ps1`.
2. Confirm Unity finishes domain reload with no compilation errors.
3. Fire the same direct weapon projectile at Torso and Head; Head should remove
   `1.5` times the Torso HP after the normal projectile calculation.
4. Confirm an unmapped limb does not play impact feedback or consume the
   projectile on configured enemies.
5. Confirm skill, melee, area, and status damage remain unchanged.

## Summon Health

Summon prefabs use `SummonHealthSystem`. Health depletion requests the summon
runtime lifecycle directly, entering despawn without the normal character
`Down` or `Revive` flow. Summon damage keeps the physical summon as the hit
actor while `CombatAttributionSnapshot` credits the caster's event bus and
status owner, including delayed status ticks after the summon has been removed.
