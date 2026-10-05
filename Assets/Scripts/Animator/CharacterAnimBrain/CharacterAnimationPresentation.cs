using System.Collections.Generic;
using Animancer;
using UnityEngine;
using UnityEngine.Playables;

internal sealed class CharacterAnimationPresentation
{
    private AnimancerLayer upperActionPresentationLayer;
    private AnimancerLayer fullBodyActionLayer;
    private AvatarMask fullBodyActionMask;
    private readonly List<AnimancerState> retiredTemporaryStates = new();
    private readonly Dictionary<AnimancerState, PairOffsetUpperAction> actionOffsetStates = new();
    private readonly List<AnimancerState> finishedOffsetStates = new();
    private readonly CharacterAnimationContributions baseContributions = new();

    private AnimancerComponent animancer;
    private CharacterAnimProfileSO profile;
    private int locomotionLayerIndex, actionLayerIndex;
    private AnimancerLayer ActLayer => animancer.Layers[actionLayerIndex];
    private AnimancerLayer LocoLayer => animancer.Layers[locomotionLayerIndex];
    private AvatarMask UpperBodyMask => profile.upperBodyMask;
    private float ActionFadeIn => profile.actionFadeIn;
    private float ActionFadeOut => profile.actionFadeOut;
    private const float ActionLayerActiveWeightThreshold = 0.001f;
    internal AnimancerLayer FullBodyLayer => fullBodyActionLayer;
    internal float LocomotionWeight => LayerWeight(animancer != null && animancer.IsGraphInitialized ? LocoLayer : null);
    internal float UpperWeight => LayerWeight(upperActionPresentationLayer);
    internal float FullBodyWeight => LayerWeight(fullBodyActionLayer);
    private static float LayerWeight(AnimancerLayer layer) => layer != null && layer.Playable.IsValid() ? layer.Weight : 0f;
    internal int PendingCleanupCount => retiredTemporaryStates.Count;

    internal void Bind(CharacterAnimationBindingSnapshot binding, int locomotion, int action)
    {
        DisposePresentationLayers();
        animancer = binding.Animancer;
        profile = binding.Profile;
        locomotionLayerIndex = locomotion;
        actionLayerIndex = action;
        InitializePresentationLayers();
    }

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

    internal AnimancerState PlayLocomotionTransition(ITransition transition)
        => RegisterLocomotion(PrepareTransitionState(LocoLayer.Play(transition), transition), transition);

    internal AnimancerState PlayLocomotionTransition(ITransition transition, float fadeDuration,
        FadeMode mode = FadeMode.FixedSpeed)
        => RegisterLocomotion(PrepareTransitionState(LocoLayer.Play(transition, fadeDuration, mode), transition), transition);

    private AnimancerState RegisterLocomotion(AnimancerState state, ITransition transition)
    {
        baseContributions.Register(state, transition, profile);
        return state;
    }

    internal void GetBaseContributions(List<PairOffsetBasePoseWeight> output)
        => baseContributions.Collect(LocoLayer, output);

    internal AnimancerState PlayLocomotionTransition(AnimationClip clip, float fadeDuration)
        => UnregisteredLocomotion(TrackPlayedState(LocoLayer.Play(clip, fadeDuration)));

    internal AnimancerState PlayLocomotionTransition(AnimancerState state, float fadeDuration)
        => UnregisteredLocomotion(TrackPlayedState(LocoLayer.Play(state, fadeDuration)));

    private AnimancerState UnregisteredLocomotion(AnimancerState state)
    {
        baseContributions.Forget(state);
        return state;
    }

    private AnimancerState PrepareTransitionState(AnimancerState state, ITransition transition)
    {
        // Animancer assigns transition events only when creating a state. Cleanup detaches
        // our callbacks, so replay must restore the authored sequence before binding new ones.
        if (state != null) state.SharedEvents = transition.Events;
        return TrackPlayedState(state);
    }

    private AnimancerState TrackPlayedState(AnimancerState state)
    {
        // A cached state may be played again before its previous fade finishes.
        // Reclaiming it cancels cleanup belonging to that previous playback.
        retiredTemporaryStates.Remove(state);
        return state;
    }

    internal AnimancerState PlayActionPresentation(ClipTransition transition,
        PairOffsetUpperAction upperAction, bool fullBody, float? fade = null)
    {
        if (transition == null || !transition.IsValid)
            return null;

        AnimancerLayer layer = fullBody ? fullBodyActionLayer : ActLayer;
        FadeActionLayer(fullBody ? ActLayer : fullBodyActionLayer);
        AnimancerState state;
        if (layer.Weight > ActionLayerActiveWeightThreshold && layer.CurrentState != null)
        {
            layer.StartFade(1f, fade ?? ActionFadeIn);
            state = fade.HasValue ? layer.Play(transition, fade.Value) : layer.Play(transition);
        }
        else
        {
            float weight = layer.Weight;
            state = layer.Play(transition, 0f, transition.FadeMode);
            layer.Weight = weight;
            layer.StartFade(1f, fade ?? ActionFadeIn);
        }

        PrepareTransitionState(state, transition);
        if (!fullBody && state != null)
            actionOffsetStates[state] = upperAction;
        return state;
    }

    internal void FadeActionLayer(AnimancerLayer layer)
    {
        if (layer != null && layer.Playable.IsValid())
            layer.StartFade(0f, ActionFadeOut);
    }

    internal void FadeOutActions()
    {
        FadeActionLayer(upperActionPresentationLayer);
        FadeActionLayer(fullBodyActionLayer);
    }

    internal void RetireTemporaryState(AnimancerState state)
    {
        if (state == null || !state.Playable.IsValid())
            return;

        // Gameplay has ended, but this pose is still needed by the next cross fade.
        state.SharedEvents = null;
        state.Speed = 0f;
        if (!retiredTemporaryStates.Contains(state))
            retiredTemporaryStates.Add(state);
    }

    internal void UpdatePresentationTransitions()
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

    internal void GetActionContributions(List<PairOffsetActionWeight> actions)
    {
        float weight = Mathf.Clamp01(ActLayer.Weight) * (1f - Mathf.Clamp01(fullBodyActionLayer.Weight));
        foreach (var pair in actionOffsetStates)
        {
            if (pair.Value == PairOffsetUpperAction.None || !pair.Key.Playable.IsValid()) continue;
            float contribution = weight * Mathf.Clamp01(pair.Key.Weight);
            if (contribution > 0f) actions.Add(new PairOffsetActionWeight(pair.Value, contribution, pair.Key.Clip));
        }
    }

    internal void StopPresentationTransitions()
    {
        foreach (var state in retiredTemporaryStates)
            if (state.Playable.IsValid())
                state.Destroy();
        retiredTemporaryStates.Clear();
        actionOffsetStates.Clear();
        baseContributions.Clear();
        finishedOffsetStates.Clear();
        if (upperActionPresentationLayer != null && upperActionPresentationLayer.Playable.IsValid())
            upperActionPresentationLayer.Stop();
        if (fullBodyActionLayer != null && fullBodyActionLayer.Playable.IsValid())
            fullBodyActionLayer.Stop();
    }

    internal void DisposePresentationLayers()
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
