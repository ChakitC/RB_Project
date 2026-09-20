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
    [Range(0.05f, 1f)] public float unavailableBrightness = 0.25f;
    [Min(0.01f)] public float appearSeconds = 0.18f;
    [Min(0.01f)] public float disappearSeconds = 0.12f;
    PlayerContext ctx;
    Camera viewCamera;
    Transform cue;
    Renderer cueRenderer;
    MaterialPropertyBlock properties;
    float readyWeight;
    DefensiveBlockAttack announcedAttack;
    int announcedRequest, announcedLife, announcedWindow;
    DefensiveBlockAttack displayedAttack;
    int displayedRequest, displayedLife, displayedWindow;
    enum Phase { Hidden, Appearing, Holding, Disappearing }
    Phase phase;
    CharacteContext displayedTarget;
    float phaseTime, width, heightScale, intensity;
    float startWidth, startHeight, startIntensity;
    static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    public bool IsVisible => cue != null && cue.gameObject.activeSelf;
    // Eligibility ends immediately; the disappearing tail is presentation only.
    public bool IsReady { get; private set; }
    public DefensiveBlockAttack ReadyAttack { get; private set; }
    public DefensiveBlockAttack ThreatAttack { get; private set; }
    // Retained for callers; readiness is presented only through the flare.
    public bool IsPromptVisible => false;

    void Awake()
    {
        ctx = GetComponentInParent<PlayerContext>();
        ctx?.ResolveReferences();
        properties = new MaterialPropertyBlock();
    }

    void LateUpdate()
    {
        if (viewCamera == null || !viewCamera.isActiveAndEnabled) viewCamera = Camera.main;
        if (ctx == null || ctx.interruptionCommand == null || viewCamera == null || cuePrefab == null ||
            ctx.HealthSystem == null || !ctx.HealthSystem.IsAlive)
        { HideImmediately(); return; }

        bool eligible = ctx.interruptionCommand.TrySelectDefensiveBlockAttack(out var attack);
        var threat = attack;
        bool hasThreat = eligible || ctx.interruptionCommand.TrySelectDefensiveBlockThreat(out threat);
        CharacteContext target = hasThreat ? threat.CasterContext : null;
        ReadyAttack = eligible ? attack : null;
        ThreatAttack = hasThreat ? threat : null;
        IsReady = eligible;
        if (hasThreat)
        {
            if (displayedTarget != target || displayedAttack != threat || displayedRequest != threat.RequestId ||
                displayedLife != target.LifeGeneration || displayedWindow != threat.WindowIndex)
            {
                HideImmediately();
                displayedTarget = target;
                displayedAttack = threat; displayedRequest = threat.RequestId;
                displayedLife = target.LifeGeneration; displayedWindow = threat.WindowIndex;
                IsReady = eligible;
                ReadyAttack = eligible ? attack : null;
                ThreatAttack = threat;
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
        { HideImmediately(false); return; }
        if (cue == null)
        {
            cue = Instantiate(cuePrefab).transform;
            cue.name = "Defensive Block Ready (local cue)";
            cueRenderer = cue.GetComponent<Renderer>();
        }
        cue.gameObject.SetActive(true);
        AdvanceAnimation(Time.unscaledDeltaTime);
        if (phase == Phase.Hidden) return;
        TryPlayReadySound(eligible ? attack : null, position);
        float height = viewCamera.orthographic ? 2f * viewCamera.orthographicSize :
            2f * viewport.z * Mathf.Tan(viewCamera.fieldOfView * Mathf.Deg2Rad * 0.5f);
        cue.SetPositionAndRotation(position, viewCamera.transform.rotation);
        cue.localScale = new Vector3(height * viewCamera.aspect * screenWidth * width,
            height * screenHeight * heightScale, 1f);
        readyWeight = Mathf.MoveTowards(readyWeight, eligible ? 1f : unavailableBrightness, Time.unscaledDeltaTime * 8f);
        properties.SetFloat(IntensityId, brightness * intensity * readyWeight);
        if (cueRenderer != null) cueRenderer.SetPropertyBlock(properties);
    }

    void TryPlayReadySound(DefensiveBlockAttack attack, Vector3 position)
    {
        if (attack == null || attack.CasterContext == null) return;
        int life = attack.CasterContext.LifeGeneration;
        if (announcedAttack == attack && announcedRequest == attack.RequestId && announcedLife == life &&
            announcedWindow == attack.WindowIndex) return;
        var settings = ctx.DefensiveBlock != null ? ctx.DefensiveBlock.Settings : null;
        if (settings == null || settings.readyCue == null) return;
        // Keep this stamp when the flare hides, so camera/range flicker cannot replay it.
        announcedAttack = attack;
        announcedRequest = attack.RequestId;
        announcedLife = life;
        announcedWindow = attack.WindowIndex;
        AudioService.Instance.PlayAtPosition(settings.readyCue, position);
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

    void HideImmediately(bool clearReady = true)
    {
        if (clearReady) { IsReady = false; ReadyAttack = null; ThreatAttack = null; }
        phase = Phase.Hidden;
        phaseTime = width = heightScale = intensity = 0f;
        displayedTarget = null;
        if (cue != null) cue.gameObject.SetActive(false);
    }
    void OnDisable() { HideImmediately(); }
    void OnDestroy()
    {
        if (cue != null) Destroy(cue.gameObject);
    }
}
