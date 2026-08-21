using UnityEngine;

public sealed class SkillCastContext
{
    public ISkillUser User { get; }
    public SkillGemDefinition SkillDef { get; }
    public SkillPayloadDef Execution { get; }
    public FinalSkillStats SkillStats { get; }
    public SkillUpgradeStatSnapshot Upgrades { get; }
    public CharacterAnimBrain AnimBrain { get; }
    public int RequestId { get; }
    public Transform CastOrigin { get; }
    public Transform AimTransform { get; }
    public Vector3 AimDirection { get; }
    public GameObject CasterObject { get; }
    public Transform CasterRoot { get; }

    /// <summary>
    /// The caster's reference hub, resolved and bound exactly once for this cast. Payloads and the
    /// runtimes they spawn read peer modules from here instead of running their own hierarchy
    /// search, because a character prefab may host a module on the root, a parent, or a child.
    /// Null only when the cast has no character behind it (tests, tooling).
    /// </summary>
    public CharacteContext CasterContext { get; }

    /// <summary>Caster's combat event bus, resolved through <see cref="CasterContext"/>.</summary>
    public CombatEventBus CasterEventBus { get; }

    /// <summary>Caster's status controller, resolved through <see cref="CasterContext"/>.</summary>
    public StatusEffectController CasterStatusEffects { get; }

    /// <summary>
    /// Scratch space shared by every payload in this one cast. Later steps read what earlier
    /// steps produced from here instead of searching the scene.
    /// </summary>
    public SkillCastExecutionState ExecutionState { get; } = new SkillCastExecutionState();

    public Vector3 CastPosition => CastOrigin != null ? CastOrigin.position : Vector3.zero;

    public bool HasUpgrade(string id) => Upgrades != null && Upgrades.HasUpgrade(id);

    public SkillCastContext(
        ISkillUser user,
        SkillGemDefinition skillDef,
        FinalSkillStats skillStats,
        CharacterAnimBrain animBrain = null,
        int requestId = 0,
        SkillUpgradeStatSnapshot upgrades = null)
    {
        User = user;
        SkillDef = skillDef;
        Execution = skillDef != null ? skillDef.payload : null;
        SkillStats = skillStats;
        Upgrades = upgrades;
        AnimBrain = animBrain;
        RequestId = requestId;
        CastOrigin = user != null ? user.CastOrigin : null;
        AimTransform = user != null ? user.AimTransform : null;
        AimDirection = ResolveAimDirection(user);

        if (user is Component component)
        {
            CasterObject = component.gameObject;

            // One resolve for the whole cast: every payload and every runtime it spawns reuses
            // this instead of repeating the self/parent/child search per hit or per frame.
            CasterContext = CharacterContextModuleLookup.ResolveContext(component.gameObject);
            CasterContext?.ResolveReferences();

            CasterRoot = CasterContext != null ? CasterContext.transform : component.transform;
            CasterEventBus = CharacterContextModuleLookup.ResolveCombatEventBus(
                component.gameObject, CasterContext);
            CasterStatusEffects = CharacterContextModuleLookup.ResolveStatusEffects(
                component.gameObject, CasterContext);
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
