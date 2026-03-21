using UnityEngine;

public class ItemPickup : MonoBehaviour
{
    public ItemDefinition item;
    public int amount = 1;
    [SerializeField] private WeaponInstanceData weaponInstance;

    public WeaponInstanceData WeaponInstance => weaponInstance;

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
}
