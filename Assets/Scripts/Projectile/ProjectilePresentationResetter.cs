using UnityEngine;

/// <summary>
/// Project-owned replacement for the presentation half of vendor mover scripts such as
/// <c>HS_ProjectileMover</c>. It restarts particle systems and restores lights when a pooled
/// projectile is switched on, and clears them when it is switched off.
///
/// It deliberately owns nothing else. It never writes the Rigidbody, never times a lifetime,
/// never toggles the root GameObject, and never destroys or returns the instance — those belong
/// to <see cref="Projectile"/> alone, which is what keeps one movement/lifetime owner per prefab.
///
/// Hooking OnEnable is safe under the atomic spawn lifecycle: the pool only activates a projectile
/// from <see cref="Projectile.CompleteSpawn"/>, after context, stats, and layer are in place.
/// </summary>
[DisallowMultipleComponent]
public sealed class ProjectilePresentationResetter : MonoBehaviour
{
    [Tooltip("Particle systems restarted on spawn and cleared on despawn. Auto-filled from children when empty.")]
    [SerializeField] ParticleSystem[] particleSystems;

    [Tooltip("Lights restored on spawn and switched off on despawn. Auto-filled from children when empty.")]
    [SerializeField] Light[] lights;

    bool[] _authoredLightEnabled;
    bool _collected;

    void Awake() => Collect();

    void Reset()
    {
        particleSystems = GetComponentsInChildren<ParticleSystem>(true);
        lights = GetComponentsInChildren<Light>(true);
    }

    void Collect()
    {
        if (_collected) return;
        _collected = true;

        if (particleSystems == null || particleSystems.Length == 0)
            particleSystems = GetComponentsInChildren<ParticleSystem>(true);

        if (lights == null || lights.Length == 0)
            lights = GetComponentsInChildren<Light>(true);

        _authoredLightEnabled = new bool[lights.Length];
        for (int i = 0; i < lights.Length; i++)
            _authoredLightEnabled[i] = lights[i] != null && lights[i].enabled;
    }

    void OnEnable()
    {
        Collect();

        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem ps = particleSystems[i];
            if (ps == null) continue;

            ps.Clear(true);
            ps.Play(true);
        }

        for (int i = 0; i < lights.Length; i++)
        {
            Light light = lights[i];
            if (light == null) continue;

            light.enabled = _authoredLightEnabled[i];
        }
    }

    void OnDisable()
    {
        if (!_collected) return;

        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem ps = particleSystems[i];
            if (ps == null) continue;

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Clear(true);
        }

        for (int i = 0; i < lights.Length; i++)
        {
            Light light = lights[i];
            if (light == null) continue;

            light.enabled = false;
        }
    }
}
