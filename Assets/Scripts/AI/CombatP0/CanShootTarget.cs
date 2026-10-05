[Opsive.Shared.Utility.Category("AI/Combat")]
public sealed class CanShootTarget : AICombatConditional
{
    public float minRange = .5f;
    public float maxRange = 10f;
    public bool allowReloadCycle;
    public bool checkAimAngle;
    public float aimToleranceDegrees = 8f;
    protected override bool Evaluate() => AICombatTaskUtility.CanShoot(ctx, sensor,
        target != null ? target.Value : null, minRange, maxRange, allowReloadCycle, checkAimAngle, aimToleranceDegrees);
}
