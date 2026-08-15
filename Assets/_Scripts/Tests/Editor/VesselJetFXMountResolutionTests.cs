#if UNITY_EDITOR
using NUnit.Framework;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Guards the NAME half of the vessel jet FX mount resolver (Docs/VESSEL_JET_FX.md).
    ///
    /// WHY THIS MATTERS:
    /// Engine mounts are resolved by SUBSTRING match against every transform under a vessel,
    /// which makes the token lists quietly dangerous in both directions — a missing exclusion
    /// hangs a plume on a cowling or on an ability executor, and an over-broad exclusion deletes
    /// real engines. Both failures are silent in play: the vessel just looks a bit wrong, and
    /// nobody can tell whether that is art or wiring.
    ///
    /// The corpus below is the REAL transform-name set from the shipped vessel models, taken
    /// from the fleet audit. Testing against real names rather than invented ones is the point:
    /// the one bug this file exists to prevent already happened once, when "rig" was added to
    /// the exclusion list for the Serpent's EngineRig and silently deleted every RIGHT-side
    /// engine in the fleet, because "right" contains "rig".
    /// </summary>
    [TestFixture]
    public class VesselJetFXMountResolutionTests
    {
        VesselJetFXConfigSO _config;

        [SetUp]
        public void SetUp()
        {
            // A fresh instance carries the class's DEFAULT token lists, so this tests the
            // defaults developers inherit — not whatever a project asset happens to hold.
            _config = ScriptableObject.CreateInstance<VesselJetFXConfigSO>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_config);

        // --- Real engine mounts across the fleet must ALL resolve ------------------------

        [TestCase("Engine Left.1")]     // Dolphin
        [TestCase("Engine Left.2")]
        [TestCase("Engine Left.3")]
        [TestCase("Engine Right.1")]
        [TestCase("Engine Right.2")]
        [TestCase("Engine Right.3")]
        [TestCase("engine left")]       // Rhino
        [TestCase("engine right")]
        [TestCase("JetTopLeft")]        // Urchin
        [TestCase("JetTopRight")]
        [TestCase("JetBottomLeft")]
        [TestCase("JetBottomRight")]
        [TestCase("Ship_Wedge_Jet_UL")] // Grizzly
        [TestCase("Ship_Wedge_Jet_UR")]
        [TestCase("Ship_Wedge_Jet_BL")]
        [TestCase("Ship_Wedge_Jet_BR")]
        [TestCase("EngineBone")]        // Serpent
        [TestCase("bbone_BackEngineInner.L")]  // Squirrel
        [TestCase("bbone_FrontEngineBase.R")]
        [TestCase("jetT.r")]            // rigged variants (dolphin/urchin rigs)
        [TestCase("jetB.l")]
        [TestCase("jet.l")]
        public void RealEngineMounts_Resolve(string name)
        {
            Assert.IsTrue(_config.IsMountName(name),
                $"'{name}' is a real engine mount in a shipped vessel model and must resolve.");
        }

        /// <summary>
        /// The regression that motivated this file. Every right-side engine in the fleet is a
        /// separate assertion so a future over-broad token names its own victims.
        /// </summary>
        [TestCase("Engine Right.1")]
        [TestCase("Engine Right.2")]
        [TestCase("Engine Right.3")]
        [TestCase("engine right")]
        [TestCase("JetTopRight")]
        [TestCase("JetBottomRight")]
        public void RightSideEngines_AreNotSwallowedByASubstringToken(string name)
        {
            Assert.IsTrue(_config.IsMountName(name),
                $"'{name}' was rejected. A mount exclusion token is a SUBSTRING match and has " +
                "swallowed the word 'right' (this is exactly how the token 'rig' once deleted " +
                "half the fleet's engines). Check mountExcludeTokens.");
        }

        // --- Housings, shrouds and FX objects must NOT resolve ---------------------------

        [TestCase("Engine case Left.1")]      // Dolphin cowling, not a nozzle
        [TestCase("Engine case Right.3")]
        [TestCase("ShroudTopLeft")]           // Urchin shroud — the nozzle is its PARENT, JetTopLeft
        [TestCase("bbone_BackEngineFrame.L")] // Squirrel structural frame
        [TestCase("bbone_FrontEngineTrim.R")]
        [TestCase("jetholdT.r")]              // rig parent that carries the rest angle
        [TestCase("jetholdb.l")]
        [TestCase("JetFX")]                   // Rhino: an existing FX object
        [TestCase("JetTest")]
        [TestCase("LeftJetParticle")]
        [TestCase("RightJetParticle")]
        [TestCase("gunM.l")]                  // Urchin gun, not an engine
        public void HousingsAndFXObjects_DoNotResolve(string name)
        {
            Assert.IsFalse(_config.IsMountName(name),
                $"'{name}' is not an engine nozzle and must not receive a plume.");
        }

        [TestCase("Wing.L")]
        [TestCase("chassis.003")]
        [TestCase("b_Tail1.L")]
        [TestCase("Body")]
        [TestCase("OrientationHandle")]
        [TestCase("")]
        [TestCase(null)]
        public void UnrelatedNames_DoNotResolve(string name)
        {
            Assert.IsFalse(_config.IsMountName(name));
        }

        // --- Loose matching is the DETECTION test and must stay generous -----------------

        /// <summary>
        /// The loose test decides whether a vessel already AUTHORS its plumes. It must match the
        /// cowling names too, because the Squirrel's authored jets hang off bones the strict
        /// test correctly rejects — if loose matching missed them the Squirrel would be given a
        /// second full set of jets on top of its hand-tuned ones.
        /// </summary>
        [TestCase("bbone_BackEngineFrame.L")]
        [TestCase("bbone_FrontEngineTrim.R")]
        [TestCase("bbone_BackEngineInner.L")]
        [TestCase("Engine case Left.1")]
        [TestCase("JetFX")]
        public void LooseMatch_CatchesNamesTheStrictTestRejects(string name)
        {
            Assert.IsTrue(_config.IsMountNameLoose(name),
                $"'{name}' mentions an engine and must be caught by the authored-FX detection.");
        }

        [TestCase("Wing.L")]
        [TestCase("chassis.003")]
        [TestCase("Body")]
        public void LooseMatch_StillRejectsUnrelatedNames(string name)
        {
            Assert.IsFalse(_config.IsMountNameLoose(name));
        }

        [Test]
        public void StrictMatch_IsAlwaysASubsetOfLooseMatch()
        {
            foreach (var name in new[]
                     {
                         "Engine Left.1", "engine right", "JetTopLeft", "Ship_Wedge_Jet_UL",
                         "EngineBone", "Engine case Left.1", "JetFX", "Wing.L", "chassis.003",
                         "bbone_FrontEngineTrim.R", "jetholdT.r", "ShroudTopLeft",
                     })
            {
                if (_config.IsMountName(name))
                    Assert.IsTrue(_config.IsMountNameLoose(name),
                        $"'{name}' passes the strict test but fails the loose one — the " +
                        "detection pass would then miss FX the spawn pass creates.");
            }
        }

        // --- Config sanity ---------------------------------------------------------------

        [Test]
        public void DefaultConfig_IsSane() =>
            Assert.IsTrue(_config.IsSane, "The shipped defaults must pass the runtime sanity gate.");
    }
}
#endif
