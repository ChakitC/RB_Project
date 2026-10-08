using UnityEngine;

public static class StatModifierRules
{
    public const string ValueTooltip = "Flat adds points. AddPercent 20 = +20%. Multiply 1.2 = x1.2. Within one stack group, three x1.2 stacks = x1.6; separate groups multiply.";

    public static bool SupportsWeaponScope(StatType stat) => stat is
        StatType.Damage or StatType.CritRate or StatType.CritMultiplier or
        StatType.FireInterval or StatType.ReloadTime or StatType.ReloadSpeed or
        StatType.Stability or StatType.BulletSpeed or StatType.MaxMagazine or StatType.MaxReserveAmmo;

    public static bool IsValidScope(StatType stat, StatModifierScope scope) =>
        scope == StatModifierScope.Global || (scope == StatModifierScope.WeaponOnly && SupportsWeaponScope(stat));

    public static float StackValue(ModifierOp operation, float value, int stacks)
    {
        if (stacks <= 0) return operation == ModifierOp.Multiply ? 1f : 0f;
        return operation == ModifierOp.Multiply
            ? Mathf.Max(0f, 1f + (value - 1f) * stacks)
            : value * stacks;
    }
}
