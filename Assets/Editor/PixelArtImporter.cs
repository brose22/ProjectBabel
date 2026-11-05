using System.IO;
using UnityEditor;
using UnityEngine;

// Imports everything in Assets/Art/Sprites as crisp pixel-art sprites: point filtering, no
// compression or mips. Pixels per unit follow the art's resolution so a sprite keeps its size in
// the world whatever it was drawn at: a square sprite fills one tile (soldiers, named soldier_*,
// fill half a tile), so 16 px art imports at 16 PPU and 32 px PixelLab art at 32. Anything else
// (sheets, odd sizes) stays at the base 16.
public class PixelArtImporter : AssetPostprocessor
{
    const string Folder = "Assets/Art/Sprites/";
    public const int PixelsPerUnit = 16;

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Folder)) return;
        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = PixelsPerUnitFor(assetPath);
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
    }

    static int PixelsPerUnitFor(string path)
    {
        if (!TryReadPngSize(path, out int width, out int height) || width != height) return PixelsPerUnit;
        // A square sprite covers a tile, or half a tile for soldiers.
        float worldSize = Path.GetFileName(path).StartsWith("soldier_") ? 0.5f : 1f;
        return Mathf.Max(1, Mathf.RoundToInt(width / worldSize));
    }

    // Reads the width and height from a PNG's IHDR chunk, which always sits at bytes 16-23.
    static bool TryReadPngSize(string path, out int width, out int height)
    {
        width = height = 0;
        if (!path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase) || !File.Exists(path)) return false;
        var header = new byte[24];
        using (var stream = File.OpenRead(path))
            if (stream.Read(header, 0, 24) < 24) return false;
        width = header[16] << 24 | header[17] << 16 | header[18] << 8 | header[19];
        height = header[20] << 24 | header[21] << 16 | header[22] << 8 | header[23];
        return width > 0 && height > 0;
    }
}
