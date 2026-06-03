using UnityEngine;
using UnityEngine.Serialization;

public class ItemDropManager : MonoBehaviour
{
    public static ItemDropManager Instance { get; private set; }

    [Header("Pickup Shell By Rarity")]
    [FormerlySerializedAs("defaultPickupPrefab")]
    [SerializeField] private GameObject commonPickupPrefab;
    [SerializeField] private GameObject rarePickupPrefab;
    [SerializeField] private GameObject epicPickupPrefab;

    [Header("Weapon Instance")]
    [SerializeField] private WeaponAffixDatabase weaponAffixDatabase;

    [Header("Drop Scatter Motion")]
    [SerializeField] private PickupDropArcSettings dropArcSettings = new PickupDropArcSettings();

    GameObject runtimePickupTemplate;

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
        DropItem(item, amount, position, WeaponRarity.Common);
    }

    public void DropItem(ItemDefinition item, int amount, Vector3 position, WeaponRarity rarity)
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

        GameObject pickupPrefab = ResolvePickupPrefab(rarity, nameof(DropItem));
        if (pickupPrefab == null)
            return;

        if (item is GunConfig)
        {
            float burstAngleOffset = CreateBurstAngleOffset();
            for (int i = 0; i < amount; i++)
                SpawnPickup(pickupPrefab, item, 1, position, rarity, i, amount, burstAngleOffset);

            return;
        }

        SpawnPickup(pickupPrefab, item, amount, position, rarity, 0, 1, CreateBurstAngleOffset());
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

            float burstAngleOffset = CreateBurstAngleOffset();
            for (int i = 0; i < weaponDropCount; i++)
                SpawnPickup(pickupPrefab, template.item, 1, position, rarity, i, weaponDropCount, burstAngleOffset);

            return;
        }

        if (template.amount <= 0)
        {
            Debug.LogWarning($"DropPickup: invalid amount {template.amount} on {pickupPrefab.name}");
            return;
        }

        SpawnPickup(pickupPrefab, template.item, template.amount, position, rarity, 0, 1, CreateBurstAngleOffset());
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

    GameObject ResolvePickupPrefab(WeaponRarity rarity, string caller)
    {
        GameObject pickupPrefab = ResolveDefaultPickupPrefab(rarity);

        if (!TryGetPickupTemplate(pickupPrefab, caller, out _))
            return null;

        return pickupPrefab;
    }

    GameObject ResolveDefaultPickupPrefab(WeaponRarity rarity)
    {
        GameObject pickupPrefab = rarity switch
        {
            WeaponRarity.Epic => epicPickupPrefab != null ? epicPickupPrefab : (rarePickupPrefab != null ? rarePickupPrefab : commonPickupPrefab),
            WeaponRarity.Rare => rarePickupPrefab != null ? rarePickupPrefab : commonPickupPrefab,
            _ => commonPickupPrefab
        };

        return pickupPrefab != null ? pickupPrefab : GetOrCreateRuntimePickupTemplate();
    }

    GameObject GetOrCreateRuntimePickupTemplate()
    {
        if (runtimePickupTemplate != null)
            return runtimePickupTemplate;

        runtimePickupTemplate = new GameObject("RuntimeItemPickupTemplate");
        runtimePickupTemplate.hideFlags = HideFlags.HideAndDontSave;
        runtimePickupTemplate.SetActive(false);

        var collider = runtimePickupTemplate.AddComponent<SphereCollider>();
        collider.isTrigger = true;
        collider.radius = 1f;

        var rigidbody = runtimePickupTemplate.AddComponent<Rigidbody>();
        rigidbody.isKinematic = true;
        rigidbody.useGravity = false;
        rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        var visualRoot = new GameObject("VisualRoot");
        visualRoot.transform.SetParent(runtimePickupTemplate.transform, false);

        runtimePickupTemplate.AddComponent<ItemPickup>();
        runtimePickupTemplate.AddComponent<ItemPickupVisualPresenter>();
        runtimePickupTemplate.AddComponent<PickupVisualMotion>();

        return runtimePickupTemplate;
    }

    void SpawnPickup(GameObject pickupPrefab, ItemDefinition item, int amount, Vector3 position, WeaponRarity rarity, int dropIndex, int dropCount, float burstAngleOffset)
    {
        Vector3 spawnPosition = ShouldPlayDropArc() ? dropArcSettings.ResolveStartPosition(position) : position;
        var pickupObject = Instantiate(pickupPrefab, spawnPosition, Quaternion.identity);
        var pickup = pickupObject.GetComponent<ItemPickup>();

        if (!ConfigurePickup(pickup, item, amount, rarity))
        {
            Destroy(pickupObject);
            return;
        }

        PlayDropArc(pickupObject, position, dropIndex, dropCount, burstAngleOffset);

        if (!pickupObject.activeSelf)
            pickupObject.SetActive(true);
    }

    bool ConfigurePickup(ItemPickup pickup, ItemDefinition item, int amount, WeaponRarity rarity)
    {
        if (pickup == null || item == null || amount <= 0)
            return false;

        WeaponInstanceData weaponInstance = null;

        if (item is GunConfig gun)
            weaponInstance = WeaponInstanceFactory.CreateInstance(gun, rarity, weaponAffixDatabase);

        pickup.Initialize(item, amount, weaponInstance);

        return true;
    }

    float CreateBurstAngleOffset()
    {
        return dropArcSettings != null ? dropArcSettings.CreateBurstAngleOffset() : 0f;
    }

    bool ShouldPlayDropArc()
    {
        return dropArcSettings != null && dropArcSettings.Enabled;
    }

    void PlayDropArc(GameObject pickupObject, Vector3 origin, int dropIndex, int dropCount, float burstAngleOffset)
    {
        if (pickupObject == null || !ShouldPlayDropArc())
            return;

        var arcMotion = pickupObject.GetComponent<PickupDropArcMotion>();
        if (arcMotion == null)
            arcMotion = pickupObject.AddComponent<PickupDropArcMotion>();

        Vector3 startPosition = dropArcSettings.ResolveStartPosition(origin);
        Vector3 landingPosition = dropArcSettings.ResolveLandingPosition(origin, dropIndex, dropCount, burstAngleOffset);
        float delay = dropArcSettings.ResolveDelay(dropIndex);

        arcMotion.Play(startPosition, landingPosition, dropArcSettings.Duration, dropArcSettings.ArcHeight, delay);
    }
}
