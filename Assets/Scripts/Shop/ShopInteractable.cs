using UnityEngine;

[DisallowMultipleComponent]
public class ShopInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private ShopCatalogBase catalog;
    [SerializeField] private ShopPanelUI shopPanel;
    [SerializeField] private int priority;
    [SerializeField] private string prompt = "Open Shop";

    private ShopCatalogBase runtimeCatalog;

    public int Priority => priority;

    public string GetPrompt(Interactor interactor)
    {
        return prompt;
    }

    public bool CanInteract(Interactor interactor)
    {
        return ResolvePanel() != null && ResolveInventory(interactor) != null && ResolveCatalog() != null;
    }

    public void Interact(Interactor interactor)
    {
        var panel = ResolvePanel();
        var inventory = ResolveInventory(interactor);
        var activeCatalog = ResolveCatalog();

        if (panel == null || inventory == null || activeCatalog == null)
            return;

        panel.Open(activeCatalog, inventory);
    }

    ShopCatalogBase ResolveCatalog()
    {
        if (catalog != null)
        {
            if (catalog is not RandomShopCatalog randomCatalog)
                return catalog;

            if (runtimeCatalog == null)
            {
                var runtimeRandomCatalog = Instantiate(randomCatalog);
                runtimeRandomCatalog.name = $"{randomCatalog.name} ({name})";
                MapRunController runController = FindFirstObjectByType<MapRunController>();
                runtimeRandomCatalog.ConfigureForTestStage(runController != null ? runController.RunConfig : null);
                runtimeCatalog = runtimeRandomCatalog;
            }

            return runtimeCatalog;
        }

        var panel = ResolvePanel();
        return panel != null ? panel.CurrentCatalog : null;
    }

    void OnDestroy()
    {
        if (runtimeCatalog != null)
            Destroy(runtimeCatalog);
    }

    ShopPanelUI ResolvePanel()
    {
        if (shopPanel == null)
            shopPanel = FindFirstObjectByType<ShopPanelUI>(FindObjectsInactive.Include);

        return shopPanel;
    }

    PlayerInventory ResolveInventory(Interactor interactor)
    {
        if (interactor != null)
        {
            var playerContext = interactor.OwnerContext as PlayerContext;
            if (playerContext != null)
            {
                playerContext.ResolveReferences();
                if (playerContext.inventory != null)
                    return playerContext.inventory;
            }

            var fromParent = interactor.GetComponentInParent<PlayerInventory>();
            if (fromParent != null)
                return fromParent;

            var fromChildren = interactor.GetComponentInChildren<PlayerInventory>(true);
            if (fromChildren != null)
                return fromChildren;
        }

        return FindFirstObjectByType<PlayerInventory>(FindObjectsInactive.Include);
    }
}
