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
            {
                _instance.EnsurePool();
                return _instance;
            }

            if (_isQuitting)
                return null;

            _instance = FindAnyObjectByType<VfxSpawner>();
            if (_instance != null)
            {
                _instance.EnsurePool();
                return _instance;
            }

            GameObject spawnerObject = new GameObject(nameof(VfxSpawner));
            _instance = spawnerObject.AddComponent<VfxSpawner>();
            _instance.EnsurePool();
            return _instance;
        }
    }

    const float LifetimeSafetyBuffer = 0.25f;

    public DamageNumber numberPrefab;

    [Header("Debug")]
    [Tooltip("ติ๊กออกเพื่อปิด VFX และเลขดาเมจทั้งหมด (gameplay ทำงานปกติ) สำหรับดีบัค")]
    [SerializeField] bool spawnVfx = true;

    [Header("Damage Numbers")]
    [Tooltip("Show damage numbers when the damaged target is the player or a companion.")]
    [SerializeField] bool showPlayerAndCompanionDamageNumbers;

    public bool SpawnVfxEnabled { get => spawnVfx; set => spawnVfx = value; }
    public bool ShowPlayerAndCompanionDamageNumbers
    {
        get => showPlayerAndCompanionDamageNumbers;
        set => showPlayerAndCompanionDamageNumbers = value;
    }

    VfxPool _pool;

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
        EnsurePool();
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

    public GameObject SpawnVfx(
        GameObject prefab,
        Vector3 pos,
        Vector3 normal,
        float extraLife = 0f,
        float scale = 1f,
        float minimumLifetime = 0f,
        Transform parent = null,
        VfxLifetimeMode lifetimeMode = VfxLifetimeMode.Auto,
        float explicitLifetime = 0f,
        bool useWorldTimeScale = true,
        System.Action<GameObject> configure = null)
    {
        Quaternion rot = normal.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(normal.normalized)
            : Quaternion.identity;

        return SpawnVfx(prefab, pos, rot, extraLife, scale, minimumLifetime, parent,
            lifetimeMode, explicitLifetime, useWorldTimeScale, configure);
    }

    public GameObject SpawnVfx(
        GameObject prefab,
        Vector3 pos,
        Quaternion rotation,
        float extraLife = 0f,
        float scale = 1f,
        float minimumLifetime = 0f,
        Transform parent = null,
        VfxLifetimeMode lifetimeMode = VfxLifetimeMode.Auto,
        float explicitLifetime = 0f,
        bool useWorldTimeScale = true,
        System.Action<GameObject> configure = null)
    {
        if (!spawnVfx) return null;
        if (prefab == null) return null;

        return EnsurePool().Get(prefab, pos, rotation, parent, scale, vfx =>
        {
            var handle = vfx.GetComponent<PooledVfxHandle>();
            float estimate = CalculateLifetime(handle.ParticleSystems, handle.TrailRenderers,
                disableLoops: lifetimeMode == VfxLifetimeMode.Auto);
            float duration = CalculateOneShotLifetime(estimate, minimumLifetime, extraLife, lifetimeMode, explicitLifetime);
            var scaler = EnsureWorldTimeScaler(vfx);
            scaler.SetUseWorldTimeScale(useWorldTimeScale);
            scaler.DestroyAfterWorldSeconds(duration);
            configure?.Invoke(vfx);
        });
    }

    public GameObject SpawnLoopingVfx(GameObject prefab, Vector3 pos, Quaternion rotation,
        Transform parent = null, float scale = 1f, bool useWorldTimeScale = true,
        System.Action<GameObject> configure = null, bool followWithoutParent = false)
    {
        if (!spawnVfx || prefab == null) return null;
        return EnsurePool().Get(prefab, pos, rotation, followWithoutParent ? null : parent, scale, vfx =>
        {
            EnsureWorldTimeScaler(vfx).SetUseWorldTimeScale(useWorldTimeScale);
            if (followWithoutParent && parent != null)
            {
                vfx.GetComponent<PooledVfxHandle>().SetOwnerScene(parent.gameObject.scene.handle);
                vfx.transform.localScale = Vector3.Scale(vfx.transform.localScale, parent.lossyScale);
                var follower = vfx.GetComponent<AnimationVfxFollowAnchor>();
                if (follower == null) follower = vfx.AddComponent<AnimationVfxFollowAnchor>();
                follower.Configure(parent, parent.InverseTransformPoint(pos),
                    Quaternion.Inverse(parent.rotation) * rotation, followScale: true);
            }
            configure?.Invoke(vfx);
        });
    }

    public static float CalculateOneShotLifetime(float estimate, float minimumLifetime, float extraLife,
        VfxLifetimeMode mode = VfxLifetimeMode.Auto, float explicitLifetime = 0f)
    {
        if (mode == VfxLifetimeMode.Explicit)
            return float.IsFinite(explicitLifetime) ? Mathf.Max(0f, explicitLifetime) : 0f;
        return Mathf.Max(estimate, Mathf.Max(0f, minimumLifetime)) + Mathf.Max(0f, extraLife);
    }

    public void StopLoopingVfx(GameObject vfx, bool allowParticlesToFinish = true, float extraLife = 0f)
    {
        if (vfx == null) return;

        PooledVfxHandle handle = vfx.GetComponent<PooledVfxHandle>();
        if (handle != null && !handle.BeginStop()) return;

        if (!allowParticlesToFinish)
        {
            if (handle != null)
                handle.ReturnToPool();
            else
                Destroy(vfx);
            return;
        }

        if (handle != null) handle.DetachForTail();
        else vfx.transform.SetParent(null, true);

        var scaler = EnsureWorldTimeScaler(vfx);
        // Lifetime is measured in the selected clock already; don't count world slow twice.
        scaler.RestoreBaseSpeeds();
        float duration;
        if (handle != null)
        {
            duration = CalculateLifetimeAndDisableLoops(handle.ParticleSystems, handle.TrailRenderers);
            StopParticleEmission(handle.ParticleSystems);
        }
        else
        {
            duration = CalculateLifetimeAndDisableLoops(vfx);
            StopParticleEmission(vfx);
        }
        duration += Mathf.Max(0f, extraLife);
        scaler.RestoreClockScale();
        scaler.DestroyAfterWorldSeconds(duration);
    }

    public void SpawnDamageNumber(Vector3 position, float number, IDamageable target)
    {
        if (!spawnVfx) return;
        if (numberPrefab == null) return;
        if (!showPlayerAndCompanionDamageNumbers && IsPlayerOrCompanion(target)) return;

        numberPrefab.Spawn(position, number);
    }

    static bool IsPlayerOrCompanion(IDamageable target)
    {
        if (target is not Component targetComponent)
            return false;

        CharacteContext targetContext = targetComponent.GetComponentInParent<CharacteContext>();
        if (targetContext == null)
            return false;

        return targetContext.TargetIdentity == AITargetIdentity.Player ||
               targetContext.TargetIdentity == AITargetIdentity.Companion;
    }

    VfxPool EnsurePool()
    {
        if (_pool != null)
            return _pool;

        _pool = GetComponent<VfxPool>();
        if (_pool == null)
            _pool = gameObject.AddComponent<VfxPool>();

        return _pool;
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

    void StopParticleEmission(ParticleSystem[] particleSystems)
    {
        for (int i = 0; i < particleSystems.Length; i++)
        {
            var ps = particleSystems[i];
            if (ps == null)
                continue;

            ps.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    float CalculateLifetimeAndDisableLoops(GameObject vfx)
    {
        return CalculateLifetime(
            vfx.GetComponentsInChildren<ParticleSystem>(true),
            vfx.GetComponentsInChildren<TrailRenderer>(true),
            disableLoops: true);
    }

    float CalculateLifetimeAndDisableLoops(ParticleSystem[] particleSystems, TrailRenderer[] trailRenderers)
    {
        return CalculateLifetime(particleSystems, trailRenderers, disableLoops: true);
    }

    internal static float EstimateLifetime(GameObject vfx)
    {
        if (vfx == null)
            return 0f;

        return CalculateLifetime(
            vfx.GetComponentsInChildren<ParticleSystem>(true),
            vfx.GetComponentsInChildren<TrailRenderer>(true),
            disableLoops: false);
    }

    static float CalculateLifetime(
        ParticleSystem[] particleSystems,
        TrailRenderer[] trailRenderers,
        bool disableLoops)
    {
        float longestLifetime = 0f;
        bool hasLifetimeSource = false;

        for (int i = 0; i < particleSystems.Length; i++)
        {
            var ps = particleSystems[i];
            if (ps == null)
                continue;

            hasLifetimeSource = true;
            var main = ps.main;
            if (disableLoops && main.loop)
                main.loop = false;

            longestLifetime = Mathf.Max(longestLifetime, EstimateParticleLifetime(ps));
        }

        for (int i = 0; i < trailRenderers.Length; i++)
        {
            var trail = trailRenderers[i];
            if (trail == null)
                continue;

            hasLifetimeSource = true;
            longestLifetime = Mathf.Max(longestLifetime, trail.time);
        }

        return hasLifetimeSource ? longestLifetime + LifetimeSafetyBuffer : 0f;
    }

    static float EstimateParticleLifetime(ParticleSystem ps)
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

    static float GetCurveMax(ParticleSystem.MinMaxCurve curve)
    {
        return curve.mode switch
        {
            ParticleSystemCurveMode.Constant => curve.constant,
            ParticleSystemCurveMode.TwoConstants => curve.constantMax,
            ParticleSystemCurveMode.Curve => EvaluateCurveMax(curve.curve, curve.curveMultiplier),
            ParticleSystemCurveMode.TwoCurves => Mathf.Max(
                EvaluateCurveMax(curve.curveMin, curve.curveMultiplier),
                EvaluateCurveMax(curve.curveMax, curve.curveMultiplier)),
            _ => curve.constantMax
        };
    }

    static float EvaluateCurveMax(AnimationCurve curve, float multiplier)
    {
        if (curve == null || curve.length == 0)
            return 0f;

        float max = 0f;
        for (int i = 0; i < curve.length; i++)
            max = Mathf.Max(max, curve.keys[i].value);

        return max * multiplier;
    }
}
