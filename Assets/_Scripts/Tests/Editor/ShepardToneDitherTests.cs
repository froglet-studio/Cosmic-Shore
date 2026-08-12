#if UNITY_EDITOR
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The automated gate for the mass crystal's Shepard-tone screen door
    /// (Docs/SHEPARD_TONE.md).
    ///
    /// Why a test and not just the validator menu item: every failure mode here is SILENT.
    /// Revert the graph and the crystal goes back to a blended ball that still looks like
    /// "a crystal" in a screenshot; leave one material transparent and it gets the stipple
    /// with none of the depth ordering, which is worse than what it replaced and equally
    /// quiet; disable _ALPHATEST_ON and URP compiles the Alpha output away entirely, so the
    /// shells stop thinning at all and the Shepard tone simply stops happening. None of
    /// those produce a console message. These assertions run from assets alone (no play
    /// mode) and fail the moment the wiring drifts.
    ///
    /// Same rules as FrogletTools > Ecology > Prism Animation > Validate Shepard Tone
    /// Dither, which is the human-facing half.
    /// </summary>
    public class ShepardToneDitherTests
    {
        const string GraphPath = "Assets/_Graphics/Materials/Graphs/ShepardGraph.shadergraph";
        const string HlslPath = "Assets/_Graphics/Materials/Graphs/ShepardToneDither.hlsl";
        const string HlslGuid = "1af4b28d920441fd9ae968eaffac68c4";
        const string FunctionName = "ShepardToneDither";
        const string ShaderName = "Shader Graphs/ShepardGraph";
        const string AlphaTestKeyword = "_ALPHATEST_ON";
        const string WireTool = "python3 Tools/Shaders/wire_shepard_tone_dither.py";
        const string MaterialTool = "python3 Tools/Shaders/enable_shepard_alpha_clip.py";

        static string GraphText()
        {
            // Normalize CRLF: a Windows checkout otherwise defeats any line-oriented read.
            return File.ReadAllText(GraphPath).Replace("\r\n", "\n");
        }

        [Test]
        public void DitherHlsl_ExistsAtThePinnedGuid()
        {
            Assert.IsTrue(File.Exists(HlslPath), $"{HlslPath} is missing — the crystal has no GPU half.");
            Assert.AreEqual(HlslGuid, AssetDatabase.AssetPathToGUID(HlslPath),
                $"{HlslPath} GUID drifted — the graph's Custom Function pins {HlslGuid} and would resolve to nothing.");

            string hlsl = File.ReadAllText(HlslPath);
            Assert.IsTrue(hlsl.Contains($"void {FunctionName}_float("),
                $"{HlslPath} no longer declares {FunctionName}_float — the Custom Function node cannot bind.");
        }

        [Test]
        public void DitherHlsl_KeepsTheStrictlyInsideZeroOneThresholdNudge()
        {
            // clip(0) KEEPS the fragment on the URP variants that clip directly rather than
            // through AlphaDiscard's epsilon, so a threshold of exactly 0 against an alpha of
            // 0 leaves confetti in a shell that should be gone. Same guard as the corridor.
            string hlsl = File.ReadAllText(HlslPath);
            Assert.IsTrue(hlsl.Contains("return n * 0.998 + 0.001;"),
                "ShepardToneSafeThreshold lost its strictly-inside-(0,1) nudge — a computed " +
                "threshold of exactly 0 survives clip() and leaves speckle in a dead shell.");
        }

        [Test]
        public void ShepardGraph_HasTheDitherSpliced()
        {
            Assert.IsTrue(File.Exists(GraphPath), $"{GraphPath} is missing.");
            string text = GraphText();

            Assert.IsTrue(text.Contains($"\"m_FunctionName\": \"{FunctionName}\""),
                $"ShepardGraph has no {FunctionName} node — the crystal is back on alpha blending. Fix: {WireTool}");
            Assert.IsTrue(text.Contains($"\"m_FunctionSource\": \"{HlslGuid}\""),
                $"ShepardGraph's {FunctionName} node points at the wrong HLSL asset. Fix: {WireTool}");

            foreach (var slot in new[] { "PositionOS", "BaseAlpha", "Start", "Stop", "Alpha", "ClipThreshold" })
            {
                Assert.IsTrue(text.Contains($"\"m_DisplayName\": \"{slot}\""),
                    $"ShepardGraph's {FunctionName} node is missing the '{slot}' slot — its signature has " +
                    $"drifted from the HLSL and the wrong argument would bind. Fix: {WireTool}");
            }
        }

        [Test]
        public void ShepardGraph_TargetIsOpaque()
        {
            string text = GraphText();
            Assert.IsFalse(text.Contains("\"m_SurfaceType\": 1"),
                "ShepardGraph's UniversalTarget is TRANSPARENT. The screen door needs the opaque " +
                "queue: half the point is that an outer shell occludes the ones behind it and you " +
                $"see them through its holes. Fix: {WireTool}");
        }

        [Test]
        public void EveryShepardMaterial_IsOpaqueAndAlphaTested()
        {
            var materials = AssetDatabase.FindAssets("t:Material")
                .Select(g => AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(m => m != null && m.shader != null && m.shader.name == ShaderName)
                .ToArray();

            Assert.IsNotEmpty(materials,
                $"No materials found on {ShaderName} — either the shader was renamed or the mass " +
                "crystal lost its materials; either way this gate is no longer guarding anything.");

            foreach (var mat in materials)
            {
                bool transparent = mat.HasProperty("_Surface") && mat.GetFloat("_Surface") > 0.5f;
                Assert.IsFalse(transparent,
                    $"{mat.name} is still in the transparent queue. It would get the dither's stipple " +
                    $"and none of its depth ordering — worse than the blending it replaced. Fix: {MaterialTool}");

                Assert.IsTrue(mat.IsKeywordEnabled(AlphaTestKeyword),
                    $"{mat.name} has {AlphaTestKeyword} disabled. URP compiles the Alpha output away " +
                    $"entirely on an opaque surface without it, so the shell never thins. Fix: {MaterialTool}");
            }
        }
    }
}
#endif
