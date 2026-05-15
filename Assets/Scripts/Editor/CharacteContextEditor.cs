#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CharacteContext), true)]
[CanEditMultipleObjects]
public class CharacteContextEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();
        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        if (GUILayout.Button("Find Ref"))
        {
            ResolveSelectedReferences();
            serializedObject.Update();
        }
    }

    void ResolveSelectedReferences()
    {
        foreach (Object selected in targets)
        {
            if (selected is not CharacteContext context)
                continue;

            Undo.RecordObject(context, "Find CharacteContext References");
            context.ResolveReferences();
            EditorUtility.SetDirty(context);

            if (PrefabUtility.IsPartOfPrefabInstance(context))
                PrefabUtility.RecordPrefabInstancePropertyModifications(context);
        }
    }
}
#endif
