using System;
using System.Collections.Generic;
using UnityEngine;

public class CharacterColliderRefs : MonoBehaviour
{
    [Serializable]
    public struct HitZoneColliderRef
    {
        public CharacterHitZone HitZone;
        public Collider Collider;
    }

    public Collider CharacterPositionCollider;

    [SerializeField] private List<HitZoneColliderRef> hitZones = new();

    public bool HasHitZones
    {
        get
        {
            if (hitZones == null)
                return false;

            for (int i = 0; i < hitZones.Count; i++)
            {
                HitZoneColliderRef entry = hitZones[i];
                if (entry.HitZone != CharacterHitZone.None &&
                    entry.Collider != null &&
                    entry.Collider != CharacterPositionCollider)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public bool TryResolveHitZone(Collider hitCollider, out CharacterHitZone hitZone)
    {
        hitZone = CharacterHitZone.None;

        if (hitCollider == null || hitCollider == CharacterPositionCollider)
            return false;

        if (hitZones == null)
            return false;

        for (int i = 0; i < hitZones.Count; i++)
        {
            HitZoneColliderRef entry = hitZones[i];
            if (entry.HitZone == CharacterHitZone.None || entry.Collider != hitCollider)
                continue;

            hitZone = entry.HitZone;
            return true;
        }

        return false;
    }
}
