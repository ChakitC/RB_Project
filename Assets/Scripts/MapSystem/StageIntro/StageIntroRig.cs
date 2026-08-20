using System;
using System.Collections;
using System.Collections.Generic;
using Animancer;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Plays the MapRun stage intro: warps the four party roles onto authored markers, poses them with
/// their <see cref="CharacterAnimProfileSO.stageIntroClip"/>, and runs a group-shot camera AnimationClip.
/// The camera clip is the master duration. Everything is restored afterwards so gameplay resumes
/// from the exact post-room-warp pose.
///
/// The rig fails open: when validation fails, <see cref="TryPlay"/> returns false and the caller
/// starts the room immediately instead of blocking the run.
/// </summary>
[DisallowMultipleComponent]
public sealed class StageIntroRig : MonoBehaviour
{
    static readonly ChainActorRole[] RequiredRoles =
    {
        ChainActorRole.Player,
        ChainActorRole.PartySlot1,
        ChainActorRole.PartySlot2,
        ChainActorRole.Helper,
    };

    [Header("Camera")]
    [SerializeField]
    [Tooltip("Cinemachine camera used for the group shot. Keep it disabled in the prefab; the rig enables it during the intro.")]
    private CinemachineCamera introCamera;

    [SerializeField]
    [Tooltip("Transform animated by the Camera Clip. Needs an Animancer/Animator so the clip can drive it.")]
    private Transform cameraAnimationRoot;

    [SerializeField]
    [Tooltip("Group-shot camera animation. This clip's length is the master duration of the intro. Leave empty to disable the intro (fail-open).")]
    private AnimationClip cameraClip;

    [SerializeField, Min(0)]
    [Tooltip("Priority applied to the intro camera while it is active. Must beat the gameplay camera.")]
    private int introCameraPriority = 100;

    [Header("Presentation")]
    [SerializeField, Min(0f)]
    [Tooltip("Seconds the screen stays fully black before the fade starts. The party is already " +
             "placed and locked during this hold; the performance begins when the fade begins.")]
    private float blackHoldSeconds = 0.35f;

    [SerializeField, Min(0f)]
    [Tooltip("Seconds to fade from black. The intro plays underneath, so this overlaps the opening " +
             "of the camera clip rather than delaying it.")]
    private float fadeInDuration = 0.6f;
    [SerializeField, Min(0f)] private float fadeOutDuration = 0.2f;
    [SerializeField, Range(0f, 0.45f)] private float letterboxThickness = 0.08f;
    [SerializeField, Min(0.05f)] private float skipHoldSeconds = 0.75f;
    [SerializeField] private int overlaySortingOrder = 31998;
    [SerializeField] private Color letterboxColor = Color.black;

    [Header("Debug")]
    [SerializeField] private bool logLifecycle;

    readonly List<StageIntroActorScope> activeScopes = new();
    readonly Dictionary<ChainActorRole, StageIntroActorMarker> markerLookup = new();

    StageIntroOverlay overlay;
    StageIntroSkipInput skipInput;
    Coroutine playRoutine;

    Action completedCallback;
    bool completionInvoked;
    bool isPlaying;

    AnimancerComponent cameraAnimancer;
    AnimatorUpdateMode savedCameraUpdateMode;
    bool cameraAnimancerCaptured;

    bool introCameraCaptured;
    int savedIntroCameraPriority;

    public bool IsPlaying => isPlaying;
    public AnimationClip CameraClip => cameraClip;
    public Transform CameraAnimationRoot => cameraAnimationRoot;
    public CinemachineCamera IntroCamera => introCamera;
    public float IntroDuration => cameraClip != null ? cameraClip.length : 0f;

    void Awake()
    {
        NormalizeScale();
    }

    void OnDisable()
    {
        if (isPlaying)
            AbortAndComplete();
    }

    /// <summary>
    /// Cancels any scale inherited from the room instance so marker offsets and the camera rig are
    /// exactly what was authored in Prefab Mode. Room prefabs are free to carry a root scale — the
    /// Start room uses 1.33 — but an intro blocked at 1.0 must not silently play 33% larger. The rig
    /// still inherits the room's position and rotation, which it needs: rooms are instantiated with a
    /// per-node yaw, so a rig outside the room hierarchy would face the wrong way.
    /// </summary>
    void NormalizeScale()
    {
        Transform parent = transform.parent;
        if (parent == null)
            return;

        Vector3 parentScale = parent.lossyScale;
        transform.localScale = new Vector3(
            Mathf.Approximately(parentScale.x, 0f) ? 1f : 1f / parentScale.x,
            Mathf.Approximately(parentScale.y, 0f) ? 1f : 1f / parentScale.y,
            Mathf.Approximately(parentScale.z, 0f) ? 1f : 1f / parentScale.z);
    }

    // ---------------------------------------------------------------- authoring / validation

    public IReadOnlyList<StageIntroActorMarker> CollectMarkers(List<StageIntroActorMarker> buffer)
    {
        buffer.Clear();
        GetComponentsInChildren(true, buffer);
        return buffer;
    }

    public StageIntroActorMarker FindMarker(ChainActorRole role)
    {
        var markers = new List<StageIntroActorMarker>();
        CollectMarkers(markers);
        for (int i = 0; i < markers.Count; i++)
            if (markers[i] != null && markers[i].Role == role)
                return markers[i];
        return null;
    }

    /// <summary>Reports every authoring problem that would stop the intro from playing.</summary>
    public void CollectValidationIssues(List<string> issues)
    {
        if (issues == null)
            throw new ArgumentNullException(nameof(issues));

        var markers = new List<StageIntroActorMarker>();
        CollectMarkers(markers);

        var seen = new Dictionary<ChainActorRole, int>();
        for (int i = 0; i < markers.Count; i++)
        {
            StageIntroActorMarker marker = markers[i];
            if (marker == null)
            {
                issues.Add("A marker reference under the rig is missing.");
                continue;
            }

            seen.TryGetValue(marker.Role, out int count);
            seen[marker.Role] = count + 1;
        }

        for (int i = 0; i < RequiredRoles.Length; i++)
        {
            ChainActorRole role = RequiredRoles[i];
            seen.TryGetValue(role, out int count);
            if (count == 0)
                issues.Add($"Missing StageIntroActorMarker for role '{role}'.");
            else if (count > 1)
                issues.Add($"Role '{role}' is assigned to {count} markers; each role needs exactly one.");
        }

        foreach (KeyValuePair<ChainActorRole, int> entry in seen)
        {
            if (Array.IndexOf(RequiredRoles, entry.Key) < 0)
                issues.Add($"Marker role '{entry.Key}' is not part of the stage intro lineup.");
        }

        if (introCamera == null)
            issues.Add("Intro CinemachineCamera is not assigned.");

        if (cameraAnimationRoot == null)
            issues.Add("Camera animation root is not assigned.");
        else if (cameraAnimationRoot.GetComponentInChildren<Animator>(true) == null &&
                 cameraAnimationRoot.GetComponentInChildren<AnimancerComponent>(true) == null)
            issues.Add("Camera animation root has no Animator/Animancer to play the Camera Clip.");

        if (cameraClip == null)
            issues.Add("Camera Clip is not assigned. The stage intro stays disabled until an author supplies one.");
        else if (cameraClip.length <= 0f)
            issues.Add("Camera Clip has zero length.");
    }

    public bool IsPlayable(out string error)
    {
        var issues = new List<string>();
        CollectValidationIssues(issues);
        error = string.Join("\n", issues);
        return issues.Count == 0;
    }

    // ---------------------------------------------------------------- playback

    /// <summary>
    /// Starts the intro. Returns false when the rig is not playable or the cinematic stage is busy —
    /// in that case <paramref name="completed"/> is never invoked and the caller should continue
    /// straight into gameplay.
    /// </summary>
    public bool TryPlay(PartyRuntime party, Action completed)
    {
        if (isPlaying)
            return false;

        if (party == null)
        {
            Log("No PartyRuntime supplied; skipping the stage intro.");
            return false;
        }

        if (!IsPlayable(out string error))
        {
            Log($"Stage intro skipped:\n{error}");
            return false;
        }

        if (!isActiveAndEnabled)
            return false;

        if (!CutsceneDirector.Instance.TryBegin(this))
        {
            Log("Cinematic stage is busy; skipping the stage intro.");
            return false;
        }

        NormalizeScale();
        BuildMarkerLookup();

        completedCallback = completed;
        completionInvoked = false;
        isPlaying = true;

        playRoutine = StartCoroutine(PlayRoutine(party));
        return true;
    }

    IEnumerator PlayRoutine(PartyRuntime party)
    {
        try
        {
            overlay ??= new StageIntroOverlay(overlaySortingOrder, letterboxColor);
            overlay.EnsureBuilt(transform);
            overlay.SetFadeAlpha(1f);
            overlay.SetLetterbox(letterboxThickness, false);
            overlay.SetSkipPromptVisible(false);

            UIManager.Instance?.SetHudVisible(false);

            skipInput = new StageIntroSkipInput(skipHoldSeconds);
            skipInput.Bind(party.Player);

            // Place and lock the party while the screen is still black.
            ApplyActorScopes(party);
            EnableIntroCamera();

            // Absorb the startup frame before timing anything. The frame that instantiates the room
            // and spawns the party measures around a full second, which would otherwise consume the
            // entire hold and fade in one step and turn the opening into a hard cut.
            yield return null;

            // Hold on black so the cut into the intro reads as a deliberate opening rather than a warp.
            float held = 0f;
            while (held < blackHoldSeconds)
            {
                held += StepTime();
                overlay.Tick();
                yield return null;
            }

            // Performance and fade start together, so the reveal happens over the opening of the shot
            // instead of after it.
            BeginActorIntroPoses();
            PlayCameraClip();

            // Let the first pose and camera frame land while still fully black.
            yield return null;

            overlay.SetLetterbox(letterboxThickness, true);
            overlay.SetSkipPromptVisible(skipInput.IsAvailable);
            overlay.SetSkipPrompt(skipInput.BindingLabel, 0f);

            float duration = IntroDuration;
            float fadeIn = Mathf.Max(fadeInDuration, 0.0001f);
            float elapsed = 0f;
            float alpha = 1f;

            while (elapsed < duration && !skipInput.Completed)
            {
                float delta = StepTime();
                elapsed += delta;

                alpha = 1f - Mathf.Clamp01(elapsed / fadeIn);
                overlay.SetFadeAlpha(alpha);

                skipInput.Tick(delta);
                overlay.SetSkipPrompt(skipInput.BindingLabel, skipInput.Progress01);
                overlay.Tick();
                yield return null;
            }

            // Skipping mid-fade must not pop back to clear before fading out.
            yield return FadeOverlay(alpha, 1f, fadeOutDuration);

            StopCameraClip();
            RestoreIntroCamera();
            RestoreActorScopes();

            overlay.SetLetterboxVisible(false);
            overlay.SetSkipPromptVisible(false);

            yield return FadeOverlay(1f, 0f, fadeOutDuration);
        }
        finally
        {
            playRoutine = null;
            Cleanup();
            InvokeCompleted();
        }
    }

    /// <summary>
    /// Unscaled frame step, clamped so one hitch cannot skip a whole beat of the opening. Loading and
    /// spawning routinely produce frames near a second long, and a fade measured in tenths must not be
    /// swallowed whole by one of them.
    /// </summary>
    static float StepTime() => Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);

    IEnumerator FadeOverlay(float from, float to, float duration)
    {
        overlay.SetFadeAlpha(from);

        float safeDuration = Mathf.Max(duration, 0.0001f);
        float elapsed = 0f;
        while (elapsed < safeDuration)
        {
            elapsed += StepTime();
            overlay.SetFadeAlpha(Mathf.Lerp(from, to, elapsed / safeDuration));
            overlay.Tick();
            yield return null;
        }

        overlay.SetFadeAlpha(to);
    }

    void AbortAndComplete()
    {
        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }

        Cleanup();
        InvokeCompleted();
    }

    /// <summary>Idempotent teardown shared by normal completion, skip, disable, and exceptions.</summary>
    void Cleanup()
    {
        if (!isPlaying)
            return;

        isPlaying = false;

        StopCameraClip();
        RestoreIntroCamera();
        RestoreActorScopes();

        skipInput?.Dispose();
        skipInput = null;

        overlay?.SetLetterboxVisible(false);
        overlay?.SetSkipPromptVisible(false);
        overlay?.SetFadeAlpha(0f);

        UIManager.Instance?.SetHudVisible(true);
        CutsceneDirector.Instance.End(this);
    }

    void InvokeCompleted()
    {
        if (completionInvoked)
            return;

        completionInvoked = true;
        Action callback = completedCallback;
        completedCallback = null;
        callback?.Invoke();
    }

    // ---------------------------------------------------------------- actors

    void BuildMarkerLookup()
    {
        markerLookup.Clear();

        var markers = new List<StageIntroActorMarker>();
        CollectMarkers(markers);
        for (int i = 0; i < markers.Count; i++)
        {
            StageIntroActorMarker marker = markers[i];
            if (marker != null && !markerLookup.ContainsKey(marker.Role))
                markerLookup.Add(marker.Role, marker);
        }
    }

    void ApplyActorScopes(PartyRuntime party)
    {
        for (int i = 0; i < RequiredRoles.Length; i++)
        {
            ChainActorRole role = RequiredRoles[i];
            PartyRuntimeActor actor = party.GetActor(role);
            CharacteContext context = actor != null ? actor.Context : null;
            if (context == null)
                continue;

            if (!markerLookup.TryGetValue(role, out StageIntroActorMarker marker) || marker == null)
                continue;

            // The Helper is a summon that is hidden outside of commands, so its scope needs the
            // manager that owns that visibility.
            AllyHelperManager cinematicHelper = role == ChainActorRole.Helper && party.Player != null
                ? party.Player.allyHelper
                : null;

            var scope = new StageIntroActorScope(context, cinematicHelper);
            if (!scope.IsValid)
                continue;

            scope.Apply(marker.Position, marker.Rotation);
            activeScopes.Add(scope);
        }
    }

    void BeginActorIntroPoses()
    {
        for (int i = 0; i < activeScopes.Count; i++)
            activeScopes[i].BeginIntroPose();
    }

    void RestoreActorScopes()
    {
        for (int i = activeScopes.Count - 1; i >= 0; i--)
            activeScopes[i].Restore();

        activeScopes.Clear();
    }

    // ---------------------------------------------------------------- camera

    void EnableIntroCamera()
    {
        if (introCamera == null)
            return;

        savedIntroCameraPriority = introCamera.Priority.Value;
        introCameraCaptured = true;

        introCamera.Priority = introCameraPriority;
        introCamera.gameObject.SetActive(true);
    }

    void RestoreIntroCamera()
    {
        if (!introCameraCaptured || introCamera == null)
        {
            introCameraCaptured = false;
            return;
        }

        introCameraCaptured = false;
        introCamera.Priority = savedIntroCameraPriority;

        // Always switch the intro camera off, even if the prefab shipped it enabled. Restoring an
        // enabled state would leave a live Cinemachine camera tied with the gameplay camera on
        // priority, and the brain keeps whichever activated last — so gameplay never gets its camera
        // back. The rig owns this camera; outside the intro it has no reason to be live.
        introCamera.gameObject.SetActive(false);
    }

    void PlayCameraClip()
    {
        if (cameraClip == null || cameraAnimationRoot == null)
            return;

        cameraAnimancer = ResolveCameraAnimancer();
        if (cameraAnimancer == null || cameraAnimancer.Animator == null)
        {
            Debug.LogWarning("[StageIntroRig] Camera animation root has no usable Animancer/Animator; the camera will not move.", this);
            return;
        }

        savedCameraUpdateMode = cameraAnimancer.Animator.updateMode;
        cameraAnimancerCaptured = true;

        cameraAnimancer.Animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        cameraAnimancer.Play(cameraClip);
    }

    void StopCameraClip()
    {
        if (!cameraAnimancerCaptured || cameraAnimancer == null)
        {
            cameraAnimancerCaptured = false;
            return;
        }

        cameraAnimancerCaptured = false;

        if (cameraAnimancer.Animator != null)
            cameraAnimancer.Animator.updateMode = savedCameraUpdateMode;

        cameraAnimancer.Stop();
    }

    AnimancerComponent ResolveCameraAnimancer()
    {
        var animancer = cameraAnimationRoot.GetComponentInChildren<AnimancerComponent>(true);
        if (animancer != null)
            return animancer;

        var animator = cameraAnimationRoot.GetComponentInChildren<Animator>(true);
        if (animator == null)
            return null;

        animancer = animator.gameObject.AddComponent<AnimancerComponent>();
        animancer.Animator = animator;
        return animancer;
    }

    void Log(string message)
    {
        if (logLifecycle)
            Debug.Log($"[StageIntroRig] {message}", this);
    }
}
