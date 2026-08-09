#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// วาด StatusApplicationSpec ทุกจุดที่ apply site ถืออยู่ (Skill payload, Active Skill Tree embedded step,
/// projectile module, passive, pickup) ผ่าน [CustomPropertyDrawer] — ไม่ใช้ Odin [ShowInInspector] property
/// เพราะบางหน้าวาดด้วย SerializedProperty ตรงๆ และจะมองไม่เห็น non-serialized property.
///
/// แต่ละ channel (modifiers / duration / tick damage / tick interval) ติดตามค่าใน StatusEffectDef จนกว่าจะถูก
/// แก้ครั้งแรก ซึ่งจะคัดลอกค่าที่เห็นอยู่ขึ้นมาเป็น application override พร้อมเปิด enable flag ของ channel นั้น
/// — enable flag ทำให้ค่า 0 หลัง override เป็นค่าจริง (duration 0 = permanent, tick 0 = ปิด) ไม่ใช่ sentinel.
/// </summary>
[CustomPropertyDrawer(typeof(StatusApplicationSpec))]
public sealed class StatusApplicationSpecDrawer : PropertyDrawer
{
    const float Spacing = 2f;
    const float IndexWidth = 24f;
    const float MoveButtonWidth = 28f;
    const float RemoveButtonWidth = 24f;
    const float AddButtonWidth = 76f;
    const float ResetButtonWidth = 152f;
    const float ScalarResetWidth = 52f;
    const float ScalarSourceWidth = 132f;
    const float TickButtonWidth = 152f;

    /// <summary>ชื่อ field ของ scalar channel: ค่า / enable flag / ป้ายที่แสดง.</summary>
    struct ScalarChannel
    {
        public string ValueField;
        public string EnabledField;
        public string Label;
        public bool AllowNegative;
    }

    static readonly ScalarChannel[] ScalarChannels =
    {
        new ScalarChannel { ValueField = "durationOverride", EnabledField = "durationOverrideEnabled", Label = "Duration", AllowNegative = false },
        new ScalarChannel { ValueField = "tickDamageOverride", EnabledField = "tickDamageOverrideEnabled", Label = "Tick Damage", AllowNegative = true },
        new ScalarChannel { ValueField = "tickIntervalOverride", EnabledField = "tickIntervalOverrideEnabled", Label = "Tick Interval", AllowNegative = false },
    };

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float line = EditorGUIUtility.singleLineHeight;
        if (!property.isExpanded)
            return line;

        int modifierCount = GetDisplayedModifierCount(property);
        // effect + stacks + modifier header + column header + rows + duration + tick header (+ two tick fields when active)
        int scalarLines = 2 + (ShouldShowTickFields(property) ? 2 : 0);
        int bodyLines = 2 + 2 + Mathf.Max(1, modifierCount) + scalarLines;
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

        DrawScalarChannel(NextLine(indented, ref y, line), property, ScalarChannels[0], true);
        DrawTickHeader(NextLine(indented, ref y, line), property);
        if (ShouldShowTickFields(property))
        {
            DrawScalarChannel(NextLine(indented, ref y, line), property, ScalarChannels[1], false);
            DrawScalarChannel(NextLine(indented, ref y, line), property, ScalarChannels[2], false);
        }

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

    // ---- effect ----

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

        if (!HasAnyOverride(property))
        {
            effect.objectReferenceValue = next;
            return;
        }

        int choice = EditorUtility.DisplayDialogComplex(
            "Change Status Effect",
            "This application overrides values from its Status Effect Def (modifiers and/or duration, tick damage, " +
            "tick interval). Reset every override to the new Status Effect Def, or keep them all?",
            "Reset To New Def",
            "Cancel",
            "Keep Override");

        if (choice == 1)
            return;

        effect.objectReferenceValue = next;

        // "Keep Override" ไม่ต้องทำอะไร — enable flag ของทุก channel ถูกเขียนไว้ชัดเจนอยู่แล้ว
        if (choice == 0)
            ClearAllOverrides(property);
    }

    // ---- modifiers ----

    static void DrawModifierHeader(Rect rect, SerializedProperty property)
    {
        bool hasOverride = HasModifierOverride(property);
        string status = hasOverride
            ? "Modifiers: Using Application Override"
            : "Modifiers: Using Status Effect Def Values";

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
                ClearModifierOverride(property);
        }

        Rect addRect = new Rect(buttonX, rect.y, AddButtonWidth, rect.height);
        if (GUI.Button(addRect, "Add Modifier"))
        {
            EnsureModifierOverride(property);
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

            EnsureModifierOverride(property);
            SerializedProperty modifier = GetModifiers(property).GetArrayElementAtIndex(index);
            SetModifierValues(modifier, nextStat, nextOperation, nextValue);
        }

        using (new EditorGUI.DisabledScope(index <= 0))
        {
            if (GUI.Button(upRect, "Up"))
            {
                EnsureModifierOverride(property);
                GetModifiers(property).MoveArrayElement(index, index - 1);
                return true;
            }
        }

        using (new EditorGUI.DisabledScope(index >= modifierCount - 1))
        {
            if (GUI.Button(downRect, "Dn"))
            {
                EnsureModifierOverride(property);
                GetModifiers(property).MoveArrayElement(index, index + 1);
                return true;
            }
        }

        if (GUI.Button(removeRect, "-"))
        {
            EnsureModifierOverride(property);
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

    // ---- scalar channels ----

    static void DrawScalarChannel(
        Rect rect,
        SerializedProperty property,
        ScalarChannel channel,
        bool showResetButton)
    {
        bool hasOverride = HasScalarOverride(property, channel);
        float displayedValue = hasOverride
            ? GetScalarValue(property, channel)
            : GetDefinitionScalar(property, channel);

        float labelWidth = EditorGUIUtility.labelWidth;
        float controlsWidth = ScalarSourceWidth + Spacing +
            (hasOverride && showResetButton ? ScalarResetWidth + Spacing : 0f);
        float fieldWidth = Mathf.Max(40f, rect.width - labelWidth - controlsWidth);

        Rect labelRect = new Rect(rect.x, rect.y, labelWidth, rect.height);
        Rect fieldRect = new Rect(rect.x + labelWidth, rect.y, fieldWidth, rect.height);
        Rect sourceRect = new Rect(fieldRect.xMax + Spacing, rect.y, ScalarSourceWidth, rect.height);

        EditorGUI.LabelField(labelRect, channel.Label);

        EditorGUI.BeginChangeCheck();
        float nextValue = EditorGUI.FloatField(fieldRect, displayedValue);
        if (EditorGUI.EndChangeCheck())
        {
            if (!channel.AllowNegative)
                nextValue = Mathf.Max(0f, nextValue);

            // แก้ครั้งแรกต้องเปิด flag และเก็บค่าที่ผู้ใช้เพิ่งพิมพ์ (ไม่ใช่ค่าเดิมของ Def)
            SetScalarEnabled(property, channel, true);
            SetScalarValue(property, channel, nextValue);
        }

        EditorGUI.LabelField(
            sourceRect,
            hasOverride ? "Application Override" : "From Status Effect Def",
            EditorStyles.miniLabel);

        if (!hasOverride || !showResetButton)
            return;

        Rect resetRect = new Rect(sourceRect.xMax + Spacing, rect.y, ScalarResetWidth, rect.height);
        if (GUI.Button(resetRect, "Reset"))
            ClearScalarOverride(property, channel);
    }

    static void DrawTickHeader(Rect rect, SerializedProperty property)
    {
        bool hasOverride = HasTickOverride(property);
        bool definitionHasTick = DefinitionHasTick(property);

        string status;
        if (hasOverride)
            status = "Tick: Using Application Override";
        else if (definitionHasTick)
            status = "Tick: Using Status Effect Def Values";
        else
            status = "Tick: Not Used";

        Rect labelRect = rect;
        labelRect.width = Mathf.Max(0f, rect.width - TickButtonWidth - Spacing);
        EditorGUI.LabelField(labelRect, status, EditorStyles.boldLabel);

        Rect buttonRect = new Rect(rect.xMax - TickButtonWidth, rect.y, TickButtonWidth, rect.height);
        if (hasOverride)
        {
            if (GUI.Button(buttonRect, "Reset Tick To Status Effect Def"))
                ClearTickOverride(property);
            return;
        }

        if (definitionHasTick)
            return;

        using (new EditorGUI.DisabledScope(GetDefinition(property) == null))
        {
            if (GUI.Button(buttonRect, "Add Tick Override"))
                EnableTickOverride(property);
        }
    }

    static bool ShouldShowTickFields(SerializedProperty property)
    {
        return DefinitionHasTick(property) || HasTickOverride(property);
    }

    static bool DefinitionHasTick(SerializedProperty property)
    {
        StatusEffectDef definition = GetDefinition(property);
        return definition != null && definition.HasTick;
    }

    static bool HasTickOverride(SerializedProperty property)
    {
        return HasScalarOverride(property, ScalarChannels[1]) ||
               HasScalarOverride(property, ScalarChannels[2]);
    }

    static void EnableTickOverride(SerializedProperty property)
    {
        ScalarChannel tickDamage = ScalarChannels[1];
        ScalarChannel tickInterval = ScalarChannels[2];

        SetScalarValue(property, tickDamage, GetDefinitionScalar(property, tickDamage));
        SetScalarValue(property, tickInterval, GetDefinitionScalar(property, tickInterval));
        SetScalarEnabled(property, tickDamage, true);
        SetScalarEnabled(property, tickInterval, true);
    }

    static void ClearTickOverride(SerializedProperty property)
    {
        ClearScalarOverride(property, ScalarChannels[1]);
        ClearScalarOverride(property, ScalarChannels[2]);
    }

    static float GetDefinitionScalar(SerializedProperty property, ScalarChannel channel)
    {
        StatusEffectDef definition = GetDefinition(property);
        if (definition == null)
            return 0f;

        if (channel.ValueField == "durationOverride")
            return definition.duration;
        if (channel.ValueField == "tickDamageOverride")
            return definition.tickDamage;

        return definition.tickInterval;
    }

    /// <summary>ตรงกับ HasDurationOverride / HasTickDamageOverride / HasTickIntervalOverride ฝั่ง runtime เป๊ะ.</summary>
    static bool HasScalarOverride(SerializedProperty property, ScalarChannel channel)
    {
        SerializedProperty enabled = property.FindPropertyRelative(channel.EnabledField);
        return enabled != null && enabled.boolValue;
    }

    static float GetScalarValue(SerializedProperty property, ScalarChannel channel)
    {
        SerializedProperty value = property.FindPropertyRelative(channel.ValueField);
        return value != null ? value.floatValue : 0f;
    }

    static void SetScalarValue(SerializedProperty property, ScalarChannel channel, float value)
    {
        SerializedProperty target = property.FindPropertyRelative(channel.ValueField);
        if (target != null)
            target.floatValue = value;
    }

    static void SetScalarEnabled(SerializedProperty property, ScalarChannel channel, bool enabled)
    {
        SerializedProperty target = property.FindPropertyRelative(channel.EnabledField);
        if (target != null)
            target.boolValue = enabled;
    }

    static void ClearScalarOverride(SerializedProperty property, ScalarChannel channel)
    {
        SetScalarEnabled(property, channel, false);
        SetScalarValue(property, channel, 0f);
    }

    // ---- override state ----

    static bool HasAnyOverride(SerializedProperty property)
    {
        if (HasModifierOverride(property))
            return true;

        for (int i = 0; i < ScalarChannels.Length; i++)
        {
            if (HasScalarOverride(property, ScalarChannels[i]))
                return true;
        }

        return false;
    }

    static void ClearAllOverrides(SerializedProperty property)
    {
        ClearModifierOverride(property);
        for (int i = 0; i < ScalarChannels.Length; i++)
            ClearScalarOverride(property, ScalarChannels[i]);
    }

    static bool HasModifierOverride(SerializedProperty property)
    {
        SerializedProperty enabled = property.FindPropertyRelative("modifiersOverrideEnabled");
        return enabled != null && enabled.boolValue;
    }

    static void EnsureModifierOverride(SerializedProperty property)
    {
        if (HasModifierOverride(property))
        {
            SetModifierOverrideEnabled(property, true);
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

        SetModifierOverrideEnabled(property, true);
    }

    static void ClearModifierOverride(SerializedProperty property)
    {
        SerializedProperty modifiers = GetModifiers(property);
        if (modifiers != null)
            modifiers.arraySize = 0;

        SetModifierOverrideEnabled(property, false);
    }

    static void SetModifierOverrideEnabled(SerializedProperty property, bool enabled)
    {
        SerializedProperty overrideEnabled = property.FindPropertyRelative("modifiersOverrideEnabled");
        if (overrideEnabled != null)
            overrideEnabled.boolValue = enabled;
    }

    static SerializedProperty GetModifiers(SerializedProperty property)
    {
        return property.FindPropertyRelative("modifiers");
    }

    static StatusEffectDef GetDefinition(SerializedProperty property)
    {
        return property.FindPropertyRelative("effect")?.objectReferenceValue as StatusEffectDef;
    }

    static IReadOnlyList<StatusEffectModifier> GetDefinitionModifiers(SerializedProperty property)
    {
        StatusEffectDef definition = GetDefinition(property);
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
