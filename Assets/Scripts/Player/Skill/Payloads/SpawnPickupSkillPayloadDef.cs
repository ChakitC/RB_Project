using UnityEngine;

[CreateAssetMenu(fileName = "Spawn Pickup Skill Payload", menuName = "Game/Skill Payload/Spawn Pickup")]
public class SpawnPickupSkillPayloadDef : SkillPayloadDef
{
    [SerializeField] private GameObject pickupPrefab;
    [SerializeField, Min(1)] private int spawnCount = 1;
    [SerializeField] private bool useSkillProjectileCount = false;
    [SerializeField, Min(0f)] private float forwardOffset = 0.5f;
    [SerializeField, Min(0f)] private float spreadWidth = 0.75f;
    [SerializeField] private float verticalOffset = 0f;

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
}
