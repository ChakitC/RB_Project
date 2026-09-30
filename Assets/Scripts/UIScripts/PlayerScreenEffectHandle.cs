using System;
using UnityEngine;

// Owned presentation advanced by the caller's pause-aware clock, not a HUD coroutine.
public sealed class PlayerScreenEffectHandle : IDisposable
{
    GameObject instance;
    readonly ParticleSystem[] particles;
    readonly Action<float> applyAlpha;
    readonly float duration, opacity, fadeIn, fadeOut;
    float elapsed;

    public bool IsActive => instance != null;

    internal PlayerScreenEffectHandle(GameObject instance, float duration, float opacity,
        float fadeIn, float fadeOut, Action<float> applyAlpha)
    {
        this.instance = instance;
        this.duration = duration;
        this.opacity = opacity;
        this.fadeIn = fadeIn;
        this.fadeOut = fadeOut;
        this.applyAlpha = applyAlpha;
        particles = instance.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var particle in particles)
        {
            // Simulate each system separately so children are never advanced twice.
            particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particle.main;
            main.stopAction = ParticleSystemStopAction.None;
            particle.Simulate(0f, false, true, false);
        }
        ApplyFade();
    }

    public void Advance(float delta)
    {
        if (!IsActive || !float.IsFinite(delta) || delta <= 0f) return;
        float step = Mathf.Min(delta, duration - elapsed);
        elapsed += step;
        if (elapsed >= duration) { Dispose(); return; }
        foreach (var particle in particles)
            if (particle != null) particle.Simulate(step, false, false, false);
        ApplyFade();
    }

    void ApplyFade()
    {
        float alpha = 1f;
        if (fadeIn > 0f) alpha = Mathf.Min(alpha, Mathf.Clamp01(elapsed / fadeIn));
        if (fadeOut > 0f) alpha = Mathf.Min(alpha, Mathf.Clamp01((duration - elapsed) / fadeOut));
        applyAlpha?.Invoke(opacity * Mathf.SmoothStep(0f, 1f, alpha));
    }

    public void Dispose()
    {
        if (instance == null) return;
        var effect = instance;
        instance = null;
        // Hide immediately, even when Unity defers destruction until end of frame.
        effect.SetActive(false);
        if (Application.isPlaying) UnityEngine.Object.Destroy(effect);
        else UnityEngine.Object.DestroyImmediate(effect);
    }
}
