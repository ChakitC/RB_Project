using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class FillMissingBasementPoses
{
    public static string Main()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Run in Edit Mode.");
        const string folder = "Assets/Data/Animation/Basement/";
        var source = AssetDatabase.LoadAssetAtPath<BasementAnimationProfileSO>(folder + "Aires_BasementAnimation.asset");
        var names = new[] { "Roma", "Feno", "Milano", "Abbygail", "Dorothy", "Noemi", "Roger" };
        var targets = names.Select(n => AssetDatabase.LoadAssetAtPath<BasementAnimationProfileSO>(folder + n + "_BasementAnimation.asset")).ToArray();
        if (source == null || targets.Any(p => p == null)) throw new Exception("Missing profile.");
        var report = new List<string>();
        foreach (var target in targets)
        {
            // Clone serialized transitions, so profiles keep independent authoring values.
            var copy = UnityEngine.Object.Instantiate(source);
            try
            {
                Undo.RecordObject(target, "Fill missing poses from Aires");
                var added = new List<string>();
                if (target.idle == null || target.idle.Clip == null) { target.idle = copy.idle; added.Add("idle"); }
                if (target.pickup == null || target.pickup.Clip == null) { target.pickup = copy.pickup; added.Add("pickup"); }
                if (target.inMenuIdle == null || target.inMenuIdle.Clip == null) { target.inMenuIdle = copy.inMenuIdle; added.Add("inMenuIdle"); }
                var entries = new List<BasementAnimationProfileSO.MenuEntry>(target.menus ?? Array.Empty<BasementAnimationProfileSO.MenuEntry>());
                foreach (var entry in copy.menus)
                {
                    if (entry == null || entry.menu == null || entry.animation == null || entry.animation.Clip == null) continue;
                    var matches = entries.Where(e => e != null && e.menu == entry.menu).ToArray();
                    if (matches.Length > 1) throw new Exception("Duplicate menu in " + target.name);
                    var existing = matches.SingleOrDefault();
                    if (existing == null)
                    {
                        entries.Add(new BasementAnimationProfileSO.MenuEntry { menu = entry.menu, animation = entry.animation });
                        added.Add(entry.menu.name);
                    }
                    else if (existing.animation == null || existing.animation.Clip == null)
                    {
                        existing.animation = entry.animation;
                        added.Add(entry.menu.name);
                    }
                }
                target.menus = entries.ToArray();
                EditorUtility.SetDirty(target);
                report.Add(target.name + ": " + string.Join(", ", added));
            }
            finally { UnityEngine.Object.DestroyImmediate(copy); }
        }
        AssetDatabase.SaveAssets();
        return string.Join("\n", report);
    }
}
