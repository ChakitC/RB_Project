#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Edit Mode coverage for the pure parts of the Special Shoot Point contract: anchor rotation,
/// profile clamping, and the runtime point registry.
///
/// The damage routing, the stagger transaction, and the animation priority table have their own
/// fixtures. Anything that needs live prefabs, an Animancer graph, or a NavMesh — points riding
/// animated bones, root-motion safety, the HUD — stays Play Mode work.
/// </summary>
public sealed class SpecialShootPointSmokeTests
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

    // ---- Shuffle bag --------------------------------------------------------------------------

    [Test]
    public void DrawnAnchorsAreUniqueWithinOneRound()
    {
        var bag = new SpecialShootPointShuffleBag();
        var results = new List<int>();

        Assert.That(bag.TryDraw(5, 3, AlwaysEligible, results), Is.True);
        Assert.That(results.Count, Is.EqualTo(3));
        CollectionAssert.AllItemsAreUnique(results);
    }

    [Test]
    public void EveryEnabledAnchorIsConsumedBeforeAnyRepeats()
    {
        const int anchorCount = 4;
        var bag = new SpecialShootPointShuffleBag();
        var results = new List<int>();
        var seen = new HashSet<int>();

        // Two draws of two out of four must cover the whole set exactly once.
        for (int round = 0; round < 2; round++)
        {
            Assert.That(bag.TryDraw(anchorCount, 2, AlwaysEligible, results), Is.True);
            for (int i = 0; i < results.Count; i++)
                Assert.That(seen.Add(results[i]), Is.True, $"Anchor {results[i]} repeated before the bag was exhausted.");
        }

        Assert.That(seen.Count, Is.EqualTo(anchorCount));
    }

    [Test]
    public void InsufficientEligibleAnchorsFailsCleanly()
    {
        var bag = new SpecialShootPointShuffleBag();
        var results = new List<int>();

        // Only index 0 is eligible, but three are asked for.
        Assert.That(bag.TryDraw(5, 3, index => index == 0, results), Is.False);
        Assert.That(results, Is.Empty);
    }

    [Test]
    public void IneligibleAnchorsAreNeverDrawn()
    {
        var bag = new SpecialShootPointShuffleBag();
        var results = new List<int>();

        for (int round = 0; round < 6; round++)
        {
            Assert.That(bag.TryDraw(6, 2, index => index % 2 == 0, results), Is.True);
            for (int i = 0; i < results.Count; i++)
                Assert.That(results[i] % 2, Is.EqualTo(0));
        }
    }

    // ---- Profile ------------------------------------------------------------------------------

    [Test]
    public void CountOverrideIsClampedToProfileMaximumAndAnchorCount()
    {
        SpecialShootPointProfileSO profile = CreateProfile();
        profile.defaultPointCount = 2;
        profile.maxPointCount = 3;

        Assert.That(profile.ResolvePointCount(99, 10), Is.EqualTo(3), "Profile maximum must win.");
        Assert.That(profile.ResolvePointCount(3, 2), Is.EqualTo(2), "Usable anchor count must win.");
        Assert.That(profile.ResolvePointCount(0, 10), Is.EqualTo(2), "Zero must fall back to the default.");
        Assert.That(profile.ResolvePointCount(2, 0), Is.EqualTo(0), "No anchors means no round.");
    }

    [Test]
    public void PointHealthIsAPercentageOfMaxHpInsideTheProfileClamps()
    {
        SpecialShootPointProfileSO profile = CreateProfile();
        profile.pointHealthPercentOfMaxHp = 3f;
        profile.pointHealthMin = 10f;
        profile.pointHealthMax = 500f;

        Assert.That(profile.ResolvePointHealth(1000f), Is.EqualTo(30f).Within(0.001f));
        Assert.That(profile.ResolvePointHealth(100f), Is.EqualTo(10f).Within(0.001f), "Lower clamp.");
        Assert.That(profile.ResolvePointHealth(100000f), Is.EqualTo(500f).Within(0.001f), "Upper clamp.");
    }

    [Test]
    public void StaggerRewardIsAPercentageOfMaxStagger()
    {
        SpecialShootPointProfileSO profile = CreateProfile();
        profile.staggerRewardPercentOfMaxStagger = 25f;

        Assert.That(profile.ResolveStaggerReward(200f), Is.EqualTo(50f).Within(0.001f));
        Assert.That(profile.ResolveStaggerReward(0f), Is.EqualTo(0f).Within(0.001f));
    }

    // ---- Registry -----------------------------------------------------------------------------

    [Test]
    public void RegistryResolvesOnlyRegisteredColliders()
    {
        var go = new GameObject("point");
        createdObjects.Add(go);

        SphereCollider collider = go.AddComponent<SphereCollider>();
        SpecialShootPointInstance instance = go.AddComponent<SpecialShootPointInstance>();

        Assert.That(SpecialShootPointRegistry.TryResolve(collider, out _), Is.False);

        SpecialShootPointRegistry.Register(collider, instance);
        Assert.That(SpecialShootPointRegistry.TryResolve(collider, out SpecialShootPointInstance resolved), Is.True);
        Assert.That(resolved, Is.SameAs(instance));

        SpecialShootPointRegistry.Unregister(collider);
        Assert.That(SpecialShootPointRegistry.TryResolve(collider, out _), Is.False);
    }

    [Test]
    public void RegistryResolveIsSafeForNullAndDestroyedEntries()
    {
        Assert.That(SpecialShootPointRegistry.TryResolve(null, out _), Is.False);

        var go = new GameObject("point");
        SphereCollider collider = go.AddComponent<SphereCollider>();
        SpecialShootPointInstance instance = go.AddComponent<SpecialShootPointInstance>();
        SpecialShootPointRegistry.Register(collider, instance);

        // The collider object outlives the component: a stale entry must never absorb a shot.
        Object.DestroyImmediate(instance);
        Assert.That(SpecialShootPointRegistry.TryResolve(collider, out _), Is.False);

        Object.DestroyImmediate(go);
    }

    // ---- Anchors ------------------------------------------------------------------------------

    [Test]
    public void AnchorIsUnusableWhenDisabledOrUnbound()
    {
        var go = new GameObject("anchor");
        createdObjects.Add(go);

        var anchor = new SpecialShootPointAnchor { anchor = go.transform, enabled = true };
        Assert.That(anchor.IsUsable, Is.True);

        anchor.enabled = false;
        Assert.That(anchor.IsUsable, Is.False);

        anchor.enabled = true;
        anchor.anchor = null;
        Assert.That(anchor.IsUsable, Is.False);
    }

    // ---- Helpers ------------------------------------------------------------------------------

    static bool AlwaysEligible(int index) => true;

    SpecialShootPointProfileSO CreateProfile()
    {
        SpecialShootPointProfileSO profile = ScriptableObject.CreateInstance<SpecialShootPointProfileSO>();
        createdObjects.Add(profile);
        return profile;
    }
}
#endif
