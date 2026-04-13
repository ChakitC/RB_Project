using UnityEngine;

public class EnemyDropper : MonoBehaviour
{
    public EnemyDropProfile profile;

    public void DropItem()
    {
        if (profile == null)
        {
            Debug.LogWarning("[EnemyDropper] Missing drop profile.", this);
            return;
        }

        if (!DropLogic.RollDrop(profile.dropChance))
            return;

        ItemRarity rarity = DropLogic.RollRarity(profile.rarityTable);
        WeaponRarity weaponRarity = WeaponRarityUtility.FromItemRarity(rarity);

        DropTable pool = DropLogic.GetPoolFromProfile(profile, rarity);
        DropEntry entry = DropLogic.RollEntryFromPool(pool);

        if (entry == null || !entry.TryResolveItem(out var item))
        {
            Debug.Log("[EnemyDropper] No item found in drop table.");
            return;
        }

        int amount = entry.ResolveAmount();

        if (ItemDropManager.Instance != null)
        {
            ItemDropManager.Instance.DropItem(item, amount, transform.position, weaponRarity);
            return;
        }

        if (!SpawnFallbackPickup(item, amount, weaponRarity))
            Debug.LogWarning("[EnemyDropper] ItemDropManager is missing and the selected item has no pickup prefab fallback.", this);
    }

    bool SpawnFallbackPickup(ItemDefinition item, int amount, WeaponRarity rarity)
    {
        if (item == null || item.pickupPrefab == null)
            return false;

        var pickupObject = Instantiate(item.pickupPrefab, transform.position, Quaternion.identity);
        var pickup = pickupObject != null ? pickupObject.GetComponent<ItemPickup>() : null;
        if (pickup == null)
            return false;

        WeaponInstanceData weaponInstance = null;
        if (item is GunConfig gun)
            weaponInstance = WeaponInstanceFactory.CreatePlainInstance(gun, rarity);

        pickup.Initialize(item, amount, weaponInstance);
        return true;
    }
}
