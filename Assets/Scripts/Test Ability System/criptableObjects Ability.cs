using UnityEngine;

[CreateAssetMenu(menuName="Ability/Ability")]
public class AbilityDef : ScriptableObject
{
    public string id;
    public float cooldown = 4f;
    public TargetingDef targeting;
    public EffectDef[] effects;
}

[CreateAssetMenu(menuName="Ability/Targeting")]
public class TargetingDef : ScriptableObject
{
    public TargetMode mode = TargetMode.Point;
    public float range = 15f;      // ระยะขว้าง
    public float explodeRadius = 3f;
    public LayerMask hitMask;
}

public abstract class EffectDef : ScriptableObject
{
    public abstract void Apply(AbilityContext ctx, Collider[] targets);
}