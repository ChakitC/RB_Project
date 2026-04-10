using System.Text;
using Animancer;
using Animancer.FSM;
using UnityEngine;

public sealed partial class CharacterAnimBrain
{
    public readonly struct DebugLayerSnapshot
    {
        public readonly bool IsValid;
        public readonly int LayerIndex;
        public readonly float Weight;
        public readonly string GameplayStateName;
        public readonly string AssetName;
        public readonly float NormalizedTime;
        public readonly float Length;
        public readonly float Speed;
        public readonly bool IsPlaying;

        public DebugLayerSnapshot(
            bool isValid,
            int layerIndex,
            float weight,
            string gameplayStateName,
            string assetName,
            float normalizedTime,
            float length,
            float speed,
            bool isPlaying)
        {
            IsValid = isValid;
            LayerIndex = layerIndex;
            Weight = weight;
            GameplayStateName = gameplayStateName;
            AssetName = assetName;
            NormalizedTime = normalizedTime;
            Length = length;
            Speed = speed;
            IsPlaying = isPlaying;
        }
    }

    public readonly struct DebugMeleeSnapshot
    {
        public readonly bool IsActive;
        public readonly string ComboName;
        public readonly string ClipName;
        public readonly int StepIndex;
        public readonly int BufferedPresses;
        public readonly bool ChainWindowOpen;
        public readonly bool PressedInWindow;
        public readonly bool WindowExpired;
        public readonly float ChainWindowStart;
        public readonly float ChainWindowEnd;
        public readonly float HitWindowStart;
        public readonly float HitWindowEnd;
        public readonly float NormalizedTime;

        public DebugMeleeSnapshot(
            bool isActive,
            string comboName,
            string clipName,
            int stepIndex,
            int bufferedPresses,
            bool chainWindowOpen,
            bool pressedInWindow,
            bool windowExpired,
            float chainWindowStart,
            float chainWindowEnd,
            float hitWindowStart,
            float hitWindowEnd,
            float normalizedTime)
        {
            IsActive = isActive;
            ComboName = comboName;
            ClipName = clipName;
            StepIndex = stepIndex;
            BufferedPresses = bufferedPresses;
            ChainWindowOpen = chainWindowOpen;
            PressedInWindow = pressedInWindow;
            WindowExpired = windowExpired;
            ChainWindowStart = chainWindowStart;
            ChainWindowEnd = chainWindowEnd;
            HitWindowStart = hitWindowStart;
            HitWindowEnd = hitWindowEnd;
            NormalizedTime = normalizedTime;
        }
    }

    public bool DebugIsInitialized => _initialized;
    public string DebugInitializationError => _initialized ? string.Empty : GetInitializationError();
    public Animator DebugBoundAnimator => _boundAnimator != null ? _boundAnimator : animancer != null ? animancer.Animator : null;
    public CharacterAnimProfileSO DebugProfile => _boundAnimProfile != null
        ? _boundAnimProfile
        : ctx != null && ctx.baseStats != null ? ctx.baseStats.animProfile : null;
    public string DebugLocomotionStateName => GetStateDisplayName(locomotionSM.CurrentState);
    public string DebugActionStateName => GetStateDisplayName(actionSM.CurrentState);
    public string DebugPendingActionName => _pendingAction.ToString();
    public bool DebugPendingPulse => _pendingPulse;
    public string DebugCurrentStatusLocomotionKindName => _currentStatusLocomotionKind.ToString();
    public string DebugResolvedStatusLocomotionKindName => ResolveStatusLocomotionKind().ToString();
    public string DebugActiveSkillDefinitionName => _activeSkillDefinition != null ? _activeSkillDefinition.name : "<none>";
    public int DebugActiveSkillRequestId => _activeSkillRequestId;
    public int DebugActiveUtilityRequestId => _activeUtilityRequestId;
    public int DebugActiveChainRequestId => _activeChainRequestId;
    public float DebugActiveSkillCastPointNormalized => _activeSkillCastPointNormalized;
    public float DebugActiveUtilityCastPointNormalized => _activeUtilityCastPointNormalized;
    public float DebugActiveChainCastPointNormalized => _activeChainCastPointNormalized;
    public float DebugActiveChainAdvancePointNormalized => _activeChainAdvancePointNormalized;
    public bool DebugSkillReleaseRequested => _activeSkillReleaseRequested;
    public bool DebugSkillReleased => _activeSkillReleased;
    public bool DebugUtilityReleaseRequested => _activeUtilityReleaseRequested;
    public bool DebugUtilityReleased => _activeUtilityReleased;
    public bool DebugChainReleaseRequested => _activeChainReleaseRequested;
    public bool DebugChainReleased => _activeChainReleased;
    public bool DebugChainAdvanceRequested => _activeChainAdvanceRequested;
    public bool DebugChainAdvanceReleased => _activeChainAdvanceReleased;
    public string DebugActiveChainKindName => _activeChainKind.ToString();
    public bool DebugChainStateCanExit => _chainStateCanExit;

    public DebugLayerSnapshot DebugLocomotionLayer => CreateLayerSnapshot(locomotionLayerIndex, locomotionSM.CurrentState);
    public DebugLayerSnapshot DebugActionLayer => CreateLayerSnapshot(actionLayerIndex, actionSM.CurrentState);
    public DebugMeleeSnapshot DebugMelee => CreateMeleeSnapshot();

    public string DebugDescribeRequests()
    {
        var sb = new StringBuilder(320);
        sb.Append("Skill(req=").Append(_activeSkillRequestId)
            .Append(", clip=").Append(DebugActiveSkillDefinitionName)
            .Append(", castN=").Append(_activeSkillCastPointNormalized.ToString("0.00"))
            .Append(", requested=").Append(_activeSkillReleaseRequested)
            .Append(", released=").Append(_activeSkillReleased)
            .Append(")");

        sb.Append(" | Utility(req=").Append(_activeUtilityRequestId)
            .Append(", castN=").Append(_activeUtilityCastPointNormalized.ToString("0.00"))
            .Append(", requested=").Append(_activeUtilityReleaseRequested)
            .Append(", released=").Append(_activeUtilityReleased)
            .Append(")");

        sb.Append(" | Chain(req=").Append(_activeChainRequestId)
            .Append(", kind=").Append(_activeChainKind)
            .Append(", castN=").Append(_activeChainCastPointNormalized.ToString("0.00"))
            .Append(", advanceN=").Append(_activeChainAdvancePointNormalized.ToString("0.00"))
            .Append(", castReq=").Append(_activeChainReleaseRequested)
            .Append(", castDone=").Append(_activeChainReleased)
            .Append(", advReq=").Append(_activeChainAdvanceRequested)
            .Append(", advDone=").Append(_activeChainAdvanceReleased)
            .Append(", canExit=").Append(_chainStateCanExit)
            .Append(")");

        return sb.ToString();
    }

    public string DebugDescribeMelee()
    {
        DebugMeleeSnapshot melee = DebugMelee;
        if (!melee.IsActive)
            return "Inactive";

        return
            $"Combo={melee.ComboName}, Step={melee.StepIndex}, Clip={melee.ClipName}, " +
            $"StateN={melee.NormalizedTime:0.00}, Buffer={melee.BufferedPresses}, " +
            $"Hit=[{melee.HitWindowStart:0.00}-{melee.HitWindowEnd:0.00}], " +
            $"Chain=[{melee.ChainWindowStart:0.00}-{melee.ChainWindowEnd:0.00}], " +
            $"Open={melee.ChainWindowOpen}, PressedInWindow={melee.PressedInWindow}, Expired={melee.WindowExpired}";
    }

    private DebugLayerSnapshot CreateLayerSnapshot(int layerIndex, IState gameplayState)
    {
        if (animancer == null || animancer.Layers == null || layerIndex < 0 || layerIndex >= animancer.Layers.Count)
            return new DebugLayerSnapshot(false, layerIndex, 0f, GetStateDisplayName(gameplayState), "<missing layer>", 0f, 0f, 0f, false);

        AnimancerLayer layer = animancer.Layers[layerIndex];
        AnimancerState state = layer != null ? layer.CurrentState : null;
        string assetName = GetAnimancerAssetName(state);

        return new DebugLayerSnapshot(
            state != null,
            layerIndex,
            layer != null ? layer.Weight : 0f,
            GetStateDisplayName(gameplayState),
            assetName,
            state != null ? state.NormalizedTime : 0f,
            state != null ? state.Length : 0f,
            state != null ? state.Speed : 0f,
            state != null && state.IsPlaying);
    }

    private DebugMeleeSnapshot CreateMeleeSnapshot()
    {
        bool isActive = locomotionSM.CurrentState == meleeCombo && meleeCombo != null;
        var currentStep = CurrentMeleeStep;
        string comboName = meleeCombo != null && meleeCombo.CurrentCombo != null
            ? meleeCombo.CurrentCombo.name
            : DefaultMeleeCombo != null ? DefaultMeleeCombo.name : "<none>";
        string clipName = meleeCombo != null && meleeCombo.DebugState != null
            ? GetAnimancerAssetName(meleeCombo.DebugState)
            : currentStep.clip != null && currentStep.clip.Clip != null ? currentStep.clip.Clip.name : "<none>";

        return new DebugMeleeSnapshot(
            isActive,
            comboName,
            clipName,
            CurrentMeleeStepIndex,
            meleeCombo != null ? meleeCombo.DebugBufferedPresses : 0,
            meleeCombo != null && meleeCombo.DebugChainWindowOpen,
            meleeCombo != null && meleeCombo.DebugPressedInWindow,
            meleeCombo != null && meleeCombo.DebugWindowExpired,
            meleeCombo != null ? meleeCombo.DebugChainWindowStart : 0f,
            meleeCombo != null ? meleeCombo.DebugChainWindowEnd : 0f,
            currentStep.hitWindowN.x,
            currentStep.hitWindowN.y,
            meleeCombo != null && meleeCombo.DebugState != null ? meleeCombo.DebugState.NormalizedTime : 0f);
    }

    private static string GetStateDisplayName(object state)
    {
        return state != null ? state.GetType().Name : "<none>";
    }

    private static string GetAnimancerAssetName(AnimancerState state)
    {
        if (state == null)
            return "<none>";

        Object mainObject = state.MainObject;
        return mainObject != null ? mainObject.name : state.GetType().Name;
    }
}
