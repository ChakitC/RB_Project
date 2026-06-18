using System;
using UnityEngine;

public class CameraF : MonoBehaviour
{
    public Transform taget;
    public float smooth;
    public Vector3 offset;

    [Header("Aim Ahead")]
    [SerializeField, Range(0f, 1f)] float lookAheadWeight = 0.4f;
    [SerializeField] float lookAheadSmooth = 0.15f;
    [SerializeField] float defaultLookAheadDistance = 3f;
    [SerializeField] WeaponLookAheadEntry[] lookAheadTable = new WeaponLookAheadEntry[]
    {
        new WeaponLookAheadEntry { weaponType = WeaponType.Sniper,  distance = 7f },
        new WeaponLookAheadEntry { weaponType = WeaponType.Hmg,     distance = 5f },
        new WeaponLookAheadEntry { weaponType = WeaponType.Rifle,   distance = 4f },
        new WeaponLookAheadEntry { weaponType = WeaponType.Smg,     distance = 3f },
        new WeaponLookAheadEntry { weaponType = WeaponType.Shotgun, distance = 2.5f },
        new WeaponLookAheadEntry { weaponType = WeaponType.Pistol,  distance = 2f },
        new WeaponLookAheadEntry { weaponType = WeaponType.Melee,   distance = 0.5f },
    };

    Vector3 _followVelocity = Vector3.zero;
    Vector3 _aimAheadVelocity = Vector3.zero;
    Vector3 _currentAimAheadOffset = Vector3.zero;
    bool _aimAheadEnabled;
    bool _prevIsAiming;

    void LateUpdate()
    {
        if (taget == null)
            return;

        TickAimAheadToggle();
        Vector3 targetAimAhead = ComputeAimAheadOffset();
        _currentAimAheadOffset = Vector3.SmoothDamp(
            _currentAimAheadOffset, targetAimAhead, ref _aimAheadVelocity, lookAheadSmooth);

        Vector3 targetPosition = taget.position + offset + _currentAimAheadOffset;
        transform.position = Vector3.SmoothDamp(
            transform.position, targetPosition, ref _followVelocity, smooth);
    }

    void TickAimAheadToggle()
    {
        PlayerContext ctx = PlayerContext.Instance;
        bool isAiming = ctx != null && ctx.WeaponSystem != null && ctx.WeaponSystem.IsAiming;
        if (isAiming && !_prevIsAiming)
            _aimAheadEnabled = !_aimAheadEnabled;
        _prevIsAiming = isAiming;
    }

    Vector3 ComputeAimAheadOffset()
    {
        if (!_aimAheadEnabled)
            return Vector3.zero;

        PlayerContext ctx = PlayerContext.Instance;
        if (ctx == null)
            return Vector3.zero;

        Transform aimTransform = ctx.aimTarget;
        if (aimTransform == null)
            return Vector3.zero;

        Vector3 delta = aimTransform.position - taget.position;
        delta.y = 0f;

        float dist = delta.magnitude;
        if (dist < 0.001f)
            return Vector3.zero;

        float maxDistance = GetCurrentLookAheadDistance();
        float lookAmount = Mathf.Min(dist * lookAheadWeight, maxDistance);
        return (delta / dist) * lookAmount;
    }

    float GetCurrentLookAheadDistance()
    {
        PlayerContext ctx = PlayerContext.Instance;
        if (ctx == null || ctx.WeaponSystem == null)
            return defaultLookAheadDistance;

        WeaponType current = ctx.WeaponSystem.gunType;
        for (int i = 0; i < lookAheadTable.Length; i++)
        {
            if (lookAheadTable[i].weaponType == current)
                return lookAheadTable[i].distance;
        }

        return defaultLookAheadDistance;
    }

    [Serializable]
    public struct WeaponLookAheadEntry
    {
        public WeaponType weaponType;
        public float distance;
    }
}
