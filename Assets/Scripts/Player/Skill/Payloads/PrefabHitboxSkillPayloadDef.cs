using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Prefab Hitbox Skill Payload", menuName = "Game/Skill Payload/Prefab Hitbox")]
public sealed class PrefabHitboxSkillPayloadDef : SkillPayloadDef
{
    public enum HitboxAnchorMode
    {
        CastOrigin = 0,
        CasterRoot = 1,
        CasterChildPath = 2,
    }

    public enum HitPolicy
    {
        OncePerSkill = 0,
        OncePerStep = 1,
    }

    [Serializable]
    public sealed class HitboxStep
    {
        [SerializeField] private string eventKey = "Hit01";
        [SerializeField] private string[] groupKeys = Array.Empty<string>();
        [SerializeField] private float damageMultiplier = 1f;
        [SerializeField] private HitPolicy hitPolicy = HitPolicy.OncePerStep;
        [SerializeField] private bool clearHitCacheOnEnter = true;
        [SerializeField] private bool overrideKnockback;
        [SerializeField] private float knockbackDistance;
        [SerializeField] private float knockbackDuration = 0.12f;
        [SerializeField] private AnimationCurve knockbackProgressCurve;
        [SerializeField] private ImpactReactionKind knockbackReaction = ImpactReactionKind.MiniStun;
        [SerializeField] private bool knockbackInterruptsActions = true;

        public string EventKey => eventKey != null ? eventKey.Trim() : string.Empty;
        public IReadOnlyList<string> GroupKeys => groupKeys ?? Array.Empty<string>();
        public float DamageMultiplier => damageMultiplier;
        public HitPolicy HitPolicy => hitPolicy;
        public bool ClearHitCacheOnEnter => clearHitCacheOnEnter;
        public bool OverrideKnockback => overrideKnockback;
        public float KnockbackDistance => knockbackDistance;
        public float KnockbackDuration => knockbackDuration;
        public AnimationCurve KnockbackProgressCurve => knockbackProgressCurve;
        public ImpactReactionKind KnockbackReaction => knockbackReaction;
        public bool KnockbackInterruptsActions => knockbackInterruptsActions;
    }

    [Header("Runtime")]
    [SerializeField] private SkillHitboxSequenceRuntime sequencePrefab;
    [SerializeField] private HitboxAnchorMode anchorMode = HitboxAnchorMode.CastOrigin;
    [SerializeField] private string anchorChildPath;
    [SerializeField] private bool followAnchor = true;
    [SerializeField] private Vector3 localPositionOffset;
    [SerializeField] private Vector3 localEulerOffset;
    [SerializeField, Min(0.1f)] private float maxSequenceLifetime = 4f;

    [Header("Targeting")]
    [SerializeField] private LayerMask targetMask = ~0;
    [SerializeField] private QueryTriggerInteraction queryTriggers = QueryTriggerInteraction.Collide;
    [SerializeField] private bool showDamageNumbers = true;

    [Header("Steps")]
    [SerializeField] private List<HitboxStep> steps = new List<HitboxStep>();

    public override bool RequiresSkillTimelineEvents => true;

    public IReadOnlyList<HitboxStep> Steps => steps;
    public LayerMask TargetMask => targetMask;
    public QueryTriggerInteraction QueryTriggers => queryTriggers;
    public bool FollowAnchor => followAnchor;
    public Vector3 LocalPositionOffset => localPositionOffset;
    public Vector3 LocalEulerOffset => localEulerOffset;
    public float MaxSequenceLifetime => Mathf.Max(0.1f, maxSequenceLifetime);
    public bool ShowDamageNumbers => showDamageNumbers;

    public override void CollectTimelineEventNames(List<string> eventNames)
    {
        if (eventNames == null || steps == null)
            return;

        for (int i = 0; i < steps.Count; i++)
        {
            HitboxStep step = steps[i];
            if (step == null)
                continue;

            string eventKey = step.EventKey;
            if (!IsValidEventKey(eventKey))
                continue;

            AddUnique(eventNames, BuildTimelineEventName(eventKey, isOn: true));
            AddUnique(eventNames, BuildTimelineEventName(eventKey, isOn: false));
        }
    }

    public override void Execute(SkillCastContext context)
    {
        if (context == null)
            return;

        if (sequencePrefab == null)
        {
            Debug.LogError($"Skill payload '{name}' has no hitbox sequence prefab assigned.", this);
            return;
        }

        if (context.AnimBrain == null || context.RequestId <= 0)
        {
            Debug.LogError(
                $"Skill '{context.SkillDef?.name ?? name}' requires a valid Animancer skill request to drive prefab hitbox events.",
                this);
            return;
        }

        ResolveSpawnPose(context, out _, out Vector3 spawnPosition, out Quaternion spawnRotation);

        SkillHitboxSequenceRuntime runtime = UnityEngine.Object.Instantiate(
            sequencePrefab,
            spawnPosition,
            spawnRotation);

        if (runtime == null)
        {
            Debug.LogError($"Failed to instantiate skill hitbox runtime from '{sequencePrefab.name}'.", this);
            return;
        }

        runtime.Initialize(context, this);
    }

    internal void ResolveSpawnPose(
        SkillCastContext context,
        out Transform anchor,
        out Vector3 worldPosition,
        out Quaternion worldRotation)
    {
        anchor = ResolveAnchor(context);

        Transform fallback = context != null
            ? (context.CastOrigin != null ? context.CastOrigin : context.CasterRoot)
            : null;

        Transform basis = anchor != null ? anchor : fallback;
        Vector3 basisPosition = basis != null ? basis.position : (context != null ? context.CastPosition : Vector3.zero);
        Quaternion basisRotation = basis != null ? basis.rotation : Quaternion.identity;

        Quaternion localRotation = Quaternion.Euler(localEulerOffset);
        worldPosition = basisPosition + basisRotation * localPositionOffset;
        worldRotation = basisRotation * localRotation;
    }

    internal Transform ResolveAnchor(SkillCastContext context)
    {
        if (context == null)
            return null;

        switch (anchorMode)
        {
            case HitboxAnchorMode.CasterRoot:
                return context.CasterRoot;

            case HitboxAnchorMode.CasterChildPath:
                if (context.CasterRoot == null || string.IsNullOrWhiteSpace(anchorChildPath))
                    return context.CastOrigin != null ? context.CastOrigin : context.CasterRoot;

                Transform resolved = context.CasterRoot.Find(anchorChildPath.Trim());
                return resolved != null ? resolved : (context.CastOrigin != null ? context.CastOrigin : context.CasterRoot);

            case HitboxAnchorMode.CastOrigin:
            default:
                return context.CastOrigin != null ? context.CastOrigin : context.CasterRoot;
        }
    }

    internal static string BuildTimelineEventName(string eventKey, bool isOn)
    {
        string safeKey = eventKey != null ? eventKey.Trim() : string.Empty;
        return string.IsNullOrEmpty(safeKey)
            ? string.Empty
            : $"{safeKey}_{(isOn ? "On" : "Off")}";
    }

    internal static bool TrySplitTimelineEventName(string eventName, out string eventKey, out bool isOn)
    {
        eventKey = string.Empty;
        isOn = false;

        if (string.IsNullOrWhiteSpace(eventName))
            return false;

        string trimmed = eventName.Trim();
        if (trimmed.EndsWith("_On", StringComparison.OrdinalIgnoreCase))
        {
            eventKey = trimmed.Substring(0, trimmed.Length - 3);
            isOn = true;
            return !string.IsNullOrWhiteSpace(eventKey);
        }

        if (trimmed.EndsWith("_Off", StringComparison.OrdinalIgnoreCase))
        {
            eventKey = trimmed.Substring(0, trimmed.Length - 4);
            isOn = false;
            return !string.IsNullOrWhiteSpace(eventKey);
        }

        return false;
    }

    internal static bool IsValidEventKey(string eventKey)
    {
        if (string.IsNullOrWhiteSpace(eventKey))
            return false;

        string trimmed = eventKey.Trim();
        if (trimmed.IndexOf(' ') >= 0)
            return false;

        if (trimmed.EndsWith("_On", StringComparison.OrdinalIgnoreCase) ||
            trimmed.EndsWith("_Off", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    static void AddUnique(List<string> eventNames, string eventName)
    {
        if (eventNames == null || string.IsNullOrWhiteSpace(eventName))
            return;

        if (!eventNames.Contains(eventName))
            eventNames.Add(eventName);
    }
}
