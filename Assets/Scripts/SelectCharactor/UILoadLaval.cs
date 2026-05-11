using UnityEngine;
using TMPro;

public class UILoadLaval : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BasementContext CTX;
    [SerializeField] private GameObject weaponEquipmentObject;
    private UIWeaponEquipment weaponEquipmentUI;
    
    
    [Header("References Text")]
    [SerializeField] private TMP_Text NameText;
    [SerializeField] private TMP_Text levelCurrentText;
    [SerializeField] private TMP_Text DamageText;
    [SerializeField] private TMP_Text ArmorText;
    [SerializeField] private TMP_Text MAXHPText;
    [SerializeField] private TMP_Text StaminaText;
    [SerializeField] private TMP_Text CritrateText;
    [SerializeField] private TMP_Text CritDamageText;
    [SerializeField] private TMP_Text EnagyText;
    [SerializeField] private TMP_Text SpeedText;
    [SerializeField] private TMP_Text currentXpText;
    [SerializeField] private TMP_Text nextXpTonewLavelText;
    [SerializeField] private TMP_Text GoldText;
    
    
    
    public int cost;
    
    [Header("Runtime")]
    [SerializeField] private LevelSystem  levelSystem;
    public PartySlot _slot; // slot ที่กำลังถูกเลือก/ผูกอยู่

    private float Damage, Armor, MAXHP, Stamina, Critrate, CritDamage, Enagy, Speed;
    private int levelCurrent, currentXP ,nextXpTonewLavel ,currentGold;
    private string characterId,  Name;

    private void Awake()
    {
        if (CTX == null)
            CTX = GetComponentInParent<BasementContext>(true);

        ResolveWeaponEquipmentUI();
    }
    
    private void BindLevelSystem(LevelSystem newLs)
    {
        // ถอดของเก่า
        if (levelSystem != null)
            levelSystem.LevelChanged -= OnLevelChanged;

        levelSystem = newLs;

        // ผูกของใหม่ (ถ้า object เปิดใช้งานอยู่)
        if (isActiveAndEnabled && levelSystem != null)
            levelSystem.LevelChanged += OnLevelChanged;
    }

    private void OnEnable()
    {
        LoadLavel();
        // ถ้า BindSlot มาก่อนแล้ว ค่อยซับตรงนี้ได้
        if (levelSystem != null)
            levelSystem.LevelChanged += OnLevelChanged;

        if (_slot != null)
            SendWeaponEquipmentData();

    }

    private void OnDisable()
    {
        if (levelSystem != null)
            levelSystem.LevelChanged -= OnLevelChanged;
    }
    private void OnLevelChanged(int newLevel)
    {
        levelCurrent = Mathf.Max(1, newLevel);
        
        Calculatestatusfromslot();
        UpdateStatTexts();
       
    }

   
    public void BindSlot(PartySlot slot)
    {
        _slot = slot;

        ClearUI();

        if (_slot == null)
        {
            BindLevelSystem(null);
            ClearWeaponEquipmentData();
            Debug.Log("[UILoadLaval] _Slot == null");
            return;
           
        }
        
        characterId = ResolveBoundCharacterId();
        
        BindLevelSystem(_slot.levelSystem);
        LoadLavel();
        Calculatestatusfromslot();
        UpdateStatTexts();
        SendWeaponEquipmentData();
    }

    public void SendWeaponEquipmentData()
    {
        ResolveWeaponEquipmentUI();

        if (weaponEquipmentUI == null)
            return;

        PlayerInventory inventory = CTX != null ? CTX.playerInventory : null;
        weaponEquipmentUI.BindSource(inventory, ResolveBoundCharacterId(), _slot);
    }

    public void ToggleWeaponEquipment()
    {
        ResolveWeaponEquipmentUI();

        if (weaponEquipmentObject == null)
            return;

        bool nextActive = !weaponEquipmentObject.activeSelf;
        weaponEquipmentObject.SetActive(nextActive);

        if (nextActive)
            SendWeaponEquipmentData();
    }

    public void OpenWeaponEquipment()
    {
        ResolveWeaponEquipmentUI();

        if (weaponEquipmentObject == null)
            return;

        weaponEquipmentObject.SetActive(true);
        SendWeaponEquipmentData();
    }

    public void CloseWeaponEquipment()
    {
        ResolveWeaponEquipmentUI();

        if (weaponEquipmentObject != null)
            weaponEquipmentObject.SetActive(false);
    }

    private void ClearWeaponEquipmentData()
    {
        ResolveWeaponEquipmentUI();

        if (weaponEquipmentUI != null)
            weaponEquipmentUI.BindSource(null, null, null);
    }

    private string ResolveBoundCharacterId()
    {
        if (_slot == null)
            return null;

        if (_slot.Selected != null && !string.IsNullOrWhiteSpace(_slot.Selected.characterId))
            return _slot.Selected.characterId;

        return _slot.IDCharacter;
    }

    private void ResolveWeaponEquipmentUI()
    {
        if (weaponEquipmentUI != null)
            return;

        if (weaponEquipmentObject != null)
        {
            weaponEquipmentUI = weaponEquipmentObject.GetComponent<UIWeaponEquipment>();
            if (weaponEquipmentUI == null)
                weaponEquipmentUI = weaponEquipmentObject.GetComponentInChildren<UIWeaponEquipment>(true);

            return;
        }

        if (transform.root != null)
        {
            weaponEquipmentUI = transform.root.GetComponentInChildren<UIWeaponEquipment>(true);
            if (weaponEquipmentUI != null)
                weaponEquipmentObject = weaponEquipmentUI.gameObject;
        }

        if (weaponEquipmentUI == null)
        {
            weaponEquipmentUI = FindFirstObjectByType<UIWeaponEquipment>(FindObjectsInactive.Include);
            if (weaponEquipmentUI != null)
                weaponEquipmentObject = weaponEquipmentUI.gameObject;
        }
    }

    private void ClearUI()
    {
        
        Damage = Armor = MAXHP = Stamina = Critrate = CritDamage = Enagy = Speed = 0f;
        levelCurrent = 1;
        currentXP = 0;
        characterId = null;
        
        if (DamageText) DamageText.text = "-";
        if (ArmorText) ArmorText.text = "-";
        if (MAXHPText) MAXHPText.text = "-";
        if (StaminaText) StaminaText.text = "-";
        if (EnagyText) EnagyText.text = "-";
        if (SpeedText) SpeedText.text = "-";
        if (CritrateText) CritrateText.text = "-";
        if (CritDamageText) CritDamageText.text = "-";
        if (NameText) NameText.text = "-";
        if (levelCurrentText) levelCurrentText.text = "-";
        if (currentXpText) currentXpText.text = "-";
        if (nextXpTonewLavelText)  nextXpTonewLavelText.text = "-";
        if (GoldText)  GoldText.text = "-"; 
        
    }

    // private void UpdateCharacterUI()
    // {
    //     if (_slot == null) return;
    //     
    //     levelSystem = _slot.levelSystem;
    //     characterId = _slot.IDCharacter;
    //     
    //     LoadLavel();
    //     Calculatestatusfromslot();
    //     UpdateStatTexts();
    // }

    private void LoadLavel()
    {
        if (SaveManager.Instance == null) return;
        
        var data = SaveManager.Instance.LoadCharacterLevel(characterId);

        var newLevel = data?.level ?? 1;
        var newCurrentXp = data?.xp ?? 0;
        // var newXptonextlevel = data?
        
        levelCurrent = Mathf.Max(1, newLevel);
        currentXP = Mathf.Max(0, newCurrentXp);
        
        
    }

    private void Calculatestatusfromslot()
    {
        var s = _slot?.Selected;
        if (s == null)
        {
            Debug.Log("[UILoadLaval] Slot.Selected Missing");
            return;
        }

        int startLevel = 1;
        int lv = Mathf.Max(0, levelCurrent - startLevel);

        Name = s.characterName;

        // Linear: base + (perLevel * lv)
        Damage     = s.Damage          + s.DamageScaling     * lv;
        Armor      = s.armor           + s.ArmorScaling      * lv;
        MAXHP      = s.maxHP           + s.MAXHPScaling      * lv;
        Stamina    = s.maxStamina      + s.StaminaScaling    * lv;
        Enagy      = s.Enagy           + s.EnagyScaling      * lv;
        Critrate   = s.critRate        + s.CritrateScaling   * lv;
        CritDamage = s.critMultiplier  + s.CritDamageScaling * lv;
        Speed      = s.speed           + s.SpeedScaling      * lv;
    }

    private void UpdateStatTexts()
    {
        UpdateGold();
        
        DamageText.text  = Damage.ToString("0");
        ArmorText.text   = Armor.ToString("0");
           MAXHPText.text   = MAXHP.ToString("0");
         StaminaText.text = Stamina.ToString("0");
          EnagyText.text   = Enagy.ToString("0");
           SpeedText.text   = Speed.ToString("0");
            NameText.text    = Name;
         levelCurrentText.text = levelCurrent.ToString("0");
         currentXpText.text = currentXP.ToString("0");
         nextXpTonewLavelText.text = nextXpTonewLavel.ToString("0");
        GoldText.text = currentGold.ToString("0");
        
      

        // Critrate เก็บเป็นเปอร์เซ็นต์ 0..100 อยู่แล้ว
        CritrateText.text = Critrate.ToString("0.#") + "%";

        // CritDamage เป็น multiplier 
        
        CritDamageText.text = "x" + CritDamage.ToString("0.##");
        
        
    }

    private void UpdateGold()
    {
        Debug.Log("--------------UpdateGoldText--------------");
        currentGold = CurrencyManager.Instance.GetCurrentGold(CTX.playerInventory);
        nextXpTonewLavel = levelSystem.XpToNext;
       
    }
    
    private void UpdateExpTexts()
    {
        LoadLavel();
        currentXpText.text = currentXP.ToString("0");
        nextXpTonewLavelText.text = nextXpTonewLavel.ToString("0");
    }
    public void ClickUplevel()
    {
        
       
        if (CTX.playerInventory.Gold < cost) return;
        CurrencyManager.Instance.SpendGold(CTX.playerInventory ,cost);
        XpManager.Instance.GrantXp(levelSystem, cost);
        UpdateExpTexts();
        UpdateGold();

    }
    

}
