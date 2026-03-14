using UnityEngine;

public static class DamageCalculator
{
    public static float CalculateFinalDamage(
        WeaponType gunType,
        float distance,
        float baseDamage,
        float criticalRate,
        float criticalDamageMultiplier,
        float targetArmor)
    {
        
        distance = Mathf.Max(0f, distance);
        baseDamage = Mathf.Max(0f, baseDamage);

        // --- ค่าเริ่มต้น ---
        float finalDamage = baseDamage;
        bool isCritical = false;

        // --- ตั้งค่าการดรอปดาเมจตามอาวุธ ---
        // เป็น ScriptableObject ในอนาคต
        
        float dropPerMeter;   // ดรอปต่อเมตร (หน่วยดาเมจ/เมตร)
        float maxRange;       // ระยะที่ยังไม่ดรอป

        switch (gunType)
        {
            case WeaponType.Sniper:
                dropPerMeter = 0.0f;   
                maxRange     = 120f;
                Debug.Log("case.DMG.Sniper");
                break;

            case WeaponType.Shotgun:
                dropPerMeter = baseDamage * 0.02f; 
                maxRange     = 12f;
                Debug.Log("case.DMG.Shotgun");
                break;

            case WeaponType.Pistol:
                dropPerMeter = baseDamage * 0.005f;
                maxRange     = 25f;
                Debug.Log("case.DMG.Pistol");
                break;

            case WeaponType.Rifle:
                dropPerMeter = baseDamage * 0.003f;
                maxRange     = 40f;
                Debug.Log("case.DMG.Rifle");
                break;

            case WeaponType.Smg:
                dropPerMeter = baseDamage * 0.08f;
                maxRange     = 3.0f;
                Debug.Log("case.DMG.Smg");
                break;

            case WeaponType.Melee:
                dropPerMeter = 0f;
                maxRange     = Mathf.Infinity; // ไม่สนระยะ
                Debug.Log("case.DMG.Melee");
                break;

            default:
                Debug.LogWarning($"Unknown gun type: {gunType}. Using default falloff.");
                dropPerMeter = baseDamage * 0.004f;
                maxRange     = 30f;
                break;
        }

        // --- ระยะทางเกินระยะมีผล → หักดาเมจแบบลิเนียร์ (คงบวกได้) ---
        if (distance > maxRange && dropPerMeter > 0f && !float.IsInfinity(maxRange))
        {
            float distanceOver = distance - maxRange;
            float reduction    = distanceOver * dropPerMeter;
            finalDamage = Mathf.Max(0f, finalDamage - reduction);
        }

        // --- คริติคอล ---
        
        float critChance01 = (criticalRate > 1f) ? criticalRate * 0.01f : criticalRate;
        critChance01 = Mathf.Clamp01(critChance01);

        if (Random.value < critChance01)
        {
            finalDamage *= Mathf.Max(1f, criticalDamageMultiplier); // กันค่าผิด
            isCritical = true;
            Debug.Log("isCritical"!);
        }

        // --- เกราะ (ลดแบบสัดส่วน) ---
        // สูตรพื้นฐาน: DamageAfterArmor = Damage * (100 / (100 + Armor))
        // ถ้า Armor ติดลบ = จุดอ่อน → ดาเมจเพิ่มขึ้นเอง
        float armorFactor = 100f / (100f + targetArmor);
        finalDamage *= armorFactor;

        // --- เคลียร์ค่าไม่ให้ติดลบ/NaN ---
        if (!float.IsFinite(finalDamage)) finalDamage = 0f;
        finalDamage = Mathf.Max(0f, finalDamage);

        Debug.Log("Distance" +distance);
        
        return finalDamage;
    }
}
