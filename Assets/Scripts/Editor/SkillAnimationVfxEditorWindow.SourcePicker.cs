#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public sealed partial class SkillAnimationVfxEditorWindow
{
    AnimationVfxSourceAssetDropdown sourceAssetDropdown;

    void DrawSourceAssetPicker(bool hitboxesOnly)
    {
        var target = authoringTarget;
        var current = target.TimelineSourceAsset;
        var rect = EditorGUI.PrefixLabel(EditorGUILayout.GetControlRect(), new GUIContent("Source Asset"));
        var pingRect = new Rect(rect.xMax - 40, rect.y, 40, rect.height);
        rect.xMax = pingRect.xMin - 2;
        var content = current != null
            ? new GUIContent(current is SkillGemDefinition skill && !string.IsNullOrWhiteSpace(skill.SkillDefinitionDisplayName)
                ? skill.SkillDefinitionDisplayName : current.name,
                AssetDatabase.GetCachedIcon(AssetDatabase.GetAssetPath(current)), AssetDatabase.GetAssetPath(current))
            : new GUIContent("Choose source asset...");

        void SelectSource(ScriptableObject asset)
        {
            // A picker callback may arrive after the character or mode has changed.
            if (this == null || target == null || authoringTarget != target || hitboxMode != hitboxesOnly) return;
            if (asset != null && !AnimationVfxSourceAssetDropdown.Supports(asset, hitboxesOnly)) return;
            if (asset == authoringTarget.TimelineSourceAsset) return;
            var entries = AnimationVfxTimelineSourceFactory.GetEntries(asset);
            SetSourceSelection(asset, entries.Count > 0 ? entries[0].Id : "main");
        }

        var evt = Event.current;
        if (rect.Contains(evt.mousePosition) && (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform))
        {
            var dragged = DragAndDrop.objectReferences;
            bool valid = GUI.enabled && dragged.Length == 1 && EditorUtility.IsPersistent(dragged[0]) &&
                AnimationVfxSourceAssetDropdown.Supports(dragged[0], hitboxesOnly);
            DragAndDrop.visualMode = valid ? DragAndDropVisualMode.Link : DragAndDropVisualMode.Rejected;
            if (valid && evt.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                SelectSource((ScriptableObject)dragged[0]);
            }
            evt.Use();
        }
        if (EditorGUI.DropdownButton(rect, content, FocusType.Keyboard))
        {
            sourceAssetDropdown = new AnimationVfxSourceAssetDropdown(hitboxesOnly, SelectSource);
            sourceAssetDropdown.Show(rect);
        }
        using (new EditorGUI.DisabledScope(current == null))
            if (GUI.Button(pingRect, new GUIContent("Ping", "Locate the current source in the Project window"), EditorStyles.miniButton))
                EditorGUIUtility.PingObject(current);
    }
}
#endif
