using UnityEngine;

public partial class GameplayCameraController
{
    bool normalBlockShakeStarted;
    float normalBlockShakeTime;
    float normalBlockShakeDegrees, normalBlockShakeSeconds, normalBlockShakeFrequency;

    void ConfigureNormalBlockShake(DefensiveBlockActorProfile settings)
    {
        ResetNormalBlockShake();
        normalBlockShakeDegrees = ShakeValue(settings != null ? settings.normalImpactShakeDegrees : 1.2f);
        normalBlockShakeSeconds = ShakeValue(settings != null ? settings.normalImpactShakeSeconds : .15f);
        normalBlockShakeFrequency = ShakeValue(settings != null ? settings.normalImpactShakeFrequency : 20f);
    }

    public void PlayNormalBlockImpact(object owner)
    {
        if (owner == null || !ReferenceEquals(owner, blockShotOwner) ||
            cooperativeShotAlly != null || normalBlockShakeStarted) return;
        normalBlockShakeStarted = true;
        normalBlockShakeTime = 0f;
    }

    void AdvanceNormalBlockShake(float delta, bool paused)
    {
        if (normalBlockShakeStarted && !paused)
            normalBlockShakeTime = Mathf.Min(normalBlockShakeSeconds, normalBlockShakeTime + ShakeValue(delta));
    }

    void ResetNormalBlockShake()
    {
        normalBlockShakeStarted = false;
        normalBlockShakeTime = 0f;
    }

    Vector3 SampleNormalBlockShake()
    {
        if (!normalBlockShakeStarted || normalBlockShakeSeconds <= 0f) return Vector3.zero;
        float envelope = Mathf.Clamp01(1f - normalBlockShakeTime / normalBlockShakeSeconds);
        float phase = normalBlockShakeTime * normalBlockShakeFrequency * Mathf.PI * 2f;
        // Apply to the clean camera state each frame; never accumulate rotation.
        return new Vector3(-Mathf.Cos(phase), Mathf.Sin(phase * .83f) * .45f,
            Mathf.Sin(phase * 1.13f) * .3f) * (normalBlockShakeDegrees * envelope * envelope);
    }
}
