using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PartyCommandController : MonoBehaviour
{
    [SerializeField] private PlayerContext playerContext;
    [SerializeField] private ChainAttackProcController chainAttackProcController;

    [Header("Party Command")]
    [SerializeField, Min(0f)] private float maximumCommandPoints = 3f;
    [SerializeField, Min(0f)] private float currentCommandPoints;
    [SerializeField] private bool fillCommandPointsOnStart = true;
    [SerializeField, Min(0f)] private float commandPointRegenPerSecond = 0.25f;
    [SerializeField] private PartyCommandDefinition[] partyCommands = Array.Empty<PartyCommandDefinition>();
    [SerializeField] private int selectedPartyCommandIndex;
    [SerializeField] private bool wrapPartyCommandSelection = true;
    [SerializeField] private bool logPartyCommandExecution;

    readonly Dictionary<int, float> _partyCommandReadyAt = new();

    public event Action<float, float> CommandPointsChanged;
    public event Action<PartyCommandLabelData> PartyCommandLabelChanged;

    public float MaximumCommandPoints => maximumCommandPoints;
    public float CurrentCommandPoints => currentCommandPoints;
    public bool HasPartyCommands => GetFirstConfiguredPartyCommandIndex() >= 0;
    public int SelectedPartyCommandIndex => NormalizeSelectedPartyCommandSelection();
    public PartyCommandDefinition[] PartyCommands => partyCommands;

    void Awake()
    {
        ResolveReferences();
        InitializeRuntimeStateFromSerializedData();
    }

    void Start()
    {
        NotifyCommandPointsChanged();
        RaisePartyCommandLabel();
    }

    void Update()
    {
        RegenerateCommandPoints();
    }

    public void ImportLegacyConfiguration(
        float legacyMaximumCommandPoints,
        float legacyCurrentCommandPoints,
        bool legacyFillCommandPointsOnStart,
        float legacyCommandPointRegenPerSecond,
        PartyCommandDefinition[] legacyPartyCommands,
        int legacySelectedPartyCommandIndex,
        bool legacyWrapPartyCommandSelection,
        bool legacyLogPartyCommandExecution,
        bool overwriteIfPartyCommandsEmpty = true)
    {
        if (legacyPartyCommands == null || legacyPartyCommands.Length == 0)
            return;

        if (overwriteIfPartyCommandsEmpty && HasAuthoringConfiguration())
            return;

        maximumCommandPoints = Mathf.Max(0f, legacyMaximumCommandPoints);
        currentCommandPoints = legacyCurrentCommandPoints;
        fillCommandPointsOnStart = legacyFillCommandPointsOnStart;
        commandPointRegenPerSecond = Mathf.Max(0f, legacyCommandPointRegenPerSecond);
        partyCommands = legacyPartyCommands;
        selectedPartyCommandIndex = legacySelectedPartyCommandIndex;
        wrapPartyCommandSelection = legacyWrapPartyCommandSelection;
        logPartyCommandExecution = legacyLogPartyCommandExecution;

        _partyCommandReadyAt.Clear();
        InitializeRuntimeStateFromSerializedData();
        NotifyCommandPointsChanged();
        RaisePartyCommandLabel();
    }

    void ResolveReferences()
    {
        if (playerContext == null)
            playerContext = GetComponent<PlayerContext>();

        if (playerContext == null)
            return;

        if (playerContext.partyCommand == null)
            playerContext.partyCommand = this;

        playerContext.ResolveReferences();

        if (chainAttackProcController == null)
            chainAttackProcController = playerContext.GetComponentInChildren<ChainAttackProcController>(true);
    }

    void InitializeRuntimeStateFromSerializedData()
    {
        if (fillCommandPointsOnStart)
            currentCommandPoints = maximumCommandPoints;
        else
            currentCommandPoints = Mathf.Clamp(currentCommandPoints, 0f, maximumCommandPoints);

        NormalizeSelectedPartyCommandSelection();
    }

    bool HasAuthoringConfiguration()
    {
        return partyCommands != null && partyCommands.Length > 0;
    }

    void RegenerateCommandPoints()
    {
        if (commandPointRegenPerSecond <= 0f || currentCommandPoints >= maximumCommandPoints)
            return;

        float nextValue = Mathf.Min(
            maximumCommandPoints,
            currentCommandPoints + commandPointRegenPerSecond * Time.deltaTime);

        if (Mathf.Approximately(nextValue, currentCommandPoints))
            return;

        currentCommandPoints = nextValue;
        NotifyCommandPointsChanged();
    }

    public bool CanSpendCommandPoints(float amount)
    {
        if (!float.IsFinite(amount))
            return false;

        if (amount <= 0f)
            return true;

        return currentCommandPoints + 0.0001f >= amount;
    }

    public bool TrySpendCommandPoints(float amount)
    {
        if (!CanSpendCommandPoints(amount))
            return false;

        if (amount <= 0f)
            return true;

        currentCommandPoints = Mathf.Max(0f, currentCommandPoints - amount);
        NotifyCommandPointsChanged();
        return true;
    }

    public bool CanRestoreCommandPoints(float amount)
    {
        if (!float.IsFinite(amount) || amount <= 0f)
            return false;

        return currentCommandPoints < maximumCommandPoints;
    }

    public bool RestoreCommandPoints(float amount)
    {
        if (!CanRestoreCommandPoints(amount))
            return false;

        float nextValue = Mathf.Clamp(currentCommandPoints + amount, 0f, maximumCommandPoints);
        if (nextValue <= currentCommandPoints)
            return false;

        currentCommandPoints = nextValue;
        NotifyCommandPointsChanged();
        return true;
    }

    public void FillCommandPoints()
    {
        currentCommandPoints = maximumCommandPoints;
        NotifyCommandPointsChanged();
    }

    public void SetMaximumCommandPoints(float value, bool fillCurrent = false)
    {
        maximumCommandPoints = Mathf.Max(0f, value);
        currentCommandPoints = fillCurrent
            ? maximumCommandPoints
            : Mathf.Clamp(currentCommandPoints, 0f, maximumCommandPoints);
        NotifyCommandPointsChanged();
    }

    void NotifyCommandPointsChanged()
    {
        CommandPointsChanged?.Invoke(currentCommandPoints, maximumCommandPoints);
        RaisePartyCommandLabel();
    }

    public bool SelectNextPartyCommand()
    {
        return TrySelectRelativePartyCommand(1);
    }

    public bool SelectPreviousPartyCommand()
    {
        return TrySelectRelativePartyCommand(-1);
    }

    public bool TrySelectPartyCommandSlot(int slotIndex)
    {
        if (!TryGetConfiguredPartyCommand(slotIndex, out _))
            return false;

        selectedPartyCommandIndex = slotIndex;
        RaisePartyCommandLabel();
        return true;
    }

    bool TrySelectRelativePartyCommand(int step)
    {
        int nextIndex = FindRelativePartyCommandIndex(step);
        if (nextIndex < 0)
            return false;

        selectedPartyCommandIndex = nextIndex;
        RaisePartyCommandLabel();
        return true;
    }

    int FindRelativePartyCommandIndex(int step)
    {
        if (partyCommands == null || partyCommands.Length == 0)
            return -1;

        int currentIndex = NormalizeSelectedPartyCommandSelection();
        if (currentIndex < 0)
            return GetFirstConfiguredPartyCommandIndex();

        int index = currentIndex + step;
        for (int i = 0; i < partyCommands.Length; i++)
        {
            if (index < 0 || index >= partyCommands.Length)
            {
                if (!wrapPartyCommandSelection)
                    return -1;

                index = Mod(index, partyCommands.Length);
            }

            if (IsPartyCommandConfigured(partyCommands[index]))
                return index;

            index += step;
        }

        return -1;
    }

    int NormalizeSelectedPartyCommandSelection()
    {
        if (TryGetConfiguredPartyCommand(selectedPartyCommandIndex, out _))
            return selectedPartyCommandIndex;

        selectedPartyCommandIndex = GetFirstConfiguredPartyCommandIndex();
        return selectedPartyCommandIndex;
    }

    int GetFirstConfiguredPartyCommandIndex()
    {
        if (partyCommands == null)
            return -1;

        for (int i = 0; i < partyCommands.Length; i++)
        {
            if (IsPartyCommandConfigured(partyCommands[i]))
                return i;
        }

        return -1;
    }

    bool TryGetConfiguredPartyCommand(int index, out PartyCommandDefinition command)
    {
        command = null;

        if (partyCommands == null || index < 0 || index >= partyCommands.Length)
            return false;

        command = partyCommands[index];
        return IsPartyCommandConfigured(command);
    }

    static bool IsPartyCommandConfigured(PartyCommandDefinition command)
    {
        return command != null && command.HasExecutionConfigured;
    }

    public bool TryExecuteSelectedPartyCommand()
    {
        int index = NormalizeSelectedPartyCommandSelection();
        if (index < 0)
            return false;

        return TryExecutePartyCommand(index);
    }

    public bool TryExecuteChainAttack(SkillChainDef chainDef)
    {
        ResolveReferences();

        string chainLabel = ResolveChainAttackLabel(chainDef);
        if (TryGetChainAttackBlockReason(chainDef, out PartyCommandBlockReason blockReason))
        {
            if (logPartyCommandExecution)
            {
                Debug.Log(
                    $"[PartyCommandController] Chain attack '{chainLabel}' blocked: " +
                    $"{BuildChainAttackBlockReasonLabel(chainDef, blockReason)}.",
                    this);
            }

            RaisePartyCommandLabel();
            return false;
        }

        bool started = chainAttackProcController.TryStartManualSequence(chainDef);
        if (!started)
        {
            if (logPartyCommandExecution)
            {
                Debug.LogWarning(
                    $"[PartyCommandController] Chain attack '{chainLabel}' failed to start " +
                    "after passing validation.",
                    this);
            }

            RaisePartyCommandLabel();
            return false;
        }

        float commandPointCost = chainDef.ClampedCommandPointCost;
        if (commandPointCost > 0f && !TrySpendCommandPoints(commandPointCost))
        {
            Debug.LogWarning(
                $"[PartyCommandController] Chain attack '{chainLabel}' started but " +
                "command points could not be spent.",
                this);
        }

        if (logPartyCommandExecution)
        {
            Debug.Log(
                $"[PartyCommandController] Executed chain attack '{chainLabel}' for " +
                $"{commandPointCost:0.##} CP.",
                this);
        }

        RaisePartyCommandLabel();
        return true;
    }

    public bool TryExecutePartyCommand(int index)
    {
        ResolveReferences();

        if (!TryGetConfiguredPartyCommand(index, out PartyCommandDefinition command))
            return false;

        selectedPartyCommandIndex = index;

        if (TryGetPartyCommandBlockReason(index, command, out PartyCommandBlockReason blockReason))
        {
            if (logPartyCommandExecution)
            {
                Debug.Log(
                    $"[PartyCommandController] Party command '{command.ResolvedDisplayName}' blocked: " +
                    $"{BuildPartyCommandBlockReasonLabel(index, blockReason)}.",
                    this);
            }

            RaisePartyCommandLabel();
            return false;
        }

        AllyHelperManager helperManager = playerContext != null ? playerContext.allyHelper : null;
        ChainAttackCoordinator coordinator = playerContext != null ? playerContext.chainAttackCoordinator : null;

        bool started = command.executionKind switch
        {
            PartyCommandExecutionKind.HelperSkill => helperManager != null &&
                                                     helperManager.TrySummonAllyHelper(
                                                         command.helperSkill,
                                                         command.ClampedHelperSkillLevel,
                                                         command.helperHideOnComplete),
            PartyCommandExecutionKind.HelperCommandSlot => helperManager != null &&
                                                           helperManager.TryExecuteCommandSlot(
                                                               command.ClampedHelperCommandSlotIndex,
                                                               command.helperHideOnComplete),
            PartyCommandExecutionKind.HelperChainAttack => helperManager != null &&
                                                           helperManager.TryStartChainAttackHelper(
                                                               command.helperChainAttackSequence,
                                                               command.helperSkill,
                                                               command.ClampedHelperSkillLevel,
                                                               command.helperHideOnComplete,
                                                               command.helperChainContinueMode,
                                                               command.ClampedHelperChainContinueNormalizedTime),
            PartyCommandExecutionKind.AllyCommandSlot => TryExecuteAllyCommandSlot(command),
            PartyCommandExecutionKind.AllySequence => coordinator != null &&
                                                      coordinator.TryStartSequence(command.allySequence),
            _ => false,
        };

        if (!started)
        {
            if (logPartyCommandExecution)
            {
                Debug.LogWarning(
                    $"[PartyCommandController] Party command '{command.ResolvedDisplayName}' failed to start " +
                    "after passing validation.",
                    this);
            }

            RaisePartyCommandLabel();
            return false;
        }

        float commandPointCost = command.ClampedCommandPointCost;
        if (commandPointCost > 0f && !TrySpendCommandPoints(commandPointCost))
        {
            Debug.LogWarning(
                $"[PartyCommandController] Party command '{command.ResolvedDisplayName}' started but " +
                "command points could not be spent.",
                this);
        }

        StampPartyCommandCooldown(index, command);

        if (logPartyCommandExecution)
        {
            Debug.Log(
                $"[PartyCommandController] Executed party command '{command.ResolvedDisplayName}' for " +
                $"{commandPointCost:0.##} CP.",
                this);
        }

        RaisePartyCommandLabel();
        return true;
    }

    bool TryGetPartyCommandBlockReason(
        int commandIndex,
        PartyCommandDefinition command,
        out PartyCommandBlockReason reason)
    {
        reason = PartyCommandBlockReason.None;

        if (command == null || !command.HasExecutionConfigured)
        {
            reason = PartyCommandBlockReason.MissingConfig;
            return true;
        }

        if (command.requireOwnerAlive && !IsPartyCommandOwnerAvailable())
        {
            reason = PartyCommandBlockReason.OwnerUnavailable;
            return true;
        }

        if (IsPartyCommandOnCooldown(commandIndex, out _))
        {
            reason = PartyCommandBlockReason.Cooldown;
            return true;
        }

        if (!CanSpendCommandPoints(command.ClampedCommandPointCost))
        {
            reason = PartyCommandBlockReason.NotEnoughCommandPoints;
            return true;
        }

        switch (command.executionKind)
        {
            case PartyCommandExecutionKind.HelperSkill:
                return TryGetHelperPartyCommandBlockReason(null, out reason);

            case PartyCommandExecutionKind.HelperCommandSlot:
                return TryGetHelperCommandSlotBlockReason(command.ClampedHelperCommandSlotIndex, out reason);

            case PartyCommandExecutionKind.HelperChainAttack:
                return TryGetHelperPartyCommandBlockReason(command.helperChainAttackSequence, out reason);

            case PartyCommandExecutionKind.AllyCommandSlot:
                return TryGetAllyCommandSlotBlockReason(command.allyCommandActorRole, command.ClampedAllyCommandSlotIndex, out reason);

            case PartyCommandExecutionKind.AllySequence:
                return TryGetAllySequencePartyCommandBlockReason(command.allySequence, out reason);

            default:
                reason = PartyCommandBlockReason.MissingConfig;
                return true;
        }
    }

    bool TryGetChainAttackBlockReason(
        SkillChainDef chainDef,
        out PartyCommandBlockReason reason)
    {
        reason = PartyCommandBlockReason.None;

        if (chainDef == null || !chainDef.HasExecutionConfigured)
        {
            reason = PartyCommandBlockReason.MissingConfig;
            return true;
        }

        if (chainDef.requireOwnerAlive && !IsPartyCommandOwnerAvailable())
        {
            reason = PartyCommandBlockReason.OwnerUnavailable;
            return true;
        }

        if (chainAttackProcController == null)
        {
            reason = PartyCommandBlockReason.SequenceUnavailable;
            return true;
        }

        if (chainDef.blockWhileChainBusy && chainAttackProcController.IsSequenceActive)
        {
            reason = PartyCommandBlockReason.SequenceBusy;
            return true;
        }

        if (chainAttackProcController.IsSequenceCooldownActive(chainDef, out _))
        {
            reason = PartyCommandBlockReason.Cooldown;
            return true;
        }

        if (!CanSpendCommandPoints(chainDef.ClampedCommandPointCost))
        {
            reason = PartyCommandBlockReason.NotEnoughCommandPoints;
            return true;
        }

        if (!chainAttackProcController.CanStartManualSequence(chainDef))
        {
            reason = PartyCommandBlockReason.MissingTarget;
            return true;
        }

        return false;
    }

    bool TryGetHelperPartyCommandBlockReason(
        HelperChainAttackSequenceDef sequenceDef,
        out PartyCommandBlockReason reason)
    {
        reason = PartyCommandBlockReason.None;

        AllyHelperManager helperManager = playerContext != null ? playerContext.allyHelper : null;
        if (helperManager == null)
        {
            reason = PartyCommandBlockReason.HelperUnavailable;
            return true;
        }

        if (helperManager.IsHelperBusy)
        {
            reason = PartyCommandBlockReason.HelperBusy;
            return true;
        }

        if (!helperManager.CanStartManualCommand(requireNotBusy: false))
        {
            reason = PartyCommandBlockReason.HelperUnavailable;
            return true;
        }

        if (sequenceDef != null && !helperManager.HasChainAttackTarget(sequenceDef))
        {
            reason = PartyCommandBlockReason.MissingTarget;
            return true;
        }

        return false;
    }

    bool TryGetHelperCommandSlotBlockReason(
        int slotIndex,
        out PartyCommandBlockReason reason)
    {
        reason = PartyCommandBlockReason.None;

        AllyHelperManager helperManager = playerContext != null ? playerContext.allyHelper : null;
        if (helperManager == null)
        {
            reason = PartyCommandBlockReason.HelperUnavailable;
            return true;
        }

        if (helperManager.IsHelperBusy)
        {
            reason = PartyCommandBlockReason.HelperBusy;
            return true;
        }

        if (!helperManager.CanStartManualCommand(requireNotBusy: false))
        {
            reason = PartyCommandBlockReason.HelperUnavailable;
            return true;
        }

        CharacterSkillManager helperSkillManager = helperManager.HelperSkillManager;
        bool hasHelperManualSkill = helperSkillManager != null &&
                                    (helperSkillManager.HasConfiguredPlayerCommandSkill ||
                                     helperSkillManager.HasConfiguredCommandSlot(slotIndex));
        if (!hasHelperManualSkill)
        {
            reason = PartyCommandBlockReason.SkillUnavailable;
            return true;
        }

        bool canStartHelperManualSkill = helperSkillManager.HasConfiguredPlayerCommandSkill
            ? helperSkillManager.CanStartPlayerCommandSkill()
            : helperSkillManager.CanStartCastSlot(slotIndex);
        if (!canStartHelperManualSkill)
        {
            reason = PartyCommandBlockReason.SkillBlocked;
            return true;
        }

        return false;
    }

    bool TryGetAllySequencePartyCommandBlockReason(
        ChainAttackSequenceDef sequenceDef,
        out PartyCommandBlockReason reason)
    {
        reason = PartyCommandBlockReason.None;

        ChainAttackCoordinator coordinator = playerContext != null ? playerContext.chainAttackCoordinator : null;
        if (coordinator == null)
        {
            reason = PartyCommandBlockReason.SequenceUnavailable;
            return true;
        }

        if (coordinator.IsSequenceActive)
        {
            reason = PartyCommandBlockReason.SequenceBusy;
            return true;
        }

        if (!coordinator.CanStartSequence(sequenceDef))
        {
            reason = PartyCommandBlockReason.MissingTarget;
            return true;
        }

        return false;
    }

    bool TryGetAllyCommandSlotBlockReason(
        ChainActorRole actorRole,
        int slotIndex,
        out PartyCommandBlockReason reason)
    {
        reason = PartyCommandBlockReason.None;

        if (!TryResolveAllyCommandTarget(actorRole, out FieldAllyMember member, out CharacterSkillManager skillManager))
        {
            reason = PartyCommandBlockReason.AllyUnavailable;
            return true;
        }

        if (member.IsBusy || member.IsReserved)
        {
            reason = PartyCommandBlockReason.AllyBusy;
            return true;
        }

        bool hasManualSkill = skillManager.HasConfiguredPlayerCommandSkill ||
                              skillManager.HasConfiguredCommandSlot(slotIndex);
        if (!hasManualSkill)
        {
            reason = PartyCommandBlockReason.SkillUnavailable;
            return true;
        }

        bool canStartManualSkill = skillManager.HasConfiguredPlayerCommandSkill
            ? skillManager.CanStartPlayerCommandSkill()
            : skillManager.CanStartCastSlot(slotIndex);
        if (!canStartManualSkill)
        {
            reason = PartyCommandBlockReason.SkillBlocked;
            return true;
        }

        return false;
    }

    bool TryExecuteAllyCommandSlot(PartyCommandDefinition command)
    {
        if (command == null)
            return false;

        if (!TryResolveAllyCommandTarget(command.allyCommandActorRole, out _, out CharacterSkillManager skillManager))
            return false;

        SkillCastStartResult result = skillManager.HasConfiguredPlayerCommandSkill
            ? skillManager.TryStartPlayerCommandSkill()
            : skillManager.TryStartCastSlot(command.ClampedAllyCommandSlotIndex);
        return result.Started;
    }

    bool TryResolveAllyCommandTarget(
        ChainActorRole actorRole,
        out FieldAllyMember member,
        out CharacterSkillManager skillManager)
    {
        member = null;
        skillManager = null;

        FieldAllyManager fieldAllyManager = playerContext != null ? playerContext.fieldAllyManager : null;
        if (fieldAllyManager == null || actorRole == ChainActorRole.None)
            return false;

        if (!fieldAllyManager.TryGetMember(actorRole, out member) || member == null)
            return false;

        skillManager = member.SkillManager;
        return skillManager != null;
    }

    bool IsPartyCommandOwnerAvailable()
    {
        if (playerContext == null || playerContext.stateHub == null)
            return true;

        return playerContext.stateHub.IsAlive && !playerContext.stateHub.Isdown;
    }

    bool IsPartyCommandOnCooldown(int commandIndex, out float remainingSeconds)
    {
        remainingSeconds = 0f;

        if (!_partyCommandReadyAt.TryGetValue(commandIndex, out float readyAt))
            return false;

        remainingSeconds = readyAt - Time.time;
        if (remainingSeconds <= 0f)
        {
            _partyCommandReadyAt.Remove(commandIndex);
            remainingSeconds = 0f;
            return false;
        }

        return true;
    }

    void StampPartyCommandCooldown(int commandIndex, PartyCommandDefinition command)
    {
        float cooldown = command != null ? command.ClampedCooldownSeconds : 0f;
        if (cooldown <= 0f)
        {
            _partyCommandReadyAt.Remove(commandIndex);
            return;
        }

        _partyCommandReadyAt[commandIndex] = Time.time + cooldown;
    }

    public string GetSelectedPartyCommandUiLabel()
    {
        int index = NormalizeSelectedPartyCommandSelection();
        if (!TryGetConfiguredPartyCommand(index, out PartyCommandDefinition command))
            return "Party Command: none";

        string label = $"{command.ResolvedDisplayName} | {command.ClampedCommandPointCost:0.##} CP";

        if (TryGetPartyCommandBlockReason(index, command, out PartyCommandBlockReason blockReason))
            return $"{label} [{BuildPartyCommandBlockReasonLabel(index, blockReason)}]";

        return label;
    }

    string BuildPartyCommandBlockReasonLabel(int commandIndex, PartyCommandBlockReason reason)
    {
        return reason switch
        {
            PartyCommandBlockReason.None => "Ready",
            PartyCommandBlockReason.NotEnoughCommandPoints => "No CP",
            PartyCommandBlockReason.Cooldown => BuildPartyCommandCooldownLabel(commandIndex),
            PartyCommandBlockReason.OwnerUnavailable => "Owner Down",
            PartyCommandBlockReason.HelperUnavailable => "Helper Offline",
            PartyCommandBlockReason.HelperBusy => "Helper Busy",
            PartyCommandBlockReason.AllyUnavailable => "Ally Offline",
            PartyCommandBlockReason.AllyBusy => "Ally Busy",
            PartyCommandBlockReason.SkillUnavailable => "No Skill",
            PartyCommandBlockReason.SkillBlocked => "Skill Blocked",
            PartyCommandBlockReason.SequenceUnavailable => "Ally Offline",
            PartyCommandBlockReason.SequenceBusy => "Chain Busy",
            PartyCommandBlockReason.MissingTarget => "No Target",
            _ => "Invalid",
        };
    }

    string BuildChainAttackBlockReasonLabel(SkillChainDef chainDef, PartyCommandBlockReason reason)
    {
        if (reason == PartyCommandBlockReason.Cooldown &&
            chainAttackProcController != null &&
            chainAttackProcController.IsSequenceCooldownActive(chainDef, out float remainingSeconds))
        {
            return $"Cooldown {remainingSeconds:0.0}s";
        }

        return reason switch
        {
            PartyCommandBlockReason.None => "Ready",
            PartyCommandBlockReason.NotEnoughCommandPoints => "No CP",
            PartyCommandBlockReason.Cooldown => "Cooldown",
            PartyCommandBlockReason.OwnerUnavailable => "Owner Down",
            PartyCommandBlockReason.SequenceUnavailable => "Chain Offline",
            PartyCommandBlockReason.SequenceBusy => "Chain Busy",
            PartyCommandBlockReason.MissingTarget => "No Target",
            _ => "Invalid",
        };
    }

    static string ResolveChainAttackLabel(SkillChainDef chainDef)
    {
        if (chainDef == null)
            return "Chain Attack";

        if (!string.IsNullOrWhiteSpace(chainDef.displayName))
            return chainDef.displayName.Trim();

        return string.IsNullOrWhiteSpace(chainDef.name) ? "Chain Attack" : chainDef.name;
    }

    string BuildPartyCommandCooldownLabel(int commandIndex)
    {
        if (!IsPartyCommandOnCooldown(commandIndex, out float remainingSeconds))
            return "Ready";

        return $"Cooldown {remainingSeconds:0.0}s";
    }

    PartyCommandLabelData BuildPartyCommandLabelData()
    {
        int index = NormalizeSelectedPartyCommandSelection();
        if (!TryGetConfiguredPartyCommand(index, out PartyCommandDefinition command))
            return new PartyCommandLabelData("none", 0f, PartyCommandBlockReason.MissingConfig, 0f);

        TryGetPartyCommandBlockReason(index, command, out PartyCommandBlockReason blockReason);

        float cooldownReadyTime = 0f;
        if (blockReason == PartyCommandBlockReason.Cooldown && _partyCommandReadyAt.TryGetValue(index, out float readyAt))
            cooldownReadyTime = readyAt;

        return new PartyCommandLabelData(command.ResolvedDisplayName, command.ClampedCommandPointCost, blockReason, cooldownReadyTime);
    }

    void RaisePartyCommandLabel()
    {
        PartyCommandLabelChanged?.Invoke(BuildPartyCommandLabelData());
    }

    static int Mod(int value, int length)
    {
        if (length <= 0)
            return 0;

        int result = value % length;
        return result < 0 ? result + length : result;
    }
}

public enum PartyCommandExecutionKind
{
    None = 0,
    HelperSkill = 1,
    HelperChainAttack = 2,
    AllySequence = 3,
    HelperCommandSlot = 4,
    AllyCommandSlot = 5,
}

public enum PartyCommandBlockReason
{
    None = 0,
    MissingConfig = 1,
    NotEnoughCommandPoints = 2,
    Cooldown = 3,
    OwnerUnavailable = 4,
    HelperUnavailable = 5,
    HelperBusy = 6,
    SequenceUnavailable = 7,
    SequenceBusy = 8,
    MissingTarget = 9,
    AllyUnavailable = 10,
    AllyBusy = 11,
    SkillUnavailable = 12,
    SkillBlocked = 13,
}

public readonly struct PartyCommandLabelData
{
    public readonly string CommandName;
    public readonly float CommandPointCost;
    public readonly PartyCommandBlockReason BlockReason;
    public readonly float CooldownReadyTime;

    public PartyCommandLabelData(string commandName, float commandPointCost, PartyCommandBlockReason blockReason, float cooldownReadyTime)
    {
        CommandName = commandName;
        CommandPointCost = commandPointCost;
        BlockReason = blockReason;
        CooldownReadyTime = cooldownReadyTime;
    }
}

[Serializable]
public sealed class PartyCommandDefinition
{
    [Header("Identity")]
    public string commandId;
    public string displayName;
    [TextArea] public string description;
    public Sprite icon;

    [Header("Cost")]
    [Min(0f)] public float commandPointCost = 1f;
    [Min(0f)] public float cooldownSeconds;
    public bool requireOwnerAlive = true;

    [Header("Execution")]
    public PartyCommandExecutionKind executionKind = PartyCommandExecutionKind.HelperSkill;
    public SkillGemDefinition helperSkill;
    [Min(1)] public int helperSkillLevel = 1;
    public bool helperHideOnComplete = true;
    [Min(0)] public int helperCommandSlotIndex;
    public HelperChainAttackSequenceDef helperChainAttackSequence;
    public ChainStepContinueMode helperChainContinueMode = ChainStepContinueMode.OnStepComplete;
    [Range(0f, 1f)] public float helperChainContinueNormalizedTime = 1f;
    public ChainActorRole allyCommandActorRole = ChainActorRole.PartySlot1;
    [Min(0)] public int allyCommandSlotIndex;
    public ChainAttackSequenceDef allySequence;

    public float ClampedCommandPointCost => Mathf.Max(0f, commandPointCost);
    public float ClampedCooldownSeconds => Mathf.Max(0f, cooldownSeconds);
    public int ClampedHelperSkillLevel => Mathf.Max(1, helperSkillLevel);
    public int ClampedHelperCommandSlotIndex => Mathf.Max(0, helperCommandSlotIndex);
    public float ClampedHelperChainContinueNormalizedTime => Mathf.Clamp(helperChainContinueNormalizedTime, 0f, 0.999f);
    public int ClampedAllyCommandSlotIndex => Mathf.Max(0, allyCommandSlotIndex);

    public bool HasExecutionConfigured
    {
        get
        {
            switch (executionKind)
            {
                case PartyCommandExecutionKind.HelperSkill:
                    return helperSkill != null;

                case PartyCommandExecutionKind.HelperCommandSlot:
                    return helperCommandSlotIndex >= 0;

                case PartyCommandExecutionKind.HelperChainAttack:
                    return helperSkill != null && helperChainAttackSequence != null;

                case PartyCommandExecutionKind.AllyCommandSlot:
                    return allyCommandActorRole != ChainActorRole.None && allyCommandSlotIndex >= 0;

                case PartyCommandExecutionKind.AllySequence:
                    return allySequence != null && allySequence.HasAnySteps;

                default:
                    return false;
            }
        }
    }

    public string ResolvedDisplayName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(displayName))
                return displayName.Trim();

            switch (executionKind)
            {
                case PartyCommandExecutionKind.HelperSkill:
                case PartyCommandExecutionKind.HelperChainAttack:
                    return ResolveSkillName(helperSkill, "Helper Command");

                case PartyCommandExecutionKind.HelperCommandSlot:
                    return $"Helper Command {ClampedHelperCommandSlotIndex + 1}";

                case PartyCommandExecutionKind.AllyCommandSlot:
                    return $"{allyCommandActorRole} Command {ClampedAllyCommandSlotIndex + 1}";

                case PartyCommandExecutionKind.AllySequence:
                    return allySequence != null
                        ? allySequence.RuntimeId
                        : "Ally Command";

                default:
                    return "Party Command";
            }
        }
    }

    static string ResolveSkillName(SkillGemDefinition skill, string fallback)
    {
        if (skill == null)
            return fallback;

        if (!string.IsNullOrWhiteSpace(skill.displayName))
            return skill.displayName.Trim();

        return string.IsNullOrWhiteSpace(skill.name) ? fallback : skill.name;
    }
}
