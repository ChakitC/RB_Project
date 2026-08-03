using System;
using UnityEngine;

[DisallowMultipleComponent]
public class LevelSystem : MonoBehaviour
{

    [Header("Ref")]
    [SerializeField] private CharacteContext CTX;
    [SerializeField] private PartySlot _slot;
    
    [Header("Config")]
    [SerializeField] private LevelTableSO table;
    [SerializeField] private string ChracterID;
    
    [Header("Runtime")]
    [SerializeField, Min(1)] private int level = 1;
    [SerializeField, Min(0)] private int currentXp = 0;

    public LevelTableSO Table => table;
        
    // ---------- Events ----------
    /// <summary>เรียกเมื่อเลเวลเปลี่ยน: (newLevel)</summary>
    public event Action<int> LevelChanged;

    /// <summary>เรียกเมื่อ XP เปลี่ยน: (level, currentXp, xpToNext)</summary>
    public event Action<int, int, int> XpChanged;

    /// <summary>เรียกเมื่อเลเวลอัพ: (oldLevel, newLevel)</summary>
    public event Action<int, int> LeveledUp;

    /// <summary>เรียกเมื่อ TotalXp เปลี่ยน: (totalXp)</summary>
    public event Action<long> TotalXpChanged;

    // ---------- Properties ----------
    public int Level => level;
    public int CurrentXp => currentXp;

    public bool HasTable => table != null;
    public bool IsMaxLevel => !HasTable ? false : level >= table.MaxLevel;

    public int XpToNext
    {
        get
        {
            if (!HasTable) return int.MaxValue;
            return table.GetXpToNext(level);
        }
    }

    /// <summary>Total XP สะสมรวมตั้งแต่เลเวล 1 (คำนวณจาก table + currentXp)</summary>
    public long TotalXp
    {
        get
        {
            if (!HasTable) return currentXp;
            return table.GetTotalXpToReach(level) + currentXp;
        }
    }

    public float Progress01
    {
        get
        {
            if (!HasTable) return 0f;
            return table.GetProgress01(level, currentXp);
        }
    }

    // ---------- Unity ----------

    private void Awake()
    {
        ResolveReferences();
    }
    
    private void Start()
    {
        ResolveReferences();
        SetState();
    }

    void ResolveReferences()
    {
        if (!CTX)
        {
            TryGetComponent(out CTX);
            if (!CTX)
                CTX = GetComponentInParent<CharacteContext>();
        }

        CTX?.ResolveReferences();

        if (CTX != null && CTX.levelSystem != this)
            CTX.levelSystem = this;
    }

    bool TryResolvePersistentCharacterId(out string characterId)
    {
        characterId = null;
        ResolveReferences();

        if (CTX != null)
        {
            if (CTX.TargetIdentity != AITargetIdentity.Player &&
                CTX.TargetIdentity != AITargetIdentity.Companion)
            {
                return false;
            }

            if (CTX.baseStats != null)
                characterId = CTX.baseStats.characterId;
        }

        if (string.IsNullOrWhiteSpace(characterId) && _slot != null)
            characterId = _slot.IDCharacter;

        return SaveDataMigration.ShouldPersistCharacterProgress(characterId);
    }

    // ---------- Public API ----------
    
    /// <summary>เพิ่ม XP แบบ delta (รองรับก้อนใหญ่)</summary>
    public void AddXp(int amount)
    {
        if (amount <= 0) return;

        if (!HasTable)
        {
            currentXp += amount;
            RaiseXp();
            RaiseTotal();
            return;
        }

        if (IsMaxLevel) return;

        // หา CharacterID ให้จบก่อน (กัน null)
        bool shouldPersist = TryResolvePersistentCharacterId(out string persistentCharacterId);
        if (shouldPersist)
            ChracterID = persistentCharacterId;

        currentXp += amount;

        while (!IsMaxLevel)
        {
            int need = table.GetXpToNext(level);
            if (need == int.MaxValue) break;
            if (need <= 0) { LevelUpOnce(); continue; }
            if (currentXp < need) break;

            currentXp -= need;
            LevelUpOnce();
        }

        if (IsMaxLevel)
            currentXp = 0;

        // Save ครั้งเดียวด้วยค่าสุดท้าย
        if (shouldPersist && SaveManager.Instance != null && !string.IsNullOrEmpty(ChracterID))
            SaveManager.Instance.SaveCharacterLevel(ChracterID, level, currentXp);

        RaiseXp();
        RaiseTotal();
    }

    /// <summary>ตั้งค่าด้วย (level,xp) เช่นตอน Load</summary>
    public void SetState()
    {
        if (!TryResolvePersistentCharacterId(out string persistentCharacterId))
        {
            ClampStateToTable();
            RaiseAll();
            return;
        }

        ChracterID = persistentCharacterId;

        if (SaveManager.Instance == null) 
        {
            Debug.Log("[LevelSystem] SaveManager is Missing");
            return;
        }
         
        var data = SaveManager.Instance.LoadCharacterLevel(ChracterID);
        
        var newLevel = data?.level ?? 1;
        var newCurrentXp = data?.xp ?? 0;

        level = Mathf.Max(1, newLevel);
        currentXp = Mathf.Max(0, newCurrentXp);

        ClampStateToTable();
        RaiseAll();
    }

    /// <summary>ตั้งค่าด้วย totalXp (สะสมรวม) เช่นตอน Load แบบเก็บค่าเดียว</summary>
    public void SetFromTotalXp(long totalXp)
    {
        if (!HasTable)
        {
            level = 1;
            currentXp = (int)Mathf.Clamp((long)0, totalXp, int.MaxValue);
            RaiseAll();
            return;
        }

        int remainder;
        int newLevel = table.GetLevelFromTotalXp(totalXp, out remainder);

        level = Mathf.Clamp(newLevel, 1, table.MaxLevel);
        currentXp = Mathf.Max(0, remainder);

        if (IsMaxLevel) currentXp = 0;

        RaiseAll();
    }

    /// <summary>อ้างอิง table ใหม่ แล้ว clamp สถานะ</summary>
    public void SetTable(LevelTableSO newTable, bool keepTotalXp = true)
    {
        long total = TotalXp;

        table = newTable;

        if (keepTotalXp) SetFromTotalXp(total);
        else
        {
            ClampStateToTable();
            RaiseAll();
        }
    }

    // ---------- Internal ----------

    private void LevelUpOnce()
    {
        int old = level;
        level++;

        // clamp เผื่อเลย Max
        if (HasTable) level = Mathf.Clamp(level, 1, table.MaxLevel);

        
       

        LeveledUp?.Invoke(old, level);
        LevelChanged?.Invoke(level);
    }

    private void ClampStateToTable()
    {
        level = Mathf.Max(1, level);
        currentXp = Mathf.Max(0, currentXp);

        if (!HasTable) return;

        level = Mathf.Clamp(level, 1, table.MaxLevel);

        if (level >= table.MaxLevel)
        {
            currentXp = 0;
            return;
        }

        // กัน currentXp เกิน need (เผื่อกรอกผิดใน inspector)
        int need = table.GetXpToNext(level);
        if (need != int.MaxValue && need > 0)
            currentXp = Mathf.Clamp(currentXp, 0, need - 1);
        
    }

    private void RaiseAll()
    {
        LevelChanged?.Invoke(level);
        RaiseXp();
        RaiseTotal();
    }

    private void RaiseXp()
    {
        int need = XpToNext;
        XpChanged?.Invoke(level, currentXp, need);
    }

    private void RaiseTotal()
    {
        TotalXpChanged?.Invoke(TotalXp);
    }
}
