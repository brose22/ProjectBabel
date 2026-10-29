using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Drawing and lookup helpers shared by the data tool windows.
public static class DataToolsGUI
{
    public static readonly Color SelectionColor = new(0.24f, 0.48f, 0.9f, 0.45f);
    public static readonly Color StripeColor = new(0f, 0f, 0f, 0.08f);
    public static readonly Color LineColor = new(0f, 0f, 0f, 0.3f);
    static readonly Color PreviewBackground = new(0.16f, 0.2f, 0.16f);

    static GUIStyle richLabel, headerLabel, chip;

    public static GUIStyle RichLabel => richLabel ??= new GUIStyle(EditorStyles.label) { richText = true, clipping = TextClipping.Clip };
    public static GUIStyle HeaderLabel => headerLabel ??= new GUIStyle(EditorStyles.miniBoldLabel) { wordWrap = true, alignment = TextAnchor.LowerLeft, clipping = TextClipping.Clip };
    static GUIStyle Chip => chip ??= new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.black } };

    // A group title for fields without one. Fields carrying a [Header] draw their own title, so
    // windows only call this where the data class has none.
    public static void Section(string title)
    {
        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
    }

    // A serialized field by name, optionally with a different label. Missing fields show an error
    // instead of throwing, so a renamed field shows up plainly in the window.
    public static SerializedProperty Field(SerializedObject so, string name, string label = null)
    {
        SerializedProperty property = so.FindProperty(name);
        if (property == null)
        {
            EditorGUILayout.HelpBox($"No serialized field \"{name}\".", MessageType.Error);
            return null;
        }
        if (label == null)
            EditorGUILayout.PropertyField(property, true);
        else
            EditorGUILayout.PropertyField(property, new GUIContent(label, property.tooltip), true);
        return property;
    }

    public static Color ElementColor(Element element) => StatusRules.TintOf(StatusRules.ForElement(element));

    // A coloured tag with a label, such as an element name.
    public static void DrawChip(Rect rect, string text, Color color)
    {
        EditorGUI.DrawRect(rect, color);
        GUI.Label(rect, text, Chip);
    }

    public static void ChipLayout(string text, Color color, float width = 60f) =>
        DrawChip(GUILayoutUtility.GetRect(width, 16f, GUILayout.Width(width)), text, color);

    // A small square in a colour, for list rows without art.
    public static void Swatch(Rect rect, Color color)
    {
        EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.5f));
        EditorGUI.DrawRect(new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, rect.height - 2f), color);
    }

    // Draws a sprite fitted inside the rect, keeping its aspect. Without one it falls back to a square in
    // the tint colour, the way the game draws art-less towers and enemies. Scale shrinks the drawn size.
    public static void DrawSprite(Rect rect, Sprite sprite, Color tint, float scale = 1f)
    {
        Vector2 center = rect.center;
        if (sprite == null || sprite.texture == null)
        {
            float side = Mathf.Min(rect.width, rect.height) * 0.6f * scale;
            Swatch(new Rect(center.x - side / 2f, center.y - side / 2f, side, side), tint);
            return;
        }
        // With a sprite atlas enabled, sprite.texture is the packed atlas but sprite.rect is still in
        // the source image, so draw from the source image.
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GetAssetPath(sprite));
        if (texture == null) texture = sprite.texture;
        Rect source = sprite.rect;
        float fit = Mathf.Min(rect.width / source.width, rect.height / source.height) * scale;
        Vector2 size = new(source.width * fit, source.height * fit);
        Rect target = new(center.x - size.x / 2f, center.y - size.y / 2f, size.x, size.y);
        Rect uv = new(source.x / texture.width, source.y / texture.height, source.width / texture.width, source.height / texture.height);
        Color old = GUI.color;
        GUI.color = tint;
        GUI.DrawTextureWithTexCoords(target, texture, uv);
        GUI.color = old;
    }

    // An outline of the given width just inside the rect.
    public static void Frame(Rect rect, Color color, float width)
    {
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, width), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - width, rect.width, width), color);
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, width, rect.height), color);
        EditorGUI.DrawRect(new Rect(rect.xMax - width, rect.y, width, rect.height), color);
    }

    // A sprite preview laid out at the given size, optionally framed in a border colour.
    public static Rect SpritePreview(Sprite sprite, Color tint, float size, float scale = 1f, Color? border = null)
    {
        Rect rect = GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size));
        EditorGUI.DrawRect(rect, PreviewBackground);
        if (border is Color color) Frame(rect, color, 3f);
        DrawSprite(new Rect(rect.x + 4f, rect.y + 4f, rect.width - 8f, rect.height - 8f), sprite, tint, scale);
        return rect;
    }

    // Toggle buttons for each value of an enum, editing an array property as a set: pressing a value
    // adds it or removes it. Values in the disabled set are drawn greyed out (but can still be removed).
    public static void EnumSetPicker<TEnum>(SerializedProperty array, ICollection<TEnum> disabled = null, int perRow = 5) where TEnum : struct, Enum
    {
        TEnum[] values = (TEnum[])Enum.GetValues(typeof(TEnum));
        var chosen = new List<int>();
        for (int i = 0; i < array.arraySize; i++)
        {
            int value = array.GetArrayElementAtIndex(i).intValue;
            if (!chosen.Contains(value)) chosen.Add(value);
        }

        bool changed = chosen.Count != array.arraySize;
        for (int start = 0; start < values.Length; start += perRow)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                for (int i = start; i < Mathf.Min(start + perRow, values.Length); i++)
                {
                    int value = Convert.ToInt32(values[i]);
                    bool on = chosen.Contains(value);
                    using (new EditorGUI.DisabledScope(!on && disabled != null && disabled.Contains(values[i])))
                    {
                        bool now = GUILayout.Toggle(on, values[i].ToString(), EditorStyles.miniButton);
                        if (now == on) continue;
                        if (now) chosen.Add(value); else chosen.Remove(value);
                        changed = true;
                    }
                }
            }
        }
        if (!changed) return;
        array.arraySize = chosen.Count;
        for (int i = 0; i < chosen.Count; i++)
            array.GetArrayElementAtIndex(i).intValue = chosen[i];
    }

    // A derived value or explanation under a field, lined up with the values and wrapped to the width.
    public static void Note(string text)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Space(EditorGUIUtility.labelWidth);
            GUILayout.Label(text, EditorStyles.wordWrappedMiniLabel);
        }
    }

    const float ButtonHeight = 18f;
    const float ButtonGap = 3f;
    // The width each button row had on its last repaint, by label. The row's height has to be known
    // before its width is, so it wraps against last frame's width.
    static readonly Dictionary<string, float> buttonRowWidths = new();

    // A labelled set of buttons, one per asset, that each open their asset. Wraps onto more lines
    // when the window is too narrow for one.
    public static void AssetButtons<TAsset>(string label, IEnumerable<TAsset> assets, Action<TAsset> open, string empty = "None")
        where TAsset : UnityEngine.Object
    {
        List<TAsset> list = assets.Where(a => a != null).ToList();
        if (list.Count == 0)
        {
            EditorGUILayout.LabelField(label, empty, EditorStyles.miniLabel);
            return;
        }

        float[] widths = list.Select(a => EditorStyles.miniButton.CalcSize(new GUIContent(a.name)).x).ToArray();
        float available = buttonRowWidths.TryGetValue(label, out float known) ? known : 250f;
        int rows = WrapRows(widths, available, null);
        Rect rect = EditorGUILayout.GetControlRect(true, rows * ButtonHeight + (rows - 1) * ButtonGap);
        Rect area = EditorGUI.PrefixLabel(rect, new GUIContent(label));
        if (Event.current.type == EventType.Repaint && !Mathf.Approximately(area.width, available))
        {
            buttonRowWidths[label] = area.width;
            // The next frame lays out with the right width.
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        var places = new Rect[list.Count];
        WrapRows(widths, area.width, places);
        for (int i = 0; i < list.Count; i++)
        {
            Rect place = places[i];
            place.position += area.position;
            if (GUI.Button(place, list[i].name, EditorStyles.miniButton))
                open(list[i]);
        }
    }

    // Lays buttons out left to right, wrapping at the width. Fills places (relative to the area's
    // corner) when given, and returns the number of rows.
    static int WrapRows(float[] widths, float width, Rect[] places)
    {
        int row = 0;
        float x = 0f;
        for (int i = 0; i < widths.Length; i++)
        {
            if (x > 0f && x + widths[i] > width)
            {
                row++;
                x = 0f;
            }
            if (places != null) places[i] = new Rect(x, row * (ButtonHeight + ButtonGap), widths[i], ButtonHeight);
            x += widths[i] + ButtonGap;
        }
        return row + 1;
    }

    public static List<TAsset> LoadAll<TAsset>() where TAsset : UnityEngine.Object
    {
        var found = new List<TAsset>();
        foreach (string guid in AssetDatabase.FindAssets($"t:{typeof(TAsset).Name}", new[] { "Assets" }))
        {
            var asset = AssetDatabase.LoadAssetAtPath<TAsset>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null) found.Add(asset);
        }
        found.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
        return found;
    }

    // Every data asset under Assets that references the target directly.
    public static List<UnityEngine.Object> FindUsers(UnityEngine.Object target)
    {
        string targetPath = AssetDatabase.GetAssetPath(target);
        var users = new List<UnityEngine.Object>();
        foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path == targetPath) continue;
            if (Array.IndexOf(AssetDatabase.GetDependencies(path, false), targetPath) >= 0)
                users.Add(AssetDatabase.LoadMainAssetAtPath(path));
        }
        return users;
    }

    // A value to sort table rows by for the given field.
    public static IComparable SortKey(SerializedProperty property) => property?.propertyType switch
    {
        SerializedPropertyType.Float => property.floatValue,
        SerializedPropertyType.Integer => (float)property.intValue,
        SerializedPropertyType.Boolean => property.boolValue ? 1f : 0f,
        SerializedPropertyType.Enum => (float)property.enumValueIndex,
        SerializedPropertyType.String => property.stringValue,
        SerializedPropertyType.ObjectReference => property.objectReferenceValue != null ? property.objectReferenceValue.name : "",
        _ => "",
    };
}
