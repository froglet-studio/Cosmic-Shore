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
    /// The automated gate for the WAKE (Docs/PRISM_ANIMATION.md §4.7.3) — the fourth
    /// citizen of §4.7's global-uniform shape and the second member of the high-poly morph family.
    /// Its failure modes are SILENT, exactly like the cradle's: a graph that lost the node renders
    /// every prism as before and nothing logs; a bank length that drifts between the C# and the
    /// HLSL leaves the tail of the bank unread; a config past the folding amplitude turns prisms
    /// inside out only where a crest happens to land. Every check runs from assets and pure code
    /// alone — no play mode — and the ones about the map's mathematics are the same predicates the
    /// offline harness (Tools/Shaders/verify_prism_wake.py) measures, so the two cannot disagree.
    /// </summary>
    public class PrismWakeTests
    {
        const string HlslPath = "Assets/_Graphics/Materials/Graphs/PrismWake.hlsl";
        const string FunctionName = "PrismWakeDeform";
        const string CradleFunctionName = "PrismCradleDeform";
        const string VesselControllerPath = "Assets/_Scripts/Controller/Vessel/VesselController.cs";
        const string SourcePath = "Assets/_Scripts/Utility/PrismWakeSource.cs";
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
                         "float4 _PrismWakeAxis[PRISM_WAKE_SLOTS]",
                         "float4 _PrismWakeShape[PRISM_WAKE_SLOTS]",
                         "float4 _PrismWakeParams",
                     })
            {
                Assert.IsTrue(hlsl.Contains(decl), $"PrismWake.hlsl no longer declares `{decl}`.");
            }
            Assert.IsFalse(hlsl.Contains("CBUFFER_START"),
                "PrismWake.hlsl declares a CBUFFER — the bank must stay a file-scope global or SRP batching breaks.");

            var slots = Regex.Match(hlsl, @"#define PRISM_WAKE_SLOTS (\d+)");
            Assert.IsTrue(slots.Success, "PRISM_WAKE_SLOTS is not #defined in PrismWake.hlsl.");
            Assert.AreEqual(PrismWake.Slots, int.Parse(slots.Groups[1].Value),
                "PrismWake.Slots (C#) and PRISM_WAKE_SLOTS (HLSL) disagree — the publisher would write a bank " +
                "the shader reads at a different length. Change both together.");

            // The #ifndef dial the offline harness drives its NEGATIVE CONTROL through. A gate
            // nobody has watched fail is a gate nobody should trust, so the dial is part of the
            // contract rather than a debugging leftover.
            Assert.IsTrue(hlsl.Contains("#ifndef PRISM_WAKE_SHEAR_GAIN"),
                "PrismWake.hlsl no longer carries the PRISM_WAKE_SHEAR_GAIN dial — " +
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
                // see the rippled position; a wake applied after the drape would ripple the very
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
                foreach (var name in new[] { "_PrismWakeCentre", "_PrismWakeAxis", "_PrismWakeShape", "_PrismWakeParams" })
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
            // Mass being consumed is not mass a ship is passing — the same exclusion every
            // vertex-stage family on the live graphs makes.
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
                $"PrismWakeConfig is not sane (amplitude {config.Amplitude}, exponent {config.RadialExponent}, " +
                $"reach {config.ReachHullRadii}, train {config.TrainHullRadii}): the shader treats that as OFF.");
            Assert.Greater(config.Amplitude, 0f,
                "Amplitude is 0 — the shader's second sentinel reads that as off and nothing ripples. " +
                "Use `enabled` to switch the wake off.");
            Assert.GreaterOrEqual(config.RadialExponent, 1f,
                "RadialExponent below 1 makes the radial falloff's derivative diverge at exactly the distance " +
                "the wake is supposed to vanish without a rim.");
            Assert.Greater(config.FullSpeed, config.EngageSpeed,
                "FullSpeed must sit above EngageSpeed or the speed window is a step, which is the one thing " +
                "the ramp exists to avoid.");
        }

        [Test]
        public void Config_NeverFolds()
        {
            var config = ResolveConfig();
            // The whole no-fold guarantee in one predicate: the map's two stretch terms are
            // c = 1 + E and b = (1 + E) + r*dE/dr, and BOTH are bounded by the amplitude alone —
            // no hull radius, no reach, no wavelength — so this can never be invalidated by
            // retuning any of them. Tools/Shaders/verify_prism_wake.py measures the same claim
            // over the whole authored range by executing the shipped HLSL.
            Assert.IsTrue(config.NeverFolds,
                $"PrismWakeConfig's amplitude {config.Amplitude} is at or past the folding bound " +
                $"{PrismWakeConfigSO.FoldingAmplitude:0.000} — a crest could turn a prism inside out.");
            Assert.Less(PrismWakeConfigSO.MaxRadialFalloffSlope * 1f, 1f,
                "MaxRadialFalloffSlope is no longer a property of the falloff's SHAPE — re-derive the no-fold " +
                "bound before changing it.");
        }

        [Test]
        public void Config_ResidencySwapIsInvisible()
        {
            var config = ResolveConfig();

            // The whole point of the margin: a prism gains or loses its high-poly geometry only
            // where the ripple provably cannot have moved any of its vertices. A zero margin puts
            // the swap exactly ON the boundary, where a float comparison decides whether the player
            // sees a prism change shape.
            Assert.Greater(config.ResidencyMargin, 0f,
                "PrismWakeConfig.ResidencyMargin is 0 — the mesh swap would happen at exactly the surface the " +
                "wake reaches, so it is a coin toss whether the geometry change is visible.");

            // The budget. 0 means the ripple only ever runs on the authored 24-triangle prism —
            // the look two rounds of the cradle's history rejected; an unbounded one is the cost
            // nobody signed up for.
            Assert.Greater(config.MaxResidentPrisms, 0,
                "PrismWakeConfig.MaxResidentPrisms is 0 — nothing is ever swapped, so the ripple runs on the " +
                "authored 24-triangle prism and reads as facets hinging.");
            Assert.LessOrEqual(config.MaxResidentPrisms, 96,
                "PrismWakeConfig.MaxResidentPrisms is above 96 — this is the feature's entire performance budget " +
                "and 'a handful of prisms at a time' is what makes the high-poly swap affordable at all.");

            long tris = (long)config.MaxResidentPrisms * config.Subdivision * config.Subdivision * 2 * 6;
            Assert.Less(tris, 200000,
                $"The residency budget is {tris} triangles ({config.MaxResidentPrisms} prisms x subdivision " +
                $"{config.Subdivision}) — lower MaxResidentPrisms or Subdivision.");
        }

        [Test]
        public void DerivedGeometry_HasExactlyOneCopyOfEachFormula()
        {
            var config = ResolveConfig();
            const float radius = 7.5f;

            float reach = PrismWake.ReachFor(radius, config);
            float train = PrismWake.TrainLengthFor(radius, config);
            float k = PrismWake.WavenumberFor(radius, config);

            // A wake is SHIP-SIZED: the reach and the train are multiples of the hull's own radius,
            // so a big ship makes a big wake with nothing authored per vessel.
            Assert.AreEqual(radius * config.ReachHullRadii, reach, 1e-4f, "ReachFor is not a multiple of the hull radius");
            Assert.AreEqual(radius * config.TrainHullRadii, train, 1e-4f, "TrainLengthFor is not a multiple of the hull radius");

            // The wavenumber's whole job: exactly WavesPerTrain full waves fit in one train. This
            // is the formula the shader does NOT re-derive — it arrives already derived — so this
            // test is what stands in for the shader agreeing with the publisher.
            Assert.AreEqual(config.WavesPerTrain, k * train / (2f * Mathf.PI), 1e-4f,
                "WavenumberFor does not put WavesPerTrain full waves in one train length.");

            // The phase advances with SPEED, so a crest holds still in the world while the ship
            // flies out from under it; a parked ship's wave does not travel.
            Assert.AreEqual(0f, PrismWake.PhaseRateFor(radius, 0f, config), 1e-6f,
                "A stationary hull's wave still travels.");
            Assert.Greater(PrismWake.PhaseRateFor(radius, 400f, config),
                           PrismWake.PhaseRateFor(radius, 200f, config),
                           "The wave does not travel faster when the ship does.");

            // A zero radius is a hull nothing has measured yet: it must produce no wake rather than
            // a division by zero in the wavenumber.
            Assert.AreEqual(0f, PrismWake.WavenumberFor(0f, config), 1e-6f,
                "An unmeasured hull (radius 0) does not fall back to a zero wavenumber.");
        }

        [Test]
        public void SpeedWindow_IsTheOnePlaceASpeedBecomesAWake()
        {
            var config = ResolveConfig();
            Assert.AreEqual(0f, config.StrengthForSpeed(0f), 1e-6f, "a parked ship has a wake");
            Assert.AreEqual(0f, config.StrengthForSpeed(config.EngageSpeed), 1e-6f,
                "the wake is already live AT the engage speed — the ramp has to start there, not before it");
            Assert.AreEqual(1f, config.StrengthForSpeed(config.FullSpeed), 1e-6f,
                "the wake is not full at the full speed");
            Assert.AreEqual(1f, config.StrengthForSpeed(config.FullSpeed * 10f), 1e-6f,
                "the wake keeps growing past the full speed — the ramp must saturate");
            float mid = config.StrengthForSpeed(0.5f * (config.EngageSpeed + config.FullSpeed));
            Assert.Greater(mid, 0f, "the window is a step, not a ramp");
            Assert.Less(mid, 1f, "the window is a step, not a ramp");
        }

        [Test]
        public void NoVesselIsGrantedAWake_TheCarriersAre()
        {
            Assert.IsTrue(File.Exists(VesselControllerPath), $"{VesselControllerPath} is missing.");
            string vessel = File.ReadAllText(VesselControllerPath);

            // The wake shipped on every vessel for one playtest and was pulled: a ripple behind
            // every hull is wallpaper, and the high-poly residency budget is SHARED, so a
            // per-vessel grant splits it until no wake is smooth. Re-adding an ensure here is the
            // specific regression this guards, and it would look entirely reasonable in a diff.
            Assert.IsFalse(vessel.Contains("AddComponent<PrismWakeSource>()"),
                "VesselController grants a PrismWakeSource again. The wake belongs to the two CARRIERS " +
                "(the skyburst missile and the Scarab ball), not to the fleet — see Docs/PRISM_ANIMATION.md §4.7.3.");

            foreach (var (path, label) in new[] { (ProjectilePath, "Projectile"), (BallPath, "AstroLeagueBall") })
            {
                Assert.IsTrue(File.Exists(path), $"{path} is missing.");
                string src = File.ReadAllText(path);
                Assert.IsTrue(src.Contains("IPrismWakeCarrier"),
                    $"{label} no longer implements IPrismWakeCarrier — its wake would fall back to a transform " +
                    "delta, which cannot tell a flight step from a pool reissue and reads an interpolated " +
                    "transform on a peer instead of the replicated velocity.");
                Assert.IsTrue(src.Contains("AddComponent<PrismWakeSource>()"),
                    $"{label} no longer grants itself a PrismWakeSource, so nothing publishes its wake.");
            }
        }

        [Test]
        public void TheSkyburstIsTheOneRoundAuthoredToLeaveAWake()
        {
            Assert.IsTrue(File.Exists(SkyburstPrefab), $"{SkyburstPrefab} is missing.");
            Assert.IsTrue(File.ReadAllText(SkyburstPrefab).Contains("leavesWake: 1"),
                "SkyBurstProjectile.prefab no longer authors leavesWake — the missile would fly with no wake, " +
                "and nothing would report it.");

            // Every OTHER round must leave it false. 54 bullets are alive at once on a firing
            // Sparrow, and 54 wakes is both wallpaper and the whole shared residency budget.
            foreach (var path in Directory.GetFiles("Assets/_Prefabs", "*.prefab", SearchOption.AllDirectories))
            {
                if (path.Replace('\\', '/').EndsWith("SkyBurstProjectile.prefab")) continue;
                Assert.IsFalse(File.ReadAllText(path).Contains("leavesWake: 1"),
                    $"{path} authors leavesWake — only the skyburst may. Adding a carrier is a design call.");
            }
        }

        [Test]
        public void TheSourceAsksACapability_AndFallsBackRatherThanFailingSilently()
        {
            Assert.IsTrue(File.Exists(SourcePath), $"{SourcePath} is missing.");
            string src = File.ReadAllText(SourcePath);

            Assert.IsTrue(src.Contains("IPrismWakeCarrier"),
                "PrismWakeSource no longer resolves a carrier — it would be back to reading one concrete type.");
            Assert.IsFalse(src.Contains("IVesselStatus"),
                "PrismWakeSource still reads IVesselStatus. It is no longer a vessel component; a carrier " +
                "answers for its own motion, and a vessel dependency here is what made it un-droppable " +
                "onto a projectile in the first place.");

            // A pooled round is repositioned while disabled, so everything describing the last
            // flight has to be dropped on reuse — otherwise the first frame publishes a wake from
            // the previous detonation to this launch bay at an absurd speed.
            Assert.IsTrue(src.Contains("void OnEnable()") && src.Contains("_hasPreviousPosition = false"),
                "PrismWakeSource no longer resets its motion state in OnEnable — a pooled carrier would " +
                "publish one frame of garbage velocity on every reissue.");
        }

    }
}
#endif
