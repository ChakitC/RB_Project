using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-145)]
[DisallowMultipleComponent]
public sealed class AccessoryLoadout : MonoBehaviour, IStatModifierProvider, IPassiveDefinitionProvider
{
    [Header("Refs")]
    [SerializeField] private CharacteContext ctx;
    [SerializeField] private StatsHub statsHub;
    [SerializeField] private PassiveController passiveController;
    [SerializeField] private ItemDatabase itemDatabase;

    [Header("Slots")]
    [SerializeField, Min(0)] private int slotCount = 5;
    [SerializeField] private List<AccessoryInstanceData> equippedAccessories = new();

    [Header("Shared Inventory Save")]
    [SerializeField] private bool usePlayerInventorySave = true;
    [SerializeField] private string equipmentOwnerId;

    public CharacteContext Context => ctx;
    public int SlotCount => slotCount;
    public string OwnerId => ResolveOwnerId();
    public bool UsesPlayerInventorySave => ShouldUsePlayerInventorySave();
    public IReadOnlyList<AccessoryInstanceData> EquippedAccessories => equippedAccessories;

    public event Action OnLoadoutChanged;

    void Awake()
    {
        ResolveReferences();
        EnsureSlotCount();
    }

    void OnEnable()
    {
        ResolveReferences();
        EnsureSlotCount();
        RefreshRuntime();
    }

    void OnDisable()
    {
        RefreshRuntime();
    }

    void OnValidate()
    {
        slotCount = Mathf.Max(0, slotCount);
        EnsureSlotCount();
    }

    public void ResolveReferences()
    {
        if (!ctx)
            TryGetComponent(out ctx);

        if (!ctx)
            ctx = GetComponentInParent<CharacteContext>();

        ctx?.ResolveReferences();

        if (ctx != null && ctx.AccessoryLoadout != this)
            ctx.AccessoryLoadout = this;

        if (!statsHub && ctx != null)
            statsHub = ctx.StatsHub;
        if (!statsHub)
            TryGetComponent(out statsHub);
        if (!statsHub && ctx != null)
            statsHub = ctx.GetComponentInChildren<StatsHub>(true);

        if (!passiveController && ctx != null)
            passiveController = ctx.PassiveController;
        if (!passiveController)
            TryGetComponent(out passiveController);
        if (!passiveController && ctx != null)
            passiveController = ctx.GetComponentInChildren<PassiveController>(true);

        if (!itemDatabase)
        {
            PlayerInventory inventory = ResolvePlayerInventory();
            if (inventory != null)
                itemDatabase = inventory.itemDatabase;
        }
    }

    public void SetPlayerInventorySaveParticipation(bool participates)
    {
        usePlayerInventorySave = participates;
    }

    public void SetSlotCount(int value)
    {
        int resolvedCount = Mathf.Max(0, value);
        if (slotCount == resolvedCount)
            return;

        slotCount = resolvedCount;
        EnsureSlotCount();
        RefreshRuntime();
    }

    public bool TryEquipFromInventory(PlayerInventory inventory, string instanceId, int slotIndex = -1)
    {
        if (inventory == null || string.IsNullOrWhiteSpace(instanceId))
            return false;

        if (!inventory.TryGetAccessoryInstanceWithDefinition(instanceId, out AccessoryDefinition definition, out AccessoryInstanceData instance))
            return false;

        return TryEquip(definition, instance, slotIndex);
    }

    public bool TryEquip(AccessoryDefinition definition, AccessoryInstanceData instance, int slotIndex = -1)
    {
        ResolveReferences();
        EnsureSlotCount();

        if (!definition || instance == null || instance.IsEmpty)
            return false;

        if (string.IsNullOrWhiteSpace(instance.instanceId))
            instance.instanceId = AccessoryInstanceFactory.CreateInstanceId();

        if (string.IsNullOrWhiteSpace(instance.accessoryId))
            instance.accessoryId = AccessoryInstanceFactory.ResolveAccessoryId(definition);

        if (!definition.CanEquip(ctx))
            return false;

        string ownerId = ResolveOwnerId();
        if (IsAccessoryInstanceUnavailable(instance.instanceId, ownerId, this))
            return false;

        int existingIndex = FindEquippedIndex(instance.instanceId);
        if (slotIndex < 0 && existingIndex >= 0)
            return true;

        int targetIndex = ResolveTargetSlot(slotIndex);
        if (targetIndex < 0)
            return false;

        if (existingIndex >= 0 && existingIndex != targetIndex)
            equippedAccessories[existingIndex] = null;

        equippedAccessories[targetIndex] = instance.DeepClone();
        RefreshRuntime();
        return true;
    }

    public bool UnequipSlot(int slotIndex)
    {
        EnsureSlotCount();
        if (!IsValidSlotIndex(slotIndex) || IsSlotEmpty(slotIndex))
            return false;

        equippedAccessories[slotIndex] = null;
        RefreshRuntime();
        return true;
    }

    public void ClearAll()
    {
        EnsureSlotCount();
        bool changed = false;

        for (int i = 0; i < equippedAccessories.Count; i++)
        {
            if (equippedAccessories[i] == null || equippedAccessories[i].IsEmpty)
                continue;

            equippedAccessories[i] = null;
            changed = true;
        }

        if (changed)
            RefreshRuntime();
    }

    public AccessoryInstanceData GetAccessoryInstance(int slotIndex)
    {
        EnsureSlotCount();
        return IsValidSlotIndex(slotIndex) ? equippedAccessories[slotIndex] : null;
    }

    public AccessoryDefinition GetAccessoryDefinition(int slotIndex)
    {
        return ResolveAccessoryDefinition(GetAccessoryInstance(slotIndex));
    }

    public InventorySlotData GetSlotData(int slotIndex)
    {
        var slotData = new InventorySlotData();
        AccessoryInstanceData instance = GetAccessoryInstance(slotIndex);
        AccessoryDefinition definition = ResolveAccessoryDefinition(instance);
        if (definition != null && instance != null)
            slotData.SetAccessoryInstance(definition, instance);

        return slotData;
    }

    public void AppendStatModifiers(List<RuntimeStatModifier> buffer)
    {
        if (!isActiveAndEnabled || buffer == null || equippedAccessories == null)
            return;

        for (int i = 0; i < equippedAccessories.Count; i++)
        {
            AccessoryInstanceData instance = equippedAccessories[i];
            AccessoryDefinition definition = ResolveAccessoryDefinition(instance);
            if (definition == null || instance == null)
                continue;

            AppendStatModifiers(buffer, definition.statModifiers, BuildSourceId(instance, "base"));

            AccessoryModifierDefinition modifier = definition.GetModifierById(instance.modifierId);
            if (modifier != null)
                AppendStatModifiers(buffer, modifier.statModifiers, BuildSourceId(instance, modifier.RuntimeId));
        }
    }

    public void AppendPassiveDefinitions(List<PassiveDefinition> buffer)
    {
        if (!isActiveAndEnabled || buffer == null || equippedAccessories == null)
            return;

        for (int i = 0; i < equippedAccessories.Count; i++)
        {
            AccessoryInstanceData instance = equippedAccessories[i];
            AccessoryDefinition definition = ResolveAccessoryDefinition(instance);
            if (definition == null || instance == null)
                continue;

            AppendPassives(buffer, definition.passives);

            AccessoryModifierDefinition modifier = definition.GetModifierById(instance.modifierId);
            if (modifier != null)
                AppendPassives(buffer, modifier.passives);
        }
    }

    public static void WriteSceneLoadoutsToSave(GameSaveData data, PlayerInventory inventory = null)
    {
        if (data == null)
            return;

        data.accessories ??= LoadPersistedAccessoryData() ?? new AccessoryLoadoutSaveData();

        var loadouts = new List<AccessoryLoadout>();
        GatherSceneLoadouts(loadouts);

        var usedInstanceIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < loadouts.Count; i++)
        {
            AccessoryLoadout loadout = loadouts[i];
            if (loadout == null || !loadout.UsesPlayerInventorySave)
                continue;

            string ownerId = loadout.ResolveOwnerId();
            if (string.IsNullOrWhiteSpace(ownerId))
                continue;

            CharacterAccessoryLoadoutSaveData entry = loadout.CreateSaveEntry(inventory, usedInstanceIds);
            RemoveEntryInstanceAssignments(data.accessories, entry, ownerId);
            UpsertLoadoutEntry(data.accessories, ownerId, entry);
        }
    }

    public static void ApplySceneLoadoutsFromInventory(GameSaveData data, PlayerInventory inventory)
    {
        if (inventory == null)
            return;

        var loadouts = new List<AccessoryLoadout>();
        GatherSceneLoadouts(loadouts);

        var usedInstanceIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < loadouts.Count; i++)
        {
            AccessoryLoadout loadout = loadouts[i];
            if (loadout == null || !loadout.UsesPlayerInventorySave)
                continue;

            CharacterAccessoryLoadoutSaveData entry = loadout.FindSavedEntryWithFallback(data?.accessories);
            if (entry == null)
                continue;

            loadout.ApplySaveEntry(entry, inventory, usedInstanceIds);
        }
    }

    public static void ReleaseRemovedInventoryAccessory(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
            return;

        var loadouts = new List<AccessoryLoadout>();
        GatherSceneLoadouts(loadouts);

        for (int i = 0; i < loadouts.Count; i++)
        {
            AccessoryLoadout loadout = loadouts[i];
            if (loadout == null || !loadout.UsesPlayerInventorySave)
                continue;

            if (loadout.UnequipInstance(instanceId))
                loadout.RefreshRuntime();
        }
    }

    public static bool IsAccessoryInstanceUnavailable(string instanceId, string requesterOwnerId, AccessoryLoadout requester)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
            return false;

        return IsAccessoryInstanceEquippedByOther(instanceId, requesterOwnerId, requester) ||
               IsAccessoryInstanceAssignedInSave(instanceId, requesterOwnerId, requester);
    }

    public static bool IsAccessoryInstanceEquippedByOther(string instanceId, string requesterOwnerId, AccessoryLoadout requester)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
            return false;

        var loadouts = new List<AccessoryLoadout>();
        GatherSceneLoadouts(loadouts);

        for (int i = 0; i < loadouts.Count; i++)
        {
            AccessoryLoadout loadout = loadouts[i];
            if (loadout == null || loadout == requester || !loadout.UsesPlayerInventorySave)
                continue;

            if (!loadout.ContainsInstance(instanceId))
                continue;

            string ownerId = loadout.ResolveOwnerId();
            if (IsSameLoadoutOwner(ownerId, requesterOwnerId, requester))
                continue;

            string legacyOwnerId = loadout.ResolveLegacyOwnerId();
            if (IsSameLoadoutOwner(legacyOwnerId, requesterOwnerId, requester))
                continue;

            return true;
        }

        return false;
    }

    public static bool IsAccessoryInstanceAssignedInSave(string instanceId, string exceptOwnerId, AccessoryLoadout requester = null)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
            return false;

        int saveSlot = SaveManager.Instance != null ? SaveManager.Instance.currentSlot : 0;
        var data = SaveSystem.LoadGame(saveSlot);
        var entries = data?.accessories?.entries;
        if (entries == null)
            return false;

        for (int i = 0; i < entries.Count; i++)
        {
            CharacterAccessoryLoadoutSaveData entry = entries[i];
            if (entry == null || IsSameLoadoutOwner(entry.ownerId, exceptOwnerId, requester))
                continue;

            if (EntryContainsInstance(entry, instanceId))
                return true;
        }

        return false;
    }

    public static bool TryFindSceneLoadoutByAccessoryInstance(string instanceId, out AccessoryLoadout loadout)
    {
        loadout = null;

        if (string.IsNullOrWhiteSpace(instanceId))
            return false;

        var loadouts = new List<AccessoryLoadout>();
        GatherSceneLoadouts(loadouts);

        for (int i = 0; i < loadouts.Count; i++)
        {
            AccessoryLoadout candidate = loadouts[i];
            if (candidate == null || !candidate.ContainsInstance(instanceId))
                continue;

            loadout = candidate;
            return true;
        }

        return false;
    }

    public static bool TryFindSceneLoadoutByOwner(string ownerId, out AccessoryLoadout loadout)
    {
        loadout = null;

        if (string.IsNullOrWhiteSpace(ownerId))
            return false;

        var loadouts = new List<AccessoryLoadout>();
        GatherSceneLoadouts(loadouts);

        for (int i = 0; i < loadouts.Count; i++)
        {
            AccessoryLoadout candidate = loadouts[i];
            if (candidate == null || !candidate.UsesPlayerInventorySave)
                continue;

            if (string.Equals(candidate.ResolveOwnerId(), ownerId, StringComparison.Ordinal) ||
                string.Equals(candidate.ResolveLegacyOwnerId(), ownerId, StringComparison.Ordinal))
            {
                loadout = candidate;
                return true;
            }
        }

        return false;
    }

    public static bool SaveLoadoutAssignment(
        string ownerId,
        string instanceId,
        int slotIndex,
        PlayerInventory inventory = null)
    {
        if (string.IsNullOrWhiteSpace(ownerId) || string.IsNullOrWhiteSpace(instanceId))
            return false;

        int targetSlotIndex = Mathf.Max(0, slotIndex);

        if (inventory == null ||
            !inventory.TryGetAccessoryInstanceWithDefinition(
                instanceId,
                out _,
                out AccessoryInstanceData inventoryInstance))
        {
            return false;
        }

        if (IsAccessoryInstanceUnavailable(instanceId, ownerId, null))
            return false;

        int saveSlot = SaveManager.Instance != null ? SaveManager.Instance.currentSlot : 0;
        GameSaveData data = SaveSystem.LoadGame(saveSlot) ?? new GameSaveData();
        data.accessories ??= new AccessoryLoadoutSaveData();
        data.accessories.entries ??= new List<CharacterAccessoryLoadoutSaveData>();

        RemoveInstanceAssignments(data.accessories, instanceId, ownerId);

        CharacterAccessoryLoadoutSaveData entry = FindLoadoutEntry(data.accessories, ownerId);
        if (entry == null)
        {
            entry = new CharacterAccessoryLoadoutSaveData
            {
                ownerId = ownerId
            };
            data.accessories.entries.Add(entry);
        }

        entry.slotCount = Mathf.Max(entry.slotCount, targetSlotIndex + 1);
        EnsureSavedAccessorySlots(entry, targetSlotIndex + 1);

        for (int i = 0; i < entry.equippedAccessories.Count; i++)
        {
            AccessoryInstanceData assignedInstance = entry.equippedAccessories[i];
            if (assignedInstance == null)
                continue;

            if (string.Equals(assignedInstance.instanceId, instanceId, StringComparison.Ordinal))
                entry.equippedAccessories[i] = null;
        }

        entry.equippedAccessories[targetSlotIndex] = inventoryInstance.DeepClone();
        SaveSystem.SaveGame(data, saveSlot);
        return true;
    }

    public static CharacterAccessoryLoadoutSaveData FindLoadoutEntry(AccessoryLoadoutSaveData data, string ownerId)
    {
        if (data?.entries == null || string.IsNullOrWhiteSpace(ownerId))
            return null;

        for (int i = 0; i < data.entries.Count; i++)
        {
            CharacterAccessoryLoadoutSaveData entry = data.entries[i];
            if (entry == null)
                continue;

            if (string.Equals(entry.ownerId, ownerId, StringComparison.Ordinal))
                return entry;
        }

        return null;
    }

    static void EnsureSavedAccessorySlots(CharacterAccessoryLoadoutSaveData entry, int minSlotCount)
    {
        if (entry == null)
            return;

        entry.equippedAccessories ??= new List<AccessoryInstanceData>();
        entry.slotCount = Mathf.Max(entry.slotCount, minSlotCount);

        while (entry.equippedAccessories.Count < entry.slotCount)
            entry.equippedAccessories.Add(null);

        while (entry.equippedAccessories.Count > entry.slotCount)
            entry.equippedAccessories.RemoveAt(entry.equippedAccessories.Count - 1);
    }

    void EnsureSlotCount()
    {
        equippedAccessories ??= new List<AccessoryInstanceData>();

        while (equippedAccessories.Count < slotCount)
            equippedAccessories.Add(null);

        while (equippedAccessories.Count > slotCount)
            equippedAccessories.RemoveAt(equippedAccessories.Count - 1);
    }

    int ResolveTargetSlot(int requestedSlot)
    {
        if (requestedSlot >= 0)
            return IsValidSlotIndex(requestedSlot) ? requestedSlot : -1;

        for (int i = 0; i < equippedAccessories.Count; i++)
        {
            if (IsSlotEmpty(i))
                return i;
        }

        return -1;
    }

    bool IsValidSlotIndex(int slotIndex)
    {
        return slotIndex >= 0 && slotIndex < equippedAccessories.Count;
    }

    bool IsSlotEmpty(int slotIndex)
    {
        if (!IsValidSlotIndex(slotIndex))
            return true;

        AccessoryInstanceData instance = equippedAccessories[slotIndex];
        return instance == null || instance.IsEmpty;
    }

    int FindEquippedIndex(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId) || equippedAccessories == null)
            return -1;

        for (int i = 0; i < equippedAccessories.Count; i++)
        {
            AccessoryInstanceData instance = equippedAccessories[i];
            if (instance == null)
                continue;

            if (string.Equals(instance.instanceId, instanceId, StringComparison.Ordinal))
                return i;
        }

        return -1;
    }

    bool ContainsInstance(string instanceId)
    {
        return FindEquippedIndex(instanceId) >= 0;
    }

    bool UnequipInstance(string instanceId)
    {
        int index = FindEquippedIndex(instanceId);
        if (index < 0)
            return false;

        equippedAccessories[index] = null;
        return true;
    }

    AccessoryDefinition ResolveAccessoryDefinition(AccessoryInstanceData instance)
    {
        if (instance == null || instance.IsEmpty)
            return null;

        if (itemDatabase != null)
        {
            var fromDatabase = itemDatabase.GetItemById(instance.accessoryId) as AccessoryDefinition;
            if (fromDatabase != null)
                return fromDatabase;
        }

        PlayerInventory inventory = ResolvePlayerInventory();
        if (inventory != null &&
            inventory.TryGetAccessoryInstanceWithDefinition(instance.instanceId, out AccessoryDefinition fromInventory, out _))
        {
            return fromInventory;
        }

        AccessoryDefinition[] definitions = Resources.FindObjectsOfTypeAll<AccessoryDefinition>();
        for (int i = 0; i < definitions.Length; i++)
        {
            AccessoryDefinition candidate = definitions[i];
            if (candidate == null)
                continue;

            if (string.Equals(candidate.RuntimeId, instance.accessoryId, StringComparison.Ordinal))
                return candidate;
        }

        return null;
    }

    PlayerInventory ResolvePlayerInventory()
    {
        if (ctx is PlayerContext playerContext && playerContext.inventory != null)
            return playerContext.inventory;

        if (ctx != null)
        {
            PlayerInventory inventory = ctx.GetComponentInParent<PlayerInventory>();
            if (inventory != null)
                return inventory;

            inventory = ctx.GetComponentInChildren<PlayerInventory>(true);
            if (inventory != null)
                return inventory;
        }

        return UnityEngine.Object.FindFirstObjectByType<PlayerInventory>(FindObjectsInactive.Include);
    }

    void RefreshRuntime()
    {
        statsHub?.RebuildModifierProviders();
        statsHub?.MarkDirty();
        passiveController?.RefreshPassiveLoadout();
        OnLoadoutChanged?.Invoke();
    }

    string ResolveOwnerId()
    {
        if (!string.IsNullOrWhiteSpace(equipmentOwnerId))
            return equipmentOwnerId;

        ResolveReferences();

        if (ctx == null)
            return null;

        string characterOwnerId = ResolveCharacterOwnerId();
        if (!string.IsNullOrWhiteSpace(characterOwnerId))
            return characterOwnerId;

        return ResolveLegacyOwnerId();
    }

    string ResolveCharacterOwnerId()
    {
        ResolveReferences();
        return CharacterEquipment.BuildCharacterOwnerId(ctx != null && ctx.baseStats != null ? ctx.baseStats.characterId : null);
    }

    string ResolveLegacyOwnerId()
    {
        ResolveReferences();

        if (ctx == null)
            return null;

        if (ctx.TargetIdentity == AITargetIdentity.Player)
            return "player";

        if (ctx.TargetIdentity == AITargetIdentity.Companion)
        {
            FieldAllyMember fieldAlly = GetComponentInParent<FieldAllyMember>(true);
            if (fieldAlly == null)
                fieldAlly = GetComponentInChildren<FieldAllyMember>(true);

            if (fieldAlly != null && fieldAlly.ActorRole != ChainActorRole.None)
                return $"ally:{fieldAlly.ActorRole}";

            return "helper";
        }

        return null;
    }

    bool ShouldUsePlayerInventorySave()
    {
        ResolveReferences();

        if (!usePlayerInventorySave || ctx == null)
            return false;

        return ctx.TargetIdentity == AITargetIdentity.Player ||
               ctx.TargetIdentity == AITargetIdentity.Companion;
    }

    CharacterAccessoryLoadoutSaveData FindSavedEntryWithFallback(AccessoryLoadoutSaveData data)
    {
        string ownerId = ResolveOwnerId();
        CharacterAccessoryLoadoutSaveData entry = FindLoadoutEntry(data, ownerId);
        if (entry != null)
            return entry;

        string legacyOwnerId = ResolveLegacyOwnerId();
        if (!string.Equals(ownerId, legacyOwnerId, StringComparison.Ordinal))
            entry = FindLoadoutEntry(data, legacyOwnerId);

        return entry;
    }

    CharacterAccessoryLoadoutSaveData CreateSaveEntry(PlayerInventory inventory, HashSet<string> usedInstanceIds)
    {
        EnsureSlotCount();

        var entry = new CharacterAccessoryLoadoutSaveData
        {
            ownerId = ResolveOwnerId(),
            slotCount = slotCount
        };

        for (int i = 0; i < equippedAccessories.Count; i++)
        {
            AccessoryInstanceData instance = equippedAccessories[i];
            if (instance == null || instance.IsEmpty)
            {
                entry.equippedAccessories.Add(null);
                continue;
            }

            string instanceId = instance.instanceId;
            if (string.IsNullOrWhiteSpace(instanceId) ||
                (inventory != null && !inventory.TryGetAccessoryInstanceWithDefinition(instanceId, out _, out _)) ||
                (usedInstanceIds != null && !usedInstanceIds.Add(instanceId)))
            {
                entry.equippedAccessories.Add(null);
                continue;
            }

            entry.equippedAccessories.Add(instance.DeepClone());
        }

        return entry;
    }

    void ApplySaveEntry(CharacterAccessoryLoadoutSaveData entry, PlayerInventory inventory, HashSet<string> usedInstanceIds)
    {
        if (entry == null)
            return;

        slotCount = Mathf.Max(slotCount, entry.slotCount);
        EnsureSlotCount();

        for (int i = 0; i < equippedAccessories.Count; i++)
            equippedAccessories[i] = null;

        if (entry.equippedAccessories != null)
        {
            int count = Mathf.Min(equippedAccessories.Count, entry.equippedAccessories.Count);
            for (int i = 0; i < count; i++)
            {
                AccessoryInstanceData savedInstance = entry.equippedAccessories[i];
                if (savedInstance == null || savedInstance.IsEmpty || string.IsNullOrWhiteSpace(savedInstance.instanceId))
                    continue;

                if (usedInstanceIds != null && usedInstanceIds.Contains(savedInstance.instanceId))
                    continue;

                if (!inventory.TryGetAccessoryInstanceWithDefinition(savedInstance.instanceId, out AccessoryDefinition definition, out AccessoryInstanceData inventoryInstance))
                    continue;

                if (!definition.CanEquip(ctx))
                    continue;

                equippedAccessories[i] = inventoryInstance.DeepClone();
                usedInstanceIds?.Add(savedInstance.instanceId);
            }
        }

        RefreshRuntime();
    }

    static void AppendStatModifiers(List<RuntimeStatModifier> buffer, List<PassiveStatModifier> modifiers, string sourceId)
    {
        if (buffer == null || modifiers == null)
            return;

        for (int i = 0; i < modifiers.Count; i++)
        {
            PassiveStatModifier modifier = modifiers[i];
            if (modifier == null)
                continue;

            buffer.Add(new RuntimeStatModifier(
                modifier.statType,
                modifier.operation,
                modifier.value,
                sourceId));
        }
    }

    static void AppendPassives(List<PassiveDefinition> buffer, List<PassiveDefinition> passives)
    {
        if (buffer == null || passives == null)
            return;

        for (int i = 0; i < passives.Count; i++)
        {
            PassiveDefinition passive = passives[i];
            if (passive != null)
                buffer.Add(passive);
        }
    }

    static string BuildSourceId(AccessoryInstanceData instance, string suffix)
    {
        string instanceId = instance != null && !string.IsNullOrWhiteSpace(instance.instanceId)
            ? instance.instanceId
            : "unknown";

        return string.IsNullOrWhiteSpace(suffix)
            ? $"accessory:{instanceId}"
            : $"accessory:{instanceId}:{suffix}";
    }

    static void GatherSceneLoadouts(List<AccessoryLoadout> buffer)
    {
        buffer.Clear();

        CharacteContext[] contexts = UnityEngine.Object.FindObjectsByType<CharacteContext>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < contexts.Length; i++)
        {
            CharacteContext context = contexts[i];
            if (context == null)
                continue;

            context.ResolveReferences();
            if (context.AccessoryLoadout != null && !buffer.Contains(context.AccessoryLoadout))
                buffer.Add(context.AccessoryLoadout);
        }

        AccessoryLoadout[] loadouts = UnityEngine.Object.FindObjectsByType<AccessoryLoadout>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < loadouts.Length; i++)
        {
            if (loadouts[i] != null && !buffer.Contains(loadouts[i]))
                buffer.Add(loadouts[i]);
        }

        buffer.Sort(CompareLoadoutOrder);
    }

    static int CompareLoadoutOrder(AccessoryLoadout left, AccessoryLoadout right)
    {
        int priorityCompare = GetLoadoutPriority(left).CompareTo(GetLoadoutPriority(right));
        if (priorityCompare != 0)
            return priorityCompare;

        return string.Compare(left != null ? left.ResolveOwnerId() : null, right != null ? right.ResolveOwnerId() : null, StringComparison.Ordinal);
    }

    static int GetLoadoutPriority(AccessoryLoadout loadout)
    {
        if (loadout == null || loadout.ctx == null)
            return 1000;

        if (loadout.ctx.TargetIdentity == AITargetIdentity.Player)
            return 0;

        if (loadout.ctx.TargetIdentity == AITargetIdentity.Companion)
            return 10;

        return 100;
    }

    static AccessoryLoadoutSaveData LoadPersistedAccessoryData()
    {
        int saveSlot = SaveManager.Instance != null ? SaveManager.Instance.currentSlot : 0;
        var data = SaveSystem.LoadGame(saveSlot);
        return CloneAccessoryData(data?.accessories);
    }

    static AccessoryLoadoutSaveData CloneAccessoryData(AccessoryLoadoutSaveData source)
    {
        if (source == null)
            return null;

        var clone = new AccessoryLoadoutSaveData();
        if (source.entries == null)
            return clone;

        for (int i = 0; i < source.entries.Count; i++)
        {
            CharacterAccessoryLoadoutSaveData entry = source.entries[i];
            if (entry == null)
                continue;

            clone.entries.Add(CloneEntry(entry));
        }

        return clone;
    }

    static CharacterAccessoryLoadoutSaveData CloneEntry(CharacterAccessoryLoadoutSaveData source)
    {
        var clone = new CharacterAccessoryLoadoutSaveData
        {
            ownerId = source.ownerId,
            slotCount = source.slotCount
        };

        if (source.equippedAccessories == null)
            return clone;

        for (int i = 0; i < source.equippedAccessories.Count; i++)
        {
            AccessoryInstanceData instance = source.equippedAccessories[i];
            clone.equippedAccessories.Add(instance != null ? instance.DeepClone() : null);
        }

        return clone;
    }

    static void UpsertLoadoutEntry(AccessoryLoadoutSaveData data, string ownerId, CharacterAccessoryLoadoutSaveData newEntry)
    {
        if (data == null || string.IsNullOrWhiteSpace(ownerId) || newEntry == null)
            return;

        data.entries ??= new List<CharacterAccessoryLoadoutSaveData>();

        for (int i = 0; i < data.entries.Count; i++)
        {
            CharacterAccessoryLoadoutSaveData entry = data.entries[i];
            if (entry == null)
                continue;

            if (!string.Equals(entry.ownerId, ownerId, StringComparison.Ordinal))
                continue;

            data.entries[i] = CloneEntry(newEntry);
            return;
        }

        data.entries.Add(CloneEntry(newEntry));
    }

    static void RemoveEntryInstanceAssignments(AccessoryLoadoutSaveData data, CharacterAccessoryLoadoutSaveData sourceEntry, string exceptOwnerId)
    {
        if (data?.entries == null || sourceEntry?.equippedAccessories == null)
            return;

        for (int i = 0; i < sourceEntry.equippedAccessories.Count; i++)
        {
            AccessoryInstanceData instance = sourceEntry.equippedAccessories[i];
            if (instance == null || string.IsNullOrWhiteSpace(instance.instanceId))
                continue;

            RemoveInstanceAssignments(data, instance.instanceId, exceptOwnerId);
        }
    }

    static void RemoveInstanceAssignments(AccessoryLoadoutSaveData data, string instanceId, string exceptOwnerId)
    {
        if (data?.entries == null || string.IsNullOrWhiteSpace(instanceId))
            return;

        for (int i = 0; i < data.entries.Count; i++)
        {
            CharacterAccessoryLoadoutSaveData entry = data.entries[i];
            if (entry == null || string.Equals(entry.ownerId, exceptOwnerId, StringComparison.Ordinal))
                continue;

            if (entry.equippedAccessories == null)
                continue;

            for (int j = 0; j < entry.equippedAccessories.Count; j++)
            {
                AccessoryInstanceData instance = entry.equippedAccessories[j];
                if (instance == null)
                    continue;

                if (string.Equals(instance.instanceId, instanceId, StringComparison.Ordinal))
                    entry.equippedAccessories[j] = null;
            }
        }
    }

    static bool EntryContainsInstance(CharacterAccessoryLoadoutSaveData entry, string instanceId)
    {
        if (entry?.equippedAccessories == null || string.IsNullOrWhiteSpace(instanceId))
            return false;

        for (int i = 0; i < entry.equippedAccessories.Count; i++)
        {
            AccessoryInstanceData instance = entry.equippedAccessories[i];
            if (instance == null)
                continue;

            if (string.Equals(instance.instanceId, instanceId, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    static bool IsSameLoadoutOwner(string ownerId, string requesterOwnerId, AccessoryLoadout requester)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
            return false;

        if (!string.IsNullOrWhiteSpace(requesterOwnerId) &&
            string.Equals(ownerId, requesterOwnerId, StringComparison.Ordinal))
        {
            return true;
        }

        if (requester == null)
            return false;

        string requesterResolvedOwnerId = requester.ResolveOwnerId();
        if (!string.IsNullOrWhiteSpace(requesterResolvedOwnerId) &&
            string.Equals(ownerId, requesterResolvedOwnerId, StringComparison.Ordinal))
        {
            return true;
        }

        string requesterLegacyOwnerId = requester.ResolveLegacyOwnerId();
        return !string.IsNullOrWhiteSpace(requesterLegacyOwnerId) &&
               string.Equals(ownerId, requesterLegacyOwnerId, StringComparison.Ordinal);
    }
}

