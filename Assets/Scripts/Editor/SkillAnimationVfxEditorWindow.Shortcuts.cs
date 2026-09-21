#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public sealed partial class SkillAnimationVfxEditorWindow
{
    bool spaceHeld;
    bool spaceUsedForScrub;
    bool spaceMouseReleasePending;
    bool spaceMouseButtonHeld;
    float spaceScrubStartX;
    float spaceScrubStartTime;
    float spaceScrubWidth = 1f;

    void HandlePreviewShortcuts()
    {
        var current = Event.current;
        if (focusedWindow != this || EditorApplication.isPlayingOrWillChangePlaymode)
        { CancelSpaceGesture(); return; }

        // Releasing Space before the mouse ends the gesture. Do not fall through
        // into ordinary absolute scrubbing while that same mouse press is held.
        if (spaceMouseReleasePending && current.button == 0 &&
            (current.type == EventType.MouseDrag || current.type == EventType.MouseUp))
        {
            if (current.type == EventType.MouseUp) spaceMouseReleasePending = false;
            current.Use(); return;
        }

        if (current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape && spaceHeld)
        {
            bool hadCapture = spaceMouseButtonHeld;
            CancelSpaceGesture(); spaceMouseReleasePending = hadCapture;
            current.Use(); return;
        }

        if (current.keyCode == KeyCode.Space && current.type == EventType.KeyDown)
        {
            if (!spaceHeld && (EditorGUIUtility.editingTextField || GUIUtility.hotControl != 0 ||
                current.alt || current.control || current.command || current.shift || !CanControlPreview())) return;
            if (!spaceHeld)
            {
                spaceScrubStartX = GUIUtility.GUIToScreenPoint(current.mousePosition).x;
                spaceScrubStartTime = CombinedPreviewTime();
            }
            spaceHeld = true; // Repeated KeyDown events never reset the drag origin or toggle playback.
            current.Use();
        }
        else if (current.keyCode == KeyCode.Space && current.type == EventType.KeyUp && spaceHeld)
        {
            bool toggle = !spaceUsedForScrub && !EditorGUIUtility.editingTextField && CanControlPreview();
            bool hadCapture = spaceMouseButtonHeld;
            CancelSpaceGesture();
            spaceMouseReleasePending = hadCapture;
            if (toggle)
            {
                if (isPlaying) PausePreview();
                else PlayPreview(GetPreviewAnimator(), GetClip(GetSource()));
                Repaint();
            }
            current.Use();
        }

        if (!spaceHeld) return;
        if (current.type == EventType.MouseMove || (current.type == EventType.MouseDrag && current.button == 0))
        {
            float delta = GUIUtility.GUIToScreenPoint(current.mousePosition).x - spaceScrubStartX;
            // Ignore tiny pointer jitter during a Space tap.
            if (!spaceUsedForScrub && Mathf.Abs(delta) < 3f) { current.Use(); return; }
            spaceUsedForScrub = true;
            PausePreview();
            ScrubCombinedTimeline(SpaceDragTime(spaceScrubStartTime, delta, spaceScrubWidth));
            current.Use();
        }
        if (current.type == EventType.MouseDown || current.type == EventType.MouseUp)
        {
            if (current.button == 0) spaceMouseButtonHeld = current.type == EventType.MouseDown;
            spaceUsedForScrub = true;
            current.Use(); // Optional clicks while holding Space must never edit markers.
        }
    }

    bool CanControlPreview()
    {
        var animator = GetPreviewAnimator();
        return GetClip(GetSource()) != null && animator != null && animator.gameObject.activeInHierarchy;
    }

    // Both views provide their ruler width; input is handled before any GUI controls.
    void BeginSpaceScrub(Rect contentArea)
    {
        if (Event.current.type == EventType.Repaint && !spaceHeld)
            spaceScrubWidth = Mathf.Max(1f, contentArea.width);
    }

    internal static float SpaceDragTime(float start, float deltaX, float width) =>
        Mathf.Clamp01(start + deltaX / Mathf.Max(1f, width));

    float CombinedPreviewTime() => hitboxMode || _cutsceneSkillFraction <= 0f ? normalizedTime :
        _playheadInCutscene ? _cutsceneNormalizedTime * _cutsceneSkillFraction :
        _cutsceneSkillFraction + normalizedTime * (1f - _cutsceneSkillFraction);

    void ScrubCombinedTimeline(float time)
    {
        if (!hitboxMode && _cutsceneSkillFraction > 0f && time < _cutsceneSkillFraction)
            ScrubToCutscene(time / _cutsceneSkillFraction);
        else
        {
            _playheadInCutscene = false;
            ScrubTo(hitboxMode ? time : (time - _cutsceneSkillFraction) / Mathf.Max(.0001f, 1f - _cutsceneSkillFraction));
        }
    }

    void CancelSpaceGesture()
    {
        spaceMouseButtonHeld = false;
        spaceHeld = spaceUsedForScrub = false;
        spaceMouseReleasePending = false;
    }

    void OnLostFocus() => CancelSpaceGesture();
}
#endif
