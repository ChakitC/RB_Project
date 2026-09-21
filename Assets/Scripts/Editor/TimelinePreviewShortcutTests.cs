#if UNITY_EDITOR
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class TimelinePreviewShortcutTests
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

    [Test]
    public void DragMovesBothDirectionsAndClampsAtClipEnds()
    {
        Assert.That(SkillAnimationVfxEditorWindow.SpaceDragTime(.5f, -200, 1000), Is.EqualTo(.3f).Within(.0001f));
        Assert.That(SkillAnimationVfxEditorWindow.SpaceDragTime(.5f, 200, 1000), Is.EqualTo(.7f).Within(.0001f));
        Assert.That(SkillAnimationVfxEditorWindow.SpaceDragTime(.5f, -900, 1000), Is.Zero);
        Assert.That(SkillAnimationVfxEditorWindow.SpaceDragTime(.5f, 900, 1000), Is.EqualTo(1));
        Assert.That(SkillAnimationVfxEditorWindow.SpaceDragTime(.5f, 0, 0), Is.EqualTo(.5f));
    }

    [Test]
    public void BackwardScrubCrossesMainClipIntoCutsceneAndCanReturn()
    {
        var window = ScriptableObject.CreateInstance<SkillAnimationVfxEditorWindow>();
        try
        {
            Set(window, "_cutsceneSkillFraction", .25f);
            Call(window, "ScrubCombinedTimeline", .625f);
            Assert.That(Get<float>(window, "normalizedTime"), Is.EqualTo(.5f));
            Assert.That(Get<bool>(window, "_playheadInCutscene"), Is.False);
            Call(window, "ScrubCombinedTimeline", .125f);
            Assert.That(Get<bool>(window, "_playheadInCutscene"), Is.True);
            Assert.That(Get<float>(window, "_cutsceneNormalizedTime"), Is.EqualTo(.5f));
            Assert.That(Get<bool>(window, "_scrubSampleIsCutscene"), Is.True);
            Call(window, "ScrubCombinedTimeline", .25f);
            Assert.That(Get<bool>(window, "_playheadInCutscene"), Is.False);
            Assert.That(Get<float>(window, "normalizedTime"), Is.Zero);
            Assert.That(Get<bool>(window, "_scrubSampleIsCutscene"), Is.False);
            Set(window, "hitboxMode", true);
            Call(window, "ScrubCombinedTimeline", .125f);
            Assert.That(Get<float>(window, "normalizedTime"), Is.EqualTo(.125f));
            Assert.That(Get<bool>(window, "_playheadInCutscene"), Is.False);
        }
        finally { Object.DestroyImmediate(window); }
    }

    [Test]
    public void FocusLossAndSourceStopClearPendingSpaceTap()
    {
        var window = ScriptableObject.CreateInstance<SkillAnimationVfxEditorWindow>();
        try
        {
            Set(window, "spaceHeld", true); Set(window, "spaceUsedForScrub", true);
            Call(window, "OnLostFocus");
            Assert.That(Get<bool>(window, "spaceHeld"), Is.False);
            Set(window, "spaceHeld", true);
            Call(window, "StopPreview", true);
            Assert.That(Get<bool>(window, "spaceHeld"), Is.False);
            Assert.That(Get<bool>(window, "spaceUsedForScrub"), Is.False);
        }
        finally { Object.DestroyImmediate(window); }
    }

    static void Set(object target, string name, object value) => target.GetType().GetField(name, Hidden).SetValue(target, value);
    static T Get<T>(object target, string name) => (T)target.GetType().GetField(name, Hidden).GetValue(target);
    static void Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Hidden).Invoke(target, args);

    public static string Run()
    {
        var tests = new TimelinePreviewShortcutTests();
        tests.DragMovesBothDirectionsAndClampsAtClipEnds();
        tests.BackwardScrubCrossesMainClipIntoCutsceneAndCanReturn();
        tests.FocusLossAndSourceStopClearPendingSpaceTap();
        return "PASS 3 timeline shortcut regression tests";
    }
}
#endif
