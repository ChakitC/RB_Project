using System;
using System.Linq;
using Animancer;
using UnityEditor;
using UnityEngine;

public static class SharedBasementProfileSetup
{
    public static string Main()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Run in Edit Mode.");
        const string folder = "Assets/Data/Animation/Basement/";
        const string sharedPath = folder + "Shared_Aires_MenuTest.asset";
        var characters = AssetDatabase.FindAssets("t:CharacterStats")
            .Select(AssetDatabase.GUIDToAssetPath).Where(p => !p.StartsWith("Assets/Tests/"))
            .Select(AssetDatabase.LoadAssetAtPath<CharacterStats>)
            .Where(d => d.CharacterPrefabBasement != null && d.basementAnimationProfile != null).ToArray();
        if (characters.Length != 8) throw new InvalidOperationException("Expected exactly eight production characters.");
        var names = new[] { "Aires_Inmenu_Idle", "Aires_AccessoryMenu_Idle", "Aires_GunMenu_Idle" };
        foreach (var name in names)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath("Assets/Animation/Ch_Aires/" + name + ".fbx");
            var clips = importer.clipAnimations;
            if (clips.Length == 0) clips = importer.defaultClipAnimations;
            var clip = clips.Single(c => c.name == name);
            clip.loopTime = true;
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }
        var profile = AssetDatabase.LoadAssetAtPath<BasementAnimationProfileSO>(sharedPath);
        if (profile == null)
        {
            profile = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<BasementAnimationProfileSO>(folder + "Aires_BasementAnimation.asset"));
            profile.name = "Shared_Aires_MenuTest";
            AssetDatabase.CreateAsset(profile, sharedPath);
        }
        Undo.RecordObject(profile, "Configure shared Basement test profile");
        var original = AssetDatabase.LoadAssetAtPath<BasementAnimationProfileSO>(folder + "Aires_BasementAnimation.asset");
        profile.idle = original.idle;
        profile.pickup = original.pickup;
        profile.inMenuIdle = Transition(names[0]);
        profile.menus = new[] { Entry("Accessory", names[1]), Entry("GunMenu", names[2]) };
        EditorUtility.SetDirty(profile);
        foreach (var character in characters)
        {
            Undo.RecordObject(character, "Use shared Basement test profile");
            character.basementAnimationProfile = profile;
            EditorUtility.SetDirty(character);
        }
        AssetDatabase.SaveAssets();
        return "Shared profile assigned: " + string.Join(", ", characters.Select(c => c.name));
    }
    static ClipTransition Transition(string name) => new ClipTransition
    {
        Clip = AssetDatabase.LoadAllAssetsAtPath("Assets/Animation/Ch_Aires/" + name + ".fbx")
            .OfType<AnimationClip>().Single(c => c.name == name),
        FadeDuration = 0.25f, Speed = 1f, NormalizedStartTime = 0f
    };
    static BasementAnimationProfileSO.MenuEntry Entry(string menu, string clip)
    {
        var definition = AssetDatabase.LoadAssetAtPath<BasementMenuDefinitionSO>("Assets/Data/Animation/Basement/Menus/" + menu + ".asset");
        return new BasementAnimationProfileSO.MenuEntry { menu = definition, animation = Transition(clip), camera = definition.defaultCamera };
    }
}
