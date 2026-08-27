#if UNITY_EDITOR
using CosmicShore.Utility;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The determinism contract of <see cref="BugSignature"/> — the shared fingerprint core of the
    /// Bug Ledger (FrogletTools ▸ Diagnostics, <c>Docs/DIAGNOSTICS.md</c>) and, later, of the
    /// in-game reporter that will write the same ids into UGS player data.
    ///
    /// The whole design leans on ONE property: the same bug must hash to the same id on every
    /// machine, every OS, every run — that is what makes one issue file per bug possible, what
    /// dedupes a thousand repeats into one issue, and what will let device hits merge into editor
    /// issues. Volatile parts of a message (counts, instance ids, positions, hex, absolute
    /// checkout paths in BOTH stack-frame formats) must therefore normalize away, while genuinely
    /// different bugs must stay apart. These tests pin each half; the cross-OS frame case exists
    /// because the first implementation failed it (unity-style "(at …)" frames kept their
    /// machine-local prefix while mono-style " in …" frames were stripped).
    /// </summary>
    public class BugSignatureTests
    {
        // ── Normalization ────────────────────────────────────────────────────────

        [Test]
        public void Normalize_CollapsesDigitsFloatsAndHex()
        {
            Assert.AreEqual("pos (#.#, -#.#, #) id 0x# n=#",
                BugSignature.NormalizeText("pos (12.5, -3.75, 0) id 0xDEADBEEF n=42", 300));
        }

        [Test]
        public void Normalize_TakesFirstLineOnly_AndHandlesEmpty()
        {
            Assert.AreEqual("first", BugSignature.NormalizeText("first\nsecond", 300));
            Assert.AreEqual("", BugSignature.NormalizeText(null, 300));
            Assert.AreEqual("", BugSignature.NormalizeText("", 300));
        }

        // ── Cross-machine determinism ────────────────────────────────────────────

        [Test]
        public void ErrorId_SameBug_SameIdAcrossMachines_MonoStyleFrames()
        {
            const string linux = "at CosmicShore.Gameplay.QuestTracker.Award (System.Int32 xp) [0x0001a] in /home/alice/dev/Cosmic-Shore/Assets/_Scripts/System/Quest/QuestTracker.cs:118";
            const string windows = "at CosmicShore.Gameplay.QuestTracker.Award (System.Int32 xp) [0x0002b] in C:/Users/bob/Cosmic-Shore/Assets/_Scripts/System/Quest/QuestTracker.cs:118";

            var idA = BugSignature.ErrorId("Quest reward failed for id 42 (retry 3)", linux, LogType.Error, out var sigA);
            var idB = BugSignature.ErrorId("Quest reward failed for id 977 (retry 12)", windows, LogType.Error, out var sigB);

            Assert.AreEqual(idA, idB, "digit collapse + [0x…] + absolute-path strip must agree across machines");
            Assert.AreEqual(sigA, sigB);
        }

        [Test]
        public void ErrorId_SameBug_SameIdAcrossMachines_UnityStyleFrames()
        {
            const string linux = "UnityEngine.Debug:LogError (object)\nCosmicShore.Gameplay.QuestTracker:Award (int) (at /home/alice/dev/Cosmic-Shore/Assets/_Scripts/System/Quest/QuestTracker.cs:118)";
            const string windows = "UnityEngine.Debug:LogError (object)\nCosmicShore.Gameplay.QuestTracker:Award (int) (at Assets\\_Scripts\\System\\Quest\\QuestTracker.cs:118)";

            Assert.AreEqual(
                BugSignature.ErrorId("boom", linux, LogType.Exception, out _),
                BugSignature.ErrorId("boom", windows, LogType.Exception, out _),
                "the '(at path:line)' frame format must strip its absolute prefix too — this exact case shipped broken once");
        }

        [Test]
        public void TopUserFrame_PrefersCosmicShoreFrame_AndKeepsRepoRelativeLocation()
        {
            const string stack = "UnityEngine.Debug:LogError (object)\nat CosmicShore.Foo.Bar () [0x00012] in /somewhere/Cosmic-Shore/Assets/_Scripts/Foo.cs:12";
            var frame = BugSignature.TopUserFrame(stack);
            StringAssert.Contains("CosmicShore.Foo.Bar", frame);
            StringAssert.Contains("Assets/_Scripts/Foo.cs:#", frame);
            StringAssert.DoesNotContain("/somewhere/", frame);
        }

        // ── Distinctness ─────────────────────────────────────────────────────────

        [Test]
        public void ErrorId_LogTypeSeparatesSignatures()
        {
            const string stack = "at CosmicShore.Foo.Bar () in Assets/X.cs:1";
            Assert.AreNotEqual(
                BugSignature.ErrorId("boom", stack, LogType.Error, out _),
                BugSignature.ErrorId("boom", stack, LogType.Exception, out _));
        }

        [Test]
        public void ErrorId_DifferentMessages_DifferentIds()
        {
            Assert.AreNotEqual(
                BugSignature.ErrorId("NullReferenceException: a", null, LogType.Exception, out _),
                BugSignature.ErrorId("IndexOutOfRangeException: b", null, LogType.Exception, out _));
        }

        [Test]
        public void ErrorId_NullInputs_AreStableAndShaped()
        {
            var id = BugSignature.ErrorId(null, null, LogType.Error, out _);
            Assert.AreEqual(id, BugSignature.ErrorId(null, null, LogType.Error, out _));
            StringAssert.StartsWith("E-", id);
            Assert.AreEqual(12, id.Length);
        }

        // ── Tool findings ────────────────────────────────────────────────────────

        [Test]
        public void ToolId_StableAcrossRuns_AndDistinctPerToolAndTitle()
        {
            var a1 = BugSignature.ToolId("Audit Vessel Skimmers", "Serpent: NearFieldSkimmer does not skim", out var sig);
            var a2 = BugSignature.ToolId("Audit Vessel Skimmers", "Serpent: NearFieldSkimmer does not skim", out _);
            Assert.AreEqual(a1, a2, "re-running a tool must refresh the same issue, never duplicate it");
            StringAssert.StartsWith("T-", a1);
            StringAssert.StartsWith("Tool|Audit Vessel Skimmers|", sig);

            Assert.AreNotEqual(a1, BugSignature.ToolId("Audit Vessel Skimmers", "Manta: NearFieldSkimmer does not skim", out _));
            Assert.AreNotEqual(a1, BugSignature.ToolId("Some Other Tool", "Serpent: NearFieldSkimmer does not skim", out _));
        }
    }
}
#endif
