using System;
using UnityEngine;

public class PlayerContext : CharacteContext
{
    [Header("Modules")]
    public PlayerUIContext playerUIContext;

    [Header("Modules")]
    public PlayerMovementCC movement;
    public AllyHelperManager allyHelper;
    public FieldAllyManager fieldAllyManager;
    public ChainAttackCoordinator chainAttackCoordinator;
    public PartyCommandController partyCommand;

    [Header("Inventory")]
    public PlayerInventory inventory;

    [Header("Aim Target Reference")]
    public Transform aimTarget;

    // Legacy party-command data is kept hidden here so existing scenes/prefabs
    // can bootstrap the new controller component without losing authoring data.
    [SerializeField, HideInInspector, Min(0f)] private float maximumCommandPoints = 3f;
    [SerializeField, HideInInspector, Min(0f)] private float currentCommandPoints;
    [SerializeField, HideInInspector] private bool fillCommandPointsOnStart = true;
    [SerializeField, HideInInspector, Min(0f)] private float commandPointRegenPerSecond = 0.25f;
    [SerializeField, HideInInspector] private PartyCommandDefinition[] partyCommands = Array.Empty<PartyCommandDefinition>();
    [SerializeField, HideInInspector] private int selectedPartyCommandIndex;
    [SerializeField, HideInInspector] private bool wrapPartyCommandSelection = true;
    [SerializeField, HideInInspector] private bool logPartyCommandExecution;

    void Awake()
    {
        ResolveReferences();
        TryMigrateLegacyPartyCommandConfiguration();
    }

    void Start()
    {
        UIMunuBar ui = UnityEngine.Object.FindAnyObjectByType<UIMunuBar>(FindObjectsInactive.Include);
        if (ui != null)
            ui.SetContext(this);
    }

    public void ResolveReferences()
    {
        if (allyHelper == null)
            allyHelper = GetComponent<AllyHelperManager>();

        if (fieldAllyManager == null)
            fieldAllyManager = GetComponent<FieldAllyManager>();

        if (chainAttackCoordinator == null)
            chainAttackCoordinator = GetComponent<ChainAttackCoordinator>();

        if (partyCommand == null)
            partyCommand = GetComponent<PartyCommandController>();
    }

    void TryMigrateLegacyPartyCommandConfiguration()
    {
        if (!HasLegacyPartyCommandConfiguration())
            return;

        if (partyCommand == null)
            partyCommand = GetComponent<PartyCommandController>();

        if (partyCommand == null)
            partyCommand = gameObject.AddComponent<PartyCommandController>();

        partyCommand.ImportLegacyConfiguration(
            maximumCommandPoints,
            currentCommandPoints,
            fillCommandPointsOnStart,
            commandPointRegenPerSecond,
            partyCommands,
            selectedPartyCommandIndex,
            wrapPartyCommandSelection,
            logPartyCommandExecution,
            overwriteIfPartyCommandsEmpty: true);
    }

    bool HasLegacyPartyCommandConfiguration()
    {
        return partyCommands != null && partyCommands.Length > 0;
    }
}
