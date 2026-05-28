using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class UIEquipment : InventorySlotOwnerBase
{
    const int FirstEquippedSlotVirtualIndex = -1;

    readonly struct EquippedSlotBinding
    {
        public readonly InventorySlotUI SlotUI;
        public readonly EquipmentItemKind ItemKind;
        public readonly int SlotIndex;

        public EquippedSlotBinding(InventorySlotUI slotUI, EquipmentItemKind itemKind, int slotIndex)
        {
            SlotUI = slotUI;
            ItemKind = itemKind;
            SlotIndex = Mathf.Max(0, slotIndex);
        }
    }

    [Header("Binding")]
    [SerializeField] private PlayerInventory inventorySource;
    [SerializeField] private string characterId;
    [SerializeField] private PartySlot boundPartySlot;
    [SerializeField] private CharacterDatabase characterDatabase;

    [Header("Equipment")]
    [SerializeField] private EquipmentItemKind equipmentKind = EquipmentItemKind.Weapon;
    [SerializeField] private bool useCustomGridItemKind;
    [SerializeField] private EquipmentItemKind gridItemKind = EquipmentItemKind.Weapon;
    [SerializeField] private bool includeAllEquipmentKindsInGrid;
    [SerializeField, Min(0)] private int equippedAccessorySlotIndex;
    [SerializeField] private List<InventorySlotUI> equippedSlotUIs = new();

    [Header("UI References")]
    [SerializeField] private RectTransform slotContainer;
    [SerializeField] private InventorySlotUI slotPrefab;
    [SerializeField] private InventorySlotUI equippedSlotUI;
    [SerializeField] private DragItemUI dragItemUI;
    [SerializeField] private Canvas rootCanvas;

    readonly List<InventorySlotUI> slotUIs = new();
    readonly List<int> equipmentSlotIndices = new();
    readonly List<EquippedSlotBinding> equippedSlotBindings = new();

    InventorySystem inventorySystem;
    string equipmentOwnerId;
    bool hasSourceBinding;
    GameSaveData cachedGameData;
    bool gameDataCacheValid;

    protected override DragItemUI DragVisual => dragItemUI;
    protected override Canvas RootCanvas => rootCanvas;

    public event Action<int, InventorySlotData> OnLeftClick;
    public event Action<int, InventorySlotData> OnRightClick;
    public event Action<int, InventorySlotData> OnPointerEnterSlot;
    public event Action<int, InventorySlotData> OnPointerExitSlot;
    public EquipmentItemKind EquipmentKind => equipmentKind;

    protected override void Awake()
    {
        if (slotContainer == null)
            slotContainer = transform as RectTransform;

        if (rootCanvas == null)
            rootCanvas = GetComponentInParent<Canvas>();

        base.Awake();
    }

    void Start()
    {
        if (hasSourceBinding)
        {
            RefreshAll();
            RefreshBoundPartySlotPreview();
            return;
        }

        if (inventorySource == null)
            inventorySource = ResolveInventorySource();

        BindSource(inventorySource, characterId, boundPartySlot);
    }

    void OnDestroy()
    {
        UnbindCurrentSource();
    }

    void OnDisable()
    {
        RefreshBoundPartySlotPreview();
    }

    public void BindSource(PlayerInventory inventory)
    {
        BindSource(inventory, null);
    }

    public void BindSource(PlayerInventory inventory, string ownerCharacterId)
    {
        BindSource(inventory, ownerCharacterId, null);
    }

    public void BindSource(PlayerInventory inventory, string ownerCharacterId, PartySlot partySlot)
    {
        BindSource(inventory, ownerCharacterId, partySlot, equipmentKind, equippedAccessorySlotIndex);
    }

    public void BindSource(
        PlayerInventory inventory,
        string ownerCharacterId,
        PartySlot partySlot,
        EquipmentItemKind itemKind,
        int equippedSlotIndex = 0)
    {
        UnbindCurrentSource();
        hasSourceBinding = true;

        inventorySource = inventory;
        characterId = ownerCharacterId;
        boundPartySlot = partySlot;
        equipmentKind = itemKind;
        if (!useCustomGridItemKind)
            gridItemKind = itemKind;

        equippedAccessorySlotIndex = Mathf.Max(0, equippedSlotIndex);
        equipmentOwnerId = CharacterEquipment.BuildCharacterOwnerId(characterId);
        inventorySystem = inventorySource != null ? inventorySource.InventorySystem : null;
        if (characterDatabase == null)
            characterDatabase = ResolveCharacterDatabase();

        if (inventorySystem != null)
        {
            inventorySystem.OnSlotChanged += HandleInventoryChanged;
            inventorySystem.OnInventoryReset += HandleInventoryReset;
        }

        if (inventorySource != null && equipmentKind == EquipmentItemKind.Weapon)
            inventorySource.OnEquippedWeaponChanged += HandleEquippedWeaponChanged;

        RefreshAll();
        RefreshBoundPartySlotPreview();
    }

    public void SetEquipmentKind(EquipmentItemKind itemKind, int equippedSlotIndex = 0)
    {
        if (hasSourceBinding)
        {
            BindSource(inventorySource, characterId, boundPartySlot, itemKind, equippedSlotIndex);
            return;
        }

        equipmentKind = itemKind;
        if (!useCustomGridItemKind)
            gridItemKind = itemKind;

        equippedAccessorySlotIndex = Mathf.Max(0, equippedSlotIndex);
        RefreshAll();
        RefreshBoundPartySlotPreview();
    }

    PlayerInventory ResolveInventorySource()
    {
        var fromParent = GetComponentInParent<PlayerInventory>(true);
        if (fromParent != null)
            return fromParent;

        if (transform.root != null)
        {
            var fromRoot = transform.root.GetComponentInChildren<PlayerInventory>(true);
            if (fromRoot != null)
                return fromRoot;
        }

        return FindFirstObjectByType<PlayerInventory>(FindObjectsInactive.Include);
    }

    public void RefreshAll()
    {
        InvalidateGameDataCache();
        RebuildEquipmentSlotIndexCache();
        RebuildSlots();

        for (int i = 0; i < equipmentSlotIndices.Count; i++)
            RefreshSlot(i);

        RefreshEquippedSlots();
    }

    public void RefreshSlot(int slotIndex)
    {
        if (!TryGetInventorySlotIndex(slotIndex, out var inventorySlotIndex))
            return;

        var slotData = inventorySystem.GetSlot(inventorySlotIndex);
        slotUIs[slotIndex].Bind(this, slotIndex, slotData);
        EquipmentItemKind slotItemKind = ResolveSlotItemKind(slotData, out EquipmentItemKind resolvedItemKind)
            ? resolvedItemKind
            : ResolveGridItemKind();
        slotUIs[slotIndex].SetEquippedCharacterIcon(ResolveEquippedCharacterIcon(slotData, slotItemKind));
    }

    public void RefreshEquippedSlot()
    {
        RefreshEquippedSlots();
    }

    public void RefreshEquippedSlots()
    {
        RebuildEquippedSlotBindingCache();

        int equippedSlotCount = GetEquippedSlotDisplayCount();
        for (int i = 0; i < equippedSlotCount; i++)
        {
            EquippedSlotBinding binding = equippedSlotBindings[i];
            InventorySlotUI slotUI = binding.SlotUI;
            if (slotUI == null)
                continue;

            var slotData = GetEquippedSlotData(binding);
            slotUI.Bind(this, ToEquippedSlotVirtualIndex(i), slotData);
            slotUI.SetEquippedCharacterIcon(ResolveEquippedCharacterIcon(slotData, binding.ItemKind));
            slotUI.SetDraggingVisual(false);
        }
    }

    public override void HandleDrop(int targetIndex)
    {
        if (!IsDragging || inventorySystem == null || inventorySource == null)
            return;

        if (TryGetEquippedSlotDisplayIndex(targetIndex, out int equippedSlotDisplayIndex))
        {
            HandleEquipDrop(equippedSlotDisplayIndex);
            return;
        }

        if (!TryGetInventorySlotIndex(DraggingSourceIndex, out var sourceIndex))
            return;

        if (!TryGetInventorySlotIndex(targetIndex, out var targetIndexInInventory))
            return;

        if (sourceIndex == targetIndexInInventory)
            return;

        inventorySystem.MoveOrSwap(sourceIndex, targetIndexInInventory);
    }

    public override void HandleSlotClick(int slotIndex, PointerEventData eventData)
    {
        if (!TryGetBoundSlotData(slotIndex, out var slotData))
            return;

        switch (eventData.button)
        {
            case PointerEventData.InputButton.Left:
                OnLeftClick?.Invoke(slotIndex, slotData);
                break;

            case PointerEventData.InputButton.Right:
                OnRightClick?.Invoke(slotIndex, slotData);
                break;
        }
    }

    public override void HandlePointerEnter(int slotIndex)
    {
        if (!TryGetBoundSlotData(slotIndex, out var slotData))
            return;

        OnPointerEnterSlot?.Invoke(slotIndex, slotData);
    }

    public override void HandlePointerExit(int slotIndex)
    {
        if (!TryGetBoundSlotData(slotIndex, out var slotData))
            return;

        OnPointerExitSlot?.Invoke(slotIndex, slotData);
    }

    void HandleInventoryChanged(int slotIndex)
    {
        RefreshAll();
    }

    void HandleInventoryReset()
    {
        RefreshAll();
    }

    void HandleEquippedWeaponChanged(string equippedInstanceId)
    {
        if (equipmentKind != EquipmentItemKind.Weapon)
            return;

        RefreshAll();
        RefreshBoundPartySlotPreview(string.IsNullOrWhiteSpace(equipmentOwnerId) ? equippedInstanceId : null);
    }

    void HandleEquipDrop(int equippedSlotDisplayIndex)
    {
        if (!TryGetInventorySlotIndex(DraggingSourceIndex, out var inventorySlotIndex))
            return;

        var slotData = inventorySystem.GetSlot(inventorySlotIndex);
        if (!TryGetEquippedSlotBinding(equippedSlotDisplayIndex, out var binding))
            return;

        if (!EquipmentAssignmentService.IsEquipmentSlot(slotData, binding.ItemKind))
            return;

        if (!EquipmentAssignmentService.TryGetInstanceId(slotData, binding.ItemKind, out string instanceId))
            return;

        bool equipped = EquipmentAssignmentService.TryEquip(
            inventorySource,
            binding.ItemKind,
            equipmentOwnerId,
            instanceId,
            binding.SlotIndex);

        if (equipped)
        {
            RefreshAll();
            if (binding.ItemKind == EquipmentItemKind.Weapon)
                RefreshBoundPartySlotPreview(instanceId);
            return;
        }

        Debug.LogWarning($"[UIEquipment] {binding.ItemKind} is already equipped by another character or cannot be equipped.", this);
    }

    void RefreshBoundPartySlotPreview(string preferredEquippedInstanceId = null)
    {
        if (equipmentKind != EquipmentItemKind.Weapon || boundPartySlot == null)
            return;

        if (string.IsNullOrWhiteSpace(preferredEquippedInstanceId))
        {
            InventorySlotData equippedSlotData = GetEquippedSlotData(0);
            if (!EquipmentAssignmentService.TryGetInstanceId(
                    equippedSlotData,
                    EquipmentItemKind.Weapon,
                    out preferredEquippedInstanceId))
            {
                preferredEquippedInstanceId = null;
            }
        }

        boundPartySlot.RefreshSelectedWeaponVisual(preferredEquippedInstanceId, inventorySource);
    }

    void RebuildEquipmentSlotIndexCache()
    {
        equipmentSlotIndices.Clear();

        if (inventorySystem == null)
            return;

        for (int i = 0; i < inventorySystem.Slots.Count; i++)
        {
            var slot = inventorySystem.GetSlot(i);
            if (ShouldShowInGrid(slot))
                equipmentSlotIndices.Add(i);
        }
    }

    bool ShouldShowInGrid(InventorySlotData slotData)
    {
        if (includeAllEquipmentKindsInGrid)
        {
            return EquipmentAssignmentService.IsEquipmentSlot(slotData, EquipmentItemKind.Weapon) ||
                   EquipmentAssignmentService.IsEquipmentSlot(slotData, EquipmentItemKind.Accessory);
        }

        return EquipmentAssignmentService.IsEquipmentSlot(slotData, ResolveGridItemKind());
    }

    EquipmentItemKind ResolveGridItemKind()
    {
        return useCustomGridItemKind ? gridItemKind : equipmentKind;
    }

    static bool ResolveSlotItemKind(InventorySlotData slotData, out EquipmentItemKind itemKind)
    {
        itemKind = EquipmentItemKind.Weapon;

        if (slotData == null || slotData.IsEmpty)
            return false;

        if (slotData.HasAccessoryInstance ||
            (slotData.item != null && slotData.item.itemType == ItemType.Accessory))
        {
            itemKind = EquipmentItemKind.Accessory;
            return true;
        }

        if (slotData.HasWeaponInstance ||
            (slotData.item != null && slotData.item.itemType == ItemType.Weapon))
        {
            itemKind = EquipmentItemKind.Weapon;
            return true;
        }

        return false;
    }

    void RebuildSlots()
    {
        int targetCount = equipmentSlotIndices.Count;

        if (slotPrefab == null || slotContainer == null)
            return;

        while (slotUIs.Count < targetCount)
        {
            var slotUI = Instantiate(slotPrefab, slotContainer);
            slotUI.name = $"{slotPrefab.name}_{slotUIs.Count:00}";
            slotUIs.Add(slotUI);
        }

        while (slotUIs.Count > targetCount)
        {
            int lastIndex = slotUIs.Count - 1;
            var slotUI = slotUIs[lastIndex];
            slotUIs.RemoveAt(lastIndex);

            if (Application.isPlaying)
                Destroy(slotUI.gameObject);
            else
                DestroyImmediate(slotUI.gameObject);
        }
    }

    void UnbindCurrentSource()
    {
        if (inventorySource != null)
            inventorySource.OnEquippedWeaponChanged -= HandleEquippedWeaponChanged;

        if (inventorySystem == null)
            return;

        inventorySystem.OnSlotChanged -= HandleInventoryChanged;
        inventorySystem.OnInventoryReset -= HandleInventoryReset;
        inventorySystem = null;
    }

    bool TryGetInventorySlotIndex(int displaySlotIndex, out int inventorySlotIndex)
    {
        inventorySlotIndex = -1;

        if (inventorySystem == null ||
            displaySlotIndex < 0 ||
            displaySlotIndex >= equipmentSlotIndices.Count ||
            displaySlotIndex >= slotUIs.Count)
        {
            return false;
        }

        inventorySlotIndex = equipmentSlotIndices[displaySlotIndex];
        return inventorySlotIndex >= 0 && inventorySlotIndex < inventorySystem.Slots.Count;
    }

    bool TryGetBoundSlotData(int slotIndex, out InventorySlotData slotData)
    {
        slotData = null;

        if (TryGetEquippedSlotDisplayIndex(slotIndex, out int equippedSlotDisplayIndex))
        {
            slotData = GetEquippedSlotData(equippedSlotDisplayIndex);
            return slotData != null && !slotData.IsEmpty;
        }

        if (!TryGetInventorySlotIndex(slotIndex, out var inventorySlotIndex))
            return false;

        slotData = inventorySystem.GetSlot(inventorySlotIndex);
        return slotData != null && !slotData.IsEmpty;
    }

    InventorySlotData GetEquippedSlotData(int equippedSlotDisplayIndex)
    {
        if (!TryGetEquippedSlotBinding(equippedSlotDisplayIndex, out var binding))
            return new InventorySlotData();

        return GetEquippedSlotData(binding);
    }

    InventorySlotData GetEquippedSlotData(EquippedSlotBinding binding)
    {
        return EquipmentAssignmentService.GetEquippedSlotData(
            inventorySource,
            binding.ItemKind,
            equipmentOwnerId,
            binding.SlotIndex);
    }

    Sprite ResolveEquippedCharacterIcon(InventorySlotData slotData, EquipmentItemKind itemKind)
    {
        string ownerId = EquipmentAssignmentService.FindAssignedOwnerId(inventorySource, itemKind, slotData);
        CharacterStats ownerStats = ResolveOwnerCharacterStats(ownerId);
        return ownerStats != null ? ownerStats.icon : null;
    }

    int GetEquippedSlotDisplayCount()
    {
        RebuildEquippedSlotBindingCache();
        return equippedSlotBindings.Count;
    }

    void RebuildEquippedSlotBindingCache()
    {
        equippedSlotBindings.Clear();

        bool hasEquippedSlotList = equippedSlotUIs != null && equippedSlotUIs.Count > 0;

        if (equipmentKind == EquipmentItemKind.Weapon)
        {
            if (equippedSlotUI != null)
            {
                AddEquippedSlotBinding(equippedSlotUI, EquipmentItemKind.Weapon, 0);
                AddAccessoryEquippedSlotBindings();
            }
            else if (hasEquippedSlotList)
            {
                AddEquippedSlotBinding(equippedSlotUIs[0], EquipmentItemKind.Weapon, 0);
            }

            return;
        }

        if (hasEquippedSlotList)
        {
            AddAccessoryEquippedSlotBindings();
            return;
        }

        AddEquippedSlotBinding(equippedSlotUI, EquipmentItemKind.Accessory, equippedAccessorySlotIndex);
    }

    void AddAccessoryEquippedSlotBindings()
    {
        if (equippedSlotUIs == null)
            return;

        for (int i = 0; i < equippedSlotUIs.Count; i++)
            AddEquippedSlotBinding(equippedSlotUIs[i], EquipmentItemKind.Accessory, i);
    }

    void AddEquippedSlotBinding(InventorySlotUI slotUI, EquipmentItemKind itemKind, int slotIndex)
    {
        if (slotUI == null)
            return;

        equippedSlotBindings.Add(new EquippedSlotBinding(slotUI, itemKind, slotIndex));
    }

    bool TryGetEquippedSlotBinding(int equippedSlotDisplayIndex, out EquippedSlotBinding binding)
    {
        RebuildEquippedSlotBindingCache();

        if (equippedSlotDisplayIndex >= 0 && equippedSlotDisplayIndex < equippedSlotBindings.Count)
        {
            binding = equippedSlotBindings[equippedSlotDisplayIndex];
            return true;
        }

        binding = default;
        return false;
    }

    static int ToEquippedSlotVirtualIndex(int equippedSlotDisplayIndex)
    {
        return FirstEquippedSlotVirtualIndex - Mathf.Max(0, equippedSlotDisplayIndex);
    }

    bool TryGetEquippedSlotDisplayIndex(int slotIndex, out int equippedSlotDisplayIndex)
    {
        equippedSlotDisplayIndex = -1;

        if (slotIndex > FirstEquippedSlotVirtualIndex)
            return false;

        int resolvedDisplayIndex = FirstEquippedSlotVirtualIndex - slotIndex;
        if (resolvedDisplayIndex < 0 || resolvedDisplayIndex >= GetEquippedSlotDisplayCount())
            return false;

        equippedSlotDisplayIndex = resolvedDisplayIndex;
        return true;
    }

    CharacterStats ResolveOwnerCharacterStats(string ownerId)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
            return null;

        if (CharacterEquipment.TryParseCharacterOwnerId(ownerId, out string characterOwnerId))
            return ResolveCharacterStats(characterOwnerId);

        if (string.Equals(ownerId, "player", StringComparison.Ordinal))
            return ResolvePartyCharacterStats(0);

        if (string.Equals(ownerId, "helper", StringComparison.Ordinal))
            return ResolveChainRoleCharacterStats(ChainActorRole.Helper);

        const string AllyOwnerPrefix = "ally:";
        if (ownerId.StartsWith(AllyOwnerPrefix, StringComparison.Ordinal) &&
            Enum.TryParse(ownerId.Substring(AllyOwnerPrefix.Length), out ChainActorRole role))
        {
            return ResolveChainRoleCharacterStats(role);
        }

        return null;
    }

    CharacterStats ResolveChainRoleCharacterStats(ChainActorRole role)
    {
        switch (role)
        {
            case ChainActorRole.Player:
                return ResolvePartyCharacterStats(0);
            case ChainActorRole.PartySlot1:
                return ResolvePartyCharacterStats(1);
            case ChainActorRole.PartySlot2:
                return ResolvePartyCharacterStats(2);
            default:
                return null;
        }
    }

    CharacterStats ResolvePartyCharacterStats(int partyIndex)
    {
        if (partyIndex < 0)
            return null;

        PartySlot partySlot = FindPartySlot(partyIndex);
        CharacterStats fromSlot = partySlot != null ? partySlot.Selected : null;
        if (fromSlot != null)
            return fromSlot;

        string slotCharacterId = partySlot != null ? partySlot.IDCharacter : null;
        if (!string.IsNullOrWhiteSpace(slotCharacterId))
            return ResolveCharacterStats(slotCharacterId);

        var data = GetCurrentGameData();
        if (data?.party?.partyIds == null || partyIndex >= data.party.partyIds.Count)
            return null;

        return ResolveCharacterStats(data.party.partyIds[partyIndex]);
    }

    CharacterStats ResolveCharacterStats(string ownerCharacterId)
    {
        if (string.IsNullOrWhiteSpace(ownerCharacterId))
            return null;

        if (boundPartySlot != null &&
            boundPartySlot.Selected != null &&
            string.Equals(boundPartySlot.Selected.characterId, ownerCharacterId, StringComparison.Ordinal))
        {
            return boundPartySlot.Selected;
        }

        PartySlot partySlot = FindPartySlot(ownerCharacterId);
        if (partySlot != null && partySlot.Selected != null)
            return partySlot.Selected;

        if (characterDatabase == null)
            characterDatabase = ResolveCharacterDatabase();

        CharacterStats fromDatabase = characterDatabase != null ? characterDatabase.GetById(ownerCharacterId) : null;
        if (fromDatabase != null)
            return fromDatabase;

        CharacterDatabase[] databases = Resources.FindObjectsOfTypeAll<CharacterDatabase>();
        for (int i = 0; i < databases.Length; i++)
        {
            CharacterDatabase database = databases[i];
            if (database == null || database == characterDatabase)
                continue;

            CharacterStats candidate = database.GetById(ownerCharacterId);
            if (candidate != null)
            {
                characterDatabase = database;
                return candidate;
            }
        }

        return null;
    }

    PartySlot FindPartySlot(int partyIndex)
    {
        PartySlot[] partySlots = FindObjectsByType<PartySlot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        PartySlot fallback = null;
        int saveSlot = SaveManager.Instance != null ? SaveManager.Instance.currentSlot : 0;

        for (int i = 0; i < partySlots.Length; i++)
        {
            PartySlot partySlot = partySlots[i];
            if (partySlot == null || partySlot.PartyIndex != partyIndex)
                continue;

            if (partySlot.CurrentSlot == saveSlot)
                return partySlot;

            fallback ??= partySlot;
        }

        return fallback;
    }

    PartySlot FindPartySlot(string ownerCharacterId)
    {
        if (string.IsNullOrWhiteSpace(ownerCharacterId))
            return null;

        PartySlot[] partySlots = FindObjectsByType<PartySlot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < partySlots.Length; i++)
        {
            PartySlot partySlot = partySlots[i];
            if (partySlot == null)
                continue;

            string selectedId = partySlot.Selected != null ? partySlot.Selected.characterId : partySlot.IDCharacter;
            if (string.Equals(selectedId, ownerCharacterId, StringComparison.Ordinal))
                return partySlot;
        }

        return null;
    }

    GameSaveData GetCurrentGameData()
    {
        if (!gameDataCacheValid)
        {
            cachedGameData = LoadCurrentGameData();
            gameDataCacheValid = true;
        }

        return cachedGameData;
    }

    void InvalidateGameDataCache()
    {
        cachedGameData = null;
        gameDataCacheValid = false;
    }

    static CharacterDatabase ResolveCharacterDatabase()
    {
        CharacterDatabase[] databases = Resources.FindObjectsOfTypeAll<CharacterDatabase>();
        for (int i = 0; i < databases.Length; i++)
        {
            CharacterDatabase candidate = databases[i];
            if (candidate != null && candidate.characters != null && candidate.characters.Count > 0)
                return candidate;
        }

        return null;
    }

    static GameSaveData LoadCurrentGameData()
    {
        int saveSlot = SaveManager.Instance != null ? SaveManager.Instance.currentSlot : 0;
        GameSaveData data = SaveSystem.LoadGame(saveSlot);
        PartyData party = SaveSystem.LoadPartyOnly(saveSlot);
        if (party != null)
        {
            data ??= new GameSaveData();
            data.party = party;
        }

        return data;
    }

    protected override bool TryGetSlotDataForDrag(int slotIndex, out InventorySlotData slotData)
    {
        slotData = null;

        if (TryGetEquippedSlotDisplayIndex(slotIndex, out _))
            return false;

        return TryGetBoundSlotData(slotIndex, out slotData);
    }

    protected override void SetDraggingVisual(int slotIndex, bool isDragging)
    {
        if (!TryGetInventorySlotIndex(slotIndex, out _))
            return;

        slotUIs[slotIndex].SetDraggingVisual(isDragging);
    }

    protected override void RefreshSlotVisual(int slotIndex)
    {
        if (TryGetEquippedSlotDisplayIndex(slotIndex, out _))
        {
            RefreshEquippedSlots();
            return;
        }

        RefreshSlot(slotIndex);
    }
}
