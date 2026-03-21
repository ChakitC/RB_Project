using UnityEngine;

public class ItemDropManager : MonoBehaviour
{
    public static ItemDropManager Instance { get; private set; }

    [Header("Weapon Instance")]
    [SerializeField] private WeaponAffixDatabase weaponAffixDatabase;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void DropItem(ItemDefinition item, int amount, Vector3 position)
    {
        if (item == null)
        {
            Debug.LogWarning("DropItem: item is null");
            return;
        }

        if (item.pickupPrefab == null)
        {
            Debug.LogWarning($"DropItem: {item.displayName} has no pickupPrefab");
            return;
        }

        var go = Instantiate(item.pickupPrefab, position, Quaternion.identity);
        ConfigurePickup(go, item, amount, WeaponRarity.Common);
    }

    public void DropPickup(GameObject pickupPrefab, Vector3 position, WeaponRarity rarity)
    {
        if (pickupPrefab == null)
        {
            Debug.LogWarning("DropPickup: pickupPrefab is null");
            return;
        }

        var go = Instantiate(pickupPrefab, position, Quaternion.identity);
        var pickup = go.GetComponent<ItemPickup>();
        if (pickup == null)
        {
            Debug.LogWarning("pickupPrefab has no ItemPickup component.");
            return;
        }

        ConfigurePickup(go, pickup.item, pickup.amount, rarity);
    }

    void ConfigurePickup(GameObject pickupObject, ItemDefinition item, int amount, WeaponRarity rarity)
    {
        if (pickupObject == null)
            return;

        var pickup = pickupObject.GetComponent<ItemPickup>();
        if (pickup == null)
        {
            Debug.LogWarning("pickupPrefab has no ItemPickup component.");
            return;
        }

        pickup.item = item;
        pickup.amount = amount;

        if (item is GunConfig gun)
        {
            var weaponInstance = WeaponInstanceFactory.CreateInstance(gun, rarity, weaponAffixDatabase);
            pickup.SetWeaponInstance(weaponInstance);
        }
    }
}
