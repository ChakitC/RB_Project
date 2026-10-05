using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "DropAmmoOnShotBehavior",
    menuName = "Combat/Passives/Behaviors/Drop Ammo On Shot")]
public sealed class DropAmmoOnShotPassiveBehavior : PassiveCustomBehavior
{
    [System.Serializable]
    public sealed class UpgradeGatedPickupEffect
    {
        [UpgradeIdPicker] public string requiredUpgradeId;
        public PickupEffectDef effect;
    }

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

    [Tooltip("Optional. Adds procChanceBonus to the proc chance when the owning slot's snapshot grants this id.")]
    [UpgradeIdPicker] [SerializeField] private string procChanceBoostUpgradeId;
    [SerializeField, Range(0f, 1f)] private float procChanceBonus = 0.05f;

    [Tooltip("Optional. Subtracts cooldownReductionSeconds from the internal cooldown when the owning slot's snapshot grants this id.")]
    [UpgradeIdPicker] [SerializeField] private string cooldownReductionUpgradeId;
    [SerializeField, Min(0f)] private float cooldownReductionSeconds = 0.5f;

    [Tooltip("Optional. Each entry's effect is added to a spawned pickup only when the owning slot's snapshot grants its requiredUpgradeId.")]
    [SerializeField] private List<UpgradeGatedPickupEffect> upgradeEffects = new();

    // Serialized migration input only. Runtime spawning is owned by PassivePickupStep.
    public override void CollectUpgradeIds(List<string> ids)
    {
        if (!string.IsNullOrWhiteSpace(extraDropUpgradeId))
            ids.Add(extraDropUpgradeId.Trim());
        if (!string.IsNullOrWhiteSpace(procChanceBoostUpgradeId))
            ids.Add(procChanceBoostUpgradeId.Trim());
        if (!string.IsNullOrWhiteSpace(cooldownReductionUpgradeId))
            ids.Add(cooldownReductionUpgradeId.Trim());

        if (upgradeEffects == null)
            return;

        for (int i = 0; i < upgradeEffects.Count; i++)
        {
            string id = upgradeEffects[i]?.requiredUpgradeId;
            if (!string.IsNullOrWhiteSpace(id))
                ids.Add(id.Trim());
        }
    }

}