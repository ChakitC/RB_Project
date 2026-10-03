using Animancer;
using UnityEngine;

internal readonly struct CharacterAnimationBindingSnapshot
{
    internal readonly CharacteContext Context;
    internal readonly AnimancerComponent Animancer;
    internal readonly Animator Animator;
    internal readonly CharacterAnimProfileSO Profile;
    internal readonly AnimancerGraph Graph;
    internal readonly int Generation;

    internal CharacterAnimationBindingSnapshot(CharacterAnimationBinding binding)
    {
        Context = binding.Context;
        Animancer = binding.Animancer;
        Animator = binding.Animator;
        Profile = binding.Profile;
        Graph = binding.Graph;
        Generation = binding.Generation;
    }
}
