using UnityEngine;

public class ItemDropManager : MonoBehaviour
{
    public static ItemDropManager Instance { get; private set; }

    private void Awake()
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

      
        GameObject go = Instantiate(item.pickupPrefab, position, Quaternion.identity);
        
        var pickup = go.GetComponent<ItemPickup>();
        if (pickup != null)
        {
            pickup.item = item;
            pickup.amount = amount;
        }
        else
        {
            Debug.LogWarning("pickupPrefab has no ItemPickup component.");
        }
    }
}
