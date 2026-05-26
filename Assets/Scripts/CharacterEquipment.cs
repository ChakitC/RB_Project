using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-150)]
[DisallowMultipleComponent]
public class CharacterEquipment : MonoBehaviour
{
    const string CharacterOwnerPrefix = "character:";

    [Header("Refs")]
    [SerializeField] private CharacteContext ctx;
    private WeaponSystem weaponSystem;

    [Header("Default Weapon")]
    [SerializeField] private GunConfig defaultWeapon;
    [SerializeField] private bool equipDefaultOnStart = true;

    [Header("Shared Inventory Save")]
    [SerializeField] private bool usePlayerInventorySave = true;
    [SerializeField] private string equipmentOwnerId;

    [Header("Runtime")]
    [SerializeField] private GunConfig currentWeapon;
    [SerializeField] private WeaponInstanceData currentWeaponInstance;
    [SerializeField] private string equippedWeaponInstanceId;

    public CharacteContext Context => ctx;
    public WeaponSystem WeaponSystem => weaponSystem;
    public GunConfig DefaultWeapon => defaultWeapon;
    public GunConfig CurrentWeapon => currentWeapon;
    public WeaponInstanceData CurrentWeaponInstance => currentWeaponInstance;
    public string EquippedWeaponInstanceId => equippedWeaponInstanceId;
    public string OwnerId => ResolveOwnerId();
    public bool IsPlayerEquipment => IsPlayerInventoryOwner();
    public bool UsesPlayerInventorySave => ShouldUsePlayerInventorySave();

    public event Action<string> OnEquippedWeaponChanged;
    public event Action<GunConfig, WeaponInstanceData> OnEquipmentChanged;

    public void SetPlayerInventorySaveParticipation(bool participates)
    {
        usePlayerInventorySave = participates;
    }

    public static string BuildCharacterOwnerId(string characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId))
            return null;

        return CharacterOwnerPrefix + characterId;
    }

    public static bool TryParseCharacterOwnerId(string ownerId, out string characterId)
    {
        characterId = null;

        if (string.IsNullOrWhiteSpace(ownerId) ||
            !ownerId.StartsWith(CharacterOwnerPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        characterId = ownerId.Substring(CharacterOwnerPrefix.Length);
        return !string.IsNullOrWhiteSpace(characterId);
    }

    void Awake()
    {
        ResolveReferences();
        MirrorExistingWeaponState();
    }

    void Start()
    {
        if (!equipDefaultOnStart)
            return;

        bool needsRuntimeEquip =
            currentWeapon == null ||
            currentWeaponInstance == null ||
            weaponSystem == null ||
            weaponSystem.CurrentWeapon != currentWeapon ||
            weaponSystem.CurrentWeaponInstance != currentWeaponInstance;

        if (needsRuntimeEquip)
            EquipDefaultWeapon();
    }

    public void ResolveReferences()
    {
        if (!ctx)
            TryGetComponent(out ctx);

        if (!ctx)
            ctx = GetComponentInParent<CharacteContext>();

        if (ctx != null && ctx.Equipment != this)
            ctx.Equipment = this;

        if (!weaponSystem && ctx != null)
            weaponSystem = ctx.WeaponSystem;

        if (!weaponSystem)
            TryGetComponent(out weaponSystem);

        if (!weaponSystem && ctx != null)
            weaponSystem = ctx.GetComponentInChildren<WeaponSystem>(true);

        if (!weaponSystem)
            weaponSystem = GetComponentInChildren<WeaponSystem>(true);

        if (ctx != null && weaponSystem != null && ctx.WeaponSystem != weaponSystem)
            ctx.WeaponSystem = weaponSystem;
    }

    public bool EquipDefaultWeapon()
    {
        ResolveReferences();

        GunConfig weapon = defaultWeapon;
        if (!weapon && currentWeapon)
            weapon = currentWeapon;
        if (!weapon && ctx != null)
            weapon = ctx.currentWeapon;
        if (!weapon && weaponSystem != null)
            weapon = weaponSystem.CurrentWeapon;

        if (!weapon)
            return false;

        var instance = currentWeaponInstance;
        if (instance == null || !IsInstanceForWeapon(instance, weapon))
            instance = WeaponInstanceFactory.CreatePlainInstance(weapon);

        return Equip(weapon, instance);
    }

    public bool Equip(GunConfig weapon)
    {
        if (!weapon)
            return Equip(null, null);

        var instance = currentWeaponInstance;
        if (instance == null || !IsInstanceForWeapon(instance, weapon))
            instance = WeaponInstanceFactory.CreatePlainInstance(weapon);

        return Equip(weapon, instance);
    }

    public bool Equip(GunConfig weapon, WeaponInstanceData instance)
    {
        ResolveReferences();

        currentWeapon = weapon;
        currentWeaponInstance = instance;

        if (currentWeaponInstance != null)
            equippedWeaponInstanceId = currentWeaponInstance.instanceId;
        else
            equippedWeaponInstanceId = null;

        if (ctx != null)
            ctx.currentWeapon = currentWeapon;

        if (weaponSystem != null)
            weaponSystem.Equip(currentWeapon, currentWeaponInstance);

        OnEquippedWeaponChanged?.Invoke(equippedWeaponInstanceId);
        OnEquipmentChanged?.Invoke(currentWeapon, currentWeaponInstance);
        return currentWeapon != null;
    }

    public bool EquipFromInventory(PlayerInventory inventory, string instanceId, bool disallowEquippedByOther = true)
    {
        if (inventory == null || string.IsNullOrWhiteSpace(instanceId))
            return false;

        string ownerId = ResolveOwnerId();
        if (disallowEquippedByOther && IsWeaponInstanceUnavailable(instanceId, ownerId, this))
            return false;

        if (!inventory.TryGetWeaponInstanceWithDefinition(instanceId, out GunConfig weapon, out WeaponInstanceData instance))
            return false;

        return Equip(weapon, instance);
    }

    public void ClearEquipment()
    {
        ResolveReferences();

        currentWeapon = null;
        currentWeaponInstance = null;
        equippedWeaponInstanceId = null;

        if (ctx != null)
            ctx.currentWeapon = null;

        if (weaponSystem != null)
            weaponSystem.Equip(null, null);

        OnEquippedWeaponChanged?.Invoke(null);
        OnEquipmentChanged?.Invoke(null, null);
    }

    void MirrorExistingWeaponState()
    {
        if (!currentWeapon && defaultWeapon)
            currentWeapon = defaultWeapon;

        if (!currentWeapon && weaponSystem != null && weaponSystem.CurrentWeapon != null)
            currentWeapon = weaponSystem.CurrentWeapon;

        if (!currentWeapon && ctx != null && ctx.currentWeapon != null)
            currentWeapon = ctx.currentWeapon;

        if (currentWeaponInstance == null && weaponSystem != null)
            currentWeaponInstance = weaponSystem.CurrentWeaponInstance;

        if (currentWeaponInstance != null)
            equippedWeaponInstanceId = currentWeaponInstance.instanceId;

        if (ctx != null && currentWeapon != null && ctx.currentWeapon != currentWeapon)
            ctx.currentWeapon = currentWeapon;
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
        return BuildCharacterOwnerId(ctx != null && ctx.baseStats != null ? ctx.baseStats.characterId : null);
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

    string FindSavedEquipmentEntryWithFallback(EquipmentSaveData data)
    {
        string ownerId = ResolveOwnerId();
        string instanceId = FindEquipmentEntry(data, ownerId);
        if (!string.IsNullOrWhiteSpace(instanceId))
            return instanceId;

        string legacyOwnerId = ResolveLegacyOwnerId();
        if (!string.Equals(ownerId, legacyOwnerId, StringComparison.Ordinal))
            instanceId = FindEquipmentEntry(data, legacyOwnerId);

        return instanceId;
    }

    bool ShouldUsePlayerInventorySave()
    {
        ResolveReferences();

        if (!usePlayerInventorySave || ctx == null)
            return false;

        return ctx.TargetIdentity == AITargetIdentity.Player ||
               ctx.TargetIdentity == AITargetIdentity.Companion;
    }

    bool IsPlayerInventoryOwner()
    {
        ResolveReferences();
        return ctx != null && ctx.TargetIdentity == AITargetIdentity.Player;
    }

    public static void WriteSceneEquipmentToSave(GameSaveData data, PlayerInventory inventory = null)
    {
        if (data == null)
            return;

        data.equipment ??= LoadPersistedEquipmentData() ?? new EquipmentSaveData();

        var equipments = new List<CharacterEquipment>();
        GatherSceneEquipments(equipments);

        var usedInstanceIds = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < equipments.Count; i++)
        {
            CharacterEquipment equipment = equipments[i];
            if (equipment == null || !equipment.UsesPlayerInventorySave)
                continue;

            string ownerId = equipment.ResolveOwnerId();
            if (string.IsNullOrWhiteSpace(ownerId))
                continue;

            string instanceId = equipment.equippedWeaponInstanceId;
            if (!string.IsNullOrWhiteSpace(instanceId) &&
                inventory != null &&
                !inventory.TryGetWeaponInstanceWithDefinition(instanceId, out _, out _))
            {
                instanceId = null;
            }

            if (!string.IsNullOrWhiteSpace(instanceId) && !usedInstanceIds.Add(instanceId))
                instanceId = null;

            if (!string.IsNullOrWhiteSpace(instanceId))
                RemoveInstanceAssignments(data.equipment, instanceId, ownerId);

            UpsertEquipmentEntry(data.equipment, ownerId, instanceId);

            string legacyOwnerId = equipment.ResolveLegacyOwnerId();
            if (!string.IsNullOrWhiteSpace(legacyOwnerId) &&
                !string.Equals(ownerId, legacyOwnerId, StringComparison.Ordinal))
            {
                UpsertEquipmentEntry(data.equipment, legacyOwnerId, null);
            }
        }
    }

    public static bool SaveEquipmentAssignment(string ownerId, string instanceId, PlayerInventory inventory = null)
    {
        if (string.IsNullOrWhiteSpace(ownerId) || string.IsNullOrWhiteSpace(instanceId))
            return false;

        if (inventory != null && !inventory.TryGetWeaponInstanceWithDefinition(instanceId, out _, out _))
            return false;

        if (IsWeaponInstanceUnavailable(instanceId, ownerId, null))
            return false;

        int saveSlot = SaveManager.Instance != null ? SaveManager.Instance.currentSlot : 0;
        var data = SaveSystem.LoadGame(saveSlot) ?? new GameSaveData();
        data.equipment ??= new EquipmentSaveData();

        RemoveInstanceAssignments(data.equipment, instanceId, ownerId);
        UpsertEquipmentEntry(data.equipment, ownerId, instanceId);
        SaveSystem.SaveGame(data, saveSlot);
        return true;
    }

    public static string ApplySceneEquipmentFromInventory(
        GameSaveData data,
        PlayerInventory inventory,
        string legacyPlayerEquippedId)
    {
        if (inventory == null)
            return null;

        var equipments = new List<CharacterEquipment>();
        GatherSceneEquipments(equipments);

        var usedInstanceIds = new HashSet<string>(StringComparer.Ordinal);
        string playerEquippedId = null;

        for (int i = 0; i < equipments.Count; i++)
        {
            CharacterEquipment equipment = equipments[i];
            if (equipment == null || !equipment.UsesPlayerInventorySave)
                continue;

            string ownerId = equipment.ResolveOwnerId();
            if (string.IsNullOrWhiteSpace(ownerId))
                continue;

            string instanceId = equipment.FindSavedEquipmentEntryWithFallback(data?.equipment);
            if (string.IsNullOrWhiteSpace(instanceId) && equipment.IsPlayerEquipment)
                instanceId = legacyPlayerEquippedId;

            if (string.IsNullOrWhiteSpace(instanceId))
                continue;

            if (usedInstanceIds.Contains(instanceId))
            {
                equipment.EquipFallbackAfterInventoryMiss();
                continue;
            }

            if (equipment.EquipFromInventory(inventory, instanceId, disallowEquippedByOther: false))
            {
                usedInstanceIds.Add(instanceId);
                if (equipment.IsPlayerEquipment)
                    playerEquippedId = instanceId;
            }
            else
            {
                equipment.EquipFallbackAfterInventoryMiss();
            }
        }

        return playerEquippedId;
    }

    public static bool IsWeaponInstanceEquippedByOther(string instanceId, CharacterEquipment requester)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
            return false;

        string requesterOwnerId = requester != null ? requester.ResolveOwnerId() : null;
        return IsWeaponInstanceEquippedByOther(instanceId, requesterOwnerId, requester);
    }

    public static bool IsWeaponInstanceUnavailable(string instanceId, string requesterOwnerId, CharacterEquipment requester)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
            return false;

        return IsWeaponInstanceEquippedByOther(instanceId, requesterOwnerId, requester) ||
               IsWeaponInstanceAssignedInSave(instanceId, requesterOwnerId, requester);
    }

    public static bool IsWeaponInstanceAssignedInSave(string instanceId, string exceptOwnerId, CharacterEquipment requester = null)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
            return false;

        int saveSlot = SaveManager.Instance != null ? SaveManager.Instance.currentSlot : 0;
        var data = SaveSystem.LoadGame(saveSlot);
        var entries = data?.equipment?.entries;
        if (entries == null)
            return false;

        for (int i = 0; i < entries.Count; i++)
        {
            CharacterEquipmentSaveData entry = entries[i];
            if (entry == null)
                continue;

            if (!string.Equals(entry.equippedWeaponInstanceId, instanceId, StringComparison.Ordinal))
                continue;

            if (IsSameEquipmentOwner(entry.ownerId, exceptOwnerId, requester))
                continue;

            return true;
        }

        return false;
    }

    static bool IsWeaponInstanceEquippedByOther(string instanceId, string requesterOwnerId, CharacterEquipment requester)
    {
        var equipments = new List<CharacterEquipment>();
        GatherSceneEquipments(equipments);

        for (int i = 0; i < equipments.Count; i++)
        {
            CharacterEquipment equipment = equipments[i];
            if (equipment == null || equipment == requester || !equipment.UsesPlayerInventorySave)
                continue;

            if (!string.Equals(equipment.equippedWeaponInstanceId, instanceId, StringComparison.Ordinal))
                continue;

            string ownerId = equipment.ResolveOwnerId();
            if (IsSameEquipmentOwner(ownerId, requesterOwnerId, requester))
                continue;

            string legacyOwnerId = equipment.ResolveLegacyOwnerId();
            if (IsSameEquipmentOwner(legacyOwnerId, requesterOwnerId, requester))
                continue;

            return true;
        }

        return false;
    }

    static bool IsSameEquipmentOwner(string ownerId, string requesterOwnerId, CharacterEquipment requester)
    {
        if (string.IsNullOrWhiteSpace(ownerId))
            return false;

        if (!string.IsNullOrWhiteSpace(requesterOwnerId) &&
            string.Equals(ownerId, requesterOwnerId, StringComparison.Ordinal))
            return true;

        if (requester == null)
            return false;

        string requesterResolvedOwnerId = requester.ResolveOwnerId();
        if (!string.IsNullOrWhiteSpace(requesterResolvedOwnerId) &&
            string.Equals(ownerId, requesterResolvedOwnerId, StringComparison.Ordinal))
            return true;

        string requesterLegacyOwnerId = requester.ResolveLegacyOwnerId();
        return !string.IsNullOrWhiteSpace(requesterLegacyOwnerId) &&
               string.Equals(ownerId, requesterLegacyOwnerId, StringComparison.Ordinal);
    }

    public static bool TryFindSceneEquipmentByOwner(string ownerId, out CharacterEquipment equipment)
    {
        equipment = null;

        if (string.IsNullOrWhiteSpace(ownerId))
            return false;

        var equipments = new List<CharacterEquipment>();
        GatherSceneEquipments(equipments);

        for (int i = 0; i < equipments.Count; i++)
        {
            CharacterEquipment candidate = equipments[i];
            if (candidate == null || !candidate.UsesPlayerInventorySave)
                continue;

            if (string.Equals(candidate.ResolveOwnerId(), ownerId, StringComparison.Ordinal) ||
                string.Equals(candidate.ResolveLegacyOwnerId(), ownerId, StringComparison.Ordinal))
            {
                equipment = candidate;
                return true;
            }
        }

        return false;
    }

    public static bool TryFindSceneEquipmentByWeaponInstance(string instanceId, out CharacterEquipment equipment)
    {
        equipment = null;

        if (string.IsNullOrWhiteSpace(instanceId))
            return false;

        var equipments = new List<CharacterEquipment>();
        GatherSceneEquipments(equipments);

        for (int i = 0; i < equipments.Count; i++)
        {
            CharacterEquipment candidate = equipments[i];
            if (candidate == null || !candidate.UsesPlayerInventorySave)
                continue;

            if (!string.Equals(candidate.equippedWeaponInstanceId, instanceId, StringComparison.Ordinal))
                continue;

            equipment = candidate;
            return true;
        }

        return false;
    }

    public static void ReleaseRemovedInventoryWeapon(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId))
            return;

        var equipments = new List<CharacterEquipment>();
        GatherSceneEquipments(equipments);

        for (int i = 0; i < equipments.Count; i++)
        {
            CharacterEquipment equipment = equipments[i];
            if (equipment == null || !equipment.UsesPlayerInventorySave)
                continue;

            if (!string.Equals(equipment.equippedWeaponInstanceId, instanceId, StringComparison.Ordinal))
                continue;

            equipment.EquipFallbackAfterInventoryMiss();
        }
    }

    void EquipFallbackAfterInventoryMiss()
    {
        currentWeaponInstance = null;
        equippedWeaponInstanceId = null;

        if (!EquipDefaultWeapon())
            ClearEquipment();
    }

    static void GatherSceneEquipments(List<CharacterEquipment> buffer)
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
            if (context.Equipment != null && !buffer.Contains(context.Equipment))
                buffer.Add(context.Equipment);
        }

        CharacterEquipment[] equipments = UnityEngine.Object.FindObjectsByType<CharacterEquipment>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < equipments.Length; i++)
        {
            if (equipments[i] != null && !buffer.Contains(equipments[i]))
                buffer.Add(equipments[i]);
        }

        buffer.Sort(CompareEquipmentLoadOrder);
    }

    static int CompareEquipmentLoadOrder(CharacterEquipment left, CharacterEquipment right)
    {
        int priorityCompare = GetEquipmentLoadPriority(left).CompareTo(GetEquipmentLoadPriority(right));
        if (priorityCompare != 0)
            return priorityCompare;

        return string.Compare(left != null ? left.ResolveOwnerId() : null, right != null ? right.ResolveOwnerId() : null, StringComparison.Ordinal);
    }

    static int GetEquipmentLoadPriority(CharacterEquipment equipment)
    {
        if (equipment == null)
            return 1000;

        if (equipment.IsPlayerEquipment)
            return 0;

        string ownerId = equipment.ResolveOwnerId();
        if (!string.IsNullOrWhiteSpace(ownerId) && ownerId.StartsWith("ally:", StringComparison.Ordinal))
            return 10;

        if (string.Equals(ownerId, "helper", StringComparison.Ordinal))
            return 20;

        return 100;
    }

    public static string FindEquipmentEntry(EquipmentSaveData data, string ownerId)
    {
        if (data?.entries == null || string.IsNullOrWhiteSpace(ownerId))
            return null;

        for (int i = 0; i < data.entries.Count; i++)
        {
            CharacterEquipmentSaveData entry = data.entries[i];
            if (entry == null)
                continue;

            if (string.Equals(entry.ownerId, ownerId, StringComparison.Ordinal))
                return entry.equippedWeaponInstanceId;
        }

        return null;
    }

    static EquipmentSaveData LoadPersistedEquipmentData()
    {
        int saveSlot = SaveManager.Instance != null ? SaveManager.Instance.currentSlot : 0;
        var data = SaveSystem.LoadGame(saveSlot);
        return CloneEquipmentData(data?.equipment);
    }

    static EquipmentSaveData CloneEquipmentData(EquipmentSaveData source)
    {
        if (source == null)
            return null;

        var clone = new EquipmentSaveData();
        if (source.entries == null)
            return clone;

        for (int i = 0; i < source.entries.Count; i++)
        {
            CharacterEquipmentSaveData entry = source.entries[i];
            if (entry == null)
                continue;

            clone.entries.Add(new CharacterEquipmentSaveData
            {
                ownerId = entry.ownerId,
                equippedWeaponInstanceId = entry.equippedWeaponInstanceId
            });
        }

        return clone;
    }

    static void RemoveInstanceAssignments(EquipmentSaveData data, string instanceId, string exceptOwnerId)
    {
        if (data?.entries == null || string.IsNullOrWhiteSpace(instanceId))
            return;

        for (int i = 0; i < data.entries.Count; i++)
        {
            CharacterEquipmentSaveData entry = data.entries[i];
            if (entry == null)
                continue;

            if (string.Equals(entry.ownerId, exceptOwnerId, StringComparison.Ordinal))
                continue;

            if (string.Equals(entry.equippedWeaponInstanceId, instanceId, StringComparison.Ordinal))
                entry.equippedWeaponInstanceId = null;
        }
    }

    static void UpsertEquipmentEntry(EquipmentSaveData data, string ownerId, string instanceId)
    {
        if (data == null || string.IsNullOrWhiteSpace(ownerId))
            return;

        data.entries ??= new List<CharacterEquipmentSaveData>();

        for (int i = 0; i < data.entries.Count; i++)
        {
            CharacterEquipmentSaveData entry = data.entries[i];
            if (entry == null)
                continue;

            if (!string.Equals(entry.ownerId, ownerId, StringComparison.Ordinal))
                continue;

            entry.equippedWeaponInstanceId = instanceId;
            return;
        }

        data.entries.Add(new CharacterEquipmentSaveData
        {
            ownerId = ownerId,
            equippedWeaponInstanceId = instanceId
        });
    }

    static bool IsInstanceForWeapon(WeaponInstanceData instance, GunConfig weapon)
    {
        if (instance == null || !weapon)
            return false;

        string weaponId = WeaponInstanceFactory.ResolveBaseWeaponId(weapon);
        return string.Equals(instance.baseWeaponId, weaponId, StringComparison.Ordinal);
    }
}
