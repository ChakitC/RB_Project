using UnityEngine;

/// <summary>
/// Resolves shared character modules through <see cref="CharacteContext"/> first, then falls back
/// to a self/parent/child search.
///
/// Character prefabs are not uniform: a peer module may sit on the context root, above it, or on a
/// child branch such as "GamePlayStats_System". A one-direction <c>GetComponent*</c> lookup silently
/// drops combat events or status applications for whichever prefab layout it does not match, so
/// every cross-module lookup goes through the context hub before touching the hierarchy.
/// </summary>
public static class CharacterContextModuleLookup
{
    public static CharacteContext ResolveContext(Component target)
    {
        return target != null ? ResolveContext(target.gameObject) : null;
    }

    public static CharacteContext ResolveContext(GameObject target)
    {
        if (target == null)
            return null;

        CharacteContext context = target.GetComponent<CharacteContext>();
        if (context == null)
            context = target.GetComponentInParent<CharacteContext>();
        if (context == null)
            context = target.GetComponentInChildren<CharacteContext>(true);
        if (context == null)
            context = SearchActorTree<CharacteContext>(target);

        return context;
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

        if (target == null)
            return null;

        CombatEventBus bus = target.GetComponent<CombatEventBus>();
        if (bus == null)
            bus = target.GetComponentInParent<CombatEventBus>();
        if (bus == null)
            bus = target.GetComponentInChildren<CombatEventBus>(true);
        if (bus == null)
            bus = SearchActorTree<CombatEventBus>(target);

        return bus;
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

        if (target == null)
            return null;

        StatusEffectController controller = target.GetComponent<StatusEffectController>();
        if (controller == null)
            controller = target.GetComponentInParent<StatusEffectController>();
        if (controller == null)
            controller = target.GetComponentInChildren<StatusEffectController>(true);
        if (controller == null)
            controller = SearchActorTree<StatusEffectController>(target);

        return controller;
    }

    /// <summary>
    /// Last resort: sweep the whole actor tree from its root. Catches sibling layouts, where the
    /// module hangs off a different branch than the object asking for it, which none of the
    /// self/parent/child lookups can reach.
    /// </summary>
    static T SearchActorTree<T>(GameObject target) where T : Component
    {
        if (target == null)
            return null;

        Transform root = target.transform.root;
        if (root == null || root == target.transform)
            return null;

        return root.GetComponentInChildren<T>(true);
    }
}
