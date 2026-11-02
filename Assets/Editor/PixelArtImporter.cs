using UnityEditor;
using UnityEngine;

// Imports everything in Assets/Art/Sprites (made by Tools/pixel_art.py) as crisp pixel-art
// sprites: 16 pixels per unit to match the 16 px grid, point filtering, no compression or mips.
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
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
    }
}
