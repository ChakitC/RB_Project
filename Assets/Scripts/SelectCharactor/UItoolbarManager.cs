using UnityEngine;

public class UItoolbarManager : MonoBehaviour
{
  public BasementContext bct;
  private bool _inventoryOpen = false;
  
  public void OpenInventoryMenu()
  {
    if (bct == null || bct.Inventory == null || bct.Inventory.InventoryMenu == null)
    {
      Debug.LogWarning("InventoryMenu not assigned on BasementContext / Inventory");
      return;
    }
    _inventoryOpen = !_inventoryOpen;
    
    bct.Inventory.InventoryMenu.SetActive(_inventoryOpen);

    if (_inventoryOpen)
      Debug.Log("Opening Inventory Menu");
    else
      Debug.Log("Closing Inventory Menu");
    
  }

  public void OpenAssociatesMenu()
  {
    Debug.Log("Opening Associates Menu");
  }

  public void OpenUpgradeMenu()
  {
    Debug.Log("Opening Upgrade Menu");
  }
  
  public void OpenShopMenu()
  {
    Debug.Log("Opening Shop Menu");    
  }
  
}
