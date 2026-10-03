using System;
using UnityEngine;

internal static class SafeAnimationEvents
{
    internal static void Invoke(Action listeners)
    {
        if (listeners == null) return;
        foreach (Action listener in listeners.GetInvocationList())
            try { listener(); } catch (Exception error) { Debug.LogException(error); }
    }
    internal static void Invoke<T>(Action<T> listeners, T value)
    {
        if (listeners == null) return;
        foreach (Action<T> listener in listeners.GetInvocationList())
            try { listener(value); } catch (Exception error) { Debug.LogException(error); }
    }
    internal static void Invoke<T, U>(Action<T, U> listeners, T first, U second)
    {
        if (listeners == null) return;
        foreach (Action<T, U> listener in listeners.GetInvocationList())
            try { listener(first, second); } catch (Exception error) { Debug.LogException(error); }
    }
}
