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
        if (IsPlayAble)
        { 
            ctx = GetComponent<CharacteContext>(); 
        }
        
        if (!SaveManager.Instance)
        {
            var def = db.GetById(fallbackId);
            ctx.baseStats = def;
            CurrentContext = def;
            Debug.Log("loading character context party");
        }
    }


    public void OnSave(GameSaveData data) { }

    public void OnLoad(GameSaveData data)
    {
        if (IsPlayAble)
        {
            if (!db || ctx == null || data == null) return;

            string id = "";
            var party = data.party;

            if (party?.partyIds != null && party.partyIds.Count > partyIndex)
                id = party.partyIds[partyIndex];

            if (string.IsNullOrWhiteSpace(id)) id = fallbackId;
            if (string.IsNullOrWhiteSpace(id)) return;

            var def = db.GetById(id);
            if (!def)
            {
                Debug.LogError($"[CharacterContextPartyLoader] Character id not found: {id}", this);
                return;
            }
        
            ctx.baseStats = def; 
        }
        else
        {
            if (!db || data == null) return;
            
            string id = "";
            var party = data.party;

            if (party?.partyIds != null && party.partyIds.Count > partyIndex)
                id = party.partyIds[partyIndex];

            if (string.IsNullOrWhiteSpace(id)) id = fallbackId;
            if (string.IsNullOrWhiteSpace(id)) return;

            var def = db.GetById(id);
            if (!def)
            {
                Debug.LogError($"[CharacterContextPartyLoader] Character id not found: {id}", this);
                return;
            }
            CurrentContext = def;
        }
       
    }
    
}