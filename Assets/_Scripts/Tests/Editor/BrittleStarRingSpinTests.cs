using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Brittle Star's ring under a SPINNING right stick (Garrett, 2026-11-03; lab
    /// <c>Docs/Studios/BrittleStarRingLab.html</c>, "Forward + motion smear"):
    /// <list type="bullet">
    /// <item><see cref="BrittleStarRules.ForwardTurn"/> moves the ring frame for frame exactly as the old nearest-turret
    /// rule did while the stick is slower than ringTurnRate (the half that must not change), and above it turns
    /// flat out, forward, never back (the wagon wheel it fixes, pinned on the old rule).</item>
    /// <item>The motion smear's copies (<see cref="BrittleStarRules.SmearCopies"/>, <see cref="BrittleStarRules.SmearCopyBack"/>)
    /// and its shader: an AVERAGE, weights summing to one, blended premultiplied, so it adds no brightness.</item>
    /// <item>The shipped config: the lab's pick (ringTurnRate 1080) and the smear shader wired.</item>
    /// <item>The smear's ring layer can draw every material the hull wears (2026-11-10: it drew none, so the ring vanished
    /// while it turned).</item>
    /// </list>
    /// The numbers were measured first on a line-for-line JavaScript copy of these rules against the lab.
    /// </summary>
    [TestFixture]
    public class BrittleStarRingSpinTests
    {
        const float SweepThreshold = 90f;
        const float SweepSmoothing = 0.06f;
        const float FlickDegrees = 120f;
        const string ConfigPath = "Assets/_SO_Assets/VesselActions/BrittleStar/BrittleStarConfig.asset";
        const string ShaderPath = "Assets/_Graphics/Materials/Graphs/BrittleStarRingSmear.shader";

        struct Run
        {
            public List<float> Phases;
            public float NetRevsPerSecond;    // after the first second
            public float BackwardFramesPerSecond;
        }

        /// <summary>The stick at full deflection, turning at <paramref name="revsPerSecond"/> (negative = the other way),
        /// sampled once a frame; the ring driven by the shipped rule or by the old one.</summary>
        static Run Spin(float revsPerSecond, float turnRate, int fps, float seconds, bool oldRule)
        {
            float dt = 1f / fps;
            float phase = 0f;
            int point = 0;
            var sweep = new BrittleStarRules.AimSweep();
            var run = new Run { Phases = new List<float>() };
            float net = 0f;
            int backward = 0;
            int frames = Mathf.RoundToInt(seconds * fps);
            for (int f = 0; f < frames; f++)
            {
                float a = Mathf.PI * 0.5f + 2f * Mathf.PI * revsPerSecond * (f + 1) * dt;
                Vector2 stick = new(Mathf.Cos(a), Mathf.Sin(a));
                float aim = BrittleStarRules.AimAngle(stick, 0.25f).Value;
                float before = phase;
                if (oldRule)
                {
                    point = BrittleStarRules.NearestPoint(aim, phase);
                    phase = Mathf.MoveTowards(phase, BrittleStarRules.PhaseToAim(aim, point, phase), turnRate * Mathf.Deg2Rad * dt);
                }
                else
                {
                    var step = sweep.Step(aim, dt, SweepSmoothing, FlickDegrees);
                    phase = BrittleStarRules.ForwardTurn(aim, phase, ref point, step, SweepThreshold, turnRate, dt);
                }
                run.Phases.Add(phase);
                if (f < fps) continue;
                float d = phase - before;
                net += d;
                if (d * Mathf.Sign(revsPerSecond) < -1e-6f) backward++;
            }
            float measured = seconds - 1f;
            run.NetRevsPerSecond = net / (2f * Mathf.PI) / measured;
            run.BackwardFramesPerSecond = backward / measured;
            return run;
        }

        // ------------------------------------------------------------------ the half that must not change

        [TestCase(1f, 540f)]
        [TestCase(1.4f, 540f)]
        [TestCase(0.25f, 540f)]
        [TestCase(-1f, 540f)]
        [TestCase(2f, 1080f)]
        public void BelowTheTurnRate_TheRingMovesExactlyAsTheNearestTurretRuleDid(float revsPerSecond, float turnRate)
        {
            var before = Spin(revsPerSecond, turnRate, 60, 3f, oldRule: true);
            var now = Spin(revsPerSecond, turnRate, 60, 3f, oldRule: false);
            for (int i = 0; i < before.Phases.Count; i++)
                Assert.AreEqual(before.Phases[i], now.Phases[i], 1e-5f, $"frame {i}: a stick the ring keeps up with moves it as before");
        }

        // ------------------------------------------------------------------ the wagon wheel

        [Test]
        public void TheOldRule_TurnsBackwardUnderAFastStick_TheBugThisFixes()
        {
            var fourTurns = Spin(4f, 540f, 60, 4f, oldRule: true);
            Assert.Greater(fourTurns.BackwardFramesPerSecond, 10f, "the nearest-turret rule turns back to the turret behind the aim");
            Assert.Less(Mathf.Abs(fourTurns.NetRevsPerSecond), 0.1f, "at 4 rev/s it barely moves: the static jitter");
        }

        [TestCase(4f, 540f)]
        [TestCase(6f, 540f)]
        [TestCase(3f, 540f)]
        [TestCase(4f, 1080f)]
        [TestCase(-4f, 1080f)]
        public void AboveTheTurnRate_TheRingTurnsFlatOutForward_NeverBack(float revsPerSecond, float turnRate)
        {
            var run = Spin(revsPerSecond, turnRate, 60, 4f, oldRule: false);
            Assert.AreEqual(0f, run.BackwardFramesPerSecond, "no frame turns against the stick");
            Assert.AreEqual(Mathf.Sign(revsPerSecond) * turnRate / 360f, run.NetRevsPerSecond, 0.01f,
                            "the ring turns at its cap, the stick's way");
        }

        [Test]
        public void AFlick_TakesTheNearestTurret()
        {
            float phase = 0f;
            int point = 0;
            var sweep = new BrittleStarRules.AimSweep();
            for (int f = 0; f < 30; f++)
                phase = BrittleStarRules.ForwardTurn(0f, phase, ref point, sweep.Step(0f, 1f / 60f, SweepSmoothing, FlickDegrees),
                                                     SweepThreshold, 1080f, 1f / 60f);
            var step = sweep.Step(Mathf.PI, 1f / 60f, SweepSmoothing, FlickDegrees);
            Assert.IsTrue(step.Flick, "a half-turn in one frame is a flick");
            BrittleStarRules.ForwardTurn(Mathf.PI, phase, ref point, step, SweepThreshold, 1080f, 1f / 60f);
            Assert.AreEqual(BrittleStarRules.NearestPoint(Mathf.PI, phase), point);
        }

        [Test]
        public void ReleasingTheStick_ForgetsTheSweep()
        {
            var sweep = new BrittleStarRules.AimSweep();
            for (int f = 0; f < 20; f++) sweep.Step(f * 0.2f, 1f / 60f, SweepSmoothing, FlickDegrees);
            Assert.Greater(sweep.RateDegrees, SweepThreshold);
            sweep.Step(null, 1f / 60f, SweepSmoothing, FlickDegrees);
            Assert.AreEqual(0f, sweep.RateDegrees);
            Assert.IsTrue(sweep.Step(0f, 1f / 60f, SweepSmoothing, FlickDegrees).Entered, "back out of the deadzone is a fresh aim");
        }

        // ------------------------------------------------------------------ the motion smear

        [Test]
        public void SmearCopies_NoneBelowTheThreshold_OnePerStep_Capped()
        {
            Assert.AreEqual(0, BrittleStarRules.SmearCopies(1f, 1f, 1.5f, 2.5f, 24), "a slow ring is drawn as it stands");
            Assert.AreEqual(0, BrittleStarRules.SmearCopies(30f, 0f, 1.5f, 2.5f, 24), "smear 0 is off");
            Assert.AreEqual(2, BrittleStarRules.SmearCopies(2f, 1f, 1.5f, 2.5f, 24), "at least two copies");
            Assert.AreEqual(12, BrittleStarRules.SmearCopies(-30f, 1f, 1.5f, 2.5f, 24), "one per 2.5 degrees, either way");
            Assert.AreEqual(24, BrittleStarRules.SmearCopies(180f, 1f, 1.5f, 2.5f, 24), "capped");
        }

        [Test]
        public void SmearCopyBack_RunsFromTheDrawnRingToAWholeArcBack()
        {
            const int copies = 9;
            Assert.AreEqual(0f, BrittleStarRules.SmearCopyBack(0, copies, 18f, 1f), "copy 0 is the ring as it stands");
            Assert.AreEqual(18f, BrittleStarRules.SmearCopyBack(copies - 1, copies, 18f, 1f), 1e-5f);
            Assert.AreEqual(-9f, BrittleStarRules.SmearCopyBack(copies - 1, copies, -18f, 0.5f), 1e-5f);
            for (int k = 1; k < copies; k++)
                Assert.AreEqual(18f / (copies - 1), BrittleStarRules.SmearCopyBack(k, copies, 18f, 1f)
                                                    - BrittleStarRules.SmearCopyBack(k - 1, copies, 18f, 1f), 1e-5f, "evenly spaced");
        }

        [Test]
        public void TheSmearShader_AveragesTheCopies_AndBlendsPremultiplied()
        {
            string shader = File.ReadAllText(ShaderPath);
            StringAssert.Contains("Blend One OneMinusSrcAlpha", shader, "premultiplied over: the mean adds no light of its own");
            StringAssert.Contains("sum / count", shader, "the copies are averaged: every one weighs 1 / count");
            StringAssert.Contains($"\"LightMode\" = \"{BrittleStarRingSmearPass.LightModeName}\"", shader);
            var size = Regex.Match(shader, @"#define RING_SMEAR_MAX_COPIES (\d+)");
            Assert.IsTrue(size.Success);
            Assert.AreEqual(BrittleStarRingSmearPass.MaxCopies, int.Parse(size.Groups[1].Value), "the shader's array holds every copy the pass sends");
        }

        // ------------------------------------------------------------------ the shipped config

        [Test]
        public void TheShippedConfig_IsTheLabsPick_WithTheSmearShaderWired()
        {
            string config = File.ReadAllText(ConfigPath);
            Assert.IsTrue(Regex.IsMatch(config, @"\n  ringTurnRate: 1080\n"), "ringTurnRate is the lab's pick (1080)");
            Assert.IsTrue(Regex.IsMatch(config, @"\n  ringSmear: 1\n"), "the smear is on");
            string meta = File.ReadAllText(ShaderPath + ".meta");
            string guid = Regex.Match(meta, @"guid: ([0-9a-f]{32})").Groups[1].Value;
            StringAssert.Contains($"ringSmearShader: {{fileID: 4800000, guid: {guid}, type: 3}}", config,
                                  "the smear shader is wired (an empty field draws no smear)");
        }

        // ------------------------------------------------------------------ the smear's ring layer

        /// <summary>The materials BrittleStar.fbx maps (Body, Domain, Engine, Window) and the domain ship material that
        /// replaces the Domain placeholder at runtime (VesselCustomization).</summary>
        static readonly string[] HullMaterials =
        {
            "Assets/_Graphics/Materials/BlueBaseVesselMaterial.mat",
            "Assets/_Graphics/Materials/GreenAccentVesselMaterial.mat",
            "Assets/_Graphics/Materials/GreenVesselMaterial.mat",
            "Assets/_Graphics/Materials/EngineVesselMaterial.mat",
            "Assets/_Graphics/Materials/ScreenVesselMaterial.mat",
        };

        [Test]
        public void TheSmearLayer_FindsTheForwardPassOnEveryHullMaterial()
        {
            // The smear redraws the ring with the hull's own materials. They wear VesselGraph, an Unlit Shader Graph whose
            // forward pass carries NO LightMode tag. A lookup by tag alone found no pass, the layer came out empty and the
            // hidden ring vanished whenever it turned (Garrett, 2026-11-10: "too dark to see").
            var lightMode = new ShaderTagId("LightMode");
            foreach (var path in HullMaterials)
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                Assert.IsNotNull(material, $"{path} is missing");
                int pass = BrittleStarRingSmearPass.ForwardPass(material);
                Assert.GreaterOrEqual(pass, 0, $"{path} ({material.shader.name}): the ring layer has no pass to draw it with");
                var mode = material.shader.FindPassTagValue(pass, lightMode);
                Assert.IsTrue(mode == ShaderTagId.none || mode.name == "UniversalForward" || mode.name == "UniversalForwardOnly"
                              || mode.name == "SRPDefaultUnlit",
                              $"{path}: pass {pass} is tagged '{mode.name}', not one URP's forward renderer draws");
            }
        }

        [Test]
        public void TheSmearLayer_NeverDrawsWithAPassTaggedForSomethingElse()
        {
            // Negative control: the smear's own shader has one pass, tagged for the smear pass only. It is no forward pass.
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            Assert.IsNotNull(shader, $"{ShaderPath} is missing");
            var material = new Material(shader);
            try { Assert.AreEqual(-1, BrittleStarRingSmearPass.ForwardPass(material)); }
            finally { Object.DestroyImmediate(material); }
        }
    }
}
