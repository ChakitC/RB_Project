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

        if (amount <= 0)
        {
            Debug.LogWarning($"DropItem: invalid amount {amount} for {item.displayName}");
            return;
        }

        if (!TryGetPickupTemplate(item.pickupPrefab, nameof(DropItem), out _))
            return;

        if (item is GunConfig)
        {
            for (int i = 0; i < amount; i++)
                SpawnPickup(item.pickupPrefab, item, 1, position, WeaponRarity.Common);

            return;
        }

        SpawnPickup(item.pickupPrefab, item, amount, position, WeaponRarity.Common);
    }

    public void DropPickup(GameObject pickupPrefab, Vector3 position, WeaponRarity rarity)
    {
        if (!TryGetPickupTemplate(pickupPrefab, nameof(DropPickup), out var template))
            return;

        if (template.item == null)
        {
            Debug.LogWarning("DropPickup: pickupPrefab has no item assigned.");
            return;
        }

        if (template.item is GunConfig)
        {
            int weaponDropCount = template.amount > 0 ? template.amount : 1;
            if (template.amount <= 0)
                Debug.LogWarning($"DropPickup: invalid weapon amount {template.amount} on {pickupPrefab.name}, defaulting to 1.");

            for (int i = 0; i < weaponDropCount; i++)
                SpawnPickup(pickupPrefab, template.item, 1, position, rarity);

            return;
        }

        if (template.amount <= 0)
        {
            Debug.LogWarning($"DropPickup: invalid amount {template.amount} on {pickupPrefab.name}");
            return;
        }

        SpawnPickup(pickupPrefab, template.item, template.amount, position, rarity);
    }

    bool TryGetPickupTemplate(GameObject pickupPrefab, string caller, out ItemPickup template)
    {
        template = null;

        if (pickupPrefab == null)
        {
            Debug.LogWarning($"{caller}: pickupPrefab is null");
            return false;
        }

        template = pickupPrefab.GetComponent<ItemPickup>();
        if (template == null)
        {
            Debug.LogWarning($"{caller}: pickupPrefab has no ItemPickup component.");
            return false;
        }

        return true;
    }

    void SpawnPickup(GameObject pickupPrefab, ItemDefinition item, int amount, Vector3 position, WeaponRarity rarity)
    {
        var pickupObject = Instantiate(pickupPrefab, position, Quaternion.identity);
        var pickup = pickupObject.GetComponent<ItemPickup>();

        if (!ConfigurePickup(pickup, item, amount, rarity))
            Destroy(pickupObject);
    }

    bool ConfigurePickup(ItemPickup pickup, ItemDefinition item, int amount, WeaponRarity rarity)
    {
        if (pickup == null || item == null || amount <= 0)
            return false;

        pickup.SetWeaponInstance(null);

        pickup.item = item;
        pickup.amount = amount;

        if (item is GunConfig gun)
        {
            var weaponInstance = WeaponInstanceFactory.CreateInstance(gun, rarity, weaponAffixDatabase);
            pickup.SetWeaponInstance(weaponInstance);
        }

        return true;
    }
}
