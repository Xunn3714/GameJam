using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class MapLayoutTests
{
    private static readonly Rect World = new Rect(-120f, -70f, 240f, 140f);
    private readonly List<MapBlockDefinition> created = new List<MapBlockDefinition>();

    [TearDown]
    public void TearDown()
    {
        foreach (MapBlockDefinition definition in created)
            Object.DestroyImmediate(definition);
        created.Clear();
    }

    [Test]
    public void SixCellsTileTheWorldRectExactly()
    {
        MapCell[] cells = MapLayoutBuilder.Assign(new System.Random(1), 3, 2, World, FullPool(), 1);

        Assert.AreEqual(6, cells.Length);
        foreach (MapCell cell in cells)
        {
            Assert.AreEqual(80f, cell.Rect.width, 0.001f);
            Assert.AreEqual(70f, cell.Rect.height, 0.001f);
            Assert.IsTrue(World.xMin <= cell.Rect.xMin && cell.Rect.xMax <= World.xMax + 0.001f);
            Assert.IsTrue(World.yMin <= cell.Rect.yMin && cell.Rect.yMax <= World.yMax + 0.001f);
        }

        for (int a = 0; a < cells.Length; a++)
        for (int b = a + 1; b < cells.Length; b++)
            Assert.IsFalse(Shrink(cells[a].Rect).Overlaps(Shrink(cells[b].Rect)), $"cell {a} overlaps cell {b}");

        Assert.AreEqual(World.width * World.height, cells.Sum(cell => cell.Rect.width * cell.Rect.height), 0.01f);
    }

    [Test]
    public void FixedRolesAppearExactlyOnceAndExitEdgeIsOnTheBoundary()
    {
        for (int seed = 1; seed <= 50; seed++)
        {
            MapCell[] cells = MapLayoutBuilder.Assign(new System.Random(seed), 3, 2, World, FullPool(), 1);
            Assert.AreEqual(1, cells.Count(cell => cell.Role == MapBlockRole.Spawn), $"seed {seed}");
            Assert.AreEqual(1, cells.Count(cell => cell.Role == MapBlockRole.Pagoda), $"seed {seed}");
            Assert.AreEqual(1, cells.Count(cell => cell.Role == MapBlockRole.Exit), $"seed {seed}");
            Assert.IsTrue(cells.All(cell => cell.Definition != null), $"seed {seed}: empty cell");

            MapCell exit = cells.Single(cell => cell.Role == MapBlockRole.Exit);
            Assert.IsTrue(exit.ExitEdge.HasValue, $"seed {seed}: exit without edge");
            bool onBoundary = exit.ExitEdge switch
            {
                MapEdge.Top => Mathf.Approximately(exit.Rect.yMax, World.yMax),
                MapEdge.Bottom => Mathf.Approximately(exit.Rect.yMin, World.yMin),
                MapEdge.Left => Mathf.Approximately(exit.Rect.xMin, World.xMin),
                MapEdge.Right => Mathf.Approximately(exit.Rect.xMax, World.xMax),
                _ => false,
            };
            Assert.IsTrue(onBoundary, $"seed {seed}: exit edge {exit.ExitEdge} is not on the map boundary");
            Assert.IsTrue(cells.Where(cell => cell != exit).All(cell => !cell.ExitEdge.HasValue));
        }
    }

    [Test]
    public void RandomSlotsAlwaysIncludeAtLeastOneVillage()
    {
        for (int seed = 1; seed <= 200; seed++)
        {
            MapCell[] cells = MapLayoutBuilder.Assign(new System.Random(seed), 3, 2, World, FullPool(), 1);
            Assert.GreaterOrEqual(cells.Count(cell => cell.Role == MapBlockRole.Village), 1, $"seed {seed}");
            Assert.IsTrue(
                cells.All(cell => MapBlockDefinition.IsFixed(cell.Role)
                    || cell.Role == MapBlockRole.Forest
                    || cell.Role == MapBlockRole.Plains
                    || cell.Role == MapBlockRole.Village),
                $"seed {seed}: unexpected role");
        }
    }

    [Test]
    public void SameSeedProducesSameLayout()
    {
        List<MapBlockDefinition> pool = FullPool();
        MapCell[] first = MapLayoutBuilder.Assign(new System.Random(42), 3, 2, World, pool, 1);
        MapCell[] second = MapLayoutBuilder.Assign(new System.Random(42), 3, 2, World, pool, 1);

        for (int index = 0; index < first.Length; index++)
        {
            Assert.AreEqual(first[index].Definition.BlockId, second[index].Definition.BlockId, $"cell {index}");
            Assert.AreEqual(first[index].ExitEdge, second[index].ExitEdge, $"cell {index}");
        }
    }

    [Test]
    public void ZeroWeightBlocksAreNeverDrawn()
    {
        List<MapBlockDefinition> pool = new List<MapBlockDefinition>
        {
            Block("spawn", MapBlockRole.Spawn, 0f),
            Block("pagoda", MapBlockRole.Pagoda, 0f),
            Block("exit", MapBlockRole.Exit, 0f),
            Block("forest_off", MapBlockRole.Forest, 0f),
            Block("plains", MapBlockRole.Plains, 1f),
            Block("village", MapBlockRole.Village, 1f),
        };

        for (int seed = 1; seed <= 100; seed++)
        {
            MapCell[] cells = MapLayoutBuilder.Assign(new System.Random(seed), 3, 2, World, pool, 1);
            Assert.IsFalse(cells.Any(cell => cell.Role == MapBlockRole.Forest), $"seed {seed}");
        }
    }

    [Test]
    public void MissingFixedDefinitionsLeaveCellsEmptyInsteadOfThrowing()
    {
        List<MapBlockDefinition> pool = new List<MapBlockDefinition>
        {
            Block("plains", MapBlockRole.Plains, 1f),
        };

        MapCell[] cells = MapLayoutBuilder.Assign(new System.Random(7), 3, 2, World, pool, 1);
        Assert.AreEqual(6, cells.Length);
        Assert.AreEqual(3, cells.Count(cell => cell.Definition == null));
        Assert.AreEqual(3, cells.Count(cell => cell.Role == MapBlockRole.Plains && cell.Definition != null));
    }

    private List<MapBlockDefinition> FullPool() => new List<MapBlockDefinition>
    {
        Block("spawn", MapBlockRole.Spawn, 0f),
        Block("pagoda", MapBlockRole.Pagoda, 0f),
        Block("exit", MapBlockRole.Exit, 0f),
        Block("forest_a", MapBlockRole.Forest, 1f),
        Block("forest_b", MapBlockRole.Forest, 1f),
        Block("plains_a", MapBlockRole.Plains, 1f),
        Block("plains_b", MapBlockRole.Plains, 1f),
        Block("village_a", MapBlockRole.Village, 1f),
        Block("village_b", MapBlockRole.Village, 1f),
    };

    private MapBlockDefinition Block(string id, MapBlockRole role, float weight)
    {
        MapBlockDefinition definition = ScriptableObject.CreateInstance<MapBlockDefinition>();
        SerializedObject serialized = new SerializedObject(definition);
        serialized.FindProperty("blockId").stringValue = id;
        serialized.FindProperty("displayName").stringValue = id;
        serialized.FindProperty("role").enumValueIndex = (int)role;
        serialized.FindProperty("weight").floatValue = weight;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        created.Add(definition);
        return definition;
    }

    private static Rect Shrink(Rect rect)
    {
        return new Rect(rect.xMin + 0.01f, rect.yMin + 0.01f, rect.width - 0.02f, rect.height - 0.02f);
    }
}
