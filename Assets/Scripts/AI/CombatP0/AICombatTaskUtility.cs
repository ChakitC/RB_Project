using UnityEngine;

public static class AICombatTaskUtility
{
    public static bool CanAct(CharacteContext ctx)
    {
        if (ctx == null || !ctx.isActiveAndEnabled || ctx.stateHub == null) return false;
        CharacterActionFacts f = ctx.stateHub.GetActionFacts();
        return f.Initialized && f.Life == LifeStateId.Alive && !f.Stunned && !f.Knockback &&
            !f.Dashing && !f.SkillPlayback && f.Blocks == ControlBlockFlags.None &&
            (ctx.AnimBrain == null || !ctx.AnimBrain.RootMotionActive) &&
            (ctx.MovementMotor == null || !ctx.MovementMotor.IsAgentTransformSuspended) &&
            (ctx.FieldAllyMember == null || (!ctx.FieldAllyMember.IsReserved &&
                !ctx.FieldAllyMember.HasActiveSequenceExecution && !ctx.FieldAllyMember.HasDeferredSequenceCleanup));
    }

    public static bool CanMove(CharacteContext ctx) => CanAct(ctx) && ctx.stateHub.CanMove();
    public static bool IsPaused => GlobalTimeScaleManager.Instance.IsPaused;
    public static float Delta(CharacteContext ctx) => IsPaused ? 0f :
        ctx != null && ctx.UsesWorldSlow && TimeSlowManager.Instance != null
            ? TimeSlowManager.Instance.WorldDeltaTime : Time.deltaTime;

    public static bool Matches(AITargetHandle handle, GameObject target) => handle.IsLive &&
        target != null && CharacterContextModuleLookup.ResolveContext(target.transform) == handle.Context;

    public static bool IsEligible(CharacteContext ctx, AITargetHandle handle) =>
        AITargetPerception.IsEligible(ctx, handle, true, false, 0);

    public static float PlanarDistance(Vector3 a, Vector3 b) =>
        Vector3.ProjectOnPlane(a - b, Vector3.up).magnitude;

    public static bool InRange(float distance, float min, float max, float tolerance = 0f) =>
        distance >= Mathf.Max(0f, min - tolerance) && distance <= max + tolerance;

    public static bool CanShoot(CharacteContext ctx, AITargetSensor sensor, GameObject target,
        float min, float max, bool allowReload, bool checkAngle, float angle)
    {
        if (!CanAct(ctx) || sensor == null || ctx.WeaponSystem == null) return false;
        AITargetSnapshot s = sensor.ReadSnapshot;
        if (!Matches(s.Target, target) || !IsEligible(ctx, s.Target) || !s.HasLineOfSight ||
            !InRange(PlanarDistance(ctx.transform.position, s.AimPosition), min, max)) return false;
        WeaponSystem weapon = ctx.WeaponSystem;
        if (weapon.CurrentWeapon == null || weapon.FirePoint == null) return false;
        CharacterActionFacts facts = ctx.stateHub.GetActionFacts();
        if (allowReload && (weapon.IsReloading || (weapon.IsMagazineEmpty && weapon.CanReload)))
        {
            // Ask the same admission policy with only the reload/ammo facts removed.
            facts.Reloading = facts.EmptyMagazine = false;
            if (!CharacterActionPolicy.Evaluate(CharacterAction.Shoot, facts).Allowed) return false;
        }
        else if (!weapon.CanFire || !ctx.stateHub.CanShoot()) return false;
        if (!checkAngle) return true;
        Vector3 direction = Vector3.ProjectOnPlane(s.AimPosition - ctx.transform.position, Vector3.up);
        return direction.sqrMagnitude < .0001f || Vector3.Angle(ctx.transform.forward, direction) <= angle;
    }
}
