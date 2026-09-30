using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerUIRuntimeBinder : MonoBehaviour
{
    [SerializeField] private PlayerUIContext uiContext;
    [SerializeField] private GameObject gameplayHudPrefab;

    GameplayHudPresenter gameplayHud;

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

        bool hasGameplayComboView = false;
        if (gameplayHudPrefab != null && uiManagers.Length > 0)
        {
            if (gameplayHud == null)
            {
                Transform hudCanvas = uiManagers[0].transform;
                Transform existing = hudCanvas.Find("GameplayHUD");
                GameObject hudRoot = existing != null
                    ? existing.gameObject
                    : Instantiate(gameplayHudPrefab, hudCanvas);
                hudRoot.name = "GameplayHUD";
                gameplayHud = hudRoot.GetComponent<GameplayHudPresenter>();
                if (gameplayHud == null)
                    gameplayHud = hudRoot.AddComponent<GameplayHudPresenter>();

                Transform legacyHud = hudCanvas.Find("PlayerHUD");
                HideLegacyDisplay(legacyHud, "PlayerVitalHud");
                HideLegacyDisplay(legacyHud, "Bullet Text");
                HideLegacyDisplay(legacyHud, "Stamina Text");
                HideLegacyDisplay(legacyHud, "HP Text");
                HideLegacyDisplay(legacyHud, "SkillChargeHud");

                Transform partyHud = hudCanvas.Find("PartyHUD");
                HideLegacyDisplay(partyHud, "Ally1");
                HideLegacyDisplay(partyHud, "Ally2");
                HideLegacyDisplay(partyHud, "Ally3");
                HideLegacyDisplay(partyHud, "BulletInfo");
                HideLegacyDisplay(partyHud, "PlayerStatus");
                HideLegacyDisplay(partyHud, "PartySkills");
            }

            gameplayHud.Bind(player);
            PartyHudPresenter partyPresenter = GetComponentInChildren<PartyHudPresenter>(true);
            if (partyPresenter != null)
            {
                partyPresenter.Bind(player);
                hasGameplayComboView = gameplayHud.TryBindComboDisplay(partyPresenter);
            }
        }

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

        PartyComboHudPresenter[] comboPresenters =
            GetComponentsInChildren<PartyComboHudPresenter>(true);
        for (int i = 0; i < comboPresenters.Length; i++)
        {
            comboPresenters[i].Bind(hasGameplayComboView ? null : player.partyComboController);
            if (hasGameplayComboView)
                comboPresenters[i].gameObject.SetActive(false);
        }

        if (uiContext.activeSkillScreen != null)
            uiContext.activeSkillScreen.BindRuntime(player);

        error = string.Empty;
        return true;
    }

    static void HideLegacyDisplay(Transform hud, string childName)
    {
        Transform child = hud != null ? hud.Find(childName) : null;
        if (child != null)
            child.gameObject.SetActive(false);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (uiContext == null)
            uiContext = GetComponentInChildren<PlayerUIContext>(true);
    }
#endif
}
