using CartoonFX;
using UnityEngine;

// Configure clones only; asset clear behavior stays intact for non-pooled users.
public sealed class PooledCartoonFxLifecycle : MonoBehaviour, IPooledVfxLifecycle
{
    public void OnVfxRent()
    {
        foreach (var effect in GetComponentsInChildren<CFXR_Effect>(true))
            effect.clearBehavior = CFXR_Effect.ClearBehavior.None;
    }

    // CFXR_Effect resets light, fade and camera-shake state in OnDisable.
    public void OnVfxReturn() { }
}
