using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CosmicShore.Soap;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Our SOAP (Assets/Plugins/CosmicShoreSoap) replaced the Obvious "Soap" plugin on
    /// 2026-11-06 without touching a single asset, prefab or scene. Two things make that
    /// true, and Unity reports neither when it breaks: a script found by a GUID nobody has
    /// any more becomes a "Missing Script", and a YAML key no field receives is dropped
    /// without a word the next time the file is saved (an event wire that quietly stops).
    /// These tests hold both.
    /// </summary>
    public class SoapFormatTests
    {
        // The plugin's script GUIDs, kept in our classes' .meta files.
        static readonly (string guid, Type type)[] PluginGuids =
        {
            ("d93ba61543949824db31c7cb89b4f009", typeof(EventListenerNoParam)),
            ("e64750262007f204b879cd45e18965e5", typeof(EventListenerBool)),
            ("f30bb439e6d7dc94489cd1be18593b0e", typeof(EventListenerInt)),
            ("2be4161beb6babe428f8453728aa3300", typeof(EventListenerString)),
            ("a49b7e678b3ebb7418adb84806d24646", typeof(ScriptableEventNoParam)),
            ("e2fb1c8bab02427087aa07a952ef4018", typeof(ScriptableEventBool)),
            ("ed2a53a13d3e4066b3ccfe94e017a207", typeof(ScriptableEventInt)),
            ("d3f39f579066605409b539400e8d7b94", typeof(ScriptableEventString)),
            ("56cdb8ad39a5bdd4f8ed3d18fae14a63", typeof(IntVariable)),
            ("a9978ce9c8bdc724896114c9952c52ea", typeof(FloatVariable)),
            ("fbb51723e892a404a87b22018981eb5d", typeof(StringVariable)),
        };

        static readonly Regex ScriptRx = new(@"m_Script: \{fileID: 11500000, guid: ([0-9a-f]{32}), type: 3\}");
        static readonly Regex TopKeyRx = new(@"^  ([A-Za-z_]\w*):");
        static readonly Regex ElementKeyRx = new(@"^(?:  - |    )([A-Za-z_]\w*):");

        // A SOAP asset is a few KB; the big .asset files are meshes, fonts and terrain.
        const long LargestAssetScanned = 1 << 20;

        [Test]
        public void PluginScriptGuids_StillNameOurClasses()
        {
            foreach (var (guid, type) in PluginGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                Assert.IsNotEmpty(path, $"No script has the plugin's GUID for {type.Name}: every asset using it is now a Missing Script.");
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                Assert.AreEqual(type, script ? script.GetClass() : null, $"{path} carries {type.Name}'s GUID but holds another class.");
            }
        }

        [Test]
        public void EverySerializedKey_OnASoapScript_HasAFieldToLandIn()
        {
            var fieldsByGuid = SoapScriptFields();
            Assert.That(fieldsByGuid.Count, Is.GreaterThan(40), "Found too few SOAP scripts to trust this scan.");

            var problems = new List<string>();
            int documents = 0;
            foreach (var path in Directory.EnumerateFiles("Assets", "*", SearchOption.AllDirectories))
            {
                bool asset = path.EndsWith(".asset", StringComparison.Ordinal);
                if (!asset && !path.EndsWith(".prefab", StringComparison.Ordinal) && !path.EndsWith(".unity", StringComparison.Ordinal)) continue;
                if (asset && new FileInfo(path).Length > LargestAssetScanned) continue;

                var text = File.ReadAllText(path);
                if (!text.Contains("m_Script:")) continue;
                foreach (var doc in text.Split("\n--- "))
                {
                    var script = ScriptRx.Match(doc);
                    if (!script.Success || !fieldsByGuid.TryGetValue(script.Groups[1].Value, out var fields)) continue;
                    documents++;
                    foreach (var key in UnknownKeys(doc, fields))
                        problems.Add($"{path.Replace('\\', '/')}: {fields.TypeName}.{key}");
                }
            }

            // 188 on 2026-11-06 (120 SOAP assets plus the listener components in prefabs and scenes,
            // counted independently with grep). Far fewer means the scan stopped seeing files.
            Assert.That(documents, Is.GreaterThan(150), "Found too few SOAP objects in the project to trust this scan.");
            Assert.IsEmpty(problems, "Serialized keys no field receives (Unity drops them on the next save):\n" + string.Join("\n", problems.Take(50)));
        }

        /// <summary>Negative control: the scan must flag a renamed field, top level and inside a response.</summary>
        [Test]
        public void UnknownKeys_FlagsAKeyNoFieldReceives()
        {
            var fields = new ScriptFields("Probe");
            fields.TopKeys.UnionWith(new[] { "CategoryIndex", "_eventResponses" });
            fields.ElementKeys["_eventResponses"] = new HashSet<string> { "Delay", "ScriptableEvent", "Response" };
            const string doc =
                "114 &1\nMonoBehaviour:\n  m_Script: {fileID: 11500000, guid: x, type: 3}\n  m_EditorClassIdentifier: \n" +
                "  CategoryIndex: 0\n  _renamed: 1\n  _eventResponses:\n  - Delay: 0\n    ScriptableEvnet: {fileID: 0}\n" +
                "    Response:\n      m_PersistentCalls:\n        m_Calls: []\n";

            CollectionAssert.AreEquivalent(new[] { "_renamed", "_eventResponses[].ScriptableEvnet" }, UnknownKeys(doc, fields).ToArray());
        }

        internal sealed class ScriptFields
        {
            public readonly string TypeName;
            public readonly HashSet<string> TopKeys = new();
            public readonly Dictionary<string, HashSet<string>> ElementKeys = new();
            public ScriptFields(string typeName) => TypeName = typeName;
        }

        /// <summary>Keys written after m_EditorClassIdentifier that no serialized field of the script receives.</summary>
        internal static IEnumerable<string> UnknownKeys(string doc, ScriptFields fields)
        {
            var lines = doc.Split('\n');
            int i = Array.FindIndex(lines, l => l.StartsWith("  m_EditorClassIdentifier:", StringComparison.Ordinal));
            if (i < 0) yield break;

            string array = null;
            for (i++; i < lines.Length; i++)
            {
                var line = lines[i].TrimEnd('\r');
                var top = TopKeyRx.Match(line);
                if (top.Success)
                {
                    var key = top.Groups[1].Value;
                    array = fields.ElementKeys.ContainsKey(key) ? key : null;
                    if (!fields.TopKeys.Contains(key)) yield return key;
                    continue;
                }
                if (array == null) continue;
                var element = ElementKeyRx.Match(line);
                if (element.Success && !fields.ElementKeys[array].Contains(element.Groups[1].Value))
                    yield return array + "[]." + element.Groups[1].Value;
            }
        }

        /// <summary>The serialized fields of every concrete SOAP script, by script GUID.</summary>
        static Dictionary<string, ScriptFields> SoapScriptFields()
        {
            var result = new Dictionary<string, ScriptFields>();
            var host = new GameObject("SoapFormatTests") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var types = TypeCache.GetTypesDerivedFrom<SoapAsset>().Concat(TypeCache.GetTypesDerivedFrom<EventListenerBase>());
                foreach (var type in types)
                {
                    if (type.IsAbstract || type.ContainsGenericParameters) continue;

                    Object instance;
                    MonoScript script;
                    if (typeof(ScriptableObject).IsAssignableFrom(type))
                    {
                        var so = ScriptableObject.CreateInstance(type);
                        instance = so;
                        script = MonoScript.FromScriptableObject(so);
                    }
                    else
                    {
                        var component = (MonoBehaviour)host.AddComponent(type);
                        instance = component;
                        script = component ? MonoScript.FromMonoBehaviour(component) : null;
                    }

                    var path = script ? AssetDatabase.GetAssetPath(script) : null;
                    if (!string.IsNullOrEmpty(path)) // test-only types have no script file
                        result[AssetDatabase.AssetPathToGUID(path)] = FieldsOf(instance, type.Name);

                    if (instance is ScriptableObject) Object.DestroyImmediate(instance);
                }
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
            return result;
        }

        static ScriptFields FieldsOf(Object instance, string typeName)
        {
            var fields = new ScriptFields(typeName);
            var so = new SerializedObject(instance);
            var structArrays = new List<string>();

            var it = so.GetIterator();
            for (bool more = it.Next(true); more; more = it.Next(false))
            {
                fields.TopKeys.Add(it.name);
                if (it.isArray && it.propertyType == SerializedPropertyType.Generic) structArrays.Add(it.name);
            }

            foreach (var name in structArrays)
            {
                var array = so.FindProperty(name);
                if (array.arraySize == 0) array.arraySize = 1;
                var element = array.GetArrayElementAtIndex(0);
                if (element.propertyType != SerializedPropertyType.Generic) continue;

                var keys = new HashSet<string>();
                var end = element.GetEndProperty();
                var child = element.Copy();
                for (bool more = child.Next(true); more && !SerializedProperty.EqualContents(child, end); more = child.Next(false))
                    keys.Add(child.name);
                fields.ElementKeys[name] = keys;
            }
            return fields;
        }
    }
}
