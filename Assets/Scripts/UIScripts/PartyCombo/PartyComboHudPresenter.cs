using UnityEngine;

[DisallowMultipleComponent]
public sealed class PartyComboHudPresenter : MonoBehaviour
{
    [SerializeField] PartyComboSlotView[] slots;

    PartyComboOpportunityController controller;

    public void Bind(PartyComboOpportunityController source)
    {
        if (controller != null)
            controller.OfferChanged -= HandleOfferChanged;

        controller = source;
        Clear();
        for (int i = 0; slots != null && i < slots.Length; i++)
        {
            PartyComboSlotView slot = slots[i];
            if (slot == null)
                continue;
            slot.Bind(controller);
        }

        if (controller == null || !controller.RuntimeFeatureEnabled)
            return;

        controller.OfferChanged += HandleOfferChanged;
        for (int i = 0; slots != null && i < slots.Length; i++)
        {
            PartyComboSlotView slot = slots[i];
            if (slot == null)
                continue;
            if (controller.TryGetOffer(slot.Role, out PartyComboOfferViewData offer))
                slot.Apply(offer);
        }
    }

    void OnDestroy()
    {
        if (controller != null)
            controller.OfferChanged -= HandleOfferChanged;
    }

    void HandleOfferChanged(PartyComboOfferViewData offer)
    {
        for (int i = 0; slots != null && i < slots.Length; i++)
        {
            if (slots[i] != null && slots[i].Role == offer.OwnerRole)
                slots[i].Apply(offer);
        }
    }

    void Clear()
    {
        for (int i = 0; slots != null && i < slots.Length; i++)
            slots[i]?.Clear();
    }
}
