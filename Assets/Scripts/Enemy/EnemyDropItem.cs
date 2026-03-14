using UnityEngine;

public class EnemyDropItem : MonoBehaviour

{
    
    public ItemDefinition dropItem;
    public int dropAmount = 1;
    public void DropItem()
    {
        
        ItemDropManager.Instance.DropItem(dropItem, dropAmount, transform.position);
            Debug.Log("Enemy Drop Item");
            
    }

    
}
