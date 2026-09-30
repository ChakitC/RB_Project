using System;
using UnityEngine;

// A presentation subscriber must not abort an owner's committed lifecycle transition.
internal static class CharacterStateNotifications
{
    public static void Publish(Action handlers)
    {
        if (handlers == null) return;
        foreach (Action handler in handlers.GetInvocationList())
            try { handler(); } catch (Exception error) { Debug.LogException(error); }
    }
    public static void Publish<T>(Action<T> handlers, T value)
    {
        if (handlers == null) return;
        foreach (Action<T> handler in handlers.GetInvocationList())
            try { handler(value); } catch (Exception error) { Debug.LogException(error); }
    }
    public static void Publish<TFirst, TSecond>(Action<TFirst, TSecond> handlers, TFirst first, TSecond second)
    {
        if (handlers == null) return;
        foreach (Action<TFirst, TSecond> handler in handlers.GetInvocationList())
            try { handler(first, second); } catch (Exception error) { Debug.LogException(error); }
    }
}
