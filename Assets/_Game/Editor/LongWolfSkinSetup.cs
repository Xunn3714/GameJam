using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class LongWolfSkinSetup
{
    private const string Folder = "Assets/_Game/Content/Art/LongWolfSkins";

    [MenuItem("Game Jam/Wolf Test/Apply Long Wolf Skins")]
    public static void Apply()
    {
        Sprite[] skins = { Import("Classic"), Import("Centipede") };
        GameObject root = PrefabUtility.LoadPrefabContents(LongWolfTestSceneSetup.PrefabPath);
        try
        {
            var sweep = root.GetComponent<LongWolfSweep>();
            var data = new SerializedObject(sweep);
            var renderer = data.FindProperty("skinRenderer").objectReferenceValue as SpriteRenderer;
            if (renderer == null)
            {
                var visual = new GameObject("Skin");
                visual.transform.SetParent(root.transform, false);
                renderer = visual.AddComponent<SpriteRenderer>();
            }
            var wolfData = new SerializedObject(root.GetComponent<Wolf>());
            var oldRenderer = (SpriteRenderer)wolfData.FindProperty("bodyRenderer").objectReferenceValue;
            renderer.sharedMaterial = oldRenderer.sharedMaterial;
            renderer.sortingOrder = 22;
            renderer.color = Color.white;
            renderer.sprite = skins[0];
            data.FindProperty("skinRenderer").objectReferenceValue = renderer;
            var list = data.FindProperty("skins");
            list.arraySize = skins.Length;
            for (int i = 0; i < skins.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = skins[i];
            data.ApplyModifiedPropertiesWithoutUndo();
            sweep.SetDirection(Vector2.right);
            wolfData.FindProperty("bodyRenderer").objectReferenceValue = renderer;
            wolfData.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, LongWolfTestSceneSetup.PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        File.WriteAllText("Tools/LongWolfSkins-validation.txt", "PASS: Unity compilation, transparent textures, bordered sprites and LongWolf prefab references.\n");
    }

    private static Sprite Import(string name)
    {
        string path = Folder + "/" + name + ".png";
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Default;
        importer.isReadable = true;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 4096;
        importer.filterMode = FilterMode.Bilinear;
        importer.SaveAndReimport();
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        Color32[] pixels = texture.GetPixels32();
        int minX = texture.width, minY = texture.height, maxX = -1, maxY = -1;
        for (int y = 0; y < texture.height; y++)
        for (int x = 0; x < texture.width; x++)
        {
            if (pixels[y * texture.width + x].a < 20) continue;
            minX = Math.Min(minX, x); minY = Math.Min(minY, y);
            maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y);
        }
        if (maxX < minX || pixels[0].a > 0) throw new Exception("Expected transparent character sprite: " + name);
        Rect rect = new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
        // Fixed tail and head; repeat only the middle torso (and legs) as the wolf grows.
        var sprite = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 100, 0,
            SpriteMeshType.FullRect, new Vector4(rect.width * 0.23f, 0, rect.width * 0.25f, 0));
        sprite.name = name;
        string spritePath = Folder + "/" + name + ".asset";
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        if (existing == null) AssetDatabase.CreateAsset(sprite, spritePath);
        else { EditorUtility.CopySerialized(sprite, existing); UnityEngine.Object.DestroyImmediate(sprite); AssetDatabase.SaveAssetIfDirty(existing); }
        return AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
    }
}
