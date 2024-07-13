using UnityEngine;
using UnityEditor;

/// <summary>
/// Custom Property Drawer for elemental floats. Only shows 'Min', 'Max', and 'Element' values if 'Enabled' is set to true.
/// 
/// Courtesy of ChatGPT 4o
/// </summary>
[CustomPropertyDrawer(typeof(ElementalFloat))]
public class ElementalFloatDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        // Calculate rects
        Rect nameRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        Rect enabledRect = new Rect(position.x, position.y + 18, position.width, EditorGUIUtility.singleLineHeight);
        Rect valueRect = new Rect(position.x, position.y + 36, position.width, EditorGUIUtility.singleLineHeight);
        Rect minRect = new Rect(position.x, position.y + 54, position.width, EditorGUIUtility.singleLineHeight);
        Rect maxRect = new Rect(position.x, position.y + 72, position.width, EditorGUIUtility.singleLineHeight);
        Rect elementRect = new Rect(position.x, position.y + 90, position.width, EditorGUIUtility.singleLineHeight);

        // Draw fields - pass GUIContent.none to each so they are drawn without labels
        SerializedProperty enabledProperty = property.FindPropertyRelative("Enabled");
        SerializedProperty valueProperty = property.FindPropertyRelative("Value");
        SerializedProperty minProperty = property.FindPropertyRelative("Min");
        SerializedProperty maxProperty = property.FindPropertyRelative("Max");
        SerializedProperty elementProperty = property.FindPropertyRelative("element");

        EditorGUI.SelectableLabel(nameRect, $"{property.name} (Elemental Float)", EditorStyles.boldLabel);
        EditorGUI.PropertyField(enabledRect, enabledProperty, new GUIContent("   Enabled"));
        EditorGUI.PropertyField(valueRect, valueProperty, new GUIContent("   Value"));

        if (enabledProperty.boolValue)
        {
            EditorGUI.PropertyField(minRect, minProperty, new GUIContent("   Min"));
            EditorGUI.PropertyField(maxRect, maxProperty, new GUIContent("   Max"));
            EditorGUI.PropertyField(elementRect, elementProperty, new GUIContent("   Element"));
        }

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        SerializedProperty enabledProperty = property.FindPropertyRelative("Enabled");
        if (enabledProperty.boolValue)
        {
            return EditorGUIUtility.singleLineHeight * 6 + 8;
        }
        else
        {
            return EditorGUIUtility.singleLineHeight * 3 + 4;
        }
    }
}