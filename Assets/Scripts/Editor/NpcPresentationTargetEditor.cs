using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(NpcPresentationTarget))]
public sealed class NpcPresentationTargetEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Shop Presentation Preview", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Opens a 16:9 preview rendered by a real Camera using this target's pose and field of view. " +
            "The dark guide represents the runtime Shop UI area.",
            MessageType.Info);

        if (GUILayout.Button("Open Camera Preview"))
            NpcPresentationCameraPreviewWindow.Open((NpcPresentationTarget)target);
    }
}
