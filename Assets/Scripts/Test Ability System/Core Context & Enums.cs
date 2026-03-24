using UnityEngine;

public enum TargetMode { Self, Point }

public struct CombatParams
{
    public WeaponType weaponType;
    public float baseDamage;
    public float critRate;              // 0..100 (%)
    public float critMultiplier;        // xN เช่น 1.5 = 150%
}

public interface IArmorProvider { float Armor { get; } } // เป้าไม่มีเกราะก็ไม่ต้องใส่คอมโพเนนต์นี้

public class AbilityContext
{
    public AbilityDef Ability;
    public Transform Caster;
    public Vector3 CastOrigin;
    public Vector3 CastDirection;
    public CombatParams Combat;         // พารามิเตอร์ที่โยนเข้า DamageCalculator

    public AbilityContext(AbilityDef ab, Transform caster, Vector3 origin, Vector3 dir, CombatParams cp)
    { Ability = ab; Caster = caster; CastOrigin = origin; CastDirection = dir; Combat = cp; }
}
