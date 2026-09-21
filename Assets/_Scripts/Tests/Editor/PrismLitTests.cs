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
    public class PrismLitTests
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
            Assert.AreEqual(shaderSlots, PrismLit.Slots,
                $"PrismLit.Slots ({PrismLit.Slots}) and the shader's " +
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

            Assert.GreaterOrEqual(PrismLit.Slots, worstRoster - 1,
                $"A Dolphin-only mode seats {worstRoster} pilots, so up to {worstRoster - 1} rivals can hold " +
                $"a sight at once, but the peer bank holds {PrismLit.Slots}. Raise " +
                "PrismLit.Slots AND PRISM_SIGHT_PEER_SLOTS together, or the extra pilots' " +
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
            string[] arrays = { "_PrismSightPeerApex", "_PrismSightPeerAxis", "_PrismSightPeerGape", "_PrismSightPeerTint", "_PrismSightPeerShape" };
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

        // ------------------------------------------------------------------
        // THE DOMAIN GATE
        // ------------------------------------------------------------------
        //
        // A gated light reaches one domain's mass. Three separate things have to agree for that
        // to be true and all three fail SILENTLY on their own: the shader has to read the gate,
        // both prism graphs have to feed the prism's own domain into the node, and ThemeManager
        // has to stamp it. Any one of them missing leaves a gated light reaching NOTHING, which
        // looks exactly like a producer nobody wired.

        const string ThemeManagerPath = "Assets/_Scripts/Controller/Managers/ThemeManager.cs";
        static readonly string[] PrismGraphs =
        {
            "Assets/_Graphics/Materials/Graphs/BlockGraph.shadergraph",
            // NOT the debris graph, despite the name: this is also the material the PLAIN
            // TRANSPARENT tier of a LIVE prism wears (TransparentPrismMaterial), while the
            // shielded/super-shielded/danger transparent variants are on BlockGraph. A gate wired
            // into only one of the two would silently exclude every plain transparent prism.
            "Assets/_Graphics/Materials/Graphs/ExplodingBlockGraph.shadergraph",
        };

        static string ReadText(string path)
        {
            Assert.IsTrue(File.Exists(path), $"{path} is missing.");
            return File.ReadAllText(path).Replace("\r\n", "\n");
        }

        /// <summary>The one concatenated JSON document in a .shadergraph that contains a needle.</summary>
        static string GraphBlock(string graph, string needle)
        {
            foreach (var block in graph.Split(new[] { "\n\n" }, System.StringSplitOptions.None))
                if (block.Contains(needle))
                    return block;
            return null;
        }

        [Test]
        public void ShaderReadsTheDomainGateBeforeAnyGeometry()
        {
            string hlsl = ReadHlsl();

            Assert.IsTrue(Regex.IsMatch(hlsl, @"float\s+Domain,\s*//[^\n]*\n\s*out float3 Color\)"),
                "The sight's entry point no longer takes `float Domain` as its LAST INPUT, immediately " +
                "before `out float3 Color`. A file-mode Custom Function node builds its call as all " +
                "inputs then all outputs, so a parameter declared after the `out` makes every prism " +
                "material render UNMATERIALED with nothing in the console " +
                "(Tools/Build/check_shadergraph_custom_function_signatures.py is the gate).");

            var gate = Regex.Match(hlsl, @"if \(tag\.y > 0\.0 && tag\.y != Domain\)\s*\n\s*continue;");
            Assert.IsTrue(gate.Success,
                "The per-light domain gate is gone from " + HlslPath + ". _PrismSightPeerShape[i].y " +
                "carries the domain a light is restricted to (0 = no gate) and the prism's own " +
                "domain arrives as the Domain parameter; without the test, the explosion passthrough " +
                "lights the opposing-domain mass it is in the middle of destroying.");

            int fill = hlsl.IndexOf("float w = PrismLitFill(", System.StringComparison.Ordinal);
            Assert.Greater(fill, gate.Index,
                "The domain gate now runs after the volume test. It must run first: rejecting a foreign " +
                "prism for one compare is the whole reason the gate is cheap.");
        }

        [Test]
        public void BothPrismGraphsFeedTheirOwnDomainIntoTheSight()
        {
            foreach (var path in PrismGraphs)
            {
                string graph = ReadText(path);

                // 1. the property exists and is PER-MATERIAL (m_GeneratePropertyBlock), which is
                //    what puts it in UnityPerMaterial where material.SetFloat can reach it. A
                //    non-generated property is a Shader.SetGlobal uniform instead - one value for
                //    every domain at once, which is no gate at all.
                string prop = GraphBlock(graph, "\"_PrismLitDomain\"");
                Assert.IsNotNull(prop, $"{path} declares no _PrismLitDomain property. ThemeManager stamps " +
                    "it per domain and the sight reads it as the prism's own domain.");
                Assert.IsTrue(prop.Contains("\"m_GeneratePropertyBlock\": true"),
                    $"{path}'s _PrismLitDomain is not a generated (per-material) property, so it lands " +
                    "outside UnityPerMaterial and material.SetFloat cannot reach it — the gate would " +
                    "read one global value for every domain at once.");

                // 2. the node has a Domain input slot, and 3. something is WIRED to it. An
                //    unwired Custom Function slot is legal and falls back to its default 0, which
                //    the shader reads as "this prism has no domain" - so every gated light would
                //    silently reach nothing at all.
                string node = GraphBlock(graph, "\"m_FunctionName\": \"PrismDestructionSight\"");
                Assert.IsNotNull(node, $"{path} no longer carries the PrismDestructionSight node.");
                string nodeId = Regex.Match(node, @"""m_ObjectId"": ""([0-9a-f]{32})""").Groups[1].Value;
                Assert.IsNotEmpty(nodeId, $"could not read the sight node's object id in {path}.");

                string slot = GraphBlock(graph, "\"m_ShaderOutputName\": \"Domain\"");
                Assert.IsNotNull(slot, $"{path}'s sight node has no Domain input slot.");
                string slotId = Regex.Match(slot, @"""m_Id"": (\d+)").Groups[1].Value;
                Assert.IsNotEmpty(slotId, $"could not read the Domain slot's id in {path}.");

                Assert.IsTrue(Regex.IsMatch(graph,
                        @"""m_InputSlot"":\s*\{\s*""m_Node"":\s*\{\s*""m_Id"":\s*""" + nodeId +
                        @"""\s*\},\s*""m_SlotId"":\s*" + slotId + @"\s*\}"),
                    $"{path} has a Domain slot on the sight node with NOTHING wired into it. An unwired " +
                    "Custom Function slot falls back to its default 0, which the shader reads as \"this " +
                    "prism has no domain\" — so every domain-gated light would reach nothing and the " +
                    "explosion passthrough would look like a producer nobody wired.");
            }
        }

        [Test]
        public void ThemeManagerStampsEveryPrismTierWithItsDomain()
        {
            string theme = ReadText(ThemeManagerPath);

            Assert.IsTrue(theme.Contains("_PrismLitDomain"),
                ThemeManagerPath + " no longer stamps _PrismLitDomain. The per-domain material clones " +
                "are the ONLY place a prism's domain reaches the shader, so without the stamp every " +
                "prism reads 0 and no domain-gated light reaches anything.");

            // It has to be on the TIER pair, not on one material: PrismStateManager swaps a prism
            // between the opaque and transparent variants of its tier, so a stamp on only one of
            // them makes a prism drop out of every gated light for half its states.
            Assert.IsTrue(Regex.IsMatch(theme,
                    @"opaque\.SetFloat\(PrismLitDomainId[\s\S]{0,400}?transparent\.SetFloat\(PrismLitDomainId"),
                "ThemeManager.PaintPrismTier stamps _PrismLitDomain on only one of the tier's two " +
                "materials. PrismStateManager swaps a prism between the opaque and transparent variant " +
                "of its tier, so both have to carry the domain or a prism falls out of every gated " +
                "light in half its states.");
        }
    }
}
#endif
