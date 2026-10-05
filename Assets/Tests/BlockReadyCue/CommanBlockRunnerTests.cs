using System;
using NUnit.Framework;

// Bridge the default Editor assembly fixture into Unity's Test Runner assembly.
public sealed class CommanBlockRunnerTests
{
    [TestCase(false)]
    [TestCase(true)]
    public void MovementAndRotationLock(bool disable)
    {
        var type = Type.GetType("CommanBlockAnimationTests, Assembly-CSharp-Editor", true);
        var fixture = Activator.CreateInstance(type);
        try
        {
            type.GetMethod("SetUp").Invoke(fixture, null);
            type.GetMethod("MovementAndRotationLockReleasesWithoutClearingOtherOwners").Invoke(fixture, new object[] { disable });
        }
        finally { type.GetMethod("TearDown").Invoke(fixture, null); }
    }
    [TestCase("TimelineReentrySkipsRemainingOldSubscribers")]
    [TestCase("ThrowingAuthoredSubscriberDoesNotDropCompletion")]
    [TestCase("SlotVfxCleanupIsIdempotentAndIsolatedBetweenActors")]
    [TestCase("PlaybackSlotReplayPreservesTimingAndRejectsOldCallback")]
    [TestCase("StaleSlotStopCannotClearAnotherSlotOnTheSameState")]
    [TestCase("AuthoredEndCanReplaySlotWithoutCompletingTheNewPlayback")]
    [TestCase("SharedTransitionDoesNotShareRuntimeCallbacksBetweenActors")]
    [TestCase("StartsImmediatelyAndFinishesWithoutTakingLocomotion")]
    [TestCase("MissingClipDoesNotStartGesture")]
    [TestCase("SelfGuardTakesPriorityOverCommandGesture")]
    [TestCase("RomaProfileUsesRequestedAsset")]
    [TestCase("AuthoredEndTimeEndsGestureBeforeClipLength")]
    [TestCase("ReplayedGestureKeepsAuthoredEndTimeAfterCleanup")]
    [TestCase("ReplayedLocomotionReactionKeepsAuthoredEndTimeAfterCancel")]
    [TestCase("UpperActionStartsDuringFullBodyFadeWithoutChangingMasks")]
    [TestCase("ReplayingGestureDuringFadeCancelsPreviousExit")]
    [TestCase("PairOffsetsBlendBothActionsAndRespectFullBodyCoverage")]
    [TestCase("PairOffsetsUseNewBonePoseAndDoNotAccumulateBetweenEvaluations")]
    [TestCase("SlidePairOffsetsUseSavedClipProfileAndRestoreLocomotion")]
    [TestCase("SlidePairOffsetsPreferExplicitKeysAndSupportPulseFallback")]
    [TestCase("PairOffsetFadeUsesEvaluatedStatesThroughInterruptionAndMissingProfiles")]
    [TestCase("PairOffsetEvaluatorIsOrderIndependentAndKeepsIdentityWeight")]
    [TestCase("PairOffsetMixerReadsActualChildWeights")]
    [TestCase("PairOffsetPreviewBlendsSavedProfilesAndProtectsCapture")]
    [TestCase("RomaPairOffsetsFollowActualRigDuringSlideFade")]
    [TestCase("AnimatedBoneKeepsCurrentPoseThroughOffsetsCoverageAndRebind")]
    [TestCase("TemporaryPoseSurvivesFadeAndIsDestroyedAfterwards")]
    [TestCase("ReclaimedTemporaryPoseIsNotDestroyedByOldCleanup")]
    [TestCase("DisableClearsFadesAndTemporaryPoses")]
    [TestCase("BlockExitKeepsSampledPoseFrozen")]
    [TestCase("FullBodyPoseBlendsAfterEndWithExcludedUpperMaskBone")]
    [TestCase("RebindReleasesOldPresentationAndAllowsNewGesture")]
    public void Run(string method)
    {
        var type = Type.GetType("CommanBlockAnimationTests, Assembly-CSharp-Editor", true);
        var fixture = Activator.CreateInstance(type);
        try
        {
            type.GetMethod("SetUp").Invoke(fixture, null);
            type.GetMethod(method).Invoke(fixture, null);
        }
        finally { type.GetMethod("TearDown").Invoke(fixture, null); }
    }
}
