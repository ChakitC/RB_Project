using UnityEngine;
using DamageNumbersPro;

public class VfxSpawner : MonoBehaviour
{
    static VfxSpawner _instance;
    static bool _isQuitting;

    public static VfxSpawner Instance
    {
        get
        {
            if (_instance != null)
                return _instance;

            if (_isQuitting)
                return null;

            _instance = FindAnyObjectByType<VfxSpawner>();
            if (_instance != null)
                return _instance;

            GameObject spawnerObject = new GameObject(nameof(VfxSpawner));
            _instance = spawnerObject.AddComponent<VfxSpawner>();
            return _instance;
        }
    }

    const float DefaultVfxLifetime = 2f;
    const float LifetimeSafetyBuffer = 0.25f;

    public DamageNumber numberPrefab;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticState()
    {
        _instance = null;
        _isQuitting = false;
        Application.quitting -= HandleApplicationQuitting;
        Application.quitting += HandleApplicationQuitting;
    }

    static void HandleApplicationQuitting()
    {
        _isQuitting = true;
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            _instance.ApplySerializedFallbacksFrom(this);
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnApplicationQuit()
    {
        _isQuitting = true;
    }

    void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    void ApplySerializedFallbacksFrom(VfxSpawner other)
    {
        if (other == null)
            return;

        if (numberPrefab == null && other.numberPrefab != null)
            numberPrefab = other.numberPrefab;
    }

    public GameObject SpawnVfx(GameObject prefab, Vector3 pos, Vector3 normal, float extraLife = 0f ,float scale = 1f)
    {
        Quaternion rot = normal.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(normal.normalized)
            : Quaternion.identity;

        return SpawnVfx(prefab, pos, rot, extraLife, scale);
    }

    public GameObject SpawnVfx(GameObject prefab, Vector3 pos, Quaternion rotation, float extraLife = 0f, float scale = 1f)
    {
        if (prefab == null) return null;

        GameObject vfx = InstantiateVfx(prefab, pos, rotation, null, scale);

        float duration = CalculateLifetimeAndDisableLoops(vfx) + Mathf.Max(0f, extraLife);
        DestroyVfxWithWorldTime(vfx, duration);
        return vfx;
    }

    public GameObject SpawnLoopingVfx(GameObject prefab, Vector3 pos, Quaternion rotation, Transform parent = null, float scale = 1f)
    {
        if (prefab == null) return null;

        GameObject vfx = InstantiateVfx(prefab, pos, rotation, parent, scale);
        EnsureWorldTimeScaler(vfx);
        return vfx;
    }

    public void StopLoopingVfx(GameObject vfx, bool allowParticlesToFinish = true, float extraLife = 0f)
    {
        if (vfx == null) return;

        if (!allowParticlesToFinish)
        {
            Destroy(vfx);
            return;
        }

        float duration = CalculateLifetimeAndDisableLoops(vfx) + Mathf.Max(0f, extraLife);
        StopParticleEmission(vfx);
        DestroyVfxWithWorldTime(vfx, duration);
    }

    public void SpawnDamageNumber(Vector3 position , float number)
    {
        if (numberPrefab == null) return;
        DamageNumber dn = numberPrefab.Spawn(position, number);
    }

    GameObject InstantiateVfx(GameObject prefab, Vector3 pos, Quaternion rotation, Transform parent, float scale)
    {
        GameObject vfx = parent
            ? Instantiate(prefab, pos, rotation, parent)
            : Instantiate(prefab, pos, rotation);

        vfx.transform.localScale *= Mathf.Max(0f, scale);
        return vfx;
    }

    void DestroyVfxWithWorldTime(GameObject vfx, float duration)
    {
        if (vfx == null)
            return;

        EnsureWorldTimeScaler(vfx).DestroyAfterWorldSeconds(duration);
    }

    WorldTimeScaledVfx EnsureWorldTimeScaler(GameObject vfx)
    {
        WorldTimeScaledVfx scaler = vfx.GetComponent<WorldTimeScaledVfx>();
        if (scaler == null)
            scaler = vfx.AddComponent<WorldTimeScaledVfx>();

        return scaler;
    }

    void StopParticleEmission(GameObject vfx)
    {
        var particleSystems = vfx.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            var ps = particleSystems[i];
            if (ps == null)
                continue;

            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    float CalculateLifetimeAndDisableLoops(GameObject vfx)
    {
        float longestLifetime = 0f;

        var particleSystems = vfx.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            var ps = particleSystems[i];
            if (ps == null)
                continue;

            var main = ps.main;
            if (main.loop)
                main.loop = false;

            longestLifetime = Mathf.Max(longestLifetime, EstimateParticleLifetime(ps));
        }

        var trailRenderers = vfx.GetComponentsInChildren<TrailRenderer>(true);
        for (int i = 0; i < trailRenderers.Length; i++)
        {
            var trail = trailRenderers[i];
            if (trail == null)
                continue;

            longestLifetime = Mathf.Max(longestLifetime, trail.time);
        }

        if (longestLifetime <= 0f)
            longestLifetime = DefaultVfxLifetime;

        return longestLifetime + LifetimeSafetyBuffer;
    }

    float EstimateParticleLifetime(ParticleSystem ps)
    {
        if (ps == null)
            return 0f;

        var main = ps.main;
        float simulationSpeed = Mathf.Max(0.0001f, main.simulationSpeed);
        float startDelay = GetCurveMax(main.startDelay);
        float startLifetime = GetCurveMax(main.startLifetime);
        float duration = (startDelay + main.duration + startLifetime) / simulationSpeed;

        if (ps.trails.enabled)
            duration += Mathf.Max(0f, ps.trails.lifetimeMultiplier * startLifetime);

        return duration;
    }

    float GetCurveMax(ParticleSystem.MinMaxCurve curve)
    {
        return curve.mode switch
        {
            ParticleSystemCurveMode.Constant => curve.constant,
            ParticleSystemCurveMode.TwoConstants => curve.constantMax,
            ParticleSystemCurveMode.Curve => curve.curve != null ? curve.curve.keys[curve.curve.length - 1].value * curve.curveMultiplier : curve.curveMultiplier,
            ParticleSystemCurveMode.TwoCurves => Mathf.Max(
                EvaluateCurveMax(curve.curveMin, curve.curveMultiplier),
                EvaluateCurveMax(curve.curveMax, curve.curveMultiplier)),
            _ => curve.constantMax
        };
    }

    float EvaluateCurveMax(AnimationCurve curve, float multiplier)
    {
        if (curve == null || curve.length == 0)
            return 0f;

        float max = 0f;
        for (int i = 0; i < curve.length; i++)
            max = Mathf.Max(max, curve.keys[i].value);

        return max * multiplier;
    }
}
