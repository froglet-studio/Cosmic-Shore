using System;
using System.Linq;
using CosmicShore.Content.Serialization;
using CosmicShore.Content.Yaml;
using CosmicShore.Engine;

namespace CosmicShore.Tests;

/// <summary>
/// The loader reads script types exactly as Unity's serializer does: what Unity writes arrives
/// (including [field: SerializeField] auto-properties), and what Unity would ignore stays ignored.
/// Found by `cs-asset serialization-audit`: vessel resource levels were dropped, and stale keys
/// were loaded into properties and non-serialized fields.
/// </summary>
public class UnitySerializationRulesTests
{
    class Script : MonoBehaviour, ISerializationCallbackReceiver
    {
        public float publicField;
        [SerializeField] float privateMarked;
        float privateUnmarked;
        [field: SerializeField] public float AutoLevel { get; private set; }
        public float Velocity { get; set; }
        [FormerlySerializedAs("oldName")] public int renamed;
        [NonSerialized] public int skipped;
        public bool afterDeserialize;

        public System.Collections.Generic.Dictionary<string, int> table = new();
        public Plain plainObject;
        public Nested nested;
        public System.Collections.Generic.List<Nested> nestedList = new();
        public int[][] jagged;
        public IShape shape;

        public float PrivateMarked => privateMarked;
        public float PrivateUnmarked => privateUnmarked;
        public void OnBeforeSerialize() { }
        public void OnAfterDeserialize() => afterDeserialize = true;
    }

    interface IShape { }
    public class Plain { public int value; }                 // no [Serializable]: Unity skips the field
    [Serializable] public class Nested { public int value; }

    static Script Read(string body)
    {
        var yaml = "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &1\nMonoBehaviour:\n" + body;
        var doc = Assert.Single(UnityYaml.ParseDocuments(yaml));
        using var loop = new GameLoop();
        var target = new GameObject("t").AddComponent<Script>();
        new SerializedReader(null).ReadInto(target, doc.Body, null);
        return target;
    }

    [Fact]
    public void WhatUnityWrites_Arrives()
    {
        var s = Read("  publicField: 1.5\n  privateMarked: 2.5\n  <AutoLevel>k__BackingField: 0.75\n  oldName: 7\n");
        Assert.Equal(1.5f, s.publicField);
        Assert.Equal(2.5f, s.PrivateMarked);
        Assert.Equal(0.75f, s.AutoLevel);        // was dropped: Prisma skipped every backing field
        Assert.Equal(7, s.renamed);
        Assert.True(s.afterDeserialize);          // was never called
    }

    [Fact]
    public void WhatUnityIgnores_StaysIgnored()
    {
        var s = Read("  privateUnmarked: 9\n  Velocity: 4\n  m_publicField: 3\n  skipped: 5\n");
        Assert.Equal(0f, s.PrivateUnmarked);      // not serialized in Unity
        Assert.Equal(0f, s.Velocity);             // properties never deserialize
        Assert.Equal(0f, s.publicField);          // no m_ spelling for script fields
        Assert.Equal(0, s.skipped);
    }

    [Fact]
    public void BuiltIns_KeepTheirNativeSpellings()
    {
        // Engine built-ins mirror Unity's native layouts through m_ keys and properties.
        Assert.True(SerializedReader.Accepts(typeof(Camera), "m_FieldOfView") || SerializedReader.Accepts(typeof(Camera), "field of view"));
        Assert.False(UnitySerializationRules.IsScriptType(typeof(Camera)));
        Assert.True(UnitySerializationRules.IsScriptType(typeof(Script)));
    }

    [Fact]
    public void FieldsWhoseTypeUnityCannotSerialize_AreSkipped()
    {
        var names = UnitySerializationRules.SerializedFields(typeof(Script)).Select(f => f.Name).ToHashSet();
        Assert.Contains("nested", names);
        Assert.Contains("nestedList", names);
        Assert.DoesNotContain("table", names);        // Dictionary
        Assert.DoesNotContain("plainObject", names);  // a class without [Serializable]
        Assert.DoesNotContain("jagged", names);       // an array of arrays
        Assert.DoesNotContain("shape", names);        // an interface needs [SerializeReference]

        var s = Read("  nested:\n    value: 3\n  plainObject:\n    value: 4\n");
        Assert.Equal(3, s.nested.value);
        Assert.Null(s.plainObject);
    }

    [Fact]
    public void TheRules_ListUnitysFields()
    {
        var names = UnitySerializationRules.SerializedFields(typeof(Script)).Select(f => f.Name).ToHashSet();
        Assert.Contains("publicField", names);
        Assert.Contains("privateMarked", names);
        Assert.Contains("<AutoLevel>k__BackingField", names);
        Assert.Contains("renamed", names);
        Assert.Contains("afterDeserialize", names);
        Assert.DoesNotContain("privateUnmarked", names);
        Assert.DoesNotContain("skipped", names);
    }
}
