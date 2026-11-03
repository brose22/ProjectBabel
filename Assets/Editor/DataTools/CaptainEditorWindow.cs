using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Edits captains: a sprite preview beside the identity fields, stats, likes and dislikes as tag
// pickers, and the captain's ability editable in place.
public class CaptainEditorWindow : DataEditorWindow<CaptainData>
{
    const string Title = "Captains";

    protected override string WindowTitle => Title;
    const float PreviewSize = 96f;

    [SerializeField] bool abilityExpanded = true;
    SerializedObject abilityObject;
    List<CaptainData> captains;

    [MenuItem("Babel/Data/Captains", priority = 21)]
    static void ShowWindow() => Open<CaptainEditorWindow>(Title);

    public static void OpenFor(CaptainData captain) => Open<CaptainEditorWindow>(Title, captain);

    protected override string DefaultFolder => "Assets/Data/Captains";

    protected override string[] TableColumns => new[]
    {
        nameof(CaptainData.element), nameof(CaptainData.role), nameof(CaptainData.color), nameof(CaptainData.power),
        nameof(CaptainData.range), nameof(CaptainData.speed), nameof(CaptainData.health), nameof(CaptainData.defense),
        nameof(CaptainData.resourceCapacity), nameof(CaptainData.resourceRegen), nameof(CaptainData.specialChargeRate),
        nameof(CaptainData.level), nameof(CaptainData.refusalTemper), nameof(CaptainData.ability),
    };

    protected override string SearchText(CaptainData captain) =>
        $"{captain.element} {captain.role} {captain.abilityName} {(captain.ability != null ? captain.ability.name : "")}";

    protected override void OnAssetsChanged() => captains = null;

    protected override void OnCreated(CaptainData captain)
    {
        captain.likes = new CaptainTag[0];
        captain.dislikes = new CaptainTag[0];
    }

    // Sprites show untinted inside a border in the captain colour; art-less captains are a square in it.
    protected override void DrawIcon(Rect rect, CaptainData captain)
    {
        DataToolsGUI.Frame(rect, captain.color, 1f);
        DataToolsGUI.DrawSprite(new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, rect.height - 4f), captain.sprite, Fill(captain));
    }

    static Color Fill(CaptainData captain) => captain.sprite == null ? captain.color : Color.white;

    protected override void DrawEditor(SerializedObject so)
    {
        var captain = (CaptainData)so.targetObject;
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(PreviewSize)))
            {
                DataToolsGUI.SpritePreview(captain.sprite, Fill(captain), PreviewSize, border: captain.color);
                DataToolsGUI.ChipLayout(captain.element.ToString(), DataToolsGUI.ElementColor(captain.element), PreviewSize);
                DataToolsGUI.SpritePreview(captain.portrait, Color.white, PreviewSize, border: captain.color);
                GUILayout.Label("portrait", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(PreviewSize));
            }
            using (new EditorGUILayout.VerticalScope())
            {
                DataToolsGUI.Field(so, nameof(CaptainData.displayName));
                DataToolsGUI.Field(so, nameof(CaptainData.element));
                DataToolsGUI.Field(so, nameof(CaptainData.role));
                DataToolsGUI.Field(so, nameof(CaptainData.color));
                DataToolsGUI.Field(so, nameof(CaptainData.sprite));
                DataToolsGUI.Field(so, nameof(CaptainData.tintSprite));
                DataToolsGUI.Field(so, nameof(CaptainData.portrait));
            }
        }

        DataToolsGUI.Field(so, nameof(CaptainData.power));
        DataToolsGUI.Field(so, nameof(CaptainData.range), "Range Bonus");
        DataToolsGUI.Field(so, nameof(CaptainData.speed));
        DataToolsGUI.Field(so, nameof(CaptainData.health));
        DataToolsGUI.Field(so, nameof(CaptainData.defense));
        DataToolsGUI.Field(so, nameof(CaptainData.defensePerLevel));
        DataToolsGUI.Field(so, nameof(CaptainData.resourceCapacity));
        DataToolsGUI.Field(so, nameof(CaptainData.resourceRegen));
        DataToolsGUI.Field(so, nameof(CaptainData.specialChargeRate));
        DataToolsGUI.Field(so, nameof(CaptainData.level));
        DataToolsGUI.Note($"{captain.power * captain.speed:0.#} damage per second per soldier, before class and level scaling");

        DrawAbility(so, captain);
        DrawRelationship(so);
    }

    void DrawAbility(SerializedObject so, CaptainData captain)
    {
        DataToolsGUI.Section("Special Ability");
        using (new EditorGUILayout.HorizontalScope())
        {
            DataToolsGUI.Field(so, nameof(CaptainData.ability));
            if (captain.ability == null)
            {
                if (GUILayout.Button("New", GUILayout.Width(50f))) CreateAbility(so, captain);
            }
            else if (GUILayout.Button("Open", GUILayout.Width(50f)))
                AbilityEditorWindow.OpenFor(captain.ability);
        }

        // The captain carries its own copy of the ability's name and description for the UI.
        DataToolsGUI.Field(so, nameof(CaptainData.abilityName));
        DataToolsGUI.Field(so, nameof(CaptainData.abilityDescription));
        AbilityData ability = captain.ability;
        if (ability != null && (captain.abilityName != ability.displayName || captain.abilityDescription != ability.description))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.HelpBox("The name or description differs from the ability's own.", MessageType.Info);
                if (GUILayout.Button("Copy from\nability", GUILayout.Width(80f), GUILayout.Height(38f)))
                {
                    so.FindProperty(nameof(CaptainData.abilityName)).stringValue = ability.displayName;
                    so.FindProperty(nameof(CaptainData.abilityDescription)).stringValue = ability.description;
                }
            }
        }
        string mismatch = RoleMismatch(captain);
        if (mismatch != null) EditorGUILayout.HelpBox(mismatch, MessageType.Warning);
        if (ability == null) return;

        if (abilityObject == null || abilityObject.targetObject != ability)
        {
            abilityObject?.Dispose();
            abilityObject = new SerializedObject(ability);
        }
        abilityExpanded = EditorGUILayout.Foldout(abilityExpanded, $"{ability.name} settings", true);
        if (!abilityExpanded) return;
        captains = DataToolsGUI.LoadAll(captains);
        int users = captains.Count(c => c != null && c.ability == ability);
        if (users > 1)
            EditorGUILayout.HelpBox($"{users} captains share this ability; changes here affect all of them.", MessageType.Info);
        using (new EditorGUI.IndentLevelScope())
        {
            abilityObject.Update();
            DataToolsGUI.Field(abilityObject, nameof(AbilityData.displayName));
            DataToolsGUI.Field(abilityObject, nameof(AbilityData.description));
            DataToolsGUI.Field(abilityObject, nameof(AbilityData.icon));
            AbilityEditorWindow.DrawFields(abilityObject);
            abilityObject.ApplyModifiedProperties();
        }
    }

    // A support role whose special doesn't support, or the other way round.
    static string RoleMismatch(CaptainData captain)
    {
        AbilityData ability = captain.ability;
        if (ability == null) return "Without an ability the tower has no special.";
        bool support = ability.shape == AbilityData.Shape.Support;
        return captain.role switch
        {
            CaptainRole.Attacker when support => "An Attacker with a Support-shape special.",
            CaptainRole.Healer when !support || ability.healFraction <= 0f => "A Healer whose special doesn't heal.",
            CaptainRole.Buffer when !support || !ability.HasBuff => "A Buffer whose special gives no boost.",
            _ => null,
        };
    }

    void CreateAbility(SerializedObject so, CaptainData captain)
    {
        string folder = AssetDatabase.IsValidFolder("Assets/Data/Abilities") ? "Assets/Data/Abilities" : "Assets";
        string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{captain.name}Special.asset");
        var ability = CreateInstance<AbilityData>();
        ability.displayName = string.IsNullOrEmpty(captain.abilityName) ? $"{captain.displayName}'s Special" : captain.abilityName;
        ability.description = captain.abilityDescription;
        if (captain.role != CaptainRole.Attacker)
        {
            ability.shape = AbilityData.Shape.Support;
            ability.statusChance = 0f;
        }
        AssetDatabase.CreateAsset(ability, path);
        AssetDatabase.SaveAssets();
        so.FindProperty(nameof(CaptainData.ability)).objectReferenceValue = ability;
        so.ApplyModifiedProperties();
        // The embedded ability editor appears, so end this pass before the layout disagrees.
        GUIUtility.ExitGUI();
    }

    void DrawRelationship(SerializedObject so)
    {
        DataToolsGUI.Field(so, nameof(CaptainData.refusalTemper));
        SerializedProperty likes = so.FindProperty(nameof(CaptainData.likes));
        SerializedProperty dislikes = so.FindProperty(nameof(CaptainData.dislikes));
        // A tag can't be both liked and disliked, so each picker greys out the other's tags.
        EditorGUILayout.LabelField("Likes", EditorStyles.miniBoldLabel);
        DataToolsGUI.EnumSetPicker(likes, Tags(dislikes));
        EditorGUILayout.LabelField("Dislikes", EditorStyles.miniBoldLabel);
        DataToolsGUI.EnumSetPicker(dislikes, Tags(likes));

        DataToolsGUI.Section("Gifts");
        DataToolsGUI.Field(so, nameof(CaptainData.likedGifts));
        DataToolsGUI.Field(so, nameof(CaptainData.dislikedGifts));
        DataToolsGUI.Field(so, nameof(CaptainData.giftGreeting));
        DataToolsGUI.Field(so, nameof(CaptainData.giftLikedLine));
        DataToolsGUI.Field(so, nameof(CaptainData.giftNeutralLine));
        DataToolsGUI.Field(so, nameof(CaptainData.giftDislikedLine));
    }

    static HashSet<CaptainTag> Tags(SerializedProperty array)
    {
        var tags = new HashSet<CaptainTag>();
        for (int i = 0; i < array.arraySize; i++)
            tags.Add((CaptainTag)array.GetArrayElementAtIndex(i).intValue);
        return tags;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        abilityObject?.Dispose();
        abilityObject = null;
    }
}
