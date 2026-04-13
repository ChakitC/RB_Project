using UnityEngine;

public class ItemPickup : MonoBehaviour
{
    public ItemDefinition item;
    public int amount = 1;
    [SerializeField] private WeaponInstanceData weaponInstance;
    [SerializeField] private ItemPickupVisualPresenter visualPresenter;

    public WeaponInstanceData WeaponInstance => weaponInstance;

    void Reset()
    {
        if (visualPresenter == null)
            TryGetComponent(out visualPresenter);
    }

    void Awake()
    {
        if (visualPresenter == null)
            TryGetComponent(out visualPresenter);

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
        var inventory = other.GetComponent<PlayerInventory>();
        if (inventory == null)
            return;

        bool pickedUp = weaponInstance != null
            ? inventory.AddWeaponInstance(weaponInstance)
            : inventory.AddItem(item, amount);

        if (pickedUp)
        {
            Destroy(gameObject);
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
}
