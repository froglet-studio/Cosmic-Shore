#if UNITY_EDITOR
using System.IO;
using System.Linq;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The automated gate for the VESSEL VISION BAND as a PLATFORM LAW (Docs/VESSEL_VISION.md):
    /// every vessel must be markable at range, from ONE shader splice and ONE stamp site, with no
    /// per-vessel authoring and no way to author a vessel or a mode in which it is off.
    ///
    /// Why a test and not just the validator menu item: every failure mode here is SILENT and
    /// none of them shows up in a screenshot. A band whose plateau peaks at 0.999 renders as a
    /// permanently slightly-weak mark. A near floor lowered under the gameplay camera's follow
    /// distance starts painting the pilot's OWN hull, which reads as a bug in the camera. A tint
    /// property that stops being exposed severs the per-vessel channel while leaving the graph
    /// looking perfectly wired. And a second stamp site is a second owner of that channel, so a
    /// pilot who changes domain keeps the old colour on some machines and not others.
    ///
    /// The band checks assert the property the law exists to guarantee: the mapping is ABSOLUTE,
    /// so the same distance to ANY vessel produces the same mark. If someone ever makes the band
    /// per-vessel or per-mode, these fail.
    /// </summary>
    public class VesselVisionLawTests
    {
        const float Tolerance = 0.0001f;

        const string GraphPath = "Assets/_Graphics/Materials/Graphs/VesselGraph.shadergraph";
        const string HlslPath = "Assets/_Graphics/Materials/Graphs/VesselVisionShading.hlsl";
        const string HelperPath = "Assets/_Scripts/Controller/Vessel/VesselHelper.cs";
        const string ConfigAssetPath = "Assets/Resources/VesselVisionShadingConfig.asset";
        const string ScriptRoot = "Assets/_Scripts";

        static VesselVisionShadingConfigSO NewConfig() =>
            ScriptableObject.CreateInstance<VesselVisionShadingConfigSO>();

        #region The law: one absolute distance → mark mapping

        [TestCase(0f, 0f)]
        [TestCase(40f, 0f)]        // the gameplay camera's own follow distance — your own hull
        [TestCase(150f, 0f)]       // nearFadeStart itself
        [TestCase(250f, 0.5f)]     // midpoint of the rising grade
        [TestCase(350f, 1f)]
        [TestCase(1200f, 1f)]      // a full cell radius away — the case the aid exists for
        [TestCase(2000f, 1f)]
        [TestCase(2750f, 0.5f)]    // midpoint of the falling grade
        [TestCase(3500f, 0f)]
        [TestCase(100000f, 0f)]
        public void Effect01_IsTheAbsoluteDistanceBand(float distance, float expected)
        {
            var config = NewConfig();
            Assert.AreEqual(expected, config.Effect01(distance), Tolerance,
                "The band is absolute and shared by the whole fleet — this mapping IS the law. " +
                "A big hull is marked at the same range as a small one, because the question the " +
                "aid answers does not depend on how big the pilot's ship is.");
        }

        [Test]
        public void Effect01_IsZeroInsideTheLocalCameraFollowDistance()
        {
            var config = NewConfig();
            for (float d = 0f; d <= VesselVisionShadingConfigSO.MinLocalHullClearance; d += 1f)
                Assert.AreEqual(0f, config.Effect01(d), Tolerance,
                    "The pilot's own vessel rides 10-40 units from its camera. The near floor is " +
                    "what excludes it, and it is the ONLY thing that does — there is deliberately " +
                    "no 'is this me' test in the law, because 'close things do not need help' is " +
                    "the rule and your own ship is the closest thing there is.");
        }

        [Test]
        public void Effect01_BothEdgesAreMonotone()
        {
            var config = NewConfig();

            float previous = -1f;
            for (float d = config.NearFadeStart; d <= config.NearFullStart; d += 1f)
            {
                float value = config.Effect01(d);
                Assert.GreaterOrEqual(value, previous - Tolerance, "rising edge is not monotone");
                previous = value;
            }

            previous = 2f;
            for (float d = config.FarFullEnd; d <= config.FarFadeEnd; d += 1f)
            {
                float value = config.Effect01(d);
                Assert.LessOrEqual(value, previous + Tolerance, "falling edge is not monotone");
                previous = value;
            }
        }

        [Test]
        public void Effect01_NeverStepsHardEnoughToPop()
        {
            // A boosted vessel closes ~6 units per frame at 60fps, so this is the largest change
            // the mark can make between two frames. Grading the edges is the whole reason they are
            // ramps rather than thresholds — a mark that pops reads as a new object appearing,
            // which is the same thing continuity of existence forbids for mass.
            var config = NewConfig();
            float previous = config.Effect01(0f);
            float worst = 0f;
            for (float d = 0f; d <= 6000f; d += 6f)
            {
                float value = config.Effect01(d);
                worst = Mathf.Max(worst, Mathf.Abs(value - previous));
                previous = value;
            }
            Assert.Less(worst, 0.06f,
                $"the band changes by {worst:0.000} between adjacent frames — that reads as a pop, " +
                "not a fade. Widen whichever grade is too short.");
        }

        [Test]
        public void Effect01_IsExactlyOneAcrossThePlateau()
        {
            var config = NewConfig();
            for (float d = config.NearFullStart; d <= config.FarFullEnd; d += 7f)
                Assert.AreEqual(1f, config.Effect01(d), Tolerance,
                    "a plateau that peaks below 1 is a law that never reaches full strength, and " +
                    "nothing would ever say so.");
        }

        [Test]
        public void Effect01_IsZeroWhenTheLawIsDisabled()
        {
            var config = NewConfig();
            var so = new SerializedObject(config);
            so.FindProperty("enabled").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();

            Assert.AreEqual(0f, config.Effect01(1000f), Tolerance);
            Assert.AreEqual(Vector4.zero, config.PackBand(),
                "a disabled law must publish the shader's OFF sentinel (band.w <= 0), not a live " +
                "band that merely evaluates to zero on the CPU.");
        }

        #endregion

        #region Authoring can bend the band but never break it

        [Test]
        public void Accessors_CannotBeAuthoredIntoAnInvertedBand()
        {
            var config = NewConfig();
            var so = new SerializedObject(config);
            so.FindProperty("nearFadeStart").floatValue = 900f;
            so.FindProperty("nearFullStart").floatValue = 100f;   // inverted
            so.FindProperty("farFullEnd").floatValue = 50f;       // inverted
            so.FindProperty("farFadeEnd").floatValue = 10f;       // inverted
            so.ApplyModifiedPropertiesWithoutUndo();

            Assert.Less(config.NearFadeStart, config.NearFullStart);
            Assert.LessOrEqual(config.NearFullStart, config.FarFullEnd);
            Assert.Less(config.FarFullEnd, config.FarFadeEnd);

            // The accessors hold the ordering, so the law still evaluates rather than dividing by
            // zero or returning NaN — a mis-authored asset degrades, it does not crash the frame.
            for (float d = 0f; d <= 2000f; d += 37f)
            {
                float value = config.Effect01(d);
                Assert.IsFalse(float.IsNaN(value), $"Effect01({d}) was NaN");
                Assert.That(value, Is.InRange(0f, 1f));
            }
        }

        [Test]
        public void Accessors_HoldTheRimWindowOpen()
        {
            var config = NewConfig();
            var so = new SerializedObject(config);
            so.FindProperty("rimInner").floatValue = 0.9f;
            so.FindProperty("rimOuter").floatValue = 0.1f;        // inverted
            so.ApplyModifiedPropertiesWithoutUndo();

            Assert.Greater(config.RimOuter, config.RimInner,
                "an inverted rim window collapses the silhouette outline into a hard step, which " +
                "is the one part of the mark that has to survive at extreme range.");
        }

        [TestCase("nearFullStart", 100f, TestName = "IsSane_rejects_inverted_rising_edge")]
        [TestCase("farFadeEnd", 1000f, TestName = "IsSane_rejects_inverted_falling_edge")]
        [TestCase("nearFadeStart", 10f, TestName = "IsSane_rejects_a_floor_inside_the_camera")]
        [TestCase("strength", 0f, TestName = "IsSane_rejects_a_law_authored_to_do_nothing")]
        public void IsSane_RejectsAnAssetThatWouldFailSilently(string field, float value)
        {
            var config = NewConfig();
            var so = new SerializedObject(config);
            so.FindProperty(field).floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();

            Assert.IsFalse(config.IsSane(out string reason),
                $"authoring {field} = {value} must be rejected — every one of these renders as a " +
                "subtly wrong mark rather than as an error.");
            Assert.IsNotEmpty(reason, "a rejection must name what is wrong.");
        }

        [Test]
        public void IsSane_RejectsABandThatCannotCrossAnArena()
        {
            var config = NewConfig();
            var so = new SerializedObject(config);
            so.FindProperty("farFullEnd").floatValue = 500f;
            so.FindProperty("farFadeEnd").floatValue = 900f;      // dies well inside a cell
            so.ApplyModifiedPropertiesWithoutUndo();

            Assert.IsFalse(config.IsSane(out _),
                "pilots on opposite sides of a cell (membrane radius 1200) are the case the aid " +
                "exists for; a band that dies before that reach cannot answer it.");
        }

        #endregion

        #region The shipped assets

        [Test]
        public void ShippedConfig_IsSane()
        {
            var config = AssetDatabase.LoadAssetAtPath<VesselVisionShadingConfigSO>(ConfigAssetPath);
            if (config == null)
                Assert.Ignore($"No asset at {ConfigAssetPath}; the law runs on the SO defaults, " +
                              "which the tests above already cover.");

            Assert.IsTrue(config.IsSane(out string reason), reason);
        }

        [Test]
        public void ShippedGraph_IsStillWired()
        {
            Assert.IsTrue(
                VesselVisionLawSource.GraphIsWired(File.ReadAllText(GraphPath), out string reason),
                reason + "\nRun: python3 Tools/Shaders/wire_vessel_vision_shading.py");
        }

        [Test]
        public void ShippedHlsl_StillDeclaresBothCutoffsAndTheQuantizerGuard()
        {
            Assert.IsTrue(
                VesselVisionLawSource.HlslDeclaresLaw(File.ReadAllText(HlslPath), out string reason),
                reason);
        }

        [Test]
        public void Stamp_HasExactlyOneCallSiteAndItIsSetShipProperties()
        {
            int sites = Directory
                .EnumerateFiles(ScriptRoot, "*.cs", SearchOption.AllDirectories)
                .Sum(file => CountOccurrences(File.ReadAllText(file), VesselVisionLawSource.StampInvocation));

            Assert.IsTrue(
                VesselVisionLawSource.StampHasExactlyOneCallSite(
                    File.ReadAllText(HelperPath), sites, out string reason),
                reason);
        }

        static int CountOccurrences(string haystack, string needle)
        {
            int count = 0;
            for (int i = haystack.IndexOf(needle, System.StringComparison.Ordinal);
                 i >= 0;
                 i = haystack.IndexOf(needle, i + 1, System.StringComparison.Ordinal))
                count++;
            return count;
        }

        #endregion
    }
}
#endif
