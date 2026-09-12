using UnityEngine;

public enum SkillFacingAssistState
{
    NotRequested = 0,
    FallbackDirection = 1,
    LockedTarget = 2,
}

/// <summary>Facing decision captured when a command is accepted, before animation delay.</summary>
public readonly struct SkillFacingSnapshot
{
    public readonly SkillFacingAssistState State;
    public readonly SkillTargetHandle Target;
    public readonly Vector3 FallbackDirection;
    public readonly float MaximumRange;

    public bool IsRequested => State != SkillFacingAssistState.NotRequested;

    SkillFacingSnapshot(
        SkillFacingAssistState state,
        SkillTargetHandle target,
        Vector3 fallbackDirection,
        float maximumRange)
    {
        State = state;
        Target = target ?? SkillTargetHandle.None;
        FallbackDirection = NormalizePlanar(fallbackDirection, Vector3.forward);
        MaximumRange = Mathf.Max(0f, maximumRange);
    }

    public static SkillFacingSnapshot Capture(
        PlayerContext player,
        float maximumRange,
        SkillTargetHandle preferredTarget = null)
    {
        if (player == null)
            return default;

        player.ResolveReferences();
        Vector3 fallback = player.thirdPersonAim != null
            ? player.thirdPersonAim.GetPlanarCameraForward()
            : GameplayCameraController.Instance != null
                ? GameplayCameraController.Instance.PlanarForward
                : player.transform.forward;

        if (SkillTargetHandle.IsAssigned(preferredTarget) &&
            preferredTarget.TryResolveAliveTarget(out _, out _))
        {
            return new SkillFacingSnapshot(
                SkillFacingAssistState.LockedTarget,
                preferredTarget,
                fallback,
                maximumRange);
        }

        if (player.Targeting != null &&
            player.Targeting.TryGetTarget(out CharacteContext target) &&
            player.Targeting.TryValidateForAction(target, maximumRange))
        {
            return new SkillFacingSnapshot(
                SkillFacingAssistState.LockedTarget,
                SkillTargetHandle.For(target),
                fallback,
                maximumRange);
        }

        return new SkillFacingSnapshot(
            SkillFacingAssistState.FallbackDirection,
            SkillTargetHandle.None,
            fallback,
            maximumRange);
    }

    public bool TryResolveDirection(Vector3 casterPosition, out Vector3 direction)
    {
        direction = FallbackDirection;
        if (!IsRequested)
            return false;

        if (State == SkillFacingAssistState.LockedTarget &&
            Target != null &&
            Target.TryResolveAliveTarget(out Transform targetTransform, out _))
        {
            Vector3 toTarget = targetTransform.position - casterPosition;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.0001f &&
                (MaximumRange <= 0f || toTarget.sqrMagnitude <= MaximumRange * MaximumRange))
            {
                direction = toTarget.normalized;
                return true;
            }
        }

        direction = NormalizePlanar(FallbackDirection, Vector3.forward);
        return true;
    }

    static Vector3 NormalizePlanar(Vector3 value, Vector3 fallback)
    {
        value.y = 0f;
        if (value.sqrMagnitude > 0.0001f)
            return value.normalized;

        fallback.y = 0f;
        return fallback.sqrMagnitude > 0.0001f ? fallback.normalized : Vector3.forward;
    }
}
