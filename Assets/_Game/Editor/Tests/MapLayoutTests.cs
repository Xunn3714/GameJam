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
        MapCell[] cells = MapLayoutBuilder.Assign(new System.Random(1), 3, 2, World, FullPool());

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
    public void EveryRoleAppearsExactlyOnceAndExitEdgeIsOnTheBoundary()
    {
        for (int seed = 1; seed <= 50; seed++)
        {
            MapCell[] cells = MapLayoutBuilder.Assign(new System.Random(seed), 3, 2, World, FullPool());
            Assert.IsTrue(cells.All(cell => cell.Definition != null), $"seed {seed}: empty cell");
            Assert.AreEqual(1, cells.Count(cell => cell.Role == MapBlockRole.Spawn), $"seed {seed}: spawn");
            Assert.AreEqual(1, cells.Count(cell => cell.Role == MapBlockRole.Exit), $"seed {seed}: exit");
            foreach (MapBlockRole role in MapLayoutBuilder.RequiredRoles)
                Assert.GreaterOrEqual(cells.Count(cell => cell.Role == role), 1, $"seed {seed}: {role}");
            Assert.IsTrue(cells.All(cell => MapLayoutBuilder.RequiredRoles.Contains(cell.Role)), $"seed {seed}: unexpected role");

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
    public void ConfigurableGridKeepsExitOnOuterBoundary()
    {
        Rect largerWorld = new Rect(-150f, -150f, 300f, 300f);
        for (int seed = 0; seed < 200; seed++)
        {
            MapCell[] cells = MapLayoutBuilder.Assign(new System.Random(seed), 3, 3, largerWorld, FullPool());
            MapCell exit = cells.Single(cell => cell.Role == MapBlockRole.Exit);

            Assert.IsTrue(exit.ExitEdge.HasValue, $"seed {seed}: exit without edge");
            Assert.IsTrue(
                exit.Column == 0 || exit.Column == 2 || exit.Row == 0 || exit.Row == 2,
                $"seed {seed}: exit was assigned to interior cell ({exit.Column}, {exit.Row})");
        }
    }

    [Test]
    public void NullPoolLeavesDefinitionsEmptyInsteadOfThrowing()
    {
        MapCell[] cells = null;
        Assert.DoesNotThrow(() =>
            cells = MapLayoutBuilder.Assign(new System.Random(5), 3, 3, World, null));
        Assert.That(cells, Has.Length.EqualTo(9));
        Assert.IsTrue(cells.All(cell => cell.Definition == null));
    }

    [Test]
    public void PagodaLandsOnExactlyOneNonSpawnCell()
    {
        for (int seed = 1; seed <= 100; seed++)
        {
            MapCell[] cells = MapLayoutBuilder.Assign(new System.Random(seed), 3, 2, World, FullPool());
            MapCell pagoda = cells.Single(cell => cell.HasPagoda);
            Assert.AreNotEqual(MapBlockRole.Spawn, pagoda.Role, $"seed {seed}");
            Assert.AreNotEqual(MapBlockRole.Exit, pagoda.Role, $"seed {seed}");
        }
    }

    [Test]
    public void SameSeedProducesSameLayout()
    {
        List<MapBlockDefinition> pool = FullPool();
        MapCell[] first = MapLayoutBuilder.Assign(new System.Random(42), 3, 2, World, pool);
        MapCell[] second = MapLayoutBuilder.Assign(new System.Random(42), 3, 2, World, pool);

        for (int index = 0; index < first.Length; index++)
        {
            Assert.AreEqual(first[index].Definition.BlockId, second[index].Definition.BlockId, $"cell {index}");
            Assert.AreEqual(first[index].ExitEdge, second[index].ExitEdge, $"cell {index}");
            Assert.AreEqual(first[index].HasPagoda, second[index].HasPagoda, $"cell {index}");
        }
    }

    [Test]
    public void ZeroWeightVariantsAreNeverDrawnWhenAnotherVariantExists()
    {
        List<MapBlockDefinition> pool = FullPool();
        pool.Add(Block("forest_off", MapBlockRole.Forest, 0f));

        for (int seed = 1; seed <= 100; seed++)
        {
            MapCell[] cells = MapLayoutBuilder.Assign(new System.Random(seed), 3, 2, World, pool);
            Assert.IsFalse(cells.Any(cell => cell.Definition.BlockId == "forest_off"), $"seed {seed}");
        }
    }

    [Test]
    public void MissingRoleDefinitionsLeaveCellsEmptyInsteadOfThrowing()
    {
        List<MapBlockDefinition> pool = new List<MapBlockDefinition>
        {
            Block("plains", MapBlockRole.Plains, 1f),
        };

        MapCell[] cells = MapLayoutBuilder.Assign(new System.Random(7), 3, 2, World, pool);
        Assert.AreEqual(6, cells.Length);
        // 只有平原有定义：其余角色的格子留空，第六格补位可能也是平原。
        Assert.GreaterOrEqual(cells.Count(cell => cell.Definition == null), 4);
        Assert.IsTrue(cells.Where(cell => cell.Definition != null).All(cell => cell.Role == MapBlockRole.Plains));
    }

    private List<MapBlockDefinition> FullPool() => new List<MapBlockDefinition>
    {
        Block("spawn", MapBlockRole.Spawn, 1f),
        Block("exit", MapBlockRole.Exit, 1f),
        Block("forest_a", MapBlockRole.Forest, 1f),
        Block("forest_b", MapBlockRole.Forest, 1f),
        Block("plains_a", MapBlockRole.Plains, 1f),
        Block("plains_b", MapBlockRole.Plains, 1f),
        Block("village_small", MapBlockRole.Village, 1f),
        Block("village_big", MapBlockRole.Village, 1f),
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
