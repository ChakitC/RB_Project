using System.Collections.Generic;
using Animancer;
using UnityEngine;
using UnityEngine.Playables;

public sealed partial class CharacterAnimBrain
{
    private AnimancerLayer upperActionPresentationLayer;
    private AnimancerLayer fullBodyActionLayer;
    private AvatarMask fullBodyActionMask;
    private readonly List<AnimancerState> retiredTemporaryStates = new();
    private readonly Dictionary<AnimancerState, PairOffsetUpperAction> actionOffsetStates = new();
    private readonly List<AnimancerState> finishedOffsetStates = new();

    private void InitializePresentationLayers()
    {
        upperActionPresentationLayer = ActLayer;
        upperActionPresentationLayer.Mask = UpperBodyMask;
        upperActionPresentationLayer.SetLayerWeightOnPlay = false;
        upperActionPresentationLayer.Weight = 0f;

        // Reserve the next layer above both configured layers. Production prefabs use 0 and 1.
        fullBodyActionLayer = animancer.Layers[Mathf.Max(locomotionLayerIndex, actionLayerIndex) + 1];
        fullBodyActionMask = new AvatarMask
        {
            name = "Character Full Body Action",
            hideFlags = HideFlags.HideAndDontSave,
        };
        fullBodyActionLayer.Mask = fullBodyActionMask;
        fullBodyActionLayer.SetLayerWeightOnPlay = false;
        fullBodyActionLayer.Weight = 0f;
    }

    private AnimancerState PlayLocomotionTransition(ITransition transition)
        => TrackPlayedState(LocoLayer.Play(transition));

    private AnimancerState PlayLocomotionTransition(ITransition transition, float fadeDuration,
        FadeMode mode = FadeMode.FixedSpeed)
        => TrackPlayedState(LocoLayer.Play(transition, fadeDuration, mode));

    private AnimancerState PlayLocomotionTransition(AnimationClip clip, float fadeDuration)
        => TrackPlayedState(LocoLayer.Play(clip, fadeDuration));

    private AnimancerState PlayLocomotionTransition(AnimancerState state, float fadeDuration)
        => TrackPlayedState(LocoLayer.Play(state, fadeDuration));

    private AnimancerState TrackPlayedState(AnimancerState state)
    {
        // A cached state may be played again before its previous fade finishes.
        // Reclaiming it cancels cleanup belonging to that previous playback.
        retiredTemporaryStates.Remove(state);
        return state;
    }

    private AnimancerState PlayActionPresentation(ClipTransition transition,
        PairOffsetUpperAction upperAction, bool fullBody)
    {
        if (transition == null || !transition.IsValid)
            return null;

        AnimancerLayer layer = fullBody ? fullBodyActionLayer : ActLayer;
        FadeActionLayer(fullBody ? ActLayer : fullBodyActionLayer);
        AnimancerState state;
        if (layer.Weight > ActionLayerActiveWeightThreshold && layer.CurrentState != null)
        {
            layer.StartFade(1f, ActionFadeIn);
            state = layer.Play(transition);
        }
        else
        {
            float weight = layer.Weight;
            state = layer.Play(transition, 0f, transition.FadeMode);
            layer.Weight = weight;
            layer.StartFade(1f, ActionFadeIn);
        }

        TrackPlayedState(state);
        if (!fullBody && state != null)
            actionOffsetStates[state] = upperAction;
        SetActivePairUpperAction(upperAction);
        return state;
    }

    private void FadeActionLayer(AnimancerLayer layer)
    {
        if (layer != null && layer.Playable.IsValid())
            layer.StartFade(0f, ActionFadeOut);
    }

    private void FadeOutActions()
    {
        FadeActionLayer(upperActionPresentationLayer);
        FadeActionLayer(fullBodyActionLayer);
    }

    private void RetireTemporaryState(AnimancerState state)
    {
        if (state == null || !state.Playable.IsValid())
            return;

        // Gameplay has ended, but this pose is still needed by the next cross fade.
        state.SharedEvents = null;
        state.Speed = 0f;
        if (!retiredTemporaryStates.Contains(state))
            retiredTemporaryStates.Add(state);
    }

    private void UpdatePresentationTransitions()
    {
        for (int i = retiredTemporaryStates.Count - 1; i >= 0; i--)
        {
            AnimancerState state = retiredTemporaryStates[i];
            if (!state.Playable.IsValid())
                retiredTemporaryStates.RemoveAt(i);
            else if (state.Weight == 0f && state.TargetWeight == 0f)
            {
                retiredTemporaryStates.RemoveAt(i);
                state.Destroy();
            }
        }

        finishedOffsetStates.Clear();
        foreach (var pair in actionOffsetStates)
            if (!pair.Key.Playable.IsValid() ||
                (pair.Key.Weight == 0f && pair.Key.TargetWeight == 0f))
                finishedOffsetStates.Add(pair.Key);
        foreach (var state in finishedOffsetStates)
            actionOffsetStates.Remove(state);
    }

    public bool TryGetPairOffsetContributions(List<PairOffsetBasePoseWeight> basePoses,
        List<PairOffsetActionWeight> actions, out PairOffsetProfilesSO profiles)
    {
        basePoses?.Clear();
        actions?.Clear();
        profiles = null;
        if (basePoses == null || actions == null || !TryInitialize())
            return false;

        profiles = PairOffsetProfiles;
        if (profiles == null)
            return false;

        float weight = Mathf.Clamp01(ActLayer.Weight) *
            (1f - Mathf.Clamp01(fullBodyActionLayer.Weight));
        foreach (var pair in actionOffsetStates)
        {
            if (pair.Value == PairOffsetUpperAction.None || !pair.Key.Playable.IsValid())
                continue;
            float contribution = weight * Mathf.Clamp01(pair.Key.Weight);
            if (contribution > 0.001f)
                actions.Add(new PairOffsetActionWeight(pair.Value, contribution));
        }

        // Keep the outgoing locomotion pose description during an exclusive takeover.
        // Its upper-body corrections disappear with the outgoing action layer.
        basePoses.AddRange(_activePairBasePoseWeights);
        return actions.Count > 0 && basePoses.Count > 0;
    }

    private void StopPresentationTransitions()
    {
        foreach (var state in retiredTemporaryStates)
            if (state.Playable.IsValid())
                state.Destroy();
        retiredTemporaryStates.Clear();
        actionOffsetStates.Clear();
        finishedOffsetStates.Clear();
        if (upperActionPresentationLayer != null && upperActionPresentationLayer.Playable.IsValid())
            upperActionPresentationLayer.Stop();
        if (fullBodyActionLayer != null && fullBodyActionLayer.Playable.IsValid())
            fullBodyActionLayer.Stop();
    }

    private void DisposePresentationLayers()
    {
        StopPresentationTransitions();
        if (fullBodyActionLayer != null && fullBodyActionLayer.Playable.IsValid())
            fullBodyActionLayer.Mask = null;
        upperActionPresentationLayer = null;
        fullBodyActionLayer = null;
        if (fullBodyActionMask != null)
        {
            if (Application.isPlaying) Object.Destroy(fullBodyActionMask);
            else Object.DestroyImmediate(fullBodyActionMask);
            fullBodyActionMask = null;
        }
    }
}
