using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates the project-level channels the dialogue stage needs: the <c>DialogueActor</c> layer that
/// separates dialogue clones from the gameplay view, and the named rendering layer that keeps world
/// lights off the clones and dialogue lights off the world.
///
/// Editor-only, idempotent, and safe to run on a project that already has them.
/// </summary>
public static class DialogueProjectSetup
{
    const string TagManagerPath = "ProjectSettings/TagManager.asset";
    const string DialogueRenderingLayerName = "Dialogue";

    [MenuItem("Tools/Dialogue/Set Up Project Layers")]
    public static void SetUpLayers()
    {
        bool changed = EnsureActorLayer(out int layerIndex);
        changed |= EnsureRenderingLayerName();

        if (changed)
            AssetDatabase.SaveAssets();

        Debug.Log(layerIndex >= 0
            ? $"[Dialogue] Layer '{DialogueLayers.ActorLayerName}' is at index {layerIndex}. " +
              $"Rendering layer {DialogueLayers.DialogueRenderingLayerIndex} is '{DialogueRenderingLayerName}'."
            : "[Dialogue] No free user layer slot; add 'DialogueActor' manually in Tags & Layers.");
    }

    /// <summary>Adds the DialogueActor layer to the first free user slot. Returns true when it wrote.</summary>
    public static bool EnsureActorLayer(out int layerIndex)
    {
        layerIndex = LayerMask.NameToLayer(DialogueLayers.ActorLayerName);
        if (layerIndex >= 0)
            return false;

        SerializedObject tagManager = LoadTagManager();
        if (tagManager == null)
            return false;

        SerializedProperty layers = tagManager.FindProperty("layers");
        if (layers == null)
            return false;

        // Slots 0-7 are Unity's built-ins; user layers start at 8.
        for (int i = 8; i < layers.arraySize; i++)
        {
            SerializedProperty slot = layers.GetArrayElementAtIndex(i);
            if (!string.IsNullOrEmpty(slot.stringValue))
                continue;

            slot.stringValue = DialogueLayers.ActorLayerName;
            tagManager.ApplyModifiedProperties();
            layerIndex = i;
            return true;
        }

        return false;
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
