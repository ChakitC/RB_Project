using UnityEngine;

public static class DamageCalculator
{
    const float MIN_SAFE_ARMOR = -99f;

    public static float CalculateFinalDamage(
        WeaponType gunType,
        float distance,
        float baseDamage,
        float criticalRate,
        float criticalDamageMultiplier,
        float targetArmor)
    {
        distance = SanitizeNonNegative(distance);
        baseDamage = SanitizeNonNegative(baseDamage);

        float finalDamage = ApplyRangeFalloff(gunType, distance, baseDamage);

        float critChance01 = NormalizeCritChance01(criticalRate);
        if (critChance01 > 0f && Random.value < critChance01)
            finalDamage *= NormalizeCritMultiplier(criticalDamageMultiplier);

        finalDamage *= GetArmorFactor(targetArmor);

        if (!float.IsFinite(finalDamage))
            return 0f;

        return Mathf.Max(0f, finalDamage);
    }

    public static float NormalizeCritChance01(float criticalRate)
    {
        if (!float.IsFinite(criticalRate) || criticalRate <= 0f)
            return 0f;

        // Keep supporting legacy 0..1 inputs while making "1" mean 1%.
        if (criticalRate >= 1f)
            return Mathf.Clamp01(criticalRate * 0.01f);

        return Mathf.Clamp01(criticalRate);
    }

    public static float NormalizeCritMultiplier(float criticalDamageMultiplier)
    {
        if (!float.IsFinite(criticalDamageMultiplier) || criticalDamageMultiplier <= 0f)
            return 1f;

        // Accept legacy bonus-style values such as 0.1 => 1.1x crit damage.
        if (criticalDamageMultiplier < 1f)
            return 1f + criticalDamageMultiplier;

        return criticalDamageMultiplier;
    }

    static float ApplyRangeFalloff(WeaponType gunType, float distance, float baseDamage)
    {
        GetRangeFalloff(gunType, baseDamage, out float dropPerMeter, out float maxRange);

        if (distance <= maxRange || dropPerMeter <= 0f || float.IsInfinity(maxRange))
            return baseDamage;

        float distanceOver = distance - maxRange;
        float reduction = distanceOver * dropPerMeter;
        return Mathf.Max(0f, baseDamage - reduction);
    }

    static void GetRangeFalloff(WeaponType gunType, float baseDamage, out float dropPerMeter, out float maxRange)
    {
        switch (gunType)
        {
            case WeaponType.Sniper:
                dropPerMeter = 0f;
                maxRange = 120f;
                break;

            case WeaponType.Shotgun:
                dropPerMeter = baseDamage * 0.02f;
                maxRange = 12f;
                break;

            case WeaponType.Pistol:
                dropPerMeter = baseDamage * 0.005f;
                maxRange = 25f;
                break;

            case WeaponType.Rifle:
                dropPerMeter = baseDamage * 0.003f;
                maxRange = 40f;
                break;

            case WeaponType.Smg:
                dropPerMeter = baseDamage * 0.08f;
                maxRange = 3f;
                break;

            case WeaponType.Melee:
                dropPerMeter = 0f;
                maxRange = Mathf.Infinity;
                break;

            default:
                Debug.LogWarning($"Unknown gun type: {gunType}. Using default falloff.");
                dropPerMeter = baseDamage * 0.004f;
                maxRange = 30f;
                break;
        }
    }

    static float GetArmorFactor(float targetArmor)
    {
        if (!float.IsFinite(targetArmor))
            return 1f;

        float safeArmor = Mathf.Max(MIN_SAFE_ARMOR, targetArmor);
        return 100f / (100f + safeArmor);
    }

    static float SanitizeNonNegative(float value)
    {
        if (!float.IsFinite(value))
            return 0f;

        return Mathf.Max(0f, value);
    }
}
