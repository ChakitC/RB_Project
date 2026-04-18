#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(PrefabHitboxSkillPayloadDef.HitboxStep))]
public sealed class PrefabHitboxStepDrawer : PropertyDrawer
{
    const float VerticalSpacing = 2f;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight;
        if (!property.isExpanded)
            return height;

        height += VerticalSpacing;
        height += GetVisiblePropertyHeight(property.FindPropertyRelative("timelineBindingMode"));
        height += GetVisiblePropertyHeight(property.FindPropertyRelative("activateEventOverride"));
        height += GetVisiblePropertyHeight(property.FindPropertyRelative("deactivateEventOverride"));
        height += HitboxGroupKeyListGUI.GetPropertyHeight(property.FindPropertyRelative("groupKeys"));
        height += GetVisiblePropertyHeight(property.FindPropertyRelative("damageMultiplier"));
        height += GetVisiblePropertyHeight(property.FindPropertyRelative("hitPolicy"));
        height += GetVisiblePropertyHeight(property.FindPropertyRelative("clearHitCacheOnEnter"));
        height += GetVisiblePropertyHeight(property.FindPropertyRelative("overrideKnockback"));

        SerializedProperty overrideKnockback = property.FindPropertyRelative("overrideKnockback");
        if (overrideKnockback != null && overrideKnockback.boolValue)
        {
            height += GetVisiblePropertyHeight(property.FindPropertyRelative("knockbackDistance"));
            height += GetVisiblePropertyHeight(property.FindPropertyRelative("knockbackDuration"));
            height += GetVisiblePropertyHeight(property.FindPropertyRelative("knockbackProgressCurve"));
            height += GetVisiblePropertyHeight(property.FindPropertyRelative("knockbackReaction"));
            height += GetVisiblePropertyHeight(property.FindPropertyRelative("knockbackInterruptsActions"));
        }

        height += GetVisiblePropertyHeight(property.FindPropertyRelative("stepStartVfx"));
        height += GetVisiblePropertyHeight(property.FindPropertyRelative("impactVfx"));
        return height;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        float lineHeight = EditorGUIUtility.singleLineHeight;

        Rect headerRect = new Rect(position.x, position.y, position.width, lineHeight);
        property.isExpanded = EditorGUI.Foldout(headerRect, property.isExpanded, label, true);

        if (!property.isExpanded)
        {
            EditorGUI.EndProperty();
            return;
        }

        float y = headerRect.yMax + VerticalSpacing;

        EditorGUI.indentLevel++;
        DrawProperty(ref y, position, property.FindPropertyRelative("timelineBindingMode"));
        DrawProperty(ref y, position, property.FindPropertyRelative("activateEventOverride"));
        DrawProperty(ref y, position, property.FindPropertyRelative("deactivateEventOverride"));
        HitboxGroupKeyListGUI.Draw(ref y, position, property.FindPropertyRelative("groupKeys"));
        DrawProperty(ref y, position, property.FindPropertyRelative("damageMultiplier"));
        DrawProperty(ref y, position, property.FindPropertyRelative("hitPolicy"));
        DrawProperty(ref y, position, property.FindPropertyRelative("clearHitCacheOnEnter"));
        DrawProperty(ref y, position, property.FindPropertyRelative("overrideKnockback"));

        SerializedProperty overrideKnockback = property.FindPropertyRelative("overrideKnockback");
        if (overrideKnockback != null && overrideKnockback.boolValue)
        {
            DrawProperty(ref y, position, property.FindPropertyRelative("knockbackDistance"));
            DrawProperty(ref y, position, property.FindPropertyRelative("knockbackDuration"));
            DrawProperty(ref y, position, property.FindPropertyRelative("knockbackProgressCurve"));
            DrawProperty(ref y, position, property.FindPropertyRelative("knockbackReaction"));
            DrawProperty(ref y, position, property.FindPropertyRelative("knockbackInterruptsActions"));
        }

        DrawProperty(ref y, position, property.FindPropertyRelative("stepStartVfx"));
        DrawProperty(ref y, position, property.FindPropertyRelative("impactVfx"));
        EditorGUI.indentLevel--;
        EditorGUI.EndProperty();
    }

    static float GetVisiblePropertyHeight(SerializedProperty property)
    {
        if (property == null)
            return 0f;

        return EditorGUI.GetPropertyHeight(property, true) + VerticalSpacing;
    }

    static void DrawProperty(ref float y, Rect totalRect, SerializedProperty property)
    {
        if (property == null)
            return;

        float height = EditorGUI.GetPropertyHeight(property, true);
        Rect rect = new Rect(totalRect.x, y, totalRect.width, height);
        EditorGUI.PropertyField(rect, property, true);
        y += height + VerticalSpacing;
    }
}

static class HitboxGroupKeyListGUI
{
    const float ButtonWidth = 72f;
    const float RemoveButtonWidth = 24f;
    const float VerticalSpacing = 2f;

    public static float GetPropertyHeight(SerializedProperty property)
    {
        float lineHeight = EditorGUIUtility.singleLineHeight;
        float height = lineHeight;
        if (property == null || !property.isExpanded)
            return height + VerticalSpacing;

        HitboxGroupOptions options = BuildOptions(property);
        string helpMessage = GetHelpMessage(property, options);
        if (!string.IsNullOrEmpty(helpMessage))
            height += VerticalSpacing + GetHelpBoxHeight(helpMessage);

        int rowCount = Mathf.Max(1, property.arraySize);
        height += rowCount * (lineHeight + VerticalSpacing);
        return height + VerticalSpacing;
    }

    public static void Draw(ref float y, Rect totalRect, SerializedProperty property)
    {
        if (property == null)
            return;

        HitboxGroupOptions options = BuildOptions(property);
        string helpMessage = GetHelpMessage(property, options);
        float lineHeight = EditorGUIUtility.singleLineHeight;

        Rect position = new Rect(totalRect.x, y, totalRect.width, GetPropertyHeight(property) - VerticalSpacing);
        Rect headerRect = new Rect(position.x, position.y, position.width, lineHeight);
        Rect foldoutRect = headerRect;
        foldoutRect.width -= ButtonWidth + 4f;

        string headerLabel = property.arraySize > 0
            ? $"{property.displayName} ({property.arraySize})"
            : property.displayName;
        property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, headerLabel, true);

        string addKey = GetFirstUnselectedKey(property, options.AvailableKeys);
        using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(addKey)))
        {
            Rect addButtonRect = new Rect(headerRect.xMax - ButtonWidth, headerRect.y, ButtonWidth, lineHeight);
            if (GUI.Button(addButtonRect, "Add Group"))
                AddGroup(property, addKey);
        }

        if (!property.isExpanded)
        {
            y += position.height + VerticalSpacing;
            return;
        }

        float rowY = headerRect.yMax + VerticalSpacing;
        if (!string.IsNullOrEmpty(helpMessage))
        {
            float helpHeight = GetHelpBoxHeight(helpMessage);
            Rect helpRect = new Rect(position.x, rowY, position.width, helpHeight);
            EditorGUI.HelpBox(helpRect, helpMessage, MessageType.Warning);
            rowY = helpRect.yMax + VerticalSpacing;
        }

        EditorGUI.indentLevel++;
        if (property.arraySize == 0)
        {
            Rect emptyRect = new Rect(position.x, rowY, position.width, lineHeight);
            EditorGUI.LabelField(emptyRect, "No hitbox groups selected.");
        }
        else
        {
            for (int i = 0; i < property.arraySize; i++)
            {
                SerializedProperty element = property.GetArrayElementAtIndex(i);
                Rect rowRect = new Rect(position.x, rowY, position.width, lineHeight);
                DrawGroupKeyRow(rowRect, property, element, i, options.AvailableKeys);
                rowY += lineHeight + VerticalSpacing;
            }
        }

        EditorGUI.indentLevel--;
        y += position.height + VerticalSpacing;
    }

    static void DrawGroupKeyRow(
        Rect rowRect,
        SerializedProperty arrayProperty,
        SerializedProperty elementProperty,
        int index,
        List<string> availableKeys)
    {
        Rect contentRect = EditorGUI.IndentedRect(rowRect);
        Rect removeRect = new Rect(contentRect.xMax - RemoveButtonWidth, contentRect.y, RemoveButtonWidth, contentRect.height);
        Rect fieldRect = new Rect(contentRect.x, contentRect.y, contentRect.width - RemoveButtonWidth - 4f, contentRect.height);
        GUIContent elementLabel = new GUIContent($"Element {index}");

        if (availableKeys.Count > 0)
        {
            string currentValue = elementProperty.stringValue ?? string.Empty;
            int selectedIndex = BuildPopupData(currentValue, availableKeys, out string[] popupLabels, out string[] popupValues);
            int newIndex = EditorGUI.Popup(fieldRect, elementLabel.text, selectedIndex, popupLabels);
            if (newIndex >= 0 && newIndex < popupValues.Length && popupValues[newIndex] != currentValue)
                elementProperty.stringValue = popupValues[newIndex];
        }
        else
        {
            elementProperty.stringValue = EditorGUI.TextField(fieldRect, elementLabel, elementProperty.stringValue);
        }

        if (GUI.Button(removeRect, "-"))
        {
            arrayProperty.DeleteArrayElementAtIndex(index);
            arrayProperty.serializedObject.ApplyModifiedProperties();
        }
    }

    static int BuildPopupData(
        string currentValue,
        List<string> availableKeys,
        out string[] popupLabels,
        out string[] popupValues)
    {
        List<string> labels = new List<string>(availableKeys.Count + 1);
        List<string> values = new List<string>(availableKeys.Count + 1);
        int selectedIndex = -1;

        string normalizedCurrent = string.IsNullOrWhiteSpace(currentValue)
            ? string.Empty
            : currentValue.Trim();

        for (int i = 0; i < availableKeys.Count; i++)
        {
            string key = availableKeys[i];
            labels.Add(key);
            values.Add(key);

            if (selectedIndex < 0 && string.Equals(key, normalizedCurrent, StringComparison.OrdinalIgnoreCase))
                selectedIndex = i;
        }

        if (selectedIndex < 0)
        {
            string missingLabel = string.IsNullOrEmpty(normalizedCurrent)
                ? "<empty>"
                : $"{normalizedCurrent} (missing)";
            labels.Insert(0, missingLabel);
            values.Insert(0, normalizedCurrent);
            selectedIndex = 0;
        }

        popupLabels = labels.ToArray();
        popupValues = values.ToArray();
        return selectedIndex;
    }

    static void AddGroup(SerializedProperty property, string groupKey)
    {
        if (property == null || string.IsNullOrEmpty(groupKey))
            return;

        int newIndex = property.arraySize;
        property.InsertArrayElementAtIndex(newIndex);
        SerializedProperty newElement = property.GetArrayElementAtIndex(newIndex);
        newElement.stringValue = groupKey;
        property.serializedObject.ApplyModifiedProperties();
    }

    static string GetFirstUnselectedKey(SerializedProperty property, List<string> availableKeys)
    {
        if (property == null || availableKeys == null || availableKeys.Count == 0)
            return null;

        HashSet<string> selectedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < property.arraySize; i++)
        {
            string value = property.GetArrayElementAtIndex(i).stringValue;
            if (!string.IsNullOrWhiteSpace(value))
                selectedKeys.Add(value.Trim());
        }

        for (int i = 0; i < availableKeys.Count; i++)
        {
            string key = availableKeys[i];
            if (!selectedKeys.Contains(key))
                return key;
        }

        return null;
    }

    static string GetHelpMessage(SerializedProperty property, HitboxGroupOptions options)
    {
        if (options.HitBoxData == null)
            return "Assign Hit Box Data before selecting hitbox groups.";

        if (options.AvailableKeys.Count == 0)
            return $"SkillHitBoxData '{options.HitBoxData.name}' has no valid group keys.";

        List<string> missingKeys = new List<string>();
        for (int i = 0; i < property.arraySize; i++)
        {
            string currentValue = property.GetArrayElementAtIndex(i).stringValue;
            if (string.IsNullOrWhiteSpace(currentValue))
            {
                missingKeys.Add("<empty>");
                continue;
            }

            bool isKnown = false;
            for (int keyIndex = 0; keyIndex < options.AvailableKeys.Count; keyIndex++)
            {
                if (string.Equals(options.AvailableKeys[keyIndex], currentValue.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    isKnown = true;
                    break;
                }
            }

            if (!isKnown)
                missingKeys.Add(currentValue.Trim());
        }

        if (missingKeys.Count == 0)
            return null;

        return $"Missing from data: {string.Join(", ", missingKeys)}";
    }

    static float GetHelpBoxHeight(string message)
    {
        float width = Mathf.Max(120f, EditorGUIUtility.currentViewWidth - 56f);
        return EditorStyles.helpBox.CalcHeight(new GUIContent(message), width);
    }

    static HitboxGroupOptions BuildOptions(SerializedProperty property)
    {
        HitboxGroupOptions options = new HitboxGroupOptions();
        if (property == null)
            return options;

        SerializedProperty hitBoxDataProperty = property.serializedObject.FindProperty("hitBoxData");
        options.HitBoxData = hitBoxDataProperty != null
            ? hitBoxDataProperty.objectReferenceValue as SkillHitBoxData
            : null;

        if (options.HitBoxData == null)
            return options;

        HashSet<string> seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        IReadOnlyList<SkillHitBoxData.HitBoxGroupData> groups = options.HitBoxData.Groups;
        for (int i = 0; i < groups.Count; i++)
        {
            SkillHitBoxData.HitBoxGroupData group = groups[i];
            if (group == null)
                continue;

            string key = group.GroupKey;
            if (string.IsNullOrWhiteSpace(key) || !seenKeys.Add(key))
                continue;

            options.AvailableKeys.Add(key);
        }

        return options;
    }

    sealed class HitboxGroupOptions
    {
        public SkillHitBoxData HitBoxData;
        public readonly List<string> AvailableKeys = new List<string>();
    }
}
#endif
