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
[CreateAssetMenu(
    fileName = "NewSkillGem",
    menuName = "Game/Skill Gem"
)]
public class SkillGemDefinition : ScriptableObject
{
    private const float DefaultCastPointNormalized = 0.35f;
    private const float WarningCastPointMin = 0.05f;
    private const float WarningCastPointMax = 0.95f;

    public enum HelperFacingMode
    {
        KeepCurrentFacing = 0,
        FaceDetectedTargetOnCast = 1,
    }

    private enum SkillConfigStatus
    {
        Valid,
        Warning,
        Error,
    }

    private ProjectileSkillPayloadDef ProjectilePayload => payload as ProjectileSkillPayloadDef;
    private bool HasProjectilePayload => ProjectilePayload != null;
    private bool HasLegacyProjectilePrefab => skillPrefab != null;
    private bool CanLegacyPrefabAffectRuntime => payload == null || HasProjectilePayload;
    private bool LegacyPrefabHasProjectileComponent =>
        skillPrefab != null && skillPrefab.GetComponent<Projectile>() != null;
    private bool HasProjectileExecutionIntent => HasProjectilePayload || (payload == null && HasLegacyProjectilePrefab);
    private bool HasAnyRadiusConfigured => baseRadius > 0f || HasRadiusOverride();
    private bool HasProjectilePresentationAssets => BallVfxPrefab != null || SkillVfxhit != null;
    private bool HasAnimationPresentationAssets => castCue != null || skillClip != null;
    private bool HasAnyPresentationAssets => HasProjectilePresentationAssets || HasAnimationPresentationAssets;
    private bool UsesLegacyProjectilePath => payload == null && LegacyPrefabHasProjectileComponent;
    private bool UsesProjectilePayloadFallback =>
        HasProjectilePayload &&
        !ProjectilePayload.HasExplicitProjectilePrefab &&
        LegacyPrefabHasProjectileComponent;
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

    [PropertyOrder(-59)]
    [ShowInInspector, ReadOnly, FoldoutGroup("Execution", Expanded = true), LabelText("Fallback Source")]
    private string FallbackExecutionSourceLabel => CanLegacyPrefabAffectRuntime && HasLegacyProjectilePrefab
        ? "Legacy / Migration Prefab"
        : "None";

    [PropertyOrder(-58)]
    [ShowInInspector, ReadOnly, FoldoutGroup("Execution", Expanded = true), LabelText("Resolved Runtime Mode")]
    private string ResolvedRuntimeModeLabel => GetResolvedRuntimeModeLabel();

    [PropertyOrder(-57)]
    [ShowInInspector, ReadOnly, FoldoutGroup("Execution", Expanded = true), LabelText("Projectile Ownership")]
    private string ProjectileOwnershipLabel => GetProjectileOwnershipLabel();

    [PropertyOrder(-56)]
    [FoldoutGroup("Execution", Expanded = true), AssetsOnly, InlineEditor, LabelText("Payload Asset")]
    public SkillPayloadDef payload;

    [PropertyOrder(-55)]
    [FoldoutGroup("Execution", Expanded = true), AssetsOnly, PreviewField(70, ObjectFieldAlignment.Left), LabelText("Legacy / Migration Prefab")]
    [ValidateInput(nameof(IsLegacyPrefabContextuallyValid), "When this field can affect runtime, it must contain a Projectile component.")]
    public GameObject skillPrefab;

    [PropertyOrder(-54)]
    [FoldoutGroup("Execution", Expanded = true), LabelText("Helper Facing")]
    [Tooltip("Used by AllyHelperManager only. Keep Current Facing for helper skills that should preserve their summon/animation direction. Face Detected Target On Cast rotates the helper toward its current detected target right before the skill releases.")]
    public HelperFacingMode helperFacingMode = HelperFacingMode.KeepCurrentFacing;

    [PropertyOrder(-40)]
    [FoldoutGroup("Presentation", Expanded = true), AssetsOnly, PreviewField(70, ObjectFieldAlignment.Left), LabelText("Projectile Trail VFX")]
    public GameObject BallVfxPrefab;

    [PropertyOrder(-39)]
    [FoldoutGroup("Presentation", Expanded = true), AssetsOnly, PreviewField(70, ObjectFieldAlignment.Left), LabelText("Projectile Hit VFX")]
    public GameObject SkillVfxhit;

    [PropertyOrder(-38)]
    [FoldoutGroup("Presentation", Expanded = true), LabelText("Hit VFX Scale"), MinValue(0.01f)]
    public float projectileHitVfxScale = 1f;

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

    [PropertyOrder(-34)]
    [FoldoutGroup("Presentation", Expanded = true), LabelText("Chain Continue")]
    public ChainStepContinueMode chainContinueMode = ChainStepContinueMode.OnStepComplete;

    [PropertyOrder(-33)]
    [FoldoutGroup("Presentation", Expanded = true), LabelText("Chain Continue Time"), Range(0f, 1f), SuffixLabel("normalized")]
    public float chainContinueNormalizedTime = 1f;

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
    [ShowInInspector, ReadOnly, FoldoutGroup("Tools", Expanded = true), LabelText("Migration")]
    private string ToolingSummary =>
        "Use these helpers to sort level rows, normalize cast timing, and move projectile prefab ownership into ProjectileSkillPayloadDef without removing runtime fallback yet.";

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

    [PropertyOrder(204)]
    [FoldoutGroup("Tools", Expanded = true), Button("Move Legacy Prefab To Projectile Payload"), ShowIf(nameof(CanMigrateLegacyPrefabToPayload))]
    private void MoveLegacyPrefabToProjectilePayload()
    {
        if (ProjectilePayload == null || skillPrefab == null)
            return;

        Projectile projectile = skillPrefab.GetComponent<Projectile>();
        if (projectile == null)
            return;

        ProjectilePayload.AssignMigratedProjectilePrefab(projectile);
        MarkDirty(this);
        MarkDirty(ProjectilePayload);
    }

    [PropertyOrder(205)]
    [FoldoutGroup("Tools", Expanded = true), Button("Clear Legacy Prefab After Migration"), ShowIf(nameof(CanClearLegacyPrefabAfterMigration))]
    private void ClearLegacyPrefabAfterMigration()
    {
        skillPrefab = null;
        MarkDirty(this);
    }

    private bool HasSkillId()
    {
        return !string.IsNullOrWhiteSpace(skillId);
    }

    private bool IsLegacyPrefabContextuallyValid()
    {
        if (skillPrefab == null || !CanLegacyPrefabAffectRuntime)
            return true;

        return LegacyPrefabHasProjectileComponent;
    }

    private bool CanMigrateLegacyPrefabToPayload =>
        HasProjectilePayload &&
        HasLegacyProjectilePrefab &&
        LegacyPrefabHasProjectileComponent &&
        !ProjectilePayload.HasExplicitProjectilePrefab;

    private bool CanClearLegacyPrefabAfterMigration =>
        HasProjectilePayload &&
        ProjectilePayload.HasExplicitProjectilePrefab &&
        HasLegacyProjectilePrefab;

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

            if (payload == null && skillPrefab == null)
                issues.Add("No execution path is configured. Assign a payload or a legacy projectile prefab.");

            if (payload == null && skillPrefab != null && !LegacyPrefabHasProjectileComponent)
                issues.Add("Legacy / migration prefab is configured, but it has no Projectile component for the legacy path.");

            if (HasProjectilePayload && !ProjectilePayload.HasResolvableProjectilePrefab(this))
                issues.Add("Projectile payload has no projectile prefab. Assign one on the payload or keep a valid legacy migration prefab until migrated.");

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

            if (HasProjectileExecutionIntent && SkillVfxhit == null && !Mathf.Approximately(projectileHitVfxScale, 1f))
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

            if (UsesProjectilePayloadFallback)
                notes.Add("Projectile payload is currently resolving through the legacy prefab fallback. Move ownership into the payload when ready.");
            else if (UsesLegacyProjectilePath)
                notes.Add("This skill is still using the legacy projectile path because no payload is assigned.");

            if (EmptyOverrideRowCount > 0)
                notes.Add($"{EmptyOverrideRowCount} per-level rows have no override values and only serve as base-stat fallback markers.");

            if (CanClearLegacyPrefabAfterMigration && !UsesProjectilePayloadFallback)
                notes.Add("Projectile payload already owns its prefab. The legacy prefab can be cleared after you confirm no migration fallback is needed.");

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
        string legacyLabel = GetLegacyUsageLabel();

        return $"{projectileLabel} / {aoeLabel} / Max Lv {Mathf.Max(1, maxLevel)} / Exec: {execLabel} / Presentation: {presentationLabel} / Legacy: {legacyLabel}";
    }

    private string GetResolvedRuntimeModeLabel()
    {
        if (payload == null)
        {
            if (!HasLegacyProjectilePrefab)
                return "Invalid";

            return LegacyPrefabHasProjectileComponent
                ? "Legacy Projectile"
                : "Invalid (Legacy Prefab Missing Projectile)";
        }

        if (HasProjectilePayload)
        {
            if (ProjectilePayload.HasExplicitProjectilePrefab)
                return "Payload -> Projectile (Explicit)";

            return LegacyPrefabHasProjectileComponent
                ? "Payload -> Projectile (Legacy Fallback)"
                : "Payload -> Projectile (Invalid)";
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

            return LegacyPrefabHasProjectileComponent
                ? "Payload is waiting for migration; legacy prefab is still supplying the projectile"
                : "Projectile owner missing";
        }

        return LegacyPrefabHasProjectileComponent
            ? "Legacy prefab still owns projectile source"
            : "Projectile owner missing";
    }

    private string GetPresentationStatusLabel()
    {
        if (!HasAnyPresentationAssets)
            return "Minimal";

        if (HasAnimationPresentationAssets && (!HasProjectileExecutionIntent || HasProjectilePresentationAssets))
            return "Configured";

        return "Partial";
    }

    private string GetLegacyUsageLabel()
    {
        if (UsesProjectilePayloadFallback)
            return "Fallback";

        if (UsesLegacyProjectilePath)
            return "Primary";

        if (HasLegacyProjectilePrefab)
            return "Dormant";

        return "None";
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

    public ChainStepContinueMode GetChainContinueMode()
    {
        return chainContinueMode;
    }

    public float GetChainContinueNormalizedTime()
    {
        if (!float.IsFinite(chainContinueNormalizedTime))
            return 1f;

        return Mathf.Clamp(chainContinueNormalizedTime, 0f, 0.999f);
    }
}
