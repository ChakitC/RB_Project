using System.Collections.Generic;
using UnityEngine;

public sealed class WeaponRuntimeEffectDispatcher
{
    readonly List<IWeaponRuntimeEffectHandler> handlers = new();

    public void Rebuild(
        CharacteContext ctx,
        Transform fallbackRoot,
        IWeaponRuntimeEffectHandler priorityHandler)
    {
        handlers.Clear();
        Add(priorityHandler);

        Transform ownerRoot = ctx ? ctx.transform : fallbackRoot.root;
        if (!ownerRoot)
            ownerRoot = fallbackRoot;

        var behaviours = ownerRoot.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IWeaponRuntimeEffectHandler handler)
                Add(handler);
        }
    }

    public void NotifyWeaponEquipped(
        CharacteContext ctx,
        Transform fallbackRoot,
        IWeaponRuntimeEffectHandler priorityHandler)
    {
        Rebuild(ctx, fallbackRoot, priorityHandler);

        for (int i = 0; i < handlers.Count; i++)
            handlers[i]?.NotifyWeaponEquipped();
    }

    public void NotifyShotFired(
        CharacteContext ctx,
        Transform fallbackRoot,
        IWeaponRuntimeEffectHandler priorityHandler)
    {
        RebuildIfEmpty(ctx, fallbackRoot, priorityHandler);

        for (int i = 0; i < handlers.Count; i++)
            handlers[i]?.HandleShotFired();
    }

    public void NotifyReloadCompleted(
        CharacteContext ctx,
        Transform fallbackRoot,
        IWeaponRuntimeEffectHandler priorityHandler)
    {
        RebuildIfEmpty(ctx, fallbackRoot, priorityHandler);

        for (int i = 0; i < handlers.Count; i++)
            handlers[i]?.HandleReloadCompleted();
    }

    void RebuildIfEmpty(
        CharacteContext ctx,
        Transform fallbackRoot,
        IWeaponRuntimeEffectHandler priorityHandler)
    {
        if (handlers.Count == 0)
            Rebuild(ctx, fallbackRoot, priorityHandler);
    }

    void Add(IWeaponRuntimeEffectHandler handler)
    {
        if (handler == null || handlers.Contains(handler))
            return;

        handlers.Add(handler);
    }
}
