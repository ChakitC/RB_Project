using UnityEngine;


public class CharacterContextPartyLoader : MonoBehaviour, IGameSaveAble, ISaveOrder
{
    [Header("Characters")]
    [SerializeField] private bool IsPlayAble;
    [SerializeField] public CharacterStats CurrentContext;
    
    [SerializeField] private CharacterDatabase db;
    [SerializeField] private int partyIndex = 0;     // 0 = player
    [SerializeField] private string fallbackId = "";

    private CharacteContext ctx;

    // ให้ทำก่อน PlayerVisual
    public int LoadOrder => -100;

    void Awake()
    {
        EnsureContextReference();
        TryApplySavedOrFallbackDefinition(null);
    }


    public void OnSave(GameSaveData data) { }

    public void OnLoad(GameSaveData data)
    {
        EnsureContextReference();
        TryApplySavedOrFallbackDefinition(data);
    }

    void EnsureContextReference()
    {
        if (ctx == null)
            ctx = GetComponent<CharacteContext>();
    }

    bool TryApplySavedOrFallbackDefinition(GameSaveData data)
    {
        if (!db)
            return false;

        string id = ResolveCharacterId(data);
        if (string.IsNullOrWhiteSpace(id))
            return false;

        var def = db.GetById(id);
        if (!def)
        {
            Debug.LogError($"[CharacterContextPartyLoader] Character id not found: {id}", this);
            return false;
        }

        ApplyDefinition(def);
        return true;
    }

    string ResolveCharacterId(GameSaveData data)
    {
        string id = "";
        var party = data?.party;

        if (party?.partyIds != null && party.partyIds.Count > partyIndex)
            id = party.partyIds[partyIndex];

        if (string.IsNullOrWhiteSpace(id) && SaveManager.Instance != null)
            id = SaveManager.Instance.LoadPartyMemberId(SaveManager.Instance.currentSlot, partyIndex);

        if (string.IsNullOrWhiteSpace(id))
            id = fallbackId;

        return id;
    }

    void ApplyDefinition(CharacterStats def)
    {
        CurrentContext = def;

        if (ctx != null)
            ctx.baseStats = def;
        else if (IsPlayAble)
            Debug.LogWarning("[CharacterContextPartyLoader] Playable loader is missing CharacteContext.", this);
    }
}
