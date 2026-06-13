#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

internal static class SkillGemAssetMenu
{
    [MenuItem("Assets/Create/Game/Skill Gem", priority = 120)]
    private static void CreateSkillGem()
    {
        string folderPath = GetSelectedFolderPath();
        string assetPath = AssetDatabase.GenerateUniqueAssetPath(Path.Combine(folderPath, "NewSkillGem.asset"));

        var skill = ScriptableObject.CreateInstance<SkillGemDefinition>();
        AssetDatabase.CreateAsset(skill, assetPath);
        SkillPayloadAssetUtility.ReplaceWithEmbedded(skill, typeof(ProjectileSkillPayloadDef), recordUndo: false);
        AssetDatabase.SaveAssets();

        Selection.activeObject = skill;
        EditorGUIUtility.PingObject(skill);
    }

    private static string GetSelectedFolderPath()
    {
        string selectedPath = AssetDatabase.GetAssetPath(Selection.activeObject);
        if (string.IsNullOrEmpty(selectedPath))
            return "Assets";

        if (Directory.Exists(selectedPath))
            return selectedPath;

        string directory = Path.GetDirectoryName(selectedPath);
        return string.IsNullOrEmpty(directory) ? "Assets" : directory.Replace('\\', '/');
    }
}
#endif
