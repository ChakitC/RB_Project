using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class BasementGunMenuWeapons : MonoBehaviour
{
    [SerializeField] private BasementGunMenuScreenController screen;
    [SerializeField] private RectTransform content;
    [SerializeField] private BasementGunMenuWeaponCard cardPrefab;
    [SerializeField] private UnityEngine.UI.ScrollRect scroll;
    [SerializeField] private UnityEngine.UI.Image equippedIcon;
    [SerializeField] private InventorySlotUI equippedTooltipAnchor;
    [SerializeField] private UnityEngine.UI.Button informationButton;
    [SerializeField] private TMP_Text weaponStats;
    [SerializeField] private TMP_Text message;
    [SerializeField] private CharacterDatabase characterDatabase;
    readonly List<BasementGunMenuWeaponCard> cards = new();
    PlayerInventory inventory;
    PartySlot partySlot;
    string ownerId;
    bool refreshPending;
    bool detailsVisible;
    Coroutine headerRefresh;
    Component detailsOwner;
    BasementGunMenuWeaponCard dragSource;
    string dragInstanceId;
    RectTransform dragImage;
    public bool CanHover => screen != null && screen.CanInteract && dragSource == null;
    public event Action EquipmentChanged;

    public void Bind(PartySlot slot, PlayerInventory source)
    {
        Unbind();
        partySlot = slot;
        inventory = source;
        ownerId = CharacterEquipment.BuildCharacterOwnerId(slot != null && slot.Selected != null ? slot.Selected.characterId : null);
        if (inventory != null)
        {
            if (inventory.InventorySystem != null)
            {
                inventory.InventorySystem.OnSlotChanged += OnSlotChanged;
                inventory.InventorySystem.OnInventoryReset += QueueRefresh;
            }
            inventory.OnEquippedWeaponChanged += OnWeaponChanged;
        }
        Refresh();
        if (scroll != null) scroll.verticalNormalizedPosition = 1;
    }

    public void Unbind()
    {
        EndWeaponDrag(dragSource);
        HideDetails();
        if (inventory != null)
        {
            if (inventory.InventorySystem != null)
            {
                inventory.InventorySystem.OnSlotChanged -= OnSlotChanged;
                inventory.InventorySystem.OnInventoryReset -= QueueRefresh;
            }
            inventory.OnEquippedWeaponChanged -= OnWeaponChanged;
        }
        inventory = null;
        partySlot = null;
        ownerId = null;
        refreshPending = false;
    }

    void OnSlotChanged(int index) => QueueRefresh();
    void OnWeaponChanged(string id) => QueueRefresh();
    void QueueRefresh() => refreshPending = true;
    void LateUpdate()
    {
        if (refreshPending) Refresh();

    }

    public bool Equip(string instanceId)
    {
        if (screen == null || !screen.CanInteract || inventory == null || partySlot == null ||
            partySlot.Selected == null || ownerId != CharacterEquipment.BuildCharacterOwnerId(partySlot.Selected.characterId) ||
            string.IsNullOrWhiteSpace(instanceId)) return false;
        var current = EquipmentAssignmentService.GetEquippedSlotData(inventory, EquipmentItemKind.Weapon, ownerId);
        if (current.HasWeaponInstance && current.weaponInstance.instanceId == instanceId) return false;
        if (!EquipmentAssignmentService.TryEquip(inventory, EquipmentItemKind.Weapon, ownerId, instanceId))
        {
            Refresh();
            message.text = "Weapon unavailable";
            return false;
        }
        partySlot.RefreshSelectedWeaponVisual(instanceId, inventory);
        Refresh();
        EquipmentChanged?.Invoke();
        return true;
    }

    public void Refresh()
    {
        // Inventory refresh can reuse a cell for another item; cancel its drag first.
        EndWeaponDrag(dragSource);
        refreshPending = false;
        if (cardPrefab == null || content == null) return;
        int count = 0;
        if (inventory != null && !string.IsNullOrWhiteSpace(ownerId))
            foreach (var data in inventory.Slots)
            {
                if (data == null || !data.HasWeaponInstance || !(data.item is GunConfig) ||
                    !EquipmentAssignmentService.TryGetInstanceId(data, EquipmentItemKind.Weapon, out string id)) continue;
                if (count == cards.Count) cards.Add(Instantiate(cardPrefab, content));
                var card = cards[count++];
                card.gameObject.SetActive(true);
                string assignedOwner = EquipmentAssignmentService.FindAssignedOwnerId(inventory, EquipmentItemKind.Weapon, id);
                card.Bind(this, data, assignedOwner, ownerId, ResolveOwnerIcon(assignedOwner));
            }
        for (int i = count; i < cards.Count; i++) cards[i].gameObject.SetActive(false);
        // Four columns match the diagonal reference artwork; no nested fitter on grid cells.
        content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(1, Mathf.CeilToInt(count / 4f)) * 214f);
        message.text = inventory == null ? "Inventory unavailable" : count == 0 ? "No weapons in inventory" : "SELECT WEAPON";
        var equipped = inventory != null && !string.IsNullOrWhiteSpace(ownerId)
            ? EquipmentAssignmentService.GetEquippedSlotData(inventory, EquipmentItemKind.Weapon, ownerId) : null;
        bool hasWeapon = equipped != null && equipped.HasWeaponInstance && equipped.item is GunConfig;
        equippedIcon.sprite = hasWeapon ? equipped.item.icon : null;
        equippedIcon.enabled = equippedIcon.sprite != null;
        informationButton.interactable = false;
        var equippedHover = equippedTooltipAnchor.GetComponent<BasementGunMenuWeaponHover>();
        if (equippedHover != null) equippedHover.Bind(this, equipped);
        if (hasWeapon)
        {
            var gun = (GunConfig)equipped.item;
            var instance = equipped.weaponInstance;
            var stats = WeaponStatPreview.Build(gun, instance);
            weaponStats.text = $"{gun.displayName}\n{instance.rarity}  +{instance.upgradeLevel}\nDAMAGE  {stats.Damage:0.#}\nFIRE INTERVAL  {stats.FireInterval:0.###} s\nMAGAZINE  {stats.MaxMagazine:0}\nRESERVE  {stats.MaxReserveAmmo:0}";
        }
        else weaponStats.text = "NO WEAPON EQUIPPED";
        if (detailsVisible) HideDetails();
    }

    // Reserved for the future Information screen; existing bindings are harmless.
    public void ToggleDetails() { }

    Sprite ResolveOwnerIcon(string assignedOwner)
    {
        if (!CharacterEquipment.TryParseCharacterOwnerId(assignedOwner, out string characterId)) return null;
        var selected = partySlot != null ? partySlot.Selected : null;
        var character = selected != null && selected.characterId == characterId
            ? selected : characterDatabase != null ? characterDatabase.GetById(characterId) : null;
        return character != null ? character.icon : null;
    }

    public void ShowHoverDetails(Component source, InventorySlotData data, Vector2 position, Camera eventCamera)
    {
        if (!CanHover || source == null || data == null || !data.HasWeaponInstance) return;
        HideDetails();
        detailsOwner = source;
        detailsVisible = true;
        weaponStats.fontSharedMaterial = weaponStats.font.material;
        message.fontSharedMaterial = message.font.material;
        var canvas = GetComponentInParent<Canvas>();
        InventorySlotTooltipUI.GetOrCreate(canvas)?.ShowFor(source, data, position, canvas, eventCamera, weaponStats.font);
        // Rebind after the tooltip has completed its forced Canvas/layout pass.
        RefreshHeaderAfterLayout();
    }

    public bool HideDetails()
    {
        bool wasVisible = detailsVisible;
        detailsVisible = false;
        if (detailsOwner != null) InventorySlotTooltipUI.HideForOwner(detailsOwner);
        detailsOwner = null;
        RefreshHeaderAfterLayout();
        return wasVisible;
    }
    public void HideHoverDetails(Component source)
    {
        if (detailsOwner == source) HideDetails();
    }

    public bool BeginWeaponDrag(BasementGunMenuWeaponCard source, string id, Sprite sprite, PointerEventData e)
    {
        if (!CanHover || inventory == null || source == null ||
            source.GetComponentInParent<BasementGunMenuWeapons>() != this ||
            FindWeaponIndex(id) < 0) return false;
        HideDetails();
        foreach (var hover in GetComponentsInChildren<BasementGunMenuWeaponHover>()) hover.Clear();
        dragSource = source;
        dragInstanceId = id;
        source.SetDraggingVisual(true);
        var canvas = GetComponentInParent<Canvas>().rootCanvas;
        dragImage = new GameObject("DraggedWeapon", typeof(RectTransform), typeof(UnityEngine.UI.Image)).GetComponent<RectTransform>();
        dragImage.SetParent(canvas.transform, false);
        dragImage.sizeDelta = new Vector2(150, 150);
        var image = dragImage.GetComponent<UnityEngine.UI.Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.color = new Color(1, 1, 1, .85f);
        MoveWeaponDrag(source, e);
        return true;
    }
    public void MoveWeaponDrag(BasementGunMenuWeaponCard source, PointerEventData e)
    {
        if (dragSource != source || dragImage == null) return;
        var canvas = dragImage.GetComponentInParent<Canvas>();
        var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)dragImage.parent, e.position, camera, out var point))
            dragImage.anchoredPosition = point + new Vector2(20f, -20f);
    }
    public bool DropWeapon(BasementGunMenuWeaponCard source)
    {
        if (source == null || source != dragSource) return false;
        string id = dragInstanceId;
        EndWeaponDrag(source);
        return Equip(id);
    }
    public bool DropWeaponOnCard(BasementGunMenuWeaponCard source, BasementGunMenuWeaponCard target, string targetId)
    {
        if (source == null || source != dragSource || target == null ||
            target.GetComponentInParent<BasementGunMenuWeapons>() != this ||
            screen == null || !screen.CanInteract || inventory == null) return false;
        // Visible cards omit empty/non-weapon slots; resolve the actual inventory indices.
        int fromIndex = FindWeaponIndex(dragInstanceId);
        int toIndex = FindWeaponIndex(targetId);
        EndWeaponDrag(source);
        if (fromIndex < 0 || toIndex < 0 || fromIndex == toIndex) return false;
        if (!inventory.MoveOrSwap(fromIndex, toIndex)) return false;
        SaveManager.Instance?.SaveInventoryOnly();
        Refresh();
        return true;
    }
    int FindWeaponIndex(string id)
    {
        if (inventory == null || string.IsNullOrWhiteSpace(id)) return -1;
        for (int i = 0; i < inventory.Slots.Count; i++)
        {
            var data = inventory.Slots[i];
            if (data != null && data.item is GunConfig && data.HasWeaponInstance && data.weaponInstance.instanceId == id)
                return i;
        }
        return -1;
    }
    public void EndWeaponDrag(BasementGunMenuWeaponCard source)
    {
        if (source != dragSource) return;
        if (dragSource != null) dragSource.SetDraggingVisual(false);
        if (dragImage != null) Destroy(dragImage.gameObject);
        dragImage = null;
        dragSource = null;
        dragInstanceId = null;
    }
    void RefreshHeaderAfterLayout()
    {
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy || !Application.isPlaying) return;
        if (headerRefresh != null) StopCoroutine(headerRefresh);
        headerRefresh = StartCoroutine(RefreshHeader());
    }
    IEnumerator RefreshHeader()
    {
        yield return null;
        if (weaponStats != null) weaponStats.SetMaterialDirty();
        if (message != null) message.SetMaterialDirty();
        headerRefresh = null;
    }
    void OnDisable()
    {
        Unbind();
        if (headerRefresh != null) StopCoroutine(headerRefresh);
        headerRefresh = null;
    }
}
