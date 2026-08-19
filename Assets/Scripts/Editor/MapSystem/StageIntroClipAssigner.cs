using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Assigns the authored intro animations to their <see cref="CharacterAnimProfileSO"/>.
///
/// These FBX files carry no explicit <c>clipAnimations</c> entry, so their AnimationClip is the
/// importer-generated default take and its fileID cannot be written into an asset by hand. This
/// tool resolves the clip through the AssetDatabase instead, which is also what makes it safe to
/// re-run after a reimport.
/// </summary>
public static class StageIntroClipAssigner
{
    readonly struct Assignment
    {
        public readonly string ProfilePath;
        public readonly string ClipSourcePath;

        public Assignment(string profilePath, string clipSourcePath)
        {
            ProfilePath = profilePath;
            ClipSourcePath = clipSourcePath;
        }
    }

    static readonly Assignment[] Assignments =
    {
        new("Assets/Scripts/Animator/Roma_AnimProfile.asset",
            "Assets/Animation/Ch_Roma_Anim/Roma_Intro_Sit.fbx"),
        new("Assets/Data/Animation/Feno/Feno_AnimProfile.asset",
            "Assets/Animation/Ch_Feno/Feno_Intro_Sit.fbx"),
        new("Assets/Data/Animation/Aires/Aires_AnimProfile.asset",
            "Assets/Animation/Ch_Aires/Aires_Intro.Stand.fbx"),
    };

    // Milano shares Roma_AnimProfile, so there is no profile this clip can be assigned to.
    const string UnassignableClipPath = "Assets/Animation/Ch_Milano/Milano_Intro_Stand.fbx";

    [MenuItem("Tools/RB/Map/Assign Stage Intro Clips")]
    public static void AssignFromMenu()
    {
        var report = new List<string>();
        int assigned = 0;

        for (int i = 0; i < Assignments.Length; i++)
        {
            Assignment assignment = Assignments[i];

            var profile = AssetDatabase.LoadAssetAtPath<CharacterAnimProfileSO>(assignment.ProfilePath);
            if (profile == null)
            {
                report.Add($"MISSING profile: {assignment.ProfilePath}");
                continue;
            }

            AnimationClip clip = LoadClip(assignment.ClipSourcePath);
            if (clip == null)
            {
                report.Add($"MISSING clip: {assignment.ClipSourcePath}");
                continue;
            }

            var serialized = new SerializedObject(profile);
            SerializedProperty clipProperty = serialized.FindProperty("stageIntroClip._Clip");
            if (clipProperty == null)
            {
                report.Add($"'{profile.name}' has no stageIntroClip field — reimport scripts first.");
                continue;
            }

            Undo.RecordObject(profile, "Assign Stage Intro Clip");
            clipProperty.objectReferenceValue = clip;

            SerializedProperty fade = serialized.FindProperty("stageIntroClip._FadeDuration");
            if (fade != null && fade.floatValue <= 0f)
                fade.floatValue = 0.25f;

            SerializedProperty speed = serialized.FindProperty("stageIntroClip._Speed");
            if (speed != null && Mathf.Approximately(speed.floatValue, 0f))
                speed.floatValue = 1f;

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(profile);

            report.Add($"{profile.name}  ←  {clip.name}");
            assigned++;
        }

        if (AssetDatabase.LoadAssetAtPath<Object>(UnassignableClipPath) != null)
        {
            report.Add(
                $"NOT ASSIGNED: {System.IO.Path.GetFileNameWithoutExtension(UnassignableClipPath)} — " +
                "Milano shares Roma_AnimProfile, so it has no profile of its own to hold an intro pose.");
        }

        AssetDatabase.SaveAssets();

        // Deliberately no confirmation dialog: a modal blocks the editor, which stalls menu-driven
        // automation until someone clicks it. The console log is the result.
        string summary = string.Join("\n", report);
        Debug.Log($"[StageIntroClipAssigner] Assigned {assigned}/{Assignments.Length} profiles.\n{summary}");
    }

    /// <summary>Pulls the imported take out of a model file, skipping Unity's preview clip.</summary>
    static AnimationClip LoadClip(string assetPath)
    {
        Object[] representations = AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath);
        for (int i = 0; i < representations.Length; i++)
        {
            if (representations[i] is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                return clip;
        }

        return AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
    }
}
