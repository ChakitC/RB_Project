#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

// Build the asset inventory only when opening the picker, never on every repaint.
public sealed class AnimationVfxSourceAssetDropdown : AdvancedDropdown
{
    readonly bool hitboxesOnly;
    readonly Action<ScriptableObject> selected;
    readonly Dictionary<int, ScriptableObject> assets = new();

    public AnimationVfxSourceAssetDropdown(bool hitboxesOnly, Action<ScriptableObject> selected)
        : base(new AdvancedDropdownState())
    {
        this.hitboxesOnly = hitboxesOnly;
        this.selected = selected;
        minimumSize = new Vector2(560, 360);
    }

    public static bool Supports(UnityEngine.Object asset, bool hitboxesOnly)
    {
        return asset is SkillGemDefinition || asset is MeleeComboSO ||
            (!hitboxesOnly && (asset is CharacterAnimProfileSO || asset is CutsceneDefSO));
    }

    public static List<ScriptableObject> FindSources(bool hitboxesOnly)
    {
        string filter = "t:SkillGemDefinition t:MeleeComboSO";
        if (!hitboxesOnly) filter += " t:CharacterAnimProfileSO t:CutsceneDefSO";
        // Load subassets as well: execution skills can be embedded in a combo asset.
        var sources = AssetDatabase.FindAssets(filter).Select(AssetDatabase.GUIDToAssetPath).Distinct()
            .SelectMany(AssetDatabase.LoadAllAssetsAtPath).OfType<ScriptableObject>()
            .Where(asset => Supports(asset, hitboxesOnly)).Distinct().ToList();
        // Combo steps are reached through Entry. Keep skills with no selectable
        // owning step visible, and compare references rather than names or folders.
        var comboSkills = new HashSet<SkillGemDefinition>(sources.OfType<MeleeComboSO>()
            .Where(combo => combo.Steps != null).SelectMany(combo => combo.Steps)
            .Where(step => step.executionSkill != null && !string.IsNullOrWhiteSpace(step.EntryId))
            .Select(step => step.executionSkill));
        return sources.Where(asset => !(asset is SkillGemDefinition skill) || !comboSkills.Contains(skill))
            .OrderBy(asset => asset.name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    protected override AdvancedDropdownItem BuildRoot()
    {
        assets.Clear();
        int id = 0;
        var root = new AdvancedDropdownItem("Source Asset") { id = id++ };
        var none = new AdvancedDropdownItem("None (clear source)") { id = id++ };
        assets.Add(none.id, null);
        root.AddChild(none);
        foreach (var category in FindSources(hitboxesOnly).GroupBy(Category))
        {
            var group = new AdvancedDropdownItem(category.Key) { id = id++ };
            root.AddChild(group);
            foreach (var asset in category)
            {
                string path = AssetDatabase.GetAssetPath(asset);
                string name = asset is SkillGemDefinition skill && !string.IsNullOrWhiteSpace(skill.SkillDefinitionDisplayName)
                    ? skill.SkillDefinitionDisplayName : asset.name;
                var item = new AdvancedDropdownItem($"{name} — {path}") { id = id++,
                    icon = AssetDatabase.GetCachedIcon(path) as Texture2D };
                assets.Add(item.id, asset);
                group.AddChild(item);
            }
        }
        return root;
    }

    static string Category(ScriptableObject asset)
    {
        if (asset is SkillGemDefinition) return "Skills";
        if (asset is MeleeComboSO) return "Melee Combos";
        if (asset is CharacterAnimProfileSO) return "Animation Profiles";
        return "Cutscenes";
    }

    protected override void ItemSelected(AdvancedDropdownItem item)
    {
        if (assets.TryGetValue(item.id, out var asset) && (asset == null || Supports(asset, hitboxesOnly)))
            selected(asset);
    }
}
#endif
