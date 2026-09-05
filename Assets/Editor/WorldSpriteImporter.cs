using System;
using UnityEditor;
using UnityEngine;

/// 本文件用于自动设定世界美术图片文件的像素及格式。
public class WorldSpriteImporter : AssetPostprocessor
{
    private const string WorldArtPath = "Assets/Art/";
    private const string SheepSpritePath = WorldArtPath + "SheepSprites/";
    private const string PoopSpritePath = WorldArtPath + "SkillSprites/Poop/";
    private const string HandDrawnPoopPath = PoopSpritePath + "shit.png";
    private const float WorldPixelsPerUnit = 128f;
    private const float HandDrawnPoopPixelsPerUnit = 200f;

    void OnPreprocessTexture()
    {
        bool isSpecialSheep = assetPath.StartsWith(
            SpecialSheepCatalog.DefaultSourceRootFolder.TrimEnd('/', '\\') + "/",
            StringComparison.OrdinalIgnoreCase);
        if (!assetPath.StartsWith(WorldArtPath, StringComparison.OrdinalIgnoreCase) && !isSpecialSheep) return;

        var importer = (TextureImporter)assetImporter;
        importer.textureType = TextureImporterType.Sprite;
        // 保留已切片的 Multiple Sprite；强制改为 Single 会让 Tile/Palette 中的子 Sprite fileID 失效。
        if (isSpecialSheep || importer.spriteImportMode != SpriteImportMode.Multiple)
            importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Bilinear;
        importer.spritePixelsPerUnit = ResolvePixelsPerUnit(importer);

        importer.textureCompression = TextureImporterCompression.Compressed;
        importer.mipmapEnabled = false; // 2D 游戏一般不需要 mipmap，除非会做大幅缩放远近效果
        if (isSpecialSheep)
        {
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(settings);
        }
    }

    private float ResolvePixelsPerUnit(TextureImporter importer)
    {
        if (string.Equals(assetPath, HandDrawnPoopPath, StringComparison.OrdinalIgnoreCase))
        {
            // 原图保留了较宽的透明画布，用主体像素范围匹配稀有大便的显示尺寸。
            return HandDrawnPoopPixelsPerUnit;
        }

        bool normalizeCanvasWidth =
            assetPath.StartsWith(SheepSpritePath, StringComparison.OrdinalIgnoreCase) ||
            assetPath.StartsWith(PoopSpritePath, StringComparison.OrdinalIgnoreCase) ||
            assetPath.StartsWith(
                SpecialSheepCatalog.DefaultSourceRootFolder.TrimEnd('/', '\\') + "/",
                StringComparison.OrdinalIgnoreCase);
        if (!normalizeCanvasWidth)
        {
            return WorldPixelsPerUnit;
        }

        // 角色和技能原画分辨率并不统一，让画布宽度恒为 1 世界单位。
        importer.GetSourceTextureWidthAndHeight(out int sourceWidth, out _);
        return Mathf.Max(1f, sourceWidth);
    }
}
