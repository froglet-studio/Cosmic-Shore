#if UNITY_EDITOR
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using CosmicShore.Utility;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Gates for the Dolphin's Echo Sight now that it is visible to every player
    /// (2026-08-19). Each of these is a drift risk that no compiler and no code review
    /// catches, because both halves read correct on their own.
    ///
    /// The COMPOSITION maths — that your own sight is bit-identical to what it was before
    /// peers existed, and that overlapping rivals never brighten each other — is proven
    /// separately by compiling and running the shipped HLSL:
    /// <c>python3 Tools/Shaders/verify_prism_sight_composition.py</c>. That cannot live here
    /// (an edit-mode test cannot execute HLSL), so it lives beside the shader and these tests
    /// cover what a C# assembly CAN see: that the two sides still agree about their contract.
    /// </summary>
    public class PrismDestructionSightTests
    {
        const string HlslPath = "Assets/_Graphics/Materials/Graphs/PrismDestructionSight.hlsl";

        static string ReadHlsl()
        {
            Assert.IsTrue(File.Exists(HlslPath), $"{HlslPath} is missing — the sight has no GPU half.");
            // Normalized because a Windows checkout delivers CRLF and every pattern below is
            // line-oriented (Docs/asset-surgery: `$` does not match before `\r` in .NET).
            return File.ReadAllText(HlslPath).Replace("\r\n", "\n");
        }

        [Test]
        public void PeerSlotCount_MatchesTheShaderArrayLength()
        {
            var m = Regex.Match(ReadHlsl(), @"^#define PRISM_SIGHT_PEER_SLOTS (\d+)\s*$", RegexOptions.Multiline);
            Assert.IsTrue(m.Success,
                "PRISM_SIGHT_PEER_SLOTS is not defined in the sight HLSL — the peer arrays are declared at " +
                "that length, so it must exist and must be a literal.");

            int shaderSlots = int.Parse(m.Groups[1].Value);
            Assert.AreEqual(shaderSlots, PrismDestructionSight.PeerSlots,
                $"PrismDestructionSight.PeerSlots ({PrismDestructionSight.PeerSlots}) and the shader's " +
                $"PRISM_SIGHT_PEER_SLOTS ({shaderSlots}) have drifted. The C# writes fixed-length arrays " +
                "into globals the shader declares at its own length: too few and the tail of the bank is " +
                "whatever the previous frame left there, too many and Unity rejects the write. They are " +
                "one number in two files — change both.");
        }

        [Test]
        public void PeerBank_IsBigEnoughForEveryDolphinOnlyRoster()
        {
            // The bank has to hold every OTHER pilot who could be holding a sight, so the bound is
            // the largest roster of any mode a Dolphin can fly in, minus the viewer. Read from the
            // authored game assets rather than restated here: raising a mode's player count is
            // exactly the change that would silently start dropping rivals' marks.
            string[] dolphinModes =
            {
                "Assets/_SO_Assets/Games/ArcadeGameRampage.asset",
                "Assets/_SO_Assets/Games/ArcadeGameBends.asset",
            };

            int worstRoster = 0;
            foreach (var path in dolphinModes)
            {
                Assert.IsTrue(File.Exists(path), $"{path} is missing — has a Dolphin-only mode been renamed?");
                var m = Regex.Match(File.ReadAllText(path).Replace("\r\n", "\n"),
                                    @"^\s*MaxPlayersAllowed: (\d+)\s*$", RegexOptions.Multiline);
                Assert.IsTrue(m.Success, $"{path} does not declare MaxPlayersAllowed.");
                worstRoster = System.Math.Max(worstRoster, int.Parse(m.Groups[1].Value));
            }

            Assert.GreaterOrEqual(PrismDestructionSight.PeerSlots, worstRoster - 1,
                $"A Dolphin-only mode seats {worstRoster} pilots, so up to {worstRoster - 1} rivals can hold " +
                $"a sight at once, but the peer bank holds {PrismDestructionSight.PeerSlots}. Raise " +
                "PrismDestructionSight.PeerSlots AND PRISM_SIGHT_PEER_SLOTS together, or the extra pilots' " +
                "marks are silently dropped — and which ones get dropped depends on dictionary order, so " +
                "different players would see different arenas.");
        }

        [Test]
        public void ShaderDeclaresEveryGlobalTheRegistryPublishes()
        {
            string hlsl = ReadHlsl();

            // Names, not values: the C# binds these by string through Shader.PropertyToID, so a
            // typo on either side fails SILENTLY - the write goes nowhere and the sight simply
            // never appears for anyone but its holder.
            string[] arrays = { "_PrismSightPeerApex", "_PrismSightPeerAxis", "_PrismSightPeerGape", "_PrismSightPeerTint" };
            foreach (var name in arrays)
                Assert.IsTrue(Regex.IsMatch(hlsl, $@"^float4 {Regex.Escape(name)}\[PRISM_SIGHT_PEER_SLOTS\];\s*$",
                                            RegexOptions.Multiline),
                    $"{name} is not declared as a float4[PRISM_SIGHT_PEER_SLOTS] at file scope in {HlslPath}. " +
                    "ShaderGraph has no array property type, so these must be declared in the HLSL itself — " +
                    "and outside every CBUFFER, since an array inside UnityPerMaterial breaks SRP batching.");

            Assert.IsTrue(Regex.IsMatch(hlsl, @"^float  _PrismSightPeerCount;\s*$", RegexOptions.Multiline),
                "_PrismSightPeerCount is not declared in the sight HLSL. It is the master sentinel: " +
                "unpublished globals read as zero and the peer loop must not execute at all.");
        }

        [Test]
        public void OwnSight_StillWinsOutrightOverEveryPeer()
        {
            string hlsl = ReadHlsl();

            // The law: a prism the local pilot's own cone covers is painted by that cone and
            // nothing else, so the instrument they are aiming with reads the same in every match.
            // In the shader that is one `return` - and it is exactly the line a later refactor
            // toward "one unified accumulation" would remove, which is how the pilot's own sight
            // would start blending with a rival's without anything looking wrong in the diff.
            var own = Regex.Match(hlsl,
                @"Color = BaseColor \+ PRISM_SIGHT_COLOR \* \(own \* PRISM_SIGHT_GAIN\);\s*\n\s*return;",
                RegexOptions.Multiline);

            Assert.IsTrue(own.Success,
                "The own-sight branch in " + HlslPath + " no longer paints with PRISM_SIGHT_COLOR and " +
                "returns immediately. That early return IS the rule that a rival sweeping across your cone " +
                "can never recolour it, and it is what makes the local look bit-identical to the pre-peer " +
                "shader (proven by Tools/Shaders/verify_prism_sight_composition.py). If the composition was " +
                "deliberately changed, re-run that script and update this test with the new guarantee.");

            // ...and it must come BEFORE the peer loop, or the return is unreachable and the whole
            // guarantee inverts without changing a character of the expression above.
            int loop = hlsl.IndexOf("for (int i = 0; i < peerCount; i++)", System.StringComparison.Ordinal);
            Assert.Greater(loop, own.Index,
                "The own-sight branch now runs after the peer loop. It must run first: the peers' " +
                "contribution is what the early return exists to skip.");
        }
    }
}
#endif
