using System;
using UnityEditor;
using UnityEngine;

/// 本文件用于自动设定世界美术图片文件的像素及格式。
public class WorldSpriteImporter : AssetPostprocessor
{
    private const string WorldArtPath = "Assets/Art/";
    private const string SheepSpritePath = WorldArtPath + "SheepSprites/";
    private const float WorldPixelsPerUnit = 128f;

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(WorldArtPath, StringComparison.OrdinalIgnoreCase)) return;

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Bilinear;
        importer.spritePixelsPerUnit = ResolvePixelsPerUnit(importer);

        importer.textureCompression = TextureImporterCompression.Compressed;
        importer.mipmapEnabled = false; // 2D 游戏一般不需要 mipmap，除非会做大幅缩放远近效果
    }

    private float ResolvePixelsPerUnit(TextureImporter importer)
    {
        if (!assetPath.StartsWith(SheepSpritePath, StringComparison.OrdinalIgnoreCase))
        {
            return WorldPixelsPerUnit;
        }

        // 羊的原画分辨率并不统一。让画布宽度恒为 1 世界单位，
        // 保持它与既有 1 单位占位羊及约 1 单位的碰撞体一致。
        importer.GetSourceTextureWidthAndHeight(out int sourceWidth, out _);
        return Mathf.Max(1f, sourceWidth);
    }
}
