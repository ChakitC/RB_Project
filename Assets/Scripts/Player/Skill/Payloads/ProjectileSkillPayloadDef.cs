using Sirenix.OdinInspector;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[HideMonoScript]
[CreateAssetMenu(fileName = "Projectile Skill Payload", menuName = "Game/Skill Payload/Projectile")]
public class ProjectileSkillPayloadDef : SkillPayloadDef
{
    [PropertyOrder(-30)]
    [ShowInInspector, ReadOnly, BoxGroup("Payload Summary"), LabelText("Payload")]
    private string PayloadSummary => "Projectile Payload";

    [PropertyOrder(-29)]
    [ShowInInspector, ReadOnly, BoxGroup("Payload Summary"), LabelText("Ownership")]
    private string OwnershipSummary => projectilePrefab != null
        ? "Projectile prefab is owned here."
        : "Projectile prefab is missing on this payload.";

    [PropertyOrder(-28)]
    [ShowInInspector, ReadOnly, BoxGroup("Payload Summary"), LabelText("Config Status")]
    private string ConfigStatusLabel => projectilePrefab != null ? "Valid" : "Error";

    [PropertyOrder(-27)]
    [ShowInInspector, ReadOnly, BoxGroup("Payload Summary"), LabelText("Legacy Fallback")]
    private string FallbackBehaviorLabel => "Disabled";

    [PropertyOrder(-20)]
    [InfoBox("Projectile execution is self-contained. Assign the projectile prefab here; root-level fallback is no longer used.", InfoMessageType.Info)]
    [SerializeField, BoxGroup("Execution"), AssetsOnly, PreviewField(70, ObjectFieldAlignment.Left)]
    [LabelText("Projectile Prefab (Owner)")]
    private Projectile projectilePrefab;

    [PropertyOrder(-19)]
    [SerializeField, BoxGroup("Execution"), AssetsOnly]
    [LabelText("Projectile Config Override")]
    private ProjectileConfig projectileConfigOverride;

    [PropertyOrder(-18)]
    [SerializeField, BoxGroup("Execution"), LabelText("Spread Angle"), MinValue(0f), SuffixLabel("deg")]
    private float spreadAngle = 10f;

    [PropertyOrder(-17)]
    [ShowInInspector, ReadOnly, BoxGroup("Execution"), LabelText("Resolution Rule")]
    private string ResolutionRuleLabel =>
        "Explicit payload prefab only";

    [PropertyOrder(-16)]
    [SerializeField, BoxGroup("Presentation"), AssetsOnly, PreviewField(70, ObjectFieldAlignment.Left)]
    [LabelText("Projectile Trail VFX")]
    private GameObject projectileTrailVfxPrefab;

    [PropertyOrder(-15)]
    [SerializeField, BoxGroup("Presentation"), AssetsOnly, PreviewField(70, ObjectFieldAlignment.Left)]
    [LabelText("Projectile Hit VFX")]
    private GameObject projectileHitVfxPrefab;

    [PropertyOrder(-14)]
    [SerializeField, BoxGroup("Presentation"), MinValue(0.01f)]
    [LabelText("Hit VFX Scale")]
    private float projectileHitVfxScale = 1f;

    public bool HasExplicitProjectilePrefab => projectilePrefab != null;
    public override bool HasExecutionPresentationAssets => projectileTrailVfxPrefab != null || projectileHitVfxPrefab != null;
    public bool HasProjectilePresentationAssets => HasExecutionPresentationAssets;
    public bool HasHitVfxScaleWithoutHitVfx => projectileHitVfxPrefab == null && !Mathf.Approximately(projectileHitVfxScale, 1f);
    public GameObject ProjectileTrailVfxPrefab => projectileTrailVfxPrefab;
    public GameObject ProjectileHitVfxPrefab => projectileHitVfxPrefab;
    public float ProjectileHitVfxScale => Mathf.Max(0.01f, projectileHitVfxScale);

    public bool HasResolvableProjectilePrefab()
    {
        return projectilePrefab != null;
    }

    public Projectile GetResolvedProjectilePrefab()
    {
        return projectilePrefab;
    }

    public void AssignMigratedProjectilePrefab(Projectile prefab)
    {
        if (prefab == null)
            return;

        projectilePrefab = prefab;
        MarkDirty(this);
    }

    public override void Execute(SkillCastContext context)
    {
        if (context == null || context.User == null || context.CastOrigin == null)
            return;

        Projectile prefab = GetResolvedProjectilePrefab();
        if (prefab == null)
        {
            Debug.LogError(
                $"Skill '{context.SkillDef?.name ?? name}' has no projectile prefab configured on its payload.",
                this);
            return;
        }

        int projectileCount = context.SkillStats != null
            ? Mathf.Max(1, context.SkillStats.projectileCount)
            : 1;

        for (int i = 0; i < projectileCount; i++)
        {
            Vector3 dir = ComputeDirection(context.AimDirection, i, projectileCount);
            GameObject projectileObject = Object.Instantiate(
                prefab.gameObject,
                context.CastOrigin.position,
                Quaternion.LookRotation(dir, Vector3.up));

            Projectile projectileInstance = projectileObject.GetComponent<Projectile>();
            if (projectileInstance == null)
            {
                Object.Destroy(projectileObject);
                continue;
            }

            projectileInstance.InitFromSkillExecution(
                projectileConfigOverride != null ? projectileConfigOverride : projectileInstance.config,
                context.User,
                context.SkillDef,
                this,
                context.SkillStats,
                dir,
                prefab);
        }
    }

    private Vector3 ComputeDirection(Vector3 baseDirection, int projectileIndex, int projectileCount)
    {
        Vector3 dir = baseDirection.sqrMagnitude > 0.0001f
            ? baseDirection.normalized
            : Vector3.forward;

        if (projectileCount <= 1)
            return dir;

        float t = (float)projectileIndex / (projectileCount - 1) - 0.5f;
        float angle = t * spreadAngle;
        return (Quaternion.AngleAxis(angle, Vector3.up) * dir).normalized;
    }

    private static void MarkDirty(Object target)
    {
#if UNITY_EDITOR
        if (target != null)
            EditorUtility.SetDirty(target);
#endif
    }
}
