using System.Linq;
using UnityEditor;
using UnityEngine;

// Edits captain templates: the role, the pools a captain is rolled from, the stat ranges, and a
// few sample rolls to see what the template makes.
public class CaptainEditorWindow : DataEditorWindow<CaptainData>
{
    const string Title = "Captain Templates";
    const int SampleCount = 4;

    protected override string WindowTitle => Title;
    const float PreviewSize = 96f;

    [SerializeField] int sampleSeed = 1;

    [MenuItem("Babel/Data/Captain Templates", priority = 21)]
    static void ShowWindow() => Open<CaptainEditorWindow>(Title);

    public static void OpenFor(CaptainData template) => Open<CaptainEditorWindow>(Title, template);

    protected override string DefaultFolder => "Assets/Data/Captains";

    protected override string[] TableColumns => new[]
    {
        nameof(CaptainData.role), nameof(CaptainData.specialClassChance), nameof(CaptainData.power), nameof(CaptainData.speed),
        nameof(CaptainData.health), nameof(CaptainData.defense), nameof(CaptainData.defensePerLevel), nameof(CaptainData.refusalTemper),
        nameof(CaptainData.resourceCapacity), nameof(CaptainData.resourceRegen), nameof(CaptainData.specialChargeRate),
    };

    protected override string SearchText(CaptainData template) =>
        $"{template.role} {string.Join(" ", template.names)} {string.Join(" ", template.abilities.Where(a => a != null).Select(a => a.name))}";

    // The tower sprite, tinted the way a Water captain's would be.
    protected override void DrawIcon(Rect rect, CaptainData template) =>
        DataToolsGUI.DrawSprite(rect, template.sprite, Tint(template, Element.Water));

    static Color Tint(CaptainData template, Element element) =>
        template.sprite == null || template.tintSprite ? Captain.TowerColor(element) : Color.white;

    protected override void DrawEditor(SerializedObject so)
    {
        var template = (CaptainData)so.targetObject;
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(PreviewSize)))
            {
                DataToolsGUI.SpritePreview(template.sprite, Tint(template, Element.Water), PreviewSize);
                DataToolsGUI.ChipLayout(template.role.ToString(), Color.gray, PreviewSize);
            }
            using (new EditorGUILayout.VerticalScope())
            {
                DataToolsGUI.Field(so, nameof(CaptainData.role));
                DataToolsGUI.Field(so, nameof(CaptainData.specialClassChance));
                DataToolsGUI.Note($"Usually {CaptainRoller.UsualDamageType(template.role)}; " +
                    $"{template.specialClassChance:0%} of captains roll one of the other damage types.");
                DataToolsGUI.Field(so, nameof(CaptainData.sprite));
                DataToolsGUI.Field(so, nameof(CaptainData.tintSprite));
            }
        }

        DataToolsGUI.Section("Stat Ranges");
        DataToolsGUI.Field(so, nameof(CaptainData.power));
        DataToolsGUI.Field(so, nameof(CaptainData.range), "Range Bonus");
        DataToolsGUI.Field(so, nameof(CaptainData.speed));
        DataToolsGUI.Field(so, nameof(CaptainData.health));
        DataToolsGUI.Field(so, nameof(CaptainData.defense));
        DataToolsGUI.Field(so, nameof(CaptainData.defensePerLevel));
        DataToolsGUI.Note($"{template.power.min * template.speed.min:0.#} to {template.power.max * template.speed.max:0.#} damage per second per soldier, before class and level scaling");
        DataToolsGUI.Field(so, nameof(CaptainData.resourceCapacity));
        DataToolsGUI.Field(so, nameof(CaptainData.resourceRegen));
        DataToolsGUI.Field(so, nameof(CaptainData.specialChargeRate));

        DataToolsGUI.Section("Pools");
        DataToolsGUI.Field(so, nameof(CaptainData.names));
        DataToolsGUI.Field(so, nameof(CaptainData.portraits));
        DataToolsGUI.Field(so, nameof(CaptainData.abilities));
        string mismatch = RoleMismatch(template);
        if (mismatch != null) EditorGUILayout.HelpBox(mismatch, MessageType.Warning);
        DataToolsGUI.AssetButtons("Open", template.abilities.Where(a => a != null), AbilityEditorWindow.OpenFor);

        DrawRelationship(so);
        DrawSamples(template);
    }

    // A Healer that can roll a special that doesn't heal, or a template with no special at all.
    static string RoleMismatch(CaptainData template)
    {
        if (template.abilities.All(a => a == null)) return "Without an ability the tower has no special.";
        if (template.role == CaptainRole.Healer && template.abilities.Any(a => a != null && a.healFraction <= 0f))
            return "A Healer can roll a special that doesn't heal.";
        return null;
    }

    void DrawRelationship(SerializedObject so)
    {
        DataToolsGUI.Section("Relationship");
        DataToolsGUI.Field(so, nameof(CaptainData.refusalTemper));
        EditorGUILayout.LabelField("Like pool (3 are rolled)", EditorStyles.miniBoldLabel);
        DataToolsGUI.EnumSetPicker<CaptainTag>(so.FindProperty(nameof(CaptainData.likePool)));
        EditorGUILayout.LabelField("Dislike pool (3 are rolled, never one already liked)", EditorStyles.miniBoldLabel);
        DataToolsGUI.EnumSetPicker<CaptainTag>(so.FindProperty(nameof(CaptainData.dislikePool)));

        DataToolsGUI.Section("Gifts");
        DataToolsGUI.Field(so, nameof(CaptainData.likedGiftPool));
        DataToolsGUI.Field(so, nameof(CaptainData.dislikedGiftPool));
        DataToolsGUI.Field(so, nameof(CaptainData.giftGreetings));
        DataToolsGUI.Field(so, nameof(CaptainData.giftLikedLines));
        DataToolsGUI.Field(so, nameof(CaptainData.giftNeutralLines));
        DataToolsGUI.Field(so, nameof(CaptainData.giftDislikedLines));
    }

    // A few captains rolled from the template as it stands, to check the pools and ranges.
    void DrawSamples(CaptainData template)
    {
        DataToolsGUI.Section("Sample Rolls");
        using (new EditorGUILayout.HorizontalScope())
        {
            sampleSeed = EditorGUILayout.IntField("Seed", sampleSeed);
            if (GUILayout.Button("Reroll", GUILayout.Width(70f))) sampleSeed = Random.Range(1, 100000);
        }
        for (int i = 0; i < SampleCount; i++)
        {
            Captain captain = template.Roll(sampleSeed * 31 + i);
            string ability = captain.Ability != null ? captain.Ability.displayName : "no special";
            DataToolsGUI.Note($"{captain.displayName}, {captain.element} {captain.ClassName}: power {captain.power:0.##}, speed {captain.speed:0.##}, " +
                $"health {captain.health}, defense {captain.defense:0.##} (+{captain.defensePerLevel:0.##}/level), {ability}. " +
                $"Likes {string.Join(", ", captain.likes)}; dislikes {string.Join(", ", captain.dislikes)}.");
        }
    }
}
