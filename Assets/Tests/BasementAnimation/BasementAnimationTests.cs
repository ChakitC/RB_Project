using System;
using System.Reflection;
using NUnit.Framework;

public sealed class BasementAnimationTests
{
    object fixture;
    [SetUp] public void Setup() => fixture = Activator.CreateInstance(Type.GetType("BasementAnimationTestFixture, Assembly-CSharp-Editor", true));
    [TearDown] public void Cleanup() => Invoke("Cleanup");
    void Invoke(string name)
    {
        try { fixture.GetType().GetMethod(name).Invoke(fixture, null); }
        catch (TargetInvocationException e) { throw e.InnerException; }
    }
    [Test] public void PlaybackAndRepeatedCommands() => Invoke(nameof(PlaybackAndRepeatedCommands));
    [Test] public void FormationStatusAndSubmenuRemainSeparate() => Invoke(nameof(FormationStatusAndSubmenuRemainSeparate));
    [Test] public void MenuPoseAndSelectionRestore() => Invoke(nameof(MenuPoseAndSelectionRestore));
    [Test] public void MenuLoopAndMissingEntryPreservePlayback() => Invoke(nameof(MenuLoopAndMissingEntryPreservePlayback));
    [Test] public void MenuCameraBakeIncludesParentsAndRejectsAmbiguity() => Invoke(nameof(MenuCameraBakeIncludesParentsAndRejectsAmbiguity));
    [Test] public void ProductionMenuPresetsMatchSources() => Invoke(nameof(ProductionMenuPresetsMatchSources));
    [Test] public void NonLoopPickupHoldsUntilRelease() => Invoke(nameof(NonLoopPickupHoldsUntilRelease));
    [Test] public void TransitionSettingsDrivePlayback() => Invoke(nameof(TransitionSettingsDrivePlayback));
    [Test] public void SharedWeaponsMountAndClearWithModel() => Invoke(nameof(SharedWeaponsMountAndClearWithModel));
    [Test] public void MissingClipDoesNotBlockSelection() => Invoke(nameof(MissingClipDoesNotBlockSelection));
    [Test] public void AllEightPrefabBindingsAndLegacyTiming() => Invoke(nameof(AllEightPrefabBindingsAndLegacyTiming));
    [Test] public void SharedAiresMenuTestProfileBindings() => Invoke(nameof(SharedAiresMenuTestProfileBindings));
    [Test] public void PreviewRebuildBindsNewModel() => Invoke(nameof(PreviewRebuildBindsNewModel));
    [Test] public void InitialPickupAndSameModelRefreshPreservePlayback() => Invoke(nameof(InitialPickupAndSameModelRefreshPreservePlayback));
    [Test] public void MenuSwitchNeverRestartsIdleAndRestoresOnce() => Invoke(nameof(MenuSwitchNeverRestartsIdleAndRestoresOnce));
    [Test] public void SharedPresentationSurvivesSlotCleanup() => Invoke(nameof(SharedPresentationSurvivesSlotCleanup));
    [Test] public void MissingAnimatorPathKeepsModelAndSelection() => Invoke(nameof(MissingAnimatorPathKeepsModelAndSelection));
}
