#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class WeaponMuzzleAuthoringValidator
{
    public static readonly string[] ActorPrefabs =
    {
        "Assets/Prefab/Player/Player.prefab",
        "Assets/Prefab/Player/Ally_Helper.prefab",
        "Assets/Prefab/Player/Ally_Stryker.prefab",
        "Assets/Prefab/AI Ally/Ally.prefab",
        "Assets/Prefab/GameEnemy/Enemy_Base.prefab",
        "Assets/Prefab/GameEnemy/Enemy_B_GR_01 Variant.prefab",
        "Assets/Prefab/GameEnemy/Enemy_E_GR_01 Variant.prefab",
        "Assets/Prefab/GameEnemy/Enemy_M_GR_01 Variant.prefab",
        "Assets/Prefab/GameEnemy/Enemy_M_GR_02 Variant.prefab",
        "Assets/Prefab/Player/SummonBase.prefab",
        "Assets/Prefab/Player/MinigunTerret_Summon.prefab",
        "Assets/Tests/DefensiveBlock/Player.Test.prefab",
        "Assets/Tests/DefensiveBlock/Aires.Test.prefab",
        "Assets/Tests/DefensiveBlock/Rector.Test.prefab"
    };

    public static List<string> Validate(bool includeIncompleteGuns = true)
    {
        var issues = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:GunConfig"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var gun = AssetDatabase.LoadAssetAtPath<GunConfig>(path);
            if (gun.WeaponPrefab == null)
            {
                if (includeIncompleteGuns) issues.Add(path + ": WeaponPrefab is missing; weapon cannot fire.");
                continue;
            }
            var binding = gun.WeaponPrefab.GetComponent<WeaponMuzzleBinding>();
            if (binding == null || !binding.IsValid)
                issues.Add(path + ": weapon-model muzzle binding is missing or outside its model.");
        }
        foreach (string path in ActorPrefabs)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (root == null) { issues.Add(path + ": prefab missing."); continue; }
            var owner = root.GetComponent<CharacteContext>();
            var sources = root.GetComponentsInChildren<WeaponAimSource>(true);
            if (owner == null || sources.Length != 1 || owner.WeaponAim != sources[0])
                issues.Add(path + ": bind exactly one actor-owned WeaponAim source on context.");
            if (root.transform.Find("FirePoint") != null || root.transform.Find("GameplayRoot/FirePoint") != null)
                issues.Add(path + ": obsolete actor FirePoint remains.");
            foreach (var binding in root.GetComponentsInChildren<WeaponMuzzleBinding>(true))
                if (!binding.IsValid) issues.Add(path + ": invalid integrated muzzle binding.");
        }
        return issues;
    }

    [MenuItem("Tools/RB/Weapons/Validate Weapon Muzzles")]
    public static void Run()
    {
        var issues = Validate();
        foreach (string issue in issues) Debug.LogWarning(issue);
        Debug.Log($"Weapon muzzle authoring: {issues.Count} configuration issue(s).");
    }
}
#endif
