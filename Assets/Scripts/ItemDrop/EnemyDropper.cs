using UnityEngine;

public class EnemyDropper : MonoBehaviour
{
    public EnemyDropProfile profile;

    public void DropItem()
    {
        if (!DropLogic.RollDrop(profile.dropChance))
            return;

        ItemRarity rarity = DropLogic.RollRarity(profile.rarityTable);
        DropTable pool = DropLogic.GetPoolFromProfile(profile, rarity);
        GameObject itemPrefab = DropLogic.RollItemFromPool(pool);

        if (itemPrefab == null)
        {
            Debug.Log("No item Prefab found");
            return;
        }

        if (ItemDropManager.Instance != null)
        {
            ItemDropManager.Instance.DropPickup(itemPrefab, transform.position, WeaponRarityUtility.FromItemRarity(rarity));
            return;
        }

        Instantiate(itemPrefab, transform.position, Quaternion.identity);
    }
}
