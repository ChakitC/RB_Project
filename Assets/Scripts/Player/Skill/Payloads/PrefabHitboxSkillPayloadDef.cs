using System;
using System.Collections.Generic;
using Animancer;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "Prefab Hitbox Skill Payload", menuName = "Game/Skill Payload/Prefab Hitbox")]
public sealed class PrefabHitboxSkillPayloadDef : SkillPayloadDef
{
    public enum TimelineBindingMode
    {
        Explicit = 0,
        Sequential = 1,
    }

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

    public enum ImpactSpawnPolicy
    {
        Disabled = 0,
        FirstHitPerStep = 1,
        EveryHit = 2,
    }

    [Serializable]
    public sealed class StepStartVfxSettings
    {
        [SerializeField] private GameObject prefab;
        [SerializeField, Min(0.01f)] private float scale = 1f;

        public GameObject Prefab => prefab;
        public float Scale => Mathf.Max(0.01f, scale);
        public bool IsEnabled => prefab != null;
    }

    [Serializable]
    public sealed class ImpactVfxSettings
    {
        [SerializeField] private GameObject prefab;
        [SerializeField, Min(0.01f)] private float scale = 1f;
        [SerializeField] private ImpactSpawnPolicy spawnPolicy = ImpactSpawnPolicy.FirstHitPerStep;

        public GameObject Prefab => prefab;
        public float Scale => Mathf.Max(0.01f, scale);
        public ImpactSpawnPolicy SpawnPolicy => prefab == null ? ImpactSpawnPolicy.Disabled : spawnPolicy;
        public bool IsEnabled => prefab != null && SpawnPolicy != ImpactSpawnPolicy.Disabled;
    }

    [Serializable]
    public sealed class HitboxStep
    {
        [SerializeField] private TimelineBindingMode timelineBindingMode = TimelineBindingMode.Sequential;
        [FormerlySerializedAs("activateEvent")]
        [SerializeField] private StringAsset activateEventOverride;
        [FormerlySerializedAs("deactivateEvent")]
        [SerializeField] private StringAsset deactivateEventOverride;
        [FormerlySerializedAs("eventKey")]
        [SerializeField, HideInInspector] private string legacyEventKey = "Hit01";
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
        [Header("VFX")]
        [SerializeField] private StepStartVfxSettings stepStartVfx = new StepStartVfxSettings();
        [SerializeField] private ImpactVfxSettings impactVfx = new ImpactVfxSettings();

        public TimelineBindingMode BindingMode => timelineBindingMode;
        public bool UsesSequentialBinding => timelineBindingMode == TimelineBindingMode.Sequential;
        public bool UsesExplicitBinding => !UsesSequentialBinding;
        public StringAsset ActivateEventAsset => activateEventOverride;
        public StringAsset DeactivateEventAsset => deactivateEventOverride;
        public StringReference ActivateEventName => UsesExplicitBinding
            ? ResolveExplicitEventName(activateEventOverride, legacyEventKey, isOn: true)
            : null;
        public StringReference DeactivateEventName => UsesExplicitBinding
            ? ResolveExplicitEventName(deactivateEventOverride, legacyEventKey, isOn: false)
            : null;
        public string StepLabel
        {
            get
            {
                if (UsesSequentialBinding)
                    return "Sequential";

                if (activateEventOverride != null)
                    return activateEventOverride.name;

                if (deactivateEventOverride != null)
                    return deactivateEventOverride.name;

                return string.IsNullOrWhiteSpace(legacyEventKey)
                    ? "<missing>"
                    : legacyEventKey.Trim();
            }
        }

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
        public StepStartVfxSettings StepStartVfx => stepStartVfx ?? (stepStartVfx = new StepStartVfxSettings());
        public ImpactVfxSettings ImpactVfx => impactVfx ?? (impactVfx = new ImpactVfxSettings());
    }

    [Header("Runtime")]
    [SerializeField] private SkillHitBoxData hitBoxData;
    [FormerlySerializedAs("sequencePrefab")]
    [SerializeField, HideInInspector] private SkillHitboxSequenceRuntime legacySequencePrefab;
    [SerializeField] private HitboxAnchorMode anchorMode = HitboxAnchorMode.CastOrigin;
    [SerializeField] private string anchorChildPath;
    [SerializeField] private bool followAnchor = true;
    [SerializeField] private Vector3 localPositionOffset;
    [SerializeField] private Vector3 localEulerOffset;
    [SerializeField, Min(0.1f)] private float maxSequenceLifetime = 4f;

    [Header("Timeline")]
    [SerializeField] private StringAsset sequentialActivateEvent;
    [SerializeField] private StringAsset sequentialDeactivateEvent;

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
    public StringReference SequentialActivateEventName => sequentialActivateEvent;
    public StringReference SequentialDeactivateEventName => sequentialDeactivateEvent;
    public SkillHitBoxData HitBoxData => hitBoxData;
    public bool HasSequentialTimelineEvents =>
        IsValidTimelineEvent(SequentialActivateEventName) &&
        IsValidTimelineEvent(SequentialDeactivateEventName);

    public override void CollectTimelineEventNames(List<StringReference> eventNames)
    {
        if (eventNames == null || steps == null)
            return;

        bool requiresSequentialEvents = false;
        for (int i = 0; i < steps.Count; i++)
        {
            HitboxStep step = steps[i];
            if (step == null)
                continue;

            if (step.UsesSequentialBinding)
            {
                requiresSequentialEvents = true;
                continue;
            }

            AddUnique(eventNames, step.ActivateEventName);
            AddUnique(eventNames, step.DeactivateEventName);
        }

        if (requiresSequentialEvents)
        {
            AddUnique(eventNames, SequentialActivateEventName);
            AddUnique(eventNames, SequentialDeactivateEventName);
        }
    }

    public override void Execute(SkillCastContext context)
    {
        if (context == null)
            return;

        if (hitBoxData == null)
        {
            string legacyHint = legacySequencePrefab != null
                ? " A legacy hitbox prefab reference is still present on this asset."
                : string.Empty;

            Debug.LogError(
                $"Skill payload '{name}' requires a SkillHitBoxData asset. Migrate the old prefab layout before using this payload.{legacyHint}",
                this);
            return;
        }

        if (context.AnimBrain == null || context.RequestId <= 0)
        {
            Debug.LogError(
                $"Skill '{context.SkillDef?.name ?? name}' requires a valid Animancer skill request to drive prefab hitbox events.",
                this);
            return;
        }

        if (!TryValidateRuntimeConfiguration(out string validationError))
        {
            Debug.LogError(validationError, this);
            return;
        }

        ResolveSpawnPose(context, out _, out Vector3 spawnPosition, out Quaternion spawnRotation);

        GameObject runtimeObject = new GameObject($"{name}_HitboxRuntime");
        runtimeObject.layer = context.CasterObject != null ? context.CasterObject.layer : 0;
        if (context.CasterRoot != null)
            runtimeObject.transform.SetParent(context.CasterRoot, false);
        runtimeObject.transform.SetPositionAndRotation(spawnPosition, spawnRotation);

        SkillHitboxSequenceRuntime runtime = runtimeObject.AddComponent<SkillHitboxSequenceRuntime>();

        if (runtime == null)
        {
            Debug.LogError($"Failed to create a runtime hitbox host for skill payload '{name}'.", this);
            return;
        }

        if (!SkillHitboxRuntimeBuilder.TryBuild(
                runtime.transform,
                hitBoxData,
                runtimeObject.layer,
                out SkillHitboxGroup[] runtimeGroups,
                out string buildError))
        {
            Debug.LogError(buildError, this);
            UnityEngine.Object.Destroy(runtimeObject);
            return;
        }

        runtime.AssignGroups(runtimeGroups);
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

    internal static bool IsValidTimelineEvent(StringReference eventName)
    {
        return eventName != null && !string.IsNullOrWhiteSpace(eventName.String);
    }

    internal static string DescribeTimelineEvent(StringReference eventName)
    {
        return eventName != null ? eventName.String : "<none>";
    }

    static StringReference ResolveExplicitEventName(StringAsset asset, string legacyEventKey, bool isOn)
    {
        if (asset != null)
            return asset;

        if (string.IsNullOrWhiteSpace(legacyEventKey))
            return null;

        string trimmed = legacyEventKey.Trim();
        if (trimmed.Length == 0)
            return null;

        return StringReference.Get($"{trimmed}_{(isOn ? "On" : "Off")}");
    }

    static void AddUnique(List<StringReference> eventNames, StringReference eventName)
    {
        if (eventNames == null || !IsValidTimelineEvent(eventName))
            return;

        if (!eventNames.Contains(eventName))
            eventNames.Add(eventName);
    }

    bool TryValidateRuntimeConfiguration(out string errorMessage)
    {
        errorMessage = null;

        if (hitBoxData == null)
        {
            errorMessage = $"Skill payload '{name}' is missing SkillHitBoxData.";
            return false;
        }

        List<string> issues = new List<string>();
        hitBoxData.CollectValidationIssues(issues);

        HashSet<string> availableGroupKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        IReadOnlyList<SkillHitBoxData.HitBoxGroupData> dataGroups = hitBoxData.Groups;
        for (int i = 0; i < dataGroups.Count; i++)
        {
            SkillHitBoxData.HitBoxGroupData group = dataGroups[i];
            if (group == null)
                continue;

            string groupKey = group.GroupKey;
            if (!string.IsNullOrWhiteSpace(groupKey))
                availableGroupKeys.Add(groupKey);
        }

        if (steps == null || steps.Count == 0)
            issues.Add($"Skill payload '{name}' has no hitbox steps configured.");

        for (int stepIndex = 0; stepIndex < steps.Count; stepIndex++)
        {
            HitboxStep step = steps[stepIndex];
            if (step == null)
            {
                issues.Add($"Skill payload '{name}' has a null hitbox step at index {stepIndex}.");
                continue;
            }

            IReadOnlyList<string> groupKeys = step.GroupKeys;
            if (groupKeys == null || groupKeys.Count == 0)
            {
                issues.Add($"Skill payload '{name}' step '{step.StepLabel}' has no group keys.");
                continue;
            }

            for (int groupIndex = 0; groupIndex < groupKeys.Count; groupIndex++)
            {
                string groupKey = groupKeys[groupIndex];
                if (string.IsNullOrWhiteSpace(groupKey))
                {
                    issues.Add($"Skill payload '{name}' step '{step.StepLabel}' has an empty group key.");
                    continue;
                }

                if (!availableGroupKeys.Contains(groupKey.Trim()))
                    issues.Add($"Skill payload '{name}' step '{step.StepLabel}' references missing group '{groupKey}'.");
            }
        }

        if (issues.Count == 0)
            return true;

        errorMessage = $"Skill payload '{name}' failed hitbox validation:\n- {string.Join("\n- ", issues)}";
        return false;
    }
}
