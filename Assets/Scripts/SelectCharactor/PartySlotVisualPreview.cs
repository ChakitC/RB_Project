using UnityEngine;

public class PartySlotVisualPreview : MonoBehaviour
{
    [Header("Presentation")]
    [SerializeField] private Transform modelRoot;
    [SerializeField] private BasementCharacterPresentation presentationPrefab;
    [SerializeField] private WeaponDatabase weaponDatabase;

    private BasementCharacterPresentation presentation;
    private int currentSlot;
    private int partyIndex;

    public Animator Animator => presentation != null ? presentation.Animator : null;
    public BasementCharacterPresentation Presentation => presentation;
    public Transform PresentationRoot => modelRoot;

    public void SetPartyContext(int saveSlot, int partyMemberIndex)
    {
        currentSlot = saveSlot;
        partyIndex = partyMemberIndex;
    }

    public void BuildCharacter(CharacterStats selected, Transform slotRoot)
    {
        if (!selected)
        {
            ClearPreview();
            return;
        }
        if (!modelRoot || !presentationPrefab)
        {
            Debug.LogError("[PartySlotVisualPreview] Assign the model root and shared presentation prefab.", this);
            return;
        }
        if (!presentation)
            presentation = Instantiate(presentationPrefab, modelRoot, false);

        bool built = presentation.Build(selected);
        var selectable = GetComponentInParent<CharacterSelectable>(true);
        if (selectable) selectable.BindPresentation(presentation);
        if (built) RefreshWeapon(selected);
        RefreshSelectionOutline();
    }

    public void RefreshWeapon(
        CharacterStats selected,
        string preferredEquippedInstanceId = null,
        PlayerInventory preferredInventory = null)
    {
        if (!presentation) return;
        if (selected == null || selected != presentation.CurrentDef)
        {
            presentation.ClearWeapon();
            return;
        }

        CharacterWeaponPreviewResolver.TryResolveSelectedCharacterWeapon(
            selected, currentSlot, partyIndex, weaponDatabase,
            preferredInventory, preferredEquippedInstanceId, out var weapon);
        presentation.ShowWeapon(weapon);
        RefreshSelectionOutline();
    }

    void RefreshSelectionOutline()
    {
        var selectable = GetComponentInParent<CharacterSelectable>(true);
        if (selectable) selectable.RefreshSelectionOutlineDeferred();
    }

    public void ClearPreview()
    {
        if (presentation) presentation.Clear();
        var selectable = GetComponentInParent<CharacterSelectable>(true);
        if (selectable) selectable.BindPresentation(presentation);
    }

    // Compatibility entry point. Authored legacy models were migrated out of SlotPoint;
    // only the presentation may destroy its owned model. Unrelated children are preserved.
    public void ClearTemporarySelectObjects(Transform slotRoot) { }
}
