using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Single source of truth for character stat math.
/// <see cref="StatsHub"/> uses it for live combat values and
/// <see cref="LobbyCharacterStatPreview"/> uses it for the Basement status panel,
/// so both sides always agree. Lift changes here rather than duplicating the formula.
/// </summary>
public static class CharacterStatFormula
{
    public const float BaseCritMultiplier = 1f;
    public const float MinStabilityPercent = 0f;
    public const float MaxStabilityPercent = 100f;

    /// <summary>
    /// Floor เป็นสัดส่วนของ baseValue กันไม่ให้ debuff จากหลายแหล่งคูณสะสม (Multiply) จนสถิติเป็น 0 ทั้งที่ไม่มีใครตั้งใจ.
    /// อยู่จุดเดียวใน ApplyModifiers เพราะรับ baseValue อยู่แล้ว — LobbyCharacterStatPreview/WeaponStatPreview
    /// ที่เรียก Compute ตัวเดียวกันจะได้ค่าตรงกับตัวจริงในเกมโดยอัตโนมัติ ไม่ต้องเดินสาย config แยก.
    /// </summary>
    static readonly Dictionary<StatType, float> MinFractionOfBase = new()
    {
        { StatType.Damage, 0.2f },
        { StatType.MoveSpeed, 0.2f },
        { StatType.MaxHP, 0.2f },
        { StatType.MaxStamina, 0.2f },
    };

    /// <summary>
    /// Authoring contract: AddPercent uses whole percentages (10 => +10%),
    /// Multiply uses direct factors (1.2 => x1.2).
    /// </summary>
    public static float ApplyModifiers(List<RuntimeStatModifier> modifiers, StatType statType, float baseValue)
    {
        float flat = 0f;
        float addPercent01 = 0f;
        float multiply = 1f;

        if (modifiers != null)
        {
            for (int i = 0; i < modifiers.Count; i++)
            {
                var modifier = modifiers[i];
                if (modifier.StatType != statType)
                    continue;

                switch (modifier.Operation)
                {
                    case ModifierOp.Flat:
                        flat += modifier.Value;
                        break;

                    case ModifierOp.AddPercent:
                        addPercent01 += modifier.Value * 0.01f;
                        break;

                    case ModifierOp.Multiply:
                        multiply *= Mathf.Max(0f, modifier.Value);
                        break;
                }
            }
        }

        float result = (baseValue + flat) * (1f + addPercent01) * multiply;

        if (MinFractionOfBase.TryGetValue(statType, out float minFraction))
            result = Mathf.Max(result, baseValue * minFraction);

        return result;
    }

    public static CharacterStatTotals Compute(in CharacterStatInputs inputs, List<RuntimeStatModifier> modifiers)
    {
        GunConfig w = inputs.Weapon;
        var totals = new CharacterStatTotals();

        totals.Damage = Mathf.Max(0f, ApplyModifiers(modifiers, StatType.Damage, inputs.WeaponDamage + inputs.CharacterDamage));
        totals.Armor = Mathf.Max(0f, ApplyModifiers(modifiers, StatType.Armor, inputs.CharacterArmor));
        totals.MoveSpeed = Mathf.Max(0f, ApplyModifiers(modifiers, StatType.MoveSpeed, inputs.CharacterMoveSpeed));
        totals.CritRatePercent = Mathf.Clamp(ApplyModifiers(modifiers, StatType.CritRate, inputs.WeaponCritRate + inputs.CharacterCritRate), 0f, 100f);
        totals.CritMultiplier = Mathf.Max(1f, ApplyModifiers(modifiers, StatType.CritMultiplier, inputs.WeaponCritMultiplier + (inputs.CharacterCritMultiplier - BaseCritMultiplier)));
        totals.FireInterval = Mathf.Max(0.01f, ApplyModifiers(modifiers, StatType.FireInterval, inputs.WeaponFireInterval));
        totals.ReloadTime = Mathf.Max(0f, ApplyModifiers(modifiers, StatType.ReloadTime, inputs.WeaponReloadTime));
        totals.Stability = Mathf.Clamp(
            ApplyModifiers(modifiers, StatType.Stability, inputs.WeaponStability),
            MinStabilityPercent,
            MaxStabilityPercent);
        totals.BulletSpeed = Mathf.Max(0f, ApplyModifiers(modifiers, StatType.BulletSpeed, inputs.WeaponBulletSpeed));
        totals.MaxMagazine = Mathf.Max(0, Mathf.RoundToInt(ApplyModifiers(modifiers, StatType.MaxMagazine, inputs.WeaponMagazine)));
        int weaponReserveAmmo = w ? WeaponInstanceFactory.ResolveMaxReserveAmmo(w, totals.MaxMagazine) : 0;
        totals.MaxReserveAmmo = w && !w.infiniteReserveAmmo
            ? Mathf.Max(0, Mathf.RoundToInt(ApplyModifiers(modifiers, StatType.MaxReserveAmmo, weaponReserveAmmo)))
            : 0;
        totals.MaxHealth = Mathf.Max(1f, ApplyModifiers(modifiers, StatType.MaxHP, inputs.CharacterMaxHealth));
        totals.MaxStamina = Mathf.Max(1f, ApplyModifiers(modifiers, StatType.MaxStamina, inputs.CharacterMaxStamina));
        totals.MaxEnergy = Mathf.Max(0f, ApplyModifiers(modifiers, StatType.MaxEnergy, inputs.CharacterMaxEnergy));
        totals.SkillBaseDamage = Mathf.Max(0f, ApplyModifiers(modifiers, StatType.Damage, inputs.CharacterDamage));

        return totals;
    }
}
