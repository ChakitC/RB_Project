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
        : "Projectile prefab owner is still unresolved here. Runtime will use SkillGemDefinition.skillPrefab as a migration fallback if available.";

    [PropertyOrder(-28)]
    [ShowInInspector, ReadOnly, BoxGroup("Payload Summary"), LabelText("Config Status")]
    private string ConfigStatusLabel => projectilePrefab != null ? "Valid" : "Migration Warning";

    [PropertyOrder(-27)]
    [ShowInInspector, ReadOnly, BoxGroup("Payload Summary"), LabelText("Fallback Behavior")]
    private string FallbackBehaviorLabel => projectilePrefab != null
        ? "No fallback required."
        : "Falls back to SkillGemDefinition.skillPrefab when it has a Projectile component.";

    [PropertyOrder(-20)]
    [InfoBox("Projectile prefab ownership should live on this payload. Leaving it empty keeps the legacy SkillGemDefinition prefab fallback active for migration only.", InfoMessageType.Info)]
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
        "Explicit payload prefab -> legacy SkillGemDefinition prefab fallback -> invalid";

    public bool HasExplicitProjectilePrefab => projectilePrefab != null;

    public bool HasResolvableProjectilePrefab(SkillGemDefinition skillDef)
    {
        return GetResolvedProjectilePrefab(skillDef) != null;
    }

    public bool UsesLegacyFallback(SkillGemDefinition skillDef)
    {
        return projectilePrefab == null &&
               skillDef != null &&
               skillDef.skillPrefab != null &&
               skillDef.skillPrefab.GetComponent<Projectile>() != null;
    }

    public Projectile GetResolvedProjectilePrefab(SkillGemDefinition skillDef)
    {
        if (projectilePrefab != null)
            return projectilePrefab;

        if (skillDef != null && skillDef.skillPrefab != null)
            return skillDef.skillPrefab.GetComponent<Projectile>();

        return null;
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

        Projectile prefab = GetResolvedProjectilePrefab(context.SkillDef);
        if (prefab == null)
        {
            Debug.LogError(
                $"Skill '{context.SkillDef?.name ?? name}' has no projectile prefab configured on its payload, and no legacy projectile fallback is available.",
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

            projectileInstance.InitFromSkillDef(
                projectileConfigOverride != null ? projectileConfigOverride : projectileInstance.config,
                context.User,
                context.SkillDef,
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
