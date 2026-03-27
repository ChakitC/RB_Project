using UnityEngine;

[CreateAssetMenu(fileName = "Projectile Skill Payload", menuName = "Game/Skill Payload/Projectile")]
public class ProjectileSkillPayloadDef : SkillPayloadDef
{
    [SerializeField] private Projectile projectilePrefab;
    [SerializeField] private ProjectileConfig projectileConfigOverride;
    [SerializeField, Min(0f)] private float spreadAngle = 10f;

    public override void Execute(SkillCastContext context)
    {
        if (context == null || context.User == null || context.CastOrigin == null)
            return;

        var prefab = ResolveProjectilePrefab(context);
        if (prefab == null)
        {
            Debug.LogError($"Skill '{context.SkillDef?.name ?? name}' has no projectile prefab configured.", this);
            return;
        }

        int projectileCount = context.SkillStats != null
            ? Mathf.Max(1, context.SkillStats.projectileCount)
            : 1;

        for (int i = 0; i < projectileCount; i++)
        {
            Vector3 dir = ComputeDirection(context.AimDirection, i, projectileCount);
            var projectileObject = Object.Instantiate(
                prefab.gameObject,
                context.CastOrigin.position,
                Quaternion.LookRotation(dir, Vector3.up));

            var projectileInstance = projectileObject.GetComponent<Projectile>();
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

    Projectile ResolveProjectilePrefab(SkillCastContext context)
    {
        if (projectilePrefab != null)
            return projectilePrefab;

        if (context != null && context.SkillDef != null && context.SkillDef.skillPrefab != null)
            return context.SkillDef.skillPrefab.GetComponent<Projectile>();

        return null;
    }

    Vector3 ComputeDirection(Vector3 baseDirection, int projectileIndex, int projectileCount)
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
}
