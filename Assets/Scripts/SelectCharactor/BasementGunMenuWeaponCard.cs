using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(CanvasGroup))]
public sealed class BasementGunMenuWeaponCard : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    [SerializeField] private UnityEngine.UI.Button button;
    [SerializeField] private UnityEngine.UI.Image icon;
    [SerializeField] private UnityEngine.UI.Image selection;
    [SerializeField] private TMP_Text title;
    [SerializeField] private TMP_Text state;
    [SerializeField] private UnityEngine.UI.Image equippedCharacterIcon;
    BasementGunMenuWeapons owner;
    string instanceId;
    BasementGunMenuWeaponHover hover;
    CanvasGroup canvasGroup;

    void Awake()
    {
        hover = GetComponent<BasementGunMenuWeaponHover>();
        canvasGroup = GetComponent<CanvasGroup>();
        button.onClick.AddListener(Select);
    }
    void Select() => owner?.Equip(instanceId);

    public void OnBeginDrag(PointerEventData e)
    {
        e.eligibleForClick = false;
        if (e.button != PointerEventData.InputButton.Left || owner == null) return;
        if (hover != null) hover.Clear();
        owner.BeginWeaponDrag(this, instanceId, icon.sprite, e);
    }
    public void OnDrag(PointerEventData e) => owner?.MoveWeaponDrag(this, e);
    public void OnEndDrag(PointerEventData e) => owner?.EndWeaponDrag(this);
    public void OnDrop(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Left || e.pointerDrag == null) return;
        owner?.DropWeaponOnCard(e.pointerDrag.GetComponent<BasementGunMenuWeaponCard>(), this, instanceId);
    }
    void OnDisable() => owner?.EndWeaponDrag(this);

    public void SetDraggingVisual(bool dragging)
    {
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = dragging ? .35f : 1f;
    }

    public void Bind(BasementGunMenuWeapons view, InventorySlotData data, string assignedOwner, string currentOwner, Sprite ownerIcon = null)
    {
        owner = view;
        instanceId = data.weaponInstance.instanceId;
        bool equipped = assignedOwner == currentOwner;
        bool unavailable = !string.IsNullOrWhiteSpace(assignedOwner) && !equipped;
        icon.sprite = data.item.icon;
        icon.enabled = icon.sprite != null;
        title.text = data.item.displayName;
        var weapon = data.weaponInstance;
        state.text = equipped ? "EQUIPPED" : unavailable ? "IN USE" : $"{weapon.rarity}  +{weapon.upgradeLevel}";
        selection.color = equipped ? new Color(1f, .73f, .16f) : Color.black;
        button.interactable = !equipped && !unavailable;
        if (equippedCharacterIcon != null)
        {
            equippedCharacterIcon.sprite = ownerIcon;
            equippedCharacterIcon.gameObject.SetActive(ownerIcon != null);
        }
        if (hover == null) hover = GetComponent<BasementGunMenuWeaponHover>();
        if (hover != null) hover.Bind(view, data);
    }
}
