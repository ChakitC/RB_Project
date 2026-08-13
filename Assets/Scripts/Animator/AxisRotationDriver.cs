using UnityEngine;

#if ODIN_INSPECTOR
using Sirenix.OdinInspector;
#endif

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Drives one transform's rotation from another transform's rotation, remapping between axes with optional offset, clamp, and multiplier. Updates continuously in Edit Mode.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[DefaultExecutionOrder(10200)]
[AddComponentMenu("Animation/Axis Rotation Driver")]
public sealed class AxisRotationDriver : MonoBehaviour
{
    public enum Axis
    {
        X,
        Y,
        Z,
    }

#if ODIN_INSPECTOR
    [Title("Source")]
    [InfoBox("Source ยังไม่ถูกกำหนด — component นี้จะไม่ทำอะไร", InfoMessageType.Warning, nameof(IsSourceMissing))]
#else
    [Header("Source")]
#endif
    [SerializeField] private Transform source;

    [SerializeField] private Axis sourceAxis = Axis.Y;
    [SerializeField] private Axis targetAxis = Axis.Z;

#if ODIN_INSPECTOR
    [Title("Mapping")]
#else
    [Header("Mapping")]
#endif
    [SerializeField] private bool maintainOffset = true;
    [SerializeField] private float multiplier = 1f;

    [SerializeField] private bool useClamp = true;

#if ODIN_INSPECTOR
    [HorizontalGroup("ClampRange")]
    [LabelWidth(70)]
    [ShowIf(nameof(useClamp))]
#endif
    [SerializeField] private float minAngle = -180f;

#if ODIN_INSPECTOR
    [HorizontalGroup("ClampRange")]
    [LabelWidth(70)]
    [ShowIf(nameof(useClamp))]
#endif
    [SerializeField] private float maxAngle = 180f;

#if ODIN_INSPECTOR
    [Title("Preview")]
#else
    [Header("Preview")]
#endif
    [SerializeField] private bool previewInEditMode = true;

    [SerializeField, HideInInspector] private Quaternion sourceRest = Quaternion.identity;
    [SerializeField, HideInInspector] private Quaternion targetRest = Quaternion.identity;
    [SerializeField, HideInInspector] private bool hasRest;

    private AxisRotationDriver _upstream;
    private bool _isEvaluating;
    private bool _loggedInvalidSourceError;

#if ODIN_INSPECTOR
    private bool IsSourceMissing => source == null;
    private bool CanCaptureRestPose => source != null;
#endif

    private Quaternion SourceBaseline => maintainOffset ? sourceRest : Quaternion.identity;
    private Quaternion TargetBaseline => maintainOffset ? targetRest : Quaternion.identity;

#if ODIN_INSPECTOR
    [Button("Capture Rest Pose")]
    [EnableIf(nameof(CanCaptureRestPose))]
#endif
    [ContextMenu("Capture Rest Pose")]
    public void CaptureRestPose()
    {
        if (source == null)
            return;

#if UNITY_EDITOR
        Undo.RecordObject(this, "Capture Rest Pose");
#endif

        CaptureRestPoseInternal();

#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif

        Evaluate();
    }

    public void Evaluate()
    {
        if (_isEvaluating)
            return;

        _isEvaluating = true;
        try
        {
            if (!ValidateConfig())
                return;

            _upstream?.Evaluate();
            ApplyRotation();
        }
        finally
        {
            _isEvaluating = false;
        }
    }

    private void OnEnable()
    {
        CacheUpstream();
        _loggedInvalidSourceError = false;

#if UNITY_EDITOR
        if (!Application.isPlaying && previewInEditMode)
            RegisterEditorTick();
#endif
    }

    private void LateUpdate()
    {
        Evaluate();
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        UnregisterEditorTick();

        if (!Application.isPlaying && hasRest)
            transform.localRotation = targetRest;
#endif
    }

    private void OnValidate()
    {
        CacheUpstream();
        _loggedInvalidSourceError = false;
        ValidateConfig();

        if (!hasRest && source != null)
            CaptureRestPoseInternal();

#if UNITY_EDITOR
        if (Application.isPlaying)
            return;

        if (previewInEditMode && enabled)
        {
            RegisterEditorTick();
            Evaluate();
        }
        else
        {
            UnregisterEditorTick();
            if (hasRest)
                transform.localRotation = targetRest;
        }
#endif
    }

    private void CacheUpstream()
    {
        _upstream = source != null ? source.GetComponent<AxisRotationDriver>() : null;
    }

    private bool ValidateConfig()
    {
        if (source == null)
            return false;

        if (source == transform)
        {
            LogInvalidSourceOnce("Source ห้ามเป็น Transform ของตัวเอง");
            return false;
        }

        if (source.IsChildOf(transform))
        {
            LogInvalidSourceOnce("Source ห้ามเป็นลูกของ Target (จะเกิด feedback loop)");
            return false;
        }

        return true;
    }

    private void LogInvalidSourceOnce(string message)
    {
        if (_loggedInvalidSourceError)
            return;

        _loggedInvalidSourceError = true;
        Debug.LogError($"[AxisRotationDriver] {message}", this);
    }

    private void CaptureRestPoseInternal()
    {
        sourceRest = source != null ? source.localRotation : Quaternion.identity;
        targetRest = transform.localRotation;
        hasRest = true;
    }

    private void ApplyRotation()
    {
        float raw = ReadSourceAngle();
        float driven = raw * multiplier;
        if (useClamp)
            driven = Mathf.Clamp(driven, minAngle, maxAngle);

        transform.localRotation = TargetBaseline * Quaternion.AngleAxis(driven, AxisVector(targetAxis));
    }

    private float ReadSourceAngle()
    {
        Quaternion delta = Quaternion.Inverse(SourceBaseline) * source.localRotation;
        Vector3 axis = AxisVector(sourceAxis);
        Vector3 p = Vector3.Project(new Vector3(delta.x, delta.y, delta.z), axis);
        float raw = 2f * Mathf.Atan2(Vector3.Dot(p, axis), delta.w) * Mathf.Rad2Deg;
        return Mathf.DeltaAngle(0f, raw);
    }

    private static Vector3 AxisVector(Axis axis)
    {
        switch (axis)
        {
            case Axis.X: return Vector3.right;
            case Axis.Y: return Vector3.up;
            default: return Vector3.forward;
        }
    }

#if UNITY_EDITOR
    private void RegisterEditorTick()
    {
        EditorApplication.update -= EditorTick;
        EditorApplication.update += EditorTick;
    }

    private void UnregisterEditorTick()
    {
        EditorApplication.update -= EditorTick;
    }

    private void EditorTick()
    {
        if (this == null)
        {
            UnregisterEditorTick();
            return;
        }

        if (Application.isPlaying || !previewInEditMode || !enabled)
        {
            UnregisterEditorTick();
            return;
        }

        Evaluate();
        SceneView.RepaintAll();
    }
#endif
}

#if UNITY_EDITOR && !ODIN_INSPECTOR
[CustomEditor(typeof(AxisRotationDriver))]
internal sealed class AxisRotationDriverEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty sourceProp = serializedObject.FindProperty("source");
        EditorGUILayout.PropertyField(sourceProp);

        if (sourceProp.objectReferenceValue == null)
            EditorGUILayout.HelpBox("Source ยังไม่ถูกกำหนด — component นี้จะไม่ทำอะไร", MessageType.Warning);

        EditorGUILayout.PropertyField(serializedObject.FindProperty("sourceAxis"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("targetAxis"));

        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("maintainOffset"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("multiplier"));

        SerializedProperty useClampProp = serializedObject.FindProperty("useClamp");
        EditorGUILayout.PropertyField(useClampProp);

        using (new EditorGUI.DisabledScope(!useClampProp.boolValue))
        {
            SerializedProperty minProp = serializedObject.FindProperty("minAngle");
            SerializedProperty maxProp = serializedObject.FindProperty("maxAngle");
            float minValue = minProp.floatValue;
            float maxValue = maxProp.floatValue;
            EditorGUILayout.MinMaxSlider("Angle Range", ref minValue, ref maxValue, -180f, 180f);
            minProp.floatValue = minValue;
            maxProp.floatValue = maxValue;
        }

        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("previewInEditMode"));

        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        var driver = (AxisRotationDriver)target;
        using (new EditorGUI.DisabledScope(sourceProp.objectReferenceValue == null))
        {
            if (GUILayout.Button("Capture Rest Pose"))
                driver.CaptureRestPose();
        }
    }
}
#endif
