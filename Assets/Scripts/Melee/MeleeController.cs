using UnityEngine;

[DefaultExecutionOrder(-109)]
[DisallowMultipleComponent]
public sealed class MeleeController : MonoBehaviour
{
    [SerializeField] private CharacteContext ctx;
    // Retain prefab bindings; the context is authoritative at runtime.
    [SerializeField, HideInInspector] private StateHub stateHub;
    [SerializeField, HideInInspector] private CharacterAnimBrain brain;
    [SerializeField, HideInInspector] private WeaponSystem weaponSystem;
    [SerializeField, HideInInspector] private StatusEffectController statusEffectController;
    [SerializeField, HideInInspector] private CombatEventBus combatEventBus;
    [SerializeField] private LayerMask targetMask = ~0;
    public LayerMask TargetMask => targetMask;
    readonly System.Collections.Generic.Dictionary<PrefabHitboxSkillPayloadDef, SkillHitboxSequenceRuntime> _hitboxRuntimes = new();
    readonly MeleeComboSession _session = new();
    int _requestId;
    bool _changingStep;
    SkillHitboxSequenceRuntime _hitboxRuntime;
    public MeleeType CurrentMeleeType { get; private set; }
    public bool IsComboActive => _session.IsActive;

    void Awake() => ResolveRefs();
    void OnEnable()
    {
        ResolveRefs();
        if (brain == null) return;
        brain.MeleeChainWindowOpened += OnChainWindowOpened;
        brain.MeleeChainWindowClosed += OnChainWindowClosed;
        brain.PlaybackEvent += OnPlaybackEvent;
    }
    void OnDisable()
    {
        InterruptMelee();
        if (brain == null) return;
        brain.MeleeChainWindowOpened -= OnChainWindowOpened;
        brain.MeleeChainWindowClosed -= OnChainWindowClosed;
        brain.PlaybackEvent -= OnPlaybackEvent;
    }
    public void PressMelee(MeleeType meleeType)
    {
        ResolveRefs();
        if (_session.IsActive)
        {
            if (meleeType == CurrentMeleeType && _session.QueuePress() == MeleeSessionAction.Advance)
                PlayCurrentStep();
            return;
        }
        TryStartMelee(meleeType);
    }
    public bool TryStartMelee(MeleeType meleeType)
    {
        ResolveRefs();
        if (_session.IsActive || !isActiveAndEnabled || ctx == null || stateHub == null || brain == null ||
            ctx.SkillManager == null || !stateHub.CanStartMelee()) return false;
        var profile = ctx.baseStats != null ? ctx.baseStats.animProfile : null;
        var combo = profile != null ? (meleeType == MeleeType.Light ? profile.lightCombo : profile.heavyCombo) : null;
        if (combo == null && profile != null) combo = profile.meleeCombo;
        if (combo == null || !combo.IsValid(out _)) return false;
        if (weaponSystem != null && weaponSystem.IsReloading && profile != null && !profile.meleeCanInterruptReload)
            return false;
        CurrentMeleeType = meleeType;
        _session.Start(combo);
        if (!PlayCurrentStep()) return false;
        weaponSystem?.CancelReload();
        weaponSystem?.SetFiring(false);
        stateHub.SetFireHeld(false);
        stateHub.WeaponSM.TryChange(WeaponStateId.Melee);
        stateHub.ReportMeleeStarted(meleeType);
        return true;
    }
    internal bool PlayCurrentStep()
    {
        if (!_session.IsActive || _changingStep) return false;
        _changingStep = true;
        try
        {
            StopCurrentStep();
            brain.CurrentMeleeStep = _session.CurrentStep;
            brain.CurrentMeleeStepIndex = _session.CurrentStepIndex;
            SkillCastStartResult result = ctx.SkillManager.TryStartMeleeStep(_session.CurrentStep, CurrentMeleeType);
            if (!result.Started) { FinishCombo(); return false; }
            _requestId = result.RequestId;
            return true;
        }
        finally { _changingStep = false; }
    }
    public void InterruptMelee()
    {
        if (_changingStep) return;
        _changingStep = true;
        try { StopCurrentStep(); FinishCombo(); }
        finally { _changingStep = false; }
    }
    void StopCurrentStep()
    {
        int oldRequest = _requestId;
        _requestId = 0;
        if (oldRequest <= 0) return;
        _hitboxRuntime?.StopExecution(oldRequest);
        ctx?.AnimDriver?.CancelSkillCastRequest(oldRequest);
    }
    void FinishCombo()
    {
        bool wasActive = _session.IsActive;
        _requestId = 0;
        _session.Clear();
        if (stateHub != null && stateHub.WeaponSM.CurrentId == WeaponStateId.Melee)
            stateHub.WeaponSM.TryChange(WeaponStateId.Ready);
        if (wasActive) brain?.ReportMeleeComboEnded();
    }
    void OnChainWindowOpened()
    {
        if (_session.IsActive && _session.NotifyChainWindowOpened() == MeleeSessionAction.Advance) PlayCurrentStep();
    }
    void OnChainWindowClosed() => _session.NotifyChainWindowClosed();
    void OnPlaybackEvent(CharacterAnimBrain.PlaybackSignal signal)
    {
        if (_changingStep || signal.Kind != CharacterAnimBrain.PlaybackKind.Melee || signal.RequestId != _requestId) return;
        if (signal.Phase == CharacterAnimBrain.PlaybackPhase.Interrupted) InterruptMelee();
        else if (signal.Phase == CharacterAnimBrain.PlaybackPhase.Completed)
        {
            _hitboxRuntime?.StopExecution(_requestId);
            _requestId = 0;
            brain.ReportMeleeStepCompleted();
            if (_session.NotifyStepCompleted() == MeleeSessionAction.Advance) PlayCurrentStep();
            else FinishCombo();
        }
    }
    internal SkillHitboxSequenceRuntime GetHitboxRuntime(PrefabHitboxSkillPayloadDef payload)
    {
        if (!_hitboxRuntimes.TryGetValue(payload, out _hitboxRuntime) || _hitboxRuntime == null)
        {
            var host = new GameObject("MeleeSkillHitboxRuntime");
            host.transform.SetParent(transform, false);
            _hitboxRuntime = host.AddComponent<SkillHitboxSequenceRuntime>();
            _hitboxRuntime.KeepForReuse();
            _hitboxRuntimes[payload] = _hitboxRuntime;
        }
        return _hitboxRuntime;
    }
    void ResolveRefs()
    {
        if (ctx == null) ctx = CharacterContextModuleLookup.ResolveContext(gameObject);
        ctx?.ResolveReferences();
        if (ctx == null) return;
        ctx.MeleeController = this;
        stateHub = ctx.stateHub;
        brain = ctx.AnimBrain;
        weaponSystem = ctx.WeaponSystem;
    }
    public static bool IsCombatOnlyHitbox(Collider other)
    {
        if (other == null || !other.isTrigger) return false;
        var group = other.GetComponentInParent<SkillHitboxGroup>();
        return group != null && group.Contains(other);
    }
}
