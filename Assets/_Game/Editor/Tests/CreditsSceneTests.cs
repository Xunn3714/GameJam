using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;

public sealed class CreditsSceneTests
{
    [Test]
    public void CreditsPrefabUsesWorldObjectsAndScrollsCameraDownward()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CreditsSceneSetup.PrefabPath);
        Assert.That(prefab, Is.Not.Null);
        Assert.That(prefab.activeSelf, Is.False);

        CreditsSceneController controller = prefab.GetComponent<CreditsSceneController>();
        Assert.That(controller, Is.Not.Null);
        Assert.That(controller.ScrollingCamera, Is.Not.Null);
        Assert.That(controller.ScrollingCamera.orthographic, Is.True);
        Assert.That(controller.StartMarker, Is.Not.Null);
        Assert.That(controller.EndMarker, Is.Not.Null);
        Assert.That(controller.StartDelay, Is.EqualTo(2.25f));
        Assert.That(controller.StartMarker.position.y, Is.GreaterThan(controller.EndMarker.position.y));
        Assert.That(controller.BackButtonRect, Is.Not.Null);

        TextMeshPro[] worldTexts = prefab.GetComponentsInChildren<TextMeshPro>(true);
        Assert.That(worldTexts.Length, Is.GreaterThanOrEqualTo(34));
        Assert.That(worldTexts.Any(text => text.text.Contains("制作与设计")), Is.True);
        Assert.That(worldTexts.Any(text => text.text.Contains("技术开发")), Is.True);
        Assert.That(worldTexts.Any(text => text.text.Contains("美术设计")), Is.True);
        Assert.That(worldTexts.Any(text => text.text.Contains("音乐与音效")), Is.True);
        Assert.That(worldTexts.Any(text => text.text.Contains("测试与鸣谢")), Is.True);
        Assert.That(worldTexts.Any(text => text.text.Contains("项目统筹")), Is.True);
        Assert.That(worldTexts.Any(text => text.text.Contains("Pkat Studio 出品")), Is.True);
        Assert.That(worldTexts
            .Where(text => text.name.EndsWith("_Names"))
            .All(text => Mathf.Approximately(text.fontSize, 36f)), Is.True);
        Assert.That(worldTexts
            .Where(text => text.name.EndsWith("_Role"))
            .All(text => Mathf.Approximately(text.fontSize, 44f)), Is.True);
        Assert.That(worldTexts.Single(text => text.name == "GameDesignLead_Role").text,
            Is.EqualTo("游戏设计负责人"));
        Assert.That(worldTexts.Single(text => text.name == "GameDesignLead_Names").text,
            Is.EqualTo("KSLJ  ·  PKAT"));
        Assert.That(worldTexts.Single(text => text.name == "TechnicalLead_Names").text,
            Is.EqualTo("DARCY"));
        Assert.That(worldTexts.Single(text => text.name == "ArtLead_Names").text,
            Is.EqualTo("ANKI0_0"));
        Assert.That(worldTexts.Single(text => text.name == "ProjectManager_Names").text,
            Is.EqualTo("DARCY  ·  KSLJ  ·  PKAT  ·  XUNN"));
        Assert.That(worldTexts.Single(text => text.name == "GameDesigner_Names").text,
            Is.EqualTo("DARCY  ·  GAILTY  ·  KSLJ  ·  PKAT"));
        Assert.That(worldTexts.Single(text => text.name == "Programmer_Names").text,
            Is.EqualTo("3WATER  ·  DARCY  ·  JIMMY\nKSLJ  ·  W1K  ·  XUNN"));
        Assert.That(worldTexts.Single(text => text.name == "2DArtist_Names").text,
            Is.EqualTo("ANKI0_0  ·  GAILTY  ·  PKAT  ·  XUNN"));
        Assert.That(worldTexts.Single(text => text.name == "PlayTester_Names").text,
            Is.EqualTo("Pkat Studio 全体成员"));
        Assert.That(worldTexts.Any(text => text.name.StartsWith("Animator")), Is.False);
        Assert.That(worldTexts
            .Any(text => text.text.Contains("感谢游玩")), Is.True);
        TMP_FontAsset creditsFont = prefab.GetComponentsInChildren<TMP_Text>(true)
            .Select(text => text.font)
            .FirstOrDefault(font => AssetDatabase.GetAssetPath(font) == CreditsSceneSetup.FontPath);
        Assert.That(creditsFont, Is.Not.Null);
        Assert.That(creditsFont.atlasPopulationMode, Is.EqualTo(AtlasPopulationMode.Static));
        Assert.That(prefab.GetComponentsInChildren<TextMeshProUGUI>(true)
            .Any(text => text.text.Contains("空格加速")), Is.True);
        Assert.That(prefab.GetComponentsInChildren<CreditsDecorationMotion>(true).Length, Is.GreaterThanOrEqualTo(6));
        Assert.That(prefab.GetComponentsInChildren<EventSystem>(true).Length, Is.EqualTo(0));
    }


    [Test]
    public void CreditsPreviewSceneContainsOneActiveSceneRoot()
    {
        Scene scene = EditorSceneManager.OpenScene(CreditsSceneSetup.PreviewScenePath, OpenSceneMode.Additive);
        try
        {
            CreditsSceneController[] controllers = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<CreditsSceneController>(true))
                .ToArray();
            Assert.That(controllers.Length, Is.EqualTo(1));
            Assert.That(controllers[0].gameObject.activeInHierarchy, Is.True);
            Assert.That(scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<EventSystem>(true))
                .Count(), Is.EqualTo(0));
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }
}
