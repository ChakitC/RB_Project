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
    public CharacterStats Selected => selected;

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

        var id = SaveManager.Instance.LoadPartyMemberId(currentSlot, partyIndex);
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

    public void RefreshSelectedWeaponVisual()
    {
        EnsureVisualPreview();
        visualPreview?.RefreshWeapon(selected);
    }

    public void ClearTemporarySelectObjects()
    {
        EnsureVisualPreview();
        visualPreview?.ClearTemporarySelectObjects(transform);
    }

    public void SetCharacterDef(CharacterStats def, bool save)
    {
        selected = def;
        IDCharacter = def ? def.characterId : "";

        if (save && SaveManager.Instance != null)
            SaveManager.Instance.SaveParty();
    }

    void EnsureVisualPreview()
    {
        if (!visualPreview)
            TryGetComponent(out visualPreview);

        if (!visualPreview)
            visualPreview = gameObject.AddComponent<PartySlotVisualPreview>();

        visualPreview.SetPartyContext(currentSlot, partyIndex);
    }
}
