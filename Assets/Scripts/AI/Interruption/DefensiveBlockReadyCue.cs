using UnityEngine;

// Player-local presentation of the selected attack's real command eligibility.
// One reusable billboard; querying readiness never spends/reserves anything.
[DisallowMultipleComponent]
public sealed class DefensiveBlockReadyCue : MonoBehaviour
{
    public GameObject cuePrefab;
    public Vector3 targetOffset = new Vector3(0f, 2.6f, 0f);
    [Range(0.1f, 2f)] public float screenWidth = 1.2f;
    [Range(0.1f, 2f)] public float screenHeight = 0.85f;
    [Min(0f)] public float brightness = 1.3f;
    [Min(0.01f)] public float appearSeconds = 0.18f;
    [Min(0.01f)] public float disappearSeconds = 0.12f;
    PlayerContext ctx;
    Camera viewCamera;
    Transform cue;
    Renderer cueRenderer;
    MaterialPropertyBlock properties;
    enum Phase { Hidden, Appearing, Holding, Disappearing }
    Phase phase;
    CharacteContext displayedTarget;
    float phaseTime, width, heightScale, intensity;
    float startWidth, startHeight, startIntensity;
    static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    public bool IsVisible => cue != null && cue.gameObject.activeSelf;
    // Eligibility ends immediately; the disappearing tail is presentation only.
    public bool IsReady { get; private set; }

    void Awake()
    {
        ctx = GetComponentInParent<PlayerContext>();
        ctx?.ResolveReferences();
        properties = new MaterialPropertyBlock();
    }

    void LateUpdate()
    {
        if (viewCamera == null || !viewCamera.isActiveAndEnabled) viewCamera = Camera.main;
        if (ctx == null || ctx.Targeting == null || viewCamera == null || cuePrefab == null ||
            ctx.HealthSystem == null || !ctx.HealthSystem.IsAlive)
        { HideImmediately(); return; }

        bool eligible = ctx.Targeting.TryGetTarget(out CharacteContext target) && target != null &&
            target.DefensiveBlockAttack != null && target.DefensiveBlockAttack.CanRequestBlock(ctx);
        IsReady = eligible;
        if (eligible)
        {
            if (displayedTarget != target)
            {
                HideImmediately();
                displayedTarget = target;
                IsReady = true;
            }
            if (phase == Phase.Hidden || phase == Phase.Disappearing) BeginPhase(Phase.Appearing);
        }
        else if (phase == Phase.Appearing || phase == Phase.Holding) BeginPhase(Phase.Disappearing);

        if (phase == Phase.Hidden) return;
        if (displayedTarget == null || !displayedTarget.isActiveAndEnabled ||
            displayedTarget.HealthSystem == null || !displayedTarget.HealthSystem.IsAlive)
        { HideImmediately(); return; }

        Vector3 position = displayedTarget.transform.position + targetOffset;
        Vector3 viewport = viewCamera.WorldToViewportPoint(position);
        if (viewport.z <= viewCamera.nearClipPlane || viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f)
        { HideImmediately(); return; }
        if (cue == null)
        {
            cue = Instantiate(cuePrefab).transform;
            cue.name = "Defensive Block Ready (local cue)";
            cueRenderer = cue.GetComponent<Renderer>();
        }
        cue.gameObject.SetActive(true);
        AdvanceAnimation(Time.unscaledDeltaTime);
        if (phase == Phase.Hidden) return;
        float height = viewCamera.orthographic ? 2f * viewCamera.orthographicSize :
            2f * viewport.z * Mathf.Tan(viewCamera.fieldOfView * Mathf.Deg2Rad * 0.5f);
        cue.SetPositionAndRotation(position, viewCamera.transform.rotation);
        cue.localScale = new Vector3(height * viewCamera.aspect * screenWidth * width,
            height * screenHeight * heightScale, 1f);
        properties.SetFloat(IntensityId, brightness * intensity);
        if (cueRenderer != null) cueRenderer.SetPropertyBlock(properties);
    }

    void BeginPhase(Phase next)
    {
        phase = next;
        phaseTime = 0f;
        startWidth = Mathf.Max(0.025f, width);
        startHeight = Mathf.Max(0.06f, heightScale);
        startIntensity = intensity;
    }

    void AdvanceAnimation(float dt)
    {
        phaseTime += dt;
        if (phase == Phase.Appearing)
        {
            float t = Mathf.Clamp01(phaseTime / Mathf.Max(0.01f, appearSeconds));
            float spread = 1f - Mathf.Pow(1f - t, 3f);
            width = Mathf.Lerp(startWidth, 1f, spread);
            heightScale = Mathf.Lerp(startHeight, 1f, Mathf.SmoothStep(0f, 1f, t));
            intensity = Mathf.Lerp(startIntensity, 1f, spread) + Mathf.Sin(t * Mathf.PI) * 0.45f;
            if (t >= 1f) phase = Phase.Holding;
        }
        else if (phase == Phase.Disappearing)
        {
            float t = Mathf.Clamp01(phaseTime / Mathf.Max(0.01f, disappearSeconds));
            width = Mathf.Lerp(startWidth, 0.025f, Mathf.SmoothStep(0f, 1f, t));
            heightScale = Mathf.Lerp(startHeight, 0.06f, t);
            intensity = startIntensity * (1f - t) * (1f - t);
            if (t >= 1f) HideImmediately();
        }
    }

    void HideImmediately()
    {
        IsReady = false;
        phase = Phase.Hidden;
        phaseTime = width = heightScale = intensity = 0f;
        displayedTarget = null;
        if (cue != null) cue.gameObject.SetActive(false);
    }
    void OnDisable() { HideImmediately(); }
    void OnDestroy() { if (cue != null) Destroy(cue.gameObject); }
}
