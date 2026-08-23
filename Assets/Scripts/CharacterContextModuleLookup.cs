using UnityEngine;

/// <summary>
/// Resolves shared character modules through <see cref="CharacteContext"/> first, then falls back
/// to a search bounded by the actor that owns the context.
///
/// Character prefabs are not uniform: a peer module may sit on the context root or on a child
/// branch such as "GamePlayStats_System". A one-direction <c>GetComponent*</c> lookup silently
/// drops combat events or status applications for whichever prefab layout it does not match, so
/// every cross-module lookup goes through the context hub before touching the hierarchy.
///
/// Two rules make the answer trustworthy rather than merely non-null:
///
/// 1. Every parent walk passes <c>includeInactive: true</c>. Actors are routinely deactivated -
///    the helper is hidden between summons, and the whole party is built under an inactive root -
///    and a hidden actor is still that actor.
/// 2. No search ever crosses an actor boundary. Party members live side by side under one
///    <c>PartyRuntimeRoot</c>, so a sweep from the scene root returns whichever character happens
///    to be first, which is a silently wrong answer rather than a missing one.
/// </summary>
public static class CharacterContextModuleLookup
{
    public static CharacteContext ResolveContext(Component target)
    {
        return target != null ? ResolveContext(target.gameObject) : null;
    }

    /// <summary>
    /// The context that owns <paramref name="target"/>, or null when it is not part of a character.
    ///
    /// Self or ancestor only. A character module never sits above its own context, so anything not
    /// found by walking up is simply not a character module. Searching downwards instead would let
    /// any object that merely *contains* an actor - a spawn point, a formation node, a pooling
    /// container, the party root - claim that actor's modules as its own.
    /// </summary>
    public static CharacteContext ResolveContext(GameObject target)
    {
        if (target == null)
            return null;

        if (target.TryGetComponent(out CharacteContext self))
            return self;

        return target.GetComponentInParent<CharacteContext>(true);
    }

    /// <summary>
    /// Resolved context for <paramref name="target"/> with its references already bound. Returns
    /// null when the object is not part of a character.
    /// </summary>
    public static CharacteContext ResolveResolvedContext(GameObject target)
    {
        CharacteContext context = ResolveContext(target);
        context?.ResolveReferences();
        return context;
    }

    public static CombatEventBus ResolveCombatEventBus(GameObject target, CharacteContext knownContext = null)
    {
        if (target == null && knownContext == null)
            return null;

        CharacteContext context = knownContext != null ? knownContext : ResolveContext(target);
        if (context != null)
        {
            context.ResolveReferences();
            if (context.CombatEventBus != null)
                return context.CombatEventBus;
        }

        return ResolveModuleWithinActor<CombatEventBus>(target, context);
    }

    public static StatusEffectController ResolveStatusEffects(GameObject target, CharacteContext knownContext = null)
    {
        if (target == null && knownContext == null)
            return null;

        CharacteContext context = knownContext != null ? knownContext : ResolveContext(target);
        if (context != null)
        {
            context.ResolveReferences();
            if (context.StatusEffects != null)
                return context.StatusEffects;
        }

        return ResolveModuleWithinActor<StatusEffectController>(target, context);
    }

    /// <summary>
    /// Finds a module for one actor without ever leaving that actor.
    ///
    /// When the context is known, its own subtree is the whole search space - that covers the
    /// sibling-branch layouts this project uses, such as a bus on "GamePlayStats_System", while
    /// making it impossible to return a neighbouring character's module. Without a context there
    /// is no actor boundary to trust, so the search is limited to the object itself and its
    /// ancestors.
    /// </summary>
    static T ResolveModuleWithinActor<T>(GameObject target, CharacteContext context) where T : Component
    {
        if (context != null)
            return context.GetComponentInChildren<T>(true);

        if (target == null)
            return null;

        if (target.TryGetComponent(out T local))
            return local;

        return target.GetComponentInParent<T>(true);
    }
}
