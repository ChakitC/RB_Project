using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BasementAccessoryScreenController : MonoBehaviour, IBasementMenuView
{
    [SerializeField] private CameraManager cameraManager;
    [SerializeField] private CharacterSelectManager characterSelection;
    [SerializeField] private CanvasGroup screenGroup;
    // Retained for old prefab serialization. Timing now belongs to menuDefinition.
    [SerializeField, HideInInspector] private float fadeInDuration = 0.5f;
    [SerializeField, HideInInspector] private float otherCharactersFadeDuration = 0.45f;
    [SerializeField] private GameObject slotArtwork;
    [SerializeField] private BasementAccessorySlotView[] slots;
    [SerializeField] private UIEquipment picker;
    [SerializeField] private GameObject pickerPanel;
    [SerializeField] private TMP_Text pickerTitle;
    [SerializeField] private TMP_Text pickerMessage;
    [SerializeField] private Button unequipButton;
    [SerializeField] private GameObject[] backgroundRoots;
    [SerializeField] private RectTransform[] letterboxBars;
    [SerializeField] private BasementMenuDefinitionSO menuDefinition;
    BasementMenuPresentationController presentation;
    PartySlot partySlot;
    PlayerInventory inventory;
    string ownerId;
    string characterId;
    bool closing;
    int selectedSlot = -1;

    public bool IsOpen { get; private set; }
    public bool IsPickerOpen => IsOpen && selectedSlot >= 0;
    public bool CanInteract => IsOpen && !closing && presentation != null && presentation.Owns(this) && presentation.CanInteract;
    public event Action EquipmentChanged;
    public CanvasGroup PresentationGroup => screenGroup;
    public CharacterSelectManager CharacterSelection => characterSelection;
    public GameObject[] BackgroundRoots => backgroundRoots;
    public RectTransform[] LetterboxBars => letterboxBars;

    void Awake()
    {
        SetVisible(false);
        pickerPanel.SetActive(false);
        picker.OnLeftClick += OnPickerItemClicked;
    }

    public void Open(PartySlot slot, PlayerInventory source)
    {
        if (!isActiveAndEnabled || IsOpen || slot == null || slot.Selected == null || source == null ||
            string.IsNullOrWhiteSpace(slot.Selected.characterId) || cameraManager == null) return;
        presentation = BasementMenuPresentationController.Resolve(cameraManager);
        if (presentation == null || !presentation.TryOpen(menuDefinition, slot, this)) return;
        partySlot = slot;
        inventory = source;
        characterId = slot.Selected.characterId;
        ownerId = CharacterEquipment.BuildCharacterOwnerId(characterId);
        IsOpen = true;
        closing = false;
        selectedSlot = -1;
        if (inventory.InventorySystem != null)
        {
            inventory.InventorySystem.OnSlotChanged += OnInventorySlotChanged;
            inventory.InventorySystem.OnInventoryReset += Refresh;
        }
        pickerPanel.SetActive(false);
        slotArtwork.SetActive(true);
        Refresh();
    }

    void Update()
    {
        if (!IsOpen) return;
        if (inventory == null) { Close(); return; }
        if (Input.GetKeyDown(KeyCode.Escape) && CanInteract) Back();
    }

    public void SelectSlot(int index)
    {
        if (!CanInteract || index < 0 || index >= AccessoryLoadout.DefaultSlotCount) return;
        selectedSlot = index;
        InventorySlotTooltipUI.HideForOwner(null);
        picker.BindSource(inventory, characterId, partySlot, EquipmentItemKind.Accessory, index);
        pickerTitle.text = $"ACCESSORY / SLOT {index + 1}";
        pickerMessage.text = HasAccessories() ? "Choose an accessory" : "No accessories in inventory";
        unequipButton.interactable = !GetSlot(index).IsEmpty;
        slotArtwork.SetActive(false);
        pickerPanel.SetActive(true);
    }

    bool HasAccessories()
    {
        foreach (var data in inventory.Slots)
            if (EquipmentAssignmentService.IsEquipmentSlot(data, EquipmentItemKind.Accessory)) return true;
        return false;
    }

    void OnPickerItemClicked(int index, InventorySlotData data)
    {
        if (!CanInteract || selectedSlot < 0 || data == null || !data.HasAccessoryInstance) return;
        if (!EquipmentAssignmentService.TryEquip(inventory, EquipmentItemKind.Accessory, ownerId,
                data.accessoryInstance.instanceId, selectedSlot))
        {
            pickerMessage.text = "Cannot equip: already assigned or unique accessory limit reached.";
            return;
        }
        AfterEquipmentChanged();
    }

    public void UnequipSelected()
    {
        if (!CanInteract || selectedSlot < 0) return;
        if (!EquipmentAssignmentService.TryUnequip(inventory, EquipmentItemKind.Accessory, ownerId, selectedSlot))
        {
            pickerMessage.text = "Could not remove this accessory.";
            return;
        }
        AfterEquipmentChanged();
    }

    void AfterEquipmentChanged()
    {
        DismissPicker();
        Refresh();
        EquipmentChanged?.Invoke();
    }

    public void Back()
    {
        if (!CanInteract) return;
        if (selectedSlot >= 0) DismissPicker();
        else Close();
    }

    void DismissPicker()
    {
        InventorySlotTooltipUI.HideForOwner(null);
        selectedSlot = -1;
        pickerPanel.SetActive(false);
        picker.BindSource(null, null, null, EquipmentItemKind.Accessory);
        slotArtwork.SetActive(true);
    }

    public void Refresh()
    {
        if (!IsOpen || inventory == null) return;
        foreach (var view in slots) if (view != null) view.Bind(this, GetSlot(view.SlotIndex));
        if (selectedSlot >= 0) unequipButton.interactable = !GetSlot(selectedSlot).IsEmpty;
    }

    InventorySlotData GetSlot(int index) => EquipmentAssignmentService.GetEquippedSlotData(
        inventory, EquipmentItemKind.Accessory, ownerId, index) ?? new InventorySlotData();

    void OnInventorySlotChanged(int index) => Refresh();
    public void Close()
    {
        if (IsOpen && presentation != null && presentation.Owns(this)) presentation.Close();
    }

    public void PresentationClosing()
    {
        closing = true;
        DismissPicker();
    }

    public void PresentationClosed()
    {
        if (inventory != null && inventory.InventorySystem != null)
        {
            inventory.InventorySystem.OnSlotChanged -= OnInventorySlotChanged;
            inventory.InventorySystem.OnInventoryReset -= Refresh;
        }
        partySlot = null;
        inventory = null;
        ownerId = characterId = null;
        IsOpen = closing = false;
    }

    void SetVisible(bool visible)
    {
        screenGroup.alpha = visible ? 1 : 0;
        screenGroup.blocksRaycasts = visible;
        screenGroup.interactable = visible && CanInteract;
    }

    void OnDisable()
    {
        if (presentation != null && presentation.Owns(this)) presentation.Close(true);
    }

    void OnDestroy()
    {
        if (picker != null) picker.OnLeftClick -= OnPickerItemClicked;
    }

    public static string BuildEffectSummary(InventorySlotData data)
    {
        var definition = data?.item as AccessoryDefinition;
        if (definition == null) return string.Empty;
        var lines = new List<string>();
        if (definition.statModifiers != null)
            foreach (var modifier in definition.statModifiers)
                if (modifier != null) lines.Add(AccessoryDisplayNameResolver.FormatStatModifierLine(modifier));
        if (definition.passives != null)
            foreach (var passive in definition.passives)
                if (passive != null) lines.Add(string.IsNullOrWhiteSpace(passive.description) ? "Passive effect" : passive.description);
        var rolled = definition.GetModifierById(data.accessoryInstance?.modifierId);
        string extra = AccessoryDisplayNameResolver.BuildModifierEffectSummary(rolled);
        if (!string.IsNullOrWhiteSpace(extra)) lines.AddRange(extra.Split('\n'));
        if (lines.Count == 0) return definition.description ?? string.Empty;
        return string.Join("\n", lines.GetRange(0, Mathf.Min(2, lines.Count))) + (lines.Count > 2 ? " …" : "");
    }
}
