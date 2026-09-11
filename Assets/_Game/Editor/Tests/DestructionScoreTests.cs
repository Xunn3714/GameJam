using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using TMPro;

public sealed class DestructionScoreTests
{
    private const string FenceDefinitionPath =
        "Assets/_Game/Content/Data/World/Obstacles/obstacle.fence.asset";
    private const string LegacyPenFenceDefinitionPath =
        "Assets/_Game/Content/Data/World/Obstacles/obstacle.pen_fence.asset";
    private const string GameplayScenePath =
        "Assets/_Game/Scenes/AlphaFlockExpansion.unity";
    private const string PaperStripSpritePath =
        "Assets/Art/UI/Panels/520_90新羊通知弹窗.png";

    [TestCase(0, 1)]
    [TestCase(1, 1)]
    [TestCase(6, 6)]
    [TestCase(150, 150)]
    public void ScoreComesDirectlyFromRequiredFlockCount(int requiredCount, int expectedScore)
    {
        ObstacleDefinition definition = ScriptableObject.CreateInstance<ObstacleDefinition>();
        try
        {
            SerializedObject serialized = new SerializedObject(definition);
            serialized.FindProperty("requiredFlockCount").intValue = requiredCount;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(definition.DestructionScore, Is.EqualTo(expectedScore));
        }
        finally
        {
            Object.DestroyImmediate(definition);
        }
    }

    [Test]
    public void MultiHitObstacleAwardsOnlyWhenFullyBroken()
    {
        ObstacleDefinition definition = ScriptableObject.CreateInstance<ObstacleDefinition>();
        GameObject obstacleObject = new GameObject("Rock", typeof(SpriteRenderer), typeof(BoxCollider2D));
        BreakableObstacle obstacle = obstacleObject.AddComponent<BreakableObstacle>();
        int brokenEvents = 0;
        System.Action<BreakableObstacle> handler = _ => brokenEvents++;

        try
        {
            SerializedObject serialized = new SerializedObject(definition);
            serialized.FindProperty("requiredFlockCount").intValue = 20;
            serialized.FindProperty("requiredDashHits").intValue = 2;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            obstacle.Configure(definition, obstacleObject.GetComponent<SpriteRenderer>());
            BreakableObstacle.AnyBroken += handler;

            Assert.That(obstacle.Break(), Is.False);
            Assert.That(brokenEvents, Is.Zero);
            Assert.That(obstacle.Break(), Is.True);
            Assert.That(brokenEvents, Is.EqualTo(1));
            Assert.That(obstacle.DestructionScore, Is.EqualTo(20));
        }
        finally
        {
            BreakableObstacle.AnyBroken -= handler;
            Object.DestroyImmediate(obstacleObject);
            Object.DestroyImmediate(definition);
        }
    }

    [Test]
    public void GameplayUsesOneSixSheepFenceDefinition()
    {
        ObstacleDefinition fence = AssetDatabase.LoadAssetAtPath<ObstacleDefinition>(FenceDefinitionPath);

        Assert.That(fence, Is.Not.Null);
        Assert.That(fence.RequiredFlockCount, Is.EqualTo(6));
        Assert.That(fence.DestructionScore, Is.EqualTo(6));
        Assert.That(
            AssetDatabase.LoadAssetAtPath<ObstacleDefinition>(LegacyPenFenceDefinitionPath),
            Is.Null);
    }

    [Test]
    public void GameplaySceneFenceSegmentsUseUnifiedDefinition()
    {
        ObstacleDefinition expected =
            AssetDatabase.LoadAssetAtPath<ObstacleDefinition>(FenceDefinitionPath);
        Scene scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Additive);
        try
        {
            int fenceSegmentCount = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            foreach (BreakableObstacle obstacle in root.GetComponentsInChildren<BreakableObstacle>(true))
            {
                if (obstacle.Definition == null
                    || obstacle.Definition.ObstacleId != "obstacle.fence")
                {
                    continue;
                }

                fenceSegmentCount++;
                Assert.That(obstacle.Definition, Is.SameAs(expected));
                Assert.That(obstacle.DestructionScore, Is.EqualTo(6));
            }

            Assert.That(fenceSegmentCount, Is.GreaterThan(0));
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }

    [Test]
    public void StatusHudPrioritizesFlockCountOverDestructionScore()
    {
        GameObject canvasObject = new GameObject(
            "Canvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        GameObject cameraObject = new GameObject("Camera", typeof(Camera));
        try
        {
            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(1920f, 1080f);
            Sprite paperStrip = AssetDatabase.LoadAssetAtPath<Sprite>(PaperStripSpritePath);
            Assert.That(paperStrip, Is.Not.Null);
            DestructionScoreHudView view =
                DestructionScoreHudView.Create(
                    canvasObject.transform,
                    cameraObject.GetComponent<Camera>(),
                    paperStrip);

            view.SetFlockCount(87);
            view.SetScore(1234);

            RectTransform hudRect = (RectTransform)view.transform;
            TMP_Text flockLabel = view.transform.Find("FlockCount").GetComponent<TMP_Text>();
            Transform scorePanel = view.transform.Find("ScorePanel");
            Assert.That(scorePanel, Is.Not.Null);
            TMP_Text scoreLabel = scorePanel.Find("Score").GetComponent<TMP_Text>();
            Assert.That(hudRect.anchorMin, Is.EqualTo(Vector2.one));
            Assert.That(hudRect.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(view.GetComponent<Image>().sprite, Is.SameAs(paperStrip));
            Assert.That(view.DisplayedFlockCount, Is.EqualTo(87));
            Assert.That(view.DisplayedScore, Is.EqualTo(1234));
            Assert.That(flockLabel.alignment, Is.EqualTo(TextAlignmentOptions.Center));
            Assert.That(scoreLabel.alignment, Is.EqualTo(TextAlignmentOptions.Center));
            Assert.That(hudRect.sizeDelta.x, Is.GreaterThan(((RectTransform)scorePanel).sizeDelta.x));
            Assert.That(flockLabel.fontSize, Is.GreaterThan(scoreLabel.fontSize));
            StringAssert.Contains("87", flockLabel.text);
            StringAssert.Contains("破坏得分", scoreLabel.text);
        }
        finally
        {
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(canvasObject);
        }
    }

    [UnityTest]
    public IEnumerator GameplaySceneStartsWithEmptyDestructionScoreHud()
    {
        EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);

        yield return new EnterPlayMode();
        yield return null;

        DestructionScoreHudView view =
            Object.FindFirstObjectByType<DestructionScoreHudView>();
        FlockController flock = Object.FindFirstObjectByType<FlockController>();
        Assert.That(view, Is.Not.Null);
        Assert.That(flock, Is.Not.Null);
        Assert.That(view.DisplayedFlockCount, Is.EqualTo(flock.MemberCount));
        Assert.That(view.DisplayedScore, Is.Zero);

        yield return new ExitPlayMode();
    }
}
