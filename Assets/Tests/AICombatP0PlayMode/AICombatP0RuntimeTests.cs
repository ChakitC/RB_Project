using System;
using System.Collections;
using UnityEngine.TestTools;

public sealed class AICombatP0RuntimeTests
{
    [UnityTest] public IEnumerator NavigationAndRecovery() => Run(nameof(NavigationAndRecovery));
    [UnityTest] public IEnumerator ReloadAndAbort() => Run(nameof(ReloadAndAbort));
    [UnityTest] public IEnumerator LastObservationIsFrozenAndConsumedOnce() => Run(nameof(LastObservationIsFrozenAndConsumedOnce));
    [UnityTest] public IEnumerator LiveTreeSharesAssetButNotActorState() => Run(nameof(LiveTreeSharesAssetButNotActorState));
    [UnityTest] public IEnumerator FiringCandidateFailuresAndRetarget() => Run(nameof(FiringCandidateFailuresAndRetarget));
    [UnityTest] public IEnumerator RealReloadCompletesWithoutHeldFire() => Run(nameof(RealReloadCompletesWithoutHeldFire));
    [UnityTest] public IEnumerator WorldSlowPauseAndMovementHandoff() => Run(nameof(WorldSlowPauseAndMovementHandoff));
    [UnityTest] public IEnumerator ExistingMovementOwnershipRegression() => Run(nameof(ExistingMovementOwnershipRegression));
    [UnityTest, PrebuildSetup(typeof(AIContextGraphPrebuild)), PostBuildCleanup(typeof(AIContextGraphPrebuild))]
    public IEnumerator ContextNestedTreesAndLifecycle() => Run(nameof(ContextNestedTreesAndLifecycle));
    [UnityTest] public IEnumerator ContextFormationControlAndAbort() => Run(nameof(ContextFormationControlAndAbort));
    [UnityTest] public IEnumerator LegacyAllyTreesFollowMovingPlayer() => Run(nameof(LegacyAllyTreesFollowMovingPlayer));
    IEnumerator Run(string name)
    {
        var type = Type.GetType("AICombatP0Checks, Assembly-CSharp-Editor", true);
        object fixture = Activator.CreateInstance(type);
        try { yield return (IEnumerator)type.GetMethod(name).Invoke(fixture, null); }
        finally { type.GetMethod("TearDown").Invoke(fixture, null); }
    }
}
