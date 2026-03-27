using UnityEngine;

public sealed class SkillCastContext
{
    public ISkillUser User { get; }
    public SkillGemDefinition SkillDef { get; }
    public FinalSkillStats SkillStats { get; }
    public Transform CastOrigin { get; }
    public Transform AimTransform { get; }
    public Vector3 AimDirection { get; }
    public GameObject CasterObject { get; }
    public Transform CasterRoot { get; }

    public Vector3 CastPosition => CastOrigin != null ? CastOrigin.position : Vector3.zero;

    public SkillCastContext(ISkillUser user, SkillGemDefinition skillDef, FinalSkillStats skillStats)
    {
        User = user;
        SkillDef = skillDef;
        SkillStats = skillStats;
        CastOrigin = user != null ? user.CastOrigin : null;
        AimTransform = user != null ? user.AimTransform : null;
        AimDirection = ResolveAimDirection(user);

        if (user is Component component)
        {
            CasterObject = component.gameObject;
            CasterRoot = component.transform.root;
        }
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
