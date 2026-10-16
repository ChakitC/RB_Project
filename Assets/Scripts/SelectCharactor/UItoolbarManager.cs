using UnityEngine;

public class UItoolbarManager : MonoBehaviour
{
  public BasementContext bct;
  [Header("Audio")]
  [SerializeField] private AudioCue inventoryCue;
  [SerializeField] private AudioCue associatesCue;
  [SerializeField] private AudioCue upgradeCue;
  [SerializeField] private AudioCue shopCue;
  private bool _inventoryOpen = false;
  private bool _shopOpen = false;
  
  public void OpenInventoryMenu()
  {
    if (bct != null && bct.SupplyRoom != null && bct.SupplyRoom.IsOpen) return;
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

    if (inventoryCue != null)
      AudioService.Instance.Play(inventoryCue);
    
  }

  public void OpenAssociatesMenu()
  {
    if (bct != null && bct.SupplyRoom != null && bct.SupplyRoom.IsOpen) return;
    Debug.Log("Opening Associates Menu");

    if (associatesCue != null)
      AudioService.Instance.Play(associatesCue);
  }

  public void OpenUpgradeMenu()
  {
    if (bct != null && bct.SupplyRoom != null && bct.SupplyRoom.IsOpen) return;
    Debug.Log("Opening Upgrade Menu");

    if (upgradeCue != null)
      AudioService.Instance.Play(upgradeCue);
  }
  
  public void OpenShopMenu()
  {
    if (bct == null) bct = GetComponentInParent<BasementContext>();
    if (bct == null) bct = FindFirstObjectByType<BasementContext>(FindObjectsInactive.Include);
    if (bct != null && bct.SupplyRoom != null)
    {
      bool wasOpen = bct.SupplyRoom.IsOpen;
      bct.SupplyRoom.Open();
      if (!wasOpen && bct.SupplyRoom.IsOpen && shopCue != null) AudioService.Instance.Play(shopCue);
      return;
    }
    var shopPanel = ResolveShopPanel();
    if (shopPanel == null)
    {
      Debug.LogWarning("Shop panel not assigned on BasementContext.");
      return;
    }

    _shopOpen = !shopPanel.IsOpen;

    if (_shopOpen)
    {
      shopPanel.Open(null, bct != null ? bct.playerInventory : null);
      Debug.Log("Opening Shop Menu");
    }
    else
    {
      shopPanel.Close();
      Debug.Log("Closing Shop Menu");
    }

    if (shopCue != null)
      AudioService.Instance.Play(shopCue);
  }

  ShopPanelUI ResolveShopPanel()
  {
    if (bct == null)
      bct = GetComponentInParent<BasementContext>();

    if (bct == null)
      bct = FindFirstObjectByType<BasementContext>(FindObjectsInactive.Include);

    if (bct != null && bct.Shop != null)
      return bct.Shop;

    return FindFirstObjectByType<ShopPanelUI>(FindObjectsInactive.Include);
  }
  
}
