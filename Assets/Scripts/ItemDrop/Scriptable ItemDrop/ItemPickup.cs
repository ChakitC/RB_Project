using UnityEngine;

public class ItemPickup : MonoBehaviour
{
    public ItemDefinition item;
    public int amount = 1;
    [SerializeField] private WeaponInstanceData weaponInstance;
    [SerializeField] private ItemPickupVisualPresenter visualPresenter;
    [SerializeField] private PickupVisualMotion pickupVisualMotion;

    bool _collecting;

    public WeaponInstanceData WeaponInstance => weaponInstance;

    void Reset()
    {
        if (visualPresenter == null)
            TryGetComponent(out visualPresenter);

        if (pickupVisualMotion == null)
            TryGetComponent(out pickupVisualMotion);
    }

    void Awake()
    {
        if (visualPresenter == null)
            TryGetComponent(out visualPresenter);

        if (pickupVisualMotion == null)
            TryGetComponent(out pickupVisualMotion);

        RefreshVisual();
    }

    public void Initialize(ItemDefinition itemDefinition, int pickupAmount, WeaponInstanceData instance = null)
    {
        item = itemDefinition;
        amount = Mathf.Max(1, pickupAmount);
        SetWeaponInstance(instance);
        RefreshVisual();
    }

    public void SetWeaponInstance(WeaponInstanceData instance)
    {
        weaponInstance = instance != null ? instance.DeepClone() : null;
        if (weaponInstance != null)
            amount = 1;
    }

    void OnTriggerEnter(Collider other)
    {
        if (_collecting)
            return;

        var inventory = ResolveInventory(other);
        if (inventory == null)
            return;

        bool pickedUp = weaponInstance != null
            ? inventory.AddWeaponInstance(weaponInstance)
            : inventory.AddItem(item, amount);

        if (pickedUp)
        {
            CompletePickup(inventory.transform);
        }
        else
        {
            Debug.Log("Cannot pick up item, inventory full.");
        }
    }

    void RefreshVisual()
    {
        if (visualPresenter == null)
            return;

        visualPresenter.Present(item);
    }

    PlayerInventory ResolveInventory(Collider other)
    {
        if (other == null)
            return null;

        var inventory = other.GetComponent<PlayerInventory>();
        if (inventory != null)
            return inventory;

        return other.GetComponentInParent<PlayerInventory>();
    }

    void CompletePickup(Transform collector)
    {
        _collecting = true;
        DisablePickupColliders();

        if (pickupVisualMotion == null)
            TryGetComponent(out pickupVisualMotion);

        if (pickupVisualMotion == null)
        {
            Destroy(gameObject);
            return;
        }

        pickupVisualMotion.enabled = true;
        pickupVisualMotion.PlayCollectTo(collector, DestroySelf);
    }

    void DisablePickupColliders()
    {
        foreach (var pickupCollider in GetComponentsInChildren<Collider>())
            pickupCollider.enabled = false;
    }

    void DestroySelf()
    {
        Destroy(gameObject);
    }
}
