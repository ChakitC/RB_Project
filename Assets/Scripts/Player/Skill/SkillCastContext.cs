using UnityEngine;

public sealed class SkillCastContext
{
    public ISkillUser User { get; }
    public SkillGemDefinition SkillDef { get; }
    public SkillPayloadDef Execution { get; }
    public FinalSkillStats SkillStats { get; }
    public CharacterAnimBrain AnimBrain { get; }
    public int RequestId { get; }
    public Transform CastOrigin { get; }
    public Transform AimTransform { get; }
    public Vector3 AimDirection { get; }
    public GameObject CasterObject { get; }
    public Transform CasterRoot { get; }

    public Vector3 CastPosition => CastOrigin != null ? CastOrigin.position : Vector3.zero;

    public SkillCastContext(
        ISkillUser user,
        SkillGemDefinition skillDef,
        FinalSkillStats skillStats,
        CharacterAnimBrain animBrain = null,
        int requestId = 0)
    {
        User = user;
        SkillDef = skillDef;
        Execution = skillDef != null ? skillDef.payload : null;
        SkillStats = skillStats;
        AnimBrain = animBrain;
        RequestId = requestId;
        CastOrigin = user != null ? user.CastOrigin : null;
        AimTransform = user != null ? user.AimTransform : null;
        AimDirection = ResolveAimDirection(user);

        if (user is Component component)
        {
            CasterObject = component.gameObject;
            CasterRoot = ResolveCasterRoot(component);
        }
    }

    static Transform ResolveCasterRoot(Component component)
    {
        if (component == null)
            return null;

        CharacteContext context = component.GetComponentInParent<CharacteContext>();
        return context != null ? context.transform : component.transform;
    }

    static Vector3 ResolveAimDirection(ISkillUser user)
    {
        if (user == null)
            return Vector3.forward;

        Vector3 dir = user.AimDirection;
        if (dir.sqrMagnitude > 0.0001f)
            return dir.normalized;

        if (user.AimTransform != null && user.AimTransform.forward.sqrMagnitude > 0.0001f)
            return user.AimTransform.forward.normalized;

        if (user.CastOrigin != null && user.CastOrigin.forward.sqrMagnitude > 0.0001f)
            return user.CastOrigin.forward.normalized;

        return Vector3.forward;
    }
}
