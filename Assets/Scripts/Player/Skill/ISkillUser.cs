using UnityEngine;

public interface ISkillUser
{
    Transform CastOrigin { get; }
    Transform AimTransform { get; }
    Vector3 AimDirection { get; }

    float currentEnagy { get; }
    void SpendEnagy(float amount);
    
    StatsHub StatsHub { get; } // (ถ้าไม่ใช้ ให้คืน 0)
}