using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

[HideMonoScript]
public class SpawnPickupSkillPayloadDef : SkillPayloadDef
{
    const int DefaultGroundLayer = 12;
    const int DefaultGroundMask = 1 << DefaultGroundLayer;

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

    [SerializeField, BoxGroup("Placement"), Min(0.5f)]
    [LabelText("Maximum Target Distance"), SuffixLabel("m")]
    private float maximumTargetDistance = 12f;

    [SerializeField, BoxGroup("Placement")]
    [LabelText("Ground Layers")]
    private LayerMask groundMask = DefaultGroundMask;

    [ShowInInspector, ReadOnly, BoxGroup("Placement"), LabelText("Placement Preview")]
    private string PlacementPreviewLabel => $"{forwardOffset:0.##}m forward, {spreadWidth:0.##}m spread, {verticalOffset:0.##}m height";

    public GameObject PickupPrefab => pickupPrefab;
    public bool UsesSkillProjectileCount => useSkillProjectileCount;
    public int SpawnCount => spawnCount;
    public bool PickupPrefabMissingSkillPickupComponent =>
        pickupPrefab != null && pickupPrefab.GetComponent<SkillPickup>() == null;

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
            ? Vector3.ProjectOnPlane(context.AimDirection, Vector3.up).normalized
            : Vector3.forward;
        if (forward.sqrMagnitude <= 0.0001f)
            forward = Vector3.forward;

        Vector3 side = Vector3.Cross(Vector3.up, forward);
        if (side.sqrMagnitude <= 0.0001f)
            side = Vector3.right;
        else
            side.Normalize();

        Vector3 basePosition = context.CastPosition + forward * forwardOffset;
        PlayerContext player = context.CasterRoot != null
            ? context.CasterRoot.GetComponent<PlayerContext>()
            : null;
        if (player != null && context.AimTransform != null)
        {
            Vector3 targetOffset = context.AimTransform.position - context.CastPosition;
            targetOffset = Vector3.ClampMagnitude(
                targetOffset,
                Mathf.Max(0.5f, maximumTargetDistance));
            basePosition = context.CastPosition + targetOffset;
        }

        Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up);

        for (int i = 0; i < count; i++)
        {
            float lateralOffset = count <= 1
                ? 0f
                : Mathf.Lerp(-spreadWidth * 0.5f, spreadWidth * 0.5f, (float)i / (count - 1));

            Vector3 spawnPosition = basePosition + side * lateralOffset;
            if (TryResolveGroundPosition(spawnPosition, out Vector3 groundedPosition))
                spawnPosition = groundedPosition;

            spawnPosition += Vector3.up * verticalOffset;
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

    bool TryResolveGroundPosition(Vector3 position, out Vector3 groundedPosition)
    {
        groundedPosition = position;
        if (groundMask.value == 0)
            return false;

        if (!Physics.Raycast(
                position + Vector3.up * 10f,
                Vector3.down,
                out RaycastHit groundHit,
                30f,
                groundMask,
                QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        groundedPosition = groundHit.point;
        return true;
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

        if (groundMask.value == 0)
            issues.Add("Spawn Pickup payload has no ground layers configured.");
    }
}
