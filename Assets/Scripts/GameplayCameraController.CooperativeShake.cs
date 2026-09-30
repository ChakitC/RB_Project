using UnityEngine;

public partial class GameplayCameraController
{
    bool cooperativeShakeStarted, cooperativeShakeActive;
    bool cooperativeSuccessShakeStarted;
    float cooperativeShakeTime;
    float cooperativeImpactDegrees, cooperativeImpactSeconds, cooperativeImpactFrequency;
    float cooperativeStruggleDegrees, cooperativeStruggleFrequency;
    float cooperativeSuccessDegrees, cooperativeSuccessSeconds, cooperativeSuccessFrequency;

    void ConfigureCooperativeShake(DefensiveBlockActorProfile settings)
    {
        cooperativeImpactDegrees = ShakeValue(settings != null ? settings.cooperativeImpactShakeDegrees : 1.8f);
        cooperativeImpactSeconds = ShakeValue(settings != null ? settings.cooperativeImpactShakeSeconds : .22f);
        cooperativeImpactFrequency = ShakeValue(settings != null ? settings.cooperativeImpactShakeFrequency : 22f);
        cooperativeStruggleDegrees = ShakeValue(settings != null ? settings.cooperativeStruggleShakeDegrees : .12f);
        cooperativeStruggleFrequency = ShakeValue(settings != null ? settings.cooperativeStruggleShakeFrequency : 9f);
        cooperativeSuccessDegrees = ShakeValue(settings != null ? settings.cooperativeSuccessShakeDegrees : 1.8f);
        cooperativeSuccessSeconds = ShakeValue(settings != null ? settings.cooperativeSuccessShakeSeconds : .2f);
        cooperativeSuccessFrequency = ShakeValue(settings != null ? settings.cooperativeSuccessShakeFrequency : 20f);
    }

    static float ShakeValue(float value) => float.IsFinite(value) ? Mathf.Max(0f, value) : 0f;

    public void BeginCooperativeBlockStruggle(object owner)
    {
        if (!ReferenceEquals(owner, blockShotOwner) || owner == null || cooperativeShotAlly == null || cooperativeShakeStarted) return;
        cooperativeShakeStarted = cooperativeShakeActive = true;
        cooperativeShakeTime = 0f;
    }

    public void AdvanceCooperativeBlockShake(object owner, float delta)
    {
        if (owner != null && ReferenceEquals(owner, blockShotOwner) && cooperativeShakeActive)
            cooperativeShakeTime += ShakeValue(delta);
    }

    public void PlayCooperativeBlockSuccess(object owner)
    {
        if (owner == null || !ReferenceEquals(owner, blockShotOwner) || cooperativeShotAlly == null ||
            !cooperativeShakeStarted || cooperativeSuccessShakeStarted) return;
        cooperativeSuccessShakeStarted = cooperativeShakeActive = true;
        cooperativeShakeTime = 0f;
    }

    public void EndCooperativeBlockStruggle(object owner)
    {
        if (owner != null && ReferenceEquals(owner, blockShotOwner)) cooperativeShakeActive = false;
    }

    void ResetCooperativeShake()
    {
        cooperativeShakeStarted = cooperativeShakeActive = false;
        cooperativeSuccessShakeStarted = false;
        cooperativeShakeTime = 0f;
    }

    Vector3 SampleCooperativeShake()
    {
        if (!cooperativeShakeActive) return Vector3.zero;
        float time = cooperativeShakeTime;
        if (cooperativeSuccessShakeStarted)
        {
            float decay = cooperativeSuccessSeconds > 0f ? Mathf.Clamp01(1f - time / cooperativeSuccessSeconds) : 0f;
            float phase = time * cooperativeSuccessFrequency * Mathf.PI * 2f;
            return new Vector3(-Mathf.Cos(phase), Mathf.Sin(phase * .83f) * .45f,
                Mathf.Sin(phase * 1.13f) * .3f) * (cooperativeSuccessDegrees * decay * decay);
        }
        float envelope = cooperativeImpactSeconds > 0f ? Mathf.Clamp01(1f - time / cooperativeImpactSeconds) : 0f;
        float impactPhase = time * cooperativeImpactFrequency * Mathf.PI * 2f;
        Vector3 impact = new Vector3(-Mathf.Cos(impactPhase), Mathf.Sin(impactPhase * .83f) * .45f,
            Mathf.Sin(impactPhase * 1.13f) * .3f) * (cooperativeImpactDegrees * envelope * envelope);
        float strugglePhase = time * cooperativeStruggleFrequency * Mathf.PI * 2f;
        Vector3 struggle = new Vector3(Mathf.Sin(strugglePhase), Mathf.Sin(strugglePhase * 1.37f) * .7f,
            Mathf.Sin(strugglePhase * .73f) * .35f) * (cooperativeStruggleDegrees * Mathf.Clamp01(time / .12f));
        // Session time excludes pause and ignores world slow. Applying a local rotation
        // to the clean shot every frame avoids drift and does not move through walls.
        return impact + struggle;
    }
}
