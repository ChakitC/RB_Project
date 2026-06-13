using UnityEngine;

[DisallowMultipleComponent]
public class AccessoryDismantleService : MonoBehaviour
{
    [Header("Dismantle Reward")]
    [SerializeField, Min(0)] private int baseScrap = 10;
    [SerializeField, Min(0)] private int modifierScrapBonus = 5;
    [SerializeField, Min(0)] private int scrapPerLevel = 3;

    [Header("Dismantle Rules")]
    [SerializeField] private bool allowAssignedAccessoryDismantle = false;
    [SerializeField] private bool saveAfterDismantle = true;

    public bool CanDismantle(
        PlayerInventory inventory,
        string instanceId,
        out int scrapReward,
        out string reason)
    {
        return CanDismantle(
            inventory,
            instanceId,
            allowAssignedAccessoryDismantle,
            baseScrap,
            modifierScrapBonus,
            scrapPerLevel,
            out scrapReward,
            out reason);
    }

    public bool TryDismantle(
        PlayerInventory inventory,
        string instanceId,
        out int scrapGranted,
        out string reason)
    {
        return TryDismantle(
            inventory,
            instanceId,
            allowAssignedAccessoryDismantle,
            saveAfterDismantle,
            baseScrap,
            modifierScrapBonus,
            scrapPerLevel,
            out scrapGranted,
            out reason);
    }

    public static bool CanDismantleWithDefaultReward(
        PlayerInventory inventory,
        string instanceId,
        out int scrapReward,
        out string reason)
    {
        return CanDismantle(
            inventory,
            instanceId,
            allowAssignedAccessoryDismantle: false,
            baseScrap: 10,
            modifierScrapBonus: 5,
            scrapPerLevel: 3,
            out scrapReward,
            out reason);
    }

    public static bool TryDismantleWithDefaultReward(
        PlayerInventory inventory,
        string instanceId,
        out int scrapGranted,
        out string reason)
    {
        return TryDismantle(
            inventory,
            instanceId,
            allowAssignedAccessoryDismantle: false,
            saveAfterDismantle: true,
            baseScrap: 10,
            modifierScrapBonus: 5,
            scrapPerLevel: 3,
            out scrapGranted,
            out reason);
    }

    public static int CalculateScrapReward(
        AccessoryInstanceData instance,
        int baseScrap,
        int modifierScrapBonus,
        int scrapPerLevel)
    {
        if (instance == null)
            return 0;

        int modifierBonus = string.IsNullOrWhiteSpace(instance.modifierId)
            ? 0
            : Mathf.Max(0, modifierScrapBonus);
        int levelBonus = Mathf.Max(0, instance.upgradeLevel) * Mathf.Max(0, scrapPerLevel);
        return Mathf.Max(0, baseScrap) + modifierBonus + levelBonus;
    }

    static bool CanDismantle(
        PlayerInventory inventory,
        string instanceId,
        bool allowAssignedAccessoryDismantle,
        int baseScrap,
        int modifierScrapBonus,
        int scrapPerLevel,
        out int scrapReward,
        out string reason)
    {
        scrapReward = 0;
        reason = null;

        if (inventory == null)
        {
            reason = "Missing inventory.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(instanceId))
        {
            reason = "Missing accessory instance id.";
            return false;
        }

        if (!inventory.TryGetAccessoryInstanceWithDefinition(instanceId, out _, out AccessoryInstanceData instance) ||
            instance == null)
        {
            reason = "Accessory instance not found.";
            return false;
        }

        if (!allowAssignedAccessoryDismantle)
        {
            string assignedOwnerId = EquipmentAssignmentService.FindAssignedOwnerId(
                inventory,
                EquipmentItemKind.Accessory,
                instanceId);

            if (!string.IsNullOrWhiteSpace(assignedOwnerId))
            {
                reason = "Cannot dismantle equipped accessory.";
                return false;
            }
        }

        scrapReward = CalculateScrapReward(instance, baseScrap, modifierScrapBonus, scrapPerLevel);
        return true;
    }

    static bool TryDismantle(
        PlayerInventory inventory,
        string instanceId,
        bool allowAssignedAccessoryDismantle,
        bool saveAfterDismantle,
        int baseScrap,
        int modifierScrapBonus,
        int scrapPerLevel,
        out int scrapGranted,
        out string reason)
    {
        scrapGranted = 0;

        if (!CanDismantle(
                inventory,
                instanceId,
                allowAssignedAccessoryDismantle,
                baseScrap,
                modifierScrapBonus,
                scrapPerLevel,
                out int scrapReward,
                out reason))
        {
            return false;
        }

        if (!inventory.RemoveAccessoryInstance(instanceId))
        {
            reason = "Could not remove accessory instance.";
            return false;
        }

        if (scrapReward > 0)
            inventory.AddScrap(scrapReward);

        scrapGranted = scrapReward;

        if (saveAfterDismantle && SaveManager.Instance != null)
            SaveManager.Instance.SaveInventoryOnly();

        reason = null;
        return true;
    }
}
