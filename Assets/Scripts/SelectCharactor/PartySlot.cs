using UnityEngine;

[RequireComponent(typeof(PartySlotVisualPreview))]
public class PartySlot : MonoBehaviour
{
    [Header("REF")]
    public LevelSystem levelSystem;

    [Header("Data")]
    [SerializeField] private CharacterDatabase db;
    [SerializeField] private int currentSlot;
    [SerializeField] private int partyIndex = 0;     // 0 = player
    [SerializeField] private string fallbackId = "";

    [Header("Visual Preview")]
    [SerializeField] private PartySlotVisualPreview visualPreview;

    [Header("Runtime")]
    public string IDCharacter;

    [SerializeField] private CharacterStats selected;

    public int LoadOrder => -100;
    public int CurrentSlot => SaveManager.Instance != null ? SaveManager.Instance.currentSlot : currentSlot;
    public int PartyIndex => partyIndex;
    public CharacterStats Selected => selected;
    public event System.Action<CharacterStats> SelectedChanged;

    void Awake()
    {
        EnsureVisualPreview();
    }

    void Start()
    {
        LoadParty();

        if (levelSystem != null)
            levelSystem.SetState();
    }

    public void LoadParty()
    {
        EnsureVisualPreview();
        visualPreview?.ClearTemporarySelectObjects(transform);

        if (SaveManager.Instance == null)
        {
            IDCharacter = fallbackId;
            return;
        }

        var id = SaveManager.Instance.LoadPartyMemberId(CurrentSlot, partyIndex);
        if (string.IsNullOrEmpty(id))
            id = fallbackId;

        SetCharacterById(id, save: false);
        BuildModel();
    }

    public void SetCharacterById(string id, bool save)
    {
        if (db == null)
        {
            Debug.LogError("[PartySlot] db null", this);
            return;
        }

        var def = db.GetById(id);
        if (def == null)
        {
            Debug.LogError($"[PartySlot] id not found: {id}", this);
            return;
        }

        def = ResolveUnlockedDefinition(def);
        if (def == null)
            return;

        SetCharacterDef(def, save);
    }

    public void BuildModel()
    {
        EnsureVisualPreview();
        visualPreview?.BuildCharacter(selected, transform);
    }

    public void RefreshSelectedCharacterVisual()
    {
        BuildModel();

        if (levelSystem != null)
            levelSystem.SetState();
    }

    public void RefreshSelectedWeaponVisual(
        string preferredEquippedInstanceId = null,
        PlayerInventory preferredInventory = null)
    {
        EnsureVisualPreview();
        visualPreview?.RefreshWeapon(selected, preferredEquippedInstanceId, preferredInventory);
    }

    public void ClearTemporarySelectObjects()
    {
        EnsureVisualPreview();
        visualPreview?.ClearTemporarySelectObjects(transform);
    }

    public void SetCharacterDef(CharacterStats def, bool save)
    {
        if (def != null && !CharacterUnlockService.IsUnlockedForSelection(def))
        {
            Debug.LogWarning($"[PartySlot] Character is locked: {def.characterId}", this);
            return;
        }

        selected = def;
        IDCharacter = def ? def.characterId : "";
        SelectedChanged?.Invoke(selected);

        if (save && SaveManager.Instance != null)
            SaveManager.Instance.SaveParty();
    }

    CharacterStats ResolveUnlockedDefinition(CharacterStats requested)
    {
        if (requested == null || CharacterUnlockService.IsUnlockedForSelection(requested))
            return requested;

        Debug.LogWarning($"[PartySlot] Saved/selected character is locked: {requested.characterId}", this);

        CharacterStats fallback = ResolveUnlockedFallback();
        if (fallback != null)
            return fallback;

        Debug.LogWarning("[PartySlot] No unlocked fallback character found.", this);
        return null;
    }

    CharacterStats ResolveUnlockedFallback()
    {
        if (db == null)
            return null;

        if (!string.IsNullOrWhiteSpace(fallbackId))
        {
            CharacterStats fallback = db.GetById(fallbackId);
            if (fallback != null && CharacterUnlockService.IsUnlockedForSelection(fallback))
                return fallback;
        }

        if (selected != null && CharacterUnlockService.IsUnlockedForSelection(selected))
            return selected;

        if (db.characters == null)
            return null;

        for (int i = 0; i < db.characters.Count; i++)
        {
            CharacterStats candidate = db.characters[i];
            if (candidate != null && CharacterUnlockService.IsUnlockedForSelection(candidate))
                return candidate;
        }

        return null;
    }

    void EnsureVisualPreview()
    {
        if (!visualPreview)
            TryGetComponent(out visualPreview);

        if (!visualPreview)
            visualPreview = gameObject.AddComponent<PartySlotVisualPreview>();

        visualPreview.SetPartyContext(CurrentSlot, partyIndex);
    }
}
