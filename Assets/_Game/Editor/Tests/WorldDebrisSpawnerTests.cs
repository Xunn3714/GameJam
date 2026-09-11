using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class WorldDebrisSpawnerTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [Test]
    public void SpawnedCountPrunesDestroyedInstances()
    {
        GameObject root = new GameObject("DebrisSpawnerTest");
        GameObject live = new GameObject("LiveDebris");
        GameObject destroyed = new GameObject("DestroyedDebris");
        try
        {
            WorldDebrisSpawner spawner = root.AddComponent<WorldDebrisSpawner>();
            List<GameObject> spawned = GetField<List<GameObject>>(spawner, "spawned");
            spawned.Add(live);
            spawned.Add(destroyed);
            Object.DestroyImmediate(destroyed);

            Assert.That(spawner.SpawnedCount, Is.EqualTo(1));
            Assert.That(spawned, Has.Count.EqualTo(1));
        }
        finally
        {
            Object.DestroyImmediate(live);
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void RegrowDoesNotExceedInitialTargetPopulation()
    {
        GameObject root = new GameObject("DebrisSpawnerTest");
        GameObject layoutObject = new GameObject("LayoutTest");
        GameObject debrisPrefab = new GameObject("DebrisPrefab");
        GameObject liveA = new GameObject("LiveA");
        GameObject liveB = new GameObject("LiveB");
        MapBlockDefinition definition = ScriptableObject.CreateInstance<MapBlockDefinition>();
        try
        {
            debrisPrefab.SetActive(false);
            SerializedObject definitionData = new SerializedObject(definition);
            definitionData.FindProperty("role").intValue = (int)MapBlockRole.Plains;
            SerializedProperty debris = definitionData.FindProperty("debris");
            debris.arraySize = 1;
            debris.GetArrayElementAtIndex(0).FindPropertyRelative("prefab").objectReferenceValue = debrisPrefab;
            debris.GetArrayElementAtIndex(0).FindPropertyRelative("weight").floatValue = 1f;
            debris.GetArrayElementAtIndex(0).FindPropertyRelative("clearance").floatValue = 0.1f;
            definitionData.ApplyModifiedPropertiesWithoutUndo();

            MapCell cell = new MapCell(0, 0, new Rect(20f, 20f, 10f, 10f));
            typeof(MapCell).GetProperty(nameof(MapCell.Definition))
                .GetSetMethod(true)
                .Invoke(cell, new object[] { definition });

            MapLayoutBuilder layout = layoutObject.AddComponent<MapLayoutBuilder>();
            SetField(layout, "cells", new[] { cell });

            WorldDebrisSpawner spawner = root.AddComponent<WorldDebrisSpawner>();
            GameObject debrisRoot = new GameObject("WorldDebris");
            debrisRoot.transform.SetParent(root.transform, false);
            SetField(spawner, "layout", layout);
            SetField(spawner, "area", new Rect(0f, 0f, 100f, 100f));
            SetField(spawner, "borderPadding", 0f);
            SetField(spawner, "regrowRandom", new System.Random(1));
            SetField(spawner, "targetPopulation", 2);
            SetField(spawner, "maximumCount", 520);
            SetField(spawner, "debrisRoot", debrisRoot.transform);
            List<GameObject> spawned = GetField<List<GameObject>>(spawner, "spawned");
            spawned.Add(liveA);
            spawned.Add(liveB);

            spawner.Regrow(10);

            Assert.That(spawner.SpawnedCount, Is.EqualTo(2));
            Assert.That(debrisRoot.transform.childCount, Is.EqualTo(0));
        }
        finally
        {
            Object.DestroyImmediate(definition);
            Object.DestroyImmediate(liveA);
            Object.DestroyImmediate(liveB);
            Object.DestroyImmediate(debrisPrefab);
            Object.DestroyImmediate(layoutObject);
            Object.DestroyImmediate(root);
        }
    }

    private static T GetField<T>(object target, string name)
    {
        return (T)target.GetType().GetField(name, PrivateInstance).GetValue(target);
    }

    private static void SetField(object target, string name, object value)
    {
        target.GetType().GetField(name, PrivateInstance).SetValue(target, value);
    }
}
