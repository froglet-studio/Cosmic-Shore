using System;
using System.Collections.Generic;
using System.IO;
using CosmicShore.Content;
using CosmicShore.Engine;

namespace CosmicShore.Tests;

/// <summary>
/// Unity's script execution order, from both sources (the [DefaultExecutionOrder] attribute and
/// the per-script executionOrder a project keeps in each script's .meta), applied to a scene
/// load's Awake/OnEnable, to Start, and to the per-frame phases.
/// </summary>
public class ExecutionOrderTests
{
    static List<string>? s_log;

    class Plain : MonoBehaviour
    {
        void Awake() => s_log?.Add("plain.Awake");
        void OnEnable() => s_log?.Add("plain.OnEnable");
        void Start() => s_log?.Add("plain.Start");
        void Update() => s_log?.Add("plain.Update");
    }

    [DefaultExecutionOrder(-100)]
    class Early : MonoBehaviour
    {
        void Awake() => s_log?.Add("early.Awake");
        void OnEnable() => s_log?.Add("early.OnEnable");
        void Start() => s_log?.Add("early.Start");
        void Update() => s_log?.Add("early.Update");
    }

    [DefaultExecutionOrder(-100)]
    class AttributeSaysEarly : MonoBehaviour
    {
        void Update() => s_log?.Add("overridden.Update");
    }

    [Fact]
    public void SceneLoad_AwakeAndOnEnable_FollowExecutionOrder_NotHierarchyOrder()
    {
        using var loop = new GameLoop();
        s_log = new List<string>();
        // The default-order behaviour comes first in the hierarchy; the -100 one must still wake first.
        var first = new GameObject("first"); first.SetActive(false); first.AddComponent<Plain>();
        var second = new GameObject("second"); second.SetActive(false);
        var child = new GameObject("child"); child.transform.SetParent(second.transform, false); child.AddComponent<Early>();

        GameObject.ActivateSceneRoots(new[] { first, second }, null);

        Assert.Equal(new[] { "early.Awake", "early.OnEnable", "plain.Awake", "plain.OnEnable" }, s_log);
        s_log = null;
    }

    [Fact]
    public void SceneLoad_AnAwakeThatDeactivatesALaterObject_KeepsItAsleep()
    {
        using var loop = new GameLoop();
        s_log = new List<string>();
        var victim = new GameObject("victim"); victim.SetActive(false); victim.AddComponent<Plain>();
        var killer = new GameObject("killer"); killer.SetActive(false);
        killer.AddComponent<Early>();
        killer.AddComponent<Deactivator>().Target = victim;

        GameObject.ActivateSceneRoots(new[] { victim, killer }, null);

        Assert.DoesNotContain("plain.Awake", s_log);
        s_log = null;
    }

    [DefaultExecutionOrder(-50)]
    class Deactivator : MonoBehaviour
    {
        public GameObject? Target;
        void Awake() => Target?.SetActive(false);
    }

    [Fact]
    public void Start_RunsInExecutionOrder()
    {
        using var loop = new GameLoop();
        s_log = new List<string>();
        var a = new GameObject("a"); a.AddComponent<Plain>();
        var b = new GameObject("b"); b.AddComponent<Early>();
        s_log.Clear();

        loop.Tick(0.016f);

        Assert.True(s_log.IndexOf("early.Start") < s_log.IndexOf("plain.Start"));
        Assert.True(s_log.IndexOf("early.Update") < s_log.IndexOf("plain.Update"));
        s_log = null;
    }

    [Fact]
    public void MetaExecutionOrder_OverridesTheAttribute()
    {
        using var loop = new GameLoop();
        ScriptExecutionOrder.Set(typeof(AttributeSaysEarly), 500);
        try
        {
            s_log = new List<string>();
            new GameObject("o").AddComponent<AttributeSaysEarly>();
            new GameObject("p").AddComponent<Plain>();
            loop.Tick(0.016f);
            Assert.True(s_log.IndexOf("plain.Update") < s_log.IndexOf("overridden.Update"));
        }
        finally
        {
            ScriptExecutionOrder.Set(typeof(AttributeSaysEarly), 0);
            s_log = null;
        }
    }

    [Fact]
    public void AssetDatabase_ReadsExecutionOrderFromScriptMetas()
    {
        var root = Path.Combine(Path.GetTempPath(), "prisma-exec-" + Guid.NewGuid().ToString("N"));
        var assets = Path.Combine(root, "Assets");
        Directory.CreateDirectory(assets);
        try
        {
            File.WriteAllText(Path.Combine(assets, "Ordered.cs"), "class Ordered {}");
            File.WriteAllText(Path.Combine(assets, "Ordered.cs.meta"),
                "fileFormatVersion: 2\nguid: 0123456789abcdef0123456789abcdef\nMonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: -56\n  icon: {instanceID: 0}\n");
            File.WriteAllText(Path.Combine(assets, "Default.cs"), "class Default {}");
            File.WriteAllText(Path.Combine(assets, "Default.cs.meta"),
                "fileFormatVersion: 2\nguid: fedcba9876543210fedcba9876543210\nMonoImporter:\n  executionOrder: 0\n");

            var orders = new AssetDatabase(root).ScriptExecutionOrders();

            Assert.Equal(-56, orders["0123456789abcdef0123456789abcdef"]);
            Assert.False(orders.ContainsKey("fedcba9876543210fedcba9876543210"));
        }
        finally { Directory.Delete(root, true); }
    }
}
