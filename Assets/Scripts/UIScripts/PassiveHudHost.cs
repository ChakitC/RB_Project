using System.Collections.Generic;
using UnityEngine;

/// <summary>Owns optional grant views; never subscribes to combat or polls gameplay.</summary>
[DisallowMultipleComponent]
public sealed class PassiveHudHost : MonoBehaviour
{
    readonly Dictionary<PassiveRuntime, PassiveHudView> views = new();
    readonly List<PassiveRuntime> grants = new();
    readonly List<PassiveRuntime> removed = new();
    CharacteContext owner;
    PassiveController controller;
    public int ViewCount => views.Count;
    public PassiveHudView GetView(PassiveRuntime grant) => views.TryGetValue(grant, out var view) ? view : null;

    public void Bind(CharacteContext context)
    {
        Detach(); owner = context;
        Attach();
    }
    void OnEnable() => Attach();
    void OnDisable() => Detach();
    void Attach()
    {
        if (!isActiveAndEnabled || owner == null) return;
        owner.ResolveReferences();
        controller = owner.PassiveController;
        if (controller == null) return;
        controller.RuntimeLoadoutChanged -= Reconcile;
        controller.RuntimeLoadoutChanged += Reconcile;
        Reconcile();
    }
    void Detach()
    {
        if (controller != null) controller.RuntimeLoadoutChanged -= Reconcile;
        controller = null;
        foreach (var view in views.Values) Release(view);
        views.Clear(); grants.Clear(); removed.Clear();
    }
    void Reconcile()
    {
        grants.Clear(); removed.Clear();
        if (controller != null && controller.isActiveAndEnabled) controller.AppendRuntimes(grants);
        foreach (var pair in views)
            if (!grants.Contains(pair.Key) || pair.Key.Grant.Definition.hudPrefab == null ||
                pair.Value == null || pair.Value.SourcePrefab != pair.Key.Grant.Definition.hudPrefab)
                removed.Add(pair.Key);
        foreach (var grant in removed) { Release(views[grant]); views.Remove(grant); }
        foreach (var grant in grants)
        {
            var prefab = grant.Grant.Definition.hudPrefab;
            if (!grant.IsAlive || prefab == null) continue;
            if (!views.TryGetValue(grant, out var view))
            {
                view = Instantiate(prefab, transform, false);
                view.name = prefab.name;
                view.SourcePrefab = prefab;
                views.Add(grant, view);
                view.gameObject.SetActive(true);
                view.Bind(grant);
            }
            else view.RefreshView();
        }
    }
    static void Release(PassiveHudView view)
    {
        if (view == null) return;
        view.Bind(null);
        view.gameObject.SetActive(false);
        if (Application.isPlaying) Destroy(view.gameObject); else DestroyImmediate(view.gameObject);
    }
}
