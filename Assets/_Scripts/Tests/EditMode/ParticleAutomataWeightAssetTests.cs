#if UNITY_EDITOR
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using System.Text.RegularExpressions;
using CosmicShore.Gameplay;

namespace CosmicShore.Tests
{
    /// <summary>
    /// ParticleAutomataWeightAsset Tests — Validates the PNCA weight pipeline.
    ///
    /// WHY THIS MATTERS:
    /// The Particle NCA runtime is driven entirely by trained weights exported
    /// from Python (Tools/NCA_Training/train_particle_nca.py). The .bytes file
    /// is self-describing — a PNCA header carries the network dimensions and
    /// the simulation constants the rule was trained with. If header parsing
    /// drifts from the export format, or the fail-loud validation softens, the
    /// automaton would either silently run garbage weights or run trained
    /// weights under the wrong simulation constants. These tests lock the
    /// parse against the committed trained asset and verify the rejection
    /// paths stay loud.
    /// </summary>
    [TestFixture]
    public class ParticleAutomataWeightAssetTests
    {
        const string TrainedAssetPath = "Assets/_SO_Assets/Automata/Weights_SphereTorus.asset";

        // Must mirror ParticleAutomataStep.compute's compile-time constants.
        const int ShaderChannelCount = 8;
        const int ShaderPerceptionDim = 46;
        const int ShaderOutputDim = 11;
        const int ShaderMaxHidden = 128;
        const int ShaderMaxNeighbors = 16;

        [Test]
        public void TrainedAsset_ParsesAndMatchesShaderConstants()
        {
            var asset = AssetDatabase.LoadAssetAtPath<ParticleAutomataWeightAsset>(TrainedAssetPath);
            if (asset == null)
                Assert.Ignore($"No trained weight asset at {TrainedAssetPath}.");

            bool ok = asset.TryLoadWeights(out var w1, out var b1, out var w2, out var b2);

            Assert.IsTrue(ok, "Trained PNCA asset failed to parse.");
            Assert.AreEqual(ShaderChannelCount, asset.ChannelCount);
            Assert.AreEqual(ShaderPerceptionDim, asset.PerceptionDim);
            Assert.AreEqual(ShaderOutputDim, asset.OutputDim);
            Assert.LessOrEqual(asset.HiddenSize, ShaderMaxHidden);
            Assert.LessOrEqual(asset.KNeighbors, ShaderMaxNeighbors);
            Assert.Greater(asset.StepsPerLoop, 0);
            Assert.Greater(asset.PerceptionRadius, 0f);
            Assert.Greater(asset.StepScale, 0f);
            Assert.That(asset.FireRate, Is.InRange(0f, 1f));

            Assert.AreEqual(asset.PerceptionDim * asset.HiddenSize, w1.Length);
            Assert.AreEqual(asset.HiddenSize, b1.Length);
            Assert.AreEqual(asset.HiddenSize * asset.OutputDim, w2.Length);
            Assert.AreEqual(asset.OutputDim, b2.Length);

            foreach (var arr in new[] { w1, b1, w2, b2 })
                foreach (var v in arr)
                    Assert.IsFalse(float.IsNaN(v) || float.IsInfinity(v),
                        "Trained weights contain non-finite values.");
        }

        [Test]
        public void MissingWeightsFile_FailsLoud()
        {
            var asset = ScriptableObject.CreateInstance<ParticleAutomataWeightAsset>();
            try
            {
                LogAssert.Expect(LogType.Error, new Regex("No weights file assigned"));
                bool ok = asset.TryLoadWeights(out _, out _, out _, out _);
                Assert.IsFalse(ok);
            }
            finally
            {
                Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void NonPncaPayload_FailsLoud()
        {
            // A TextAsset built from a string is valid bytes but has no PNCA
            // header — the loader must reject it loudly, not guess.
            var bogus = new TextAsset(new string('x', 64));
            var asset = ScriptableObject.CreateInstance<ParticleAutomataWeightAsset>();
            try
            {
                var so = new SerializedObject(asset);
                so.FindProperty("weightsFile").objectReferenceValue = bogus;
                so.ApplyModifiedPropertiesWithoutUndo();

                LogAssert.Expect(LogType.Error, new Regex("Bad magic"));
                bool ok = asset.TryLoadWeights(out _, out _, out _, out _);
                Assert.IsFalse(ok);
            }
            finally
            {
                Object.DestroyImmediate(asset);
                Object.DestroyImmediate(bogus);
            }
        }
    }
}
#endif
