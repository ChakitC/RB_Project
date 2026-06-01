using UnityEngine;

public interface IPlayerFullscreenEffectController
{
    bool PlayHeal(float amount, float currentHealth, float maximumHealth);
    bool PlayPerfectDodge(Vector3 worldDashDirection, float slowDuration, float slowScale);
}

public class PlayerUIContext : MonoBehaviour
{
    public GameObject inventoryUI;
    public GameObject passiveTree;
    public MonoBehaviour fullscreenEffects;

    public IPlayerFullscreenEffectController FullscreenEffects =>
        fullscreenEffects as IPlayerFullscreenEffectController;
}
