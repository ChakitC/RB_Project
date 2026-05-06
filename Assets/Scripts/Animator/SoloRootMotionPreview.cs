using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Plays one AnimationClip for scene preview, with an edit-mode root motion toggle.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[AddComponentMenu("Animation/Solo Root Motion Preview")]
[DefaultExecutionOrder(DefaultExecutionOrder)]
public sealed class SoloRootMotionPreview : MonoBehaviour, IAnimationClipSource
{
    public const int DefaultExecutionOrder = -5000;
    private const float RootMotionScrubStep = 1f / 30f;
    private const int MaxRootMotionScrubSteps = 300;

    [Serializable]
    private struct TransformPose
    {
        public Transform transform;
        public Vector3 localPosition;
        public Quaternion localRotation;
        public Vector3 localScale;

        public TransformPose(Transform transform)
        {
            this.transform = transform;
            localPosition = transform.localPosition;
            localRotation = transform.localRotation;
            localScale = transform.localScale;
        }

        public void Restore()
        {
            if (transform == null)
                return;

            transform.localPosition = localPosition;
            transform.localRotation = localRotation;
            transform.localScale = localScale;
        }
    }

    [SerializeField, Tooltip("The Animator component which this preview controls.")]
    private Animator animator;

    [SerializeField, Tooltip("The animation that will be previewed.")]
    private AnimationClip clip;

    [SerializeField, Range(0f, 1f), Tooltip("The normalized time that the preview starts at.")]
    private float normalizedStartTime;

    [SerializeField, Tooltip("The speed at which the preview plays.")]
    private float speed = 1f;

    [SerializeField, Tooltip("Should root motion move the Animator transform while previewing?")]
    private bool applyRootMotion;

    [SerializeField, Tooltip("Should disabling this component stop and rewind the preview?")]
    private bool stopOnDisable = true;

    private PlayableGraph graph;
    private AnimationClipPlayable playable;
    private bool isPlaying;
    private bool hasCachedAnimatorRootMotion;
    private bool cachedAnimatorRootMotion;
    private Animator cachedAnimator;
    private bool hasPreviewOrigin;
    private Vector3 previewOriginPosition;
    private Quaternion previewOriginRotation;
    private Vector3 previewOriginScale;

    [SerializeField, HideInInspector]
    private bool hasRestorePose;

    [SerializeField, HideInInspector]
    private Transform restorePoseRoot;

    [SerializeField, HideInInspector]
    private TransformPose[] restorePose = Array.Empty<TransformPose>();

#if UNITY_EDITOR
    [SerializeField, Tooltip("Should the clip be automatically applied to this object in Edit Mode?")]
    private bool applyInEditMode = true;

    private double lastEditorUpdateTime;
#endif

    public Animator Animator
    {
        get => animator;
        set
        {
            if (animator == value)
                return;

            StopPreview(false);
            animator = value;
            Play();
        }
    }

    public AnimationClip Clip
    {
        get => clip;
        set
        {
            if (clip == value)
                return;

            clip = value;
            Play();
        }
    }

    public float Length { get; private set; } = float.NaN;
    public bool IsLooping { get; private set; }
    public bool IsInitialized => graph.IsValid();

    public bool IsPlaying
    {
        get => isPlaying;
        set
        {
            if (value)
                Play();
            else
                Pause();
        }
    }

    public float NormalizedStartTime
    {
        get => normalizedStartTime;
        set => normalizedStartTime = Mathf.Clamp01(value);
    }

    public float Time
    {
        get
        {
            if (playable.IsValid())
                return (float)playable.GetTime();

            return float.IsNaN(Length) ? 0f : normalizedStartTime * Length;
        }
        set
        {
            if (!playable.IsValid())
                return;

            SetPreviewTime(value);
        }
    }

    public float NormalizedTime
    {
        get
        {
            if (Length <= 0f)
                return 0f;

            return Time / Length;
        }
        set
        {
            if (Length <= 0f)
                return;

            Time = value * Length;
        }
    }

    public float Speed
    {
        get => speed;
        set
        {
            speed = value;
            if (playable.IsValid())
                playable.SetSpeed(speed);
        }
    }

    public bool ApplyRootMotion
    {
        get => applyRootMotion;
        set
        {
            if (applyRootMotion == value)
                return;

            applyRootMotion = value;
            ApplyAnimatorRootMotionSetting();
        }
    }

    public bool StopOnDisable
    {
        get => stopOnDisable;
        set => stopOnDisable = value;
    }

#if UNITY_EDITOR
    public bool ApplyInEditMode
    {
        get => applyInEditMode;
        set
        {
            if (applyInEditMode == value)
                return;

            applyInEditMode = value;
            if (!Application.isPlaying)
            {
                if (applyInEditMode && enabled)
                    Play();
                else
                    StopPreview(false);
            }
        }
    }
#endif

#if UNITY_EDITOR
    private void Reset()
    {
        animator = GetComponent<Animator>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (animator == null)
            animator = GetComponentInParent<Animator>();

        CacheRestorePose();
    }

    private void OnValidate()
    {
        normalizedStartTime = Mathf.Clamp01(normalizedStartTime);
        if (Application.isPlaying)
            return;

        ApplyAnimatorRootMotionSetting();

        if (applyInEditMode && enabled)
        {
            if (!IsInitialized)
                Play();

            if (playable.IsValid())
            {
                SetTime(normalizedStartTime * Length);
                Evaluate(0f);
            }
        }
        else
        {
            StopPreview(false);
        }
    }
#endif

    public void Play()
    {
        Play(clip);
    }

    public void Play(AnimationClip previewClip)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying && !applyInEditMode)
            return;
#endif

        if (previewClip == null)
        {
            Length = 0f;
            IsLooping = false;
            StopPreview(false);
            return;
        }

        clip = previewClip;
        Length = clip.length;
        IsLooping = clip.isLooping;

        if (animator == null)
            return;

        EnsureRestorePoseCached();

        if (!hasPreviewOrigin)
            CachePreviewOrigin();

        ApplyAnimatorRootMotionSetting();

        if (graph.IsValid())
            graph.Destroy();

        playable = AnimationPlayableUtilities.PlayClip(animator, clip, out graph);
        playable.SetSpeed(speed);
        SetTime(normalizedStartTime * Length);

        if (speed != 0f)
        {
            isPlaying = true;
            graph.Play();
        }
        else
        {
            isPlaying = false;
            graph.Stop();
        }

        Evaluate(0f);

#if UNITY_EDITOR
        RegisterEditorUpdate();
#endif
    }

    public void Pause()
    {
        isPlaying = false;

        if (graph.IsValid())
            graph.Stop();
    }

    public void Rewind()
    {
        if (!IsInitialized)
            Play();

        if (playable.IsValid())
        {
            RestorePreviewRootToOrigin();
            SetTime(normalizedStartTime * Length);
            Evaluate(0f);
        }
    }

    public void ResetPreviewOrigin()
    {
        Transform previewRoot = GetPreviewRoot();
        if (previewRoot == null)
            return;

        if (!hasPreviewOrigin)
            CachePreviewOrigin();

        RestorePreviewRootToOrigin();

        Rewind();
    }

    public void Evaluate()
    {
        Evaluate(0f);
    }

    public void Evaluate(float deltaTime)
    {
        if (!graph.IsValid())
            return;

        ApplyAnimatorRootMotionSetting();
        graph.Evaluate(deltaTime);
    }

    private void OnEnable()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            if (applyInEditMode)
                Play();

            RegisterEditorUpdate();
            return;
        }
#endif

        Play();
    }

    private void LateUpdate()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
            return;
#endif

        StopAtClipEndIfNeeded();
    }

    private void OnDisable()
    {
        if (stopOnDisable)
        {
            Rewind();
            StopPreview(true);
        }
        else
        {
            Pause();
            RestoreAnimatorRootMotionSetting();
        }

#if UNITY_EDITOR
        UnregisterEditorUpdate();
#endif
    }

    private void OnDestroy()
    {
        StopPreview(true);
        RestoreCachedPose();

#if UNITY_EDITOR
        UnregisterEditorUpdate();
#endif
    }

    private void StopPreview(bool clearOrigin)
    {
        isPlaying = false;

        if (graph.IsValid())
        {
            graph.Stop();
            graph.Destroy();
        }

        RestoreAnimatorRootMotionSetting();

        if (clearOrigin)
            hasPreviewOrigin = false;
    }

    private void StopAtClipEndIfNeeded()
    {
        if (!isPlaying ||
            IsLooping ||
            !playable.IsValid())
            return;

        float currentTime = (float)playable.GetTime();
        if (speed >= 0f && currentTime >= Length)
        {
            Pause();
            Time = Length;
        }
        else if (speed < 0f && currentTime <= 0f)
        {
            Pause();
            Time = 0f;
        }
    }

    private void SetTime(double value)
    {
        playable.SetTime(value);
        playable.SetTime(value);
    }

    private void SetPreviewTime(double value)
    {
        if (!playable.IsValid())
            return;

        double targetTime = ClampPreviewTime(value);

        if (ShouldScrubRootMotion())
        {
            ScrubRootMotionToTime(targetTime);
            return;
        }

        SetTime(targetTime);
        Evaluate(0f);
    }

    private double ClampPreviewTime(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            return 0d;

        if (Length <= 0f)
            return 0d;

        return Math.Max(0d, Math.Min(value, Length));
    }

    private bool ShouldScrubRootMotion()
    {
#if UNITY_EDITOR
        return applyRootMotion && !Application.isPlaying;
#else
        return false;
#endif
    }

    private void ScrubRootMotionToTime(double targetTime)
    {
        if (!playable.IsValid())
            return;

        if (!hasPreviewOrigin)
            CachePreviewOrigin();

        RestorePreviewRootToOrigin();
        ApplyAnimatorRootMotionSetting();

        bool wasPlaying = isPlaying;
        double startTime = ClampPreviewTime(normalizedStartTime * Length);
        double deltaTime = targetTime - startTime;
        double direction = Math.Sign(deltaTime);
        double remaining = Math.Abs(deltaTime);

        graph.Play();
        SetTime(startTime);

        playable.SetSpeed(0d);
        graph.Evaluate(0f);

        if (remaining > 0.0001d)
        {
            playable.SetSpeed(direction);

            double stepSize = Math.Max(RootMotionScrubStep, remaining / MaxRootMotionScrubSteps);
            while (remaining > 0.0001d)
            {
                float step = (float)Math.Min(remaining, stepSize);
                graph.Evaluate(step);
                remaining -= step;
            }
        }

        SetTime(targetTime);
        playable.SetSpeed(speed);

        if (wasPlaying)
            graph.Play();
        else
            graph.Stop();

#if UNITY_EDITOR
        lastEditorUpdateTime = EditorApplication.timeSinceStartup;
#endif
    }

    private void EnsureRestorePoseCached()
    {
        Transform previewRoot = GetPreviewRoot();
        if (previewRoot == null)
            return;

        if (hasRestorePose && restorePoseRoot == previewRoot && restorePose != null && restorePose.Length > 0)
            return;

        CacheRestorePose();
    }

    private void CacheRestorePose()
    {
        Transform previewRoot = GetPreviewRoot();
        if (previewRoot == null)
            return;

        Transform[] transforms = previewRoot.GetComponentsInChildren<Transform>(true);
        restorePose = new TransformPose[transforms.Length];

        for (int i = 0; i < transforms.Length; i++)
            restorePose[i] = new TransformPose(transforms[i]);

        restorePoseRoot = previewRoot;
        hasRestorePose = true;
    }

    private void RestoreCachedPose()
    {
        if (!hasRestorePose || restorePose == null)
            return;

        for (int i = 0; i < restorePose.Length; i++)
            restorePose[i].Restore();
    }

    private void CachePreviewOrigin()
    {
        Transform previewRoot = GetPreviewRoot();
        if (previewRoot == null)
            return;

        previewOriginPosition = previewRoot.position;
        previewOriginRotation = previewRoot.rotation;
        previewOriginScale = previewRoot.localScale;
        hasPreviewOrigin = true;
    }

    private void RestorePreviewRootToOrigin()
    {
        if (!hasPreviewOrigin)
            return;

        Transform previewRoot = GetPreviewRoot();
        if (previewRoot == null)
            return;

        previewRoot.SetPositionAndRotation(previewOriginPosition, previewOriginRotation);
        previewRoot.localScale = previewOriginScale;
    }

    internal Transform GetPreviewRoot()
    {
        return animator != null ? animator.transform : transform;
    }

    private void ApplyAnimatorRootMotionSetting()
    {
        if (animator == null)
            return;

        if (!hasCachedAnimatorRootMotion || cachedAnimator != animator)
        {
            RestoreAnimatorRootMotionSetting();
            cachedAnimator = animator;
            cachedAnimatorRootMotion = animator.applyRootMotion;
            hasCachedAnimatorRootMotion = true;
        }

        if (animator.applyRootMotion != applyRootMotion)
            animator.applyRootMotion = applyRootMotion;
    }

    private void RestoreAnimatorRootMotionSetting()
    {
        if (!hasCachedAnimatorRootMotion || cachedAnimator == null)
            return;

        cachedAnimator.applyRootMotion = cachedAnimatorRootMotion;
        cachedAnimator = null;
        hasCachedAnimatorRootMotion = false;
    }

    public void GetAnimationClips(List<AnimationClip> clips)
    {
        if (clip != null)
            clips.Add(clip);
    }

#if UNITY_EDITOR
    private void RegisterEditorUpdate()
    {
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;
        lastEditorUpdateTime = EditorApplication.timeSinceStartup;
    }

    private void UnregisterEditorUpdate()
    {
        EditorApplication.update -= OnEditorUpdate;
    }

    private void OnEditorUpdate()
    {
        if (Application.isPlaying ||
            !this ||
            !enabled ||
            !applyInEditMode)
        {
            UnregisterEditorUpdate();
            return;
        }

        if (!isPlaying ||
            !graph.IsValid())
        {
            lastEditorUpdateTime = EditorApplication.timeSinceStartup;
            return;
        }

        double currentTime = EditorApplication.timeSinceStartup;
        float deltaTime = Mathf.Min((float)(currentTime - lastEditorUpdateTime), 0.1f);
        lastEditorUpdateTime = currentTime;

        Evaluate(deltaTime);
        StopAtClipEndIfNeeded();

        SceneView.RepaintAll();
    }

#endif
}

#if UNITY_EDITOR
[CustomEditor(typeof(SoloRootMotionPreview))]
internal sealed class SoloRootMotionPreviewEditor : Editor
{
    private SerializedProperty animatorProperty;
    private SerializedProperty clipProperty;
    private SerializedProperty applyInEditModeProperty;
    private SerializedProperty applyRootMotionProperty;
    private SerializedProperty normalizedStartTimeProperty;
    private SerializedProperty speedProperty;
    private SerializedProperty stopOnDisableProperty;

    private void OnEnable()
    {
        animatorProperty = serializedObject.FindProperty("animator");
        clipProperty = serializedObject.FindProperty("clip");
        applyInEditModeProperty = serializedObject.FindProperty("applyInEditMode");
        applyRootMotionProperty = serializedObject.FindProperty("applyRootMotion");
        normalizedStartTimeProperty = serializedObject.FindProperty("normalizedStartTime");
        speedProperty = serializedObject.FindProperty("speed");
        stopOnDisableProperty = serializedObject.FindProperty("stopOnDisable");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(animatorProperty);
        EditorGUILayout.PropertyField(clipProperty);
        EditorGUILayout.PropertyField(applyInEditModeProperty);
        EditorGUILayout.PropertyField(applyRootMotionProperty);
        EditorGUILayout.PropertyField(normalizedStartTimeProperty);
        EditorGUILayout.PropertyField(speedProperty);
        EditorGUILayout.PropertyField(stopOnDisableProperty);

        serializedObject.ApplyModifiedProperties();

        var preview = (SoloRootMotionPreview)target;

        using (new EditorGUI.DisabledScope(preview.Clip == null || preview.Animator == null))
        {
            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Play"))
                    preview.Play();

                if (GUILayout.Button("Pause"))
                    preview.Pause();

                if (GUILayout.Button("Rewind"))
                    preview.Rewind();
            }

            if (GUILayout.Button("Reset Preview Origin"))
                preview.ResetPreviewOrigin();

            EditorGUI.BeginChangeCheck();
            float normalizedTime = EditorGUILayout.Slider("Normalized Time", preview.NormalizedTime, 0f, 1f);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(preview.GetPreviewRoot(), "Scrub Animation Preview");
                preview.NormalizedTime = normalizedTime;
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Length", preview.Length.ToString("0.###"));
        EditorGUILayout.LabelField("Looping", preview.IsLooping ? "True" : "False");
        EditorGUILayout.LabelField("Initialized", preview.IsInitialized ? "True" : "False");
    }
}
#endif
