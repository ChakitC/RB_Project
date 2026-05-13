using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(ShopCatalogEntry))]
public class ShopCatalogEntryDrawer : PropertyDrawer
{
    const float VerticalSpacing = 3f;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float line = EditorGUIUtility.singleLineHeight;

        if (!property.isExpanded)
            return line;

        // Header + entry id + quantity + buy price + sell price + stock.
        return line * 6f + VerticalSpacing * 5f;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        float line = EditorGUIUtility.singleLineHeight;
        Rect row = new Rect(position.x, position.y, position.width, line);

        DrawHeader(row, property);

        if (property.isExpanded)
            DrawExpandedFields(position, property, line);

        EditorGUI.EndProperty();
    }

    void DrawHeader(Rect rect, SerializedProperty property)
    {
        SerializedProperty item = property.FindPropertyRelative("item");

        Rect foldoutRect = new Rect(rect.x, rect.y, 16f, rect.height);
        property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, GUIContent.none, true);

        Rect itemRect = new Rect(rect.x + 18f, rect.y, rect.width - 18f, rect.height);
        DrawObjectField(itemRect, item, "Item Config");
    }

    void DrawExpandedFields(Rect position, SerializedProperty property, float line)
    {
        int previousIndent = EditorGUI.indentLevel;
        EditorGUI.indentLevel++;

        float y = position.y + line + VerticalSpacing;
        DrawTextField(position, ref y, property.FindPropertyRelative("entryId"), "Entry Id");
        DrawIntField(position, ref y, property.FindPropertyRelative("quantity"), "Quantity", 1);
        DrawIntField(position, ref y, property.FindPropertyRelative("buyPrice"), "Buy Price", 0);
        DrawIntField(position, ref y, property.FindPropertyRelative("sellPrice"), "Sell Price", 0);
        DrawIntField(position, ref y, property.FindPropertyRelative("stock"), "Stock", -1);

        EditorGUI.indentLevel = previousIndent;
    }

    void DrawTextField(Rect position, ref float y, SerializedProperty property, string label)
    {
        float line = EditorGUIUtility.singleLineHeight;
        Rect rect = new Rect(position.x, y, position.width, line);
        property.stringValue = EditorGUI.TextField(rect, label, property.stringValue);
        y += line + VerticalSpacing;
    }

    void DrawIntField(Rect position, ref float y, SerializedProperty property, string label, int minValue)
    {
        float line = EditorGUIUtility.singleLineHeight;
        Rect rect = new Rect(position.x, y, position.width, line);
        property.intValue = Mathf.Max(minValue, EditorGUI.IntField(rect, label, property.intValue));
        y += line + VerticalSpacing;
    }

    void DrawObjectField(Rect rect, SerializedProperty property, string label)
    {
        property.objectReferenceValue = EditorGUI.ObjectField(
            rect,
            label,
            property.objectReferenceValue,
            typeof(ItemDefinition),
            false);
    }
}
