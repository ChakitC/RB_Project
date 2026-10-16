#if UNITY_EDITOR
using System;
using System.Linq;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BasementMenuCameraPresetSO))]
public sealed class BasementMenuCameraPresetEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("sourceFbx"));
        var preset = (BasementMenuCameraPresetSO)target;
        var source = serializedObject.FindProperty("sourceFbx").objectReferenceValue as GameObject;
        var cameras = source != null ? source.GetComponentsInChildren<Camera>(true) : Array.Empty<Camera>();
        var path = serializedObject.FindProperty("cameraPath");
        var paths = cameras.Select(c => AnimationUtility.CalculateTransformPath(c.transform, source.transform)).ToArray();
        var labels = paths.Select(p => string.IsNullOrEmpty(p) ? "(Root Camera)" : p).ToArray();
        int current = Array.IndexOf(paths, path.stringValue);
        if (cameras.Length == 1 && current < 0) { current = 0; path.stringValue = paths[0]; }
        int selected = EditorGUILayout.Popup("Camera", current + 1, new[] { "Select a Camera" }.Concat(labels).ToArray()) - 1;
        if (selected >= 0) path.stringValue = paths[selected];
        serializedObject.ApplyModifiedProperties();
        using (new EditorGUI.DisabledScope(EditorApplication.isPlaying || selected < 0))
            if (GUILayout.Button("Bake Camera")) Bake(preset);
        EditorGUILayout.HelpBox("Export a static camera relative to the character origin. Bake reads Unity's imported hierarchy and lens. Reimport does not overwrite this preset.", MessageType.Info);
        DrawPropertiesExcluding(serializedObject, "m_Script", "sourceFbx", "cameraPath");
        serializedObject.ApplyModifiedProperties();
    }

    public static void Bake(BasementMenuCameraPresetSO preset)
    {
        if (preset == null || preset.sourceFbx == null) throw new ArgumentException("Assign the camera FBX.");
        var root = preset.sourceFbx.transform;
        var cameras = preset.sourceFbx.GetComponentsInChildren<Camera>(true);
        var matches = cameras.Where(c => AnimationUtility.CalculateTransformPath(c.transform, root) == preset.cameraPath).ToArray();
        Camera camera;
        if (matches.Length == 1) camera = matches[0];
        else if (cameras.Length == 1 && string.IsNullOrEmpty(preset.cameraPath)) camera = cameras[0];
        else throw new ArgumentException("Select exactly one Camera path in the source FBX.");

        Undo.RecordObject(preset, "Bake Basement Menu Camera");
        // The imported model root is the common authoring origin. Include every intermediate transform.
        preset.position = root.InverseTransformPoint(camera.transform.position);
        preset.rotation = Quaternion.Inverse(root.rotation) * camera.transform.rotation;
        preset.lens = LensSettings.FromCamera(camera);
        preset.lens.ModeOverride = camera.orthographic ? LensSettings.OverrideModes.Orthographic :
            camera.usePhysicalProperties ? LensSettings.OverrideModes.Physical : LensSettings.OverrideModes.Perspective;
        preset.cameraPath = AnimationUtility.CalculateTransformPath(camera.transform, root);
        EditorUtility.SetDirty(preset);
    }
}
#endif
