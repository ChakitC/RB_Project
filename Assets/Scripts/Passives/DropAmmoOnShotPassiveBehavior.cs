using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "DropAmmoOnShotBehavior",
    menuName = "Combat/Passives/Behaviors/Drop Ammo On Shot")]
public sealed class DropAmmoOnShotPassiveBehavior : PassiveCustomBehavior
{
    [Header("Trigger")]
    [SerializeField, Range(0f, 1f)] private float procChance = 0.08f;
    [SerializeField, Min(0f)] private float internalCooldownSeconds = 1.5f;
    [SerializeField] private PassiveOriginFilter originFilter = PassiveOriginFilter.ExternalOnly;

    [Header("Pickup")]
    [SerializeField] private GameObject pickupPrefab;
    [SerializeField] private bool overrideCollectorRule = true;
    [SerializeField] private PickupCollectorRule collectorRule = PickupCollectorRule.PlayerOrCompanionOnly;
    [SerializeField, Min(1)] private int dropCount = 1;

    [Header("Placement")]
    [SerializeField, Min(0f)] private float backOffset = 0.35f;
    [SerializeField] private float verticalOffset = 0.65f;
    [SerializeField, Min(0f)] private float scatterRadius = 0.45f;

    [Header("Drop Arc")]
    [SerializeField] private bool useDropArcMotion;
    [SerializeField] private PickupDropArcSettings dropArcSettings = new PickupDropArcSettings();

    [Header("Toss")]
    [SerializeField] private bool applyRigidbodyImpulse = true;
    [SerializeField, Min(0f)] private float horizontalImpulse = 1.6f;
    [SerializeField, Min(0f)] private float upwardImpulse = 2.2f;

    [Header("Progression")]
    [Tooltip("Optional. Second pickup drops only when the owning slot's snapshot grants this id.")]
    [UpgradeIdPicker] [SerializeField] private string extraDropUpgradeId;

    readonly Dictionary<int, float> nextReadyTimeByController = new();

    public override void CollectUpgradeIds(List<string> ids)
    {
        if (!string.IsNullOrWhiteSpace(extraDropUpgradeId))
            ids.Add(extraDropUpgradeId.Trim());
    }

    float WorldNow
    {
        get
        {
            TimeSlowManager timeSlow = TimeSlowManager.Instance;
            return timeSlow != null ? timeSlow.WorldTime : Time.time;
        }
    }

    public override void OnPassiveEvent(
        PassiveController controller,
        CustomPassiveDef definition,
        in PassiveEventContext context,
        SkillUpgradeStatSnapshot upgrades)
    {
        if (controller == null || context.Type != PassiveEventType.ShotFired)
            return;

        if (!MatchesOriginFilter(context.Origin))
            return;

        if (pickupPrefab == null || pickupPrefab.GetComponent<SkillPickup>() == null)
            return;

        float now = WorldNow;
        int controllerId = controller.GetInstanceID();
        if (IsCooldownActive(controllerId, now))
            return;

        float chance = Mathf.Clamp01(procChance);
        if (chance <= 0f || Random.value > chance)
            return;

        if (!TryResolveDropSource(controller, out CharacteContext ctx, out Transform sourceRoot))
            return;

        bool grantExtraDrop = !string.IsNullOrWhiteSpace(extraDropUpgradeId) &&
                              upgrades != null && upgrades.HasUpgrade(extraDropUpgradeId);
        SpawnDrops(controller, ctx, sourceRoot, grantExtraDrop ? dropCount + 1 : dropCount);
        StampCooldown(controllerId, now);
    }

    public override void OnUnequipped(PassiveController controller, CustomPassiveDef definition)
    {
        if (controller != null)
            nextReadyTimeByController.Remove(controller.GetInstanceID());
    }

    void SpawnDrops(PassiveController controller, CharacteContext ctx, Transform sourceRoot, int requestedCount)
    {
        int count = Mathf.Max(1, requestedCount);
        Vector3 origin = ResolveSpawnOrigin(sourceRoot);
        bool useDropArc = ShouldUseDropArcMotion();
        float burstAngleOffset = useDropArc ? dropArcSettings.CreateBurstAngleOffset() : 0f;

        for (int i = 0; i < count; i++)
        {
            Vector3 spawnPosition = useDropArc
                ? dropArcSettings.ResolveStartPosition(origin)
                : ResolveScatteredSpawnPosition(origin);

            GameObject pickupObject = Object.Instantiate(
                pickupPrefab,
                spawnPosition,
                ResolveSpawnRotation(sourceRoot));

            SkillPickup pickup = pickupObject.GetComponent<SkillPickup>();
            if (pickup == null)
            {
                Object.Destroy(pickupObject);
                continue;
            }

            if (overrideCollectorRule)
                pickup.SetCollectorRule(collectorRule);

            pickup.Initialize(new PickupContext(
                pickupObject,
                controller.gameObject,
                sourceRoot,
                ctx != null ? ctx.EnegySystem : null,
                null,
                null));

            if (useDropArc)
                PlayDropArc(pickupObject, origin, i, count, burstAngleOffset);
            else
                ApplyImpulse(pickupObject, sourceRoot);
        }
    }

    bool IsCooldownActive(int controllerId, float now)
    {
        if (!nextReadyTimeByController.TryGetValue(controllerId, out float readyTime))
            return false;

        if (now < readyTime)
            return true;

        nextReadyTimeByController.Remove(controllerId);
        return false;
    }

    void StampCooldown(int controllerId, float now)
    {
        float cooldown = Mathf.Max(0f, internalCooldownSeconds);
        if (cooldown <= 0f)
        {
            nextReadyTimeByController.Remove(controllerId);
            return;
        }

        nextReadyTimeByController[controllerId] = now + cooldown;
    }

    Vector3 ResolveSpawnOrigin(Transform sourceRoot)
    {
        if (sourceRoot == null)
            return Vector3.up * verticalOffset;

        return sourceRoot.position -
               sourceRoot.forward * Mathf.Max(0f, backOffset) +
               Vector3.up * verticalOffset;
    }

    Quaternion ResolveSpawnRotation(Transform sourceRoot)
    {
        if (sourceRoot == null)
            return Quaternion.identity;

        Vector3 forward = sourceRoot.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.0001f)
            return sourceRoot.rotation;

        return Quaternion.LookRotation(forward.normalized, Vector3.up);
    }

    Vector3 ResolveScatteredSpawnPosition(Vector3 origin)
    {
        Vector3 scatter = Random.insideUnitSphere;
        scatter.y = 0f;
        if (scatter.sqrMagnitude > 0.0001f)
            scatter = scatter.normalized * Random.Range(0f, scatterRadius);

        return origin + scatter;
    }

    bool ShouldUseDropArcMotion()
    {
        return useDropArcMotion && dropArcSettings != null && dropArcSettings.Enabled;
    }

    void PlayDropArc(GameObject pickupObject, Vector3 origin, int dropIndex, int dropCount, float burstAngleOffset)
    {
        if (pickupObject == null || !ShouldUseDropArcMotion())
            return;

        var arcMotion = pickupObject.GetComponent<PickupDropArcMotion>();
        if (arcMotion == null)
            arcMotion = pickupObject.AddComponent<PickupDropArcMotion>();

        Vector3 startPosition = dropArcSettings.ResolveStartPosition(origin);
        Vector3 landingPosition = dropArcSettings.ResolveLandingPosition(origin, dropIndex, dropCount, burstAngleOffset);
        float delay = dropArcSettings.ResolveDelay(dropIndex);

        arcMotion.Play(startPosition, landingPosition, dropArcSettings.Duration, dropArcSettings.ArcHeight, delay);
    }

    void ApplyImpulse(GameObject pickupObject, Transform sourceRoot)
    {
        if (!applyRigidbodyImpulse || pickupObject == null)
            return;

        Rigidbody body = pickupObject.GetComponent<Rigidbody>();
        if (body == null)
            return;

        Vector3 direction = sourceRoot != null ? -sourceRoot.forward : Random.insideUnitSphere;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            direction = Random.insideUnitSphere;

        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            direction = Vector3.forward;

        direction.Normalize();
        Vector3 impulse = direction * horizontalImpulse + Vector3.up * upwardImpulse;
        body.AddForce(impulse, ForceMode.Impulse);
    }

    bool TryResolveDropSource(
        PassiveController controller,
        out CharacteContext ctx,
        out Transform sourceRoot)
    {
        ctx = null;
        sourceRoot = null;

        if (controller == null)
            return false;

        ctx = controller.GetComponent<CharacteContext>();
        if (ctx == null)
            ctx = controller.GetComponentInParent<CharacteContext>();

        if (ctx != null)
        {
            ctx.ResolveReferences();
            sourceRoot = ctx.transform;
            return true;
        }

        sourceRoot = controller.transform.root != null ? controller.transform.root : controller.transform;
        return sourceRoot != null;
    }

    bool MatchesOriginFilter(PassiveEventOrigin origin)
    {
        return originFilter switch
        {
            PassiveOriginFilter.ExternalOnly => origin == PassiveEventOrigin.External,
            PassiveOriginFilter.NonPassive => origin != PassiveEventOrigin.Passive,
            PassiveOriginFilter.PassiveOnly => origin == PassiveEventOrigin.Passive,
            PassiveOriginFilter.Any => true,
            _ => false,
        };
    }
}
