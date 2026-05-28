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

        Debug.LogWarning("[EnemyDropper] ItemDropManager is missing; item drops require the central pickup shell setup.", this);
    }
}
