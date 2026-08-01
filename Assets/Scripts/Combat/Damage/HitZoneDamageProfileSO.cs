using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "Hit Zone Damage Profile",
    menuName = "Combat/Damage/Hit Zone Damage Profile")]
public sealed class HitZoneDamageProfileSO : ScriptableObject
{
    [Serializable]
    public struct HitZoneMultiplier
    {
        public HitZoneMultiplier(CharacterHitZone hitZone, float multiplier)
        {
            HitZone = hitZone;
            Multiplier = multiplier;
        }

        public CharacterHitZone HitZone;
        [Min(0f)] public float Multiplier;
    }

    [SerializeField]
    private List<HitZoneMultiplier> multipliers = new()
    {
        new HitZoneMultiplier(CharacterHitZone.Torso, 1f),
        new HitZoneMultiplier(CharacterHitZone.Head, 1.5f)
    };

    public float GetMultiplier(CharacterHitZone hitZone)
    {
        if (hitZone == CharacterHitZone.None || multipliers == null)
            return 1f;

        for (int i = 0; i < multipliers.Count; i++)
        {
            HitZoneMultiplier entry = multipliers[i];
            if (entry.HitZone != hitZone)
                continue;

            if (!float.IsFinite(entry.Multiplier))
                return 1f;

            return Mathf.Max(0f, entry.Multiplier);
        }

        return 1f;
    }
}
