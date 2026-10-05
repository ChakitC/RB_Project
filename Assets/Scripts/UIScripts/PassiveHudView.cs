using UnityEngine;

/// <summary>A presentation instance bound to exactly one installed grant.</summary>
public abstract class PassiveHudView : MonoBehaviour
{
    public PassiveRuntime Runtime { get; private set; }
    public PassiveHudView SourcePrefab { get; internal set; }
    public bool HasLiveRuntime => Runtime != null && Runtime.IsAlive && Runtime.Controller.ContainsRuntime(Runtime);
    public void Bind(PassiveRuntime runtime)
    {
        Unsubscribe();
        Runtime = runtime;
        if (isActiveAndEnabled && HasLiveRuntime) Subscribe();
        RefreshView();
    }
    protected virtual void OnEnable()
    { if (HasLiveRuntime) { Subscribe(); RefreshView(); } }
    protected virtual void OnDisable() => Unsubscribe();
    protected abstract void Subscribe();
    protected abstract void Unsubscribe();
    public abstract void RefreshView();
}
