using System;
using System.Collections.Generic;
using System.Text;
using Animancer;
using Sirenix.OdinInspector;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[HideMonoScript]
public class SkillGemDefinition : ScriptableObject
{
    private const float DefaultCastPointNormalized = 0.35f;
    private const float WarningCastPointMin = 0.05f;
    private const float WarningCastPointMax = 0.95f;

    private enum SkillConfigStatus
    {
        Valid,
        Warning,
        Error,
    }

    private ProjectileSkillPayloadDef ProjectilePayload => payload as ProjectileSkillPayloadDef;
    private bool HasProjectilePayload => ProjectilePayload != null;
    private bool HasProjectileExecutionIntent => HasProjectilePayload;
    private bool HasAnyRadiusConfigured => baseRadius > 0f || HasRadiusOverride();
    private bool HasProjectilePresentationAssets => ProjectilePayload != null && ProjectilePayload.HasProjectilePresentationAssets;
    private bool HasAnimationPresentationAssets => castCue != null || skillClip != null || HasSkillVfxEvents;
    private bool HasAnyPresentationAssets => HasProjectilePresentationAssets || HasAnimationPresentationAssets;
    private bool HasBlockingError => GetConfigStatus() == SkillConfigStatus.Error;
    private bool HasWarning => !HasBlockingError && !string.IsNullOrEmpty(WarningSummary);
    private bool HasInfo => !string.IsNullOrEmpty(InfoSummary);

    [PropertyOrder(-130)]
    [ShowInInspector, ReadOnly, FoldoutGroup("Authoring Summary", Expanded = true), LabelText("Skill")]
    private string SummarySkillName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;

    [PropertyOrder(-129)]
    [ShowInInspector, ReadOnly, FoldoutGroup("Authoring Summary", Expanded = true), LabelText("Identity")]
    private string SummaryIdentity => $"ID: {FormatSkillId(skillId)} / Tags: {FormatTags(tags)}";

    [PropertyOrder(-128)]
    [InfoBox("$BlockingErrorSummary", InfoMessageType.Error, VisibleIf = nameof(HasBlockingError))]
    [InfoBox("$WarningSummary", InfoMessageType.Warning, VisibleIf = nameof(HasWarning))]
    [InfoBox("$InfoSummary", InfoMessageType.Info, VisibleIf = nameof(HasInfo))]
    [ShowInInspector, ReadOnly, FoldoutGroup("Authoring Summary", Expanded = true), LabelText("Runtime Summary")]
    private string RuntimeSummaryLine => BuildRuntimeSummaryLine();

    [PropertyOrder(-127)]
    [ShowInInspector, ReadOnly, FoldoutGroup("Authoring Summary", Expanded = true), HorizontalGroup("Authoring Summary/StatusStrip")]
    [LabelText("Config Status")]
    private string ConfigStatusLabel => GetConfigStatus().ToString();

    [PropertyOrder(-126)]
    [ShowInInspector, ReadOnly, FoldoutGroup("Authoring Summary", Expanded = true), HorizontalGroup("Authoring Summary/StatusStrip")]
    [LabelText("Execution Mode")]
    private string ExecutionStatusLabel => GetResolvedRuntimeModeLabel();

    [PropertyOrder(-125)]
    [ShowInInspector, ReadOnly, FoldoutGroup("Authoring Summary", Expanded = true), HorizontalGroup("Authoring Summary/StatusStrip")]
    [LabelText("Level Data")]
    private string LevelDataStatusLabel => $"{LevelRowCount} rows / {DuplicateLevelRowCount} duplicate / {EmptyOverrideRowCount} empty";

    [PropertyOrder(-124)]
    [ShowInInspector, ReadOnly, FoldoutGroup("Authoring Summary", Expanded = true), HorizontalGroup("Authoring Summary/StatusStrip")]
    [LabelText("Presentation")]
    private string PresentationStatusLabel => GetPresentationStatusLabel();

    [PropertyOrder(-100)]
    [FoldoutGroup("Identity", Expanded = true), AssetsOnly, PreviewField(80, ObjectFieldAlignment.Left), LabelText("Icon")]
    public Sprite icon;

    [PropertyOrder(-99)]
    [FoldoutGroup("Identity", Expanded = true), LabelText("Display Name")]
    public string displayName;

    [PropertyOrder(-98)]
    [FoldoutGroup("Identity", Expanded = true), LabelText("Skill ID")]
    [ValidateInput(nameof(HasSkillId), "Skill ID is required for runtime lookup.")]
    public string skillId;

    [PropertyOrder(-97)]
    [FoldoutGroup("Identity", Expanded = true), LabelText("Tags")]
    public SkillTag tags;

    [PropertyOrder(-96)]
    [FoldoutGroup("Identity", Expanded = true), LabelText("Description"), TextArea(3, 5)]
    public string description;

    [PropertyOrder(-80)]
    [FoldoutGroup("Gameplay", Expanded = true), LabelText("Damage"), MinValue(0f)]
    public float baseDamage = 10f;

    [PropertyOrder(-79)]
    [FoldoutGroup("Gameplay", Expanded = true), LabelText("Stagger Power"), MinValue(0f)]
    public float baseStaggerPower = 10f;

    [PropertyOrder(-79)]
    [FoldoutGroup("Gameplay", Expanded = true), LabelText("Mana Cost"), MinValue(0f)]
    public float baseManaCost = 10f;

    [PropertyOrder(-78)]
    [FoldoutGroup("Gameplay", Expanded = true), LabelText("Cast Time"), MinValue(0f), SuffixLabel("s")]
    public float baseCastTime = 0.5f;

    [PropertyOrder(-77)]
    [FoldoutGroup("Gameplay", Expanded = true), LabelText("Cooldown"), MinValue(0f), SuffixLabel("s")]
    public float baseCooldown = 0f;

    [PropertyOrder(-76)]
    [FoldoutGroup("Gameplay", Expanded = true), LabelText("Crit Chance"), MinValue(0f), SuffixLabel("%")]
    public float baseCritChance = 5f;

    [PropertyOrder(-75)]
    [FoldoutGroup("Gameplay", Expanded = true), LabelText("Projectile Count"), MinValue(1)]
    public int baseProjectilesCount = 1;

    [PropertyOrder(-74)]
    [FoldoutGroup("Gameplay", Expanded = true), LabelText("Projectile Speed"), MinValue(0f), SuffixLabel("m/s")]
    public float projectileSpeed = 5f;

    [PropertyOrder(-73)]
    [FoldoutGroup("Gameplay", Expanded = true), LabelText("Area Of Effect"), ToggleLeft]
    public bool AreaofEffec = false;

    [PropertyOrder(-72)]
    [FoldoutGroup("Gameplay", Expanded = true), LabelText("Radius"), MinValue(0f), SuffixLabel("m")]
    public float baseRadius = 0f;

    [PropertyOrder(-71)]
    [FoldoutGroup("Gameplay", Expanded = true), LabelText("Max Level"), MinValue(1)]
    public int maxLevel = 20;

    [PropertyOrder(-60)]
    [ShowInInspector, ReadOnly, FoldoutGroup("Execution", Expanded = true), LabelText("Primary Source")]
    private string PrimaryExecutionSourceLabel => payload != null
        ? $"Payload ({payload.name})"
        : "None";

    [PropertyOrder(-58)]
    [ShowInInspector, ReadOnly, FoldoutGroup("Execution", Expanded = true), LabelText("Resolved Runtime Mode")]
    private string ResolvedRuntimeModeLabel => GetResolvedRuntimeModeLabel();

    [PropertyOrder(-57)]
    [ShowInInspector, ReadOnly, FoldoutGroup("Execution", Expanded = true), LabelText("Projectile Ownership")]
    private string ProjectileOwnershipLabel => GetProjectileOwnershipLabel();

    [PropertyOrder(-56)]
    [SerializeField, HideInInspector]
    public SkillPayloadDef payload;

    [PropertyOrder(-37)]
    [FoldoutGroup("Presentation", Expanded = true), AssetsOnly, LabelText("Cast Cue")]
    public AudioCue castCue;

    [PropertyOrder(-36)]
    [FoldoutGroup("Presentation", Expanded = true), LabelText("Skill Clip")]
    [Tooltip("Per-skill Animancer transition. Set the clip plus Fade, Speed, Start Time, and optional transition events here.")]
    public ClipTransition skillClip;

    [PropertyOrder(-35)]
    [FoldoutGroup("Presentation", Expanded = true), LabelText("Cast Point"), Range(0f, 1f), SuffixLabel("normalized")]
    public float castPointNormalized = DefaultCastPointNormalized;

    [SerializeField, HideInInspector]
    private List<SkillVfxEvent> skillVfxEvents = new List<SkillVfxEvent>();

    [PropertyOrder(-26)]
    [FoldoutGroup("Feedback", Expanded = false), LabelText("HitLag Duration"), MinValue(0f), SuffixLabel("s")]
    [SerializeField] private float hitLagDuration = 0.06f;

    [PropertyOrder(-26)]
    [FoldoutGroup("Feedback", Expanded = false), LabelText("HitLag Time Scale"), Range(0.01f, 1f)]
    [SerializeField] private float hitLagTimeScale = 0.05f;

    [PropertyOrder(-26)]
    [FoldoutGroup("Feedback", Expanded = false), LabelText("HitLag Shape (optional)")]
    [SerializeField] private AnimationCurve hitLagShape = null;

    public float HitLagDuration => hitLagDuration;
    public float HitLagTimeScale => hitLagTimeScale;
    public AnimationCurve HitLagShape => hitLagShape;
    public bool HasHitLag => HasHitLagMarker();

    [PropertyOrder(-25)]
    [FoldoutGroup("Cutscene Skill", Expanded = false), LabelText("Is Cutscene Skill"), ToggleLeft]
    [SerializeField] private bool isCutsceneSkill;

    [PropertyOrder(-24)]
    [FoldoutGroup("Cutscene Skill", Expanded = false), ShowIf(nameof(isCutsceneSkill)), HideLabel]
    [SerializeField] private CutsceneDef cutsceneDef = new CutsceneDef();

    public bool IsCutsceneSkill => isCutsceneSkill;
    public CutsceneDef CutsceneDef => cutsceneDef;

    [PropertyOrder(-34)]
    [FoldoutGroup("Pre-Cast Block", Expanded = false), LabelText("Blockable"), ToggleLeft]
    [SerializeField] private bool blockablePreCast;

    [PropertyOrder(-33)]
    [FoldoutGroup("Pre-Cast Block", Expanded = false), LabelText("Open Event")]
    [SerializeField] private CombatTimelineEventName preCastOpenTimelineEvent = CombatTimelineEventName.PreCastOpen;

    [PropertyOrder(-32)]
    [FoldoutGroup("Pre-Cast Block", Expanded = false), LabelText("Close Event")]
    [SerializeField] private CombatTimelineEventName preCastCloseTimelineEvent = CombatTimelineEventName.PreCastClose;

    [PropertyOrder(-31)]
    [FoldoutGroup("Pre-Cast Block", Expanded = false), LabelText("Fallback Window"), ToggleLeft]
    [SerializeField] private bool useFallbackPreCastWindow = true;

    [PropertyOrder(-30)]
    [FoldoutGroup("Pre-Cast Block", Expanded = false), LabelText("Fallback Open"), Range(0f, 1f), SuffixLabel("normalized")]
    [SerializeField] private float fallbackPreCastOpenNormalized = 0f;

    [PropertyOrder(-29)]
    [FoldoutGroup("Pre-Cast Block", Expanded = false), LabelText("Fallback Close"), Range(0f, 1f), SuffixLabel("normalized")]
    [SerializeField] private float fallbackPreCastCloseNormalized = DefaultCastPointNormalized;

    [PropertyOrder(-28)]
    [FoldoutGroup("Pre-Cast Block", Expanded = false), AssetsOnly, LabelText("Indicator Prefab")]
    [SerializeField] private GameObject preCastIndicatorPrefab;

    [PropertyOrder(-27)]
    [FoldoutGroup("Pre-Cast Block", Expanded = false), LabelText("Cancel On Stun"), ToggleLeft]
    [SerializeField] private bool cancelPreCastOnStun = true;

    [PropertyOrder(-26)]
    [FoldoutGroup("Pre-Cast Block", Expanded = false), LabelText("Cancel On Stagger"), ToggleLeft]
    [SerializeField] private bool cancelPreCastOnStagger = true;

    [Serializable]
    public class LevelData
    {
        [HorizontalGroup("Row", Width = 70), LabelText("Req Lv"), MinValue(1)]
        public int requiredLevel = 1;

        [HorizontalGroup("Row"), LabelText("Has Override"), ReadOnly]
        [ShowInInspector]
        private bool HasOverride => HasAnyOverride();

        [HorizontalGroup("Row"), LabelText("Override Count"), ReadOnly]
        [ShowInInspector]
        private int OverrideCount => GetOverrideCount();

        [LabelText("Damage")]
        public float damage;

        [LabelText("Mana")]
        public float manaCost;

        [LabelText("Cast"), SuffixLabel("s")]
        public float castTime;

        [LabelText("Cooldown"), SuffixLabel("s")]
        public float cooldown;

        [LabelText("Radius"), SuffixLabel("m")]
        public float radius;

        [LabelText("Projectiles")]
        public int projectiles;

        [LabelText("Crit"), SuffixLabel("%")]
        public float critChance;

        public bool HasAnyOverride()
        {
            return !Mathf.Approximately(damage, 0f) ||
                   !Mathf.Approximately(manaCost, 0f) ||
                   !Mathf.Approximately(castTime, 0f) ||
                   !Mathf.Approximately(cooldown, 0f) ||
                   !Mathf.Approximately(radius, 0f) ||
                   projectiles != 0 ||
                   !Mathf.Approximately(critChance, 0f);
        }

        public int GetOverrideCount()
        {
            int count = 0;
            if (!Mathf.Approximately(damage, 0f)) count++;
            if (!Mathf.Approximately(manaCost, 0f)) count++;
            if (!Mathf.Approximately(castTime, 0f)) count++;
            if (!Mathf.Approximately(cooldown, 0f)) count++;
            if (!Mathf.Approximately(radius, 0f)) count++;
            if (projectiles != 0) count++;
            if (!Mathf.Approximately(critChance, 0f)) count++;
            return count;
        }
    }

    [PropertyOrder(-20)]
    [FoldoutGroup("Level Scaling", Expanded = true), TableList(AlwaysExpanded = true)]
    [LabelText("Per-Level Overrides")]
    [Tooltip("Runtime uses the highest required level with a real override. Rows with no override values still act as base-stat fallback markers.")]
    public List<LevelData> perLevelData = new();

    public IReadOnlyList<SkillVfxEvent> SkillVfxEvents => skillVfxEvents ?? (skillVfxEvents = new List<SkillVfxEvent>());
    public bool HasSkillVfxEvents => skillVfxEvents != null && skillVfxEvents.Count > 0;
    public bool RequiresSkillTimelineEvents =>
        (payload != null && payload.RequiresSkillTimelineEvents) || HasSkillVfxEvents;

    public void ReplaceSkillVfxEvents(List<SkillVfxEvent> events)
    {
        skillVfxEvents = events ?? new List<SkillVfxEvent>();
    }

    public void MoveSkillVfxCue(int oldCueIndex, int newCueIndex)
    {
        if (skillVfxEvents == null || oldCueIndex < 0 || newCueIndex < 0 || oldCueIndex == newCueIndex)
            return;

        for (int i = 0; i < skillVfxEvents.Count; i++)
        {
            SkillVfxEvent cue = skillVfxEvents[i];
            if (cue == null)
                continue;

            if (cue.cueIndex == oldCueIndex)
            {
                cue.cueIndex = newCueIndex;
            }
            else if (oldCueIndex < newCueIndex && cue.cueIndex > oldCueIndex && cue.cueIndex <= newCueIndex)
            {
                cue.cueIndex--;
            }
            else if (oldCueIndex > newCueIndex && cue.cueIndex >= newCueIndex && cue.cueIndex < oldCueIndex)
            {
                cue.cueIndex++;
            }
        }
    }

    public void RemoveSkillVfxCue(int cueIndex)
    {
        if (skillVfxEvents == null || cueIndex < 0)
            return;

        skillVfxEvents.RemoveAll(cue => cue != null && cue.cueIndex == cueIndex);
        for (int i = 0; i < skillVfxEvents.Count; i++)
        {
            SkillVfxEvent cue = skillVfxEvents[i];
            if (cue != null && cue.cueIndex > cueIndex)
                cue.cueIndex--;
        }
    }

    public int GetSkillVfxMarkerCount()
    {
        AnimancerEvent.Sequence events = skillClip?.Events;
        if (events == null)
            return 0;

        StringReference vfxEventName = CombatTimelineEventNames.ToStringReference(CombatTimelineEventName.Vfx);
        int count = 0;
        for (int i = 0; i < events.Count; i++)
        {
            if (events.GetName(i) == vfxEventName)
                count++;
        }

        return count;
    }

    public void CollectTimelineEventNames(List<CombatTimelineEventName> eventNames)
    {
        if (eventNames == null)
            return;

        payload?.CollectTimelineEventNames(eventNames);

        if (HasSkillVfxEvents)
            CombatTimelineEventNames.AddUnique(eventNames, CombatTimelineEventName.Vfx);

        if (HasShakeCameraMarker())
            CombatTimelineEventNames.AddUnique(eventNames, CombatTimelineEventName.ShakeCamera);

        if (HasHitLagMarker())
            CombatTimelineEventNames.AddUnique(eventNames, CombatTimelineEventName.HitLag);
    }

    bool HasShakeCameraMarker()
    {
        AnimancerEvent.Sequence events = skillClip?.Events;
        if (events == null)
            return false;

        StringReference shakeName =
            CombatTimelineEventNames.ToStringReference(CombatTimelineEventName.ShakeCamera);
        for (int i = 0; i < events.Count; i++)
        {
            if (events.GetName(i) == shakeName)
                return true;
        }

        return false;
    }

    bool HasHitLagMarker()
    {
        AnimancerEvent.Sequence events = skillClip?.Events;
        if (events == null)
            return false;

        StringReference hitLagName =
            CombatTimelineEventNames.ToStringReference(CombatTimelineEventName.HitLag);
        for (int i = 0; i < events.Count; i++)
        {
            if (events.GetName(i) == hitLagName)
                return true;
        }

        return false;
    }

    public void CollectSkillVfxValidationIssues(List<string> issues)
    {
        CollectSkillVfxValidationIssues(skillVfxEvents, GetSkillVfxMarkerCount(), issues);
    }

    public static void CollectSkillVfxValidationIssues(
        IReadOnlyList<SkillVfxEvent> events,
        List<string> issues)
    {
        CollectSkillVfxValidationIssues(events, -1, issues);
    }

    private static void CollectSkillVfxValidationIssues(
        IReadOnlyList<SkillVfxEvent> events,
        int markerCount,
        List<string> issues)
    {
        AnimationVfxValidation.CollectIssues(
            new SkillVfxCueSource(events),
            markerCount,
            "Skill Clip",
            issues);
    }

    [PropertyOrder(-19)]
    [SerializeField, FoldoutGroup("Level Scaling", Expanded = true), BoxGroup("Level Scaling/Effective Preview"), LabelText("Selected Level"), MinValue(1)]
    private int previewLevel = 1;

    [PropertyOrder(-18)]
    [ShowInInspector, ReadOnly, FoldoutGroup("Level Scaling", Expanded = true), BoxGroup("Level Scaling/Effective Preview"), LabelText("Effective At Selected Level")]
    private string EffectivePreviewLabel => BuildEffectivePreview(previewLevel);

    [PropertyOrder(-17)]
    [ShowInInspector, ReadOnly, FoldoutGroup("Level Scaling", Expanded = true), BoxGroup("Level Scaling/Effective Preview"), LabelText("Delta From Base")]
    private string DeltaPreviewLabel => BuildDeltaPreview(previewLevel);

    [PropertyOrder(200)]
    [ShowInInspector, ReadOnly, FoldoutGroup("Tools", Expanded = true), LabelText("Authoring")]
    private string ToolingSummary =>
        "Use these helpers to sort level rows and normalize cast timing. Execution data is owned by the embedded payload.";

    [PropertyOrder(201)]
    [FoldoutGroup("Tools", Expanded = true), Button("Sort Level Rows")]
    private void SortLevelRows()
    {
        if (perLevelData == null || perLevelData.Count <= 1)
            return;

        perLevelData.Sort((left, right) =>
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return 1;
            if (right == null)
                return -1;
            return left.requiredLevel.CompareTo(right.requiredLevel);
        });

        MarkDirty(this);
    }

    [PropertyOrder(202)]
    [FoldoutGroup("Tools", Expanded = true), Button("Remove Empty Level Rows"), ShowIf(nameof(HasEmptyOverrideRows))]
    private void RemoveEmptyLevelRows()
    {
        if (perLevelData == null || perLevelData.Count == 0)
            return;

        perLevelData.RemoveAll(entry => entry == null || !entry.HasAnyOverride());
        MarkDirty(this);
    }

    [PropertyOrder(203)]
    [FoldoutGroup("Tools", Expanded = true), Button("Normalize Cast Point")]
    private void NormalizeCastPoint()
    {
        castPointNormalized = GetCastPointNormalized();
        MarkDirty(this);
    }

    private bool HasSkillId()
    {
        return !string.IsNullOrWhiteSpace(skillId);
    }

    private bool HasEmptyOverrideRows => EmptyOverrideRowCount > 0;
    private int LevelRowCount => perLevelData != null ? perLevelData.Count : 0;

    private int DuplicateLevelRowCount
    {
        get
        {
            if (perLevelData == null || perLevelData.Count == 0)
                return 0;

            var counts = new Dictionary<int, int>();
            int duplicates = 0;

            for (int i = 0; i < perLevelData.Count; i++)
            {
                LevelData entry = perLevelData[i];
                if (entry == null)
                    continue;

                int requiredLevel = Mathf.Max(1, entry.requiredLevel);
                if (!counts.TryGetValue(requiredLevel, out int count))
                {
                    counts.Add(requiredLevel, 1);
                    continue;
                }

                count++;
                counts[requiredLevel] = count;
                if (count > 1)
                    duplicates++;
            }

            return duplicates;
        }
    }

    private int EmptyOverrideRowCount
    {
        get
        {
            if (perLevelData == null || perLevelData.Count == 0)
                return 0;

            int count = 0;
            for (int i = 0; i < perLevelData.Count; i++)
            {
                LevelData entry = perLevelData[i];
                if (entry == null || !entry.HasAnyOverride())
                    count++;
            }

            return count;
        }
    }

    private string BlockingErrorSummary
    {
        get
        {
            var issues = new List<string>();

            if (!HasSkillId())
                issues.Add("Skill ID is required.");

            if (payload == null)
                issues.Add("Execution payload is required.");
            else
                payload.CollectValidationIssues(issues);

            CollectSkillVfxValidationIssues(issues);

#if UNITY_EDITOR
            if (payload != null && !IsPayloadEmbedded())
                issues.Add("Execution payload must be embedded in the same asset as this skill.");
            else if (payload != null)
            {
                int embeddedPayloadCount = GetEmbeddedPayloadCount();
                if (embeddedPayloadCount != 1)
                    issues.Add($"Skill asset must contain exactly one embedded payload, but found {embeddedPayloadCount}.");
            }
#endif

            return string.Join("\n", issues);
        }
    }

    private string WarningSummary
    {
        get
        {
            var warnings = new List<string>();

            if (AreaofEffec && !HasAnyRadiusConfigured)
                warnings.Add("Area Of Effect is enabled, but base radius and all level overrides currently resolve to 0.");

            if (skillClip != null && (castPointNormalized < WarningCastPointMin || castPointNormalized > WarningCastPointMax))
                warnings.Add("Cast Point is technically valid, but sits in an extreme range that is easy to mistime in animation-driven skills.");

            if (ProjectilePayload != null && ProjectilePayload.HasHitVfxScaleWithoutHitVfx)
                warnings.Add("Hit VFX Scale is set, but there is no projectile hit VFX assigned.");

            if (DuplicateLevelRowCount > 0)
                warnings.Add($"Per-level data has {DuplicateLevelRowCount} duplicate required level entries.");

            return string.Join("\n", warnings);
        }
    }

    private string InfoSummary
    {
        get
        {
            var notes = new List<string>();

            if (EmptyOverrideRowCount > 0)
                notes.Add($"{EmptyOverrideRowCount} per-level rows have no override values and only serve as base-stat fallback markers.");

#if UNITY_EDITOR
            if (payload != null && IsPayloadEmbedded())
                notes.Add("Execution payload is embedded and owned by this skill asset.");
#endif

            return string.Join("\n", notes);
        }
    }

    private SkillConfigStatus GetConfigStatus()
    {
        if (!string.IsNullOrEmpty(BlockingErrorSummary))
            return SkillConfigStatus.Error;
        if (!string.IsNullOrEmpty(WarningSummary))
            return SkillConfigStatus.Warning;
        return SkillConfigStatus.Valid;
    }

    private string BuildRuntimeSummaryLine()
    {
        string projectileLabel = HasProjectileExecutionIntent ? "Projectile" : "Non-Projectile";
        string aoeLabel = AreaofEffec && HasAnyRadiusConfigured ? "AoE" : "Single Target";
        string execLabel = GetResolvedRuntimeModeLabel();
        string presentationLabel = GetPresentationStatusLabel();

        return $"{projectileLabel} / {aoeLabel} / Max Lv {Mathf.Max(1, maxLevel)} / Exec: {execLabel} / Presentation: {presentationLabel}";
    }

    private string GetResolvedRuntimeModeLabel()
    {
        if (payload == null)
            return "Invalid";

        if (HasProjectilePayload)
        {
            if (ProjectilePayload.HasExplicitProjectilePrefab)
                return "Payload -> Projectile";

            return "Payload -> Projectile (Invalid)";
        }

        return $"Payload -> {FormatPayloadTypeName(payload)}";
    }

    private string GetProjectileOwnershipLabel()
    {
        if (!HasProjectileExecutionIntent)
            return "Not a projectile execution path";

        if (HasProjectilePayload)
        {
            if (ProjectilePayload.HasExplicitProjectilePrefab)
                return "Payload owns projectile prefab";

            return "Projectile owner missing";
        }

        return "Projectile owner missing";
    }

    private string GetPresentationStatusLabel()
    {
        if (!HasAnyPresentationAssets)
            return "Minimal";

        if (HasAnimationPresentationAssets && (!HasProjectileExecutionIntent || HasProjectilePresentationAssets))
            return "Configured";

        return "Partial";
    }

    private bool HasRadiusOverride()
    {
        if (perLevelData == null || perLevelData.Count == 0)
            return false;

        for (int i = 0; i < perLevelData.Count; i++)
        {
            LevelData entry = perLevelData[i];
            if (entry != null && entry.radius > 0f)
                return true;
        }

        return false;
    }

    private FinalSkillStats BuildPreviewStats(int level)
    {
        int clampedLevel = ClampLevel(level);
        var stats = new FinalSkillStats
        {
            damage = baseDamage,
            areaRadius = baseRadius,
            projectileCount = baseProjectilesCount,
            manaCost = baseManaCost,
            castTime = baseCastTime,
            cooldown = baseCooldown,
            staggerPower = baseStaggerPower,
            critChance = baseCritChance,
            critMultiplier = 2f,
        };

        ApplyLevelData(stats, clampedLevel);
        stats.projectileCount = Mathf.Max(1, stats.projectileCount);
        stats.areaRadius = Mathf.Max(0f, stats.areaRadius);
        stats.manaCost = Mathf.Max(0f, stats.manaCost);
        stats.castTime = Mathf.Max(0f, stats.castTime);
        stats.cooldown = Mathf.Max(0f, stats.cooldown);
        stats.critChance = Mathf.Clamp(stats.critChance, 0f, 100f);
        return stats;
    }

    private string BuildEffectivePreview(int level)
    {
        FinalSkillStats stats = BuildPreviewStats(level);
        int clampedLevel = ClampLevel(level);
        return $"Lv {clampedLevel}: Damage {stats.damage:0.##}, Mana {stats.manaCost:0.##}, Cast {stats.castTime:0.##}s, Cooldown {stats.cooldown:0.##}s, Radius {stats.areaRadius:0.##}m, Projectiles {stats.projectileCount}, Crit {stats.critChance:0.##}%";
    }

    private string BuildDeltaPreview(int level)
    {
        FinalSkillStats stats = BuildPreviewStats(level);
        return $"Damage {FormatSigned(stats.damage - baseDamage)}, Mana {FormatSigned(stats.manaCost - baseManaCost)}, Cast {FormatSigned(stats.castTime - baseCastTime)}s, Cooldown {FormatSigned(stats.cooldown - baseCooldown)}s, Radius {FormatSigned(stats.areaRadius - baseRadius)}m, Projectiles {FormatSigned(stats.projectileCount - baseProjectilesCount)}, Crit {FormatSigned(stats.critChance - baseCritChance)}%";
    }

    private static string FormatSigned(float value)
    {
        return value >= 0f ? $"+{value:0.##}" : value.ToString("0.##");
    }

    private static string FormatSigned(int value)
    {
        return value >= 0 ? $"+{value}" : value.ToString();
    }

    private static string FormatSkillId(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "Missing" : value.Trim();
    }

    private static string FormatTags(SkillTag value)
    {
        return value == SkillTag.None ? "None" : value.ToString();
    }

    private static string FormatPayloadTypeName(SkillPayloadDef value)
    {
        if (value == null)
            return "None";

        string name = value.GetType().Name;
        string[] suffixes = { "SkillPayloadDef", "PayloadDef", "Definition", "Def" };
        for (int i = 0; i < suffixes.Length; i++)
        {
            string suffix = suffixes[i];
            if (name.EndsWith(suffix, StringComparison.Ordinal))
            {
                name = name.Substring(0, name.Length - suffix.Length);
                break;
            }
        }

        if (string.IsNullOrEmpty(name))
            return value.name;

        var builder = new StringBuilder(name.Length + 8);
        for (int i = 0; i < name.Length; i++)
        {
            char current = name[i];
            if (i > 0 && char.IsUpper(current) && !char.IsWhiteSpace(name[i - 1]))
                builder.Append(' ');

            builder.Append(current);
        }

        return builder.ToString();
    }

#if UNITY_EDITOR
    private bool IsPayloadEmbedded()
    {
        if (payload == null || !AssetDatabase.IsSubAsset(payload))
            return false;

        string skillPath = AssetDatabase.GetAssetPath(this);
        return !string.IsNullOrEmpty(skillPath) &&
               string.Equals(skillPath, AssetDatabase.GetAssetPath(payload), StringComparison.OrdinalIgnoreCase);
    }

    private int GetEmbeddedPayloadCount()
    {
        string skillPath = AssetDatabase.GetAssetPath(this);
        if (string.IsNullOrEmpty(skillPath))
            return 0;

        int count = 0;
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(skillPath);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is SkillPayloadDef)
                count++;
        }

        return count;
    }
#endif

    private static void MarkDirty(UnityEngine.Object target)
    {
#if UNITY_EDITOR
        if (target != null)
            EditorUtility.SetDirty(target);
#endif
    }

    public int ClampLevel(int level)
    {
        return Mathf.Clamp(level, 1, Mathf.Max(1, maxLevel));
    }

    public LevelData GetLevelData(int level)
    {
        if (perLevelData == null || perLevelData.Count == 0)
            return null;

        int clampedLevel = ClampLevel(level);
        LevelData bestMatch = null;
        int bestRequiredLevel = int.MinValue;

        for (int i = 0; i < perLevelData.Count; i++)
        {
            LevelData entry = perLevelData[i];
            if (entry == null)
                continue;

            if (!entry.HasAnyOverride())
                continue;

            int requiredLevel = Mathf.Max(1, entry.requiredLevel);
            if (requiredLevel > clampedLevel)
                continue;

            if (bestMatch != null && requiredLevel <= bestRequiredLevel)
                continue;

            bestMatch = entry;
            bestRequiredLevel = requiredLevel;
        }

        if (bestMatch != null)
            return bestMatch;

        int index = Mathf.Clamp(clampedLevel - 1, 0, perLevelData.Count - 1);
        return perLevelData[index];
    }

    public void ApplyLevelData(FinalSkillStats stats, int level)
    {
        if (stats == null)
            return;

        LevelData levelData = GetLevelData(level);
        if (levelData == null)
            return;

        stats.damage = levelData.damage;
        stats.manaCost = levelData.manaCost;
        stats.castTime = levelData.castTime;
        stats.cooldown = levelData.cooldown;
        stats.areaRadius = levelData.radius;
        stats.projectileCount = levelData.projectiles;
        stats.critChance = levelData.critChance;
    }

    public float GetCastPointNormalized()
    {
        if (!float.IsFinite(castPointNormalized))
            return DefaultCastPointNormalized;

        return Mathf.Clamp(castPointNormalized, 0f, 0.999f);
    }

    public bool BlockablePreCast => blockablePreCast;
    public bool UseFallbackPreCastWindow => blockablePreCast && useFallbackPreCastWindow;
    public CombatTimelineEventName PreCastOpenEventName => preCastOpenTimelineEvent;
    public CombatTimelineEventName PreCastCloseEventName => preCastCloseTimelineEvent;
    public GameObject PreCastIndicatorPrefab => preCastIndicatorPrefab;
    public bool CancelPreCastOnStun => cancelPreCastOnStun;
    public bool CancelPreCastOnStagger => cancelPreCastOnStagger;

    public float FallbackPreCastOpenNormalized
    {
        get
        {
            if (!float.IsFinite(fallbackPreCastOpenNormalized))
                return 0f;

            return Mathf.Clamp(fallbackPreCastOpenNormalized, 0f, 0.999f);
        }
    }

    public float FallbackPreCastCloseNormalized
    {
        get
        {
            float close = float.IsFinite(fallbackPreCastCloseNormalized)
                ? Mathf.Clamp(fallbackPreCastCloseNormalized, 0f, 0.999f)
                : GetCastPointNormalized();

            float open = FallbackPreCastOpenNormalized;
            if (close <= open)
                close = Mathf.Max(open, GetCastPointNormalized());

            return Mathf.Clamp(close, 0f, 0.999f);
        }
    }

    public bool IsPreCastOpenEvent(CombatTimelineEventName eventName)
    {
        return eventName == PreCastOpenEventName;
    }

    public bool IsPreCastCloseEvent(CombatTimelineEventName eventName)
    {
        return eventName == PreCastCloseEventName;
    }

    public void CollectPreCastTimelineEventNames(List<CombatTimelineEventName> eventNames)
    {
        if (!blockablePreCast)
            return;

        CombatTimelineEventNames.AddUnique(eventNames, PreCastOpenEventName);
        CombatTimelineEventNames.AddUnique(eventNames, PreCastCloseEventName);
    }

    public ChainStepContinueMode GetChainContinueMode()
    {
        return payload != null
            ? payload.GetChainContinueMode()
            : ChainStepContinueMode.OnStepComplete;
    }

    public float GetChainContinueNormalizedTime()
    {
        return payload != null
            ? payload.GetChainContinueNormalizedTime()
            : 1f;
    }

}
