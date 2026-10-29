using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Edits soldier classes: sprite and portrait previews, only the style fields the attack style uses,
// hit damage under a chosen captain, and which captains like or dislike the class.
public class SoldierEditorWindow : DataEditorWindow<SoldierClassData>
{
    const string Title = "Soldiers";
    const float PreviewSize = 96f;

    [SerializeField] int previewCaptain;
    List<CaptainData> captains;

    [MenuItem("Babel/Data/Soldiers", priority = 23)]
    static void ShowWindow() => Open<SoldierEditorWindow>(Title);

    public static void OpenFor(SoldierClassData soldierClass) => Open<SoldierEditorWindow>(Title, soldierClass);

    protected override string DefaultFolder => "Assets/Data/Soldiers";

    protected override string[] TableColumns => new[]
    {
        nameof(SoldierClassData.attackStyle), nameof(SoldierClassData.actCost), nameof(SoldierClassData.respawnCost),
        nameof(SoldierClassData.maxHealth), nameof(SoldierClassData.damageMultiplier), nameof(SoldierClassData.attackRange),
        nameof(SoldierClassData.moveSpeed), nameof(SoldierClassData.knockbackDistance),
        nameof(SoldierClassData.projectileSpeed), nameof(SoldierClassData.splashRadius), nameof(SoldierClassData.color),
    };

    protected override string SearchText(SoldierClassData soldierClass) => soldierClass.attackStyle.ToString();

    protected override void OnAssetsChanged() => captains = null;

    protected override void DrawIcon(Rect rect, SoldierClassData soldierClass)
    {
        DataToolsGUI.Frame(rect, soldierClass.color, 1f);
        DataToolsGUI.DrawSprite(new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, rect.height - 4f), soldierClass.sprite, Fill(soldierClass));
    }

    // Like the game: drawn art shows as painted, art-less soldiers are a square in their colour.
    static Color Fill(SoldierClassData soldierClass) => soldierClass.sprite == null ? soldierClass.color : Color.white;

    protected override void DrawEditor(SerializedObject so)
    {
        var soldierClass = (SoldierClassData)so.targetObject;
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(PreviewSize)))
            {
                DataToolsGUI.SpritePreview(soldierClass.sprite, Fill(soldierClass), PreviewSize, border: soldierClass.color);
                DataToolsGUI.SpritePreview(soldierClass.portrait, Color.white, PreviewSize, border: soldierClass.color);
                GUILayout.Label("portrait", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(PreviewSize));
            }
            using (new EditorGUILayout.VerticalScope())
            {
                DataToolsGUI.Section("Identity");
                DataToolsGUI.Field(so, nameof(SoldierClassData.displayName));
                DataToolsGUI.Field(so, nameof(SoldierClassData.color));
                DataToolsGUI.Field(so, nameof(SoldierClassData.sprite));
                DataToolsGUI.Field(so, nameof(SoldierClassData.portrait));
            }
        }

        DataToolsGUI.Field(so, nameof(SoldierClassData.actCost));
        DataToolsGUI.Field(so, nameof(SoldierClassData.respawnCost));

        DataToolsGUI.Field(so, nameof(SoldierClassData.maxHealth));
        DataToolsGUI.Field(so, nameof(SoldierClassData.damageMultiplier));
        DataToolsGUI.Field(so, nameof(SoldierClassData.attackRange));
        DataToolsGUI.Field(so, nameof(SoldierClassData.moveSpeed));
        DrawDamagePreview(soldierClass);

        SerializedProperty style = DataToolsGUI.Field(so, nameof(SoldierClassData.attackStyle));
        // Each style reads one field of its own; the others keep their values.
        switch ((SoldierClassData.AttackStyle)style.enumValueIndex)
        {
            case SoldierClassData.AttackStyle.Melee:
                DataToolsGUI.Field(so, nameof(SoldierClassData.knockbackDistance));
                break;
            case SoldierClassData.AttackStyle.Ranged:
                DataToolsGUI.Field(so, nameof(SoldierClassData.projectileSpeed));
                break;
            case SoldierClassData.AttackStyle.Magic:
                DataToolsGUI.Field(so, nameof(SoldierClassData.splashRadius));
                break;
        }

        DrawRelationships(soldierClass);
    }

    // Damage per hit is the captain's power times the class multiplier, so show it for a real captain.
    void DrawDamagePreview(SoldierClassData soldierClass)
    {
        captains = DataToolsGUI.LoadAll(captains);
        if (captains.Count == 0) return;
        previewCaptain = Mathf.Clamp(previewCaptain, 0, captains.Count - 1);
        previewCaptain = EditorGUILayout.Popup("Under Captain", previewCaptain, captains.Select(c => c.name).ToArray());
        CaptainData captain = captains[previewCaptain];
        float hit = captain.power * soldierClass.damageMultiplier;
        string splash = soldierClass.attackStyle == SoldierClassData.AttackStyle.Magic && soldierClass.splashRadius > 0f
            ? $" to every enemy within {soldierClass.splashRadius:0.##} tiles" : "";
        DataToolsGUI.Note($"{hit:0.#} damage per hit{splash}, at most {hit * captain.speed:0.#} per second before travel time");
    }

    // Captains like or dislike a class through the tag its display name pluralises to.
    void DrawRelationships(SoldierClassData soldierClass)
    {
        DataToolsGUI.Section("Captains");
        if (!Relationships.TryGetClassTag(soldierClass, out CaptainTag tag))
        {
            EditorGUILayout.HelpBox($"\"{soldierClass.displayName}s\" isn't a soldier tag (Knights, Archers, Mages, Rogues), " +
                "so no captain can like or dislike this class.", MessageType.Warning);
            return;
        }
        captains = DataToolsGUI.LoadAll(captains);
        DataToolsGUI.AssetButtons("Liked By", captains.Where(c => c != null && c.likes != null && c.likes.Contains(tag)), CaptainEditorWindow.OpenFor, "Nobody");
        DataToolsGUI.AssetButtons("Disliked By", captains.Where(c => c != null && c.dislikes != null && c.dislikes.Contains(tag)), CaptainEditorWindow.OpenFor, "Nobody");
    }
}
