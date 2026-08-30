#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Edit Mode coverage for the deferred-ChainReady transaction on <see cref="StaggerMeter"/>.
///
/// This is the part of the Special Shoot Point design that must be deterministic regardless of
/// whether <c>EnemyHealth</c>, the projectile, or the point callback happens to run first, so it is
/// asserted directly against the meter rather than through a live hit.
/// </summary>
public sealed class SpecialPointStaggerTransactionSmokeTests
{
    readonly List<Object> createdObjects = new();

    [TearDown]
    public void TearDown()
    {
        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            if (createdObjects[i] != null)
                Object.DestroyImmediate(createdObjects[i]);
        }

        createdObjects.Clear();
    }

    // ---- Baseline: nothing about ordinary stagger moved ----------------------------------------

    [Test]
    public void UndeferredStaggerStillEntersChainReadyImmediately()
    {
        StaggerMeter meter = CreateMeter();

        meter.ApplyStagger(meter.MaxStagger);

        Assert.That(meter.IsChainReady, Is.True);
    }

    // ---- Deferral -------------------------------------------------------------------------------

    [Test]
    public void ChainReadyIsHeldBackWhileADirectHitTransactionIsOpen()
    {
        StaggerMeter meter = CreateMeter();

        meter.BeginDirectHitStaggerDeferral();
        meter.ApplyStagger(meter.MaxStagger);

        Assert.That(meter.IsChainReady, Is.False, "The transaction must not have committed yet.");
        Assert.That(meter.CurrentStagger, Is.EqualTo(meter.MaxStagger).Within(0.001f),
            "The meter value still changes while deferred; only the transition waits.");

        meter.EndDirectHitStaggerDeferral();

        Assert.That(meter.IsChainReady, Is.True, "Closing the transaction must commit the transition.");
    }

    [Test]
    public void NestedDeferralsOnlyCommitOnTheOutermostClose()
    {
        StaggerMeter meter = CreateMeter();

        meter.BeginDirectHitStaggerDeferral();
        meter.BeginDirectHitStaggerDeferral();
        meter.ApplyStagger(meter.MaxStagger);

        meter.EndDirectHitStaggerDeferral();
        Assert.That(meter.IsChainReady, Is.False);

        meter.EndDirectHitStaggerDeferral();
        Assert.That(meter.IsChainReady, Is.True);
    }

    [Test]
    public void ClosingAnUnopenedTransactionIsHarmless()
    {
        StaggerMeter meter = CreateMeter();

        meter.EndDirectHitStaggerDeferral();
        meter.EndDirectHitStaggerDeferral();

        Assert.That(meter.IsDirectHitStaggerDeferred, Is.False);
        Assert.That(meter.IsChainReady, Is.False);
    }

    // ---- Final point below max: Mini Stun only --------------------------------------------------

    [Test]
    public void RewardBelowMaxProducesNoChainReady()
    {
        StaggerMeter meter = CreateMeter();

        meter.BeginDirectHitStaggerDeferral();
        meter.ApplyStagger(meter.MaxStagger * 0.2f);
        meter.ApplySpecialPointReward(meter.MaxStagger * 0.25f);

        bool pinned = meter.BeginPendingSpecialPointBreak();
        Assert.That(pinned, Is.False, "A meter that is not full must not be pinned.");

        meter.BeginSpecialPointReactionHold();
        meter.EndDirectHitStaggerDeferral();

        Assert.That(meter.IsChainReady, Is.False);

        bool enteredChainReady = meter.EndSpecialPointReactionHold();
        Assert.That(enteredChainReady, Is.False, "Mini Stun only.");
        Assert.That(meter.IsChainReady, Is.False);
    }

    // ---- Final point fills the meter: Mini Stun, then ChainReady ---------------------------------

    [Test]
    public void RewardThatFillsTheMeterDefersChainReadyUntilTheReactionCompletes()
    {
        StaggerMeter meter = CreateMeter();

        meter.BeginDirectHitStaggerDeferral();
        meter.ApplyStagger(meter.MaxStagger * 0.9f);
        meter.ApplySpecialPointReward(meter.MaxStagger * 0.25f);

        Assert.That(meter.BeginPendingSpecialPointBreak(), Is.True);
        meter.BeginSpecialPointReactionHold();
        meter.EndDirectHitStaggerDeferral();

        Assert.That(meter.IsChainReady, Is.False, "ChainReady must wait for the Mini Stun to finish.");
        Assert.That(meter.HasPendingSpecialPointBreak, Is.True);

        Assert.That(meter.EndSpecialPointReactionHold(), Is.True);
        Assert.That(meter.IsChainReady, Is.True);
        Assert.That(meter.HasPendingSpecialPointBreak, Is.False);
    }

    [Test]
    public void RegularHitStaggerFillingTheMeterAlsoWaitsForTheReaction()
    {
        StaggerMeter meter = CreateMeter();

        // The shot's own stagger is what fills the meter, before the reward is even applied. The
        // outcome must not depend on which of the two got there first.
        meter.BeginDirectHitStaggerDeferral();
        meter.ApplyStagger(meter.MaxStagger);
        meter.ApplySpecialPointReward(meter.MaxStagger * 0.25f);

        Assert.That(meter.BeginPendingSpecialPointBreak(), Is.True);
        meter.BeginSpecialPointReactionHold();
        meter.EndDirectHitStaggerDeferral();

        Assert.That(meter.IsChainReady, Is.False);
        Assert.That(meter.EndSpecialPointReactionHold(), Is.True);
        Assert.That(meter.IsChainReady, Is.True);
    }

    [Test]
    public void PinnedMeterRejectsFurtherStaggerGain()
    {
        StaggerMeter meter = CreateMeter();

        meter.ApplyStaggerToJustBelowMax();
        meter.BeginDirectHitStaggerDeferral();
        meter.ApplySpecialPointReward(meter.MaxStagger);
        Assert.That(meter.BeginPendingSpecialPointBreak(), Is.True);
        meter.EndDirectHitStaggerDeferral();

        Assert.That(meter.ApplyStagger(50f), Is.False, "A pinned meter must refuse further gain.");
        Assert.That(meter.CurrentStagger, Is.EqualTo(meter.MaxStagger).Within(0.001f));
    }

    // ---- Cancellation ---------------------------------------------------------------------------

    [Test]
    public void CancellingAPendingBreakNeverEntersChainReady()
    {
        StaggerMeter meter = CreateMeter();

        meter.BeginDirectHitStaggerDeferral();
        meter.ApplyStagger(meter.MaxStagger);
        Assert.That(meter.BeginPendingSpecialPointBreak(), Is.True);
        meter.BeginSpecialPointReactionHold();
        meter.EndDirectHitStaggerDeferral();

        meter.CancelPendingSpecialPointBreak();

        Assert.That(meter.HasPendingSpecialPointBreak, Is.False);
        Assert.That(meter.IsChainReady, Is.False);

        // A later release must not resurrect the transition.
        Assert.That(meter.ReleaseSpecialPointBreakAndEnterChainReady(), Is.False);
        Assert.That(meter.IsChainReady, Is.False);
    }

    [Test]
    public void ReleaseIsIdempotentAndOnlyEntersChainReadyOnce()
    {
        StaggerMeter meter = CreateMeter();

        meter.BeginDirectHitStaggerDeferral();
        meter.ApplyStagger(meter.MaxStagger);
        meter.BeginPendingSpecialPointBreak();
        meter.EndDirectHitStaggerDeferral();

        Assert.That(meter.ReleaseSpecialPointBreakAndEnterChainReady(), Is.True);
        Assert.That(meter.ReleaseSpecialPointBreakAndEnterChainReady(), Is.False);
    }

    // ---- Helpers ---------------------------------------------------------------------------------

    StaggerMeter CreateMeter()
    {
        var go = new GameObject("stagger-meter-fixture");
        createdObjects.Add(go);
        return go.AddComponent<StaggerMeter>();
    }
}

static class StaggerMeterTestExtensions
{
    /// <summary>Fills the meter to just under max without tripping the ChainReady transition.</summary>
    public static void ApplyStaggerToJustBelowMax(this StaggerMeter meter)
    {
        meter.ApplyStagger(meter.MaxStagger - 1f);
    }
}
#endif
