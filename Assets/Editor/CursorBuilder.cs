using System.IO;
using UnityEditor;
using UnityEngine;

// Draws the pixel-art arrow cursor (dark outline, cream fill, lit left edge, gold shaded right edge),
// saves it to Assets/Art/Cursors and makes it the project's default cursor. Re-run to regenerate.
public static class CursorBuilder
{
    const string Path = "Assets/Art/Cursors/cursor_arrow.png";
    const int Scale = 2;

    static readonly string[] Map =
    {
        "#...........",
        "##..........",
        "#o#.........",
        "#oo#........",
        "#ooo#.......",
        "#oooo#......",
        "#ooooo#.....",
        "#oooooo#....",
        "#ooooooo#...",
        "#oooooooo#..",
        "#ooooooooo#.",
        "#ooooo#####.",
        "#oo#oo#.....",
        "#o#.#oo#....",
        "##..#oo#....",
        "#....#oo#...",
        ".....#oo#...",
        "......##....",
    };

    static readonly Color32 Outline = new Color32(0x2A, 0x1B, 0x12, 255);
    static readonly Color32 Fill = new Color32(0xF7, 0xEB, 0xC6, 255);
    static readonly Color32 Light = new Color32(0xFF, 0xFF, 0xFF, 255);
    static readonly Color32 Shade = new Color32(0xD9, 0xA4, 0x41, 255);

    [MenuItem("Tools/Build Cursor")]
    public static void Build()
    {
        int w = Map[0].Length, h = Map.Length;
        var tex = new Texture2D(w * Scale, h * Scale, TextureFormat.RGBA32, false);
        var clear = new Color32(0, 0, 0, 0);
        for (int y = 0; y < tex.height; y++)
            for (int x = 0; x < tex.width; x++)
                tex.SetPixel(x, y, clear);

        for (int row = 0; row < h; row++)
        for (int col = 0; col < w; col++)
        {
            char c = Map[row][col];
            if (c == '.') continue;
            Color32 color = Outline;
            if (c == 'o')
            {
                bool left = col > 0 && Map[row][col - 1] == '#';
                bool right = col < w - 1 && Map[row][col + 1] == '#';
                color = left ? Light : right ? Shade : Fill;
            }
            for (int dy = 0; dy < Scale; dy++)
            for (int dx = 0; dx < Scale; dx++)
                tex.SetPixel(col * Scale + dx, (h - 1 - row) * Scale + dy, color);
        }
        tex.Apply();

        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
        File.WriteAllBytes(Path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(Path, ImportAssetOptions.ForceUpdate);

        var importer = (TextureImporter)AssetImporter.GetAtPath(Path);
        importer.textureType = TextureImporterType.Cursor;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();

        var cursor = AssetDatabase.LoadAssetAtPath<Texture2D>(Path);
        PlayerSettings.defaultCursor = cursor;
        PlayerSettings.cursorHotspot = new Vector2(Scale, Scale);
        AssetDatabase.SaveAssets();
        Debug.Log("Cursor built and set as the default cursor: " + Path);
    }
}
