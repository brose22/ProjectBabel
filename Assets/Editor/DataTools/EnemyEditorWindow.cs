using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Edits enemies: a sprite preview drawn to scale against the largest enemy, stats with derived damage
// rates, and the armor type and which soldier classes it counters.
public class EnemyEditorWindow : DataEditorWindow<EnemyData>
{
    const string Title = "Enemies";

    protected override string WindowTitle => Title;
    const float PreviewSize = 96f;

    List<EnemyData> enemies;

    [MenuItem("Babel/Data/Enemies", priority = 22)]
    static void ShowWindow() => Open<EnemyEditorWindow>(Title);

    public static void OpenFor(EnemyData enemy) => Open<EnemyEditorWindow>(Title, enemy);

    protected override string DefaultFolder => "Assets/Data/Enemies";

    protected override string[] TableColumns => new[]
    {
        nameof(EnemyData.size), nameof(EnemyData.maxHealth), nameof(EnemyData.moveSpeed), nameof(EnemyData.goldReward),
        nameof(EnemyData.xpReward), nameof(EnemyData.leakDamage), nameof(EnemyData.attackDamage),
        nameof(EnemyData.attackRange), nameof(EnemyData.attackInterval), nameof(EnemyData.towerDamage),
        nameof(EnemyData.towerAttackRange), nameof(EnemyData.rangedAttackRange), nameof(EnemyData.armor),
    };

    protected override string SearchText(EnemyData enemy) => enemy.armor == ArmorType.Unarmored ? "" : enemy.armor.ToString();

    protected override void OnAssetsChanged()
    {
        enemies = null;
    }

    // Like the game: drawn art shows as painted, art-less enemies are a square in their colour.
    protected override void DrawIcon(Rect rect, EnemyData enemy) =>
        DataToolsGUI.DrawSprite(rect, enemy.sprite, enemy.sprite != null ? Color.white : enemy.color);

    protected override void DrawEditor(SerializedObject so)
    {
        var enemy = (EnemyData)so.targetObject;
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(PreviewSize)))
            {
                EnemyData largest = Largest();
                float scale = largest != null && largest.size > 0f ? Mathf.Clamp01(enemy.size / largest.size) : 1f;
                DataToolsGUI.SpritePreview(enemy.sprite, enemy.sprite != null ? Color.white : enemy.color, PreviewSize, scale);
                string note = largest == null || largest == enemy ? "largest enemy" : $"to scale with {largest.name}";
                GUILayout.Label(note, EditorStyles.centeredGreyMiniLabel, GUILayout.Width(PreviewSize));
                DataToolsGUI.SpritePreview(enemy.portrait, Color.white, PreviewSize);
                GUILayout.Label("portrait", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(PreviewSize));
            }
            using (new EditorGUILayout.VerticalScope())
            {
                DataToolsGUI.Section("Identity");
                DataToolsGUI.Field(so, nameof(EnemyData.displayName));
                DataToolsGUI.Field(so, nameof(EnemyData.color));
                DataToolsGUI.Field(so, nameof(EnemyData.sprite));
                DataToolsGUI.Field(so, nameof(EnemyData.portrait));
                DataToolsGUI.Field(so, nameof(EnemyData.size));
            }
        }

        DataToolsGUI.Field(so, nameof(EnemyData.maxHealth));
        DataToolsGUI.Field(so, nameof(EnemyData.moveSpeed));
        DataToolsGUI.Field(so, nameof(EnemyData.goldReward));
        DataToolsGUI.Field(so, nameof(EnemyData.xpReward));
        DataToolsGUI.Field(so, nameof(EnemyData.leakDamage));

        DataToolsGUI.Field(so, nameof(EnemyData.attackDamage));
        DataToolsGUI.Field(so, nameof(EnemyData.attackRange));
        DataToolsGUI.Field(so, nameof(EnemyData.attackInterval));
        DataToolsGUI.Field(so, nameof(EnemyData.rangedAttackRange), "Ranged Attack Range");
        DataToolsGUI.Note(enemy.rangedAttackRange > 0f
            ? $"{PerSecond(enemy.attackDamage, enemy.attackInterval)} damage per second; shoots soldiers within {enemy.rangedAttackRange:0.##} tiles while walking"
            : $"{PerSecond(enemy.attackDamage, enemy.attackInterval)} damage per second, melee only");

        DataToolsGUI.Field(so, nameof(EnemyData.towerDamage));
        using (new EditorGUI.DisabledScope(enemy.towerDamage <= 0f))
        {
            DataToolsGUI.Field(so, nameof(EnemyData.towerAttackRange));
            DataToolsGUI.Field(so, nameof(EnemyData.towerAttackInterval));
        }
        DataToolsGUI.Note(enemy.towerDamage > 0f ? $"{PerSecond(enemy.towerDamage, enemy.towerAttackInterval)} damage per second to towers" : "Never hits towers");

        DrawArmor(so, enemy);
    }

    static string PerSecond(float damage, float interval) => interval > 0f ? $"{damage / interval:0.#}" : "∞";

    EnemyData Largest()
    {
        enemies = DataToolsGUI.LoadAll(enemies);
        return enemies.Where(e => e != null).OrderByDescending(e => e.size).FirstOrDefault();
    }

    void DrawArmor(SerializedObject so, EnemyData enemy)
    {
        DataToolsGUI.Field(so, nameof(EnemyData.armor));
        if (enemy.armor == ArmorType.Unarmored)
        {
            DataToolsGUI.Note("No counters: every soldier class deals full damage");
            return;
        }

        string[] classes = { "Knight", "Archer", "Mage", "Rogue" };
        var notes = new System.Collections.Generic.List<string>();
        for (int c = 0; c < ArmorRules.ClassCount; c++)
        {
            float multiplier = ArmorRules.DamageMultiplier(enemy.armor, c);
            if (!Mathf.Approximately(multiplier, 1f)) notes.Add($"{classes[c]} {multiplier:0.##}×");
        }
        DataToolsGUI.Note($"{ArmorRules.NameOf(enemy.armor)}: {string.Join(", ", notes)} damage");
    }
}
