#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(StatusApplicationSpec))]
public sealed class StatusApplicationSpecDrawer : PropertyDrawer
{
    const float Spacing = 2f;
    const float IndexWidth = 24f;
    const float MoveButtonWidth = 28f;
    const float RemoveButtonWidth = 24f;
    const float AddButtonWidth = 76f;
    const float ResetButtonWidth = 152f;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float line = EditorGUIUtility.singleLineHeight;
        if (!property.isExpanded)
            return line;

        int modifierCount = GetDisplayedModifierCount(property);
        int bodyLines = 2 + 2 + Mathf.Max(1, modifierCount) + 2;
        return line + Spacing + bodyLines * (line + Spacing);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        float line = EditorGUIUtility.singleLineHeight;
        Rect headerRect = new Rect(position.x, position.y, position.width, line);
        property.isExpanded = EditorGUI.Foldout(headerRect, property.isExpanded, label, true);
        if (!property.isExpanded)
        {
            EditorGUI.EndProperty();
            return;
        }

        int originalIndent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = originalIndent + 1;
        Rect indented = EditorGUI.IndentedRect(position);
        EditorGUI.indentLevel = 0;

        float y = headerRect.yMax + Spacing;
        DrawEffectField(NextLine(indented, ref y, line), property);
        DrawProperty(NextLine(indented, ref y, line), property.FindPropertyRelative("stacks"));
        DrawModifierHeader(NextLine(indented, ref y, line), property);
        DrawColumnHeaders(NextLine(indented, ref y, line));

        int modifierCount = GetDisplayedModifierCount(property);
        if (modifierCount == 0)
        {
            EditorGUI.LabelField(NextLine(indented, ref y, line), "No modifiers. Use Add Modifier to create an empty override entry.");
        }
        else
        {
            for (int i = 0; i < modifierCount; i++)
            {
                if (DrawModifierRow(NextLine(indented, ref y, line), property, i, modifierCount))
                    break;
            }
        }

        DrawProperty(NextLine(indented, ref y, line), property.FindPropertyRelative("durationOverride"));
        DrawProperty(NextLine(indented, ref y, line), property.FindPropertyRelative("tickDamageOverride"));

        EditorGUI.indentLevel = originalIndent;
        EditorGUI.EndProperty();
    }

    static Rect NextLine(Rect area, ref float y, float lineHeight)
    {
        Rect rect = new Rect(area.x, y, area.width, lineHeight);
        y += lineHeight + Spacing;
        return rect;
    }

    static void DrawProperty(Rect rect, SerializedProperty property)
    {
        if (property != null)
            EditorGUI.PropertyField(rect, property, true);
    }

    static void DrawEffectField(Rect rect, SerializedProperty property)
    {
        SerializedProperty effect = property.FindPropertyRelative("effect");
        if (effect == null)
            return;

        StatusEffectDef current = effect.objectReferenceValue as StatusEffectDef;
        EditorGUI.BeginChangeCheck();
        StatusEffectDef next = EditorGUI.ObjectField(
            rect,
            new GUIContent(effect.displayName, effect.tooltip),
            current,
            typeof(StatusEffectDef),
            false) as StatusEffectDef;

        if (!EditorGUI.EndChangeCheck() || next == current)
            return;

        if (!HasModifierOverride(property))
        {
            effect.objectReferenceValue = next;
            return;
        }

        int choice = EditorUtility.DisplayDialogComplex(
            "Change Status Effect",
            "This application has its own modifier override. Reset it to the new Status Effect Def, or keep the existing override?",
            "Reset To New Def",
            "Cancel",
            "Keep Override");

        if (choice == 1)
            return;

        effect.objectReferenceValue = next;
        if (choice == 0)
            ClearOverride(property);
        else
            SetOverrideEnabled(property, true);
    }

    static void DrawModifierHeader(Rect rect, SerializedProperty property)
    {
        bool hasOverride = HasModifierOverride(property);
        string status = hasOverride
            ? "Using Application Override"
            : "Using Status Effect Def Modifiers";

        float buttonsWidth = AddButtonWidth + Spacing;
        if (hasOverride)
            buttonsWidth += ResetButtonWidth + Spacing;

        Rect labelRect = rect;
        labelRect.width = Mathf.Max(0f, rect.width - buttonsWidth);
        EditorGUI.LabelField(labelRect, status, EditorStyles.boldLabel);

        float buttonX = rect.xMax - AddButtonWidth;
        if (hasOverride)
        {
            Rect resetRect = new Rect(buttonX - Spacing - ResetButtonWidth, rect.y, ResetButtonWidth, rect.height);
            if (GUI.Button(resetRect, "Reset To Status Effect Def"))
                ClearOverride(property);
        }

        Rect addRect = new Rect(buttonX, rect.y, AddButtonWidth, rect.height);
        if (GUI.Button(addRect, "Add Modifier"))
        {
            EnsureOverride(property);
            SerializedProperty modifiers = GetModifiers(property);
            int index = modifiers.arraySize;
            modifiers.InsertArrayElementAtIndex(index);
            SetModifierValues(modifiers.GetArrayElementAtIndex(index), StatType.Damage, ModifierOp.Flat, 0f);
        }
    }

    static void DrawColumnHeaders(Rect rect)
    {
        GetRowRects(rect, out Rect index, out Rect stat, out Rect operation, out Rect value, out _, out _, out _);
        EditorGUI.LabelField(index, "#", EditorStyles.miniLabel);
        EditorGUI.LabelField(stat, "Stat Type", EditorStyles.miniLabel);
        EditorGUI.LabelField(operation, "Operation", EditorStyles.miniLabel);
        EditorGUI.LabelField(value, "Value", EditorStyles.miniLabel);
    }

    static bool DrawModifierRow(Rect rect, SerializedProperty property, int index, int modifierCount)
    {
        bool hasOverride = HasModifierOverride(property);
        ReadModifierValues(property, index, hasOverride, out StatType statType, out ModifierOp operation, out float value);

        GetRowRects(rect, out Rect indexRect, out Rect statRect, out Rect operationRect, out Rect valueRect,
            out Rect upRect, out Rect downRect, out Rect removeRect);

        EditorGUI.LabelField(indexRect, (index + 1).ToString());

        EditorGUI.BeginChangeCheck();
        StatType nextStat = (StatType)EditorGUI.EnumPopup(statRect, statType);
        ModifierOp nextOperation = (ModifierOp)EditorGUI.EnumPopup(operationRect, operation);
        float nextValue = EditorGUI.FloatField(valueRect, value);
        if (EditorGUI.EndChangeCheck())
        {
            if (nextOperation == ModifierOp.Multiply &&
                operation != ModifierOp.Multiply &&
                Mathf.Approximately(nextValue, 0f))
            {
                nextValue = 1f;
            }

            EnsureOverride(property);
            SerializedProperty modifier = GetModifiers(property).GetArrayElementAtIndex(index);
            SetModifierValues(modifier, nextStat, nextOperation, nextValue);
        }

        using (new EditorGUI.DisabledScope(index <= 0))
        {
            if (GUI.Button(upRect, "Up"))
            {
                EnsureOverride(property);
                GetModifiers(property).MoveArrayElement(index, index - 1);
                return true;
            }
        }

        using (new EditorGUI.DisabledScope(index >= modifierCount - 1))
        {
            if (GUI.Button(downRect, "Dn"))
            {
                EnsureOverride(property);
                GetModifiers(property).MoveArrayElement(index, index + 1);
                return true;
            }
        }

        if (GUI.Button(removeRect, "-"))
        {
            EnsureOverride(property);
            GetModifiers(property).DeleteArrayElementAtIndex(index);
            return true;
        }

        return false;
    }

    static void GetRowRects(
        Rect rect,
        out Rect index,
        out Rect stat,
        out Rect operation,
        out Rect value,
        out Rect up,
        out Rect down,
        out Rect remove)
    {
        float controlsWidth = MoveButtonWidth * 2f + RemoveButtonWidth + Spacing * 6f;
        float fieldsWidth = Mathf.Max(0f, rect.width - IndexWidth - controlsWidth);
        float statWidth = fieldsWidth * 0.4f;
        float operationWidth = fieldsWidth * 0.34f;
        float valueWidth = fieldsWidth - statWidth - operationWidth;

        float x = rect.x;
        index = new Rect(x, rect.y, IndexWidth, rect.height);
        x += IndexWidth + Spacing;
        stat = new Rect(x, rect.y, statWidth, rect.height);
        x += statWidth + Spacing;
        operation = new Rect(x, rect.y, operationWidth, rect.height);
        x += operationWidth + Spacing;
        value = new Rect(x, rect.y, valueWidth, rect.height);
        x += valueWidth + Spacing;
        up = new Rect(x, rect.y, MoveButtonWidth, rect.height);
        x += MoveButtonWidth + Spacing;
        down = new Rect(x, rect.y, MoveButtonWidth, rect.height);
        x += MoveButtonWidth + Spacing;
        remove = new Rect(x, rect.y, RemoveButtonWidth, rect.height);
    }

    static void ReadModifierValues(
        SerializedProperty property,
        int index,
        bool hasOverride,
        out StatType statType,
        out ModifierOp operation,
        out float value)
    {
        if (hasOverride)
        {
            SerializedProperty modifier = GetModifiers(property).GetArrayElementAtIndex(index);
            statType = (StatType)modifier.FindPropertyRelative("statType").intValue;
            operation = (ModifierOp)modifier.FindPropertyRelative("operation").intValue;
            value = modifier.FindPropertyRelative("value").floatValue;
            return;
        }

        StatusEffectModifier modifierFromDefinition = GetDefinitionModifiers(property)[index];
        statType = modifierFromDefinition != null ? modifierFromDefinition.statType : StatType.Damage;
        operation = modifierFromDefinition != null ? modifierFromDefinition.operation : ModifierOp.Flat;
        value = modifierFromDefinition != null ? modifierFromDefinition.value : 0f;
    }

    static int GetDisplayedModifierCount(SerializedProperty property)
    {
        if (HasModifierOverride(property))
            return GetModifiers(property)?.arraySize ?? 0;

        return GetDefinitionModifiers(property).Count;
    }

    static bool HasModifierOverride(SerializedProperty property)
    {
        SerializedProperty enabled = property.FindPropertyRelative("modifiersOverrideEnabled");
        SerializedProperty modifiers = GetModifiers(property);
        return (enabled != null && enabled.boolValue) || (modifiers != null && modifiers.arraySize > 0);
    }

    static void EnsureOverride(SerializedProperty property)
    {
        if (HasModifierOverride(property))
        {
            SetOverrideEnabled(property, true);
            return;
        }

        IReadOnlyList<StatusEffectModifier> source = GetDefinitionModifiers(property);
        SerializedProperty modifiers = GetModifiers(property);
        modifiers.arraySize = 0;
        modifiers.arraySize = source.Count;

        for (int i = 0; i < source.Count; i++)
        {
            StatusEffectModifier modifier = source[i];
            SetModifierValues(
                modifiers.GetArrayElementAtIndex(i),
                modifier != null ? modifier.statType : StatType.Damage,
                modifier != null ? modifier.operation : ModifierOp.Flat,
                modifier != null ? modifier.value : 0f);
        }

        SetOverrideEnabled(property, true);
    }

    static void ClearOverride(SerializedProperty property)
    {
        SerializedProperty modifiers = GetModifiers(property);
        if (modifiers != null)
            modifiers.arraySize = 0;

        SetOverrideEnabled(property, false);
    }

    static void SetOverrideEnabled(SerializedProperty property, bool enabled)
    {
        SerializedProperty overrideEnabled = property.FindPropertyRelative("modifiersOverrideEnabled");
        if (overrideEnabled != null)
            overrideEnabled.boolValue = enabled;
    }

    static SerializedProperty GetModifiers(SerializedProperty property)
    {
        return property.FindPropertyRelative("modifiers");
    }

    static IReadOnlyList<StatusEffectModifier> GetDefinitionModifiers(SerializedProperty property)
    {
        StatusEffectDef definition = property.FindPropertyRelative("effect")?.objectReferenceValue as StatusEffectDef;
        return definition != null && definition.modifiers != null
            ? definition.modifiers
            : System.Array.Empty<StatusEffectModifier>();
    }

    static void SetModifierValues(
        SerializedProperty modifier,
        StatType statType,
        ModifierOp operation,
        float value)
    {
        modifier.FindPropertyRelative("statType").intValue = (int)statType;
        modifier.FindPropertyRelative("operation").intValue = (int)operation;
        modifier.FindPropertyRelative("value").floatValue = value;
    }
}
#endif
