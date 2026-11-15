#if UNITY_EDITOR
using System.IO;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The hull light (VesselHullLighting.hlsl, Docs/VesselModels/COLOR.md section 2a). What can fail silently: the
    /// light stops reading the sky (a renamed skybox property makes every hull fall back to a white key), the hull
    /// slots lose their per-material gloss, or the graph loses the splice. None of these shows as an error.
    /// </summary>
    public class VesselHullLightingTests
    {
        const string SkyboxPath = "Assets/_Graphics/Skyboxes/HyperSeaSkybox.mat";
        const string GraphPath = "Assets/_Graphics/Materials/Graphs/VesselGraph.shadergraph";
        const string ConfigPath = "Assets/Resources/VesselHullLightingConfig.asset";

        [Test]
        public void TheKeyIsTheSkysOwnCore_InItsColourAndDirection()
        {
            var sky = AssetDatabase.LoadAssetAtPath<Material>(SkyboxPath);
            Assert.IsNotNull(sky, "the HyperSea skybox moved; the hull light reads its core from it");
            Assert.IsTrue(sky.HasVector("_CoreDirection"), "the skybox no longer names _CoreDirection");

            VesselHullLighting.ReadSky(sky, out var key, out var keyColor, out var skyColor, out var band, out var bandColor);

            Vector3 core = ((Vector3)sky.GetVector("_CoreDirection")).normalized;
            Assert.That(Vector3.Distance(key, core), Is.LessThan(1e-4f), "the key must come from where the sky draws the core");
            Assert.That(key.magnitude, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(band.magnitude, Is.EqualTo(1f).Within(1e-4f));

            Color expected = (QualitySettings.activeColorSpace == ColorSpace.Linear
                ? sky.GetColor("_CoreColor").linear : sky.GetColor("_CoreColor")) * sky.GetFloat("_CoreBrightness");
            Assert.That(keyColor.r, Is.EqualTo(expected.r).Within(1e-4f));
            Assert.That(keyColor.g, Is.EqualTo(expected.g).Within(1e-4f));
            Assert.That(keyColor.b, Is.EqualTo(expected.b).Within(1e-4f));
            Assert.That(skyColor.maxColorComponent, Is.GreaterThan(0f), "the sky fill read black from the HyperSea sky");
            Assert.That(bandColor.maxColorComponent, Is.GreaterThan(0f), "the band read black from the HyperSea sky");
        }

        [Test]
        public void ASkyWithNoCore_StillLightsTheHull()
        {
            VesselHullLighting.ReadSky(null, out var key, out var keyColor, out _, out _, out var bandColor);
            Assert.That(key.magnitude, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(keyColor.maxColorComponent, Is.GreaterThan(0f));
            Assert.That(bandColor.maxColorComponent, Is.EqualTo(0f), "no sky, no band to reflect");
        }

        [Test]
        public void TheConfigShipsOn_AndItsRimNeverInverts()
        {
            var config = AssetDatabase.LoadAssetAtPath<VesselHullLightingConfigSO>(ConfigPath);
            Assert.IsNotNull(config, $"{ConfigPath} is missing; the hull light would run on defaults");
            Assert.That(config.Strength, Is.GreaterThan(0f), "the hull light ships off");
            Vector4 rim = config.PackRim();
            Assert.That(rim.y, Is.GreaterThan(rim.x), "rim outer must sit outside rim inner");
            Assert.That(config.PackShape().y, Is.GreaterThan(0f), "a zero shade floor punches a black hole in every hull");
        }

        [Test]
        public void TheHullSlotsCarryTheirGloss()
        {
            AssertFloat("Assets/_Graphics/Materials/BlueBaseVesselMaterial.mat", "_HullGloss", 1f);
            AssertFloat("Assets/_Graphics/Materials/ScreenVesselMaterial.mat", "_HullGloss", 1.6f);
            AssertFloat("Assets/_Graphics/Materials/EngineVesselMaterial.mat", "_HullSelfLit", 1f);
            AssertFloat("Assets/_Graphics/Materials/EngineVesselMaterial.mat", "_HullGloss", 0f);
        }

        [Test]
        public void TheGraphCarriesTheSplice()
        {
            string graph = File.ReadAllText(GraphPath);
            StringAssert.Contains("\"m_FunctionName\": \"VesselHullLight\"", graph);
            StringAssert.Contains("\"m_DefaultReferenceName\": \"_VesselHullDomain\"", graph);
            StringAssert.Contains("\"m_DefaultReferenceName\": \"_HullGloss\"", graph);
            StringAssert.Contains("\"m_DefaultReferenceName\": \"_HullSelfLit\"", graph);
        }

        static void AssertFloat(string path, string property, float expected)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Assert.IsNotNull(material, path);
            Assert.IsTrue(material.HasFloat(property), $"{path} has no {property}");
            Assert.That(material.GetFloat(property), Is.EqualTo(expected).Within(1e-4f), $"{path} {property}");
        }
    }
}
#endif
