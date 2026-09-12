using UnityEngine;

public struct InterruptionTargetContext
{
    public CharacteContext Context;
    public SkillTargetHandle LifeHandle;
    public Transform Transform;
    public Transform Anchor;
    public PreCastBlockController Block;
    public CharacterKnockbackMotor Knockback;
    public HealthSystem Health;

    public bool IsCurrentLife
    {
        get
        {
            return Context != null &&
                   LifeHandle != null &&
                   LifeHandle.TryResolveEffectTarget(out CharacteContext resolved) &&
                   resolved == Context;
        }
    }
}
