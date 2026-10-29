using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Edits captains' special abilities. Only the fields the chosen shape uses are shown; the others keep
// their values, so switching shape back and forth loses nothing.
public class AbilityEditorWindow : DataEditorWindow<AbilityData>
{
    const string Title = "Abilities";
    const float PreviewSize = 64f;

    List<CaptainData> captains;

    [MenuItem("Babel/Data/Abilities", priority = 20)]
    static void ShowWindow() => Open<AbilityEditorWindow>(Title);

    public static void OpenFor(AbilityData ability) => Open<AbilityEditorWindow>(Title, ability);

    protected override string DefaultFolder => "Assets/Data/Abilities";

    protected override string[] TableColumns => new[]
    {
        nameof(AbilityData.shape), nameof(AbilityData.chargeSeconds), nameof(AbilityData.rangeMultiplier),
        nameof(AbilityData.radius), nameof(AbilityData.damageMultiplier), nameof(AbilityData.knockbackDistance),
        nameof(AbilityData.zoneDuration), nameof(AbilityData.statusChance), nameof(AbilityData.healFraction),
        nameof(AbilityData.buffEffect), nameof(AbilityData.buffAmount), nameof(AbilityData.hitStopSeconds),
        nameof(AbilityData.cameraShake),
    };

    protected override string SearchText(AbilityData ability) => $"{ability.shape} {ability.buffEffect}";

    protected override void OnAssetsChanged() => captains = null;

    // The icon art, or a chip with the shape's initial for abilities without one.
    protected override void DrawIcon(Rect rect, AbilityData ability)
    {
        if (ability.icon != null)
            DataToolsGUI.DrawSprite(rect, ability.icon, Color.white);
        else
            DataToolsGUI.DrawChip(rect, ability.shape.ToString().Substring(0, 1), ShapeColor(ability.shape));
    }

    static Color ShapeColor(AbilityData.Shape shape) => shape switch
    {
        AbilityData.Shape.Area => new Color(1f, 0.6f, 0.3f),
        AbilityData.Shape.Single => new Color(1f, 0.85f, 0.4f),
        AbilityData.Shape.Line => new Color(0.6f, 0.8f, 1f),
        AbilityData.Shape.Zone => new Color(0.85f, 0.55f, 1f),
        _ => new Color(0.45f, 0.9f, 0.55f),
    };

    protected override void DrawEditor(SerializedObject so)
    {
        var ability = (AbilityData)so.targetObject;
        DataToolsGUI.Section("Identity");
        using (new EditorGUILayout.HorizontalScope())
        {
            DataToolsGUI.SpritePreview(ability.icon, ShapeColor(ability.shape), PreviewSize);
            using (new EditorGUILayout.VerticalScope())
            {
                DataToolsGUI.Field(so, nameof(AbilityData.displayName));
                DataToolsGUI.Field(so, nameof(AbilityData.description));
                DataToolsGUI.Field(so, nameof(AbilityData.icon));
            }
        }
        DrawFields(so);
        DrawUsers();
    }

    // Everything but the name and description, for this window and the one embedded in the captain window.
    public static void DrawFields(SerializedObject so)
    {
        var ability = (AbilityData)so.targetObject;
        DataToolsGUI.Field(so, nameof(AbilityData.chargeSeconds));

        SerializedProperty shapeProperty = DataToolsGUI.Field(so, nameof(AbilityData.shape));
        var shape = (AbilityData.Shape)shapeProperty.enumValueIndex;
        if (shape == AbilityData.Shape.Support)
            DrawSupportFields(so);
        else
            DrawAttackFields(so, shape);

        DataToolsGUI.Field(so, nameof(AbilityData.hitStopSeconds));
        DataToolsGUI.Field(so, nameof(AbilityData.cameraShake));
        DataToolsGUI.Field(so, nameof(AbilityData.cameraShakeSeconds));

        EditorGUILayout.Space(6f);
        EditorGUILayout.HelpBox(Summary(ability), MessageType.None);
        foreach (string problem in Problems(ability))
            EditorGUILayout.HelpBox(problem, MessageType.Warning);
    }

    static void DrawAttackFields(SerializedObject so, AbilityData.Shape shape)
    {
        DataToolsGUI.Field(so, nameof(AbilityData.rangeMultiplier), shape == AbilityData.Shape.Line ? "Range & Length Multiplier" : null);
        if (shape != AbilityData.Shape.Single)
            DataToolsGUI.Field(so, nameof(AbilityData.radius), shape == AbilityData.Shape.Line ? "Half Width" : "Radius");

        DataToolsGUI.Field(so, nameof(AbilityData.damageMultiplier), shape == AbilityData.Shape.Zone ? "Damage Per Second Multiplier" : null);
        // A zone only burns; it never pushes enemies.
        if (shape == AbilityData.Shape.Zone)
            DataToolsGUI.Field(so, nameof(AbilityData.zoneDuration));
        else
            DataToolsGUI.Field(so, nameof(AbilityData.knockbackDistance));
        DataToolsGUI.Field(so, nameof(AbilityData.statusChance));
    }

    static void DrawSupportFields(SerializedObject so)
    {
        DataToolsGUI.Field(so, nameof(AbilityData.healFraction));
        SerializedProperty effect = DataToolsGUI.Field(so, nameof(AbilityData.buffEffect));
        var buff = (SupportEffect)effect.enumValueIndex;
        using (new EditorGUI.DisabledScope(buff is SupportEffect.None or SupportEffect.Heal))
            DataToolsGUI.Field(so, nameof(AbilityData.buffAmount));
    }

    // A plain-language reading of the numbers, so a typo shows up as a sentence that looks wrong.
    static string Summary(AbilityData a)
    {
        string status = a.statusChance > 0f ? $", {a.statusChance:P0} chance of the captain's status" : "";
        string knock = a.knockbackDistance > 0f ? $", knocks back {a.knockbackDistance:0.##} tiles" : "";
        string damage = $"{a.damageMultiplier:0.##}× soldier power";
        string charge = $"Charges in {a.chargeSeconds:0.#}s. ";
        return charge + a.shape switch
        {
            AbilityData.Shape.Single => $"Hits the target for {damage}{knock}{status}.",
            AbilityData.Shape.Area => $"Hits every enemy within {a.radius:0.##} tiles of the target for {damage}{knock}{status}.",
            AbilityData.Shape.Line => $"Hits every enemy in a {a.radius * 2f:0.##}-tile-wide line out to {a.rangeMultiplier:0.##}× range for {damage}{knock}{status}.",
            AbilityData.Shape.Zone => $"Leaves a {a.radius:0.##}-tile zone for {a.zoneDuration:0.#}s dealing {damage} per second{status}.",
            _ => SupportSummary(a),
        };
    }

    static string SupportSummary(AbilityData a)
    {
        var parts = new List<string>();
        if (a.healFraction > 0f) parts.Add($"heals every tower and soldier {a.healFraction:P0}");
        if (a.HasBuff) parts.Add($"gives every tower +{a.buffAmount:P0} {ObjectNames.NicifyVariableName(a.buffEffect.ToString())} for the rest of the wave");
        return parts.Count == 0 ? "Does nothing yet." : char.ToUpper(parts[0][0]) + string.Join(" and ", parts).Substring(1) + ".";
    }

    static IEnumerable<string> Problems(AbilityData a)
    {
        if (a.chargeSeconds <= 0f) yield return "Charge seconds must be above 0.";
        if (a.shape == AbilityData.Shape.Support)
        {
            if (a.healFraction <= 0f && !a.HasBuff) yield return "This support special neither heals nor boosts.";
            if (a.buffEffect == SupportEffect.Heal) yield return "Heal is not a boost. Healing comes from Heal Fraction; set Buff Effect to None or a stat.";
            else if (a.buffEffect != SupportEffect.None && a.buffAmount <= 0f) yield return "Buff Amount is 0, so the boost does nothing.";
            else if (a.buffEffect == SupportEffect.None && a.buffAmount > 0f) yield return "Buff Amount is set but Buff Effect is None.";
            yield break;
        }
        if (a.shape != AbilityData.Shape.Single && a.radius <= 0f) yield return "Radius is 0, so nothing is hit.";
        if (a.damageMultiplier <= 0f && a.statusChance <= 0f) yield return "No damage and no status chance.";
        if (a.shape == AbilityData.Shape.Zone && a.zoneDuration <= 0f) yield return "Zone duration is 0.";
    }

    // The captains that fire this ability, as buttons that open them.
    void DrawUsers()
    {
        captains ??= DataToolsGUI.LoadAll<CaptainData>();
        DataToolsGUI.Section("Used by");
        DataToolsGUI.AssetButtons("Captains", captains.Where(c => c != null && c.ability == Selected), CaptainEditorWindow.OpenFor, "Nobody");
    }
}
