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

    public override Vector2 GetWindowSize() => new Vector2(390, draft.mode == DefensiveBlockMode.Cooperative ? 420 : 202);

    public override void OnGUI(Rect rect)
    {
        if (draft == null || Application.isPlaying) { editorWindow.Close(); return; }
        GUILayout.Label(draft.mode == DefensiveBlockMode.Cooperative ? "Cooperative Block" : "Timed Approach", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        float duration = EditorGUILayout.FloatField("Duration (seconds)", draft.timedApproachSeconds);
        float distance = EditorGUILayout.FloatField("Stand-off distance", draft.approachStandOff);
        int selected = Mathf.Clamp(draft.selectedWindow, 0, draft.WindowCount - 1);
        var window = draft.windows[selected];
        bool useImpact = EditorGUILayout.Toggle($"Window {selected + 1} Impact", window.useImpactMarker);
        float impact = window.impactNormalized;
        using (new EditorGUI.DisabledScope(!useImpact))
            impact = EditorGUILayout.Slider("Impact normalized time", impact, 0f, 1f);
        EditorGUILayout.LabelField("Off uses the original approach timing.", EditorStyles.miniLabel);
        var meter = draft.cooperative.Copy();
        if (draft.mode == DefensiveBlockMode.Cooperative)
        {
            meter.duration = EditorGUILayout.FloatField("Struggle seconds", meter.duration);
            meter.initialMeter = EditorGUILayout.Slider("Initial meter", meter.initialMeter, 0f, 1f);
            meter.gainPerPress = EditorGUILayout.FloatField("Gain per press", meter.gainPerPress);
            meter.drainPerSecond = EditorGUILayout.FloatField("Drain per second", meter.drainPerSecond);
            meter.worldScale = EditorGUILayout.Slider("World scale", meter.worldScale, .05f, 1f);
            meter.enemyStruggleClip = (AnimationClip)EditorGUILayout.ObjectField("Enemy Struggle Clip", meter.enemyStruggleClip, typeof(AnimationClip), false);
            meter.enemyStruggleFadeSeconds = EditorGUILayout.FloatField("Enemy Struggle Fade", meter.enemyStruggleFadeSeconds);
            EditorGUILayout.LabelField("No clip: hold enemy impact pose.", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("Ends the whole request; no Continue Skill.", EditorStyles.miniLabel);
        }
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(draft, "Edit Timed Approach");
            draft.timedApproachSeconds = duration;
            draft.approachStandOff = distance;
            draft.cooperative = meter;
            window.useImpactMarker = useImpact;
            window.impactNormalized = impact;
            changed?.Invoke();
        }
        EditorGUILayout.HelpBox(draft.ValidationError ?? "Changes are saved with Save All in the Timeline.",
            draft.ValidationError == null ? MessageType.Info : MessageType.Warning);
    }
}
#endif
