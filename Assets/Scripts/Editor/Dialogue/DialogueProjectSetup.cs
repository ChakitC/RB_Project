using UnityEditor;
using UnityEngine;

/// <summary>
/// Names the rendering layer the dialogue stage runs its lights on, so an author looking at a light
/// or a renderer can see which channel is which.
///
/// The stage used to need a dedicated Unity layer as well; it does not any more — see
/// <see cref="DialogueLayers"/> for why clones sit on layer 0 with everything else.
///
/// Editor-only, idempotent, and safe to run on a project that already has this.
/// </summary>
public static class DialogueProjectSetup
{
    const string TagManagerPath = "ProjectSettings/TagManager.asset";
    const string DialogueRenderingLayerName = "Dialogue";

    [MenuItem("Tools/Dialogue/Set Up Project Layers")]
    public static void SetUpLayers()
    {
        if (EnsureRenderingLayerName())
            AssetDatabase.SaveAssets();

        Debug.Log(
            $"[Dialogue] Rendering layer {DialogueLayers.DialogueRenderingLayerIndex} is " +
            $"'{DialogueRenderingLayerName}'.");
    }

    /// <summary>Names the reserved rendering layer so authors can see which channel dialogue uses.</summary>
    public static bool EnsureRenderingLayerName()
    {
        SerializedObject tagManager = LoadTagManager();
        SerializedProperty renderingLayers = tagManager?.FindProperty("m_RenderingLayers");
        if (renderingLayers == null ||
            renderingLayers.arraySize <= DialogueLayers.DialogueRenderingLayerIndex)
        {
            return false;
        }

        SerializedProperty slot =
            renderingLayers.GetArrayElementAtIndex(DialogueLayers.DialogueRenderingLayerIndex);

        if (slot.stringValue == DialogueRenderingLayerName)
            return false;

        slot.stringValue = DialogueRenderingLayerName;
        tagManager.ApplyModifiedProperties();
        return true;
    }

    static SerializedObject LoadTagManager()
    {
        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(TagManagerPath);
        if (assets == null || assets.Length == 0)
        {
            Debug.LogError($"[Dialogue] Could not load '{TagManagerPath}'.");
            return null;
        }

        return new SerializedObject(assets[0]);
    }
}
