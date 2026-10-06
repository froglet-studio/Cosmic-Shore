using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Content.Serialization;
using CosmicShore.Content.Yaml;
using CosmicShore.Engine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The writer that sends trained sessions back into the Unity project. The golden check
    /// (the seven AI Training assets round-trip byte-identical) needs the bleeding-edge tree;
    /// this pins the format rules that check proved, on a type of its own.
    /// </summary>
    public class UnityYamlWriterTests
    {
        public enum Tier { Low = 1, High = 7 }

        [Serializable]
        public class Ring
        {
            [SerializeField] int head;
            [SerializeField] float[] values;
            public Ring(int capacity) { values = new float[capacity]; head = capacity; } // no parameterless ctor
            public int Head => head;
            public float[] Values => values;
        }

        public class Base : ScriptableObject { public int baseValue = 3; }

        public class Sample : Base
        {
            public List<int> ints = new();
            public List<float> floats = new();
            [SerializeField] string stamp;
            public string text;
            public Tier tier;
            public bool flag;
            public float tiny;
            public List<int> empty = new();
            public Ring ring;
            [NonSerialized] public int skipped = 9;
            public string Stamp { get => stamp; set => stamp = value; }
        }

        const string Header =
            "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n" +
            "  m_Script: {fileID: 11500000, guid: 0123456789abcdef0123456789abcdef, type: 3}\n  m_Name: Sample\n  m_EditorClassIdentifier: \n";

        static string Write(Sample s) => UnityYamlWriter.WriteScriptableObject(s, Header + "  stale: 1\n");

        [Fact]
        public void WritesEditorFormat()
        {
            var s = new Sample
            {
                ints = { 1, 2, -1 },
                floats = { 0.5f, 2f },
                Stamp = "2026-09-29T15:08:32Z",
                text = "café → ok",
                tier = Tier.High,
                flag = true,
                tiny = 1e-7f,
            };
            string yaml = Write(s);
            Assert.StartsWith(Header, yaml);
            Assert.DoesNotContain("stale", yaml);           // the body is regenerated, not copied
            Assert.DoesNotContain("skipped", yaml);
            var body = yaml.Substring(Header.Length).Split('\n');
            Assert.Equal("  baseValue: 3", body[0]);        // base class first
            Assert.Equal("  ints: 0100000002000000ffffffff", body[1]);
            Assert.Equal("  floats:", body[2]);
            Assert.Equal("  - 0.5", body[3]);
            Assert.Equal("  - 2", body[4]);
            Assert.Contains("  stamp: 2026-09-29T15:08:32Z\n", yaml); // a colon without a space stays plain
            Assert.Contains("  text: \"caf\\xE9 \\u2192 ok\"\n", yaml);
            Assert.Contains("  tier: 7\n", yaml);
            Assert.Contains("  flag: 1\n", yaml);
            Assert.Contains("  tiny: 1e-7\n", yaml);
            Assert.Contains("  empty: []\n", yaml);
            // A null serializable class is its default instance, even without a parameterless ctor.
            Assert.Contains("  ring:\n    head: 0\n    values: []\n", yaml);
        }

        [Fact]
        public void RoundTripsThroughTheReader()
        {
            var s = new Sample
            {
                ints = { 5, -3 },
                floats = { 0.25f, -1.5f },
                Stamp = "2026-09-29T15:08:32Z",
                text = "café → \"quoted\"",
                tier = Tier.Low,
                flag = true,
                tiny = 3.5e-8f,
                ring = new Ring(3),
            };
            s.ring.Values[1] = 4.25f;
            string yaml = Write(s);

            var doc = Assert.Single(UnityYaml.ParseDocuments(yaml));
            var back = new Sample { baseValue = 0 };
            new SerializedReader(null).ReadInto(back, doc.Body, null);

            Assert.Equal(3, back.baseValue);
            Assert.Equal(new[] { 5, -3 }, back.ints);
            Assert.Equal(new[] { 0.25f, -1.5f }, back.floats);
            Assert.Equal(s.Stamp, back.Stamp);
            Assert.Equal(s.text, back.text);
            Assert.Equal(Tier.Low, back.tier);
            Assert.True(back.flag);
            Assert.Equal(s.tiny, back.tiny);
            Assert.Empty(back.empty);
            Assert.Equal(3, back.ring.Head);
            Assert.Equal(new[] { 0f, 4.25f, 0f }, back.ring.Values);

            // And the second write is identical to the first: the format is a fixed point.
            Assert.Equal(yaml, Write(back));
        }
    }
}
