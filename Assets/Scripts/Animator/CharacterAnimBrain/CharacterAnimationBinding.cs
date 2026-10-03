using Animancer;
using UnityEngine;

internal sealed class CharacterAnimationBinding
{
    internal CharacteContext Context { get; private set; }
    internal AnimancerComponent Animancer { get; private set; }
    internal Animator Animator { get; private set; }
    internal CharacterAnimProfileSO Profile { get; private set; }
    internal AnimancerGraph Graph { get; private set; }
    internal int Generation { get; private set; }
    internal CharacterAnimationBindingSnapshot Snapshot => new(this);
    private bool warnedAmbiguous;
    internal void Resolve(Component owner, ref CharacteContext context, ref AnimancerComponent animancer)
    {
        if (context == null) context = owner.GetComponentInParent<CharacteContext>();
        context?.ResolveReferences();
        if (animancer == null) animancer = owner.GetComponent<AnimancerComponent>();
        if (animancer == null && context != null)
        {
            var candidates = context.GetComponentsInChildren<AnimancerComponent>(true);
            if (candidates.Length == 1) animancer = candidates[0];
            else if (candidates.Length > 1 && !warnedAmbiguous)
            {
                warnedAmbiguous = true;
                Debug.LogWarning("[CharacterAnimBrain] Multiple Animancer components found. Bind this actor's Animancer explicitly on its Brain.", owner);
            }
        }
        Context = context;
    }
    internal void Commit(AnimancerComponent animancer, CharacterAnimProfileSO profile)
    {
        Animancer = animancer;
        Animator = animancer.Animator;
        Profile = profile;
        Graph = animancer.Graph;
        Generation++;
    }
    internal CharacterAnimProfileSO ResolveProfile(CharacterAnimProfileSO overrideProfile, CharacteContext context)
        => overrideProfile != null ? overrideProfile : context != null && context.baseStats != null ? context.baseStats.animProfile : null;
}
