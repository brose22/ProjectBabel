using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Edits enemies: a sprite preview drawn to scale against the largest enemy, stats with derived damage
// rates, and the damage type and element with what they beat and lose to.
public class EnemyEditorWindow : DataEditorWindow<EnemyData>
{
    const string Title = "Enemies";

    protected override string WindowTitle => Title;
    const float PreviewSize = 96f;

    List<EnemyData> enemies;
    List<CaptainData> captains;

    [MenuItem("Babel/Data/Enemies", priority = 22)]
    static void ShowWindow() => Open<EnemyEditorWindow>(Title);

    public static void OpenFor(EnemyData enemy) => Open<EnemyEditorWindow>(Title, enemy);

    protected override string DefaultFolder => "Assets/Data/Enemies";

    protected override string[] TableColumns => new[]
    {
        nameof(EnemyData.size), nameof(EnemyData.maxHealth), nameof(EnemyData.moveSpeed), nameof(EnemyData.goldReward),
        nameof(EnemyData.xpReward), nameof(EnemyData.leakDamage), nameof(EnemyData.attackDamage),
        nameof(EnemyData.attackRange), nameof(EnemyData.attackInterval), nameof(EnemyData.towerDamage),
        nameof(EnemyData.towerAttackRange), nameof(EnemyData.rangedAttackRange), nameof(EnemyData.damageType),
        nameof(EnemyData.element),
    };

    protected override string SearchText(EnemyData enemy) => $"{enemy.damageType} {enemy.element}";

    protected override void OnAssetsChanged()
    {
        enemies = null;
        captains = null;
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

        DrawMatchups(so, enemy);
    }

    static string PerSecond(float damage, float interval) => interval > 0f ? $"{damage / interval:0.#}" : "∞";

    EnemyData Largest()
    {
        enemies = DataToolsGUI.LoadAll(enemies);
        return enemies.Where(e => e != null).OrderByDescending(e => e.size).FirstOrDefault();
    }

    void DrawMatchups(SerializedObject so, EnemyData enemy)
    {
        DataToolsGUI.Field(so, nameof(EnemyData.damageType));
        DataToolsGUI.Field(so, nameof(EnemyData.element));
        var strong = new List<string>();
        var weak = new List<string>();
        foreach (DamageType type in System.Enum.GetValues(typeof(DamageType)))
        {
            if (DamageTypeRules.Beats(enemy.damageType, type)) strong.Add(type.ToString());
            if (DamageTypeRules.Beats(type, enemy.damageType)) weak.Add(type.ToString());
        }
        foreach (Element element in System.Enum.GetValues(typeof(Element)))
        {
            if (ElementRules.Beats((int)enemy.element, (int)element)) strong.Add(element.ToString());
            if (ElementRules.Beats((int)element, (int)enemy.element)) weak.Add(element.ToString());
        }
        DataToolsGUI.Note($"Deals and takes ×1.5 against {string.Join(", ", strong)}; ×0.75 against {string.Join(", ", weak)} (stacking)");
    }
}
