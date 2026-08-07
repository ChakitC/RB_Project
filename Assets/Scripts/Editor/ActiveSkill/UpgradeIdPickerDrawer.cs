using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(UpgradeIdPickerAttribute))]
public sealed class UpgradeIdPickerDrawer : PropertyDrawer
{
    static readonly Dictionary<int, List<string>> IdCacheByTreeInstanceId = new();
    static readonly HashSet<string> CustomModePropertyKeys = new();

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        string propertyKey = GetPropertyKey(property);
        List<string> ids = ResolveIds(property);
        bool customMode = CustomModePropertyKeys.Contains(propertyKey) || ids == null || ids.Count == 0;

        string currentValue = property.stringValue;

        if (customMode)
        {
            EditorGUI.BeginChangeCheck();
            string newValue = EditorGUI.TextField(position, label, currentValue);
            if (EditorGUI.EndChangeCheck())
                property.stringValue = newValue;
            return;
        }

        bool valueMissingFromSet = !string.IsNullOrWhiteSpace(currentValue) && !ids.Contains(currentValue);

        Rect fieldRect = EditorGUI.PrefixLabel(position, label);
        Color previousColor = GUI.color;
        if (valueMissingFromSet)
            GUI.color = new Color(1f, 0.65f, 0.3f);

        string buttonLabel = string.IsNullOrWhiteSpace(currentValue) ? "<none>" : currentValue;
        bool clicked = EditorGUI.DropdownButton(fieldRect, new GUIContent(buttonLabel), FocusType.Keyboard);
        GUI.color = previousColor;

        if (!clicked)
            return;

        SerializedObject serializedObject = property.serializedObject;
        string propertyPath = property.propertyPath;

        var sortedIds = new List<string>(ids);
        sortedIds.Sort(StringComparer.Ordinal);

        var menu = new GenericMenu();
        for (int i = 0; i < sortedIds.Count; i++)
        {
            string id = sortedIds[i];
            menu.AddItem(new GUIContent(id), string.Equals(id, currentValue, StringComparison.Ordinal), () =>
            {
                SerializedProperty target = serializedObject.FindProperty(propertyPath);
                if (target == null)
                    return;

                serializedObject.Update();
                target.stringValue = id;
                serializedObject.ApplyModifiedProperties();
            });
        }

        menu.AddSeparator("");
        menu.AddItem(new GUIContent("Custom…"), false, () => CustomModePropertyKeys.Add(propertyKey));
        menu.DropDown(fieldRect);
    }

    static List<string> ResolveIds(SerializedProperty property)
    {
        if (property.serializedObject.targetObject is not SkillUpgradeTreeDefinition tree)
            return null;

        int instanceId = tree.GetInstanceID();
        if (IdCacheByTreeInstanceId.TryGetValue(instanceId, out List<string> cached))
            return cached;

        IReadOnlyList<SkillGemDefinition> owners = SkillUpgradeTreeValidator.FindOwningSkills(tree);
        var collected = new List<string>();
        for (int i = 0; i < owners.Count; i++)
            owners[i]?.CollectUpgradeIds(collected);

        var unique = new List<string>();
        for (int i = 0; i < collected.Count; i++)
        {
            if (!unique.Contains(collected[i]))
                unique.Add(collected[i]);
        }

        IdCacheByTreeInstanceId[instanceId] = unique;
        return unique;
    }

    static string GetPropertyKey(SerializedProperty property)
    {
        int targetId = property.serializedObject.targetObject != null
            ? property.serializedObject.targetObject.GetInstanceID()
            : 0;
        return $"{targetId}:{property.propertyPath}";
    }

    public static void ClearCache()
    {
        IdCacheByTreeInstanceId.Clear();
    }
}

sealed class UpgradeIdPickerCacheInvalidator : AssetPostprocessor
{
    static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        UpgradeIdPickerDrawer.ClearCache();
    }
}
