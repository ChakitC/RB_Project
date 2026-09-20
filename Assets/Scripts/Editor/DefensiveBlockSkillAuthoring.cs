#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

public static class DefensiveBlockSkillAuthoring
{
    public static bool IsOwned(SkillGemDefinition skill) => skill != null && skill.defensiveBlock != null &&
        AssetDatabase.IsMainAsset(skill) && AssetDatabase.IsSubAsset(skill.defensiveBlock) &&
        AssetDatabase.GetAssetPath(skill) == AssetDatabase.GetAssetPath(skill.defensiveBlock);

    // Call only from an explicit Save/migration. Never changes or deletes the source profile.
    public static DefensiveBlockAttackProfile EnsureOwned(SkillGemDefinition skill)
    {
        if (skill == null || !AssetDatabase.IsMainAsset(skill))
            throw new InvalidOperationException("Save the Skill as its own asset before adding Block.");
        if (IsOwned(skill)) return skill.defensiveBlock;
        var profile = ScriptableObject.CreateInstance<DefensiveBlockAttackProfile>();
        if (skill.defensiveBlock != null) EditorUtility.CopySerialized(skill.defensiveBlock, profile);
        profile.name = "Block Profile";
        profile.hideFlags = HideFlags.None;
        AssetDatabase.AddObjectToAsset(profile, skill);
        Undo.RegisterCreatedObjectUndo(profile, "Create Skill Block Profile");
        Undo.RecordObject(skill, "Bind Skill Block Profile");
        skill.defensiveBlock = profile;
        EditorUtility.SetDirty(profile);
        EditorUtility.SetDirty(skill);
        return profile;
    }

    [MenuItem("Tools/RB/Defensive Block/Embed Existing Skill Profiles")]
    public static void EmbedExistingProfiles()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode first.");
        int count = 0;
        foreach (string id in AssetDatabase.FindAssets("t:SkillGemDefinition", new[] { "Assets" }))
        {
            var skill = AssetDatabase.LoadAssetAtPath<SkillGemDefinition>(AssetDatabase.GUIDToAssetPath(id));
            if (EmbedExistingProfile(skill)) count++;
        }
        Debug.Log($"Embedded Block Profiles for {count} Skills. Original external profiles were preserved.");
    }

    public static bool EmbedExistingProfile(SkillGemDefinition skill)
    {
        if (skill == null || skill.defensiveBlock == null || IsOwned(skill)) return false;
        var previous = skill.defensiveBlock;
        EnsureOwned(skill);
        AssetDatabase.SaveAssetIfDirty(skill);
        foreach (var draft in Resources.FindObjectsOfTypeAll<DefensiveBlockTimelineSession>())
            if (draft.skill == skill) draft.FollowOwnershipMigration(previous);
        return true;
    }
}
#endif
