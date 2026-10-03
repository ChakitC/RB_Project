using System.Collections.Generic;
using Animancer;
using UnityEngine;

public sealed partial class CharacterAnimBrain
{
    private readonly CharacterAnimationPresentation presentation = new();
    private AnimationPlayback CreatePlayback() => new(PlayClip, Timeline, sessions);
    private AnimancerState PlayClip(AnimationPlaybackOptions options)
    {
        if (options.TargetLayer != AnimationPlaybackOptions.Layer.Locomotion)
            return PlayActionPresentation(options.Transition, options.PairAction,
                options.TargetLayer == AnimationPlaybackOptions.Layer.FullBodyAction, options.FadeDuration);
        return options.FadeDuration.HasValue
            ? presentation.PlayLocomotionTransition(options.Transition, options.FadeDuration.Value, options.FadeMode)
            : presentation.PlayLocomotionTransition(options.Transition);
    }

    private AnimancerLayer fullBodyActionLayer => presentation.FullBodyLayer;
    private AnimancerState PlayActionPresentation(ClipTransition transition, PairOffsetUpperAction action, bool fullBody, float? fade = null)
    {
        var state = presentation.PlayActionPresentation(transition, action, fullBody, fade);
        if (state != null) SetActivePairUpperAction(action);
        return state;
    }

    public bool TryGetPairOffsetContributions(List<PairOffsetBasePoseWeight> basePoses,
        List<PairOffsetActionWeight> actions, out PairOffsetProfilesSO profiles)
    {
        basePoses?.Clear();
        actions?.Clear();
        profiles = null;
        if (basePoses == null || actions == null || !TryInitialize()) return false;
        profiles = PairOffsetProfiles;
        if (profiles == null) return false;
        presentation.GetActionContributions(actions);
        basePoses.AddRange(_activePairBasePoseWeights);
        return actions.Count > 0 && basePoses.Count > 0;
    }
}
