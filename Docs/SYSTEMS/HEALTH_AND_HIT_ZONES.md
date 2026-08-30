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

- skill projectiles, **except** a direct hit on a live Special Shoot Point
- melee hits
- built-in and module-driven area damage
- status-effect ticks
- legacy `Bullet` damage, **except** a direct hit on a live Special Shoot Point

Special Shoot Points are the one case where a player Active Skill projectile and
the legacy `Bullet` carry a hit zone. Both now run their direct hit through
`SpecialShootPointHitScope`, and when the collider they struck is a live point
they pass the zone that point's anchor authored — so a head anchor takes the
normal Headshot multiplier regardless of which of the three delivery paths fired
the shot. Their behaviour on ordinary body colliders is unchanged: still
`CharacterHitZone.None`, still no headshot. `DamageableExtensions.TakeDamage`
gained an optional trailing `hitZone` parameter for this; it defaults to `None`,
so every existing caller is unaffected.

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

When the hit lands on a live Special Shoot Point, the **same** resolved result
also reduces that point's HP. The enemy's `TakeDamage` is still called exactly
once and no second normal damage event is published, so the player sees one
damage number; the point's own feedback is its ring/crack fill and hit flash. See
`Docs/SYSTEMS/SPECIAL_SHOOT_POINTS.md` → *One damage result, never two enemy
hits*.

## Collider Resolution

`CharacterColliderRefs` owns the serialized Hit Zone-to-`Collider` mappings.
Mappings use exact collider references. `CharacterPositionCollider` must never
be registered as a Hit Zone.

`CharacterColliderRefs.SetCollidersEnabled(bool)` controls the authored
position collider and all registered Hit Zone colliders as one set.
`EnemyHealth.Die()` disables that set so a dead enemy no longer blocks
positioning or accepts Head/Torso impacts. Enemy prefabs must therefore assign
`CharacterPositionCollider` as well as their Hit Zone mappings.

`CharacterPositionCollider` is the Enemy's *body*, not a hurtbox. Enemies carry no
`CharacterController`, so this collider is also what `CharacterKnockbackMotor` and
`CharacterVerticalMotor` sweep against, and `EnemyHealth.Die()` is the whole
collision lifecycle for a dead enemy. It must be enabled, non-trigger, on the
`Enemy` layer, and outside `hitZones`; the Hit Zones stay trigger colliders on the
`Hit` layer. See `Docs/PREFABS_AND_AUTHORING.md` → *Enemy Body Collider*.

When a target has at least one valid Hit Zone mapping, an opted-in weapon
projectile accepts only mapped colliders. Unmapped actor colliders are ignored
before impact VFX, module hit notification, or despawn, so the projectile passes
through them. A limb collider may be mapped to `Torso` when it should receive
normal body damage without introducing a separate limb multiplier.

### Special Shoot Point colliders

A live Special Shoot Point collider is deliberately **absent** from
`CharacterColliderRefs.hitZones`. That list is exact-collider matched and is what
keeps ordinary hit-zone validation honest; adding and removing pooled colliders
from it across round reuse would weaken it. `SpecialShootPointRegistry` is a
separate collider-to-point lookup instead, so the point carries the zone its
selected anchor authored without touching the authored mappings.

Registration is scoped to the window in which the collider is actually live:
`SpecialShootPointInstance` registers on collider enable and unregisters on
disable, on pool return, on `OnDisable`, and on `OnDestroy`. A destroyed
component behind a surviving entry is pruned on lookup rather than returned, so a
stale entry can never absorb a shot.

`Projectile.TryResolveHitZoneImpact` consults the registry **before** the
`useHitZones` early-out, so a point resolves its zone even for a projectile that
opted out of hit zones entirely.

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

`Rector.prefab` additionally owns trigger colliders for both upper arms,
forearms, thighs, and lower legs. These nine limb/body-section colliders are
registered as `Torso`, so direct weapon hits on them deal normal body damage.
They are authored on the visual prefab because the enemy model is rebuilt from
that source at runtime; scene-only additions would be discarded.

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
