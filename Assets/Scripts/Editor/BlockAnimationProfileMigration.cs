#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class BlockAnimationProfileMigration
{
    [MenuItem("Tools/RB/Defensive Block/Migrate Animation To Character Profiles")]
    public static void Migrate()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        var bindings = new Dictionary<CharacterAnimProfileSO, BlockAnimationProfile>();
        foreach (string guid in AssetDatabase.FindAssets("t:CharacterStats", new[] { "Assets" }))
        {
            var character = AssetDatabase.LoadAssetAtPath<CharacterStats>(AssetDatabase.GUIDToAssetPath(guid));
            if (character?.defensiveBlock?.animation == null) continue;
            if (character.animProfile == null) throw new InvalidOperationException($"Missing Animation Profile: {character.name}");
            var legacy = character.defensiveBlock.animation;
            if (bindings.TryGetValue(character.animProfile, out var other) && other != legacy &&
                JsonUtility.ToJson(BlockAnimationSettings.FromLegacy(other)) != JsonUtility.ToJson(BlockAnimationSettings.FromLegacy(legacy)))
                throw new InvalidOperationException($"Conflicting Block animations share {character.animProfile.name}. Split the animation profiles before migration.");
            bindings[character.animProfile] = legacy;
        }
        // These player profiles previously inherited the Player prefab's default guard animation.
        var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Player/Player.prefab");
        var fallback = player != null ? player.GetComponentInChildren<DefensiveBlockController>(true)?.defaultSettings?.animation : null;
        if (fallback != null)
            foreach (string name in new[] { "Aires", "Roma", "Feno", "Milano" })
            {
                var profile = AssetDatabase.LoadAssetAtPath<CharacterAnimProfileSO>($"Assets/Data/Animation/{name}/{name}_AnimProfile.asset");
                if (profile != null && !bindings.ContainsKey(profile)) bindings.Add(profile, fallback);
            }
        int migrated = 0;
        foreach (var pair in bindings)
        {
            if (!TryMigrate(pair.Key, pair.Value)) continue;
            AssetDatabase.SaveAssetIfDirty(pair.Key);
            migrated++;
        }
        Debug.Log($"Block animation migration: {migrated} character profiles updated; existing authored Block data preserved.");
    }

    public static bool TryMigrate(CharacterAnimProfileSO target, BlockAnimationProfile legacy)
    {
        if (target == null || legacy == null || (target.block != null && target.block.HasClips)) return false;
        Undo.RecordObject(target, "Migrate Block Animation");
        target.block = BlockAnimationSettings.FromLegacy(legacy);
        EditorUtility.SetDirty(target);
        return true;
    }
}
#endif
