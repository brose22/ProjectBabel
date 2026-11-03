using UnityEditor;
using UnityEngine;

// Draws a template's stat range as min and max side by side on one line.
[CustomPropertyDrawer(typeof(StatRange))]
public class StatRangeDrawer : PropertyDrawer
{
    const float DashWidth = 12f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        Rect fields = label == GUIContent.none ? position : EditorGUI.PrefixLabel(position, label);
        int indent = EditorGUI.indentLevel;
        EditorGUI.indentLevel = 0;
        float width = (fields.width - DashWidth) / 2f;
        EditorGUI.PropertyField(new Rect(fields.x, fields.y, width, fields.height), property.FindPropertyRelative(nameof(StatRange.min)), GUIContent.none);
        EditorGUI.LabelField(new Rect(fields.x + width, fields.y, DashWidth, fields.height), "–", EditorStyles.centeredGreyMiniLabel);
        EditorGUI.PropertyField(new Rect(fields.x + width + DashWidth, fields.y, width, fields.height), property.FindPropertyRelative(nameof(StatRange.max)), GUIContent.none);
        EditorGUI.indentLevel = indent;
        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => EditorGUIUtility.singleLineHeight;
}
