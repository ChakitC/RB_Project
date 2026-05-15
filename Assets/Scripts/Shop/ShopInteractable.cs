using UnityEngine;

[DisallowMultipleComponent]
public class ShopInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private ShopCatalogBase catalog;
    [SerializeField] private ShopPanelUI shopPanel;
    [SerializeField] private int priority;
    [SerializeField] private string prompt = "Open Shop";

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
            return catalog;

        var panel = ResolvePanel();
        return panel != null ? panel.CurrentCatalog : null;
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
