using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

// Builds the grey-box match: placeholder sprites and tiles, starter data assets, prefabs, and
// the Match scene. Existing data assets and prefabs are kept so tuning survives a rebuild (only
// fields added since they were saved get filled in); the scene itself is regenerated.
public static class GreyBoxMatchBuilder
{
    const string ArtFolder = "Assets/Art/Placeholder";
    // Generated pixel art from Tools/pixel_art.py.
    const string SpriteFolder = "Assets/Art/Sprites";
    const string DataFolder = "Assets/Data";
    const string PrefabFolder = "Assets/Prefabs";
    const string ScenePath = "Assets/Scenes/Match.unity";

    static readonly Vector2Int MapSize = new(24, 12);

    static readonly string[] SliceCaptains = { "Cinder", "Marin", "Ember" };
    const float WaveHealthGrowth = 0.06f;

    // The slice city. Waypoint cells; the first and last sit just off the map so enemies walk on
    // and off screen. The road winds through town from the gate (top left) to the castle (right).
    static readonly Vector2Int[] PathCells =
    {
        new(-1, 10), new(3, 10), new(3, 3), new(8, 3), new(8, 9), new(13, 9), new(13, 2), new(18, 2), new(18, 7), new(24, 7),
    };

    // Props that block building: the town's houses in the road's loops, trees around the edge,
    // and a few rocks. They leave open ground at every bend for towers.
    static readonly (string art, Vector2Int cell)[] Props =
    {
        ("prop_house", new(5, 6)), ("prop_house", new(6, 6)), ("prop_house", new(11, 6)), ("prop_house", new(11, 5)),
        ("prop_house", new(16, 5)), ("prop_house", new(16, 6)), ("prop_house", new(21, 3)), ("prop_house", new(22, 3)),
        ("prop_tree", new(0, 0)), ("prop_tree", new(1, 0)), ("prop_tree", new(0, 1)), ("prop_tree", new(6, 0)),
        ("prop_tree", new(7, 0)), ("prop_tree", new(15, 0)), ("prop_tree", new(1, 6)), ("prop_tree", new(5, 11)),
        ("prop_tree", new(10, 11)), ("prop_tree", new(11, 11)), ("prop_tree", new(20, 10)), ("prop_tree", new(22, 11)),
        ("prop_tree", new(23, 11)), ("prop_tree", new(23, 10)),
        ("prop_rock", new(2, 1)), ("prop_rock", new(9, 6)), ("prop_rock", new(12, 0)), ("prop_rock", new(20, 0)),
        ("prop_rock", new(17, 10)),
    };

    [MenuItem("Babel/Build Grey-Box Match Scene")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        // Open the new scene first: opening a scene unloads unsaved assets, which would
        // break references to anything created below.
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        foreach (string folder in new[] { ArtFolder, DataFolder + "/Abilities", DataFolder + "/Captains", DataFolder + "/Enemies", DataFolder + "/Soldiers", PrefabFolder, "Assets/Scenes" })
            EnsureFolder(folder);

        Sprite square = CreateSpriteAsset(ArtFolder + "/Square.png", 16, 16, (x, y) => Color.white);
        Sprite disc = CreateSpriteAsset(ArtFolder + "/Disc.png", 64, 32, (x, y) =>
        {
            float dx = x - 31.5f, dy = y - 31.5f;
            return dx * dx + dy * dy <= 32f * 32f ? Color.white : Color.clear;
        });

        // A small rounded dot for ranged shots.
        Sprite dot = CreateSpriteAsset(ArtFolder + "/Dot.png", 4, 16, (x, y) =>
            (x is 0 or 3) && (y is 0 or 3) ? Color.clear : Color.white);

        TileBase groundTile = LoadOrCreate<Tile>(ArtFolder + "/GroundTile.asset", t => { t.sprite = square; t.color = new Color(0.22f, 0.36f, 0.22f); });
        TileBase pathTile = LoadOrCreate<Tile>(ArtFolder + "/PathTile.asset", t => { t.sprite = square; t.color = new Color(0.55f, 0.45f, 0.3f); });
        UseTileArt(groundTile, square, "tile_grass");
        UseTileArt(pathTile, square, "tile_path");
        TileBase groundAccentTile = LoadOrCreate<Tile>(ArtFolder + "/GroundAccentTile.asset", t => { t.sprite = ArtSprite("tile_grass_flowers"); });

        MatchData matchData = CreateData();
        ApplyArt();
        Enemy enemyPrefab = CreateEnemyPrefab(square);
        Soldier soldierPrefab = CreateSoldierPrefab(square, dot);
        Tower towerPrefab = CreateTowerPrefab(square, disc, soldierPrefab);

        PopulateScene(scene, square, groundTile, pathTile, groundAccentTile, matchData, enemyPrefab, towerPrefab);
        AssetDatabase.SaveAssets();
        Debug.Log($"Grey-box match built at {ScenePath}. Press Play to try it.");
    }

    // ---------- Data ----------

    static MatchData CreateData()
    {
        EnemyData grunt = LoadOrCreate<EnemyData>(DataFolder + "/Enemies/Grunt.asset", e =>
        {
            e.displayName = "Grunt";
            e.color = new Color(0.85f, 0.2f, 0.25f);
        });

        // Passes towers and chips at them; Knights that hold it up also keep it off the towers.
        EnemyData raider = LoadOrCreate<EnemyData>(DataFolder + "/Enemies/Raider.asset", e =>
        {
            e.displayName = "Raider";
            e.color = new Color(0.6f, 0.3f, 0.85f);
            e.size = 0.5f;
            e.maxHealth = 35f;
            e.moveSpeed = 1.3f;
            e.goldReward = 9;
            e.leakDamage = 1;
            e.attackDamage = 4f;
            e.towerDamage = 7.5f;
            e.towerAttackRange = 1.5f;
            e.towerAttackInterval = 1.5f;
        });

        EnemyData slinger = LoadOrCreate<EnemyData>(DataFolder + "/Enemies/Slinger.asset", e =>
        {
            e.displayName = "Slinger";
            e.color = new Color(0.9f, 0.5f, 0.6f);
            e.size = 0.5f;
            e.maxHealth = 25f;
            e.moveSpeed = 1.3f;
            e.goldReward = 8;
            e.xpReward = 10;
            e.leakDamage = 1;
            e.attackDamage = 4f;
            e.attackInterval = 1.5f;
            e.rangedAttackRange = 2f;
            e.hasWeakness = true;
            e.weakness = Element.Earth;
        });

        EnemyData brute = LoadOrCreate<EnemyData>(DataFolder + "/Enemies/Brute.asset", e =>
        {
            e.displayName = "Brute";
            e.color = new Color(0.55f, 0.1f, 0.15f);
            e.size = 0.85f;
            e.maxHealth = 120f;
            e.moveSpeed = 0.8f;
            e.goldReward = 15;
            e.xpReward = 25;
            e.leakDamage = 2;
            e.attackDamage = 10f;
            e.attackInterval = 1.2f;
            e.hasWeakness = true;
            e.weakness = Element.Water;
        });

        EnemyData bombard = LoadOrCreate<EnemyData>(DataFolder + "/Enemies/Bombard.asset", e =>
        {
            e.displayName = "Bombard";
            e.color = new Color(0.5f, 0.2f, 0.5f);
            e.size = 0.8f;
            e.maxHealth = 90f;
            e.moveSpeed = 0.8f;
            e.goldReward = 15;
            e.xpReward = 25;
            e.leakDamage = 2;
            e.attackDamage = 8f;
            e.attackInterval = 2f;
            e.rangedAttackRange = 2.75f;
            e.towerDamage = 5f;
            e.towerAttackRange = 2f;
            e.towerAttackInterval = 2f;
            e.hasWeakness = true;
            e.weakness = Element.Air;
        });

        // GDD cost tiers. Act: Rogue very low, Knight/Archer low, Mage high.
        // Respawn: Rogue/Archer low, Knight medium, Mage high.
        SoldierClassData knight = SoldierClass("Knight", new Color(0.4f, 0.55f, 1f), SoldierClassData.AttackStyle.Melee,
            actCost: 2, respawnCost: 8, maxHealth: 50f, damage: 1.1f, attackRange: 0.4f, moveSpeed: 3f, knockback: 0.5f);
        // Shorter reach than most captains' range, so archers step out a little for far targets.
        SoldierClassData archer = SoldierClass("Archer", new Color(0.45f, 0.85f, 0.35f), SoldierClassData.AttackStyle.Ranged,
            actCost: 2, respawnCost: 4, maxHealth: 12f, damage: 1.2f, attackRange: 2f, moveSpeed: 3.5f);
        SoldierClassData rogue = SoldierClass("Rogue", new Color(0.9f, 0.9f, 0.75f), SoldierClassData.AttackStyle.Melee,
            actCost: 1, respawnCost: 4, maxHealth: 20f, damage: 1f, attackRange: 0.4f, moveSpeed: 4f);
        // Weak against lone enemies, strong against the bunched-up late waves.
        SoldierClassData mage = SoldierClass("Mage", new Color(1f, 0.45f, 0.75f), SoldierClassData.AttackStyle.Magic,
            actCost: 5, respawnCost: 12, maxHealth: 18f, damage: 2f, attackRange: 1.5f, moveSpeed: 3f, splash: 1f);
        var classes = new List<SoldierClassData> { knight, archer, rogue, mage };
        var defaultMix = new List<SoldierClassData> { knight, archer };

        AbilityData flareVolley = Ability("FlareVolley", "Flare Volley", "Burst area damage", AbilityData.Shape.Area,
            chargeSeconds: 20f, radius: 1.3f, damageMultiplier: 2.5f, statusChance: 0.35f);
        AbilityData undertow = Ability("Undertow", "Undertow", "Area damage, pulls enemies back", AbilityData.Shape.Area,
            chargeSeconds: 18f, radius: 1.1f, damageMultiplier: 1.5f, knockbackDistance: 1.2f, statusChance: 0.35f);
        AbilityData rockfall = Ability("Rockfall", "Rockfall", "Single-target stun hit", AbilityData.Shape.Single,
            chargeSeconds: 16f, damageMultiplier: 4f, statusChance: 0.6f);
        AbilityData galeStrike = Ability("GaleStrike", "Gale Strike", "Long-range pierce", AbilityData.Shape.Line,
            chargeSeconds: 20f, rangeMultiplier: 2f, radius: 0.35f, damageMultiplier: 2.5f, statusChance: 0.3f);
        AbilityData scorchedEarth = Ability("ScorchedEarth", "Scorched Earth", "Damage zone on the path", AbilityData.Shape.Zone,
            chargeSeconds: 22f, radius: 1f, damageMultiplier: 1f, zoneDuration: 5f, statusChance: 0.5f);
        AbilityData crosswind = Ability("Crosswind", "Crosswind", "Pushes enemies back, applies Dizzy", AbilityData.Shape.Area,
            chargeSeconds: 20f, radius: 1.3f, damageMultiplier: 1f, knockbackDistance: 1.5f, statusChance: 0.4f);

        // Support specials reach every tower: Healers restore a share of max health, Buffers boost a
        // stat until the wave ends. The second description is the pre-rework one, for upgrading.
        AbilityData tidalMend = SupportAbility("TidalMend", "Tidal Mend", "Heals all towers and soldiers 35%",
            "Heals tower and soldiers", chargeSeconds: 20f, healFraction: 0.35f);
        AbilityData flowState = SupportAbility("FlowState", "Flow State", "All towers act 25% faster this wave",
            "Faster soldier acts", chargeSeconds: 22f, buffEffect: SupportEffect.ActSpeed, buffAmount: 0.25f);
        AbilityData kindle = SupportAbility("Kindle", "Kindle", "All towers deal 25% more damage this wave",
            "Damage boost to nearby towers", chargeSeconds: 22f, buffEffect: SupportEffect.Damage, buffAmount: 0.25f);
        AbilityData stoneWard = SupportAbility("StoneWard", "Stone Ward", "All towers take 30% less damage this wave",
            "Defense boost, towers and soldiers", chargeSeconds: 22f, buffEffect: SupportEffect.Defense, buffAmount: 0.3f);
        AbilityData mendTheLand = SupportAbility("MendTheLand", "Mend the Land", "Heals all 25%, +15% defense this wave",
            "Area heal plus short shield", chargeSeconds: 24f, healFraction: 0.25f, buffEffect: SupportEffect.Defense, buffAmount: 0.15f);
        AbilityData secondWind = SupportAbility("SecondWind", "Second Wind", "Heals all 20%, +15% act speed this wave",
            "Heal plus speed boost", chargeSeconds: 22f, healFraction: 0.2f, buffEffect: SupportEffect.ActSpeed, buffAmount: 0.15f);

        var allCaptains = new List<CaptainData>
        {
            Captain("Cinder", Element.Fire, new Color(0.95f, 0.45f, 0.15f), 12f, 2f, 0.8f, "Flare Volley", "Burst area damage", flareVolley,
                new[] { CaptainTag.Mages, CaptainTag.Ability, CaptainTag.Moving }, new[] { CaptainTag.Knights, CaptainTag.Stable, CaptainTag.Selling }, refusalTemper: 1.25f),
            Captain("Brine", Element.Water, new Color(0.2f, 0.45f, 0.9f), 9f, 2.5f, 1f, "Undertow", "Area damage, pulls enemies back", undertow,
                new[] { CaptainTag.Archers, CaptainTag.Mages, CaptainTag.Moving }, new[] { CaptainTag.Knights, CaptainTag.Selling, CaptainTag.Stable }, refusalTemper: 1f),
            Captain("Slate", Element.Earth, new Color(0.55f, 0.5f, 0.45f), 14f, 1.75f, 0.6f, "Rockfall", "Single-target stun hit", rockfall,
                new[] { CaptainTag.Knights, CaptainTag.Rogues, CaptainTag.Stable }, new[] { CaptainTag.Mages, CaptainTag.Moving, CaptainTag.Ability }, refusalTemper: 1f),
            Captain("Zephyr", Element.Air, new Color(0.6f, 0.9f, 0.9f), 8f, 3f, 1.2f, "Gale Strike", "Long-range pierce", galeStrike,
                new[] { CaptainTag.Rogues, CaptainTag.Archers, CaptainTag.Moving }, new[] { CaptainTag.Knights, CaptainTag.Stable, CaptainTag.Selling }, refusalTemper: 1f),
            Captain("Pyre", Element.Fire, new Color(0.75f, 0.15f, 0.1f), 10f, 2f, 1f, "Scorched Earth", "Damage zone on the path", scorchedEarth,
                new[] { CaptainTag.Rogues, CaptainTag.Mages, CaptainTag.Casualties }, new[] { CaptainTag.Archers, CaptainTag.Stable, CaptainTag.Leaks }, refusalTemper: 1.5f),
            Captain("Skye", Element.Air, new Color(0.8f, 0.8f, 1f), 9f, 2.25f, 1f, "Crosswind", "Pushes enemies back, applies Dizzy", crosswind,
                new[] { CaptainTag.Mages, CaptainTag.Moving, CaptainTag.Ability }, new[] { CaptainTag.Rogues, CaptainTag.Stable, CaptainTag.Selling }, refusalTemper: 1.25f),
            Captain("Marin", Element.Water, new Color(0.3f, 0.75f, 0.8f), 7f, 2f, 1f, "Tidal Mend", tidalMend.description, tidalMend,
                new[] { CaptainTag.Knights, CaptainTag.Stable, CaptainTag.Ability }, new[] { CaptainTag.Rogues, CaptainTag.Leaks, CaptainTag.Casualties },
                CaptainRole.Healer, refusalTemper: 0.75f),
            Captain("Rill", Element.Water, new Color(0.35f, 0.6f, 1f), 7f, 2f, 1.1f, "Flow State", flowState.description, flowState,
                new[] { CaptainTag.Rogues, CaptainTag.Ability, CaptainTag.Stable }, new[] { CaptainTag.Mages, CaptainTag.Leaks, CaptainTag.Moving },
                CaptainRole.Buffer, refusalTemper: 1f),
            Captain("Ember", Element.Fire, new Color(1f, 0.6f, 0.3f), 8f, 2f, 1f, "Kindle", kindle.description, kindle,
                new[] { CaptainTag.Archers, CaptainTag.Ability, CaptainTag.Leaks }, new[] { CaptainTag.Knights, CaptainTag.Selling, CaptainTag.Stable },
                CaptainRole.Buffer, refusalTemper: 1.25f),
            Captain("Boulder", Element.Earth, new Color(0.6f, 0.45f, 0.3f), 7f, 1.75f, 0.9f, "Stone Ward", stoneWard.description, stoneWard,
                new[] { CaptainTag.Knights, CaptainTag.Archers, CaptainTag.Stable }, new[] { CaptainTag.Mages, CaptainTag.Moving, CaptainTag.Casualties },
                CaptainRole.Buffer, refusalTemper: 0.75f),
            Captain("Terra", Element.Earth, new Color(0.5f, 0.7f, 0.35f), 7f, 2f, 1f, "Mend the Land", mendTheLand.description, mendTheLand,
                new[] { CaptainTag.Knights, CaptainTag.Stable, CaptainTag.Leaks }, new[] { CaptainTag.Rogues, CaptainTag.Moving, CaptainTag.Casualties },
                CaptainRole.Healer, refusalTemper: 0.75f),
            Captain("Gust", Element.Air, new Color(0.75f, 0.95f, 0.85f), 7f, 2.25f, 1.1f, "Second Wind", secondWind.description, secondWind,
                new[] { CaptainTag.Archers, CaptainTag.Moving, CaptainTag.Ability }, new[] { CaptainTag.Knights, CaptainTag.Stable, CaptainTag.Casualties },
                CaptainRole.Healer, refusalTemper: 0.75f),
        };

        // The vertical slice runs with one captain of each role (GDD month 12); the other captains
        // are still built so they are ready for later milestones.
        var roster = new List<CaptainData>();
        foreach (string name in SliceCaptains)
            roster.Add(allCaptains.Find(c => c.name == name));

        string matchPath = DataFolder + "/GreyBoxMatch.asset";
        bool matchExisted = AssetDatabase.LoadAssetAtPath<MatchData>(matchPath) != null;
        MatchData match = LoadOrCreate<MatchData>(matchPath, m =>
        {
            m.roster = roster;
            m.availableClasses = classes;
            m.defaultMix = defaultMix;
            m.restoreBaseCost = 15;
            m.relocationCost = 25;
            m.waves = BuildWaves(grunt, raider, slinger, brute, bombard);
        });
        if (matchExisted) UpgradeMatch(match, roster, classes, defaultMix, grunt, raider, slinger, brute, bombard);
        return match;
    }

    // ~56 enemies over 10 waves (GDD: ~50 kills per match); Raiders join from wave 3 and
    // follow each wave's Grunts. Slingers join from wave 2, Brutes from wave 4 and Bombards from
    // wave 6, after the Raiders.
    static List<WaveDefinition> BuildWaves(EnemyData grunt, EnemyData raider, EnemyData slinger, EnemyData brute, EnemyData bombard)
    {
        int[] grunts = { 2, 3, 3, 4, 4, 4, 4, 4, 5, 5 };
        int[] raiders = { 0, 0, 1, 1, 2, 2, 2, 3, 3, 4 };
        var waves = new List<WaveDefinition>();
        for (int i = 0; i < grunts.Length; i++)
        {
            // +6% enemy health per wave, tuned for the slice's three captains by simulated playtests.
            var wave = new WaveDefinition { healthMultiplier = 1f + WaveHealthGrowth * i };
            wave.groups.Add(new SpawnGroup { enemy = grunt, count = grunts[i], spawnInterval = Mathf.Max(0.6f, 1.4f - 0.08f * i) });
            if (raiders[i] > 0)
                wave.groups.Add(new SpawnGroup { enemy = raider, count = raiders[i], spawnInterval = 1.5f, delayBefore = 1.5f });
            if (i >= 1)
                wave.groups.Add(new SpawnGroup { enemy = slinger, count = 1 + i / 3, spawnInterval = 1.2f, delayBefore = 1.5f });
            if (i >= 3)
                wave.groups.Add(new SpawnGroup { enemy = brute, count = 1 + (i - 3) / 3, spawnInterval = 2.5f, delayBefore = 2f });
            if (i >= 5)
                wave.groups.Add(new SpawnGroup { enemy = bombard, count = 1 + (i - 5) / 3, spawnInterval = 3f, delayBefore = 2f });
            waves.Add(wave);
        }
        return waves;
    }

    static SoldierClassData SoldierClass(string name, Color color, SoldierClassData.AttackStyle style, int actCost, int respawnCost,
        float maxHealth, float damage, float attackRange, float moveSpeed, float knockback = 0f, float splash = 0f)
    {
        return LoadOrCreate<SoldierClassData>($"{DataFolder}/Soldiers/{name}.asset", s =>
        {
            s.displayName = name;
            s.color = color;
            s.attackStyle = style;
            s.actCost = actCost;
            s.respawnCost = respawnCost;
            s.maxHealth = maxHealth;
            s.damageMultiplier = damage;
            s.attackRange = attackRange;
            s.moveSpeed = moveSpeed;
            s.knockbackDistance = knockback;
            s.projectileSpeed = 10f;
            s.splashRadius = splash;
        });
    }

    // ---------- Upgrades ----------
    // Assets saved before Milestone 2 lack its fields. Fill only what is still unset so
    // designer tuning survives, and leave assets untouched when there is nothing to do.

    static void UpgradeMatch(MatchData match, List<CaptainData> roster, List<SoldierClassData> classes, List<SoldierClassData> defaultMix,
        EnemyData grunt, EnemyData raider, EnemyData slinger, EnemyData brute, EnemyData bombard)
    {
        bool changed = false;
        // Captains added after the match was saved join the end of its roster.
        foreach (CaptainData captain in roster)
        {
            if (match.roster.Contains(captain)) continue;
            match.roster.Add(captain);
            changed = true;
        }
        if (match.availableClasses.Count == 0)
        {
            match.availableClasses = new List<SoldierClassData>(classes);
            changed = true;
        }
        if (match.defaultMix.Count == 0)
        {
            match.defaultMix = new List<SoldierClassData>(defaultMix);
            changed = true;
        }
        // 0 is a legitimate "free" setting, so only fields never saved count as unset.
        if (!IsSerialized(match, nameof(MatchData.restoreBaseCost)))
        {
            match.restoreBaseCost = 15;
            changed = true;
        }
        if (!IsSerialized(match, nameof(MatchData.relocationCost)))
        {
            match.relocationCost = 25;
            changed = true;
        }
        if (!match.waves.Exists(w => w.groups.Exists(g => g.enemy != null && g.enemy.towerDamage > 0f)))
        {
            match.waves = BuildWaves(grunt, raider, slinger, brute, bombard);
            changed = true;
        }
        // Waves built before ranged enemies existed get rebuilt with them.
        if (!match.waves.Exists(w => w.groups.Exists(g => g.enemy != null && g.enemy.rangedAttackRange > 0f)))
        {
            match.waves = BuildWaves(grunt, raider, slinger, brute, bombard);
            changed = true;
        }
        if (!changed) return;
        EditorUtility.SetDirty(match);
        Debug.Log($"Upgraded {AssetDatabase.GetAssetPath(match)} with Milestone 2 data.");
    }

    // Whether the field appears in the asset's text serialization. Fields added to the class
    // after the asset was last saved are missing until it is saved again.
    static bool IsSerialized(Object asset, string field)
    {
        string path = AssetDatabase.GetAssetPath(asset);
        return !File.Exists(path) || Regex.IsMatch(File.ReadAllText(path), $@"^\s*{field}:", RegexOptions.Multiline);
    }

    static Soldier UpgradeSoldierPrefab(Soldier prefab, Sprite projectileSprite)
    {
        var so = new SerializedObject(prefab);
        SerializedProperty property = so.FindProperty("projectileSprite");
        if (property.objectReferenceValue != null) return prefab;
        property.objectReferenceValue = projectileSprite;
        so.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SavePrefabAsset(prefab.gameObject);
        Debug.Log($"Upgraded {AssetDatabase.GetAssetPath(prefab)} with a projectile sprite.");
        return prefab;
    }

    static Tower UpgradeTowerPrefab(Tower prefab, Sprite square)
    {
        if (new SerializedObject(prefab).FindProperty("healthBar").objectReferenceValue != null) return prefab;

        string path = AssetDatabase.GetAssetPath(prefab);
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Tower tower = root.GetComponent<Tower>();
            // Reuse a bar left by an earlier, unwired attempt rather than stacking another.
            Transform existing = root.transform.Find(TowerHealthBarName);
            HealthBar bar = existing != null && existing.TryGetComponent(out HealthBar found) ? found : AddTowerHealthBar(root.transform, square);
            Wire(tower, ("healthBar", bar));
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        Debug.Log($"Upgraded {path} with a tower health bar.");
        return AssetDatabase.LoadAssetAtPath<Tower>(path);
    }

    static CaptainData Captain(string name, Element element, Color color, float power, float range, float speed,
        string ability, string abilityDescription, AbilityData abilityData, CaptainTag[] likes, CaptainTag[] dislikes,
        CaptainRole role = CaptainRole.Attacker, float refusalTemper = 1f)
    {
        CaptainData captain = LoadOrCreate<CaptainData>($"{DataFolder}/Captains/{name}.asset", c =>
        {
            c.displayName = name;
            c.element = element;
            c.role = role;
            c.color = color;
            c.power = power;
            c.range = range;
            c.speed = speed;
            c.abilityName = ability;
            c.abilityDescription = abilityDescription;
            c.ability = abilityData;
            c.likes = likes;
            c.dislikes = dislikes;
            c.refusalTemper = refusalTemper;
        });
        // Captains saved before Milestone 3 have no ability yet.
        if (captain.ability == null)
        {
            captain.ability = abilityData;
            EditorUtility.SetDirty(captain);
            Debug.Log($"Upgraded {AssetDatabase.GetAssetPath(captain)} with its ability.");
        }
        // Support captains saved before the aura rework still describe the old special.
        if (captain.role != CaptainRole.Attacker && captain.ability == abilityData && captain.abilityDescription != abilityDescription)
        {
            captain.abilityDescription = abilityDescription;
            EditorUtility.SetDirty(captain);
            Debug.Log($"Upgraded {AssetDatabase.GetAssetPath(captain)} with its reworked special's description.");
        }
        // Captains saved before bond data existed lack a refusal temper. 1 is a legitimate value, so only a never-saved field counts as unset.
        if (!IsSerialized(captain, nameof(CaptainData.refusalTemper)))
        {
            captain.refusalTemper = refusalTemper;
            EditorUtility.SetDirty(captain);
            Debug.Log($"Upgraded {AssetDatabase.GetAssetPath(captain)} with its refusal temper.");
        }
        return captain;
    }

    static AbilityData Ability(string name, string displayName, string description, AbilityData.Shape shape, float chargeSeconds,
        float rangeMultiplier = 1f, float radius = 1f, float damageMultiplier = 2f, float knockbackDistance = 0f, float zoneDuration = 4f, float statusChance = 0.3f)
    {
        return LoadOrCreate<AbilityData>($"{DataFolder}/Abilities/{name}.asset", a =>
        {
            a.displayName = displayName;
            a.description = description;
            a.shape = shape;
            a.chargeSeconds = chargeSeconds;
            a.rangeMultiplier = rangeMultiplier;
            a.radius = radius;
            a.damageMultiplier = damageMultiplier;
            a.knockbackDistance = knockbackDistance;
            a.zoneDuration = zoneDuration;
            a.statusChance = statusChance;
        });
    }

    static AbilityData SupportAbility(string name, string displayName, string description, string preReworkDescription,
        float chargeSeconds, float healFraction = 0f, SupportEffect buffEffect = SupportEffect.None, float buffAmount = 0f)
    {
        void Apply(AbilityData a)
        {
            a.displayName = displayName;
            a.description = description;
            a.shape = AbilityData.Shape.Support;
            a.chargeSeconds = chargeSeconds;
            a.statusChance = 0f;
            a.healFraction = healFraction;
            a.buffEffect = buffEffect;
            a.buffAmount = buffAmount;
            a.cameraShake = 0.05f;
        }

        AbilityData ability = LoadOrCreate<AbilityData>($"{DataFolder}/Abilities/{name}.asset", Apply);
        // Abilities saved before the aura rework were tuned for short, local boosts; one still
        // carrying its old description gets the map-wide, whole-wave numbers.
        if (ability.description == preReworkDescription)
        {
            Apply(ability);
            EditorUtility.SetDirty(ability);
            Debug.Log($"Upgraded {AssetDatabase.GetAssetPath(ability)} to a map-wide support special.");
        }
        return ability;
    }

    // ---------- Prefabs ----------

    static Enemy CreateEnemyPrefab(Sprite square)
    {
        return LoadOrCreatePrefab<Enemy>(PrefabFolder + "/Enemy.prefab", root =>
        {
            SpriteRenderer body = AddSprite("Body", root.transform, square, Color.white, 10, Vector3.zero, Vector3.one * 0.6f);
            HealthBar bar = AddBar(root.transform, square, new Vector3(0f, 0.45f), 0.6f, new Color(0.3f, 0.9f, 0.3f), false);
            Enemy enemy = root.AddComponent<Enemy>();
            Wire(enemy, ("body", body), ("healthBar", bar));
        });
    }

    static Soldier CreateSoldierPrefab(Sprite square, Sprite projectileSprite)
    {
        string path = PrefabFolder + "/Soldier.prefab";
        bool existed = AssetDatabase.LoadAssetAtPath<Soldier>(path) != null;
        Soldier prefab = LoadOrCreatePrefab<Soldier>(path, root =>
        {
            SpriteRenderer body = AddSprite("Body", root.transform, square, Color.white, 11, Vector3.zero, Vector3.one * 0.3f);
            HealthBar bar = AddBar(root.transform, square, new Vector3(0f, 0.28f), 0.35f, new Color(0.4f, 0.8f, 1f), true);
            Soldier soldier = root.AddComponent<Soldier>();
            Wire(soldier, ("body", body), ("healthBar", bar), ("projectileSprite", projectileSprite));
        });
        return existed ? UpgradeSoldierPrefab(prefab, projectileSprite) : prefab;
    }

    static Tower CreateTowerPrefab(Sprite square, Sprite disc, Soldier soldierPrefab)
    {
        string path = PrefabFolder + "/Tower.prefab";
        bool existed = AssetDatabase.LoadAssetAtPath<Tower>(path) != null;
        Tower prefab = LoadOrCreatePrefab<Tower>(path, root =>
        {
            SpriteRenderer range = AddSprite("RangeIndicator", root.transform, disc, new Color(1f, 1f, 1f, 0.12f), 2, Vector3.zero, Vector3.one);
            SpriteRenderer body = AddSprite("Body", root.transform, square, Color.white, 5, Vector3.zero, Vector3.one * 0.8f);
            HealthBar reserve = AddBar(root.transform, square, new Vector3(0f, -0.45f), 0.8f, new Color(1f, 0.85f, 0.2f), false);
            HealthBar health = AddTowerHealthBar(root.transform, square);
            Tower tower = root.AddComponent<Tower>();
            Wire(tower, ("body", body), ("rangeIndicator", range), ("reserveBar", reserve), ("healthBar", health), ("soldierPrefab", soldierPrefab));
        });
        return existed ? UpgradeTowerPrefab(prefab, square) : prefab;
    }

    const string TowerHealthBarName = "HealthBar";

    // Sits just above the reserve bar and only shows once the tower has taken damage.
    static HealthBar AddTowerHealthBar(Transform parent, Sprite square)
    {
        HealthBar bar = AddBar(parent, square, new Vector3(0f, -0.33f), 0.8f, new Color(0.85f, 0.2f, 0.2f), true);
        bar.gameObject.name = TowerHealthBarName;
        return bar;
    }

    static HealthBar AddBar(Transform parent, Sprite square, Vector3 localPosition, float width, Color fillColor, bool hideWhenFull)
    {
        var barObject = new GameObject("Bar");
        barObject.transform.SetParent(parent, false);
        barObject.transform.localPosition = localPosition;
        AddSprite("Back", barObject.transform, square, new Color(0f, 0f, 0f, 0.7f), 20, Vector3.zero, new Vector3(width, 0.08f, 1f));
        SpriteRenderer fill = AddSprite("Fill", barObject.transform, square, fillColor, 21, Vector3.zero, new Vector3(width, 0.08f, 1f));
        HealthBar bar = barObject.AddComponent<HealthBar>();
        var so = new SerializedObject(bar);
        so.FindProperty("fill").objectReferenceValue = fill.transform;
        so.FindProperty("width").floatValue = width;
        so.FindProperty("hideWhenFull").boolValue = hideWhenFull;
        so.ApplyModifiedPropertiesWithoutUndo();
        return bar;
    }

    // ---------- Scene ----------

    static void PopulateScene(Scene scene, Sprite square, TileBase groundTile, TileBase pathTile, TileBase groundAccentTile,
        MatchData matchData, Enemy enemyPrefab, Tower towerPrefab)
    {
        // Camera: 384x216 reference at 16 px per unit shows 24 x 13.5 tiles; the top strip is for the HUD.
        var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
        cameraObject.transform.position = new Vector3(MapSize.x * 0.5f, 6.75f, -10f);
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 6.75f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.08f, 0.09f, 0.12f);
        PixelPerfectCamera pixelPerfect = cameraObject.AddComponent<PixelPerfectCamera>();
        pixelPerfect.assetsPPU = 16;
        pixelPerfect.refResolutionX = 384;
        pixelPerfect.refResolutionY = 216;

        var lightObject = new GameObject("Global Light 2D");
        Light2D light = lightObject.AddComponent<Light2D>();
        var lightSo = new SerializedObject(light);
        lightSo.FindProperty("m_LightType").intValue = (int)Light2D.LightType.Global;
        lightSo.ApplyModifiedPropertiesWithoutUndo();

        // Path waypoints sit at cell centers.
        var pathObject = new GameObject("EnemyPath");
        EnemyPath path = pathObject.AddComponent<EnemyPath>();
        for (int i = 0; i < PathCells.Length; i++)
        {
            var point = new GameObject($"Point {i}");
            point.transform.SetParent(pathObject.transform, false);
            point.transform.position = new Vector3(PathCells[i].x + 0.5f, PathCells[i].y + 0.5f, 0f);
        }

        var gridObject = new GameObject("Grid");
        gridObject.AddComponent<Grid>();
        Tilemap ground = AddTilemap(gridObject.transform, "Ground", 0);
        Tilemap pathTilemap = AddTilemap(gridObject.transform, "Path", 1);
        Tilemap propTilemap = AddTilemap(gridObject.transform, "Props", 2);
        foreach ((string art, Vector2Int cell) in Props)
        {
            TileBase prop = PropTile(art);
            if (prop != null) propTilemap.SetTile(new Vector3Int(cell.x, cell.y, 0), prop);
        }
        GridMap gridMap = gridObject.AddComponent<GridMap>();
        Wire(gridMap, ("groundTilemap", ground), ("pathTilemap", pathTilemap), ("propTilemap", propTilemap), ("groundTile", groundTile),
            ("pathTile", pathTile), ("groundAccentTile", groundAccentTile), ("path", path));
        var gridSo = new SerializedObject(gridMap);
        gridSo.FindProperty("size").vector2IntValue = MapSize;
        gridSo.ApplyModifiedPropertiesWithoutUndo();
        gridMap.Paint();

        Vector2Int spawn = PathCells[1], goal = PathCells[^2];
        AddProp("Spawn Marker", "prop_gate", square, new Color(0.6f, 0.1f, 0.15f), new Vector3(0.5f, spawn.y + 0.5f));
        AddProp("Base Marker", "prop_castle", square, new Color(0.25f, 0.4f, 0.95f), new Vector3(MapSize.x - 0.5f, goal.y + 0.5f));

        var matchObject = new GameObject("Match");
        MatchManager match = matchObject.AddComponent<MatchManager>();
        WaveSpawner spawner = matchObject.AddComponent<WaveSpawner>();
        PlacementController placement = matchObject.AddComponent<PlacementController>();
        MatchHUD hud = matchObject.AddComponent<MatchHUD>();
        SpriteRenderer hoverMarker = AddSprite("Hover Marker", matchObject.transform, square, Color.white, 4, Vector3.zero, Vector3.one);
        hoverMarker.enabled = false;

        Wire(match, ("data", matchData), ("grid", gridMap), ("spawner", spawner), ("towerPrefab", towerPrefab));
        Wire(spawner, ("enemyPrefab", enemyPrefab), ("path", path));
        Wire(placement, ("match", match), ("grid", gridMap), ("hud", hud), ("hoverMarker", hoverMarker));
        Wire(hud, ("match", match), ("placement", placement));

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddSceneToBuild(ScenePath);
    }

    static Tilemap AddTilemap(Transform parent, string name, int sortingOrder)
    {
        var tilemapObject = new GameObject(name);
        tilemapObject.transform.SetParent(parent, false);
        Tilemap tilemap = tilemapObject.AddComponent<Tilemap>();
        tilemapObject.AddComponent<TilemapRenderer>().sortingOrder = sortingOrder;
        return tilemap;
    }

    static void AddSceneToBuild(string path)
    {
        var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        scenes.RemoveAll(s => s.path == path);
        scenes.Insert(0, new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    // ---------- Helpers ----------

    static SpriteRenderer AddSprite(string name, Transform parent, Sprite sprite, Color color, int sortingOrder, Vector3 localPosition, Vector3 localScale)
    {
        var spriteObject = new GameObject(name);
        spriteObject.transform.SetParent(parent, false);
        spriteObject.transform.localPosition = localPosition;
        spriteObject.transform.localScale = localScale;
        SpriteRenderer renderer = spriteObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        return renderer;
    }

    static void Wire(Object target, params (string field, Object value)[] references)
    {
        var so = new SerializedObject(target);
        foreach ((string field, Object value) in references)
        {
            SerializedProperty property = so.FindProperty(field);
            if (property == null)
                throw new InvalidOperationException($"{target.GetType().Name} has no serialized field '{field}'");
            property.objectReferenceValue = value;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---------- Art ----------

    static Sprite ArtSprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{SpriteFolder}/{name}.png");

    static IEnumerable<T> LoadAll<T>(string folder) where T : Object
    {
        foreach (string guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { folder }))
            yield return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
    }

    // Gives generated art (named after the asset: captain_cinder, soldier_knight, enemy_grunt) to
    // data that has no sprite yet, so art set by hand is kept and a second run changes nothing.
    // Captains without their own art share a pale generic sprite tinted with the captain colour.
    static void ApplyArt()
    {
        foreach (CaptainData captain in LoadAll<CaptainData>(DataFolder + "/Captains"))
        {
            if (captain.sprite != null) continue;
            Sprite own = ArtSprite("captain_" + captain.name.ToLowerInvariant());
            Sprite sprite = own != null ? own : ArtSprite("captain_generic");
            if (sprite == null) continue;
            captain.sprite = sprite;
            captain.tintSprite = own == null;
            MarkUpgraded(captain, "its sprite");
        }
        foreach (SoldierClassData soldierClass in LoadAll<SoldierClassData>(DataFolder + "/Soldiers"))
            UseArt(soldierClass, soldierClass.sprite, "soldier_", s => soldierClass.sprite = s);
        foreach (EnemyData enemy in LoadAll<EnemyData>(DataFolder + "/Enemies"))
            UseArt(enemy, enemy.sprite, "enemy_", s => enemy.sprite = s);
    }

    static void UseArt(Object asset, Sprite current, string prefix, Action<Sprite> assign)
    {
        if (current != null) return;
        Sprite sprite = ArtSprite(prefix + asset.name.ToLowerInvariant());
        if (sprite == null) return;
        assign(sprite);
        MarkUpgraded(asset, "its sprite");
    }

    static void MarkUpgraded(Object asset, string what)
    {
        EditorUtility.SetDirty(asset);
        Debug.Log($"Upgraded {AssetDatabase.GetAssetPath(asset)} with {what}.");
    }

    // Swaps a tile still showing the placeholder square for its generated art, drawn as painted.
    static void UseTileArt(TileBase tileBase, Sprite placeholder, string artName)
    {
        if (tileBase is not Tile tile || tile.sprite != placeholder) return;
        Sprite sprite = ArtSprite(artName);
        if (sprite == null) return;
        tile.sprite = sprite;
        tile.color = Color.white;
        MarkUpgraded(tile, "its art");
    }

    // A tile asset for a prop's art (PropTree.asset for prop_tree), or null if the art is missing.
    static TileBase PropTile(string artName)
    {
        Sprite art = ArtSprite(artName);
        if (art == null) return null;
        string assetName = "Prop" + char.ToUpperInvariant(artName[5]) + artName.Substring(6);
        return LoadOrCreate<Tile>($"{ArtFolder}/{assetName}.asset", t => t.sprite = art);
    }

    // A map prop drawn with its generated art, or a tinted square if the art is missing.
    static void AddProp(string name, string artName, Sprite square, Color fallbackColor, Vector3 position)
    {
        Sprite art = ArtSprite(artName);
        if (art != null) AddSprite(name, null, art, Color.white, 3, position, Vector3.one);
        else AddSprite(name, null, square, fallbackColor, 3, position, Vector3.one * 0.9f);
    }

    static T LoadOrCreate<T>(string path, Action<T> init) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        asset = ScriptableObject.CreateInstance<T>();
        init(asset);
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    static T LoadOrCreatePrefab<T>(string path, Action<GameObject> build) where T : Component
    {
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null) return existing;
        var root = new GameObject(Path.GetFileNameWithoutExtension(path));
        build(root);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab.GetComponent<T>();
    }

    static Sprite CreateSpriteAsset(string path, int size, int pixelsPerUnit, Func<int, int, Color> pixel)
    {
        if (!File.Exists(path))
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int x = 0; x < size; x++)
                for (int y = 0; y < size; y++)
                    texture.SetPixel(x, y, pixel(x, y));
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }
}
