using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BasementAccessoryScreenController : MonoBehaviour
{
    [SerializeField] private CameraManager cameraManager;
    [SerializeField] private CharacterSelectManager characterSelection;
    [SerializeField] private CanvasGroup screenGroup;
    [SerializeField, Min(0f)] private float fadeInDuration = 0.5f;
    [SerializeField, Min(0f)] private float otherCharactersFadeDuration = 0.45f;
    [SerializeField] private GameObject slotArtwork;
    [SerializeField] private BasementAccessorySlotView[] slots;
    [SerializeField] private UIEquipment picker;
    [SerializeField] private GameObject pickerPanel;
    [SerializeField] private TMP_Text pickerTitle;
    [SerializeField] private TMP_Text pickerMessage;
    [SerializeField] private Button unequipButton;
    [SerializeField] private GameObject[] backgroundRoots;
    [SerializeField] private RectTransform[] letterboxBars;
    readonly List<(Behaviour component, bool enabled)> hiddenUI = new();
    readonly List<BasementAccessoryRendererFade> hiddenCharacters = new();
    float otherCharactersHiddenAmount;
    PartySlot partySlot;
    PlayerInventory inventory;
    string ownerId;
    string characterId;
    bool selectionWasEnabled;
    bool closing;
    bool presentationReady;
    int selectedSlot = -1;
    Coroutine closeRoutine;
    Coroutine openRoutine;

    public bool IsOpen { get; private set; }
    public bool IsPickerOpen => IsOpen && selectedSlot >= 0;
    public bool CanInteract => IsOpen && presentationReady && !closing && cameraManager != null && !cameraManager.IsAccessoryTransitioning;
    public event Action EquipmentChanged;

    void Awake()
    {
        SetVisible(false);
        pickerPanel.SetActive(false);
        picker.OnLeftClick += OnPickerItemClicked;
    }

    public void Open(PartySlot slot, PlayerInventory source)
    {
        if (IsOpen || closing || slot == null || slot.Selected == null || source == null ||
            string.IsNullOrWhiteSpace(slot.Selected.characterId) || cameraManager == null)
            return;
        if (!cameraManager.BeginAccessoryView(slot.PresentationRoot)) return;

        partySlot = slot;
        inventory = source;
        characterId = slot.Selected.characterId;
        ownerId = CharacterEquipment.BuildCharacterOwnerId(characterId);
        IsOpen = true;
        selectedSlot = -1;
        partySlot.SelectedChanged += OnCharacterChanged;
        if (inventory.InventorySystem != null)
        {
            inventory.InventorySystem.OnSlotChanged += OnInventorySlotChanged;
            inventory.InventorySystem.OnInventoryReset += Refresh;
        }
        hiddenUI.Clear();
        foreach (var root in backgroundRoots)
        {
            if (root == null) continue;
            foreach (var canvas in root.GetComponentsInChildren<Canvas>(true)) HideUI(canvas);
            foreach (var raycaster in root.GetComponentsInChildren<GraphicRaycaster>(true)) HideUI(raycaster);
        }
        hiddenCharacters.Clear();
        otherCharactersHiddenAmount = 0f;
        foreach (var member in FindObjectsByType<PartySlot>(FindObjectsSortMode.None))
            if (member != slot)
                foreach (var renderer in member.GetComponentsInChildren<Renderer>(true))
                {
                    hiddenCharacters.Add(new BasementAccessoryRendererFade(renderer));
                }
        if (characterSelection != null)
        {
            selectionWasEnabled = characterSelection.enabled;
            characterSelection.CancelDrag();
            characterSelection.enabled = false;
        }
        pickerPanel.SetActive(false);
        slotArtwork.SetActive(true);
        presentationReady = false;
        SetVisible(false);
        screenGroup.blocksRaycasts = true;
        Refresh();
        openRoutine = StartCoroutine(RevealWithCamera());
    }

    IEnumerator RevealWithCamera()
    {
        // Let Cinemachine begin its blend before checking whether it has finished.
        yield return null;
        while (cameraManager != null)
        {
            float progress = cameraManager.GetAccessoryRevealProgress(fadeInDuration);
            screenGroup.alpha = Mathf.SmoothStep(0f, 1f, progress);
            if (progress >= 1f && !cameraManager.IsAccessoryTransitioning) break;
            yield return null;
        }
        presentationReady = true;
        SetVisible(true);
        openRoutine = null;
    }

    void CancelReveal()
    {
        if (openRoutine != null) StopCoroutine(openRoutine);
        openRoutine = null;
        presentationReady = false;
    }

    void HideUI(Behaviour component)
    {
        hiddenUI.Add((component, component.enabled));
        component.enabled = false;
    }

    void Update()
    {
        if (!IsOpen) return;
        otherCharactersHiddenAmount = Mathf.MoveTowards(otherCharactersHiddenAmount, closing ? 0f : 1f,
            otherCharactersFadeDuration > 0f ? Time.unscaledDeltaTime / otherCharactersFadeDuration : 1f);
        float eased = Mathf.SmoothStep(0f, 1f, otherCharactersHiddenAmount);
        foreach (var character in hiddenCharacters) character.Apply(eased);
        if (partySlot == null || !partySlot.gameObject.activeInHierarchy || inventory == null)
        {
            Close();
            return;
        }
        screenGroup.interactable = CanInteract;
        if (Input.GetKeyDown(KeyCode.Escape) && CanInteract) Back();
        UpdateLetterbox();
    }

    void UpdateLetterbox()
    {
        Rect r = cameraManager.AccessoryViewport;
        if (letterboxBars == null || letterboxBars.Length != 4) return;
        SetBar(letterboxBars[0], Vector2.zero, new Vector2(r.xMin, 1));
        SetBar(letterboxBars[1], new Vector2(r.xMax, 0), Vector2.one);
        SetBar(letterboxBars[2], new Vector2(r.xMin, 0), new Vector2(r.xMax, r.yMin));
        SetBar(letterboxBars[3], new Vector2(r.xMin, r.yMax), new Vector2(r.xMax, 1));
    }

    static void SetBar(RectTransform bar, Vector2 min, Vector2 max)
    {
        if (bar == null) return;
        bar.anchorMin = min;
        bar.anchorMax = max;
        bar.offsetMin = bar.offsetMax = Vector2.zero;
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
    void OnCharacterChanged(CharacterStats value)
    {
        if (value == null || value.characterId != characterId) Close();
    }

    public void Close()
    {
        if (!IsOpen || closing) return;
        closing = true;
        CancelReveal();
        DismissPicker();
        SetVisible(false);
        cameraManager.EndAccessoryView();
        closeRoutine = StartCoroutine(FinishClose());
    }

    IEnumerator FinishClose()
    {
        while ((cameraManager != null && cameraManager.IsAccessoryTransitioning) || otherCharactersHiddenAmount > 0f)
            yield return null;
        RestoreBackground();
        closeRoutine = null;
    }

    void RestoreBackground()
    {
        if (partySlot != null) partySlot.SelectedChanged -= OnCharacterChanged;
        if (inventory != null && inventory.InventorySystem != null)
        {
            inventory.InventorySystem.OnSlotChanged -= OnInventorySlotChanged;
            inventory.InventorySystem.OnInventoryReset -= Refresh;
        }
        foreach (var state in hiddenUI)
        {
            if (state.component != null) state.component.enabled = state.enabled;
        }
        hiddenUI.Clear();
        foreach (var state in hiddenCharacters)
            state.Dispose();
        hiddenCharacters.Clear();
        otherCharactersHiddenAmount = 0f;
        if (characterSelection != null && IsOpen) characterSelection.enabled = selectionWasEnabled;
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
        if (!IsOpen) return;
        CancelReveal();
        if (closeRoutine != null) StopCoroutine(closeRoutine);
        closeRoutine = null;
        cameraManager?.EndAccessoryView(true);
        DismissPicker();
        RestoreBackground();
        SetVisible(false);
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
