#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Guards the authoring rules that keep a pooled projectile owned by exactly one component, and
/// the scan that surfaces dangling projectile prefab references.
/// </summary>
public sealed class ProjectileAuthoringValidationTests
{
    /// <summary>
    /// Broken references that are known, reported, and deliberately not guessed at.
    ///
    /// Keyed on the full <c>path|property|guid</c> triple rather than the asset path alone:
    /// allowlisting a whole prefab would hide a second, unrelated break appearing in the same file
    /// later. The scan still fails on anything new, and on a stale entry once one is repaired.
    /// </summary>
    static readonly string[] KnownBrokenProjectileReferences =
    {
        "Assets/Prefab/GameEnemy/Enemy_Base.prefab|projectilePrefab|522002f7fd0905a44ad43b9329339bca",
    };

    static string KeyOf(in ProjectileBrokenReference reference) =>
        $"{reference.AssetPath}|{reference.PropertyName}|{reference.MissingGuid}";

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

    // ---- the rule itself -------------------------------------------------------------------------

    [Test]
    public void ASecondComponentDrivingTheRootRigidbodyIsRejected()
    {
        GameObject root = CreateProjectileRoot("RivalOwner");
        var rival = root.AddComponent<ProjectileRivalMoverProbe>();
        rival.Bind(root.GetComponent<Rigidbody>());

        var issues = new List<string>();
        Assert.That(ProjectileRootOwnerRule.Validate(root, issues), Is.False);
        Assert.That(issues, Is.Not.Empty);
        Assert.That(issues[0], Does.Contain(nameof(ProjectileRivalMoverProbe)));
    }

    [Test]
    public void APresentationOnlyComponentIsAccepted()
    {
        GameObject root = CreateProjectileRoot("PresentationOnly");
        root.AddComponent<ProjectilePresentationResetter>();

        var issues = new List<string>();
        Assert.That(ProjectileRootOwnerRule.Validate(root, issues), Is.True,
            "A component that only resets particles and lights is not a second owner.");
        Assert.That(issues, Is.Empty);
    }

    [Test]
    public void APrefabWithoutAProjectileIsIgnoredByTheRule()
    {
        var root = Track(new GameObject("NotAProjectile"));
        root.AddComponent<Rigidbody>();
        root.AddComponent<ProjectileRivalMoverProbe>();

        var issues = new List<string>();
        Assert.That(ProjectileRootOwnerRule.Validate(root, issues), Is.True);
    }

    // ---- the project sweep -----------------------------------------------------------------------

    [Test]
    public void EveryGameplayProjectilePrefabHasASingleMovementAndLifetimeOwner()
    {
        var issues = new List<string>();
        bool clean = ProjectileAuthoringValidator.ValidateGameplayProjectilePrefabs(issues);

        Assert.That(clean, Is.True, string.Join("\n", issues));
    }

    [Test]
    public void GameplayProjectilePrefabsAreActuallyBeingScanned()
    {
        // A rule that silently matches nothing is worse than no rule. Vendor demo prefabs are
        // excluded, so the scan must still find the project's own projectile prefabs.
        List<string> paths = ProjectileAuthoringValidator.FindGameplayProjectilePrefabPaths();

        Assert.That(paths, Is.Not.Empty, "No gameplay projectile prefabs were discovered.");
        Assert.That(paths, Has.None.Matches<string>(ProjectileAuthoringValidator.IsVendorPath));
    }

    [Test]
    public void NoNewBrokenProjectilePrefabReferences()
    {
        var broken = new List<ProjectileBrokenReference>();
        ProjectileAuthoringValidator.CollectBrokenProjectileReferences(broken);

        var unexpected = new List<string>();
        var stillKnown = new HashSet<string>();

        for (int i = 0; i < broken.Count; i++)
        {
            string key = KeyOf(broken[i]);
            if (System.Array.IndexOf(KnownBrokenProjectileReferences, key) >= 0)
            {
                stillKnown.Add(key);
                continue;
            }

            unexpected.Add($"{broken[i]} (allowlist key: {key})");
        }

        Assert.That(unexpected, Is.Empty, string.Join("\n", unexpected));

        for (int i = 0; i < KnownBrokenProjectileReferences.Length; i++)
        {
            Assert.That(stillKnown, Does.Contain(KnownBrokenProjectileReferences[i]),
                $"'{KnownBrokenProjectileReferences[i]}' is no longer broken. " +
                "Remove it from KnownBrokenProjectileReferences.");
        }
    }

    [Test]
    public void NoShippedSplitConfigurationLoopsBackOnItself()
    {
        var issues = new List<string>();
        Assert.That(ProjectileAuthoringValidator.ValidateSplitGraphs(issues), Is.True, string.Join("\n", issues));
    }

    // ---- helpers ---------------------------------------------------------------------------------

    T Track<T>(T obj) where T : Object
    {
        createdObjects.Add(obj);
        return obj;
    }

    GameObject CreateProjectileRoot(string name)
    {
        var go = Track(new GameObject(name));
        go.AddComponent<Rigidbody>();
        go.AddComponent<SphereCollider>();
        go.AddComponent<Projectile>();
        go.SetActive(false);
        return go;
    }
}
#endif
