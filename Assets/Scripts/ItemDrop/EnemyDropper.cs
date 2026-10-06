using UnityEngine;

public class EnemyDropper : MonoBehaviour
{
    public EnemyDropProfile profile;
    EnemyContext ctx;

    EnemyContext Context
    {
        get
        {
            if (ctx == null)
                ctx = GetComponentInParent<EnemyContext>(true);
            return ctx;
        }
    }

    Vector3 DropPosition => Context != null ? Context.transform.position : transform.position;

    public void DropItem()
    {
        if (profile == null)
        {
            Debug.LogWarning("[EnemyDropper] Missing drop profile.", this);
            return;
        }

        ItemDropManager dropManager = ItemDropManager.Instance;
        if (dropManager == null)
        {
            Debug.LogWarning("[EnemyDropper] ItemDropManager is missing; item drops require the central pickup shell setup.", this);
            return;
        }

        DropGuaranteedCurrency(dropManager);

        if (!DropLogic.RollDrop(profile.dropChance))
            return;

        int enemyLevel = ResolveEnemyLevel();
        ItemRarity rarity = DropLogic.RollRarity(profile.ResolveRarityTable(enemyLevel));
        WeaponRarity weaponRarity = WeaponRarityUtility.FromItemRarity(rarity);

        DropTable pool = DropLogic.GetPoolFromProfile(profile, rarity);
        DropEntry entry = DropLogic.RollEntryFromPool(pool);

        if (entry == null || !entry.TryResolveItem(out var item))
        {
            Debug.Log("[EnemyDropper] No item found in drop table.");
            return;
        }

        int amount = entry.ResolveAmount();

        dropManager.DropItem(item, amount, DropPosition, weaponRarity);
    }

    void DropGuaranteedCurrency(ItemDropManager dropManager)
    {
        int goldAmount = profile.RollGoldAmount();
        if (profile.goldItem != null && goldAmount > 0)
            dropManager.DropItem(profile.goldItem, goldAmount, DropPosition, WeaponRarity.Common);

        int scrapAmount = profile.RollScrapAmount();
        if (profile.scrapItem != null && scrapAmount > 0)
            dropManager.DropItem(profile.scrapItem, scrapAmount, DropPosition, WeaponRarity.Common);
    }

    int ResolveEnemyLevel()
    {
        EnemyContext context = Context;
        context?.ResolveReferences();
        EnemyLevelSystem levelSystem = context != null
            ? context.EnemyLevelSystem
            : GetComponentInParent<EnemyLevelSystem>(true);
        return levelSystem != null ? levelSystem.Level : 1;
    }
}
