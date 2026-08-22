#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;

/// <summary>
/// Walks the <c>ProjectileConfig -&gt; SplitOnHitModule.childConfig</c> graph looking for a config
/// that can reach itself. A cycle is not automatically wrong at runtime - the generation budget in
/// <see cref="SplitOnHitModule"/> and the hard ceiling in <see cref="Projectile"/> both stop it -
/// but it is almost always an authoring slip, and it is worth naming before a designer ships a
/// config whose only brake is the safety net.
/// </summary>
public static class ProjectileSplitGraphAnalyzer
{
    /// <summary>
    /// True when <paramref name="root"/> can reach itself through split children.
    /// <paramref name="cyclePath"/> receives the configs visited on the way, root first.
    /// </summary>
    public static bool TryFindCycle(ProjectileConfig root, List<ProjectileConfig> cyclePath)
    {
        cyclePath?.Clear();
        if (root == null)
            return false;

        var visiting = new HashSet<ProjectileConfig>();
        return Walk(root, root, visiting, cyclePath);
    }

    /// <summary>Deepest split budget any module in the graph authorizes, for reporting.</summary>
    public static int MaxAuthoredSplitGenerations(ProjectileConfig root)
    {
        if (root == null)
            return 0;

        int max = 0;
        var seen = new HashSet<ProjectileConfig>();
        var pending = new Stack<ProjectileConfig>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            ProjectileConfig config = pending.Pop();
            if (config == null || !seen.Add(config))
                continue;

            foreach (SplitOnHitModule split in EnumerateSplitModules(config))
            {
                if (split.ResolvedMaxSplitGenerations > max)
                    max = split.ResolvedMaxSplitGenerations;

                if (split.childConfig != null)
                    pending.Push(split.childConfig);
            }
        }

        return max;
    }

    public static string DescribeCycle(IReadOnlyList<ProjectileConfig> cyclePath)
    {
        if (cyclePath == null || cyclePath.Count == 0)
            return string.Empty;

        var builder = new StringBuilder();
        for (int i = 0; i < cyclePath.Count; i++)
        {
            if (i > 0) builder.Append(" -> ");
            builder.Append(cyclePath[i] != null ? cyclePath[i].name : "<null>");
        }

        builder.Append(" -> ").Append(cyclePath[0] != null ? cyclePath[0].name : "<null>");
        return builder.ToString();
    }

    static bool Walk(
        ProjectileConfig current,
        ProjectileConfig target,
        HashSet<ProjectileConfig> visiting,
        List<ProjectileConfig> cyclePath)
    {
        if (current == null || !visiting.Add(current))
            return false;

        cyclePath?.Add(current);

        foreach (SplitOnHitModule split in EnumerateSplitModules(current))
        {
            ProjectileConfig child = split.childConfig;
            if (child == null)
                continue;

            if (child == target)
                return true;

            if (Walk(child, target, visiting, cyclePath))
                return true;
        }

        if (cyclePath != null && cyclePath.Count > 0)
            cyclePath.RemoveAt(cyclePath.Count - 1);

        return false;
    }

    static IEnumerable<SplitOnHitModule> EnumerateSplitModules(ProjectileConfig config)
    {
        if (config == null || config.modules == null)
            yield break;

        for (int i = 0; i < config.modules.Count; i++)
        {
            if (config.modules[i] is SplitOnHitModule split)
                yield return split;
        }
    }
}
#endif
