public interface IWeaponRuntimeEffectHandler
{
    void NotifyWeaponEquipped();
    void HandleShotFired();
    void HandleReloadCompleted();
}
