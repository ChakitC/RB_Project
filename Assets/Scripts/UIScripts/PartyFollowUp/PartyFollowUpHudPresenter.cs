using UnityEngine;

[DisallowMultipleComponent]
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: null, sourceAssembly: "Assembly-CSharp", sourceClassName: "PartyComboHudPresenter")]
public sealed class PartyFollowUpHudPresenter : MonoBehaviour
{
    [SerializeField] PartyFollowUpSlotView[] slots;

    PartyFollowUpOpportunityController controller;

    public void Bind(PartyFollowUpOpportunityController source)
    {
        if (controller != null)
            controller.OfferChanged -= HandleOfferChanged;

        controller = source;
        Clear();
        for (int i = 0; slots != null && i < slots.Length; i++)
        {
            PartyFollowUpSlotView slot = slots[i];
            if (slot == null)
                continue;
            slot.Bind(controller);
        }

        if (controller == null || !controller.RuntimeFeatureEnabled)
            return;

        controller.OfferChanged += HandleOfferChanged;
        for (int i = 0; slots != null && i < slots.Length; i++)
        {
            PartyFollowUpSlotView slot = slots[i];
            if (slot == null)
                continue;
            if (controller.TryGetOffer(slot.Role, out PartyFollowUpOfferViewData offer))
                slot.Apply(offer);
        }
    }

    void OnDestroy()
    {
        if (controller != null)
            controller.OfferChanged -= HandleOfferChanged;
    }

    void HandleOfferChanged(PartyFollowUpOfferViewData offer)
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
