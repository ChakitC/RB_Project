#if UNITY_EDITOR
using UnityEngine;

// Test-only component for ordering and custom-state reset on real pooled objects.
public sealed class VfxLifecycleProbe : MonoBehaviour, IPooledVfxLifecycle
{
    public int Value;
    public int RentCount;
    public int ReturnCount;
    public int EnableCount;
    public Vector3 EnabledPosition;
    public bool RentWasInactive;

    public void OnVfxRent()
    {
        RentWasInactive = !gameObject.activeInHierarchy;
        Value = 0;
        RentCount++;
    }

    public void OnVfxReturn() { Value = 0; ReturnCount++; }
    void OnEnable() { EnableCount++; EnabledPosition = transform.position; }
}
#endif
