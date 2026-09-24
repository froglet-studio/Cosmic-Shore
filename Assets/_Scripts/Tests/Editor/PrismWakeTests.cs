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
                $"half-thickness {config.HalfThicknessFraction}, pulses/s {config.PulsesPerSecond}): " +
                "the shader treats that as OFF.");
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
        public void ThePulseClockHasExactlyOneCopy()
        {
            var config = ResolveConfig();
            // The pulse rate is the one piece of derived geometry left after the sphere replaced the
            // cylinder: the front's radius comes from the config, the reach comes from the warhead,
            // and the shell thickness is a fraction of the reach. So the publisher owns ONE number
            // and there is nothing for it to disagree with the shader about.
            Assert.AreEqual(config.PulsesPerSecond, PrismWake.PulseRate(config), 1e-6f,
                "PrismWake.PulseRate is no longer the config's own rate — the pulse clock has two copies.");
            Assert.Greater(config.PulsesPerSecond, 0f,
                "PulsesPerSecond is 0 — a front would be born and never travel, so the shell would stand still " +
                "at the round's own skin.");
        }

        [Test]
        public void NothingButTheHeavyWarheadIsGrantedAFront()
        {
            Assert.IsTrue(File.Exists(VesselControllerPath), $"{VesselControllerPath} is missing.");
            string vessel = File.ReadAllText(VesselControllerPath);

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
            string ball = File.ReadAllText(BallPath);
            Assert.IsFalse(ball.Contains("AddComponent<PrismWakeSource>()"),
                "AstroLeagueBall grants a PrismWakeSource again — a ball is in play for a whole match, so its " +
                "front would be continuous, which is exactly what got it pulled.");
            Assert.IsFalse(ball.Contains("IPrismWakeCarrier"),
                "AstroLeagueBall implements IPrismWakeCarrier again — it is not a shockwave carrier.");

            Assert.IsTrue(File.Exists(ProjectilePath), $"{ProjectilePath} is missing.");
            string projectile = File.ReadAllText(ProjectilePath);
            Assert.IsTrue(projectile.Contains("IPrismWakeCarrier"),
                "Projectile no longer implements IPrismWakeCarrier — nothing would answer for the round's blast " +
                "reach, and PrismWakeSource has no fallback (inventing a radius the blast does not have is the " +
                "one thing a front must never do).");
            Assert.IsTrue(projectile.Contains("AddComponent<PrismWakeSource>()"),
                "Projectile no longer grants itself a PrismWakeSource, so nothing publishes the skyburst's front.");
        }

        [Test]
        public void TheWarheadIsTheDiscriminator_NotAnAuthoredFlag()
        {
            string projectile = File.ReadAllText(ProjectilePath);

            // THE SPARROW'S TWO MISSILES ARE ONE PREFAB AND ONE POOL, told apart by a per-shot
            // ProjectilePayload — so a prefab bool cannot discriminate them and `leavesWake` had to
            // go. What CAN: WarheadBlastRadiusMultiplier is already 0 on the base rocket (its
            // payload disarms the warhead) and 0 on every non-skyburst prefab in the game, so
            // "gate on the warhead" is the exact discriminator with nothing new authored anywhere —
            // the same no-op argument Projectile.PublishFuzeLit already makes for itself.
            Assert.IsTrue(projectile.Contains("WarheadBlastRadiusMultiplier"),
                "Projectile.TryGetShockwaveReach no longer reads WarheadBlastRadiusMultiplier — the front would " +
                "stop being gated on there being a warhead at all.");
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
            string src = File.ReadAllText(SourcePath);
            string carrier = File.ReadAllText(CarrierPath);

            Assert.IsTrue(carrier.Contains("TryGetShockwaveReach"),
                "IPrismWakeCarrier no longer asks for a REACH. It used to ask for a velocity and a radius, and " +
                "the speed window that came with that is what made the first cut invisible — its engage speed " +
                "sat above the top speed of the hull that flew it.");
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
                "PrismWakeSource no longer resets on enable — a pooled carrier would inherit the previous " +
                "flight's pulse phase and reach.");
            Assert.IsTrue(Regex.IsMatch(src, @"void OnEnable\(\)[\s\S]{0,600}?_reach = 0f"),
                "PrismWakeSource.OnEnable no longer clears its cached reach, so a reissued round would publish " +
                "the previous detonation's blast radius for a frame.");
            Assert.IsTrue(Regex.IsMatch(src, @"void OnEnable\(\)[\s\S]{0,600}?_pulse = 0f"),
                "PrismWakeSource.OnEnable no longer restarts the pulse clock, so a reissued round's first front " +
                "would appear part-way out instead of at the round's own skin.");
        }

        [Test]
        public void TheFrontAndTheFuzeLitSphereAreOnePair()
        {
            // The two halves of the same weapon and the reason neither duplicates the other: the
            // armed fuze publishes a LIT SPHERE (Docs/LIT.md) that says WHERE this warhead will go
            // off, statically, in the shooter's domain colour; the front says HOW FAR, kinetically,
            // by sweeping out to the warhead's own radius. Two channels of the surface description,
            // one round. If the lit half is ever dropped the front is the only read left and this
            // pairing needs re-arguing rather than silently becoming a solo effect.
            string projectile = File.ReadAllText(ProjectilePath);
            Assert.IsTrue(projectile.Contains("PublishFuzeLit"),
                "Projectile no longer publishes its armed fuze as a LIT volume — the shockwave front was " +
                "designed as the kinetic half of that pair (Docs/PRISM_ANIMATION.md §4.7.3, Docs/LIT.md).");
        }
    }
}
#endif
