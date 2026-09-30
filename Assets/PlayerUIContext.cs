using UnityEngine;

public interface IPlayerFullscreenEffectController
{
    bool PlayHeal(float amount, float currentHealth, float maximumHealth);
    bool PlayPerfectDodge(Vector3 worldDashDirection, float slowDuration, float slowScale);
    bool PlayScreenWind(Vector3 worldWindDirection, float duration);
    PlayerScreenEffectHandle BeginScreenWind(Vector3 worldWindDirection, float duration);
}

public class PlayerUIContext : MonoBehaviour
{
    public GameObject inventoryUI;
    public ActiveSkillScreenController activeSkillScreen;
    public MonoBehaviour fullscreenEffects;

    public IPlayerFullscreenEffectController FullscreenEffects =>
        fullscreenEffects as IPlayerFullscreenEffectController;
}
