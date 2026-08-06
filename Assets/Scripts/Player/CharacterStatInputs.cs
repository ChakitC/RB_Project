using UnityEngine;

/// <summary>
/// Input snapshot consumed by <see cref="CharacterStatFormula"/>.
/// Character values are already level-scaled; weapon values are read straight off the
/// authored <see cref="GunConfig"/> so combat and lobby previews cannot drift apart.
/// </summary>
public struct CharacterStatInputs
{
    public float CharacterDamage;
    public float CharacterArmor;
    public float CharacterMoveSpeed;
    public float CharacterCritRate;
    public float CharacterCritMultiplier;
    public float CharacterMaxHealth;
    public float CharacterMaxStamina;
    public float CharacterMaxEnergy;

    /// <summary>Equipped weapon, or null for an unarmed character.</summary>
    public GunConfig Weapon;

    public float WeaponDamage => Weapon ? Weapon.damage : 0f;
    public float WeaponCritRate => Weapon ? Weapon.critRate : 0f;

    public float WeaponCritMultiplier => Weapon
        ? Mathf.Max(1f, Weapon.critMultiplier)
        : CharacterStatFormula.BaseCritMultiplier;

    public float WeaponFireInterval => Weapon ? Weapon.fireRate : 0f;
    public float WeaponReloadTime => Weapon ? Weapon.reloadTime : 0f;
    public float WeaponStability => Weapon ? Weapon.stability : 0f;
    public float WeaponBulletSpeed => Weapon ? Weapon.BulletSpeed : 0f;
    public float WeaponMagazine => Weapon ? Weapon.maxMagazine : 0f;
}
