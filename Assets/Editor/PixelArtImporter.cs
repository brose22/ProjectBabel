using UnityEditor;
using UnityEngine;

// Imports everything in Assets/Art/Sprites (made by Tools/pixel_art.py) as crisp pixel-art
// sprites: 16 pixels per unit to match the 16 px grid, point filtering, no compression or mips.
public class PixelArtImporter : AssetPostprocessor
{
    const string Folder = "Assets/Art/Sprites/";
    const string IconFolder = "Assets/Art/Sprites/Icons/Controller/";
    public const int PixelsPerUnit = 16;

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Folder)) return;
        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        // The controller icons are larger than they are shown; PadIconLibraryBuilder filters those.
        if (!assetPath.StartsWith(IconFolder))
        {
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
        }
        importer.wrapMode = TextureWrapMode.Clamp;
    }
}
