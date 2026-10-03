using UnityEngine;

public sealed class AITargetPerception
{
    readonly RaycastHit[] sightHits = new RaycastHit[64];

    public void Collect(CharacteContext owner, AITargetMemory memory, AITargetSelector selector,
        AITargetHandle taunt, AITargetScanPolicy policy, float now,
        out AITargetCandidate best, out AITargetCandidate current, out AITargetCandidate taunted)
    {
        best = current = taunted = default;
        var actors = CharacterContextRegistry.ActiveContexts;
        for (int i = 0; i < actors.Count; i++)
        {
            AITargetHandle handle = new AITargetHandle(actors[i]);
            if (!IsEligible(owner, handle, policy.RequireAlive, policy.TeamFilter, policy.OwnerTeam)) continue;
            bool layerMatches = (policy.TargetLayers.value & (1 << handle.Context.gameObject.layer)) != 0;
            bool inRange = (handle.Context.transform.position - policy.Origin).sqrMagnitude <= policy.Radius * policy.Radius;
            if (handle != taunt && handle != memory.Current && (!layerMatches || !inRange)) continue;
            AITargetCandidate candidate = Observe(owner, handle, policy.Origin, policy.Forward, policy.Radius,
                policy.UseFov, policy.Fov, policy.RequireLos, policy.Obstacles, policy.Triggers, policy.FallbackHeight);
            candidate.IsVisible &= layerMatches;
            candidate.Score = selector.Score(candidate, memory, policy, now);
            if (handle == memory.Current) current = candidate;
            if (handle == taunt) taunted = candidate;
            if (candidate.IsVisible && IsBetter(candidate, best, memory.Current)) best = candidate;
        }
    }

    public static bool IsEligible(CharacteContext owner, AITargetHandle target, bool requireAlive,
        bool useTeamFilter, int ownerTeamId)
    {
        if (!target.IsLive || owner == null || !owner.isActiveAndEnabled || target.Context == owner ||
            !CharacterFactionUtility.AreHostile(owner, target.Context))
            return false;

        CharacteContext ctx = target.Context;
        AITargetInfo info = ctx.TargetInfo;
        if (info != null && (!info.IsTargetable || (requireAlive && !info.IsAlive) ||
            (useTeamFilter && info.TeamId == ownerTeamId)))
            return false;
        if (ctx.HealthSystem != null && !ctx.HealthSystem.IsAlive)
            return false;
        return ctx.stateHub == null || (ctx.stateHub.IsAlive && !ctx.stateHub.Isdown);
    }

    public AITargetCandidate Observe(CharacteContext owner, AITargetHandle target, Vector3 origin,
        Vector3 forward, float radius, bool useFov, float fov, bool requireLos,
        LayerMask obstacles, QueryTriggerInteraction triggers, float fallbackHeight)
    {
        CharacteContext ctx = target.Context;
        Transform aim = ctx.TargetInfo != null ? ctx.TargetInfo.AimPoint : null;
        Vector3 point = aim != null ? aim.position : ctx.transform.position + Vector3.up * fallbackHeight;
        bool los = ThirdPersonTargetingUtility.HasLineOfSightNonAlloc(origin, point,
            ctx.transform, obstacles, sightHits, triggers, owner.transform);
        Vector3 flatForward = Vector3.ProjectOnPlane(forward, Vector3.up);
        Vector3 flatDirection = Vector3.ProjectOnPlane(point - origin, Vector3.up);
        bool insideFov = !useFov || fov >= 360f || flatForward.sqrMagnitude < 0.0001f ||
            flatDirection.sqrMagnitude < 0.0001f || Vector3.Angle(flatForward, flatDirection) <= fov * 0.5f;
        bool inRange = (ctx.transform.position - origin).sqrMagnitude <= radius * radius;
        return new AITargetCandidate
        {
            Handle = target, Point = point, Distance = Vector3.Distance(origin, point),
            BasePriority = ctx.TargetInfo != null ? ctx.TargetInfo.BaseTargetPriority : 0f,
            Identity = ctx.TargetIdentity,
            CombatRole = ctx.TargetInfo != null ? ctx.TargetInfo.CombatRole :
                ctx.baseStats != null ? ctx.baseStats.combatRole : CharacterCombatRole.Generic,
            HasLineOfSight = los, IsVisible = inRange && insideFov && (!requireLos || los)
        };
    }

    public static bool IsBetter(AITargetCandidate candidate, AITargetCandidate best, AITargetHandle current)
    {
        if (best.Handle.InstanceId == 0 || candidate.Score > best.Score) return true;
        if (candidate.Score < best.Score) return false;
        if (candidate.Handle == current) return true;
        if (best.Handle == current) return false;
        return candidate.Handle.InstanceId < best.Handle.InstanceId;
    }
}
