using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public sealed class AlphaUiIntegrationTests
{
    private const string AlphaScenePath =
        "Assets/_Game/Scenes/AlphaFlockExpansion.unity";
    private const string PauseSystemPrefabPath =
        "Assets/_Game/Content/Perfabs/UI/PauseSystem.prefab";

    [Test]
    public void PauseSystemPrefabHasOneConfiguredManager()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PauseSystemPrefabPath);
        Assert.That(prefab, Is.Not.Null);

        PauseManager[] managers = prefab.GetComponentsInChildren<PauseManager>(true);
        Assert.That(managers, Has.Length.EqualTo(1));

        SerializedObject serialized = new SerializedObject(managers[0]);
        string[] requiredReferences =
        {
            "pausePanel",
            "pauseWindow",
            "settingPanel",
            "collectionPanel",
            "continueButton",
            "mainMenuButton",
            "settingsButton",
            "collectionButton",
            "restartButton",
            "exitButton",
            "settingsBackButton",
            "collectionBackButton"
        };

        foreach (string propertyName in requiredReferences)
        {
            Object value = serialized.FindProperty(propertyName).objectReferenceValue;
            Assert.That(value, Is.Not.Null, $"PauseManager.{propertyName} is not configured.");
        }
    }

    [Test]
    public void PauseManagerCoordinatesPauseSettingsAndCollectionPanels()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PauseSystemPrefabPath);
        GameObject instance = Object.Instantiate(prefab);
        try
        {
            PauseManager manager = instance.GetComponent<PauseManager>();
            SerializedObject serialized = new SerializedObject(manager);
            GameObject pausePanel =
                (GameObject)serialized.FindProperty("pausePanel").objectReferenceValue;
            GameObject pauseWindow =
                (GameObject)serialized.FindProperty("pauseWindow").objectReferenceValue;
            GameObject settingPanel =
                (GameObject)serialized.FindProperty("settingPanel").objectReferenceValue;
            GameObject collectionPanel =
                (GameObject)serialized.FindProperty("collectionPanel").objectReferenceValue;

            manager.OpenPause();
            Assert.That(manager.IsPaused, Is.True);
            Assert.That(pausePanel.activeSelf, Is.True);
            Assert.That(Time.timeScale, Is.Zero);

            manager.ShowSettings();
            Assert.That(settingPanel.activeSelf, Is.True);
            Assert.That(pauseWindow.activeSelf, Is.False);

            manager.BackToPause();
            manager.ShowCollection();
            Assert.That(collectionPanel.activeSelf, Is.True);
            Assert.That(pauseWindow.activeSelf, Is.False);

            manager.BackFromCollection();
            Assert.That(collectionPanel.activeSelf, Is.False);
            Assert.That(pauseWindow.activeSelf, Is.True);

            manager.ResumeGame();
            Assert.That(manager.IsPaused, Is.False);
            Assert.That(pausePanel.activeSelf, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));

            manager.SetResultLocked(true);
            manager.OpenPause();
            Assert.That(manager.IsPaused, Is.False);
            Assert.That(pausePanel.activeSelf, Is.False);
        }
        finally
        {
            Time.timeScale = 1f;
            Object.DestroyImmediate(instance);
        }
    }

    [Test]
    public void AlphaSceneKeepsMainGameplayAndUsesSingleUiStack()
    {
        Scene scene = EditorSceneManager.OpenScene(AlphaScenePath, OpenSceneMode.Additive);
        try
        {
            GameObject[] objects = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Select(transform => transform.gameObject)
                .ToArray();

            Assert.That(objects.SelectMany(item => item.GetComponents<PauseManager>()).ToArray(),
                Has.Length.EqualTo(1));
            Assert.That(objects.SelectMany(item => item.GetComponents<EventSystem>()).ToArray(),
                Has.Length.EqualTo(1));
            Assert.That(objects.SelectMany(item => item.GetComponents<FlockActionController>()).ToArray(),
                Has.Length.EqualTo(1));
            Assert.That(objects.SelectMany(item => item.GetComponents<TaskPanelToggle>()).ToArray(),
                Has.Length.EqualTo(1));

            AlphaFlockExpansionController controller = objects
                .Select(item => item.GetComponent<AlphaFlockExpansionController>())
                .FirstOrDefault(item => item != null);
            Assert.That(controller, Is.Not.Null);

            SerializedObject controllerData = new SerializedObject(controller);
            Assert.That(controllerData.FindProperty("flockActions").objectReferenceValue, Is.Not.Null);
            Assert.That(controllerData.FindProperty("pauseManager").objectReferenceValue, Is.Not.Null);
            Assert.That(controllerData.FindProperty("bannerView").objectReferenceValue, Is.Not.Null);

            MvpHudView hud = objects
                .Select(item => item.GetComponent<MvpHudView>())
                .FirstOrDefault(item => item != null);
            Assert.That(hud, Is.Not.Null);
            SerializedObject hudData = new SerializedObject(hud);
            Assert.That(hudData.FindProperty("taskChecklistView").objectReferenceValue, Is.Not.Null);

            PauseManager pauseManager = objects
                .Select(item => item.GetComponent<PauseManager>())
                .FirstOrDefault(item => item != null);
            SerializedObject pauseData = new SerializedObject(pauseManager);
            Assert.That(pauseData.FindProperty("taskPanelToggle").objectReferenceValue, Is.Not.Null);
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
        }
    }
}
