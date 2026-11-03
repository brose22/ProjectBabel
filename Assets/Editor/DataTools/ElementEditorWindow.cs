using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Edits how each element is presented: icon preview, colour checked against the status tint the
// game draws, the status rules it maps to, and the captains and enemies tied to the element.
public class ElementEditorWindow : DataEditorWindow<ElementData>
{
    const string Title = "Elements";

    protected override string WindowTitle => Title;
    const float PreviewSize = 64f;

    List<ElementData> elements;
    List<CaptainData> captains;
    List<EnemyData> enemies;

    [MenuItem("Babel/Data/Elements", priority = 24)]
    static void ShowWindow() => Open<ElementEditorWindow>(Title);

    public static void OpenFor(ElementData element) => Open<ElementEditorWindow>(Title, element);

    protected override string DefaultFolder => "Assets/Data/Elements";

    protected override string[] TableColumns => new[]
    {
        nameof(ElementData.element), nameof(ElementData.color), nameof(ElementData.icon), nameof(ElementData.statusName),
    };

    protected override string SearchText(ElementData element) => $"{element.element} {element.statusName}";

    protected override void OnAssetsChanged()
    {
        elements = null;
        captains = null;
        enemies = null;
    }

    protected override void DrawIcon(Rect rect, ElementData element)
    {
        DataToolsGUI.Frame(rect, element.color, 1f);
        DataToolsGUI.DrawSprite(new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, rect.height - 4f), element.icon,
            element.icon == null ? element.color : Color.white);
    }

    protected override void DrawEditor(SerializedObject so)
    {
        var element = (ElementData)so.targetObject;
        using (new EditorGUILayout.HorizontalScope())
        {
            DataToolsGUI.SpritePreview(element.icon, element.icon == null ? element.color : Color.white, PreviewSize, border: element.color);
            using (new EditorGUILayout.VerticalScope())
            {
                DataToolsGUI.Field(so, nameof(ElementData.element));
                DataToolsGUI.Field(so, nameof(ElementData.displayName));
                DataToolsGUI.Field(so, nameof(ElementData.color));
                DataToolsGUI.Field(so, nameof(ElementData.icon));
            }
        }
        DrawTintCheck(so, element);
        DrawDuplicateCheck(element);

        DataToolsGUI.Field(so, nameof(ElementData.statusName));
        DataToolsGUI.Field(so, nameof(ElementData.description));
        DrawStatusRules(so, element);
        DrawLinks(element);
    }

    // The tooltip promises the UI colour matches the status tint, so flag drift and offer to fix it.
    static void DrawTintCheck(SerializedObject so, ElementData element)
    {
        Color tint = DataToolsGUI.ElementColor(element.element);
        if (Approximately(element.color, tint)) return;
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.HelpBox($"The colour differs from the {element.element} status tint the game draws.", MessageType.Info);
            Rect swatch = GUILayoutUtility.GetRect(38f, 38f, GUILayout.Width(38f), GUILayout.Height(38f));
            DataToolsGUI.Swatch(swatch, tint);
            if (GUILayout.Button("Use status\ntint", GUILayout.Width(80f), GUILayout.Height(38f)))
                so.FindProperty(nameof(ElementData.color)).colorValue = tint;
        }
    }

    static bool Approximately(Color a, Color b) =>
        Mathf.Abs(a.r - b.r) < 0.01f && Mathf.Abs(a.g - b.g) < 0.01f && Mathf.Abs(a.b - b.b) < 0.01f;

    void DrawDuplicateCheck(ElementData element)
    {
        elements = DataToolsGUI.LoadAll(elements);
        List<string> others = elements.Where(e => e != null && e != element && e.element == element.element).Select(e => e.name).ToList();
        if (others.Count > 0)
            EditorGUILayout.HelpBox($"{string.Join(", ", others)} also describes {element.element}; the UI may pick either.", MessageType.Warning);
    }

    // What the game actually does for this element, read from StatusRules so it can't drift.
    static void DrawStatusRules(SerializedObject so, ElementData element)
    {
        StatusKind kind = StatusRules.ForElement(element.element);
        string effect = kind switch
        {
            StatusKind.Slow => $"moves at {StatusRules.SlowSpeedMultiplier:0.##}× speed",
            StatusKind.Burn => $"takes {StatusRules.BurnPowerPerSecond:0.##}× the caster's power per second",
            StatusKind.Stun => $"attacks {StatusRules.StunAttackIntervalMultiplier:0.##}× slower",
            _ => "stands still and holds its attacks",
        };
        DataToolsGUI.Note($"In game: {kind} for {StatusRules.DurationOf(kind):0.#}s, the enemy {effect}");
        if (element.statusName != kind.ToString())
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.HelpBox($"The status name differs from the game's {kind} status.", MessageType.Info);
                if (GUILayout.Button($"Use \"{kind}\"", GUILayout.Width(80f), GUILayout.Height(38f)))
                    so.FindProperty(nameof(ElementData.statusName)).stringValue = kind.ToString();
            }
        }
    }

    void DrawLinks(ElementData element)
    {
        DataToolsGUI.Section("Used by");
        captains = DataToolsGUI.LoadAll(captains);
        DataToolsGUI.AssetButtons("Portraits in", captains.Where(c => c != null && c.portraits.Any(p => p.element == element.element)), CaptainEditorWindow.OpenFor);
    }
}
