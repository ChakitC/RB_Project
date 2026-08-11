#if UNITY_EDITOR
using System;
using System.Collections;
using System.Globalization;
using System.Reflection;
using UnityEditor;

/// <summary>
/// หา object ที่ "เป็นเจ้าของ" field ของ SerializedProperty หนึ่งตัว.
///
/// จำเป็นเพราะ drawer ของ <see cref="ConditionalStatusRoute"/> ต้องอ่าน target ที่ขึ้นกับ behavior
/// (เช่น HealArea ที่ target เปลี่ยนตาม HealTargetMode) ซึ่งอยู่บน instance ของ step ที่ฝังอยู่ใน
/// composite — ไม่ใช่บน <c>serializedObject.targetObject</c>.
/// </summary>
public static class SerializedPropertyOwnerUtility
{
    const BindingFlags Flags =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    /// <summary>คืน object ที่ประกาศ field ของ property นี้ (null เมื่อเดินไม่ถึง).</summary>
    public static object GetOwner(SerializedProperty property)
    {
        if (property == null)
            return null;

        object current = property.serializedObject?.targetObject;
        if (current == null)
            return null;

        string path = property.propertyPath.Replace(".Array.data[", "[");
        string[] tokens = path.Split('.');

        // ตัวสุดท้ายคือ field ของ property เอง — เจ้าของคือทุกอย่างก่อนหน้านั้น
        for (int i = 0; i < tokens.Length - 1 && current != null; i++)
            current = Step(current, tokens[i]);

        return current;
    }

    static object Step(object owner, string token)
    {
        int bracket = token.IndexOf('[');
        if (bracket < 0)
            return GetFieldValue(owner, token);

        string fieldName = token.Substring(0, bracket);
        string indexText = token.Substring(bracket + 1, token.Length - bracket - 2);
        if (!int.TryParse(indexText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index))
            return null;

        return GetElement(GetFieldValue(owner, fieldName), index);
    }

    static object GetFieldValue(object owner, string fieldName)
    {
        for (Type type = owner?.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo field = type.GetField(fieldName, Flags);
            if (field != null)
                return field.GetValue(owner);
        }

        return null;
    }

    static object GetElement(object collection, int index)
    {
        if (collection is not IEnumerable enumerable || index < 0)
            return null;

        IEnumerator enumerator = enumerable.GetEnumerator();
        for (int i = 0; i <= index; i++)
        {
            if (!enumerator.MoveNext())
                return null;
        }

        return enumerator.Current;
    }
}
#endif
