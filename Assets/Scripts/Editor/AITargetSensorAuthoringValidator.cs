using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class AITargetSensorAuthoringValidator
{
    [MenuItem("Tools/RB/AI/Validate Target Sensors")]
    public static void Validate()
    {
        var issues = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefab" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (root == null) continue;
            foreach (AITargetSensor sensor in root.GetComponentsInChildren<AITargetSensor>(true))
                Check(sensor, path, issues);
        }
        foreach (AITargetSensor sensor in Object.FindObjectsByType<AITargetSensor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Check(sensor, sensor.gameObject.scene.path, issues);
        Debug.Log($"AI target sensor validation: {issues.Count} issues.\n" + string.Join("\n", issues));
    }

    static void Check(AITargetSensor sensor, string path, List<string> output)
    {
        var local = new List<string>();
        sensor.CollectAuthoringIssues(local);
        foreach (string issue in local) output.Add($"{path}/{sensor.name}: {issue}");
    }
}
