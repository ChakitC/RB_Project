#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// วาด <see cref="ConditionalStatusRoute"/> เป็นบล็อกเดียว: หัวเรื่อง + Target ที่อ่านจาก
/// <see cref="SkillStatusRouteTargetAttribute"/> (read-only) + รายการ application.
///
/// จงใจไม่เพิ่ม foldout อีกชั้นรอบ list เพราะ route หนึ่งตัวคือ list เดียวอยู่แล้ว — การซ้อน foldout
/// ทำให้ต้องกดสองครั้งกว่าจะเห็นสิ่งที่ field นี้มีอยู่จริง. Target โชว์เป็นข้อความ ไม่ให้แก้ตรงนี้
/// เพราะมันมาจาก declaration ของ payload/step ไม่ใช่ค่าที่ author ได้.
/// </summary>
[CustomPropertyDrawer(typeof(ConditionalStatusRoute))]
public sealed class ConditionalStatusRouteDrawer : PropertyDrawer
{
    const float Spacing = 2f;
    const float MoveButtonWidth = 28f;
    const float RemoveButtonWidth = 24f;
    const float AddButtonWidth = 116f;
    const float IndexWidth = 24f;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float line = EditorGUIUtility.singleLineHeight;
        SerializedProperty applications = GetApplications(property);
        if (applications == null)
            return line * 2f + Spacing;

        // header + target line
        float height = (line + Spacing) * 2f;

        if (applications.arraySize == 0)
        {
            height += line + Spacing;
        }
        else
        {
            for (int i = 0; i < applications.arraySize; i++)
            {
                SerializedProperty element = applications.GetArrayElementAtIndex(i);
                height += line + Spacing;
                height += EditorGUI.GetPropertyHeight(
                    element.FindPropertyRelative("spec"), GUIContent.none, true) + Spacing;
            }
        }

        // add button
        height += line + Spacing;
        return height;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        float line = EditorGUIUtility.singleLineHeight;
        float y = position.y;

        SerializedProperty applications = GetApplications(property);
        if (applications == null)
        {
            EditorGUI.HelpBox(
                new Rect(position.x, y, position.width, line * 2f),
                $"'{property.displayName}' has no '{ConditionalStatusRoute.ApplicationsFieldName}' list.",
                MessageType.Error);
            EditorGUI.EndProperty();
            return;
        }

        string title = label != null && !string.IsNullOrEmpty(label.text)
            ? label.text
            : "Conditional Status Effects";
        EditorGUI.LabelField(NextLine(position, ref y, line), title, EditorStyles.boldLabel);

        DrawTargetLine(NextLine(position, ref y, line), property);

        if (applications.arraySize == 0)
        {
            EditorGUI.LabelField(
                NextLine(position, ref y, line),
                "No conditional status effects. Use Add Application to gate one behind an upgrade id.",
                EditorStyles.miniLabel);
        }
        else
        {
            for (int i = 0; i < applications.arraySize; i++)
            {
                if (DrawApplication(position, ref y, line, applications, i))
                    break;
            }
        }

        Rect addRow = NextLine(position, ref y, line);
        Rect addRect = new Rect(addRow.xMax - AddButtonWidth, addRow.y, AddButtonWidth, addRow.height);
        if (GUI.Button(addRect, "Add Application"))
        {
            int index = applications.arraySize;
            applications.InsertArrayElementAtIndex(index);
            ResetApplication(applications.GetArrayElementAtIndex(index));
        }

        EditorGUI.EndProperty();
    }

    /// <summary>
    /// Target มาจาก declaration ไม่ใช่จากข้อมูลที่ serialize ไว้ — ถ้าประกาศผิดต้องขึ้น error ตรงนี้
    /// ไม่ใช่เดาเป็น Self เงียบๆ แล้วให้ designer ไปเจอผลผิดตอนเล่น.
    /// </summary>
    void DrawTargetLine(Rect rect, SerializedProperty property)
    {
        SkillStatusRouteFieldInfo info = fieldInfo != null
            ? SkillStatusRouteMetadata.FindRouteField(fieldInfo.DeclaringType, fieldInfo.Name)
            : null;

        object owner = SerializedPropertyOwnerUtility.GetOwner(property);
        if (!SkillStatusRouteMetadata.TryResolveTarget(info, owner, out SkillStatusTarget target,
                out string error))
        {
            EditorGUI.LabelField(rect, "Target", $"<invalid> — {error}", EditorStyles.miniLabel);
            return;
        }

        using (new EditorGUI.DisabledScope(true))
            EditorGUI.LabelField(rect, "Target", SkillStatusRoute.DescribeTarget(target));
    }

    bool DrawApplication(Rect position, ref float y, float line, SerializedProperty applications, int index)
    {
        SerializedProperty element = applications.GetArrayElementAtIndex(index);
        Rect header = NextLine(position, ref y, line);

        float controlsWidth = MoveButtonWidth * 2f + RemoveButtonWidth + Spacing * 3f;
        Rect indexRect = new Rect(header.x, header.y, IndexWidth, header.height);
        Rect idRect = new Rect(
            header.x + IndexWidth + Spacing,
            header.y,
            Mathf.Max(60f, header.width - IndexWidth - controlsWidth - Spacing * 2f),
            header.height);

        EditorGUI.LabelField(indexRect, (index + 1).ToString());

        SerializedProperty id = element.FindPropertyRelative("requiredUpgradeId");
        if (id != null)
        {
            float previousLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 130f;
            EditorGUI.PropertyField(idRect, id, new GUIContent("Required Upgrade Id"));
            EditorGUIUtility.labelWidth = previousLabelWidth;
        }

        float x = idRect.xMax + Spacing;
        Rect upRect = new Rect(x, header.y, MoveButtonWidth, header.height);
        x += MoveButtonWidth + Spacing;
        Rect downRect = new Rect(x, header.y, MoveButtonWidth, header.height);
        x += MoveButtonWidth + Spacing;
        Rect removeRect = new Rect(x, header.y, RemoveButtonWidth, header.height);

        SerializedProperty spec = element.FindPropertyRelative("spec");
        float specHeight = EditorGUI.GetPropertyHeight(spec, GUIContent.none, true);
        Rect specRect = new Rect(position.x, y, position.width, specHeight);
        y += specHeight + Spacing;

        EditorGUI.indentLevel++;
        EditorGUI.PropertyField(specRect, spec, new GUIContent("Status Effect"), true);
        EditorGUI.indentLevel--;

        using (new EditorGUI.DisabledScope(index <= 0))
        {
            if (GUI.Button(upRect, "Up"))
            {
                applications.MoveArrayElement(index, index - 1);
                return true;
            }
        }

        using (new EditorGUI.DisabledScope(index >= applications.arraySize - 1))
        {
            if (GUI.Button(downRect, "Dn"))
            {
                applications.MoveArrayElement(index, index + 1);
                return true;
            }
        }

        if (GUI.Button(removeRect, "-"))
        {
            applications.DeleteArrayElementAtIndex(index);
            return true;
        }

        return false;
    }

    static Rect NextLine(Rect area, ref float y, float lineHeight)
    {
        var rect = new Rect(area.x, y, area.width, lineHeight);
        y += lineHeight + Spacing;
        return rect;
    }

    static SerializedProperty GetApplications(SerializedProperty property) =>
        property?.FindPropertyRelative(ConditionalStatusRoute.ApplicationsFieldName);

    // InsertArrayElementAtIndex copies the previous element, so a fresh row would silently inherit
    // the row above it (including its status and overrides) if it is not cleared here.
    static void ResetApplication(SerializedProperty element)
    {
        SerializedProperty id = element.FindPropertyRelative("requiredUpgradeId");
        if (id != null)
            id.stringValue = string.Empty;

        SerializedProperty spec = element.FindPropertyRelative("spec");
        if (spec == null)
            return;

        SetObject(spec, "effect", null);
        SetInt(spec, "stacks", 1);
        SerializedProperty modifiers = spec.FindPropertyRelative("modifiers");
        if (modifiers != null && modifiers.isArray)
            modifiers.arraySize = 0;

        SetBool(spec, "modifiersOverrideEnabled", false);
        SetFloat(spec, "durationOverride", 0f);
        SetBool(spec, "durationOverrideEnabled", false);
        SetFloat(spec, "tickDamageOverride", 0f);
        SetBool(spec, "tickDamageOverrideEnabled", false);
        SetFloat(spec, "tickIntervalOverride", 0f);
        SetBool(spec, "tickIntervalOverrideEnabled", false);
    }

    static void SetObject(SerializedProperty spec, string name, Object value)
    {
        SerializedProperty property = spec.FindPropertyRelative(name);
        if (property != null)
            property.objectReferenceValue = value;
    }

    static void SetInt(SerializedProperty spec, string name, int value)
    {
        SerializedProperty property = spec.FindPropertyRelative(name);
        if (property != null)
            property.intValue = value;
    }

    static void SetFloat(SerializedProperty spec, string name, float value)
    {
        SerializedProperty property = spec.FindPropertyRelative(name);
        if (property != null)
            property.floatValue = value;
    }

    static void SetBool(SerializedProperty spec, string name, bool value)
    {
        SerializedProperty property = spec.FindPropertyRelative(name);
        if (property != null)
            property.boolValue = value;
    }
}
#endif
