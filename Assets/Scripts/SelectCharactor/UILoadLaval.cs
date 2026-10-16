using UnityEngine;
using TMPro;

public class UILoadLaval : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BasementContext CTX;
    [SerializeField] private GameObject weaponEquipmentObject;
    [SerializeField] private GameObject accessoryEquipmentObject;
    [SerializeField] private BasementAccessoryScreenController accessoryScreen;
    [SerializeField] private BasementGunMenuScreenController gunMenuScreen;
    [SerializeField] private UnityEngine.UI.Button accessoryScreenButton;
    [SerializeField] private BasementCharacterMenuScreenController characterMenu;

    [Tooltip("Optional. Used to resolve static weapon affix stat bonuses for the status preview. " +
             "When empty, any loaded WeaponAffixDatabase is used instead.")]
    [SerializeField] private WeaponAffixDatabase affixDatabase;

    [Header("Active Skill Tree")]
    [SerializeField] private ActiveSkillScreenController activeSkillScreenPrefab;
    private UIEquipment weaponEquipmentUI;
    private UIEquipment accessoryEquipmentUI;
    private ActiveSkillScreenController activeSkillScreenInstance;
    private BasementCharacterPresentation statusPresentation;
    
    
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
        ResolveAccessoryEquipmentUI();
        if (accessoryScreen != null) accessoryScreen.EquipmentChanged += RefreshStats;
        if (gunMenuScreen != null) gunMenuScreen.EquipmentChanged += RefreshStats;
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
        SyncStatusPresentation();
        LoadLavel();
        // ถ้า BindSlot มาก่อนแล้ว ค่อยซับตรงนี้ได้
        if (levelSystem != null)
            levelSystem.LevelChanged += OnLevelChanged;

        if (_slot != null)
        {
            SendEquipmentData();
            RefreshStats();
        }

    }

    private void OnDisable()
    {
        ReleaseStatusPresentation();
        if (levelSystem != null)
            levelSystem.LevelChanged -= OnLevelChanged;
    }

    private void OnDestroy()
    {
        if (accessoryScreen != null) accessoryScreen.EquipmentChanged -= RefreshStats;
        if (gunMenuScreen != null) gunMenuScreen.EquipmentChanged -= RefreshStats;
        UnsubscribeEquipmentChanged();

        if (activeSkillScreenInstance != null)
        {
            activeSkillScreenInstance.Closed -= OnActiveSkillsClosed;
            Destroy(activeSkillScreenInstance.gameObject);
        }
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
        SyncStatusPresentation();
        if (accessoryScreenButton != null) accessoryScreenButton.interactable = slot != null && slot.Selected != null;

        ClearUI();

        if (_slot == null)
        {
            BindLevelSystem(null);
            ClearEquipmentData();
            Debug.Log("[UILoadLaval] _Slot == null");
            return;
           
        }
        
        characterId = ResolveBoundCharacterId();
        
        BindLevelSystem(_slot.levelSystem);
        LoadLavel();
        Calculatestatusfromslot();
        UpdateStatTexts();
        SendEquipmentData();
    }

    public void SendEquipmentData()
    {
        // Dedicated screens bind when opened. Refreshing their hidden legacy
        // counterparts here rebuilds inventory UI and the weapon preview on Status entry.
        if (gunMenuScreen == null) SendWeaponEquipmentData();
        if (accessoryScreen == null) SendAccessoryEquipmentData();
    }

    // Submenus hide the Canvas, keeping this parent status session alive.
    // Re-resolve when a preview is rebuilt or the public slot binding changes.
    private void LateUpdate() => SyncStatusPresentation();

    private void SyncStatusPresentation()
    {
        // The integrated screen owns its pose through the profile's menu entry.
        if (characterMenu != null)
        {
            ReleaseStatusPresentation();
            return;
        }
        var next = isActiveAndEnabled && _slot != null && _slot.Selected != null
            ? _slot.GetComponent<PartySlotVisualPreview>()?.Presentation : null;
        if (next == statusPresentation)
        {
            // Bind/Unbind may reset animation state without replacing the presentation object.
            if (next != null) next.SetInCharacterMenu(true);
            return;
        }
        ReleaseStatusPresentation();
        statusPresentation = next;
        statusPresentation?.SetInCharacterMenu(true);
    }

    private void ReleaseStatusPresentation()
    {
        if (statusPresentation != null) statusPresentation.SetInCharacterMenu(false);
        statusPresentation = null;
    }

    public void SendWeaponEquipmentData()
    {
        ResolveWeaponEquipmentUI();
        EnsureBoundSlot();

        if (weaponEquipmentUI == null)
            return;

        PlayerInventory inventory = CTX != null ? CTX.playerInventory : null;
        weaponEquipmentUI.BindSource(inventory, ResolveBoundCharacterId(), _slot, EquipmentItemKind.Weapon);
    }

    public void SendAccessoryEquipmentData()
    {
        ResolveAccessoryEquipmentUI();
        EnsureBoundSlot();

        if (accessoryEquipmentUI == null || accessoryEquipmentUI == weaponEquipmentUI)
            return;

        PlayerInventory inventory = CTX != null ? CTX.playerInventory : null;
        accessoryEquipmentUI.BindSource(inventory, ResolveBoundCharacterId(), _slot, EquipmentItemKind.Accessory);
    }

    public void ToggleWeaponEquipment()
    {
        if (gunMenuScreen != null)
        {
            if (gunMenuScreen.IsOpen) gunMenuScreen.Close();
            else OpenWeaponEquipment();
            return;
        }

        ResolveWeaponEquipmentUI();
        ResolveAccessoryEquipmentUI();

        if (weaponEquipmentObject == null)
            return;

        bool nextActive = !weaponEquipmentObject.activeSelf;
        weaponEquipmentObject.SetActive(nextActive);

        if (accessoryEquipmentObject != null && accessoryEquipmentObject != weaponEquipmentObject)
            accessoryEquipmentObject.SetActive(nextActive);

        if (nextActive)
            SendEquipmentData();
    }

    public void ToggleAccessoryEquipment()
    {
        ResolveAccessoryEquipmentUI();

        if (accessoryEquipmentObject == null)
            return;

        bool nextActive = !accessoryEquipmentObject.activeSelf;
        accessoryEquipmentObject.SetActive(nextActive);

        if (nextActive)
            SendAccessoryEquipmentData();
    }

    public void OpenWeaponEquipment()
    {
        if (characterMenu != null && !characterMenu.CanInteract) return;
        if (gunMenuScreen != null)
        {
            EnsureBoundSlot();
            gunMenuScreen.Open(_slot, CTX != null ? CTX.playerInventory : null);
            return;
        }

        ResolveWeaponEquipmentUI();
        ResolveAccessoryEquipmentUI();
        EnsureBoundSlot();

        if (weaponEquipmentObject == null)
            return;

        weaponEquipmentObject.SetActive(true);

        if (accessoryEquipmentObject != null && accessoryEquipmentObject != weaponEquipmentObject)
            accessoryEquipmentObject.SetActive(true);

        SendEquipmentData();
    }

    public void OpenAccessoryEquipment()
    {
        ResolveAccessoryEquipmentUI();
        EnsureBoundSlot();

        if (accessoryEquipmentObject == null)
            return;

        accessoryEquipmentObject.SetActive(true);
        SendAccessoryEquipmentData();
    }

    public void OpenAccessoryScreen()
    {
        if (characterMenu != null && !characterMenu.CanInteract) return;
        if (_slot == null || _slot.Selected == null || accessoryScreen == null || CTX == null) return;
        accessoryScreen.Open(_slot, CTX.playerInventory);
    }

    public void OpenActiveSkillTree()
    {
        if (characterMenu != null && !characterMenu.CanInteract) return;
        EnsureBoundSlot();
        CharacterStats selectedCharacter = _slot != null ? _slot.Selected : null;
        if (selectedCharacter == null)
        {
            Debug.LogWarning("[UILoadLaval] Select a character before opening the Active Skill Tree.", this);
            return;
        }

        if (activeSkillScreenInstance == null && activeSkillScreenPrefab != null)
        {
            activeSkillScreenInstance = Instantiate(activeSkillScreenPrefab);
            activeSkillScreenInstance.Closed += OnActiveSkillsClosed;
        }

        if (activeSkillScreenInstance == null)
        {
            Debug.LogWarning("[UILoadLaval] Active Skill Screen prefab is not assigned.", this);
            return;
        }

        activeSkillScreenInstance.BindLobby(selectedCharacter);
        activeSkillScreenInstance.Open();
        characterMenu?.SuspendForSkills();
    }

    public void CloseActiveSkillTree() => activeSkillScreenInstance?.Close();

    private void OnActiveSkillsClosed()
    {
        characterMenu?.Resume();
    }

    public void CloseWeaponEquipment()
    {
        if (gunMenuScreen != null && gunMenuScreen.IsOpen)
        {
            gunMenuScreen.Close();
            return;
        }

        ResolveWeaponEquipmentUI();
        ResolveAccessoryEquipmentUI();
        EnsureBoundSlot();

        if (_slot != null)
            _slot.RefreshSelectedWeaponVisual();

        if (weaponEquipmentObject != null)
            weaponEquipmentObject.SetActive(false);

        if (accessoryEquipmentObject != null && accessoryEquipmentObject != weaponEquipmentObject)
            accessoryEquipmentObject.SetActive(false);

        RefreshStats();
    }

    public void CloseAccessoryEquipment()
    {
        ResolveAccessoryEquipmentUI();
        EnsureBoundSlot();

        if (accessoryEquipmentObject != null)
            accessoryEquipmentObject.SetActive(false);

        RefreshStats();
    }

    private void ClearWeaponEquipmentData()
    {
        ResolveWeaponEquipmentUI();

        if (weaponEquipmentUI != null)
            weaponEquipmentUI.BindSource(null, null, null);
    }

    private void ClearAccessoryEquipmentData()
    {
        ResolveAccessoryEquipmentUI();

        if (accessoryEquipmentUI != null && accessoryEquipmentUI != weaponEquipmentUI)
            accessoryEquipmentUI.BindSource(null, null, null);
    }

    private void ClearEquipmentData()
    {
        ClearWeaponEquipmentData();
        ClearAccessoryEquipmentData();
    }

    private string ResolveBoundCharacterId()
    {
        EnsureBoundSlot();

        if (_slot == null)
            return null;

        if (_slot.Selected != null && !string.IsNullOrWhiteSpace(_slot.Selected.characterId))
            return _slot.Selected.characterId;

        return _slot.IDCharacter;
    }

    private void EnsureBoundSlot()
    {
        if (_slot != null)
            return;

        PartySlot fallback = FindDefaultPartySlot();
        if (fallback == null)
            return;

        _slot = fallback;
        characterId = ResolveBoundCharacterIdWithoutFallback();
        BindLevelSystem(_slot.levelSystem);
    }

    private PartySlot FindDefaultPartySlot()
    {
        var slots = FindObjectsByType<PartySlot>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        PartySlot best = null;

        for (int i = 0; i < slots.Length; i++)
        {
            PartySlot slot = slots[i];
            if (slot == null || slot.Selected == null)
                continue;

            if (best == null || slot.PartyIndex < best.PartyIndex)
                best = slot;
        }

        return best;
    }

    private string ResolveBoundCharacterIdWithoutFallback()
    {
        if (_slot == null)
            return null;

        if (_slot.Selected != null && !string.IsNullOrWhiteSpace(_slot.Selected.characterId))
            return _slot.Selected.characterId;

        return _slot.IDCharacter;
    }

    private void ResolveWeaponEquipmentUI()
    {
        UIEquipment previous = weaponEquipmentUI;
        weaponEquipmentUI = ResolveEquipmentUI(weaponEquipmentObject, EquipmentItemKind.Weapon, null);
        RebindEquipmentChanged(previous, weaponEquipmentUI);

        if (weaponEquipmentUI != null && weaponEquipmentObject == null)
            weaponEquipmentObject = weaponEquipmentUI.gameObject;
    }

    private void ResolveAccessoryEquipmentUI()
    {
        UIEquipment previous = accessoryEquipmentUI;
        accessoryEquipmentUI = ResolveEquipmentUI(accessoryEquipmentObject, EquipmentItemKind.Accessory, weaponEquipmentUI);
        RebindEquipmentChanged(previous, accessoryEquipmentUI);

        if (accessoryEquipmentUI != null && accessoryEquipmentObject == null)
            accessoryEquipmentObject = accessoryEquipmentUI.gameObject;
    }

    private void RebindEquipmentChanged(UIEquipment previous, UIEquipment next)
    {
        if (previous == next)
            return;

        if (previous != null)
            previous.EquipmentChanged -= HandleEquipmentChanged;

        if (next != null)
            next.EquipmentChanged += HandleEquipmentChanged;
    }

    private void UnsubscribeEquipmentChanged()
    {
        if (weaponEquipmentUI != null)
            weaponEquipmentUI.EquipmentChanged -= HandleEquipmentChanged;

        if (accessoryEquipmentUI != null)
            accessoryEquipmentUI.EquipmentChanged -= HandleEquipmentChanged;
    }

    private UIEquipment ResolveEquipmentUI(GameObject equipmentObject, EquipmentItemKind itemKind, UIEquipment excludedUI)
    {
        if (equipmentObject != null)
        {
            UIEquipment equipmentUI = equipmentObject.GetComponent<UIEquipment>();
            if (equipmentUI == null)
                equipmentUI = equipmentObject.GetComponentInChildren<UIEquipment>(true);

            if (equipmentUI == null)
                return null;

            if (equipmentUI != excludedUI)
                return equipmentUI;

            Debug.LogWarning($"[UILoadLaval] {itemKind} equipment UI cannot use the same UIEquipment instance.", this);
            return null;
        }

        UIEquipment[] equipmentUIs = transform.root != null
            ? transform.root.GetComponentsInChildren<UIEquipment>(true)
            : FindObjectsByType<UIEquipment>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < equipmentUIs.Length; i++)
        {
            UIEquipment candidate = equipmentUIs[i];
            if (candidate != null && candidate.GetComponentInParent<BasementAccessoryScreenController>(true) != null)
                continue;
            if (candidate == null || candidate == excludedUI || candidate.EquipmentKind != itemKind)
                continue;

            return candidate;
        }

        if (itemKind != EquipmentItemKind.Weapon)
            return null;

        for (int i = 0; i < equipmentUIs.Length; i++)
        {
            UIEquipment candidate = equipmentUIs[i];
            if (candidate != null && candidate.GetComponentInParent<BasementAccessoryScreenController>(true) != null)
                continue;
            if (candidate != null && candidate != excludedUI)
                return candidate;
        }

        return null;
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

        Name = s.characterName;

        // Final combined stats: base + level + weapon + accessories + passives,
        // computed through the same formula StatsHub uses in combat.
        CharacterStatTotals totals = LobbyCharacterStatPreview.Build(
            s,
            levelCurrent,
            ResolveBoundCharacterId(),
            CTX != null ? CTX.playerInventory : null,
            affixDatabase);

        Damage     = totals.Damage;
        Armor      = totals.Armor;
        MAXHP      = totals.MaxHealth;
        Stamina    = totals.MaxStamina;
        Enagy      = totals.MaxEnergy;
        Critrate   = totals.CritRatePercent;
        CritDamage = totals.CritMultiplier;
        Speed      = totals.MoveSpeed;
    }

    /// <summary>Recalculates and redraws the stat block. Safe to call while the panel is open.</summary>
    public void RefreshStats()
    {
        if (_slot == null || _slot.Selected == null)
            return;

        LoadLavel();
        Calculatestatusfromslot();
        UpdateStatTexts();
    }

    private void HandleEquipmentChanged()
    {
        RefreshStats();
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
        if (characterMenu != null && !characterMenu.CanInteract) return;
        
       
        if (CTX.playerInventory.Gold < cost) return;
        CurrencyManager.Instance.SpendGold(CTX.playerInventory ,cost);
        XpManager.Instance.GrantXp(levelSystem, cost);
        UpdateExpTexts();
        UpdateGold();
        RefreshStats();

    }
    

}
