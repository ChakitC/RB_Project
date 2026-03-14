using UnityEngine;

public class EnemyDropper : MonoBehaviour
{
    public EnemyDropProfile profile;

    public void DropItem()
    {
        // STEP 1: roll ว่าดรอปมั้ย
        if (!DropLogic.RollDrop(profile.dropChance))
            return;

        // STEP 2: roll rarity
        ItemRarity rarity = DropLogic.RollRarity(profile.rarityTable);

        // STEP 3: เลือก pool จาก rarity
        DropTable pool = DropLogic.GetPoolFromProfile(profile, rarity);

        // STEP 4: สุ่มไอเทมจริง
        GameObject itemPrefab = DropLogic.RollItemFromPool(pool);

        if (itemPrefab != null)
            Instantiate(itemPrefab, transform.position, Quaternion.identity);
        else
        {
            Debug.Log("No item Prefab found");
        }
    }
}