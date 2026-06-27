#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class StatusLocomotionPoseMigrationTool
{
    [MenuItem("Tools/Status/Migrate Locomotion Pose")]
    static void MigrateLocomotionPose()
    {
        string[] guids = AssetDatabase.FindAssets("t:StatusEffectDef");
        int migratedCount = 0;
        int skippedCount = 0;

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            StatusEffectDef def = AssetDatabase.LoadAssetAtPath<StatusEffectDef>(path);
            if (def == null)
                continue;

            if (def.locomotionPose != StatusLocomotionPose.Auto)
            {
                skippedCount++;
                continue;
            }

            StatusLocomotionPose resolved = ResolveFromLegacyStrings(def);
            if (resolved == StatusLocomotionPose.Auto || resolved == StatusLocomotionPose.None)
            {
                skippedCount++;
                continue;
            }

            Undo.RecordObject(def, "Migrate StatusLocomotionPose");
            def.locomotionPose = resolved;
            EditorUtility.SetDirty(def);
            migratedCount++;
            Debug.Log($"[StatusPoseMigration] {path} → {resolved}");
        }

        if (migratedCount > 0)
            AssetDatabase.SaveAssets();

        Debug.Log($"StatusLocomotionPose migration complete: {migratedCount} migrated, {skippedCount} skipped (already set or no match).");
    }

    static StatusLocomotionPose ResolveFromLegacyStrings(StatusEffectDef def)
    {
        if (HasLegacyMarker(def, "ministun", "ministune", "mini_stun", "mini-stun"))
            return StatusLocomotionPose.MiniStun;

        if (HasLegacyMarker(def, "freez", "freeze", "frozen"))
            return StatusLocomotionPose.Freeze;

        if (HasLegacyMarker(def, "stun", "stune"))
            return StatusLocomotionPose.Stun;

        if (HasLegacyMarker(def, "root", "rooted"))
            return StatusLocomotionPose.Root;

        return StatusLocomotionPose.None;
    }

    static bool HasLegacyMarker(StatusEffectDef def, params string[] tokens)
    {
        if (MatchesAny(def.effectId, tokens)) return true;
        if (MatchesAny(def.name, tokens)) return true;

        if (def.tags != null)
        {
            for (int i = 0; i < def.tags.Count; i++)
            {
                if (MatchesAny(def.tags[i], tokens))
                    return true;
            }
        }

        return false;
    }

    static bool MatchesAny(string value, string[] tokens)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        string normalized = value.Trim().ToLowerInvariant()
            .Replace(" ", "").Replace("_", "").Replace("-", "").Replace(":", "");

        for (int i = 0; i < tokens.Length; i++)
        {
            if (normalized.Contains(tokens[i]))
                return true;
        }

        return false;
    }
}
#endif
