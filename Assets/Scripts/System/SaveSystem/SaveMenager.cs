using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    [Header("Config")]
    public int currentSlot = 0;

    public KeyCode saveKey = KeyCode.F5;
    public KeyCode loadKey = KeyCode.F9;

    private readonly List<IGameSaveAble> _participants = new();
    private readonly List<IGameSaveParty> _partyParticipants = new();

    // cache เกมล่าสุดที่โหลด (กันอ่านไฟล์เกมซ้ำทุกซีน)
    private GameSaveData _loaded;

    private void Awake()
    {
        Debug.Log("saveDataPath = " + SaveSystem.CurrentDirectory);

        // Singleton
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        SceneManager.sceneLoaded += OnSceneLoaded;

        // ซีนแรก
        RefreshParticipants();
        Load();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshParticipants();

        // ซีนใหม่ต้อง Apply โหลดให้ participants ชุดใหม่
        if (_loaded != null) ApplyLoadedToParticipants();
        else Load();
    }

    public void RefreshParticipants()
    {
        _participants.Clear();
        _partyParticipants.Clear();

        var monos = FindObjectsOfType<MonoBehaviour>(true);

        _participants.AddRange(monos.OfType<IGameSaveAble>());
        _partyParticipants.AddRange(monos.OfType<IGameSaveParty>());

        Debug.Log($"[SaveManager] Found SaveAble: {_participants.Count}, Party: {_partyParticipants.Count}");
    }

    int OrderOf(object o) => (o as ISaveOrder)?.LoadOrder ?? 0;

    // ---------- Party Only ----------

  
    
    public void SaveParty()
    {
        var party = CollectPartyData();

        SaveSystem.UpdatePartyOnly(party, currentSlot);

        // Apply ทันทีในซีนปัจจุบัน
        ApplyParty(party);

        // sync cache
        if (_loaded != null) _loaded.party = party;
    }

    
    private PartyData CollectPartyData()
    {
        var party = new PartyData();

        // ทางหลัก: ให้พวกที่ implement IGameSaveParty เป็นคนกรอก
        if (_partyParticipants.Count > 0)
        {
            foreach (var p in _partyParticipants.OrderBy(OrderOf))
            {
                try { p.OnSaveParty(party); }
                catch (Exception ex) { Debug.LogError($"[SaveManager] Error in OnSaveParty on {p}: {ex}"); }
            }

            return HasPartyIds(party) ? party : LoadPersistedPartyFallback(party);
            
        }

        // fallback: ของเดิมคุณ (ถ้ายังไม่ได้ทำให้ PartySelectionCommitter implements IGameSaveParty)
        var committer = FindAnyObjectByType<PartySelectionCommitter>();
        if (committer != null)
        {
            committer.OnSaveParty(party);
            return HasPartyIds(party) ? party : LoadPersistedPartyFallback(party);
        }

        PartyData fallbackParty = LoadPersistedPartyFallback(null);
        if (HasPartyIds(fallbackParty))
            return fallbackParty;

        Debug.LogWarning("[SaveManager] No party source found (no IGameSaveParty and no PartySelectionCommitter).");
        return party;
    }

    PartyData LoadPersistedPartyFallback(PartyData fallback)
    {
        PartyData persistedParty = SaveSystem.LoadPartyOnly(currentSlot) ?? _loaded?.party;
        return HasPartyIds(persistedParty) ? ClonePartyData(persistedParty) : fallback;
    }

    static bool HasPartyIds(PartyData party)
    {
        if (party?.partyIds == null)
            return false;

        for (int i = 0; i < party.partyIds.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(party.partyIds[i]))
                return true;
        }

        return false;
    }

    static PartyData ClonePartyData(PartyData source)
    {
        var clone = new PartyData();
        if (source?.partyIds == null)
            return clone;

        clone.partyIds.AddRange(source.partyIds);
        return clone;
    }

    //ApplyParty เรียง order
    private void ApplyParty(PartyData party)
    {
        if (party == null) return;

        foreach (var p in _partyParticipants.OrderBy(OrderOf))
        {
            try { p.OnLoadParty(party); }
            catch (Exception ex) { Debug.LogError($"[SaveManager] Error in OnLoadParty on {p}: {ex}"); }
        }
    }

  
    public string LoadPartyMemberId(int slot, int index)  //มันต้อง retund เป็น String โว้ย
    {
        var id = SaveSystem.GetPartyMemberId(slot, index);
        return id;
    }
    
    // ---------- Full Save ----------
    public void Save()
    {
        var data = LoadCurrentSlotDataForCache() ?? new GameSaveData();

        // (แนะนำ) ฝัง snapshot party ปัจจุบันไว้ในเกมเซฟด้วย เผื่อ party.json หาย
        data.party = CollectPartyData();

        foreach (var p in _participants.OrderBy(OrderOf))
        {
            try { p.OnSave(data); }
            catch (Exception ex) { Debug.LogError($"[SaveManager] Error in OnSave on {p}: {ex}"); }
        }

        SaveSystem.SaveGame(data, currentSlot);
        _loaded = data;
    }

    public bool SaveInventoryOnly()
    {
        return SaveInventorySections(includeEquipment: false, includeAccessories: false);
    }

    public bool SaveInventoryAndEquipment()
    {
        return SaveInventorySections(includeEquipment: true, includeAccessories: false);
    }

    public bool SaveInventoryAndAccessories()
    {
        return SaveInventorySections(includeEquipment: false, includeAccessories: true);
    }

    bool SaveInventorySections(bool includeEquipment, bool includeAccessories)
    {
        PlayerInventory inventory = _participants.OfType<PlayerInventory>().FirstOrDefault();
        if (inventory == null)
        {
            Debug.LogWarning("[SaveManager] No PlayerInventory participant found for partial save.");
            return false;
        }

        var data = new GameSaveData
        {
            inventory = inventory.ToData()
        };

        if (includeEquipment)
            CharacterEquipment.WriteSceneEquipmentToSave(data, inventory);
        if (includeAccessories)
            AccessoryLoadout.WriteSceneLoadoutsToSave(data, inventory);

        SaveSystem.SaveInventory(data.inventory, currentSlot);
        if (includeEquipment)
            SaveSystem.SaveEquipment(data.equipment, currentSlot);
        if (includeAccessories)
            SaveSystem.SaveAccessories(data.accessories, currentSlot);

        _loaded ??= new GameSaveData();
        _loaded.inventory = data.inventory;
        if (includeEquipment)
            _loaded.equipment = data.equipment;
        if (includeAccessories)
            _loaded.accessories = data.accessories;

        return true;
    }

    public void Load()
    {
        var data = LoadCurrentSlotDataForCache();

        // ถ้าไม่มี game.json แต่มี party.json -> ยังโหลดได้
        if (data == null)
        {
            Debug.LogWarning("[SaveManager] No save data to load");
            return;
        }

        // override party ด้วย party-only ล่าสุดถ้ามี
        _loaded = data;

        // 1) apply party ก่อน (เพื่อให้ตัวที่ใช้ partyIds[0] เซ็ตตัวละครได้ก่อน)
        ApplyParty(data.party);

        // 2) แล้วค่อยโหลดระบบอื่น (เรียง order)
        foreach (var p in _participants.OrderBy(OrderOf))
        {
            try { p.OnLoad(data); }
            catch (Exception ex) { Debug.LogError($"[SaveManager] Error in OnLoad on {p}: {ex}"); }
        }
    }

    public void RefreshLoadedCacheFromDisk()
    {
        _loaded = LoadCurrentSlotDataForCache();
    }

    private GameSaveData LoadCurrentSlotDataForCache()
    {
        var data = SaveSystem.LoadGame(currentSlot);
        var partyOnly = SaveSystem.LoadPartyOnly(currentSlot);

        if (data == null)
        {
            if (partyOnly == null)
                return null;

            data = new GameSaveData();
        }

        if (partyOnly != null)
            data.party = partyOnly;

        return data;
    }

    private void ApplyLoadedToParticipants()
    {
        if (_loaded == null) return;

        // ทุกครั้งที่ apply ให้เอา party-only ล่าสุดมาทับอีกที (กันกรณีเพิ่ง SaveParty)
        var partyOnly = SaveSystem.LoadPartyOnly(currentSlot);
        if (partyOnly != null)
            _loaded.party = partyOnly;

        ApplyParty(_loaded.party);

        foreach (var p in _participants.OrderBy(OrderOf))
        {
            try { p.OnLoad(_loaded); }
            catch (Exception ex) { Debug.LogError($"[SaveManager] Error in OnLoad on {p}: {ex}"); }
        }
    }



    public void SaveCharacterLevel(string characterId, int level, int currentXp)
    {
        
        var old = SaveSystem.LoadCharacterProgress(currentSlot, characterId);

        var data = old ?? new CharacterProgressData();
        data.level = level;
        data.xp = currentXp;

        SaveSystem.SaveCharacterProgress(currentSlot, characterId, data);
    }

    public (int level, int xp)? LoadCharacterLevel( string characterId)
    {
        var data = SaveSystem.LoadCharacterProgress(currentSlot, characterId);
        if (data == null) return null;

        return (data.level, data.xp);
    }

    public CharacterProgressData LoadCharacterProgressData(string characterId)
    {
        var data = SaveSystem.LoadCharacterProgress(currentSlot, characterId);
        return data != null ? data.DeepClone() : new CharacterProgressData();
    }

    public void SaveCharacterProgressData(string characterId, CharacterProgressData progress)
    {
        if (string.IsNullOrWhiteSpace(characterId))
            return;

        var data = progress != null ? progress.DeepClone() : new CharacterProgressData();
        if (data.unlockedPassiveNodeIds == null)
            data.unlockedPassiveNodeIds = new List<string>();
        if (data.activeSkillTrees == null)
            data.activeSkillTrees = new List<CharacterSkillTreeProgressSaveData>();
        if (data.selectedSkillOptions == null)
            data.selectedSkillOptions = new List<CharacterSkillSelectionSaveData>();

        SaveSystem.SaveCharacterProgress(currentSlot, characterId, data);
    }

    public bool LoadCharacterUnlockState(string characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId))
            return false;

        var data = SaveSystem.LoadCharacterProgress(currentSlot, characterId);
        return data != null && data.unlocked;
    }

    public void SaveCharacterUnlockState(string characterId, bool unlocked)
    {
        if (string.IsNullOrWhiteSpace(characterId))
            return;

        var data = SaveSystem.LoadCharacterProgress(currentSlot, characterId) ?? new CharacterProgressData();
        data.unlocked = unlocked;

        if (data.unlockedPassiveNodeIds == null)
            data.unlockedPassiveNodeIds = new List<string>();

        SaveSystem.SaveCharacterProgress(currentSlot, characterId, data);
    }
    

    // Debug
    private void Update()
    {
        
        if (Input.GetKeyDown(saveKey)) Save();
        if (Input.GetKeyDown(loadKey)) Load();
        // Debug.Log($"Save path: {SaveSystem.CurrentDirectory}");
        
    }
}
