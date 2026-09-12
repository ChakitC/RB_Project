using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Allocation-free inventory of enabled character actors for player targeting.</summary>
public static class CharacterContextRegistry
{
    static readonly List<CharacteContext> activeContexts = new List<CharacteContext>(64);

    public static IReadOnlyList<CharacteContext> ActiveContexts => activeContexts;
    public static event Action<CharacteContext> ContextUnregistered;

    public static void Register(CharacteContext context)
    {
        if (context == null || activeContexts.Contains(context))
            return;

        activeContexts.Add(context);
    }

    public static void Unregister(CharacteContext context)
    {
        if (context != null && activeContexts.Remove(context))
            ContextUnregistered?.Invoke(context);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        activeContexts.Clear();
        ContextUnregistered = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void ReconcileActiveScene()
    {
        CharacteContext[] contexts = UnityEngine.Object.FindObjectsByType<CharacteContext>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        for (int i = 0; i < contexts.Length; i++)
        {
            CharacteContext context = contexts[i];
            if (context != null && context.isActiveAndEnabled)
                Register(context);
        }
    }
}
