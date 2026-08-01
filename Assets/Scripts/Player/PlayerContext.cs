using System;
using UnityEngine;

[DefaultExecutionOrder(-200)]
public class PlayerContext : CharacteContext
{
    public static PlayerContext Instance { get; private set; }
    [Header("Modules")]
    public PlayerUIContext playerUIContext;

    [Header("Modules")]
    public PlayerMovementCC movement;
    public AllyHelperManager allyHelper;
    public FieldAllyManager fieldAllyManager;
    public PartyFormationController partyFormation;
    public ChainAttackCoordinator chainAttackCoordinator;
    public PartyCommandController partyCommand;
    public InterruptionCommandController interruptionCommand;
    public ThirdPersonAimController thirdPersonAim;

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

    public override bool UsesWorldSlow => false;
    public override AITargetIdentity TargetIdentity => AITargetIdentity.Player;

    void Awake()
    {
        Instance = this;
        ResolveReferences();
        TryMigrateLegacyPartyCommandConfiguration();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        UIMunuBar ui = UnityEngine.Object.FindAnyObjectByType<UIMunuBar>(FindObjectsInactive.Include);
        if (ui != null)
            ui.SetContext(this);
    }

    public override void ResolveReferences()
    {
        base.ResolveReferences();

        if (playerUIContext == null)
            playerUIContext = ResolveActorComponent(playerUIContext);

        if (allyHelper == null)
            allyHelper = ResolveActorComponent(allyHelper);

        if (fieldAllyManager == null)
            fieldAllyManager = ResolveActorComponent(fieldAllyManager);

        if (partyFormation == null)
            partyFormation = ResolveActorComponent(partyFormation);

        if (chainAttackCoordinator == null)
            chainAttackCoordinator = ResolveActorComponent(chainAttackCoordinator);

        if (partyCommand == null)
            partyCommand = ResolveActorComponent(partyCommand);

        if (interruptionCommand == null)
            interruptionCommand = ResolveActorComponent(interruptionCommand);

        if (movement == null)
            movement = ResolveActorComponent(movement);

        if (inventory == null)
            inventory = ResolveActorComponent(inventory);

        if (thirdPersonAim == null)
            thirdPersonAim = ResolveActorComponent(thirdPersonAim);

        if (Application.isPlaying && thirdPersonAim == null)
            thirdPersonAim = gameObject.AddComponent<ThirdPersonAimController>();
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
