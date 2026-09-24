#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using CosmicShore.Editor;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The automated gate for the WAKE (Docs/PRISM_ANIMATION.md §4.7.3) — a warhead's SHOCKWAVE
    /// FRONT, the fourth citizen of §4.7's global-uniform shape and the second member of the
    /// high-poly morph family. Its failure modes are SILENT, exactly like the cradle's: a graph that
    /// lost the node renders every prism as before and nothing logs; a bank length that drifts
    /// between the C# and the HLSL leaves the tail of the bank unread; a config past the folding
    /// amplitude turns prisms inside out only where a crest happens to land; a grant added to a
    /// second carrier splits a SHARED 96-prism budget until no front is smooth. Every check runs
    /// from assets and pure code alone — no play mode — and the ones about the map's mathematics are
    /// the same predicates the offline harness (Tools/Shaders/verify_prism_wake.py) measures by
    /// executing the shipped HLSL, so the two cannot disagree.
    /// </summary>
    public class PrismWakeTests
    {
        const string HlslPath = "Assets/_Graphics/Materials/Graphs/PrismWake.hlsl";
        const string FunctionName = "PrismWakeDeform";
        const string CradleFunctionName = "PrismCradleDeform";
        const string VesselControllerPath = "Assets/_Scripts/Controller/Vessel/VesselController.cs";
        const string SourcePath = "Assets/_Scripts/Utility/PrismWakeSource.cs";
        const string CarrierPath = "Assets/_Scripts/Utility/IPrismWakeCarrier.cs";
        const string ProjectilePath = "Assets/_Scripts/Controller/Projectiles/Projectile.cs";
        const string BlastPath = "Assets/_Scripts/Controller/Projectiles/AOEExplosion.cs";
        const string DetonatorPath = "Assets/_Scripts/Controller/ImpactEffects/EffectsSO/ProjectileDetonatorSO.cs";
        const string WarheadPrefab = "Assets/_Prefabs/Projectile/AOEMissileWarhead.prefab";
        const string ConePath = "Assets/_Scripts/Controller/Projectiles/AOEConicExplosion.cs";
        const string CylinderPath = "Assets/_Scripts/Controller/Projectiles/AOECylindricalExplosion.cs";
        const string BallPath = "Assets/_Scripts/Controller/Arcade/AstroLeague/AstroLeagueBall.cs";
        const string SkyburstPrefab = "Assets/_Prefabs/Projectile/SkyBurstProjectile.prefab";

        static readonly string[] LiveGraphs =
        {
            "Assets/_Graphics/Materials/Graphs/BlockGraph.shadergraph",
            "Assets/_Graphics/Materials/Graphs/ExplodingBlockGraph.shadergraph",
        };

        // Slot ids in the order the HLSL declares its parameters (inputs first, then outputs):
        // Position 0, Normal 1, OutPosition 2, OutNormal 3 — the morph family's shared signature.
        const int SlotPosition = 0;
        const int SlotNormal = 1;
        const int SlotOutPosition = 2;
        const int SlotOutNormal = 3;

        static string[] Blocks(string path)
        {
            // Normalise CRLF first: a Windows checkout otherwise collapses the whole file into one
            // block and every block-scoped check reads the wrong property.
            string text = File.ReadAllText(path).Replace("\r\n", "\n");
            return text.Split(new[] { "\n\n" }, System.StringSplitOptions.RemoveEmptyEntries);
        }

        static string BlockObjectId(string[] blocks, string serializedDescriptor)
        {
            var block = blocks.FirstOrDefault(b =>
                b.Contains($"\"m_SerializedDescriptor\": \"{serializedDescriptor}\""));
            Assert.IsNotNull(block, $"no {serializedDescriptor} block node");
            var m = Regex.Match(block, "\"m_ObjectId\":\\s*\"([0-9a-f]+)\"");
            Assert.IsTrue(m.Success, $"{serializedDescriptor} block has no m_ObjectId");
            return m.Groups[1].Value;
        }

        /// <summary>
        /// A C# file with its COMMENTS removed, for the assertions that ban a token.
        ///
        /// <para>It exists because of a defect this suite shipped and then hit: a ruling comment that
        /// NAMES the thing it forbids — "do not re-add an <c>AddComponent&lt;PrismWakeSource&gt;()</c>
        /// here" — trips an <c>Assert.IsFalse(file.Contains(...))</c> gate, so the gate pressures
        /// whoever writes the record into describing the ban vaguely, which is the opposite of what a
        /// ruling record is for. Two of this file's own bans failed on exactly that the first time the
        /// carrier moved. General rule: <b>a textual gate that forbids a token cannot be allowed to
        /// read the comment documenting the ban.</b></para>
        ///
        /// <para>String and char literals are respected, so a <c>"//"</c> inside one is not mistaken
        /// for the start of a comment — which matters because a banned token is usually quoted inside
        /// the very assertion message that bans it.</para>
        /// </summary>
        static string CodeOnly(string source)
        {
            var sb = new System.Text.StringBuilder(source.Length);
            for (int i = 0; i < source.Length; i++)
            {
                char c = source[i];

                if (c == '"' || c == '\'')
                {
                    char quote = c;
                    sb.Append(c);
                    for (i++; i < source.Length; i++)
                    {
                        sb.Append(source[i]);
                        if (source[i] == '\\') { if (++i < source.Length) sb.Append(source[i]); continue; }
                        if (source[i] == quote) break;
                    }
                    continue;
                }

                if (c == '/' && i + 1 < source.Length && source[i + 1] == '/')
                {
                    while (i < source.Length && source[i] != '\n') i++;
                    if (i < source.Length) sb.Append('\n');
                    continue;
                }

                if (c == '/' && i + 1 < source.Length && source[i + 1] == '*')
                {
                    i += 2;
                    while (i + 1 < source.Length && !(source[i] == '*' && source[i + 1] == '/')) i++;
                    i++;
                    continue;
                }

                sb.Append(c);
            }
            return sb.ToString();
        }

        static string CodeOf(string path) => CodeOnly(File.ReadAllText(path));

        static PrismWakeConfigSO ResolveConfig()
        {
            // No asset is a legal state — the SO's own defaults apply — but the defaults must
            // themselves be sane, or the feature silently never engages.
            return AssetDatabase.LoadAssetAtPath<PrismWakeConfigSO>(
                       "Assets/Resources/" + PrismWake.ConfigResourcePath + ".asset")
                   ?? ScriptableObject.CreateInstance<PrismWakeConfigSO>();
        }

        [Test]
        public void WakeHlsl_ExistsDeclaresTheFunctionAndTheFileScopeBank()
        {
            Assert.IsTrue(File.Exists(HlslPath), $"{HlslPath} is missing.");
            string hlsl = File.ReadAllText(HlslPath);

            Assert.IsTrue(hlsl.Contains("void PrismWakeDeform_float(float3 Position, float3 Normal,"),
                "PrismWake.hlsl no longer declares PrismWakeDeform_float(Position, Normal, ...) — every wired " +
                "graph would fail to compile, or bind its slots to the wrong parameters.");

            // The bank is FILE-SCOPE arrays (Shader Graph has no array property type, and an array
            // inside UnityPerMaterial breaks SRP batching) — so they must be declared here, outside
            // any CBUFFER, at the length the C# publisher writes.
            foreach (var decl in new[]
                     {
                         "float4 _PrismWakeCentre[PRISM_WAKE_SLOTS]",
                         "float4 _PrismWakeShape[PRISM_WAKE_SLOTS]",
                         "float4 _PrismWakeParams",
                     })
            {
                Assert.IsTrue(hlsl.Contains(decl), $"PrismWake.hlsl no longer declares `{decl}`.");
            }
            Assert.IsFalse(hlsl.Contains("CBUFFER_START"),
                "PrismWake.hlsl declares a CBUFFER — the bank must stay a file-scope global or SRP batching breaks.");

            // A SPHERE HAS NO AXIS. The cylinder's third array is gone, and its return would mean
            // the map is no longer radial about the round — which is what every proof of this
            // effect's normal (a shear-free diagonal Jacobian) and of its purity rests on. Comments
            // are stripped because the header deliberately RECORDS that the array was retired.
            string code = string.Join("\n", hlsl.Replace("\r\n", "\n").Split('\n')
                                                .Where(l => !l.TrimStart().StartsWith("//")));
            Assert.IsFalse(code.Contains("_PrismWakeAxis"),
                "PrismWake.hlsl declares _PrismWakeAxis again — the front is spherical about the round, and a " +
                "cylindrical frame invalidates both the analytic normal and Tools/Shaders/verify_prism_wake.py.");

            var slots = Regex.Match(hlsl, @"#define PRISM_WAKE_SLOTS (\d+)");
            Assert.IsTrue(slots.Success, "PRISM_WAKE_SLOTS is not #defined in PrismWake.hlsl.");
            Assert.AreEqual(PrismWake.Slots, int.Parse(slots.Groups[1].Value),
                "PrismWake.Slots (C#) and PRISM_WAKE_SLOTS (HLSL) disagree — the publisher would write a bank " +
                "the shader reads at a different length. Change both together.");

            // The #ifndef dial the offline harness drives its NEGATIVE CONTROL through. The radial
            // stretch term carries the ENTIRE derivative of the wavelet, so it is exactly what a
            // normal that merely looks plausible would omit — and a gate nobody has watched fail is
            // a gate nobody should trust, so the dial is part of the contract.
            Assert.IsTrue(hlsl.Contains("#ifndef PRISM_WAKE_RADIAL_GAIN"),
                "PrismWake.hlsl no longer carries the PRISM_WAKE_RADIAL_GAIN dial — " +
                "Tools/Shaders/verify_prism_wake.py's negative control has nothing to switch off, so its " +
                "derivative test would no longer be a proof.");
            Assert.IsTrue(hlsl.Contains("#ifndef PRISM_WAKE_MIN_STRETCH"),
                "PrismWake.hlsl no longer carries the PRISM_WAKE_MIN_STRETCH floor on the Jacobian's two " +
                "stretch terms.");

            // The guid the wirer and the validator pin must be the one the .meta actually carries.
            string meta = File.ReadAllText(HlslPath + ".meta");
            var guid = Regex.Match(meta, @"guid:\s*([0-9a-f]{32})");
            Assert.IsTrue(guid.Success, "PrismWake.hlsl.meta carries no guid.");
            Assert.AreEqual(PrismClockWiringValidator.WakeHlslGuid, guid.Groups[1].Value,
                "PrismClockWiringValidator.WakeHlslGuid does not match PrismWake.hlsl.meta.");
        }

        [Test]
        public void EveryLiveGraph_RunsTheWakeThenTheCradle()
        {
            foreach (var graphPath in LiveGraphs)
            {
                Assert.IsTrue(File.Exists(graphPath), $"{graphPath} is missing.");
                var blocks = Blocks(graphPath);

                Assert.IsTrue(PrismClockWiringValidator.TryFindCustomFunctionNodeId(blocks, FunctionName, out var wakeId),
                    $"{graphPath} has no {FunctionName} Custom Function node — run Tools/Shaders/wire_prism_wake.py.");
                Assert.IsTrue(PrismClockWiringValidator.TryFindCustomFunctionNodeId(blocks, CradleFunctionName, out var cradleId),
                    $"{graphPath} has no {CradleFunctionName} Custom Function node.");

                var wakeBlock = blocks.First(b => b.Contains($"\"m_FunctionName\": \"{FunctionName}\""));
                Assert.IsTrue(wakeBlock.Contains($"\"m_FunctionSource\": \"{PrismClockWiringValidator.WakeHlslGuid}\""),
                    $"{graphPath}: {FunctionName} does not source PrismWake.hlsl.");

                var edges = PrismClockWiringValidator.ParseEdges(blocks[0]);

                // The ORDER of the two morphs is the thing here, and nothing on screen would report
                // it being swapped. The cradle closes mass onto a hull RESTING on it, so it has to
                // see the rippled position; a front applied after the drape would ripple the very
                // vertices the drape had just closed onto the hull and open the hole back up.
                foreach (var (slot, outSlot, label) in new[]
                         {
                             (SlotPosition, SlotOutPosition, "Position"),
                             (SlotNormal, SlotOutNormal, "Normal"),
                         })
                {
                    var feeder = edges.FirstOrDefault(e => e.inNode == cradleId && e.inSlot == slot);
                    Assert.IsNotNull(feeder.outNode, $"{graphPath}: {CradleFunctionName}.{label} is unconnected.");
                    Assert.AreEqual((wakeId, outSlot), (feeder.outNode, feeder.outSlot),
                        $"{graphPath}: {CradleFunctionName}.{label} is not fed by {FunctionName}.Out{label} — " +
                        "the wake must run BEFORE the cradle.");

                    // ...and the wake wraps the chain rather than replacing it.
                    var src = edges.FirstOrDefault(e => e.inNode == wakeId && e.inSlot == slot);
                    Assert.IsNotNull(src.outNode, $"{graphPath}: {FunctionName}.{label} is unconnected — the vertex chain was dropped.");
                    Assert.AreNotEqual(wakeId, src.outNode, $"{graphPath}: {FunctionName}.{label} is fed by itself.");
                    Assert.AreNotEqual(cradleId, src.outNode, $"{graphPath}: {FunctionName}.{label} is fed by the cradle — the two morphs form a cycle.");
                }

                // The wake is NOT last: it must reach the blocks only through the cradle.
                string posBlock = BlockObjectId(blocks, "VertexDescription.Position");
                string nrmBlock = BlockObjectId(blocks, "VertexDescription.Normal");
                Assert.IsFalse(edges.Any(e => e.outNode == wakeId && (e.inNode == posBlock || e.inNode == nrmBlock)),
                    $"{graphPath}: {FunctionName} feeds a VertexDescription block directly — the cradle would then " +
                    "be bypassed on that channel.");

                // The bank must NOT also exist as graph properties: a same-named property would
                // shadow the file-scope declaration and read the per-material default (zero).
                foreach (var name in new[] { "_PrismWakeCentre", "_PrismWakeShape", "_PrismWakeParams" })
                {
                    Assert.IsFalse(blocks.Any(b => b.Contains("ShaderProperty") &&
                                                   (b.Contains($"\"m_DefaultReferenceName\": \"{name}\"") ||
                                                    b.Contains($"\"m_OverrideReferenceName\": \"{name}\""))),
                        $"{graphPath} declares {name} as a graph PROPERTY — the wake bank is a file-scope global in the HLSL, never a property.");
                }
            }
        }

        [Test]
        public void Specs_NameTheWakeOnBothLiveGraphs_AndNotOnSuction()
        {
            var byName = PrismClockWiringValidator.Specs.ToDictionary(s => s.GraphName);
            foreach (var graph in new[] { "BlockGraph", "ExplodingBlockGraph" })
            {
                Assert.IsTrue(byName[graph].CustomFunctions.Contains(FunctionName),
                    $"PrismClockWiringValidator.Specs[{graph}] no longer names {FunctionName} — the menu " +
                    "validator would print ALL PRESENT with the wake unwired.");
            }
            // Mass being consumed is not mass a blast front is sweeping through — the same exclusion
            // every vertex-stage family on the live graphs makes.
            Assert.IsFalse(byName["SuctionGraph"].CustomFunctions.Contains(FunctionName),
                "SuctionGraph must not carry the wake: it renders mass being consumed.");
            Assert.AreEqual("PrismWake.hlsl", PrismClockWiringValidator.CustomFunctionSourceHint(FunctionName));
            Assert.AreEqual(PrismClockWiringValidator.WakeHlslGuid,
                PrismClockWiringValidator.ExpectedCustomFunctionSourceGuid(FunctionName));
        }

        [Test]
        public void Config_IsSaneWhenAuthored()
        {
            var config = ResolveConfig();
            Assert.IsTrue(config.IsSane,
                $"PrismWakeConfig is not sane (amplitude {config.Amplitude}, Q {config.WavesInFront}, " +
                $"half-thickness {config.HalfThicknessFraction}): the shader treats that as OFF.");
            Assert.Greater(config.Amplitude, 0f,
                "Amplitude is 0 — the shader's second sentinel reads that as off and nothing ripples. " +
                "Use `enabled` to switch the wake off.");
            Assert.GreaterOrEqual(config.WavesInFront, 1,
                "WavesInFront below 1 is less than a whole wavelet: the sine would be cut off mid-swing at both " +
                "faces of the shell, which costs the second derivative the whole-cycle choice buys.");
        }

        [Test]
        public void Config_NeverFolds_AndTheBoundIsAFunctionOfTheBandwidth()
        {
            var config = ResolveConfig();
            // The whole no-fold guarantee in ONE comparison. The map's radial stretch is
            // a = 1 + A*P'(s) with max|P'| = 2*pi*Q exactly (at s = 0), so a > 0 iff A*2*pi*Q < 1 —
            // a dimensionless bound with no radius, no reach and no thickness in it, so retuning
            // any of those cannot invalidate it. The tangential stretch b = f(r)/r then follows with
            // no second bound, because a > 0 makes f strictly increasing and the publisher births
            // the front at c = sigma so the shell never straddles the centre.
            // Tools/Shaders/verify_prism_wake.py MEASURES both over the whole authored range by
            // executing the shipped HLSL.
            Assert.IsTrue(config.NeverFolds,
                $"PrismWakeConfig's amplitude {config.Amplitude} is at or past the folding bound " +
                $"{config.FoldingAmplitude:0.0000} for Q = {config.WavesInFront} — a crest could turn a prism " +
                "inside out.");

            // The bound is DERIVED from the bandwidth rather than written down, which is what makes
            // raising Q safe: a shorter wavelet has a steeper crest, so it must carry less amplitude.
            Assert.AreEqual(1f / (PrismWakeConfigSO.TwoPi * config.WavesInFront), config.FoldingAmplitude, 1e-6f,
                "FoldingAmplitude is no longer 1/(2*pi*Q) — it is the no-fold condition itself, not a constant.");
        }

        [Test]
        public void FrontRadius_IsBornAtTheShellAndDiesAtTheReach()
        {
            var config = ResolveConfig();
            const float reach = 95.2f;   // a skyburst at resting Mass
            float sigma = config.HalfThicknessFor(reach);

            // The second half of the no-fold proof is a PUBLISHER guarantee, so it is asserted here
            // rather than derived in the shader: the front is born at its own half-thickness, so its
            // inner face sits exactly ON the round's centre and never past it. Birth any closer and
            // the shell straddles the centre, where f(r) can reach zero and the prism inverts.
            Assert.AreEqual(sigma, config.FrontRadiusAt(0f, reach), 1e-4f,
                "the front is not born at the shell's half-thickness — it would straddle the round's centre.");
            Assert.AreEqual(reach, config.FrontRadiusAt(1f, reach), 1e-4f,
                "the front does not die at the warhead's own reach — the shell would stop short of, or run past, " +
                "the radius the blast actually goes off in, which is the one fact this effect exists to tell.");
            Assert.Greater(config.FrontRadiusAt(0.5f, reach), config.FrontRadiusAt(0.25f, reach),
                "the front does not travel outward.");
            Assert.Greater(sigma, 0f, "the shell has no thickness.");
            Assert.Less(sigma, reach, "the shell is thicker than the blast it is drawing.");
        }

        [Test]
        public void FrontEnvelope_IsZeroWithZeroSlopeAtBothEnds()
        {
            // The recycle seam. A front dies at the reach and the next appears at the shell's own
            // half-thickness, ~1.6 times a second at the shipped rate, so BOTH ends must reach zero
            // in value AND slope or a front pops into or out of existence — the continuity law,
            // applied to the effect's own life rather than to a prism's.
            Assert.AreEqual(0f, PrismWakeConfigSO.FrontEnvelope(0f), 1e-6f, "a front is born at full strength");
            Assert.AreEqual(0f, PrismWakeConfigSO.FrontEnvelope(1f), 1e-6f, "a front dies at full strength");
            Assert.AreEqual(1f, PrismWakeConfigSO.FrontEnvelope(0.5f), 1e-5f, "a front never reaches full strength");

            const float h = 1e-3f;
            Assert.Less(PrismWakeConfigSO.FrontEnvelope(h) / h, 0.05f,
                "the envelope leaves zero with a non-zero slope at BIRTH — a front would appear with a kink.");
            Assert.Less(PrismWakeConfigSO.FrontEnvelope(1f - h) / h, 0.05f,
                "the envelope reaches zero with a non-zero slope at the REACH — a front would blink out at the " +
                "edge of the blast volume.");

            // Monotone up then down, so there is exactly one crest and the front never flickers.
            float prev = 0f;
            for (int i = 1; i <= 50; i++)
            {
                float v = PrismWakeConfigSO.FrontEnvelope(i / 100f);
                Assert.Greater(v, prev, "the envelope is not monotone on the way up");
                prev = v;
            }
        }

        [Test]
        public void Config_ResidencySwapIsInvisible()
        {
            var config = ResolveConfig();

            // The whole point of the margin: a prism gains or loses its high-poly geometry only
            // where the front provably cannot have moved any of its vertices. A zero margin puts
            // the swap exactly ON the boundary, where a float comparison decides whether the player
            // sees a prism change shape.
            Assert.Greater(config.ResidencyMargin, 0f,
                "PrismWakeConfig.ResidencyMargin is 0 — the mesh swap would happen at exactly the surface the " +
                "front reaches, so it is a coin toss whether the geometry change is visible.");

            // And the volume it sweeps must COVER every radius the map can move a vertex at. The
            // front dies AT the reach and the shell reaches sigma past its own centre, so the
            // outermost displaced vertex of a pulse's last frame is at reach + sigma — and the
            // margin is an ABSOLUTE distance while the shell is a FRACTION of the reach, so
            // "the margin covers the overshoot" holds at one authored pair and not the next (it was
            // true by 0.2 of a unit at 0.25/24 and false by 19 the first time the shell was
            // thickened). ResidencyRadiusFor adds sigma, which is what makes this structural.
            foreach (float reach in new[] { 4f, 95.2f, 480f, 2400f })
            {
                float sigma = config.HalfThicknessFor(reach);
                Assert.Greater(config.ResidencyRadiusFor(reach), reach + sigma,
                    $"at reach {reach} the residency sweep ({config.ResidencyRadiusFor(reach):0.0}) does not cover " +
                    $"the shell's outer face at its last frame ({reach + sigma:0.0}) — a prism would be swapped to " +
                    "the dense mesh while its own vertices are already displaced, and it would POP.");
            }

            // The budget. 0 means the ripple only ever runs on the authored 24-triangle prism —
            // the look two rounds of the cradle's history rejected; an unbounded one is the cost
            // nobody signed up for.
            Assert.Greater(config.MaxResidentPrisms, 0,
                "PrismWakeConfig.MaxResidentPrisms is 0 — nothing is ever swapped, so the ripple runs on the " +
                "authored 24-triangle prism and reads as facets hinging.");
            // The GPU pays in TRIANGLES, not in prisms, so that is the authority and the prism count
            // is one half of a trade against the subdivision: the same budget buys a few very smooth
            // prisms or more slightly coarser ones. The first playtest of the front said it could not
            // be seen at all, and what a player reads at arena range is how many prisms are MOVING —
            // so the overtune spent the budget on count (128 x 10) rather than on smoothness
            // (96 x 12), which is fewer triangles than before, not more.
            long tris = (long)config.MaxResidentPrisms * config.Subdivision * config.Subdivision * 2 * 6;
            Assert.Less(tris, 200000,
                $"The residency budget is {tris} triangles ({config.MaxResidentPrisms} prisms x subdivision " +
                $"{config.Subdivision}) — lower MaxResidentPrisms or Subdivision.");

            // And a ceiling on the count itself, because the triangle bound alone would admit a
            // thousand prisms at subdivision 2 — which is the authored prism with none of the
            // smoothness the family exists for, and a thousand state changes a frame.
            Assert.LessOrEqual(config.MaxResidentPrisms, 160,
                "PrismWakeConfig.MaxResidentPrisms is above 160 — 'a handful of prisms at a time' is what makes " +
                "the high-poly swap affordable at all, and a swap is a state change on a live prism.");
            Assert.GreaterOrEqual(config.Subdivision, 8,
                "PrismWakeConfig.Subdivision below 8 is not enough surface for a smooth map: the front would read " +
                "as facets hinging, which is the look two rounds of the cradle's history rejected.");
        }

        [Test]
        public void TheFrontHasNoCLOCKOfItsOwn()
        {
            // The whole simplification the carrier move bought. A detonating blast ALREADY has a
            // travelling wavefront — its trigger expands from nothing to its full radius over its own
            // authored duration, and that radius is what its damage pass uses — so the front's
            // position is READ from the carrier. A rate authored on this side would be a second
            // answer to a question the blast already answers, and the two would drift: the ripple
            // would arrive at a prism before or after the blast that is supposed to be causing it.
            // CodeOnly, not ReadAllText: the comments in all three files legitimately NAME the
            // clock they are recording the removal of, and a ban that could read them would be
            // banning the record instead of the code.
            string wake = CodeOf("Assets/_Scripts/Utility/PrismWake.cs");
            string cfg  = CodeOf("Assets/_Scripts/ScriptableObjects/PrismWakeConfigSO.cs");
            string src  = CodeOf(SourcePath);

            Assert.IsFalse(cfg.Contains("pulsesPerSecond"),
                "PrismWakeConfigSO authors a pulses-per-second again. The front's position is the carrier's own " +
                "wavefront; a rate here is a second clock for one front and it will drift from the blast.");
            Assert.IsFalse(wake.Contains("PulseRate"),
                "PrismWake.PulseRate is back — see above; the publisher must not own a clock the carrier already " +
                "owns.");
            Assert.IsFalse(src.Contains("Time.deltaTime * "),
                "PrismWakeSource integrates something per frame again. The only per-frame integration left is the " +
                "engage/release ease (MoveTowards); the front's POSITION is read, never advanced.");

            // What the source DOES still own is the mapping into the legal travel band, and that is
            // what keeps a carrier from breaking the no-fold proof: a blast honestly starts at radius
            // ZERO, and a front there would straddle its own centre.
            var config = ResolveConfig();
            foreach (float reach in new[] { 4f, 95.2f, 480f, 2400f })
            {
                Assert.AreEqual(config.HalfThicknessFor(reach), config.FrontRadiusAt(0f, reach), 1e-4f,
                    $"at reach {reach} a progress of 0 no longer floors the front at the shell's own " +
                    "half-thickness — a blast reports progress 0 before it has expanded at all, so the front " +
                    "would be centred on the blast's own origin and the shell would straddle it.");
                Assert.AreEqual(reach, config.FrontRadiusAt(1f, reach), 1e-3f,
                    $"at reach {reach} a progress of 1 no longer puts the front at the full reach — the ripple " +
                    "would stop short of the volume the blast actually reached.");
            }
        }

        [Test]
        public void TheFirstFrameIsSILENT_SoTheResidencySwapIsInvisible()
        {
            // The ordering the whole invisible-swap contract rests on, now that the carrier is a
            // blast rather than a round in flight. AOEExplosion.Initialize/Detonate run a frame
            // BEFORE ExplodeAsync's expansion loop does (it awaits its delay first), so the carrier
            // answers true at progress 0 for exactly one frame — and on that frame the published
            // strength is the envelope at 0, which is exactly zero. PrismWake.Flush swaps every
            // prism in the blast's volume to the high-poly mesh on that frame, where the map provably
            // moves nothing. A carrier that only began answering once it was moving would hand those
            // prisms the dense mesh with their vertices already displaced, and they would pop.
            Assert.AreEqual(0f, PrismWakeConfigSO.FrontEnvelope(0f), 0f,
                "FrontEnvelope(0) is not exactly zero — the residency swap frame would displace vertices.");
            Assert.AreEqual(0f, PrismWakeConfigSO.FrontEnvelope(1f), 0f,
                "FrontEnvelope(1) is not exactly zero — the front would blink out at the edge of the blast.");

            string blast = CodeOf(BlastPath);
            Assert.IsTrue(blast.Contains("_expansion01 = 0f;"),
                "AOEExplosion.Initialize no longer zeroes _expansion01, so a re-used blast would answer a stale " +
                "progress on its first frame and the residency swap would happen mid-sweep.");
            Assert.IsTrue(blast.Contains("_expansion01 = ease;"),
                "AOEExplosion no longer publishes its eased expansion as the front's progress — the ripple would " +
                "stop tracking the blast's own wavefront.");

            // And the engage ease must NOT be re-authored: on a 0.15 s detonation it is pure
            // attenuation, because the envelope's C1 zero at birth already IS the engage.
            var config = ResolveConfig();
            Assert.AreEqual(0f, config.EngageSeconds, 1e-6f,
                "PrismWakeConfig.EngageSeconds is non-zero again. The warhead blast sweeps for 0.15 s and the " +
                "front's own envelope is already zero — value and slope — at birth, so an engage ease only ever " +
                "holds the front below full depth for the whole event (at the old 0.15 s it never reached half).");
            Assert.Greater(config.ReleaseSeconds, 0f,
                "PrismWakeConfig.ReleaseSeconds is 0. This one earns its keep where the engage does not: a blast " +
                "cancelled mid-sweep by a turn end freezes with the envelope at full value, and that must fade " +
                "rather than blink.");
        }

        [Test]
        public void NothingButTheHeavyWarheadIsGrantedAFront()
        {
            Assert.IsTrue(File.Exists(VesselControllerPath), $"{VesselControllerPath} is missing.");
            string vessel = CodeOf(VesselControllerPath);

            // The wake shipped on every vessel for one playtest and was pulled: a ripple behind
            // every hull is wallpaper, and the high-poly residency budget is SHARED, so a
            // per-vessel grant splits it until no front is smooth. Re-adding an ensure here is the
            // specific regression this guards, and it would look entirely reasonable in a diff.
            Assert.IsFalse(vessel.Contains("AddComponent<PrismWakeSource>()"),
                "VesselController grants a PrismWakeSource again. The front belongs to the Sparrow's HEAVY " +
                "skyburst warhead and to nothing else — see Docs/PRISM_ANIMATION.md §4.7.3.");

            // The BALL was the second carrier and was pulled for the same reason one step down: a
            // ball is in play for a whole match, so its front was continuous, and a shockwave that
            // never stops is not an event. Its own file records the ruling; this is the gate.
            Assert.IsTrue(File.Exists(BallPath), $"{BallPath} is missing.");
            string ball = CodeOf(BallPath);
            Assert.IsFalse(ball.Contains("AddComponent<PrismWakeSource>()"),
                "AstroLeagueBall grants a PrismWakeSource again — a ball is in play for a whole match, so its " +
                "front would be continuous, which is exactly what got it pulled.");
            Assert.IsFalse(ball.Contains("IPrismWakeCarrier"),
                "AstroLeagueBall implements IPrismWakeCarrier again — it is not a shockwave carrier.");

            // The MISSILE IN FLIGHT was the third carrier and went the same way one step further
            // down: a front trailing a travelling object read as a WAKE, which is a texture the round
            // wears, and the round is in the air for seconds. The front belongs to the thing the round
            // is carrying.
            Assert.IsTrue(File.Exists(ProjectilePath), $"{ProjectilePath} is missing.");
            string projectile = CodeOf(ProjectilePath);
            Assert.IsFalse(projectile.Contains("AddComponent<PrismWakeSource>()"),
                "Projectile grants itself a PrismWakeSource again — a front on the round in flight is a wake, " +
                "which is what got it moved to the warhead BLAST (Docs/PRISM_ANIMATION.md §4.7.3).");
            Assert.IsFalse(projectile.Contains("class Projectile : MonoBehaviour, IPrismWakeCarrier"),
                "Projectile implements IPrismWakeCarrier again — the round in flight is not the carrier; its " +
                "warhead blast is.");

            // THE ONE CARRIER. The blast answers the capability itself, and it is granted a source at
            // the single site that knows which of the ~dozen AOEExplosion instances the game spawns
            // is THE WARHEAD. Both halves are required: a blast that answers and is never granted a
            // source publishes nothing, and a grant with nothing answering does nothing at all
            // (PrismWakeSource has no fallback, because inventing a radius the blast does not have is
            // the one thing a front must never do).
            Assert.IsTrue(File.Exists(BlastPath), $"{BlastPath} is missing.");
            string blast = CodeOf(BlastPath);
            Assert.IsTrue(blast.Contains("AOEExplosion : ElementalShipComponent, IPrismWakeCarrier"),
                "AOEExplosion no longer implements IPrismWakeCarrier — nothing would answer for the warhead's " +
                "reach or its wavefront progress, and the front would never publish.");
            Assert.IsTrue(blast.Contains("public virtual bool TryGetShockwave"),
                "AOEExplosion.TryGetShockwave is no longer VIRTUAL. It reads MaxScale through the authored " +
                "collider radius, which is a radius only for a SPHERE — the conic and cylindrical blasts must be " +
                "able to refuse in code rather than be trusted never to be granted a source.");

            Assert.IsTrue(File.Exists(DetonatorPath), $"{DetonatorPath} is missing.");
            string detonator = CodeOf(DetonatorPath);
            Assert.IsTrue(detonator.Contains("AddComponent<PrismWakeSource>()"),
                "ProjectileDetonatorSO no longer grants the warhead blast a PrismWakeSource, so nothing publishes " +
                "the skyburst's shockwave front at all.");
            // The grant must sit INSIDE the warhead branch. One indentation level out and every
            // blast the detonation spawns gets a front — the prism cairn and the skyburst cone
            // included, which is four fronts sharing one budget for one detonation.
            int warheadAt = detonator.IndexOf("proj.WarheadBlast", System.StringComparison.Ordinal);
            int grantAt   = detonator.IndexOf("AddComponent<PrismWakeSource>()", System.StringComparison.Ordinal);
            Assert.Greater(warheadAt, 0, "ProjectileDetonatorSO no longer spawns the warhead blast.");
            Assert.Greater(grantAt, warheadAt,
                "ProjectileDetonatorSO grants the PrismWakeSource before it reaches the warhead branch — every " +
                "blast in the detonation would get a front, and they SHARE one residency budget.");

            // The two non-spherical shapes refuse in CODE. A rule enforced by which prefab somebody
            // dropped a component onto is not enforced by the code.
            foreach (var path in new[] { ConePath, CylinderPath })
            {
                Assert.IsTrue(File.Exists(path), $"{path} is missing.");
                Assert.IsTrue(CodeOf(path).Contains("public override bool TryGetShockwave"),
                    $"{path} no longer refuses a shockwave front. Its MaxScale is not a radius — the cone's is a " +
                    "base diameter across the gape axis and the plate's is a diameter with its length on a " +
                    "separate dial — so an inherited answer draws a sphere of the wrong size around the wrong " +
                    "thing.");
            }
        }

        [Test]
        public void TheCarrierBlastTOUCHESNoPrismMass()
        {
            // The reason this front is honest rather than a lie, and it is an authored fact rather
            // than an argument: the warhead's whole payload is aimed at LIVING things (it debuffs
            // pilots and jousts creatures) and it authors affectsPrisms: 0. So the prisms ripple as
            // the shockwave crosses them and are still standing afterwards, which is exactly what
            // happened to them. A blast that DESTROYED the mass it rippled would be saying the same
            // thing twice, and the ripple would be the less legible of the two.
            Assert.IsTrue(File.Exists(WarheadPrefab), $"{WarheadPrefab} is missing.");
            string warhead = File.ReadAllText(WarheadPrefab);
            Assert.IsTrue(Regex.IsMatch(warhead, @"affectsPrisms:\s*0"),
                "AOEMissileWarhead.prefab now affects prism MASS. The shockwave front was granted to it because " +
                "it does not: a blast that destroys what it ripples says the same thing twice, and the ripple " +
                "would be describing mass that is no longer there (Docs/PRISM_ANIMATION.md §4.7.3).");
        }

        [Test]
        public void TheWarheadIsTheDiscriminator_NotAnAuthoredFlag()
        {
            string projectile = CodeOf(ProjectilePath);

            // THE SPARROW'S TWO MISSILES ARE ONE PREFAB AND ONE POOL, told apart by a per-shot
            // ProjectilePayload — so a prefab bool cannot discriminate them and `leavesWake` had to
            // go. What CAN: WarheadBlastRadiusMultiplier is already 0 on the base rocket (its
            // payload disarms the warhead) and 0 on every non-skyburst prefab in the game, so
            // "gate on the warhead" is the exact discriminator with nothing new authored anywhere —
            // the same no-op argument Projectile.PublishFuzeLit already makes for itself.
            Assert.IsTrue(projectile.Contains("WarheadBlastRadiusMultiplier"),
                "Projectile no longer exposes WarheadBlastRadiusMultiplier — the warhead blast is spawned behind " +
                "that gate, so the front would stop being gated on there being a warhead at all.");

            // The discriminator did not move with the carrier, and that is the point: the warhead
            // BLAST only ever exists on a heavy shot, because the one site that spawns it is already
            // fenced by Payload.ArmWarhead through this multiplier. So "the heavy skyburst and nothing
            // else" still falls out of the weapon rather than out of a bool somebody has to set.
            string detonatorSrc = CodeOf(DetonatorPath);
            Assert.IsTrue(detonatorSrc.Contains("proj.WarheadBlastRadiusMultiplier > 0f"),
                "ProjectileDetonatorSO no longer gates the warhead on its radius multiplier — the base rocket " +
                "would spawn a warhead blast, and with it a shockwave front it is not supposed to have.");
            Assert.IsFalse(projectile.Contains("leavesWake"),
                "Projectile carries a `leavesWake` field again. The base and heavy rockets share ONE prefab and " +
                "ONE pool (ProjectilePayload), so an authored bool grants the front to BOTH — gate on the " +
                "warhead instead.");

            // And no prefab may carry the retired field: a serialized key the type no longer
            // declares still greps as a live edge, and Unity never prunes it.
            foreach (var path in Directory.GetFiles("Assets/_Prefabs", "*.prefab", SearchOption.AllDirectories))
            {
                Assert.IsFalse(File.ReadAllText(path).Contains("leavesWake:"),
                    $"{path} still serializes `leavesWake` — the field is retired; the warhead is the gate.");
            }

            // The one round in the game that IS authored with a warhead blast, so the one that has a
            // front. If this goes to zero the skyburst has no shockwave and nothing reports it.
            Assert.IsTrue(File.Exists(SkyburstPrefab), $"{SkyburstPrefab} is missing.");
            var multiplier = Regex.Match(File.ReadAllText(SkyburstPrefab), @"warheadBlastRadiusMultiplier:\s*([0-9.]+)");
            Assert.IsTrue(multiplier.Success,
                "SkyBurstProjectile.prefab authors no warheadBlastRadiusMultiplier — its warhead, its fuze LIT " +
                "sphere and its shockwave front would all collapse together.");
            Assert.Greater(float.Parse(multiplier.Groups[1].Value), 0f,
                "SkyBurstProjectile.prefab's warheadBlastRadiusMultiplier is 0 — no warhead, so no front.");
        }

        [Test]
        public void TheSourceAsksForAREACH_AndHasNoFallback()
        {
            Assert.IsTrue(File.Exists(SourcePath), $"{SourcePath} is missing.");
            Assert.IsTrue(File.Exists(CarrierPath), $"{CarrierPath} is missing.");
            string src = CodeOf(SourcePath);
            string carrier = CodeOf(CarrierPath);

            Assert.IsTrue(carrier.Contains("bool TryGetShockwave(out float reach, out float progress01)"),
                "IPrismWakeCarrier no longer asks for a REACH and a PROGRESS. It used to ask for a velocity, and " +
                "the speed window that came with that is what made the first cut invisible — its engage speed " +
                "sat above the top speed of the hull that flew it. The progress is what makes the front the " +
                "blast's OWN wavefront instead of a pulse running beside it.");
            Assert.IsTrue(src.Contains("IPrismWakeCarrier"),
                "PrismWakeSource no longer resolves a carrier — it would be back to reading one concrete type.");
            Assert.IsFalse(src.Contains("IVesselStatus"),
                "PrismWakeSource still reads IVesselStatus. It is no longer a vessel component; a carrier " +
                "answers for its own blast, and a vessel dependency here is what made it un-droppable onto a " +
                "projectile in the first place.");

            // No transform-measurement fallback, deliberately: a front's radius is the blast's own
            // radius, and a measured fallback would have to invent it. A carrier that answers
            // nothing must produce nothing, not a plausible-looking shockwave for a blast that is
            // not coming.
            Assert.IsFalse(src.Contains("_hasPreviousPosition"),
                "PrismWakeSource measures a transform delta again — that was the cylinder's velocity fallback, " +
                "and for a FRONT the equivalent fallback would invent a blast radius the round does not have.");

            // A pooled round is repositioned while disabled, so everything describing the last
            // flight has to be dropped on reuse — otherwise the first frame publishes a front from
            // the previous detonation's reach around this launch bay.
            Assert.IsTrue(src.Contains("void OnEnable()"),
                "PrismWakeSource no longer resets on enable — a pooled carrier would inherit the previous life's " +
                "progress and reach.");
            Assert.IsTrue(Regex.IsMatch(src, @"void OnEnable\(\)[\s\S]{0,600}?_reach = 0f"),
                "PrismWakeSource.OnEnable no longer clears its cached reach, so a re-used carrier would publish " +
                "the previous blast's radius for a frame.");
            Assert.IsTrue(Regex.IsMatch(src, @"void OnEnable\(\)[\s\S]{0,600}?_progress = 0f"),
                "PrismWakeSource.OnEnable no longer clears its cached progress, so a re-used carrier's first " +
                "frame would publish a front part-way out — which is a frame with a NON-ZERO strength, and that " +
                "is the frame the residency pass swaps prisms in on.");
        }

        [Test]
        public void TheFrontAndTheFuzeLitSphereAreOnePair()
        {
            // The two halves of the same weapon and the reason neither duplicates the other, now
            // separated in TIME as well as in channel: on the way IN the armed fuze publishes a LIT
            // SPHERE (Docs/LIT.md) that says WHERE this warhead will go off, statically, in the
            // shooter's domain colour; WHEN it goes off the blast's own front says HOW FAR,
            // kinetically, in vertices, by sweeping that same volume. A promise and its payoff, drawn
            // on the same mass. If the lit half is ever dropped the front is the only read left, and
            // it arrives too late to be a warning — so this pairing needs re-arguing rather than
            // silently becoming a solo effect.
            string projectile = CodeOf(ProjectilePath);
            Assert.IsTrue(projectile.Contains("PublishFuzeLit"),
                "Projectile no longer publishes its armed fuze as a LIT volume — the shockwave front was " +
                "designed as the kinetic half of that pair (Docs/PRISM_ANIMATION.md §4.7.3, Docs/LIT.md).");
        }
    }
}
#endif
