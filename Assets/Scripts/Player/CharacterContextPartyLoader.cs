using System;
using Opsive.BehaviorDesigner.Runtime;
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
    public int PartyIndex => partyIndex;

    /// <summary>
    /// Raised with (previous, current) after this loader swaps the character on its context.
    ///
    /// Actors that own character-sourced data cannot poll for this: the helper is deactivated
    /// between summons, so its own <c>Update</c> never runs while the party changes underneath it.
    /// </summary>
    public event Action<CharacterStats, CharacterStats> BaseStatsChanged;

    public void ConfigurePartyIndex(int index)
    {
        if (index < 0)
            throw new System.ArgumentOutOfRangeException(nameof(index));

        partyIndex = index;
    }

    public bool TryApplyRuntimeDefinition()
    {
        EnsureContextReference();
        return TryApplySavedOrFallbackDefinition(null);
    }

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
        if (ctx == null)
            ctx = GetComponentInParent<CharacteContext>();

        ctx?.ResolveReferences();

        if (ctx != null && ctx.CharacterLoad != this)
            ctx.CharacterLoad = this;
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
        {
            CharacterStats previous = ctx.baseStats;
            ctx.baseStats = def;
            if (ctx.UsesPersistentProgression)
                ctx.ActiveSkillProgress?.ReloadFromSave();
            ApplyBehaviorSubtree(def);

            // Reloading the same character is a no-op for anything downstream, so do not make
            // listeners cancel casts and rebuild loadouts over it.
            if (previous != def)
                BaseStatsChanged?.Invoke(previous, def);
        }
        else if (IsPlayAble)
            Debug.LogWarning("[CharacterContextPartyLoader] Playable loader is missing CharacteContext.", this);
    }

    void ApplyBehaviorSubtree(CharacterStats def)
    {
        if (def == null || def.behaviorSubtree == null || ctx is not AllyContext allyContext)
            return;

        allyContext.ResolveReferences();
        BehaviorTree behaviorTree = allyContext.BehaviorTree;
        if (behaviorTree != null && !object.ReferenceEquals(behaviorTree.Subgraph, def.behaviorSubtree))
            behaviorTree.Subgraph = def.behaviorSubtree;
    }
}
