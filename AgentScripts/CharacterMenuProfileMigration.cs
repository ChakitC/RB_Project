using System;
using System.Collections.Generic;
using System.Linq;
using Animancer;
using UnityEditor;
using UnityEngine;

public static class CharacterMenuProfileMigration
{
    const string Folder = "Assets/Data/Animation/Basement/";
    public static string Main()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        var menu = AssetDatabase.LoadAssetAtPath<BasementMenuDefinitionSO>(Folder + "Menus/CharacterMenu.asset");
        if (menu == null || menu.defaultCamera == null) throw new InvalidOperationException("Missing status menu/camera.");
        var profiles = AssetDatabase.FindAssets("t:CharacterStats")
            .Select(AssetDatabase.GUIDToAssetPath).Where(p => !p.StartsWith("Assets/Tests/"))
            .Select(AssetDatabase.LoadAssetAtPath<CharacterStats>)
            .Select(c => c.basementAnimationProfile).Where(p => p != null).Distinct().ToArray();
        var results = new List<string>();
        foreach (var profile in profiles)
        {
            var entries = (profile.menus ?? Array.Empty<BasementAnimationProfileSO.MenuEntry>()).ToList();
            if (entries.Count(e => e != null && e.menu == menu) > 1)
                throw new InvalidOperationException("Duplicate CharacterMenu in " + profile.name);
            var entry = profile.FindMenu(menu);
            if (entry == null)
            {
                if (profile.inMenuIdle?.Clip == null) throw new InvalidOperationException("Missing legacy pose: " + profile.name);
                entry = new BasementAnimationProfileSO.MenuEntry { menu = menu, animation = new ClipTransition() };
                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(profile.inMenuIdle), entry.animation);
                entries.Add(entry);
                profile.menus = entries.ToArray();
            }
            if (entry.camera == null)
            {
                string name = profile.name.Replace("_BasementAnimation", "") + "_CharacterMenuCamera";
                string path = Folder + "Menus/" + name + ".asset";
                entry.camera = AssetDatabase.LoadAssetAtPath<BasementMenuCameraPresetSO>(path);
                if (entry.camera == null)
                {
                    entry.camera = UnityEngine.Object.Instantiate(menu.defaultCamera);
                    entry.camera.name = name;
                    AssetDatabase.CreateAsset(entry.camera, path);
                }
            }
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssetIfDirty(profile);
            results.Add(profile.name + ": " + entry.animation.Clip.name + " / " + entry.camera.name);
        }
        return string.Join("\n", results);
    }
}
