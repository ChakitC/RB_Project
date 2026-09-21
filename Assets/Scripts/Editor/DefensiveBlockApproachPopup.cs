#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

// Edits the same transient draft as the Timeline; Save All remains the commit point.
public sealed class DefensiveBlockApproachPopup : PopupWindowContent
{
    readonly DefensiveBlockTimelineSession draft;
    readonly Action changed;

    DefensiveBlockApproachPopup(DefensiveBlockTimelineSession draft, Action changed)
    { this.draft = draft; this.changed = changed; }

    public static void Show(DefensiveBlockTimelineSession draft, Action changed)
    {
        PopupWindow.Show(new Rect(Event.current != null ? Event.current.mousePosition : Vector2.zero, Vector2.one),
            new DefensiveBlockApproachPopup(draft, changed));
    }

    public override Vector2 GetWindowSize() => new Vector2(310, 132);

    public override void OnGUI(Rect rect)
    {
        if (draft == null || Application.isPlaying) { editorWindow.Close(); return; }
        GUILayout.Label("Timed Approach", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        float duration = EditorGUILayout.FloatField("Duration (seconds)", draft.timedApproachSeconds);
        float distance = EditorGUILayout.FloatField("Stand-off distance", draft.approachStandOff);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(draft, "Edit Timed Approach");
            draft.timedApproachSeconds = duration;
            draft.approachStandOff = distance;
            changed?.Invoke();
        }
        EditorGUILayout.HelpBox(draft.ValidationError ?? "Changes are saved with Save All in the Timeline.",
            draft.ValidationError == null ? MessageType.Info : MessageType.Warning);
    }
}
#endif
