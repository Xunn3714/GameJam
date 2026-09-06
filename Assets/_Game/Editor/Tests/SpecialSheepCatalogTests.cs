using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class SpecialSheepCatalogTests
{
    private SpecialSheepCatalog catalog;

    [SetUp]
    public void SetUp()
    {
        catalog = SpecialSheepCatalogEditorUtility.LoadOrCreateAndSynchronize();
        Assert.IsNotNull(catalog);
    }

    [Test]
    public void DefaultFolderProbabilitiesLeaveTheRemainderForCommonSheep()
    {
        Assert.IsNotNull(catalog.BaseSheepPrefab);
        Assert.That(catalog.GetProbabilityPercent(SheepQuality.Green), Is.EqualTo(20f).Within(0.0001f));
        Assert.That(catalog.GetProbabilityPercent(SheepQuality.Blue), Is.EqualTo(5f).Within(0.0001f));
        Assert.That(catalog.GetProbabilityPercent(SheepQuality.Purple), Is.EqualTo(1f).Within(0.0001f));
        Assert.That(catalog.GetProbabilityPercent(SheepQuality.Gold), Is.EqualTo(0.3f).Within(0.0001f));
        Assert.That(catalog.GetProbabilityPercent(SheepQuality.EasterEgg), Is.EqualTo(0.1f).Within(0.0001f));
        Assert.That(catalog.CommonProbabilityPercent, Is.EqualTo(73.6f).Within(0.0001f));
    }

    [Test]
    public void GrassSheepQualityComesFromItsGreenFolder()
    {
        SpecialSheepCatalog.Tier greenTier = null;
        foreach (SpecialSheepCatalog.Tier tier in catalog.Tiers)
        {
            if (tier != null && tier.Quality == SheepQuality.Green)
            {
                greenTier = tier;
                break;
            }
        }

        Assert.IsNotNull(greenTier);
        Assert.IsTrue(
            ContainsDisplayName(greenTier.Entries, "草羊"),
            "草羊.png 位于绿色羊文件夹，因此必须登记为 Green。"
        );
    }

    [Test]
    public void GeneratedNameAndDescriptionFollowAFileRenameButCustomTextIsPreserved()
    {
        SpecialSheepCatalog.Entry generated = new();
        generated.EditorBindImportedSprite("guid", "sheep.special.guid", "旧名字", null);
        generated.EditorBindImportedSprite("guid", "sheep.special.guid", "新名字", null);
        Assert.AreEqual("新名字", generated.DisplayName);
        Assert.AreEqual("一只独特的新名字。", generated.CodexDescription);

        SpecialSheepCatalog.Entry customized = new();
        customized.EditorBindImportedSprite("guid", "sheep.special.guid", "文件名", null);
        SerializedObject serialized = new(ScriptableObject.CreateInstance<EntryHolder>());
        try
        {
            EntryHolder holder = (EntryHolder)serialized.targetObject;
            holder.Entry = customized;
            serialized.Update();
            SerializedProperty entry = serialized.FindProperty("entry");
            entry.FindPropertyRelative("displayName").stringValue = "自定义名字";
            entry.FindPropertyRelative("codexDescription").stringValue = "自定义描述";
            serialized.ApplyModifiedPropertiesWithoutUndo();

            customized.EditorBindImportedSprite("guid", "sheep.special.guid", "改名后的文件", null);
            Assert.AreEqual("自定义名字", customized.DisplayName);
            Assert.AreEqual("自定义描述", customized.CodexDescription);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(serialized.targetObject);
        }
    }

    [TestCase(0.05d, SheepQuality.EasterEgg)]
    [TestCase(0.2d, SheepQuality.Gold)]
    [TestCase(0.8d, SheepQuality.Purple)]
    [TestCase(2d, SheepQuality.Blue)]
    [TestCase(10d, SheepQuality.Green)]
    [TestCase(80d, SheepQuality.Common)]
    public void QualityRollUsesOneAbsoluteRollPerGroup(double roll, SheepQuality expected)
    {
        Assert.AreEqual(expected, catalog.RollQuality(roll));
    }

    [Test]
    public void CatalogContainsEveryDirectPngAndIgnoresNestedEffectPngs()
    {
        HashSet<string> catalogPaths = new(StringComparer.OrdinalIgnoreCase);
        int expectedDirectPngCount = 0;

        foreach (SpecialSheepCatalog.Tier tier in catalog.Tiers)
        {
            string folder = $"{catalog.SourceRootFolder}/{tier.FolderName}";
            if (AssetDatabase.IsValidFolder(folder))
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
                    if (string.Equals(parent, folder, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase))
                    {
                        expectedDirectPngCount++;
                    }
                }
            }

            foreach (SpecialSheepCatalog.Entry entry in tier.Entries)
            {
                if (entry?.Sprite == null)
                    continue;
                catalogPaths.Add(AssetDatabase.GetAssetPath(entry.Sprite));
            }
        }

        Assert.AreEqual(expectedDirectPngCount, catalogPaths.Count);
        foreach (string path in catalogPaths)
        {
            string relative = path.Substring(catalog.SourceRootFolder.Length).TrimStart('/');
            Assert.AreEqual(2, relative.Split('/').Length, $"Nested PNG must not be a sheep entry: {path}");
        }
    }

    [Test]
    public void ExistingFourSpecialSheepKeepTheirStableTypeIds()
    {
        Assert.AreEqual("礼帽羊", catalog.Find("sheep.special.tophat")?.DisplayName);
        Assert.AreEqual("领结羊", catalog.Find("sheep.special.redbow")?.DisplayName);
        Assert.AreEqual("山羊", catalog.Find("sheep.special.horned")?.DisplayName);
        Assert.AreEqual("黑羊", catalog.Find("sheep.special.black")?.DisplayName);
    }

    [Test]
    public void ImportedSpecialSheepUseNormalizedSpriteSettings()
    {
        foreach (SpecialSheepCatalog.Tier tier in catalog.Tiers)
        {
            foreach (SpecialSheepCatalog.Entry entry in tier.Entries)
            {
                if (entry?.Sprite == null)
                    continue;

                string path = AssetDatabase.GetAssetPath(entry.Sprite);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.IsNotNull(importer, path);
                Assert.AreEqual(TextureImporterType.Sprite, importer.textureType, path);
                Assert.AreEqual(SpriteImportMode.Single, importer.spriteImportMode, path);
                importer.GetSourceTextureWidthAndHeight(out int sourceWidth, out _);
                Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(sourceWidth).Within(0.001f), path);
                Assert.IsFalse(importer.mipmapEnabled, path);
            }
        }
    }

    private static bool ContainsDisplayName(
        IReadOnlyList<SpecialSheepCatalog.Entry> entries,
        string displayName)
    {
        foreach (SpecialSheepCatalog.Entry entry in entries)
        {
            if (entry != null && string.Equals(entry.DisplayName, displayName, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private sealed class EntryHolder : ScriptableObject
    {
        [SerializeField] private SpecialSheepCatalog.Entry entry;

        public SpecialSheepCatalog.Entry Entry
        {
            get => entry;
            set => entry = value;
        }
    }
}

public sealed class SpecialSheepRunStateTests
{
    [Test]
    public void ActiveTypeCannotBeActivatedAgain()
    {
        SpecialSheepRunState state = new();
        Assert.IsTrue(state.TryActivate("sheep.special.test"));
        Assert.AreEqual(SpecialSheepRunStatus.Active, state.GetStatus("sheep.special.test"));
        Assert.IsFalse(state.TryActivate("sheep.special.test"));
    }

    [Test]
    public void UncollectedActiveTypeCanReturnToAvailable()
    {
        SpecialSheepRunState state = new();
        state.TryActivate("sheep.special.test");
        Assert.IsTrue(state.ReleaseIfActive("sheep.special.test"));
        Assert.IsTrue(state.IsAvailable("sheep.special.test"));
    }

    [Test]
    public void CollectedTypeCannotBeReleasedOrActivatedAgain()
    {
        SpecialSheepRunState state = new();
        state.TryActivate("sheep.special.test");
        state.MarkCollected("sheep.special.test");

        Assert.AreEqual(SpecialSheepRunStatus.Collected, state.GetStatus("sheep.special.test"));
        Assert.IsFalse(state.ReleaseIfActive("sheep.special.test"));
        Assert.IsFalse(state.TryActivate("sheep.special.test"));
    }
}

public sealed class ProgressiveSheepSpawnerGroupTests
{
    [TestCase(SheepQuality.Common, false)]
    [TestCase(SheepQuality.Green, false)]
    [TestCase(SheepQuality.Blue, false)]
    [TestCase(SheepQuality.Purple, true)]
    [TestCase(SheepQuality.Gold, true)]
    [TestCase(SheepQuality.EasterEgg, false)]
    public void AcquisitionVfxOnlySupportsPurpleAndGold(SheepQuality quality, bool expected)
    {
        Assert.AreEqual(expected, SpecialSheepAcquisitionVfx.SupportsQuality(quality));
    }

    [Test]
    public void RewardReservationsExhaustAvailableColorPurpleAndGoldTypesWithoutDuplicates()
    {
        GameObject spawnerObject = new("TestRewardSpawner");
        SpecialSheepCatalog runtimeCatalog = UnityEngine.Object.Instantiate(
            SpecialSheepCatalogEditorUtility.LoadOrCreateAndSynchronize());

        try
        {
            ProgressiveSheepSpawner spawner = spawnerObject.AddComponent<ProgressiveSheepSpawner>();
            SerializedObject serialized = new(spawner);
            serialized.FindProperty("specialSheepCatalog").objectReferenceValue = runtimeCatalog;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            HashSet<string> expected = new(StringComparer.Ordinal);
            foreach (SpecialSheepCatalog.Tier tier in runtimeCatalog.Tiers)
            {
                if (tier == null
                    || (tier.Quality != SheepQuality.Purple
                        && tier.Quality != SheepQuality.Gold
                        && tier.Quality != SheepQuality.EasterEgg))
                    continue;

                foreach (SpecialSheepCatalog.Entry entry in tier.Entries)
                    if (entry != null && entry.CanSpawn) expected.Add(entry.TypeId);
            }

            Assert.IsNotEmpty(expected);
            HashSet<string> reserved = new(StringComparer.Ordinal);
            while (spawner.TryReserveRewardSpecialGroup(out ProgressiveSheepSpawner.RewardSpecialGroupReservation reservation))
            {
                Assert.IsTrue(reserved.Add(reservation.TypeId), $"重复预留了特殊羊 {reservation.TypeId}");
                Assert.AreEqual(SpecialSheepRunStatus.Active, spawner.GetSpecialSheepStatus(reservation.TypeId));
                Assert.LessOrEqual(reserved.Count, expected.Count);
            }

            CollectionAssert.AreEquivalent(expected, reserved);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(spawnerObject);
            UnityEngine.Object.DestroyImmediate(runtimeCatalog);
        }
    }

    [Test]
    public void RewardReservationPrefersUndiscoveredThenFallsBackToAllEligibleTypes()
    {
        GameObject spawnerObject = new("TestPreferredRewardSpawner");
        SpecialSheepCatalog runtimeCatalog = UnityEngine.Object.Instantiate(
            SpecialSheepCatalogEditorUtility.LoadOrCreateAndSynchronize());

        try
        {
            ProgressiveSheepSpawner spawner = spawnerObject.AddComponent<ProgressiveSheepSpawner>();
            SerializedObject serialized = new(spawner);
            serialized.FindProperty("specialSheepCatalog").objectReferenceValue = runtimeCatalog;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            List<string> eligibleTypeIds = new();
            foreach (SpecialSheepCatalog.Tier tier in runtimeCatalog.Tiers)
            {
                if (tier == null
                    || (tier.Quality != SheepQuality.Purple
                        && tier.Quality != SheepQuality.Gold
                        && tier.Quality != SheepQuality.EasterEgg))
                    continue;

                foreach (SpecialSheepCatalog.Entry entry in tier.Entries)
                    if (entry != null && entry.CanSpawn) eligibleTypeIds.Add(entry.TypeId);
            }

            Assert.GreaterOrEqual(eligibleTypeIds.Count, 2);
            string onlyUndiscoveredTypeId = eligibleTypeIds[0];
            Assert.IsTrue(spawner.TryReserveRewardSpecialGroup(
                typeId => !string.Equals(typeId, onlyUndiscoveredTypeId, StringComparison.Ordinal),
                out ProgressiveSheepSpawner.RewardSpecialGroupReservation preferred));
            Assert.AreEqual(onlyUndiscoveredTypeId, preferred.TypeId);

            Assert.IsTrue(spawner.TryReserveRewardSpecialGroup(
                _ => true,
                out ProgressiveSheepSpawner.RewardSpecialGroupReservation fallback));
            CollectionAssert.Contains(eligibleTypeIds, fallback.TypeId);
            Assert.AreNotEqual(preferred.TypeId, fallback.TypeId);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(spawnerObject);
            UnityEngine.Object.DestroyImmediate(runtimeCatalog);
        }
    }

    [Test]
    public void OneRollAppliesOneSpecialTypeToTheWholeGroupAndNextGroupUsesAnotherType()
    {
        GameObject flockObject = new("TestFlock");
        GameObject prefabObject = new("TestRecruitablePrefab");
        GameObject spawnerObject = new("TestSpawner");
        SpecialSheepCatalog runtimeCatalog = UnityEngine.Object.Instantiate(
            SpecialSheepCatalogEditorUtility.LoadOrCreateAndSynchronize());

        try
        {
            SerializedObject catalogSerialized = new(runtimeCatalog);
            SerializedProperty tiers = catalogSerialized.FindProperty("tiers");
            for (int index = 0; index < tiers.arraySize; index++)
            {
                SerializedProperty tier = tiers.GetArrayElementAtIndex(index);
                SheepQuality quality = (SheepQuality)tier.FindPropertyRelative("quality").enumValueIndex;
                tier.FindPropertyRelative("probabilityPercent").floatValue =
                    quality == SheepQuality.Green ? 100f : 0f;
            }
            catalogSerialized.ApplyModifiedPropertiesWithoutUndo();

            FlockController flock = flockObject.AddComponent<FlockController>();
            prefabObject.AddComponent<SpriteRenderer>();
            RecruitableSheep prefab = prefabObject.AddComponent<RecruitableSheep>();
            ProgressiveSheepSpawner spawner = spawnerObject.AddComponent<ProgressiveSheepSpawner>();

            SerializedObject serialized = new(spawner);
            serialized.FindProperty("flock").objectReferenceValue = flock;
            serialized.FindProperty("specialSheepCatalog").objectReferenceValue = runtimeCatalog;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            runtimeCatalog.EditorSetBaseSheepPrefab(prefab);

            Assert.IsTrue(spawner.SpawnBatch(5, 5));
            RecruitableSheep[] firstAndOnlyGroup = spawner.GetComponentsInChildren<RecruitableSheep>();
            Assert.AreEqual(5, firstAndOnlyGroup.Length);
            string firstTypeId = firstAndOnlyGroup[0].GetComponent<SheepIdentity>().SheepTypeId;
            foreach (RecruitableSheep sheep in firstAndOnlyGroup)
                Assert.AreEqual(firstTypeId, sheep.GetComponent<SheepIdentity>().SheepTypeId);

            Assert.AreEqual(
                SpecialSheepRunStatus.Active,
                spawner.GetSpecialSheepStatus(firstTypeId));

            Assert.IsTrue(spawner.SpawnBatch(5, 5));
            RecruitableSheep[] bothGroups = spawner.GetComponentsInChildren<RecruitableSheep>();
            Assert.AreEqual(10, bothGroups.Length);
            string secondTypeId = bothGroups[5].GetComponent<SheepIdentity>().SheepTypeId;
            Assert.AreNotEqual(firstTypeId, secondTypeId);
            for (int index = 5; index < bothGroups.Length; index++)
                Assert.AreEqual(secondTypeId, bothGroups[index].GetComponent<SheepIdentity>().SheepTypeId);

            Assert.IsTrue(spawner.MarkRecruited(firstAndOnlyGroup[0]));
            Assert.AreEqual(
                SpecialSheepRunStatus.Collected,
                spawner.GetSpecialSheepStatus(firstTypeId));

            Assert.AreEqual(
                9,
                spawner.DespawnWildSheepFartherThan(new Vector2(10000f, 10000f), 1f));
            Assert.AreEqual(
                SpecialSheepRunStatus.Collected,
                spawner.GetSpecialSheepStatus(firstTypeId));
            Assert.AreEqual(
                SpecialSheepRunStatus.Available,
                spawner.GetSpecialSheepStatus(secondTypeId));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(spawnerObject);
            UnityEngine.Object.DestroyImmediate(prefabObject);
            UnityEngine.Object.DestroyImmediate(flockObject);
            UnityEngine.Object.DestroyImmediate(runtimeCatalog);
        }
    }
}
