#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Authoring validation for Special Shoot Point enemies.
///
/// The runtime rejects a trigger rather than running a degraded round, which is correct but silent
/// until someone plays the enemy. This surfaces the same authoring problems at edit time: a missing
/// profile or runtime point prefab, too few usable anchors for the configured count, duplicate
/// anchor transforms, and a point prefab with no usable collider.
/// </summary>
public static class SpecialShootPointAuthoringValidator
{
    /// <summary>
    /// Validates one authored controller.
    /// </summary>
    /// <returns>True when the controller could actually run a round.</returns>
    public static bool Validate(SpecialShootPointController controller, out string report)
    {
        var issues = new List<string>();

        if (controller == null)
        {
            report = "SpecialShootPointController is missing.";
            return false;
        }

        SpecialShootPointProfileSO profile = controller.EditorProfile;
        IReadOnlyList<SpecialShootPointAnchor> anchors = controller.EditorAnchors;

        if (profile == null)
        {
            issues.Add("No SpecialShootPointProfileSO assigned.");
        }
        else
        {
            if (profile.runtimePointPrefab == null)
                issues.Add("Profile has no runtime point prefab.");
            else if (profile.runtimePointPrefab.GetComponentInChildren<SphereCollider>(true) == null)
                issues.Add("Runtime point prefab has no SphereCollider to act as its hit collider.");

            if (profile.defaultPointCount > profile.maxPointCount)
                issues.Add($"Default point count ({profile.defaultPointCount}) exceeds the maximum ({profile.maxPointCount}).");

            if (profile.pointHealthMin > profile.pointHealthMax)
                issues.Add("Point HP minimum clamp is above the maximum clamp.");

            if (profile.lastSecondWarningThreshold > profile.activeDuration)
                issues.Add("Last-second warning threshold is longer than the whole active window.");
        }

        int usable = 0;
        var seenTransforms = new HashSet<int>();

        if (anchors == null || anchors.Count == 0)
        {
            issues.Add(controller.EditorUsesAnchorSet
                ? "The model's SpecialShootPointAnchorSet has no anchors."
                : "No anchors are authored, and no SpecialShootPointAnchorSet was found on the model.");
        }
        else
        {
            for (int i = 0; i < anchors.Count; i++)
            {
                SpecialShootPointAnchor anchor = anchors[i];

                if (anchor == null)
                {
                    issues.Add($"Anchor {i} is null.");
                    continue;
                }

                if (anchor.anchor == null)
                {
                    // A disabled anchor with no transform is deliberate authoring, not an error.
                    if (anchor.enabled)
                        issues.Add($"Anchor {i} is enabled but has no Transform.");

                    continue;
                }

                if (!seenTransforms.Add(anchor.anchor.GetInstanceID()))
                    issues.Add($"Anchor {i} reuses the same Transform as an earlier anchor ('{anchor.anchor.name}').");

                if (anchor.colliderRadius <= 0f)
                    issues.Add($"Anchor {i} has a non-positive collider radius.");

                if (anchor.enabled)
                    usable++;
            }
        }

        // Selecting more points than there are anchors is what makes a trigger fail at runtime, so
        // it is worth naming the exact shortfall.
        if (profile != null)
        {
            int needed = Mathf.Clamp(profile.defaultPointCount, 1, Mathf.Max(1, profile.maxPointCount));
            if (usable < needed)
            {
                issues.Add(
                    $"{usable} usable anchor(s) for a default round of {needed}. " +
                    "The trigger will report Failure.");
            }
        }

        // Anchors serialized on the context root do not survive a model rebuild, because the bones
        // they point at are destroyed with the old model. Worth flagging even when everything else
        // validates, since it only breaks after the first rebuild.
        if (!controller.EditorUsesAnchorSet && usable > 0)
        {
            issues.Add(
                "Anchors are authored on the controller rather than on a SpecialShootPointAnchorSet " +
                "on the visual model. Bone references will dangle after a runtime model rebuild. " +
                "This is only correct for actors whose model is never rebuilt, such as turrets.");
        }

        if (issues.Count == 0)
        {
            report = $"'{controller.name}': {usable} usable anchor(s). OK.";
            return true;
        }

        var builder = new StringBuilder();
        builder.AppendLine($"'{controller.name}' has {issues.Count} Special Shoot Point authoring issue(s):");
        for (int i = 0; i < issues.Count; i++)
            builder.AppendLine($"  - {issues[i]}");

        report = builder.ToString();
        return false;
    }

    [MenuItem("Tools/RB/Validate Special Shoot Point Authoring")]
    static void ValidateSelection()
    {
        Object[] selection = Selection.GetFiltered(typeof(GameObject), SelectionMode.Deep);
        if (selection.Length == 0)
        {
            Debug.LogWarning("[SpecialShootPointAuthoringValidator] Select one or more prefabs or scene objects first.");
            return;
        }

        int checkedCount = 0;
        int failedCount = 0;

        for (int i = 0; i < selection.Length; i++)
        {
            var go = (GameObject)selection[i];
            SpecialShootPointController[] controllers = go.GetComponentsInChildren<SpecialShootPointController>(true);

            for (int c = 0; c < controllers.Length; c++)
            {
                checkedCount++;

                if (Validate(controllers[c], out string report))
                {
                    Debug.Log($"[SpecialShootPointAuthoringValidator] {report}", controllers[c]);
                }
                else
                {
                    failedCount++;
                    Debug.LogWarning($"[SpecialShootPointAuthoringValidator] {report}", controllers[c]);
                }
            }
        }

        if (checkedCount == 0)
            Debug.Log("[SpecialShootPointAuthoringValidator] No SpecialShootPointController found in the selection.");
        else
            Debug.Log($"[SpecialShootPointAuthoringValidator] Checked {checkedCount} controller(s), {failedCount} with issues.");
    }
}
#endif
