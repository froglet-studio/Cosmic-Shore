#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The MOBILE look of holes and waves (Docs/BLACK_HOLE.md §5.4; Yash, 2026-10-29: "make sure we are not warping
    /// space ... mobile friendly ... the same [as the Meerkat shader]"). It fails SILENTLY in two directions: a lost
    /// material makes every hole a plain sphere; a config left on the Studio lens look quietly brings back the
    /// full-screen copy, the depth texture and the stretch on a phone. Tools/Shaders/verify_black_hole_glow.py checks
    /// the pixels, the ripple's waveform and the compiles (GLES 3.0 included) offline; this pins the same in the Editor.
    /// </summary>
    public class BlackHoleGlowTests
    {
        const string ShaderPath = "Assets/_Graphics/Materials/Graphs/BlackHoleGlow.shader";
        const string ConfigAssetPath = "Assets/Resources/" + BlackHoleRegistry.ConfigResourcePath + ".asset";
        const float BloomThreshold = 0.2f;

        static BlackHoleConfigSO LoadConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<BlackHoleConfigSO>(ConfigAssetPath);
            return config != null ? config : ScriptableObject.CreateInstance<BlackHoleConfigSO>();
        }

        [Test]
        public void Config_TheMobileLookIsTheDefaultAndWarpsNothing()
        {
            var fresh = ScriptableObject.CreateInstance<BlackHoleConfigSO>();
            Assert.AreEqual(HoleLook.Mobile, fresh.Look, "a new BlackHoleConfig must start on the Mobile look.");
            Assert.IsFalse(fresh.SpaceWarp);
            Object.DestroyImmediate(fresh);

            var config = LoadConfig();
            Assert.AreEqual(HoleLook.Mobile, config.Look,
                "the shipped BlackHoleConfig is on the Studio lens look: every phone pays the full-screen copy, the depth " +
                "texture and the prism / hull stretch again (Docs/BLACK_HOLE.md §5.4).");
            Assert.IsFalse(config.SpaceWarp);
            Assert.IsTrue(config.LensEnabled, "lensEnabled is off: holes draw as plain spheres, not the mobile look.");
        }

        [Test]
        public void Glow_MaterialShaderShipTogetherAndCompile()
        {
            Assert.IsTrue(File.Exists(ShaderPath), $"{ShaderPath} is missing.");
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Resources/" + BlackHoleGlow.MaterialResourcePath + ".mat");
            Assert.IsNotNull(material, "Assets/Resources/BlackHoleGlow.mat is missing — every hole falls back to a plain sphere.");
            Assert.IsNotNull(material.shader, "BlackHoleGlow.mat has no shader.");
            Assert.AreEqual("CosmicShore/BlackHoleGlow", material.shader.name, "BlackHoleGlow.mat points at the wrong shader.");
            var errors = ShaderUtil.GetShaderMessages(material.shader)
                .Where(m => m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)
                .Select(m => $"{m.message} ({m.file}:{m.line}, {m.platform})")
                .ToArray();
            Assert.IsFalse(ShaderUtil.ShaderHasError(material.shader),
                "CosmicShore/BlackHoleGlow does not compile:\n" + string.Join("\n", errors));
        }

        /// <summary>The Meerkat water tube's recipe: one forward pass, no copy, no depth texture, no texture at all.</summary>
        [Test]
        public void Glow_IsOneCheapForwardPassThatReadsNothing()
        {
            string shader = Regex.Replace(File.ReadAllText(ShaderPath), @"//[^\n]*", "");
            Assert.AreEqual(1, Regex.Matches(shader, @"\bPass\s*\{").Count, "the glow must stay ONE pass.");
            StringAssert.Contains("\"LightMode\" = \"UniversalForward\"", shader, "the glow must be drawn by URP's forward pass.");
            StringAssert.Contains("Blend One OneMinusSrcAlpha", shader);
            StringAssert.Contains("ZWrite Off", shader);
            StringAssert.Contains("#pragma target 3.0", shader, "the glow must run on GLES 3.0.");
            foreach (var banned in new[] { "GrabPass", "_CameraOpaqueTexture", "_CameraDepthTexture", "SampleSceneDepth",
                         "DeclareDepthTexture", "DeclareOpaqueTexture", "SAMPLE_TEXTURE", "TEXTURE2D", "tex2D" })
                Assert.IsFalse(shader.Contains(banned), $"the mobile glow reads {banned}: it must read no screen, depth or texture.");
        }

        [Test]
        public void Waves_StayLocalAndUnderTheBloom()
        {
            Assert.AreEqual(1f, BlackHoleGlow.ReachFade(0f, 220f), 1e-6f);
            Assert.AreEqual(1f, BlackHoleGlow.ReachFade(0.6f * 220f, 220f), 1e-6f);
            Assert.AreEqual(0f, BlackHoleGlow.ReachFade(220f, 220f), 1e-6f);
            Assert.AreEqual(0f, BlackHoleGlow.ReachFade(5000f, 220f), 1e-6f);
            float prev = 1f;
            for (float f = 0f; f <= 260f; f += 1f)
            {
                float v = BlackHoleGlow.ReachFade(f, 220f);
                Assert.LessOrEqual(v, prev + 1e-6f, "the reach fade must only ever ease out.");
                prev = v;
            }

            var config = LoadConfig();
            Assert.Less(config.MobileWaveGlow, BloomThreshold, "a shell's light ring would bloom into a flash.");
            Assert.Less(config.MobileWaveGlow * config.GwGlow * 1.5f, BloomThreshold, "a ripple's crest would bloom into a flash.");

            var wave = new BlackHoleWaves.Wave { Kind = BlackHoleWaves.Kind.Gravitational, Centre = Vector3.zero, Axis = Vector3.up, Mass = 1f };
            Assert.IsTrue(BlackHoleGlow.RippleShape(wave, 0.5f, config, out float inner, out float outer, out var shape,
                out var w, out var axis, out float fade));
            Assert.AreEqual(Mathf.Min(config.GwSpeed * 0.5f, config.MobileWaveReach), outer, 1e-3f);
            Assert.AreEqual(0f, inner, 1e-6f);
            Assert.AreEqual(1f, axis.w, "the sin² pattern needs the axis the holes met along.");
            Assert.AreEqual(0.5f, w.x, 1e-6f);
            Assert.Greater(shape.z, 0f);
            Assert.Greater(fade, 0f);
            float pastReach = 1.01f * config.MobileWaveReach / config.GwSpeed;
            Assert.IsFalse(BlackHoleGlow.RippleShape(wave, pastReach, config, out _, out _, out _, out _, out _, out _),
                "a wave whose front has left the reach must not draw: the mobile waves stay local.");
        }

        /// <summary>The studio's values stay in the config for the Studio lens look (A/B), documented, not deleted.</summary>
        [Test]
        public void Config_KeepsTheStudioLensParameters()
        {
            string asset = File.ReadAllText(ConfigAssetPath);
            foreach (var key in new[] { "warpEnabled", "lensStrength", "whiteCoreSkyMix", "shellBend", "gwAmp", "gwPrism", "gwShake",
                         "shadowSize", "whiteCoreSize", "whiteCoreBrightness", "photonRingGlow", "photonRingWidth", "gwSpeed",
                         "gwPeriod", "gwRingdown", "gwGlow", "whiteLensStrength" })
                Assert.IsTrue(Regex.IsMatch(asset, $@"^  {key}: ", RegexOptions.Multiline), $"BlackHoleConfig.asset lost {key}.");
        }
    }
}
#endif
