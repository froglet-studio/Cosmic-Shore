#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Structural gates for Docs/PRISM_ANIMATION.md §5 C11 — spindle evaporate /
    /// condense on the GPU clock. A withering creature leaves its body prisms
    /// standing as a skeleton, so the spindles ARE the wither visual. An unwired
    /// graph or a WaitForSeconds cascade between ForceWither calls is a silent
    /// snap / a per-frame CPU regression. These tests fail that class without
    /// needing play mode.
    /// </summary>
    public class PrismSpindleDeathClockTests
    {
        const string WiringTool = "Tools/Shaders/wire_prism_spindle_death_clock.py";
        const string FunctionClock = "PrismDeathClock";
        const string SpindlePath = "Assets/_Scripts/Controller/Environment/FloraAndFauna/Spindle.cs";
        const string LightFaunaPath = "Assets/_Scripts/Controller/Environment/FloraAndFauna/LightFauna.cs";
        const string LifeFormPath = "Assets/_Scripts/Controller/Environment/FloraAndFauna/LifeForm.cs";
        const string CellPath = "Assets/_Scripts/Controller/Environment/Cell.cs";
        const string HlslPath = "Assets/_Graphics/Materials/Graphs/PrismClockAnimation.hlsl";

        static readonly string[] SpindleGraphs =
        {
            "Assets/_Graphics/Materials/Graphs/SpindleGraph.shadergraph",
            "Assets/_Graphics/Materials/Graphs/AnimatedSpindleGraph.shadergraph",
        };

        static readonly string[] LivePrismGraphs =
        {
            "Assets/_Graphics/Materials/Graphs/BlockGraph.shadergraph",
            "Assets/_Graphics/Materials/Graphs/ExplodingBlockGraph.shadergraph",
        };

        static readonly string[] PerInstanceProps =
        {
            "_DeathStartTime", "_DeathDuration", "_DeathDirection",
        };

        [Test]
        public void SpindleGraphs_DeclareDeathClockAndUnexposedPrismClock()
        {
            foreach (var graphPath in SpindleGraphs)
            {
                Assert.IsTrue(File.Exists(graphPath), $"{graphPath} is missing.");
                string text = File.ReadAllText(graphPath).Replace("\r\n", "\n");
                var blocks = text.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var prop in PerInstanceProps)
                {
                    var block = blocks.FirstOrDefault(b =>
                        (b.Contains($"\"m_DefaultReferenceName\": \"{prop}\"") ||
                         b.Contains($"\"m_OverrideReferenceName\": \"{prop}\"")) &&
                        b.Contains("ShaderProperty"));
                    Assert.IsNotNull(block,
                        $"{graphPath} does not declare {prop} — run {WiringTool}.");
                    Assert.IsTrue(block.Contains("\"m_GeneratePropertyBlock\": true"),
                        $"{graphPath}: {prop} is UNEXPOSED — a per-spindle stamp cannot reach it.");
                    Assert.IsTrue(block.Contains("\"hlslDeclarationOverride\": 3"),
                        $"{graphPath}: {prop} is not Hybrid Per Instance — every spindle would render the " +
                        "material default and the stamp would go nowhere.");
                }

                var clockBlock = blocks.FirstOrDefault(b =>
                    b.Contains("\"m_DefaultReferenceName\": \"_PrismClock\"") &&
                    b.Contains("ShaderProperty"));
                Assert.IsNotNull(clockBlock,
                    $"{graphPath} has no _PrismClock property — Time node is not a legal clock.");
                Assert.IsTrue(clockBlock.Contains("\"m_GeneratePropertyBlock\": false"),
                    $"{graphPath}: _PrismClock must be an UNEXPOSED global. An exposed clock is a " +
                    "per-material Time, which desyncs from PrismClock.Now.");

                Assert.IsTrue(text.Contains($"\"m_FunctionName\": \"{FunctionClock}\""),
                    $"{graphPath} has no {FunctionClock} Custom Function node. Run {WiringTool}.");
            }
        }

        [Test]
        public void LivePrismGraphs_DoNotCarrySpindleDeathClock()
        {
            foreach (var graphPath in LivePrismGraphs)
            {
                Assert.IsTrue(File.Exists(graphPath), $"{graphPath} is missing.");
                string text = File.ReadAllText(graphPath);
                Assert.IsFalse(text.Contains($"\"m_FunctionName\": \"{FunctionClock}\""),
                    $"{graphPath} must not carry {FunctionClock} — that is the spindle fade, " +
                    "not a live-prism vertex path. Do not dump spindle CFs onto BlockGraph Specs.");
            }
        }

        [Test]
        public void Spindle_StampsOnceAndHasNoPerFrameFade()
        {
            Assert.IsTrue(File.Exists(SpindlePath), $"{SpindlePath} is missing.");
            string src = File.ReadAllText(SpindlePath);
            Assert.IsFalse(src.Contains("SetFadeValue"),
                "Spindle.SetFadeValue is back — that is the per-frame MPB write C11 retired.");
            Assert.IsFalse(src.Contains("EvaporateCoroutine"),
                "Spindle.EvaporateCoroutine is back — fade must be a stamp + scheduler settle.");
            Assert.IsFalse(src.Contains("CondenseCoroutine"),
                "Spindle.CondenseCoroutine is back — fade must be a stamp + scheduler settle.");
            Assert.IsFalse(Regex.IsMatch(src, @"deathAnimation\s*\+="),
                "Spindle still advances a deathAnimation accumulator — that is the Time.deltaTime fade.");
            Assert.IsFalse(src.Contains("Time.deltaTime"),
                "Spindle.cs writes Time.deltaTime — the fade must run on _PrismClock with zero per-frame CPU.");
            Assert.IsTrue(src.Contains("StampDeathFade"),
                "Spindle lost StampDeathFade — evaporate/condense have nowhere to stamp.");
            Assert.IsTrue(src.Contains("PrismTimerManager"),
                "Spindle no longer schedules a settle through PrismTimerManager — Destroy would never run " +
                "and LifeForm.DieCoroutine's empty-tracker wait would stall.");
            Assert.IsTrue(src.Contains("ForceWither(float evaporateDelay"),
                "ForceWither must take an evaporateDelay so ordered wither can stamp i * interval " +
                "in one pass instead of WaitForSeconds between calls.");
        }

        [Test]
        public void OrderedWither_StampsOffsetsInOnePass()
        {
            Assert.IsTrue(File.Exists(LightFaunaPath), $"{LightFaunaPath} is missing.");
            string fauna = File.ReadAllText(LightFaunaPath);
            Assert.IsTrue(fauna.Contains("ForceWither(i * interval)"),
                "LightFauna.WitherCoroutine must stamp ForceWither(i * interval) — the ecology-LOCKED " +
                "order is a StartTime offset, never a per-frame cascade.");
            Assert.IsTrue(fauna.Contains("LeaveSkeleton()"),
                "LeaveSkeleton must still run before the wither — a body prism is parented to a spindle.");
            Assert.IsTrue(fauna.Contains("ReleaseHeart()"),
                "ReleaseHeart is gone — starvation's crystal would never become collectable.");

            int wither = fauna.IndexOf("IEnumerator WitherCoroutine()", StringComparison.Ordinal);
            Assert.Greater(wither, 0, "WitherCoroutine not found.");
            int next = fauna.IndexOf("IEnumerator DevouredCoroutine", wither + 1, StringComparison.Ordinal);
            string body = fauna.Substring(wither, (next > wither ? next : fauna.Length) - wither);
            Assert.IsFalse(Regex.IsMatch(body, @"ForceWither\(\)\s*;"),
                "WitherCoroutine still calls ForceWither() with no delay inside the ordered loop — " +
                "that collapses the stagger back onto one frame.");
            Assert.IsTrue(body.Contains("coreDelay") && body.Contains("ReleaseHeart()"),
                "ReleaseHeart must wait until the wither has reached the core (count * interval), " +
                "not run at stamp time.");
            int stamp = body.IndexOf("ForceWither(i * interval)", StringComparison.Ordinal);
            int release = body.IndexOf("ReleaseHeart()", StringComparison.Ordinal);
            Assert.Greater(release, stamp,
                "ReleaseHeart must run AFTER the ordered stamps — starvation's crystal is collectable " +
                "when the wither reaches the core.");

            Assert.IsTrue(File.Exists(LifeFormPath), $"{LifeFormPath} is missing.");
            string flora = File.ReadAllText(LifeFormPath);
            Assert.IsFalse(flora.Contains("WitherOutwardCoroutine"),
                "LifeForm.WitherOutwardCoroutine is back — flora joust must stamp i * witherRingInterval " +
                "inline, same as LightFauna.");
            Assert.IsTrue(flora.Contains("ForceWither(i * witherRingInterval)"),
                "LifeForm.WitherToSkeleton must stamp ForceWither(i * witherRingInterval) in one pass.");
        }

        [Test]
        public void Wither_DoesNotReuseConveyorHide()
        {
            string fauna = File.ReadAllText(LightFaunaPath);
            Assert.IsFalse(fauna.Contains("BeginBulkTransport"),
                "LightFauna must not BeginBulkTransport — that is C8's off-screen conveyor gate. " +
                "A wither is a VISIBLE death.");
            Assert.IsFalse(fauna.Contains("HideForTransport"),
                "LightFauna must not HideForTransport — spindles fade in place.");

            string spindle = File.ReadAllText(SpindlePath);
            Assert.IsFalse(spindle.Contains("BeginBulkTransport"),
                "Spindle must not BeginBulkTransport.");
            Assert.IsFalse(spindle.Contains("HideForTransport"),
                "Spindle must not HideForTransport.");

            // Cell still owns C9 suction; this only asserts the wither files stay clean.
            Assert.IsTrue(File.Exists(CellPath));
        }

        [Test]
        public void Hlsl_DeclaresDeathClockAndDurationZeroIdentity()
        {
            Assert.IsTrue(File.Exists(HlslPath), $"{HlslPath} is missing.");
            string hlsl = File.ReadAllText(HlslPath);
            Assert.IsTrue(hlsl.Contains("void PrismDeathClock_float("),
                "PrismDeathClock_float is missing — spindle graphs would stamp State nobody computes.");
            int clock = hlsl.IndexOf("void PrismDeathClock_float(", StringComparison.Ordinal);
            Assert.Greater(clock, 0);
            int next = hlsl.IndexOf("void ", clock + "void PrismDeathClock_float(".Length, StringComparison.Ordinal);
            string body = hlsl.Substring(clock, (next > clock ? next : hlsl.Length) - clock);
            Assert.IsTrue(body.Contains("Duration <= 0.0") || body.Contains("Duration <= 0"),
                "Duration <= 0 must return LegacyState — unstamped spindles (LegacyState default 0) " +
                "stay visible. A missing identity makes every spindle evaporate on spawn.");
            Assert.IsTrue(body.Contains("Direction < 0.0") || body.Contains("Direction < 0"),
                "Direction < 0 must condense (1→0). Without it evaporate and condense are the same fade.");
        }
    }
}
#endif
