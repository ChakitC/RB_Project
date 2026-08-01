using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerFullscreenEffectController : MonoBehaviour, IPlayerFullscreenEffectController
{
    const string DefaultHealScreenEffectPath = "Assets/Hovl Studio/Fullscreen effects/Prefabs/Screen healing.prefab";
    const string DefaultPerfectDodgeScreenEffectPath = "Assets/Hovl Studio/Fullscreen effects/Prefabs/Screen wind straight.prefab";

    [Header("Refs")]
    [SerializeField] private PlayerContext playerContext;
    [SerializeField] private PlayerUIContext playerUIContext;
    [SerializeField] private UIManager uiManager;

    [Header("Screen Effect Prefabs")]
    [SerializeField] private GameObject healScreenEffectPrefab;
    [SerializeField] private GameObject perfectDodgeScreenEffectPrefab;

    [Header("Screen Effect Runtime")]
    [SerializeField] private Camera targetCamera;
    [SerializeField, Min(0.001f)] private float screenEffectCameraDistance = 0.004f;
    [SerializeField, Min(0.01f)] private float screenEffectSizeMultiplier = 1.15f;
    [SerializeField, Min(0.01f)] private float minimumScreenEffectLifetime = 1.1f;
    [SerializeField, Min(0f)] private float healEffectCooldownSeconds = 0.08f;
    [SerializeField, Min(0f)] private float screenEffectOpacityMultiplier = 1.35f;
    [SerializeField] private bool useScreenEffectFade = true;
    [SerializeField, Min(0f)] private float fadeInDuration = 0.08f;
    [SerializeField, Min(0f)] private float fadeOutDuration = 0.25f;
    [SerializeField] private bool useUnscaledEffectTime = true;

    float _nextHealEffectTime;

#if UNITY_EDITOR
    void Reset()
    {
        ResolveLocalReferences();
        AssignDefaultScreenEffectPrefabsInEditor();
    }

    void OnValidate()
    {
        ResolveLocalReferences();
        AssignDefaultScreenEffectPrefabsInEditor();
    }
#endif

    void Awake()
    {
        ResolveReferences();
    }

    public void BindContext(PlayerContext context, PlayerUIContext uiContext)
    {
        playerContext = context;
        playerUIContext = uiContext;
        ResolveLocalReferences();

        if (playerContext != null && playerUIContext != null)
            playerContext.playerUIContext = playerUIContext;

        if (targetCamera == null && isActiveAndEnabled)
            targetCamera = Camera.main;
    }

    void ResolveReferences()
    {
        ResolveLocalReferences();

        if (playerContext == null)
            playerContext = GetComponentInParent<PlayerContext>();

        if (playerContext == null)
            playerContext = FindAnyObjectByType<PlayerContext>(FindObjectsInactive.Include);

        if (playerContext != null)
        {
            playerContext.ResolveReferences();
            if (playerContext.playerUIContext == null && playerUIContext != null)
                playerContext.playerUIContext = playerUIContext;
        }

        if (targetCamera == null)
            targetCamera = Camera.main;
    }

    void ResolveLocalReferences()
    {
        if (uiManager == null)
            uiManager = GetComponentInParent<UIManager>();

        if (playerUIContext == null)
            TryGetComponent(out playerUIContext);

        if (playerUIContext != null && playerUIContext.fullscreenEffects == null)
            playerUIContext.fullscreenEffects = this;
    }

    public bool PlayHeal(float amount, float currentHealth, float maximumHealth)
    {
        if (Application.isPlaying && Time.unscaledTime < _nextHealEffectTime)
            return true;

        _nextHealEffectTime = Time.unscaledTime + healEffectCooldownSeconds;
        GameObject prefab = ResolveScreenEffectPrefab(healScreenEffectPrefab, DefaultHealScreenEffectPath);
        return PlayScreenEffect(prefab, minimumScreenEffectLifetime, 0f);
    }

    public bool PlayPerfectDodge(Vector3 worldDashDirection, float slowDuration, float slowScale)
    {
        GameObject prefab = ResolveScreenEffectPrefab(perfectDodgeScreenEffectPrefab, DefaultPerfectDodgeScreenEffectPath);
        float lifetime = Mathf.Max(minimumScreenEffectLifetime, slowDuration);
        float rotationZ = ResolveScreenDirectionAngle(worldDashDirection);
        return PlayScreenEffect(prefab, lifetime, rotationZ);
    }

    bool PlayScreenEffect(GameObject prefab, float minimumLifetime, float localRotationZ)
    {
        if (prefab == null)
            return false;

        Camera effectCamera = ResolveEffectCamera();
        if (effectCamera == null)
            return false;

        GameObject instance = Instantiate(prefab);
        instance.name = $"{prefab.name} (Runtime)";

#if UNITY_EDITOR
        if (!Application.isPlaying)
            instance.hideFlags = HideFlags.DontSave;
#endif

        ConfigureScreenEffectInstance(instance, effectCamera, localRotationZ);
        float lifetime = ResolveEffectLifetime(instance, minimumLifetime);
        ScreenEffectFadeState fadeState = useScreenEffectFade
            ? new ScreenEffectFadeState(instance)
            : null;

        fadeState?.Apply(ResolveScreenEffectVisualAlpha(0f, lifetime));
        PlayParticleSystems(instance);

        if (Application.isPlaying)
        {
            StartCoroutine(FadeAndDestroyScreenEffectAfterRealtime(instance, lifetime, fadeState));
        }
#if UNITY_EDITOR
        else
        {
            FadeAndDestroyScreenEffectAfterEditorDelay(instance, lifetime, fadeState);
        }
#endif

        return true;
    }

    Camera ResolveEffectCamera()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        return targetCamera;
    }

    void ConfigureScreenEffectInstance(GameObject instance, Camera effectCamera, float localRotationZ)
    {
        if (instance == null || effectCamera == null)
            return;

        Transform instanceTransform = instance.transform;
        instanceTransform.SetParent(effectCamera.transform, false);
        float safeDistance = ResolveSafeScreenEffectDistance(effectCamera);
        instanceTransform.localPosition = Vector3.forward * safeDistance;
        instanceTransform.localRotation = Quaternion.Euler(0f, 0f, localRotationZ);
        instanceTransform.localScale = Vector3.one;

        Vector2 screenSize = ResolveScreenEffectWorldSize(effectCamera, safeDistance) * screenEffectSizeMultiplier;

        var screenEffects = instance.GetComponentsInChildren<Hovl.HS_ScreenEffect>(true);
        for (int i = 0; i < screenEffects.Length; i++)
        {
            Hovl.HS_ScreenEffect screenEffect = screenEffects[i];
            screenEffect.sourceCamera = effectCamera;
            screenEffect.fallbackDistance = safeDistance;
            screenEffect.snapOnStart = false;
            screenEffect.parentToCameraOnStart = false;
            screenEffect.enabled = false;
        }

        ApplyScreenEffectWorldSize(instance, screenSize);

        if (!useUnscaledEffectTime)
            return;

        ParticleSystem[] particleSystems = instance.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem.MainModule main = particleSystems[i].main;
            main.useUnscaledTime = true;
        }
    }

    float ResolveSafeScreenEffectDistance(Camera effectCamera)
    {
        if (effectCamera == null)
            return screenEffectCameraDistance;

        return Mathf.Max(screenEffectCameraDistance, effectCamera.nearClipPlane + 0.001f);
    }

    static Vector2 ResolveScreenEffectWorldSize(Camera effectCamera, float distance)
    {
        if (effectCamera == null)
            return Vector2.one;

        float height;
        if (effectCamera.orthographic)
        {
            height = 2f * effectCamera.orthographicSize;
        }
        else
        {
            float fovRad = effectCamera.fieldOfView * Mathf.Deg2Rad;
            height = 2f * distance * Mathf.Tan(fovRad * 0.5f);
        }

        return new Vector2(height * effectCamera.aspect, height);
    }

    static void ApplyScreenEffectWorldSize(GameObject instance, Vector2 screenSize)
    {
        ParticleSystem[] particleSystems = instance.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];
            ParticleSystem.MainModule main = particleSystem.main;
            main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(screenSize.x);
            main.startSizeY = new ParticleSystem.MinMaxCurve(screenSize.y);
            main.startSizeZ = new ParticleSystem.MinMaxCurve(1f);

            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.scale = new Vector3(screenSize.x, screenSize.y, 1f);
        }
    }

    static void PlayParticleSystems(GameObject instance)
    {
        ParticleSystem[] particleSystems = instance.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];
            particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particleSystem.Play(true);
        }
    }

    static float ResolveEffectLifetime(GameObject instance, float minimumLifetime)
    {
        float lifetime = Mathf.Max(0.01f, minimumLifetime);
        ParticleSystem[] particleSystems = instance.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem.MainModule main = particleSystems[i].main;
            float particleLifetime = main.duration + main.startDelay.constantMax + main.startLifetime.constantMax;
            lifetime = Mathf.Max(lifetime, particleLifetime);
        }

        return lifetime + 0.1f;
    }

    IEnumerator FadeAndDestroyScreenEffectAfterRealtime(GameObject instance, float delay, ScreenEffectFadeState fadeState)
    {
        float elapsed = 0f;
        while (instance != null && elapsed < delay)
        {
            fadeState?.Apply(ResolveScreenEffectVisualAlpha(elapsed, delay));
            elapsed += useUnscaledEffectTime ? Time.unscaledDeltaTime : Time.deltaTime;
            yield return null;
        }

        fadeState?.Apply(0f);

        if (instance != null)
            Destroy(instance);
    }

#if UNITY_EDITOR
    void FadeAndDestroyScreenEffectAfterEditorDelay(GameObject instance, float delay, ScreenEffectFadeState fadeState)
    {
        double startedAt = UnityEditor.EditorApplication.timeSinceStartup;

        void Tick()
        {
            double elapsed = UnityEditor.EditorApplication.timeSinceStartup - startedAt;
            if (instance != null && elapsed < delay)
            {
                fadeState?.Apply(ResolveScreenEffectVisualAlpha((float)elapsed, delay));
                return;
            }

            UnityEditor.EditorApplication.update -= Tick;
            fadeState?.Apply(0f);

            if (instance != null)
                DestroyImmediate(instance);
        }

        UnityEditor.EditorApplication.update += Tick;
    }
#endif

    float ResolveScreenEffectVisualAlpha(float elapsed, float lifetime)
    {
        float alpha = Mathf.Max(0f, screenEffectOpacityMultiplier);
        if (!useScreenEffectFade)
            return alpha;

        float fadeAlpha = 1f;
        if (fadeInDuration > 0f)
            fadeAlpha = Mathf.Min(fadeAlpha, Mathf.Clamp01(elapsed / fadeInDuration));

        if (fadeOutDuration > 0f)
            fadeAlpha = Mathf.Min(fadeAlpha, Mathf.Clamp01((lifetime - elapsed) / fadeOutDuration));

        return alpha * Mathf.SmoothStep(0f, 1f, fadeAlpha);
    }

    sealed class ScreenEffectFadeState
    {
        readonly MaterialFadeTarget[] materialTargets;

        public ScreenEffectFadeState(GameObject instance)
        {
            List<MaterialFadeTarget> targets = new();
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                Material[] materials = renderer.sharedMaterials;
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    MaterialFadeTarget target = new(renderer, materialIndex, materials[materialIndex]);
                    if (target.HasAnyFadeProperty)
                        targets.Add(target);
                }
            }

            materialTargets = targets.ToArray();
        }

        public void Apply(float alpha)
        {
            alpha = Mathf.Max(0f, alpha);

            for (int i = 0; i < materialTargets.Length; i++)
                materialTargets[i].Apply(alpha);
        }
    }

    sealed class MaterialFadeTarget
    {
        static readonly int OpacityId = Shader.PropertyToID("_Opacity");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int TintColorId = Shader.PropertyToID("_TintColor");

        readonly Renderer renderer;
        readonly int materialIndex;
        readonly MaterialPropertyBlock propertyBlock = new();
        readonly bool hasOpacity;
        readonly bool hasColor;
        readonly bool hasBaseColor;
        readonly bool hasTintColor;
        readonly float opacity;
        readonly Color color;
        readonly Color baseColor;
        readonly Color tintColor;

        public bool HasAnyFadeProperty => hasOpacity || hasColor || hasBaseColor || hasTintColor;

        public MaterialFadeTarget(Renderer renderer, int materialIndex, Material material)
        {
            this.renderer = renderer;
            this.materialIndex = materialIndex;

            if (material == null)
                return;

            hasOpacity = material.HasProperty(OpacityId);
            if (hasOpacity)
                opacity = material.GetFloat(OpacityId);

            hasColor = material.HasProperty(ColorId);
            if (hasColor)
                color = material.GetColor(ColorId);

            hasBaseColor = material.HasProperty(BaseColorId);
            if (hasBaseColor)
                baseColor = material.GetColor(BaseColorId);

            hasTintColor = material.HasProperty(TintColorId);
            if (hasTintColor)
                tintColor = material.GetColor(TintColorId);
        }

        public void Apply(float alpha)
        {
            if (renderer == null)
                return;

            renderer.GetPropertyBlock(propertyBlock, materialIndex);

            if (hasOpacity)
            {
                propertyBlock.SetFloat(OpacityId, opacity * alpha);
            }
            else
            {
                if (hasColor)
                    propertyBlock.SetColor(ColorId, MultiplyColorAlpha(color, alpha));
                if (hasBaseColor)
                    propertyBlock.SetColor(BaseColorId, MultiplyColorAlpha(baseColor, alpha));
                if (hasTintColor)
                    propertyBlock.SetColor(TintColorId, MultiplyColorAlpha(tintColor, alpha));
            }

            renderer.SetPropertyBlock(propertyBlock, materialIndex);
        }
    }

    static Color MultiplyColorAlpha(Color color, float alpha)
    {
        color.a *= alpha;
        return color;
    }

    float ResolveScreenDirectionAngle(Vector3 worldDirection)
    {
        worldDirection.y = 0f;
        if (worldDirection.sqrMagnitude <= 0.0001f)
            return 0f;

        worldDirection.Normalize();
        Camera effectCamera = ResolveEffectCamera();
        if (effectCamera == null)
            return 0f;

        Vector3 cameraRight = effectCamera.transform.right;
        Vector3 cameraForward = effectCamera.transform.forward;
        cameraRight.y = 0f;
        cameraForward.y = 0f;

        if (cameraRight.sqrMagnitude > 0.0001f)
            cameraRight.Normalize();
        if (cameraForward.sqrMagnitude > 0.0001f)
            cameraForward.Normalize();

        Vector2 screenDirection = new(
            Vector3.Dot(worldDirection, cameraRight),
            Vector3.Dot(worldDirection, cameraForward));

        if (screenDirection.sqrMagnitude <= 0.0001f)
            return 0f;

        screenDirection.Normalize();
        return Mathf.Atan2(screenDirection.y, screenDirection.x) * Mathf.Rad2Deg;
    }

    static GameObject ResolveScreenEffectPrefab(GameObject assignedPrefab, string fallbackAssetPath)
    {
        if (assignedPrefab != null)
            return assignedPrefab;

#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(fallbackAssetPath);
#else
        return null;
#endif
    }

#if UNITY_EDITOR
    void AssignDefaultScreenEffectPrefabsInEditor()
    {
        if (healScreenEffectPrefab == null)
            healScreenEffectPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(DefaultHealScreenEffectPath);

        if (perfectDodgeScreenEffectPrefab == null)
            perfectDodgeScreenEffectPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(DefaultPerfectDodgeScreenEffectPath);
    }
#endif
}
