using UnityEditor;
using UnityEngine;

/// 本文件用于自动设定Tilemap美术图片文件的像素及格式
public class WorldSpriteImporter : AssetPostprocessor
{
    private const string TargetPath = "Art";
    private const float PixelsPerUnit = 128f;

    void OnPreprocessTexture()
    {
        if (!assetPath.Contains(TargetPath)) return;

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Bilinear;
        importer.spritePixelsPerUnit = PixelsPerUnit;

        importer.textureCompression = TextureImporterCompression.Compressed;
        importer.mipmapEnabled = false; // 2D 游戏一般不需要 mipmap，除非会做大幅缩放远近效果
    }
}
