#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PlayerFullscreenEffectController))]
[CanEditMultipleObjects]
public class PlayerFullscreenEffectControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Heal"))
                PreviewHeal();

            if (GUILayout.Button("Perfect Dodge"))
                PreviewPerfectDodge();
        }
    }

    void PreviewHeal()
    {
        foreach (Object selected in targets)
        {
            if (selected is not PlayerFullscreenEffectController controller)
                continue;

            bool played = controller.PlayHeal(1f, 1f, 1f);
            if (!played)
                Debug.LogWarning("[PlayerFullscreenEffectController] Heal preview failed. Assign a screen effect prefab and target camera/MainCamera.", controller);
        }
    }

    void PreviewPerfectDodge()
    {
        foreach (Object selected in targets)
        {
            if (selected is not PlayerFullscreenEffectController controller)
                continue;

            bool played = controller.PlayPerfectDodge(Vector3.forward, 0.35f, 0.2f);
            if (!played)
                Debug.LogWarning("[PlayerFullscreenEffectController] Perfect Dodge preview failed. Assign a screen effect prefab and target camera/MainCamera.", controller);
        }
    }
}
#endif
