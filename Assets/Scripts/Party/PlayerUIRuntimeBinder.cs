using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerUIRuntimeBinder : MonoBehaviour
{
    [SerializeField] private PlayerUIContext uiContext;

    public bool TryBind(PlayerContext player, out string error)
    {
        if (player == null)
        {
            error = "Cannot bind Player UI without PlayerContext.";
            return false;
        }

        if (uiContext == null)
            uiContext = GetComponentInChildren<PlayerUIContext>(true);

        if (uiContext == null)
        {
            error = $"Player UI root '{name}' is missing PlayerUIContext.";
            return false;
        }

        player.ResolveReferences();
        if (player.inventory == null)
        {
            error = "PlayerContext is missing PlayerInventory.";
            return false;
        }

        player.playerUIContext = uiContext;

        InventoryUI[] inventoryViews = GetComponentsInChildren<InventoryUI>(true);
        for (int i = 0; i < inventoryViews.Length; i++)
            inventoryViews[i].BindSource(player.inventory);

        UIEquipment[] equipmentViews = GetComponentsInChildren<UIEquipment>(true);
        for (int i = 0; i < equipmentViews.Length; i++)
            equipmentViews[i].BindSource(player.inventory);

        UIWeaponUpgrade[] upgradeViews = GetComponentsInChildren<UIWeaponUpgrade>(true);
        for (int i = 0; i < upgradeViews.Length; i++)
            upgradeViews[i].BindSource(player.inventory);

        UIManager[] uiManagers = GetComponentsInChildren<UIManager>(true);
        for (int i = 0; i < uiManagers.Length; i++)
            uiManagers[i].BindContext(player);

        PlayerFullscreenEffectController[] fullscreenControllers =
            GetComponentsInChildren<PlayerFullscreenEffectController>(true);
        for (int i = 0; i < fullscreenControllers.Length; i++)
            fullscreenControllers[i].BindContext(player, uiContext);

        InteractionIndicatorPresenter[] interactionPresenters =
            GetComponentsInChildren<InteractionIndicatorPresenter>(true);
        for (int i = 0; i < interactionPresenters.Length; i++)
            interactionPresenters[i].Bind(player);

        ActiveSkillChargePresenter[] chargePresenters =
            GetComponentsInChildren<ActiveSkillChargePresenter>(true);
        for (int i = 0; i < chargePresenters.Length; i++)
            chargePresenters[i].Bind(player);

        SkillCastFeedbackPresenter[] castFeedbackPresenters =
            GetComponentsInChildren<SkillCastFeedbackPresenter>(true);
        for (int i = 0; i < castFeedbackPresenters.Length; i++)
            castFeedbackPresenters[i].Bind(player);

        if (uiContext.activeSkillScreen != null)
            uiContext.activeSkillScreen.BindRuntime(player);

        error = string.Empty;
        return true;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (uiContext == null)
            uiContext = GetComponentInChildren<PlayerUIContext>(true);
    }
#endif
}
