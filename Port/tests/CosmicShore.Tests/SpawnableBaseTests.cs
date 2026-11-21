using System;
using System.Collections.Generic;
using CosmicShore.Engine;
using CosmicShore.Gameplay;
using Object = CosmicShore.Engine.Object;

namespace CosmicShore.Tests;

// Track-content groundwork: SpawnableBase + SpawnPoint + SpawnTrailData ported
// verbatim. SpawnableBase is the unified pattern-generation/spawning tree —
// SpawnableWaypointTrack (and the rest of the spawnable family) build on it, and
// the CrystalCollisionTurnMonitor deviation restores once the waypoint track ports.
public class SpawnableBaseTests : IDisposable
{
    readonly GameLoop loop = new();
    readonly List<GameObject> spawned = new();

    public void Dispose()
    {
        foreach (var go in spawned)
            if (go) go.SetActive(false);
        loop.Dispose();
    }

    // ── doubles ─────────────────────────────────────────────────────────────

    /// <summary>Three points on a line; hash covers seed + count so cache tests can flip it.</summary>
    class LineSpawnable : SpawnableBase
    {
        public int Count = 3;
        public int GenerateCalls;

        protected override int GetParameterHash() => HashCode.Combine(seed, Count);

        protected override SpawnPoint[] GeneratePoints()
        {
            GenerateCalls++;
            var points = new SpawnPoint[Count];
            for (int i = 0; i < Count; i++)
                points[i] = new SpawnPoint(new Vector3(i * 10f, 0f, 0f));
            return points;
        }
    }

    LineSpawnable MakeLine(int count = 3)
    {
        var go = new GameObject("line-spawnable");
        spawned.Add(go);
        var line = go.AddComponent<LineSpawnable>();
        line.Count = count;
        return line;
    }

    // ── SpawnPoint ──────────────────────────────────────────────────────────

    [Fact]
    public void SpawnPoint_LookRotation_DegenerateDirectionIsIdentity()
    {
        Assert.Equal(Quaternion.identity, SpawnPoint.LookRotation(Vector3.zero, Vector3.up));
        Assert.Equal(Quaternion.identity, SpawnPoint.LookRotation(Vector3.one, Vector3.one, Vector3.up));

        var look = SpawnPoint.LookRotation(Vector3.zero, new Vector3(0f, 0f, 5f), Vector3.up);
        Assert.NotEqual(Quaternion.identity, look * Quaternion.Euler(90f, 0f, 0f)); // sanity: usable rotation
    }

    // ── Cached access ───────────────────────────────────────────────────────

    [Fact]
    public void GetTrailData_CachesUntilParameterHashChanges()
    {
        var line = MakeLine();

        var first = line.GetTrailData();
        var second = line.GetTrailData();
        Assert.Same(first, second);
        Assert.Equal(1, line.GenerateCalls);

        line.Count = 5; // hash changes → regenerate
        var third = line.GetTrailData();
        Assert.NotSame(first, third);
        Assert.Equal(2, line.GenerateCalls);
        Assert.Equal(5, third[0].Points.Length);
    }

    [Fact]
    public void InvalidateCache_ForcesRegeneration()
    {
        var line = MakeLine();
        line.GetTrailData();
        line.InvalidateCache();
        line.GetTrailData();
        Assert.Equal(2, line.GenerateCalls);
    }

    [Fact]
    public void GetSpawnPoints_ReturnsFirstTrailPoints()
    {
        var line = MakeLine(count: 4);
        var points = line.GetSpawnPoints();
        Assert.Equal(4, points.Length);
        Assert.Equal(new Vector3(30f, 0f, 0f), points[3].Position);
    }

    // ── Spawning ────────────────────────────────────────────────────────────

    [Fact]
    public void Spawn_LeafPrefab_InstantiatesObjectAtEachPoint()
    {
        var line = MakeLine();
        var prefab = new GameObject("leaf-prefab");
        spawned.Add(prefab);
        SetField(line, "leafPrefab", prefab);

        var container = line.Spawn();
        spawned.Add(container);

        Assert.Equal(3, container.transform.childCount);
        Assert.Equal(new Vector3(20f, 0f, 0f), container.transform.GetChild(2).position);
    }

    [Fact]
    public void Spawn_WithChildren_PositionsChildContainersAtPoints()
    {
        var parent = MakeLine();
        var child = MakeLine(count: 2);
        var childLeaf = new GameObject("child-leaf");
        spawned.Add(childLeaf);
        SetField(child, "leafPrefab", childLeaf);
        SetField(parent, "children", new List<SpawnableBase> { child });

        var container = parent.Spawn();
        spawned.Add(container);

        // One child container per parent point, each holding the child's leaf spawns.
        Assert.Equal(3, container.transform.childCount);
        Assert.Equal(2, container.transform.GetChild(0).childCount);
    }

    static void SetField(object target, string name, object value)
    {
        for (Type t = target.GetType(); t != null; t = t.BaseType)
        {
            var field = t.GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.DeclaredOnly);
            if (field == null) continue;
            field.SetValue(target, value);
            return;
        }
        throw new InvalidOperationException($"Field '{name}' not found on {target.GetType().Name}.");
    }
}
