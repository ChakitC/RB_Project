using UnityEngine;

internal sealed class ChainSkillCastBridge
{
    readonly FieldAllyMember owner;

    ISkillUser _directSkillUser;
    SkillGemDefinition _lastResolvedAttackSkill;
    int _lastResolvedAttackLevel = 1;
    SkillCastOrchestrator _skillCastOrchestrator;

    public ChainSkillCastBridge(FieldAllyMember owner)
    {
        this.owner = owner;
    }

    public SkillGemDefinition LastResolvedAttackSkill => _lastResolvedAttackSkill;
    public int LastResolvedAttackLevel => Mathf.Max(1, _lastResolvedAttackLevel);
    public string ChainCastPathLabel => owner.UseDirectSkillUserForChainCastDebug
        ? "Direct ISkillUser"
        : "ChainSkillUserProxy";
    public string ResolvedDirectSkillUserDescription => DescribeSkillUser(ResolveDirectSkillUser());

    public void EnsureInitialized()
    {
        _skillCastOrchestrator ??= new SkillCastOrchestrator(owner);
    }

    public void InvalidateDirectSkillUserCache()
    {
        _directSkillUser = null;
    }

    public void ClearAimOverrides()
    {
        owner.SkillUserProxyRef?.ClearAimOverrides();
        owner.AimTargetDriverRef?.ClearOverride();
    }

    public void ApplyChainAimTargetOverride(Transform lockedTarget)
    {
        if (owner.AimTargetDriverRef == null)
            return;

        if (lockedTarget != null)
            owner.AimTargetDriverRef.SetOverrideTarget(lockedTarget, preferChainAttackPoint: true);
        else
            owner.AimTargetDriverRef.ClearOverride();
    }

    public bool TryResolveAttackSkill(
        ChainAttackStepDef step,
        out SkillInstance runtimeSkill,
        out SkillGemDefinition skillDef,
        out int skillLevel)
    {
        runtimeSkill = null;
        skillDef = null;
        skillLevel = 1;

        if (step == null)
            return false;

        if (step.skillSource == ChainStepSkillSource.ActorDefault)
        {
            if (!TryResolveActorDefaultSkill(step, out runtimeSkill, out skillDef, out skillLevel))
                return false;

            _lastResolvedAttackSkill = skillDef;
            _lastResolvedAttackLevel = skillLevel;
            return true;
        }

        skillDef = step.skillDef;
        if (skillDef == null)
            return false;

        skillLevel = skillDef.ClampLevel(step.ClampedSkillLevel);
        runtimeSkill = CreateTransientRuntimeSkill(skillDef, skillLevel);
        _lastResolvedAttackSkill = skillDef;
        _lastResolvedAttackLevel = skillLevel;
        return true;
    }

    public bool TryResolveAttackSkillUser(PendingSequenceExecution execution, out ISkillUser attackSkillUser)
    {
        attackSkillUser = null;

        if (execution != null)
            ApplyChainAimTargetOverride(execution.lockedTarget);

        if (owner.UseDirectSkillUserForChainCastDebug)
        {
            owner.SkillUserProxyRef?.ClearAimOverrides();
            attackSkillUser = ResolveDirectSkillUser();
            if (attackSkillUser == null)
            {
                owner.LogExecution($"Direct chain cast debug path is enabled but no direct ISkillUser was found on '{owner.ActorName}'.");
                return false;
            }

            return true;
        }

        if (owner.SkillUserProxyRef == null)
            return false;

        owner.SkillUserProxyRef.ClearAimOverrides();

        if (execution != null)
        {
            if (ChainAttackTargetingUtility.TryResolveTargetAnchor(execution.lockedTarget, out Transform aimAnchor))
                owner.SkillUserProxyRef.SetAimTargetOverride(aimAnchor);
            else if (execution.lockedTarget != null)
                owner.SkillUserProxyRef.SetAimTargetOverride(execution.lockedTarget);
        }

        attackSkillUser = owner.SkillUserProxyRef;
        return true;
    }

    public bool TryReleaseAttackPayload(PendingSequenceExecution execution, CharacterAnimBrain animBrain, int requestId)
    {
        if (execution == null || execution.attackSkillDef == null)
            return false;

        SkillInstance runtimeSkill = execution.attackRuntimeSkill ??
                                     CreateTransientRuntimeSkill(
                                         execution.attackSkillDef,
                                         Mathf.Max(1, execution.attackSkillLevel));

        ISkillUser attackSkillUser = execution.attackSkillUser;
        if (attackSkillUser == null && !TryResolveAttackSkillUser(execution, out attackSkillUser))
            return false;

        execution.attackSkillUser = attackSkillUser;

        EnsureInitialized();
        SkillCastStartResult castResult = _skillCastOrchestrator.TryStartCast(new SkillCastRequest(
            runtimeSkill,
            attackSkillUser,
            animationDriver: animBrain,
            requestedId: requestId,
            ignoreResourceCosts: execution.ignoreResourceCosts,
            useAnimationDriver: false,
            debugSource: $"chain:{execution.step.RuntimeId}"));
        execution.attackPayloadReleased = castResult.Started;
        return castResult.Started;
    }

    public string DescribeSkillUser(ISkillUser skillUser)
    {
        if (skillUser == null)
            return "null";

        if (skillUser is Component component)
            return $"{component.GetType().Name}:{component.name}";

        return skillUser.GetType().Name;
    }

    int ResolveActorDefaultSkillLevel(ChainAttackStepDef step)
    {
        if (owner.UseRuntimeChainSkillOverride &&
            owner.RuntimeChainSkillRef != null &&
            owner.OverrideRuntimeChainSkillLevel)
        {
            return Mathf.Max(1, owner.RuntimeChainSkillLevel);
        }

        return step != null ? step.ClampedSkillLevel : 1;
    }

    bool TryResolveActorDefaultSkill(
        ChainAttackStepDef step,
        out SkillInstance runtimeSkill,
        out SkillGemDefinition skillDef,
        out int skillLevel)
    {
        runtimeSkill = null;
        skillDef = null;
        skillLevel = 1;

        if (owner.UseRuntimeChainSkillOverride && owner.RuntimeChainSkillRef != null)
        {
            skillDef = owner.RuntimeChainSkillRef;
            skillLevel = skillDef.ClampLevel(ResolveActorDefaultSkillLevel(step));
            runtimeSkill = CreateTransientRuntimeSkill(skillDef, skillLevel);
            return true;
        }

        if (owner.TryGetConfiguredChainAttackRuntimeSkill(out runtimeSkill) &&
            runtimeSkill != null &&
            runtimeSkill.def != null)
        {
            skillDef = runtimeSkill.def;
            skillLevel = Mathf.Max(1, runtimeSkill.level);
            return true;
        }

        skillDef = owner.ResolvedActorDefaultChainSkillRef;
        if (skillDef == null)
            return false;

        skillLevel = skillDef.ClampLevel(ResolveActorDefaultSkillLevel(step));
        runtimeSkill = CreateTransientRuntimeSkill(skillDef, skillLevel);
        return true;
    }

    static SkillInstance CreateTransientRuntimeSkill(SkillGemDefinition skillDef, int skillLevel)
    {
        if (skillDef == null)
            return null;

        return new SkillInstance
        {
            def = skillDef,
            level = Mathf.Max(1, skillLevel),
        };
    }

    ISkillUser ResolveDirectSkillUser()
    {
        if (owner.DirectSkillUserSourceRef is ISkillUser typedSource)
        {
            _directSkillUser = typedSource;
            return _directSkillUser;
        }

        if (_directSkillUser != null)
            return _directSkillUser;

        CharacteContext actorContext = owner.ActorContextRef;
        if (actorContext != null && actorContext.EnegySystem != null)
        {
            _directSkillUser = actorContext.EnegySystem;
            return _directSkillUser;
        }

        MonoBehaviour[] behaviours = owner.GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null || behaviour == owner || behaviour == owner.SkillUserProxyRef)
                continue;

            if (behaviour is ISkillUser skillUser)
            {
                _directSkillUser = skillUser;
                return _directSkillUser;
            }
        }

        return null;
    }
}
