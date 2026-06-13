using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

[HideMonoScript]
public class SpawnPickupSkillPayloadDef : SkillPayloadDef
{
    private bool UsesFixedSpawnCount => !useSkillProjectileCount;

    [PropertyOrder(-10)]
    [InfoBox("Spawns pickup prefabs in front of the caster. You can use a fixed count or read the count from the skill's projectile stat.")]
    [SerializeField, BoxGroup("Setup"), AssetsOnly, Required, PreviewField(70, ObjectFieldAlignment.Left)]
    [ValidateInput(nameof(HasValidPickupPrefab), "Pickup prefab must contain a SkillPickup component.")]
    [LabelText("Pickup Prefab")]
    private GameObject pickupPrefab;

    [SerializeField, BoxGroup("Spawn Count"), ToggleLeft]
    [LabelText("Use Skill Projectile Count")]
    private bool useSkillProjectileCount = false;

    [SerializeField, BoxGroup("Spawn Count"), ShowIf(nameof(UsesFixedSpawnCount)), Min(1)]
    [LabelText("Spawn Count")]
    private int spawnCount = 1;

    [ShowInInspector, ReadOnly, BoxGroup("Spawn Count"), LabelText("Count Source")]
    private string CountSourceLabel => useSkillProjectileCount
        ? "Skill Stats -> Projectile Count"
        : "Fixed Spawn Count";

    [SerializeField, BoxGroup("Placement"), HorizontalGroup("Placement/Row"), Min(0f)]
    [LabelText("Forward"), SuffixLabel("m")]
    private float forwardOffset = 0.5f;

    [SerializeField, BoxGroup("Placement"), HorizontalGroup("Placement/Row"), Min(0f)]
    [LabelText("Spread"), SuffixLabel("m")]
    private float spreadWidth = 0.75f;

    [SerializeField, BoxGroup("Placement"), HorizontalGroup("Placement/Row")]
    [LabelText("Height"), SuffixLabel("m")]
    private float verticalOffset = 0f;

    [ShowInInspector, ReadOnly, BoxGroup("Placement"), LabelText("Placement Preview")]
    private string PlacementPreviewLabel => $"{forwardOffset:0.##}m forward, {spreadWidth:0.##}m spread, {verticalOffset:0.##}m height";

    public override void Execute(SkillCastContext context)
    {
        if (context == null || pickupPrefab == null)
        {
            Debug.LogError($"Pickup payload '{name}' is missing its pickup prefab.", this);
            return;
        }

        int count = useSkillProjectileCount && context.SkillStats != null
            ? Mathf.Max(1, context.SkillStats.projectileCount)
            : Mathf.Max(1, spawnCount);

        Vector3 forward = context.AimDirection.sqrMagnitude > 0.0001f
            ? context.AimDirection.normalized
            : Vector3.forward;

        Vector3 side = Vector3.Cross(Vector3.up, forward);
        if (side.sqrMagnitude <= 0.0001f)
            side = Vector3.right;
        else
            side.Normalize();

        Vector3 basePosition = context.CastPosition + forward * forwardOffset + Vector3.up * verticalOffset;
        Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up);

        for (int i = 0; i < count; i++)
        {
            float lateralOffset = count <= 1
                ? 0f
                : Mathf.Lerp(-spreadWidth * 0.5f, spreadWidth * 0.5f, (float)i / (count - 1));

            Vector3 spawnPosition = basePosition + side * lateralOffset;
            var pickupObject = Object.Instantiate(pickupPrefab, spawnPosition, rotation);
            var skillPickup = pickupObject.GetComponent<SkillPickup>();
            if (skillPickup == null)
            {
                Debug.LogWarning($"Spawned pickup '{pickupPrefab.name}' has no SkillPickup component.", pickupObject);
                continue;
            }

            skillPickup.Initialize(new PickupContext(
                pickupObject,
                context.CasterObject,
                context.CasterRoot,
                context.User,
                context.SkillDef,
                context.SkillStats));
        }
    }

    private bool HasValidPickupPrefab(GameObject prefab)
    {
        return prefab == null || prefab.GetComponent<SkillPickup>() != null;
    }

    public override void CollectValidationIssues(List<string> issues)
    {
        if (issues == null)
            return;

        if (pickupPrefab == null)
        {
            issues.Add("Spawn Pickup payload has no pickup prefab configured.");
            return;
        }

        if (pickupPrefab.GetComponent<SkillPickup>() == null)
            issues.Add("Spawn Pickup payload prefab has no SkillPickup component.");
    }
}
