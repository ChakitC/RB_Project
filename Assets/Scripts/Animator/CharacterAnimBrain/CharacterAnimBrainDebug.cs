using System;
using System.Collections.Generic;
using System.Text;
using Animancer;
using UnityEngine;

[DefaultExecutionOrder(1000)]
[DisallowMultipleComponent]
public sealed class CharacterAnimBrainDebug : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private CharacterAnimBrain brain;
    [SerializeField] private CharacteContext ctx;
    [SerializeField] private StateHub stateHub;
    [SerializeField] private StatusEffectController statusEffectController;
    [SerializeField] private Animator animator;

    [Header("Options")]
    [SerializeField] private bool debugInInspector = true;
    [SerializeField] private bool logTimelineToConsole;
    [SerializeField] private bool showOnScreen;
    [SerializeField, Min(4)] private int timelineCapacity = 24;
    [SerializeField, Min(0f)] private float snapshotRefreshInterval;
    [SerializeField] private Vector2 screenPos = new Vector2(10f, 140f);
    [SerializeField] private Vector2 screenSize = new Vector2(720f, 480f);

    [Header("Snapshot")]
    [SerializeField, TextArea(10, 40)] private string dbgSummary;
    [SerializeField, TextArea(5, 20)] private string dbgProfileValidation;
    [SerializeField, TextArea(5, 20)] private string dbgGateReasons;
    [SerializeField, TextArea(10, 30)] private string dbgTimeline;
    [SerializeField] private int dbgTimelineCount;

    private readonly Queue<string> timelineEntries = new();
    private readonly List<string> buffer = new();
    private readonly StringBuilder summaryBuilder = new(2048);
    private readonly StringBuilder profileBuilder = new(1024);
    private readonly StringBuilder gateBuilder = new(1024);

    private CharacterAnimBrain subscribedBrain;
    private StateHub subscribedStateHub;
    private StatusEffectController subscribedStatusEffects;
    private float nextSnapshotRefreshTime = -1f;

    private string lastInitSignature;
    private string lastLocomotionState;
    private string lastActionState;
    private string lastLocomotionClip;
    private string lastActionClip;
    private string lastPendingSignature;
    private string lastStatusSignature;
    private string lastRequestSignature;
    private string lastRootMotionSignature;
    private string lastFlagsSignature;
    private string lastMeleeSignature;

    void Awake()
    {
        ResolveRefs();
    }

    void OnEnable()
    {
        ResolveRefs();
        SyncSubscriptions();
        ClearObservedState();

        if (IsAnyOutputEnabled())
        {
            RecordEvent("Anim debug attached");
            PollStateChanges();
            RefreshSnapshotStrings(force: true);
        }
    }

    void OnDisable()
    {
        UnsubscribeFromRefs();
    }

    void LateUpdate()
    {
        ResolveRefs();
        SyncSubscriptions();

        if (!IsAnyOutputEnabled())
            return;

        PollStateChanges();
        RefreshSnapshotStrings(force: false);
    }

    void OnGUI()
    {
        if (!showOnScreen)
            return;

        string overlayText = BuildOverlayText();
        if (string.IsNullOrWhiteSpace(overlayText))
            return;

        GUI.TextArea(new Rect(screenPos.x, screenPos.y, screenSize.x, screenSize.y), overlayText);
    }

    private void ResolveRefs()
    {
        if (!ctx)
            TryGetComponent(out ctx);

        if (!brain)
            brain = ctx != null && ctx.AnimBrain != null ? ctx.AnimBrain : GetComponent<CharacterAnimBrain>();

        if (!stateHub)
            stateHub = ctx != null && ctx.stateHub != null ? ctx.stateHub : GetComponent<StateHub>();

        if (!statusEffectController)
            statusEffectController = GetComponent<StatusEffectController>();

        Animator resolvedAnimator = brain != null ? brain.DebugBoundAnimator : null;
        if (resolvedAnimator != null)
            animator = resolvedAnimator;
        else if (!animator)
            animator = GetComponent<Animator>();
    }

    private bool IsAnyOutputEnabled()
    {
        return debugInInspector || showOnScreen || logTimelineToConsole;
    }

    private void SyncSubscriptions()
    {
        if (brain != subscribedBrain)
        {
            if (subscribedBrain != null)
                UnsubscribeBrain(subscribedBrain);

            subscribedBrain = brain;

            if (subscribedBrain != null)
                SubscribeBrain(subscribedBrain);
        }

        if (stateHub != subscribedStateHub)
        {
            if (subscribedStateHub != null)
                UnsubscribeStateHub(subscribedStateHub);

            subscribedStateHub = stateHub;

            if (subscribedStateHub != null)
                SubscribeStateHub(subscribedStateHub);
        }

        if (statusEffectController != subscribedStatusEffects)
        {
            if (subscribedStatusEffects != null)
                subscribedStatusEffects.EffectsChanged -= OnEffectsChanged;

            subscribedStatusEffects = statusEffectController;

            if (subscribedStatusEffects != null)
                subscribedStatusEffects.EffectsChanged += OnEffectsChanged;
        }
    }

    private void UnsubscribeFromRefs()
    {
        if (subscribedBrain != null)
            UnsubscribeBrain(subscribedBrain);

        if (subscribedStateHub != null)
            UnsubscribeStateHub(subscribedStateHub);

        if (subscribedStatusEffects != null)
            subscribedStatusEffects.EffectsChanged -= OnEffectsChanged;

        subscribedBrain = null;
        subscribedStateHub = null;
        subscribedStatusEffects = null;
    }

    private void SubscribeBrain(CharacterAnimBrain target)
    {
        target.MeleeHitStart += OnMeleeHitStart;
        target.MeleeHitEnd += OnMeleeHitEnd;
        target.MeleeComboEnded += OnMeleeComboEnded;
        target.SkillCastMomentReached += OnSkillCastMomentReached;
        target.SkillCastInterrupted += OnSkillCastInterrupted;
        target.SkillCompleted += OnSkillCompleted;
        target.ChainCastMomentReached += OnChainCastMomentReached;
        target.ChainAdvanceMomentReached += OnChainAdvanceMomentReached;
        target.ChainPlaybackInterrupted += OnChainPlaybackInterrupted;
        target.ChainPlaybackCompleted += OnChainPlaybackCompleted;
    }

    private void UnsubscribeBrain(CharacterAnimBrain target)
    {
        target.MeleeHitStart -= OnMeleeHitStart;
        target.MeleeHitEnd -= OnMeleeHitEnd;
        target.MeleeComboEnded -= OnMeleeComboEnded;
        target.SkillCastMomentReached -= OnSkillCastMomentReached;
        target.SkillCastInterrupted -= OnSkillCastInterrupted;
        target.SkillCompleted -= OnSkillCompleted;
        target.ChainCastMomentReached -= OnChainCastMomentReached;
        target.ChainAdvanceMomentReached -= OnChainAdvanceMomentReached;
        target.ChainPlaybackInterrupted -= OnChainPlaybackInterrupted;
        target.ChainPlaybackCompleted -= OnChainPlaybackCompleted;
    }

    private void SubscribeStateHub(StateHub target)
    {
        target.ShotFired += OnShotFired;
        target.FireHeldChanged += OnFireHeldChanged;
        target.ReloadStarted += OnReloadStarted;
        target.DashStarted += OnDashStarted;
    }

    private void UnsubscribeStateHub(StateHub target)
    {
        target.ShotFired -= OnShotFired;
        target.FireHeldChanged -= OnFireHeldChanged;
        target.ReloadStarted -= OnReloadStarted;
        target.DashStarted -= OnDashStarted;
    }

    private void ClearObservedState()
    {
        lastInitSignature = null;
        lastLocomotionState = null;
        lastActionState = null;
        lastLocomotionClip = null;
        lastActionClip = null;
        lastPendingSignature = null;
        lastStatusSignature = null;
        lastRequestSignature = null;
        lastRootMotionSignature = null;
        lastFlagsSignature = null;
        lastMeleeSignature = null;
        nextSnapshotRefreshTime = -1f;
    }

    private void PollStateChanges()
    {
        string initSignature = brain != null
            ? brain.DebugIsInitialized ? "Initialized" : $"InitFailed:{brain.DebugInitializationError}"
            : "MissingBrain";
        ObserveValue("Init", initSignature, ref lastInitSignature);

        if (brain == null)
            return;

        ObserveValue("Locomotion", brain.DebugLocomotionStateName, ref lastLocomotionState);
        ObserveValue("Action", brain.DebugActionStateName, ref lastActionState);
        ObserveValue("Locomotion Clip", brain.DebugLocomotionLayer.AssetName, ref lastLocomotionClip);
        ObserveValue("Action Clip", brain.DebugActionLayer.AssetName, ref lastActionClip);

        string pendingSignature = $"{brain.DebugPendingActionName} | pendingPulse={brain.DebugPendingPulse}";
        ObserveValue("Pending", pendingSignature, ref lastPendingSignature);

        string statusSignature = $"{brain.DebugCurrentStatusLocomotionKindName} <= {brain.DebugResolvedStatusLocomotionKindName}";
        ObserveValue("Status", statusSignature, ref lastStatusSignature);

        string requestSignature =
            $"skill={brain.DebugActiveSkillRequestId}/{brain.DebugSkillReleaseRequested}/{brain.DebugSkillReleased}, " +
            $"utility={brain.DebugActiveUtilityRequestId}/{brain.DebugUtilityReleaseRequested}/{brain.DebugUtilityReleased}, " +
            $"chain={brain.DebugActiveChainRequestId}/{brain.DebugActiveChainKindName}/{brain.DebugChainReleaseRequested}/{brain.DebugChainReleased}/{brain.DebugChainAdvanceRequested}/{brain.DebugChainAdvanceReleased}";
        ObserveValue("Requests", requestSignature, ref lastRequestSignature);

        string rootMotionSignature =
            $"brain={brain.RootMotionActive}, animator={(animator != null ? animator.applyRootMotion : false)}, canExitChain={brain.DebugChainStateCanExit}";
        ObserveValue("RootMotion", rootMotionSignature, ref lastRootMotionSignature);

        string flagsSignature =
            $"holding={brain.IsHoldingFire}, downed={brain.IsDowned}, skillActive={brain.IsSkillActive}, utilityActive={brain.IsUtilityActive}";
        ObserveValue("Flags", flagsSignature, ref lastFlagsSignature);

        CharacterAnimBrain.DebugMeleeSnapshot melee = brain.DebugMelee;
        string meleeSignature =
            $"active={melee.IsActive}, combo={melee.ComboName}, step={melee.StepIndex}, buffer={melee.BufferedPresses}, clip={melee.ClipName}, chainOpen={melee.ChainWindowOpen}";
        ObserveValue("Melee", meleeSignature, ref lastMeleeSignature);
    }

    private void ObserveValue(string label, string currentValue, ref string previousValue)
    {
        currentValue ??= "<null>";

        if (string.Equals(previousValue, currentValue, StringComparison.Ordinal))
            return;

        if (string.IsNullOrEmpty(previousValue))
            RecordEvent($"{label} -> {currentValue}");
        else
            RecordEvent($"{label}: {previousValue} -> {currentValue}");

        previousValue = currentValue;
    }

    private void RefreshSnapshotStrings(bool force)
    {
        if (!debugInInspector && !showOnScreen)
            return;

        float now = Time.unscaledTime;
        if (!force && snapshotRefreshInterval > 0f && now < nextSnapshotRefreshTime)
            return;

        nextSnapshotRefreshTime = snapshotRefreshInterval > 0f
            ? now + snapshotRefreshInterval
            : now;

        dbgSummary = BuildSummary();
        dbgProfileValidation = BuildProfileValidationReport();
        dbgGateReasons = BuildGateReasonReport();
        dbgTimeline = string.Join("\n", timelineEntries);
        dbgTimelineCount = timelineEntries.Count;
    }

    private string BuildSummary()
    {
        summaryBuilder.Clear();

        if (brain == null)
        {
            summaryBuilder.Append("CharacterAnimBrain missing.");
            return summaryBuilder.ToString();
        }

        CharacterAnimBrain.DebugLayerSnapshot locomotionLayer = brain.DebugLocomotionLayer;
        CharacterAnimBrain.DebugLayerSnapshot actionLayer = brain.DebugActionLayer;
        CharacterAnimBrain.DebugMeleeSnapshot melee = brain.DebugMelee;

        summaryBuilder.Append("Init: ").Append(brain.DebugIsInitialized ? "Ready" : "Failed");
        if (!brain.DebugIsInitialized)
            summaryBuilder.Append(" | ").Append(brain.DebugInitializationError);
        summaryBuilder.AppendLine();

        summaryBuilder.Append("Animator: ").Append(GetObjectName(brain.DebugBoundAnimator))
            .Append(" | applyRootMotion=").Append(animator != null && animator.applyRootMotion)
            .AppendLine();

        summaryBuilder.Append("Profile: ").Append(GetObjectName(brain.DebugProfile)).AppendLine();

        summaryBuilder.Append("Input: MoveDirLocal=").Append(FormatVector2(brain.MoveDirLocal))
            .Append(" | MoveSpeed01=").Append(brain.MoveSpeed01.ToString("0.00"))
            .Append(" | HoldingFire=").Append(brain.IsHoldingFire)
            .Append(" | Downed=").Append(brain.IsDowned)
            .Append(" | RootMotion=").Append(brain.RootMotionActive)
            .AppendLine();

        summaryBuilder.Append("Locomotion: ").Append(brain.DebugLocomotionStateName)
            .Append(" | Clip=").Append(locomotionLayer.AssetName)
            .Append(" | N=").Append(locomotionLayer.NormalizedTime.ToString("0.00"))
            .Append(" | Speed=").Append(locomotionLayer.Speed.ToString("0.00"))
            .Append(" | Weight=").Append(locomotionLayer.Weight.ToString("0.00"))
            .AppendLine();

        summaryBuilder.Append("Action: ").Append(brain.DebugActionStateName)
            .Append(" | Clip=").Append(actionLayer.AssetName)
            .Append(" | N=").Append(actionLayer.NormalizedTime.ToString("0.00"))
            .Append(" | Speed=").Append(actionLayer.Speed.ToString("0.00"))
            .Append(" | Weight=").Append(actionLayer.Weight.ToString("0.00"))
            .AppendLine();

        summaryBuilder.Append("Pending: ").Append(brain.DebugPendingActionName)
            .Append(" | Pulse=").Append(brain.DebugPendingPulse)
            .AppendLine();

        summaryBuilder.Append("Status: Current=").Append(brain.DebugCurrentStatusLocomotionKindName)
            .Append(" | Resolved=").Append(brain.DebugResolvedStatusLocomotionKindName)
            .AppendLine();

        summaryBuilder.Append("Requests: ").Append(brain.DebugDescribeRequests()).AppendLine();

        summaryBuilder.Append("Melee: ").Append(brain.DebugDescribeMelee()).AppendLine();

        summaryBuilder.Append("Effects: ").Append(BuildEffectsSummary()).AppendLine();

        if (stateHub != null)
        {
            summaryBuilder.Append("StateHub: Life=").Append(stateHub.LifeSM != null ? stateHub.LifeSM.CurrentId.ToString() : "<null>")
                .Append(" | Move=").Append(stateHub.MoveSM != null ? stateHub.MoveSM.CurrentId.ToString() : "<null>")
                .Append(" | Weapon=").Append(stateHub.WeaponSM != null ? stateHub.WeaponSM.CurrentId.ToString() : "<null>")
                .Append(" | UI=").Append(stateHub.UISM != null ? stateHub.UISM.CurrentId.ToString() : "<null>")
                .AppendLine();
        }

        if (melee.IsActive)
        {
            summaryBuilder.Append("Melee Windows: Hit=[")
                .Append(melee.HitWindowStart.ToString("0.00")).Append('-').Append(melee.HitWindowEnd.ToString("0.00"))
                .Append("] | Chain=[")
                .Append(melee.ChainWindowStart.ToString("0.00")).Append('-').Append(melee.ChainWindowEnd.ToString("0.00"))
                .Append("] | Open=").Append(melee.ChainWindowOpen)
                .Append(" | Pressed=").Append(melee.PressedInWindow)
                .Append(" | Expired=").Append(melee.WindowExpired)
                .AppendLine();
        }

        return summaryBuilder.ToString();
    }

    private string BuildProfileValidationReport()
    {
        profileBuilder.Clear();

        CharacterAnimProfileSO profile = brain != null ? brain.DebugProfile : ctx != null && ctx.baseStats != null ? ctx.baseStats.animProfile : null;
        if (profile == null)
        {
            profileBuilder.Append("No CharacterAnimProfileSO resolved.");
            return profileBuilder.ToString();
        }

        AddProfileIssue(profileBuilder, profile.upperBodyMask == null, "Upper body mask missing.");
        AddProfileIssue(profileBuilder, profile.locomotionMixer == null || !profile.locomotionMixer.IsValid, "Locomotion mixer missing or invalid.");
        AddProfileIssue(profileBuilder, profile.crawlMixer == null || !profile.crawlMixer.IsValid, "Crawl mixer missing or invalid.");
        AddProfileIssue(profileBuilder, profile.dead == null || !profile.dead.IsValid, "Dead clip missing or invalid.");
        AddProfileIssue(profileBuilder, profile.shootPulse == null || !profile.shootPulse.IsValid, "Shoot pulse clip missing or invalid.");
        AddProfileIssue(profileBuilder, profile.reload == null || !profile.reload.IsValid, "Reload clip missing or invalid.");
        AddProfileIssue(profileBuilder, profile.dashF == null || !profile.dashF.IsValid, "Dash forward clip missing or invalid.");
        AddProfileIssue(profileBuilder, profile.dashB == null || !profile.dashB.IsValid, "Dash backward clip missing or invalid.");
        AddProfileIssue(profileBuilder, profile.dashL == null || !profile.dashL.IsValid, "Dash left clip missing or invalid.");
        AddProfileIssue(profileBuilder, profile.dashR == null || !profile.dashR.IsValid, "Dash right clip missing or invalid.");
        AddProfileIssue(profileBuilder, profile.miniStune == null || !profile.miniStune.IsValid, "Mini stun clip missing or invalid.");
        AddProfileIssue(profileBuilder, profile.stune == null || !profile.stune.IsValid, "Stun clip missing or invalid.");
        AddProfileIssue(profileBuilder, profile.root == null || !profile.root.IsValid, "Root clip missing or invalid.");
        AddProfileIssue(profileBuilder, profile.freez == null || !profile.freez.IsValid, "Freeze clip missing or invalid.");
        AddProfileIssue(profileBuilder, profile.utilityWarpOutClip == null || !profile.utilityWarpOutClip.IsValid, "Utility warp-out clip missing or invalid.");
        AddProfileIssue(profileBuilder, profile.utilityWarpInClip == null || !profile.utilityWarpInClip.IsValid, "Utility warp-in clip missing or invalid.");

        if (profile.meleeCombo == null)
        {
            profileBuilder.AppendLine("Default melee combo missing.");
        }
        else if (!profile.meleeCombo.IsValid(out string comboReason))
        {
            profileBuilder.AppendLine($"Default melee combo invalid: {comboReason}");
        }

        if (profile.lightCombo != null && !profile.lightCombo.IsValid(out string lightReason))
            profileBuilder.AppendLine($"Light combo invalid: {lightReason}");

        if (profile.heavyCombo != null && !profile.heavyCombo.IsValid(out string heavyReason))
            profileBuilder.AppendLine($"Heavy combo invalid: {heavyReason}");

        if (profile.skillClip == null || !profile.skillClip.IsValid)
            profileBuilder.AppendLine("Legacy skill clip missing. Per-skill clips can still work.");

        if (profileBuilder.Length == 0)
            profileBuilder.Append("No obvious profile issues.");

        return profileBuilder.ToString().TrimEnd();
    }

    private string BuildGateReasonReport()
    {
        gateBuilder.Clear();
        gateBuilder.Append("CanShoot: ").Append(BuildShootGateReason()).AppendLine();
        gateBuilder.Append("CanMove: ").Append(BuildMoveGateReason()).AppendLine();
        gateBuilder.Append("CanUseSkill: ").Append(BuildSkillGateReason()).AppendLine();
        gateBuilder.Append("CanStartMelee: ").Append(BuildMeleeGateReason());
        return gateBuilder.ToString();
    }

    private string BuildShootGateReason()
    {
        if (stateHub == null)
            return "StateHub missing.";

        if (stateHub.CanShoot())
            return "Allowed";

        buffer.Clear();

        if (!stateHub.IsAlive)
            buffer.Add("life!=Alive");
        if (stateHub.Isdown)
            buffer.Add("downed");
        if (brain != null && brain.IsSkillActive)
            buffer.Add("skill active");
        if (stateHub.MoveSM != null && stateHub.MoveSM.CurrentId == MoveStateId.Dash)
            buffer.Add("move=dash");
        if (stateHub.UISM != null && stateHub.UISM.CurrentId == UIStateId.Inventory)
            buffer.Add("ui=inventory");
        if (stateHub.UISM != null && stateHub.UISM.CurrentId == UIStateId.Pause)
            buffer.Add("ui=pause");
        if (stateHub.WeaponSM != null && stateHub.WeaponSM.CurrentId == WeaponStateId.Reloading)
            buffer.Add("weapon=reloading");
        if (stateHub.WeaponSM != null && stateHub.WeaponSM.CurrentId == WeaponStateId.Melee)
            buffer.Add("weapon=melee");
        if (stateHub.WeaponSM != null && stateHub.WeaponSM.CurrentId == WeaponStateId.NoBullet)
            buffer.Add("weapon=no bullet");

        AppendStatusBlockers(buffer, ControlBlockFlags.Shoot, includeStun: true);
        return buffer.Count > 0 ? string.Join(", ", buffer) : "None";
    }

    private string BuildMoveGateReason()
    {
        if (stateHub == null)
            return "StateHub missing.";

        if (stateHub.CanMove())
            return "Allowed";

        buffer.Clear();

        if (!stateHub.IsAlive && !stateHub.Isdown)
            buffer.Add("dead");
        if (stateHub.UISM != null && stateHub.UISM.CurrentId == UIStateId.Inventory)
            buffer.Add("ui=inventory");
        if (stateHub.UISM != null && stateHub.UISM.CurrentId == UIStateId.Pause)
            buffer.Add("ui=pause");
        if (stateHub.WeaponSM != null && stateHub.WeaponSM.CurrentId == WeaponStateId.Melee)
            buffer.Add("weapon=melee");
        if (stateHub.MoveSM != null && stateHub.MoveSM.CurrentId == MoveStateId.Dash)
            buffer.Add("move=dash");
        if (stateHub.MoveSM != null && stateHub.MoveSM.CurrentId == MoveStateId.Stunned)
            buffer.Add("move=stunned");

        AppendStatusBlockers(buffer, ControlBlockFlags.Move, includeStun: true);
        return buffer.Count > 0 ? string.Join(", ", buffer) : "None";
    }

    private string BuildSkillGateReason()
    {
        if (stateHub == null)
            return "StateHub missing.";

        if (stateHub.CanUseSkill())
            return "Allowed";

        buffer.Clear();

        if (!stateHub.IsAlive)
            buffer.Add("life!=Alive");
        if (stateHub.Isdown)
            buffer.Add("downed");
        if (stateHub.UISM != null && stateHub.UISM.CurrentId == UIStateId.Inventory)
            buffer.Add("ui=inventory");
        if (stateHub.UISM != null && stateHub.UISM.CurrentId == UIStateId.Pause)
            buffer.Add("ui=pause");
        if (stateHub.MoveSM != null && stateHub.MoveSM.CurrentId == MoveStateId.Dash)
            buffer.Add("move=dash");
        if (stateHub.WeaponSM != null && stateHub.WeaponSM.CurrentId == WeaponStateId.Melee)
            buffer.Add("weapon=melee");

        AppendStatusBlockers(buffer, ControlBlockFlags.Skill, includeStun: true);
        return JoinReasons(buffer);
    }

    private string BuildMeleeGateReason()
    {
        if (stateHub == null)
            return "StateHub missing.";

        if (stateHub.CanStartMelee() && HasAnyValidMeleeCombo())
            return "Allowed";

        buffer.Clear();

        if (!stateHub.IsAlive)
            buffer.Add("life!=Alive");
        if (stateHub.Isdown)
            buffer.Add("downed");
        if (stateHub.UISM != null && stateHub.UISM.CurrentId == UIStateId.Inventory)
            buffer.Add("ui=inventory");
        if (stateHub.UISM != null && stateHub.UISM.CurrentId == UIStateId.Pause)
            buffer.Add("ui=pause");
        if (stateHub.MoveSM != null && stateHub.MoveSM.CurrentId == MoveStateId.Dash)
            buffer.Add("move=dash");
        if (stateHub.MoveSM != null && stateHub.MoveSM.CurrentId == MoveStateId.Stunned)
            buffer.Add("move=stunned");
        if (!HasAnyValidMeleeCombo())
            buffer.Add("no valid melee combo");

        AppendStatusBlockers(buffer, ControlBlockFlags.None, includeStun: true);
        return JoinReasons(buffer);
    }

    private void AppendStatusBlockers(List<string> target, ControlBlockFlags requiredFlags, bool includeStun)
    {
        if (statusEffectController == null || statusEffectController.ActiveEffects == null)
            return;

        for (int i = 0; i < statusEffectController.ActiveEffects.Count; i++)
        {
            StatusEffectInstance instance = statusEffectController.ActiveEffects[i];
            StatusEffectDef definition = instance != null ? instance.Definition : null;
            if (definition == null || instance.CurrentStacks <= 0)
                continue;

            if (includeStun && definition.pushStunnedState)
            {
                target.Add($"status(stun)={definition.name}");
                continue;
            }

            if (requiredFlags == ControlBlockFlags.None)
                continue;

            if ((definition.controlBlocks & requiredFlags) != 0)
                target.Add($"status({requiredFlags})={definition.name}");
        }
    }

    private bool HasAnyValidMeleeCombo()
    {
        CharacterAnimProfileSO profile = brain != null ? brain.DebugProfile : ctx != null && ctx.baseStats != null ? ctx.baseStats.animProfile : null;
        if (profile == null)
            return false;

        if (profile.meleeCombo != null && profile.meleeCombo.IsValid(out _))
            return true;
        if (profile.lightCombo != null && profile.lightCombo.IsValid(out _))
            return true;
        if (profile.heavyCombo != null && profile.heavyCombo.IsValid(out _))
            return true;

        return false;
    }

    private string BuildEffectsSummary()
    {
        if (statusEffectController == null || statusEffectController.ActiveEffects == null || statusEffectController.ActiveEffects.Count == 0)
            return "None";

        buffer.Clear();

        for (int i = 0; i < statusEffectController.ActiveEffects.Count; i++)
        {
            StatusEffectInstance instance = statusEffectController.ActiveEffects[i];
            StatusEffectDef definition = instance != null ? instance.Definition : null;
            if (definition == null || instance.CurrentStacks <= 0)
                continue;

            buffer.Add($"{definition.name}(x{instance.CurrentStacks}, block={definition.controlBlocks}, stun={definition.pushStunnedState})");
        }

        return JoinReasons(buffer);
    }

    private void RecordEvent(string message)
    {
        string entry = $"[{Time.frameCount}] {message}";

        while (timelineEntries.Count >= Mathf.Max(4, timelineCapacity))
            timelineEntries.Dequeue();

        timelineEntries.Enqueue(entry);
        dbgTimelineCount = timelineEntries.Count;

        if (logTimelineToConsole)
            Debug.Log($"[CharacterAnimBrainDebug] {entry}", this);
    }

    private void AddProfileIssue(StringBuilder builder, bool condition, string message)
    {
        if (!condition)
            return;

        builder.AppendLine(message);
    }

    private string JoinReasons(List<string> reasons)
    {
        if (reasons == null || reasons.Count == 0)
            return "Blocked by unknown rule.";

        return string.Join(", ", reasons);
    }

    private static string GetObjectName(UnityEngine.Object obj)
    {
        return obj != null ? obj.name : "<none>";
    }

    private static string FormatVector2(Vector2 value)
    {
        return $"({value.x:0.00}, {value.y:0.00})";
    }

    private string BuildOverlayText()
    {
        if (string.IsNullOrWhiteSpace(dbgSummary))
            RefreshSnapshotStrings(force: true);

        if (string.IsNullOrWhiteSpace(dbgSummary))
            return string.Empty;

        return
            dbgSummary +
            "\nProfile Validation:\n" + dbgProfileValidation +
            "\n\nGate Reasons:\n" + dbgGateReasons +
            "\n\nTimeline:\n" + dbgTimeline;
    }

    private void OnShotFired() => RecordEvent("StateHub.ShotFired");
    private void OnFireHeldChanged(bool held) => RecordEvent($"StateHub.FireHeldChanged={held}");
    private void OnReloadStarted(float reloadTime) => RecordEvent($"StateHub.ReloadStarted duration={reloadTime:0.00}");
    private void OnDashStarted(float dashDuration, Vector3 dashDirWorld) => RecordEvent($"StateHub.DashStarted duration={dashDuration:0.00} dir={dashDirWorld}");
    private void OnMeleeHitStart() => RecordEvent("Brain.MeleeHitStart");
    private void OnMeleeHitEnd() => RecordEvent("Brain.MeleeHitEnd");
    private void OnMeleeComboEnded() => RecordEvent("Brain.MeleeComboEnded");
    private void OnSkillCastMomentReached(int requestId) => RecordEvent($"Brain.SkillCastMomentReached request={requestId}");
    private void OnSkillCastInterrupted(int requestId) => RecordEvent($"Brain.SkillCastInterrupted request={requestId}");
    private void OnSkillCompleted() => RecordEvent("Brain.SkillCompleted");
    private void OnChainCastMomentReached(int requestId) => RecordEvent($"Brain.ChainCastMomentReached request={requestId}");
    private void OnChainAdvanceMomentReached(int requestId) => RecordEvent($"Brain.ChainAdvanceMomentReached request={requestId}");
    private void OnChainPlaybackInterrupted(int requestId) => RecordEvent($"Brain.ChainPlaybackInterrupted request={requestId}");
    private void OnChainPlaybackCompleted(int requestId) => RecordEvent($"Brain.ChainPlaybackCompleted request={requestId}");
    private void OnEffectsChanged() => RecordEvent($"StatusEffects.EffectsChanged -> {BuildEffectsSummary()}");
}
