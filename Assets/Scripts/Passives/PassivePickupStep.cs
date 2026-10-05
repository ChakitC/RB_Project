using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;
using Object = UnityEngine.Object;

[Serializable]
public sealed class PassivePickupStep : PassiveStep
{
    [Header("Pickup")]
    [SerializeField] public GameObject pickupPrefab;
    [SerializeField] public bool overrideCollectorRule = true;
    [SerializeField] public PickupCollectorRule collectorRule = PickupCollectorRule.PlayerOrCompanionOnly;
    [SerializeField, Min(1)] public int dropCount = 1;

    [Header("Placement")]
    [SerializeField, Min(0f)] public float backOffset = 0.35f;
    [SerializeField] public float verticalOffset = 0.65f;
    [SerializeField, Min(0f)] public float scatterRadius = 0.45f;

    [Header("Drop Arc")]
    [SerializeField] public bool useDropArcMotion;
    [SerializeField] public PickupDropArcSettings dropArcSettings = new PickupDropArcSettings();

    [Header("Toss")]
    [SerializeField] public bool applyRigidbodyImpulse = true;
    [SerializeField, Min(0f)] public float horizontalImpulse = 1.6f;
    [SerializeField, Min(0f)] public float upwardImpulse = 2.2f;

    [Header("Progression")]
    [Tooltip("Optional. Second pickup drops only when the owning slot's snapshot grants this id.")]
    [UpgradeIdPicker] [SerializeField] public string extraDropUpgradeId;

    [Tooltip("Optional. Adds procChanceBonus to the proc chance when the owning slot's snapshot grants this id.")]
    [UpgradeIdPicker] [SerializeField] public string procChanceBoostUpgradeId;
    [SerializeField, Range(0f, 1f)] public float procChanceBonus = 0.05f;

    [Tooltip("Optional. Subtracts cooldownReductionSeconds from the internal cooldown when the owning slot's snapshot grants this id.")]
    [UpgradeIdPicker] [SerializeField] public string cooldownReductionUpgradeId;
    [SerializeField, Min(0f)] public float cooldownReductionSeconds = 0.5f;

    [Tooltip("Optional. Each entry's effect is added to a spawned pickup only when the owning slot's snapshot grants its requiredUpgradeId.")]
    [SerializeField] public List<DropAmmoOnShotPassiveBehavior.UpgradeGatedPickupEffect> upgradeEffects = new();

    public override PassiveResult Execute(PassiveExecution execution)
    {
        if (pickupPrefab == null || pickupPrefab.GetComponent<SkillPickup>() == null) return default;
        var upgrades = execution.Runtime.Grant.Upgrades;
        var effects = new List<PickupEffectDef>();
        ResolveGrantedUpgradeEffects(upgrades, effects);
        bool extra = !string.IsNullOrWhiteSpace(extraDropUpgradeId) && upgrades?.HasUpgrade(extraDropUpgradeId) == true;
        SpawnDrops(execution.Runtime.Controller, execution.Owner, execution.Owner.transform, dropCount + (extra ? 1 : 0), effects);
        return PassiveResult.Success();
    }
    void ResolveGrantedUpgradeEffects(SkillUpgradeStatSnapshot upgrades, List<PickupEffectDef> buffer)
    {
        buffer.Clear();
        if (upgradeEffects == null || upgrades == null)
            return;

        for (int i = 0; i < upgradeEffects.Count; i++)
        {
            DropAmmoOnShotPassiveBehavior.UpgradeGatedPickupEffect entry = upgradeEffects[i];
            if (entry == null || entry.effect == null)
                continue;

            if (string.IsNullOrWhiteSpace(entry.requiredUpgradeId) || upgrades.HasUpgrade(entry.requiredUpgradeId))
                buffer.Add(entry.effect);
        }
    }

    void SpawnDrops(PassiveController controller, CharacteContext ctx, Transform sourceRoot, int requestedCount, List<PickupEffectDef> grantedEffects)
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

            if (grantedEffects != null)
            {
                for (int e = 0; e < grantedEffects.Count; e++)
                    pickup.AddEffect(grantedEffects[e]);
            }

            if (useDropArc)
                PlayDropArc(pickupObject, origin, i, count, burstAngleOffset);
            else
                ApplyImpulse(pickupObject, sourceRoot);
        }
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


}