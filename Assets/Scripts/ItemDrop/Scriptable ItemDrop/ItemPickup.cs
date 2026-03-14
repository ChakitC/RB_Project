using UnityEngine;

public class ItemPickup : MonoBehaviour
{
    public ItemDefinition item;
    public int amount = 1;

    private void OnTriggerEnter(Collider other)
    {
        var inventory = other.GetComponent<PlayerInventory>();
        if (inventory == null) return;

        if (inventory.AddItem(item, amount))
        {
            // เก็บสำเร็จ → ลบ object ทิ้ง
            Destroy(gameObject);
        }
        else
        {
            // กระเป๋าเต็ม
            Debug.Log("Cannot pick up item, inventory full.");
        }
    }
}
