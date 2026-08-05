#if UNITY_EDITOR
using System.IO;
using System.Linq;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The automated gate for the speed tunnel as a PLATFORM LAW (Docs/SPEED_TUNNEL.md): the
    /// quasi dolly zoom must apply to EVERY vessel, driven by ONE static driver, with no
    /// per-vessel authoring and no way to author a vessel or mode in which it is off.
    ///
    /// Why a test and not just the validator menu item: this law's failure mode is SILENT in
    /// both directions. A vessel that stops tunnelling looks like a vessel that isn't going
    /// very fast, and a SECOND writer of the Panini override doesn't error — it just makes
    /// the effect flicker or stick during a vessel swap, because
    /// <c>PostProcessingManager.SetSpeedTunnelPanini</c> is one global override with no
    /// ref-counting and any caller passing 0 releases everyone. Neither shows up in a
    /// screenshot. That is exactly how the retired per-vessel component would creep back.
    ///
    /// The window checks assert the property the law exists to guarantee: the mapping is
    /// ABSOLUTE, so the same speed on ANY vessel produces the same visual. If someone ever
    /// makes the window per-vessel, these fail.
    /// </summary>
    public class SpeedTunnelLawTests
    {
        const float Tolerance = 0.0001f;

        const string VesselFolder = "Assets/_Prefabs/Spacevessels";
        const string ControllerPath = "Assets/_Scripts/Controller/Vessel/VesselController.cs";
        const string ConfigAssetPath = "Assets/Resources/SpeedTunnelConfig.asset";

        static SpeedTunnelConfigSO NewConfig() => ScriptableObject.CreateInstance<SpeedTunnelConfigSO>();

        #region The law: one absolute speed → effect mapping

        [TestCase(0f, 0f)]
        [TestCase(69f, 0f)]
        [TestCase(70f, 0f)]
        [TestCase(175f, 0.5f)]
        [TestCase(280f, 1f)]
        [TestCase(720f, 1f)]
        [TestCase(100000f, 1f)]
        public void Effect01_IsTheAbsoluteSpeedWindow(float speed, float expected)
        {
            var config = NewConfig();
            Assert.AreEqual(expected, config.Effect01(speed), Tolerance,
                "The window is absolute and shared by the whole fleet — this mapping IS the " +
                "law. Speeds below the floor cost nothing; speeds above the ceiling saturate " +
                "rather than distorting further.");
        }

        [Test]
        public void Effect01_IsVesselAgnostic()
        {
            var config = NewConfig();

            // The property the law exists to guarantee, stated as an assertion: nothing about
            // the vessel enters this function. If a per-vessel scalar, a per-vessel window, or
            // a normalization by the vessel's own top speed is ever introduced, Effect01 stops
            // being a pure function of speed and this test is the thing that has to be deleted
            // to allow it. Do not delete it — that is a design change, not a refactor.
            var method = typeof(SpeedTunnelConfigSO).GetMethod(nameof(SpeedTunnelConfigSO.Effect01));
            Assert.IsNotNull(method);
            var parameters = method.GetParameters();
            Assert.AreEqual(1, parameters.Length,
                "Effect01 must take speed and nothing else — no vessel, no status, no context.");
            Assert.AreEqual(typeof(float), parameters[0].ParameterType);
        }

        [Test]
        public void Effect01_IsMonotonic()
        {
            var config = NewConfig();
            float previous = -1f;
            for (float speed = 0f; speed <= 400f; speed += 5f)
            {
                float value = config.Effect01(speed);
                Assert.GreaterOrEqual(value, previous,
                    $"Effect strength must never fall as speed rises (speed {speed}).");
                previous = value;
            }
        }

        [Test]
        public void Effect01_IsZeroWhenTheLawIsDisabled()
        {
            var config = NewConfig();
            var so = new SerializedObject(config);
            so.FindProperty("enabled").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();

            Assert.AreEqual(0f, config.Effect01(10000f), Tolerance,
                "The master switch must produce exactly zero effect, not a small residue — " +
                "otherwise 'off' still writes the camera and the Panini override every frame.");
        }

        [Test]
        public void InvertedWindow_CannotDivideByZeroOrInvert()
        {
            var config = NewConfig();
            var so = new SerializedObject(config);
            so.FindProperty("minEffectSpeed").floatValue = 300f;
            so.FindProperty("maxEffectSpeed").floatValue = 100f;
            so.ApplyModifiedPropertiesWithoutUndo();

            Assert.Greater(config.MaxEffectSpeed, config.MinEffectSpeed,
                "A mis-authored asset must not be able to invert the window.");
            float value = config.Effect01(200f);
            Assert.IsFalse(float.IsNaN(value), "Effect01 produced NaN from an inverted window.");
            Assert.That(value, Is.InRange(0f, 1f));
        }

        #endregion

        #region Optics

        [TestCase(0f, 60f)]
        [TestCase(0.5f, 47.5f)]
        [TestCase(1f, 35f)]
        public void FovFor_DropsBelowHomeProportionally(float effect01, float expected)
        {
            var config = NewConfig();
            Assert.AreEqual(expected, config.FovFor(60f, effect01), Tolerance,
                "The effect only ever moves DOWN from whatever FOV the game is actually " +
                "running with, and returns there exactly at zero.");
        }

        [Test]
        public void FovFor_NeverProducesADegenerateCamera()
        {
            var config = NewConfig();
            Assert.GreaterOrEqual(config.FovFor(5f, 1f), SpeedTunnelConfigSO.MinFieldOfView,
                "A home FOV smaller than the drop must clamp, not invert the projection.");
        }

        [TestCase(0f, 0f)]
        [TestCase(0.5f, -0.25f)]
        [TestCase(1f, -0.5f)]
        public void PaniniOffset_IsAlwaysARelaxation(float effect01, float expected)
        {
            var config = NewConfig();
            float offset = config.PaniniOffsetFor(effect01);
            Assert.AreEqual(expected, offset, Tolerance);
            Assert.LessOrEqual(offset, 0f,
                "The tunnel relaxes Panini toward rectilinear — a positive offset would " +
                "compress the frame further and read as the opposite effect.");
        }

        #endregion

        #region The law is un-authorable

        [Test]
        public void NoVesselPrefabCarriesItsOwnTunnelDriver()
        {
            int vessels = 0;
            foreach (var path in VesselPrefabPaths())
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!prefab || !prefab.GetComponentInChildren<VesselStatus>(true)) continue;
                vessels++;

                foreach (var component in prefab.GetComponentsInChildren<Component>(true))
                {
                    if (component == null) continue;
                    Assert.IsFalse(component.GetType().Name.Contains("SpeedTunnel"),
                        $"{prefab.name} carries {component.GetType().Name}. The tunnel is driven by " +
                        "the single static VesselSpeedTunnel; a per-vessel writer races the one " +
                        "global Panini override across a vessel swap.");
                }

                Assert.IsFalse(File.ReadAllText(path).Contains("SpeedTunnelEffectController"),
                    $"{prefab.name} still references the retired per-vessel SpeedTunnelEffectController.");
            }

            Assert.Greater(vessels, 0, "Found no vessel prefabs — the sweep is not actually checking anything.");
        }

        [Test]
        public void TheLawIsBoundOnTheUniversalVesselEntryPoint()
        {
            Assert.IsTrue(File.Exists(ControllerPath), $"{ControllerPath} is missing.");
            string source = File.ReadAllText(ControllerPath);

            Assert.IsTrue(source.Contains("VesselSpeedTunnel.SetTarget"),
                "VesselController.Initialize is the ONE method every vessel calls on every spawn " +
                "path. Binding anywhere else — a prefab component, a camera, a game mode — is what " +
                "makes a platform law forgettable.");
            Assert.IsTrue(source.Contains("VesselSpeedTunnel.ClearTarget"),
                "Without an identity-guarded release a destroyed vessel keeps driving the camera.");
            Assert.IsTrue(source.Contains("player.IsLocalPilot"),
                "IsLocalUser misses the legacy non-networked single-player spawn path — the exact " +
                "escape hatch this law must not have.");
        }

        [Test]
        public void ShippedConfig_IsSaneWhenAuthored()
        {
            var config = AssetDatabase.LoadAssetAtPath<SpeedTunnelConfigSO>(ConfigAssetPath);
            if (config == null)
            {
                // Legal on a fresh clone: the runtime falls back to the SO's own defaults, so
                // the law holds with zero authoring (the PrismOcclusionConfig precedent).
                Assert.Pass("No Resources config authored — code defaults apply.");
                return;
            }

            Assert.IsTrue(config.IsSane(out string reason), reason);
            Assert.IsTrue(config.Enabled,
                "'enabled' is a global debug switch, not a shipping state.");
        }

        #endregion

        static string[] VesselPrefabPaths() =>
            AssetDatabase.FindAssets("t:Prefab", new[] { VesselFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => !string.IsNullOrEmpty(p))
                .OrderBy(p => p)
                .ToArray();
    }
}
#endif
