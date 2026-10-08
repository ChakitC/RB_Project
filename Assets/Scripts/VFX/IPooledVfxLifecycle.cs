// Implement on VFX scripts with custom state. Both callbacks run while inactive.
// Restore owned material/property state and cancel owned work here, not in callers.
public interface IPooledVfxLifecycle
{
    void OnVfxRent();
    void OnVfxReturn();
}
