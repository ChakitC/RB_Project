#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed partial class SkillAnimationVfxEditorWindow
{
    static bool CuesDiffer(IAnimationVfxTimelineSource source, List<AnimationVfxCue> cues)
    {
        if (cues == null) return false;
        if (source.CueCount != cues.Count) return true;
        for (int i = 0; i < cues.Count; i++)
            if (JsonUtility.ToJson(source.GetCue(i)) != JsonUtility.ToJson(cues[i])) return true;
        return false;
    }

    bool HasVfxChanges()
    {
        var source = GetSource();
        if (source == null || authoringTarget == null) return false;
        return !authoringTarget.TryGetTimelineVfxDraft(source, out var cues, out _) || CuesDiffer(source, cues);
    }

    bool HasPendingAuthoringChanges() =>
        (hitboxSession != null && hitboxSession.IsDirty) ||
        (blockSession != null && blockSession.IsDirty) || HasVfxChanges();

    void DrawSaveAllBar()
    {
        var source = GetSource();
        if (authoringTarget == null) return;
        using (new EditorGUILayout.HorizontalScope())
        using (new EditorGUI.DisabledScope(source == null || EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (DrawTintedButton("Save All Changes", new Color(.55f, .9f, .55f))) SaveAllWithDialog();
            GUILayout.Label(HasPendingAuthoringChanges() ? "Unsaved changes" : "Saved", EditorStyles.miniLabel, GUILayout.Width(105));
            if (DrawTintedButton("Load / Sync VFX Data", new Color(1f, .78f, .35f)))
            {
                authoringTarget.LoadTimelineVfxData(source);
                UpdateAuthoringDirtyState();
            }
        }
    }

    bool SaveAllWithDialog()
    {
        if (TrySaveAllChanges(out string error)) return true;
        EditorUtility.DisplayDialog("Save All Changes", error, "OK");
        return false;
    }

    // Preflight every pending section before writing any asset. VFX is captured
    // first but applied after Hitbox so its read does not invalidate that draft.
    internal bool TrySaveAllChanges(out string error)
    {
        var issues = new List<string>();
        var source = GetSource();
        if (source == null || authoringTarget == null || EditorApplication.isPlayingOrWillChangePlaymode)
        { error = "Choose a source in Edit Mode before saving."; return false; }
        if (!AssetDatabase.Contains(source.SourceAsset))
        { error = "Save the selected source as an asset first."; return false; }
        bool saveHitbox = hitboxSession != null && hitboxSession.IsDirty;
        bool saveBlock = blockSession != null && blockSession.IsDirty;
        if (saveHitbox)
        {
            if (hitboxSession.skill != source.SourceAsset) issues.Add("Hitbox draft belongs to another Skill / Step.");
            foreach (var issue in hitboxSession.ValidateSave(AnchorExists)) issues.Add("Hitbox: " + issue);
        }
        if (saveBlock)
        {
            if (blockSession.skill != source.SourceAsset) issues.Add("Block draft belongs to another Skill / Step.");
            if (!blockSession.ValidateSave(out var blockError)) issues.Add("Block: " + blockError);
        }
        if (!authoringTarget.TryGetTimelineVfxDraft(source, out var cues, out var vfxIssues))
            foreach (var issue in vfxIssues) issues.Add("VFX: " + issue);
        if (issues.Count > 0) { error = string.Join("\n", issues); return false; }

        if (saveHitbox && !hitboxSession.Save(out error, AnchorExists)) return false;
        if (saveBlock && !blockSession.Save(out error)) return false;
        if (CuesDiffer(source, cues)) { source.ReplaceCues(cues); source.Save(); }
        // Other timeline edits already save on mouse release; never flush unrelated assets.
        UpdateAuthoringDirtyState();
        BuildTimelineEvents(GetSource());
        hitboxIssues.Clear(); Repaint(); SceneView.RepaintAll();
        error = null;
        return true;
    }
}
#endif
