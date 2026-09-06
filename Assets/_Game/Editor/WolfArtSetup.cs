using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Imports the supplied single-wolf artwork and updates its existing prefab through Unity.</summary>
[InitializeOnLoad]
public static class WolfArtSetup
{
    private const string Folder = "Assets/_Game/Content/Art/Characters/Wolf/";
    private const string Prefab = "Assets/_Game/Content/Perfabs/Wolf/Wolf.prefab";
    private const string Request = "Tools/WolfArt.request";
    private const string Report = "Tools/WolfArt-validation.txt";
    private static readonly string[] Names = { "狼_正面", "狼_下走", "狼_走1", "狼_走2", "狼_张嘴" };
    private static readonly string[] Fields = { "idleSprite", "downSprite", "walkSprite1", "walkSprite2", "attackSprite" };
    private static double nextCheck;

    static WolfArtSetup() => EditorApplication.update += CheckRequest;

    private static void CheckRequest()
    {
        if (EditorApplication.timeSinceStartup < nextCheck) return;
        nextCheck = EditorApplication.timeSinceStartup + 1;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(Request)) return;
        string command = File.ReadAllText(Request).Trim();
        File.Delete(Request);
        if (command != "apply") return;
        try { Apply(); }
        catch (Exception error) { File.WriteAllText(Report, error.ToString()); Debug.LogException(error); }
    }

    [MenuItem("Game Jam/Wolf Test/Apply Single Wolf Artwork")]
    public static void Apply()
    {
        foreach (string name in Names)
        {
            string path = Folder + name + ".png";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 230f;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 1024;
            importer.SaveAndReimport();
        }
        GameObject root = PrefabUtility.LoadPrefabContents(Prefab);
        try
        {
            ApplyTo(root);
            PrefabUtility.SaveAsPrefabAsset(root, Prefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        var saved = new SerializedObject(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab).GetComponent<Wolf>());
        foreach (string field in Fields)
            if (saved.FindProperty(field).objectReferenceValue == null)
                throw new InvalidOperationException("Missing wolf sprite: " + field);
        File.WriteAllText(Report, "PASS: Unity compilation, five PNG sprite imports, prefab save and all five serialized sprite references.\n");
    }

    // Also used by scene setup so rebuilding the prototype cannot replace the artwork with a red circle.
    public static void ApplyTo(GameObject root)
    {
        if (root.GetComponent<LongWolfSweep>() != null) return;
        var wolf = root.GetComponent<Wolf>();
        var serialized = new SerializedObject(wolf);
        Sprite idle = AssetDatabase.LoadAssetAtPath<Sprite>(Folder + Names[0] + ".png");
        if (idle == null) return;
        for (int i = 0; i < Names.Length; i++)
            serialized.FindProperty(Fields[i]).objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(Folder + Names[i] + ".png");
        var renderer = (SpriteRenderer)serialized.FindProperty("bodyRenderer").objectReferenceValue;
        renderer.sprite = idle;
        renderer.color = Color.white;
        renderer.transform.localScale = Vector3.one;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
