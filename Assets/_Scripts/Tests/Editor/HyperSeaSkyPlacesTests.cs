using System.IO;
using CosmicShore.ScriptableObjects;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicShore.Tests
{
    /// <summary>
    /// A place for each game (<see cref="HyperSeaSkyPlacesSO"/>, the footage critique's weak link 1): the shipped table
    /// names real scenes and real sky numbers, never a colour, and a place's sky is a copy that leaves the asset alone.
    /// </summary>
    public class HyperSeaSkyPlacesTests
    {
        const string AssetPath = "Assets/Resources/" + HyperSeaSkyPlacesSO.ResourcePath + ".asset";
        const string SkyPath = "Assets/_Graphics/Skyboxes/HyperSeaSkybox.mat";

        static HyperSeaSkyPlacesSO Shipped()
        {
            var asset = AssetDatabase.LoadAssetAtPath<HyperSeaSkyPlacesSO>(AssetPath);
            Assert.IsNotNull(asset, $"{AssetPath} is missing");
            return asset;
        }

        static Material Sky()
        {
            var sky = AssetDatabase.LoadAssetAtPath<Material>(SkyPath);
            Assert.IsNotNull(sky, $"{SkyPath} is missing");
            return sky;
        }

        [Test]
        public void EveryPlaceNamesScenesThatExist()
        {
            foreach (var place in Shipped().Places)
            {
                Assert.IsNotEmpty(place.Scenes, $"{place.Name} lists no scene");
                foreach (var scene in place.Scenes)
                    Assert.IsNotEmpty(AssetDatabase.FindAssets($"t:Scene {scene}"), $"{place.Name}: no scene named {scene}");
            }
        }

        [Test]
        public void EveryValueIsASkyNumberNeverAColour()
        {
            var shader = Sky().shader;
            foreach (var place in Shipped().Places)
            {
                foreach (var f in place.Floats)
                {
                    int i = shader.FindPropertyIndex(f.Property);
                    Assert.GreaterOrEqual(i, 0, $"{place.Name}: the sky has no {f.Property}");
                    var type = shader.GetPropertyType(i);
                    Assert.IsTrue(type is ShaderPropertyType.Float or ShaderPropertyType.Range, $"{place.Name}: {f.Property} is a {type}");
                }
                foreach (var v in place.Vectors)
                {
                    int i = shader.FindPropertyIndex(v.Property);
                    Assert.GreaterOrEqual(i, 0, $"{place.Name}: the sky has no {v.Property}");
                    Assert.AreEqual(ShaderPropertyType.Vector, shader.GetPropertyType(i), $"{place.Name}: {v.Property} is not a vector");
                }
            }
        }

        [Test]
        public void ASceneBelongsToOnePlaceAtMost()
        {
            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (var place in Shipped().Places)
                foreach (var scene in place.Scenes)
                    Assert.IsTrue(seen.Add(scene), $"{scene} is in two places");
        }

        [Test]
        public void APlaceSkyIsACopyWithThePlaceValues()
        {
            var table = Shipped();
            var sky = Sky();
            string before = File.ReadAllText(SkyPath);
            Assert.IsNull(table.SkyFor("NoSuchScene", sky), "a scene no place lists draws its own sky");
            foreach (var place in table.Places)
            {
                var copy = table.SkyFor(place.Scenes[0], sky);
                Assert.IsNotNull(copy);
                Assert.AreNotSame(sky, copy);
                Assert.AreSame(copy, table.SkyFor(place.Scenes[0], sky), "the copy is made once");
                foreach (var f in place.Floats)
                    Assert.AreEqual(f.Value, copy.GetFloat(f.Property), 1e-5f, $"{place.Name} {f.Property}");
                foreach (var v in place.Vectors)
                    Assert.AreEqual(v.Value, copy.GetVector(v.Property), $"{place.Name} {v.Property}");
                foreach (var c in new[] { "_CellOverlayColor", "_NebulaColor1", "_DeepColor", "_CoreColor" })
                    Assert.AreEqual(sky.GetColor(c), copy.GetColor(c), $"{place.Name} changed {c}");
            }
            Assert.AreEqual(before, File.ReadAllText(SkyPath), "a place wrote to the sky asset");
        }

        [Test]
        public void AColourIsRefused()
        {
            var table = ScriptableObject.CreateInstance<HyperSeaSkyPlacesSO>();
            var copy = new Material(Sky());
            var colour = copy.GetColor("_DeepColor");
            var so = new SerializedObject(table);
            so.FindProperty("places").arraySize = 1;
            var row = so.FindProperty("places").GetArrayElementAtIndex(0);
            row.FindPropertyRelative("vectors").arraySize = 1;
            var v = row.FindPropertyRelative("vectors").GetArrayElementAtIndex(0);
            v.FindPropertyRelative("property").stringValue = "_DeepColor";
            v.FindPropertyRelative("value").vector4Value = new Vector4(1, 0, 0, 1);
            so.ApplyModifiedPropertiesWithoutUndo();
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("colour set"));
            table.Places[0].Apply(copy);
            Assert.AreEqual(colour, copy.GetColor("_DeepColor"));
            Object.DestroyImmediate(copy);
            Object.DestroyImmediate(table);
        }
    }
}
