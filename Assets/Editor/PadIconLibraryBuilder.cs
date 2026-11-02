using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Babel > Build Pad Icon Library fills the PadIconLibrary from the icon pack and sets up the sprites
// it uses. The art is much larger than it is shown, so those filter smoothly with mipmaps rather than
// dropping pixels like the game's pixel art.
public static class PadIconLibraryBuilder
{
    const string PackFolder = "Assets/Art/Controller";
    const string LibraryPath = "Assets/Resources/PadIconLibrary.asset";

    // File names without their extension, in PadGlyph order.
    static readonly (PadFamily family, string folder, string[] names)[] Sets =
    {
        (PadFamily.Xbox, "Xbox Series", new[]
        {
            "xbox_button_color_a", "xbox_button_color_b", "xbox_button_color_x", "xbox_button_color_y",
            "xbox_lb", "xbox_rb", "xbox_lt", "xbox_rt",
            "xbox_stick_l", "xbox_stick_r", "xbox_stick_l_press", "xbox_stick_r_press",
            "xbox_button_view", "xbox_button_menu",
        }),
        (PadFamily.PlayStation, "PlayStation Series", new[]
        {
            "playstation_button_color_cross", "playstation_button_color_circle", "playstation_button_color_square", "playstation_button_color_triangle",
            "playstation_trigger_l1", "playstation_trigger_r1", "playstation_trigger_l2", "playstation_trigger_r2",
            "playstation_stick_l", "playstation_stick_r", "playstation_stick_l_press", "playstation_stick_r_press",
            "playstation5_button_create", "playstation5_button_options",
        }),
        // Nintendo pads swap the labels: the bottom button is B and the left one Y.
        (PadFamily.Switch, "Nintendo Switch", new[]
        {
            "switch_button_b", "switch_button_a", "switch_button_y", "switch_button_x",
            "switch_button_l", "switch_button_r", "switch_button_zl", "switch_button_zr",
            "switch_stick_l", "switch_stick_r", "switch_stick_l_press", "switch_stick_r_press",
            "switch_button_minus", "switch_button_plus",
        }),
        (PadFamily.SteamDeck, "Steam Deck", new[]
        {
            "steamdeck_button_a", "steamdeck_button_b", "steamdeck_button_x", "steamdeck_button_y",
            "steamdeck_button_l1", "steamdeck_button_r1", "steamdeck_button_l2", "steamdeck_button_r2",
            "steamdeck_stick_l", "steamdeck_stick_r", "steamdeck_stick_l_press", "steamdeck_stick_r_press",
            "steamdeck_button_view", "steamdeck_button_options",
        }),
    };

    [MenuItem("Babel/Build Pad Icon Library", priority = 103)]
    public static void Build()
    {
        var sets = new List<PadIconLibrary.Set>();
        foreach ((PadFamily family, string folder, string[] names) in Sets)
        {
            var sprites = new Sprite[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                string path = $"{PackFolder}/{folder}/Default/{names[i]}.png";
                PrepareSprite(path);
                sprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprites[i] == null)
                {
                    Debug.LogError($"Pad icon library not built: no sprite at {path}.");
                    return;
                }
            }
            sets.Add(new PadIconLibrary.Set { family = family, sprites = sprites });
        }

        var library = AssetDatabase.LoadAssetAtPath<PadIconLibrary>(LibraryPath);
        if (library == null)
        {
            library = ScriptableObject.CreateInstance<PadIconLibrary>();
            AssetDatabase.CreateAsset(library, LibraryPath);
        }
        library.sets = sets.ToArray();
        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();
        Debug.Log($"Pad icon library built with {sets.Count} controller sets.");
    }

    static void PrepareSprite(string path)
    {
        if (AssetImporter.GetAtPath(path) is not TextureImporter importer) return;
        if (importer.filterMode == FilterMode.Bilinear && importer.mipmapEnabled) return;
        importer.filterMode = FilterMode.Bilinear;
        importer.mipmapEnabled = true;
        importer.SaveAndReimport();
    }
}
