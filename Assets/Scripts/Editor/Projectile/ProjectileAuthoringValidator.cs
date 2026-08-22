#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Authoring checks for pooled projectiles:
///
/// 1. one movement/lifetime owner per gameplay projectile prefab (<see cref="ProjectileRootOwnerRule"/>),
/// 2. no dangling projectile/bullet prefab references on gameplay prefabs,
/// 3. no split config that can reach itself (<see cref="ProjectileSplitGraphAnalyzer"/>).
///
/// Vendor and demo folders are excluded on purpose. Packs such as Hovl Studio ship dozens of demo
/// prefabs that pair their own mover with a Rigidbody; those are not gameplay projectiles, and
/// fixing them would mean editing vendor content.
/// </summary>
public static class ProjectileAuthoringValidator
{
    /// <summary>Folders searched for gameplay prefabs.</summary>
    public static readonly string[] GameplayPrefabRoots =
    {
        "Assets/Prefab",
        "Assets/Character",
        "Assets/Scripts/Projectile",
    };

    /// <summary>Third-party and demo content, excluded from every rule here.</summary>
    public static readonly string[] VendorPathFragments =
    {
        "/VFX/",
        "/Plugins/",
        "/Opsive/",
        "/MK/",
        "/DamageNumbersPro/",
        "/Voxel Labs/",
    };

    static readonly Regex GuidReferenceLine =
        new(@"^\s*(?:-\s*)?(?<key>[A-Za-z_][A-Za-z0-9_]*)\s*:\s*\{fileID:[^}]*guid:\s*(?<guid>[0-9a-f]{32})",
            RegexOptions.Compiled);

    [MenuItem("Tools/Validation/Projectile Authoring Report")]
    public static void ReportToConsole()
    {
        var issues = new List<string>();
        bool clean = ValidateAll(issues);

        if (clean)
        {
            Debug.Log("[ProjectileAuthoringValidator] No projectile authoring issues found.");
            return;
        }

        Debug.LogWarning(
            $"[ProjectileAuthoringValidator] {issues.Count} issue(s):\n - " + string.Join("\n - ", issues));
    }

    public static bool ValidateAll(List<string> issues)
    {
        bool clean = ValidateGameplayProjectilePrefabs(issues);
        clean &= ValidateProjectileReferences(issues);
        clean &= ValidateSplitGraphs(issues);
        return clean;
    }

    // ---- 1. one owner per projectile root -------------------------------------------------------

    public static bool ValidateGameplayProjectilePrefabs(List<string> issues)
    {
        bool clean = true;

        foreach (string path in FindGameplayProjectilePrefabPaths())
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (root == null)
                continue;

            var prefabIssues = new List<string>();
            if (ProjectileRootOwnerRule.Validate(root, prefabIssues))
                continue;

            clean = false;
            for (int i = 0; i < prefabIssues.Count; i++)
                issues?.Add($"{path}: {prefabIssues[i]}");
        }

        return clean;
    }

    /// <summary>Prefabs under the gameplay roots whose root object carries a <see cref="Projectile"/>.</summary>
    public static List<string> FindGameplayProjectilePrefabPaths()
    {
        var paths = new List<string>();
        string projectileScriptGuid = ResolveProjectileScriptGuid();
        if (string.IsNullOrEmpty(projectileScriptGuid))
            return paths;

        foreach (string path in EnumerateGameplayPrefabPaths())
        {
            string text;
            try { text = File.ReadAllText(path); }
            catch (IOException) { continue; }

            if (text.IndexOf(projectileScriptGuid, StringComparison.Ordinal) < 0)
                continue;

            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (root != null && root.GetComponent<Projectile>() != null)
                paths.Add(path);
        }

        return paths;
    }

    // ---- 2. dangling projectile references ------------------------------------------------------

    public static bool ValidateProjectileReferences(List<string> issues)
    {
        var broken = new List<ProjectileBrokenReference>();
        CollectBrokenProjectileReferences(broken);

        for (int i = 0; i < broken.Count; i++)
            issues?.Add(broken[i].ToString());

        return broken.Count == 0;
    }

    public static void CollectBrokenProjectileReferences(List<ProjectileBrokenReference> results)
    {
        if (results == null)
            return;

        foreach (string path in EnumerateGameplayPrefabPaths())
            CollectBrokenReferences(path, results, projectileOnly: true);
    }

    /// <summary>
    /// Reads the asset as text and reports every reference whose GUID no longer resolves. Text is
    /// used rather than SerializedObject so a missing script or missing asset cannot hide the row.
    /// </summary>
    public static void CollectBrokenReferences(
        string assetPath,
        List<ProjectileBrokenReference> results,
        bool projectileOnly)
    {
        if (results == null || string.IsNullOrEmpty(assetPath) || !File.Exists(assetPath))
            return;

        string[] lines;
        try { lines = File.ReadAllLines(assetPath); }
        catch (IOException) { return; }

        for (int i = 0; i < lines.Length; i++)
        {
            Match match = GuidReferenceLine.Match(lines[i]);
            if (!match.Success)
                continue;

            var reference = new ProjectileBrokenReference(
                assetPath,
                match.Groups["key"].Value,
                match.Groups["guid"].Value);

            if (projectileOnly && !reference.IsProjectileReference)
                continue;

            if (!string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(reference.MissingGuid)))
                continue;

            results.Add(reference);
        }
    }

    // ---- 3. split graph cycles ------------------------------------------------------------------

    public static bool ValidateSplitGraphs(List<string> issues)
    {
        bool clean = true;
        var cyclePath = new List<ProjectileConfig>();

        foreach (string guid in AssetDatabase.FindAssets("t:ProjectileConfig"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (IsVendorPath(path))
                continue;

            var config = AssetDatabase.LoadAssetAtPath<ProjectileConfig>(path);
            if (config == null || !ProjectileSplitGraphAnalyzer.TryFindCycle(config, cyclePath))
                continue;

            clean = false;
            issues?.Add(
                $"{path}: split childConfig graph loops back on itself " +
                $"({ProjectileSplitGraphAnalyzer.DescribeCycle(cyclePath)}). Splitting stops at the " +
                $"generation budget, but the loop is almost certainly unintended.");
        }

        return clean;
    }

    // ---- helpers --------------------------------------------------------------------------------

    public static bool IsVendorPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        string normalized = path.Replace('\\', '/');
        for (int i = 0; i < VendorPathFragments.Length; i++)
        {
            if (normalized.IndexOf(VendorPathFragments[i], StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    static IEnumerable<string> EnumerateGameplayPrefabPaths()
    {
        for (int i = 0; i < GameplayPrefabRoots.Length; i++)
        {
            string root = GameplayPrefabRoots[i];
            if (!Directory.Exists(root))
                continue;

            foreach (string file in Directory.EnumerateFiles(root, "*.prefab", SearchOption.AllDirectories))
            {
                string path = file.Replace('\\', '/');
                if (!IsVendorPath(path))
                    yield return path;
            }
        }
    }

    static string ResolveProjectileScriptGuid()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:MonoScript Projectile"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.EndsWith("/Projectile.cs", StringComparison.Ordinal))
                return guid;
        }

        return null;
    }
}
#endif
