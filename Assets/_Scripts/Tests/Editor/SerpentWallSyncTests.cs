using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.UI;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Serpent's wall on every peer (R_VesselActions/SERPENT_SEED_WALL.md, "Multiplayer"; Garrett's Brood Rush
    /// playtest, 2026-11: "the Serpent's wall was still not syncing"). Three ways a wall stood on one machine only:
    /// an AI's cloak press ran on the host alone (VesselActionTape is local), the stance's wall was built only
    /// where the press could write the replicated flag, and the wall's size and reach read each peer's own,
    /// unreplicated, Mass and Space levels.
    /// </summary>
    public class SerpentWallSyncTests
    {
        readonly List<Object> _made = new();

        T Make<T>(string name) where T : ScriptableObject
        {
            var so = ScriptableObject.CreateInstance<T>();
            so.name = name;
            _made.Add(so);
            return so;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _made) if (o) Object.DestroyImmediate(o);
            _made.Clear();
        }

        [Test]
        public void TheWallsAbilitiesSendAnAutopilotsPressToEveryPeer()
        {
            Assert.IsTrue(Make<CloakSeedWallActionSO>("CloakSeedWallAction").AutopilotPressTravels, "the cloak lays the wall");
            Assert.IsTrue(Make<SeedWallActionSO>("SeedWallAction").AutopilotPressTravels, "the seed grows the wall");
            Assert.IsTrue(Make<SniperScopeActionSO>("SniperScopeAction").AutopilotPressTravels,
                "the cloak asks the scope on every peer, so the scope has to be up on every peer too");
            Assert.IsFalse(Make<ConsumeBoostActionSO>("ConsumeBoostAction").AutopilotPressTravels,
                "an action is local unless it says otherwise: a boost is motion, which the transform carries");
        }

        [Test]
        public void AnAutopilotsActionIsNamedByWhereTheInputMapsHoldIt()
        {
            var shot = Make<SniperShotActionSO>("SniperShotAction");
            var cloak = Make<CloakSeedWallActionSO>("CloakSeedWallAction");
            var stance = Make<ToggleTranslationModeActionSO>("ToggleStationaryModeAction");
            var shared = new Dictionary<InputEvents, List<ShipActionSO>>
            {
                [InputEvents.Button1Action] = new() { stance },
                [InputEvents.Button2Action] = new() { shot, cloak },
            };
            var maps = new[] { shared, new Dictionary<InputEvents, List<ShipActionSO>>(), null };

            Assert.IsTrue(R_VesselActionHandler.TryLocateBoundAction(maps, cloak, out var map, out var ie, out var slot));
            Assert.AreEqual((0, InputEvents.Button2Action, 1), ((int)map, ie, (int)slot));
            Assert.AreSame(cloak, R_VesselActionHandler.BoundActionAt(maps, map, ie, slot),
                "every peer resolves the same place to the same action, not to the shot that shares the trigger");

            // An AI pilot presses its own clone of the asset, which no map holds: found by the asset's name.
            var aiClone = Object.Instantiate(cloak);
            _made.Add(aiClone);
            Assert.IsTrue(R_VesselActionHandler.TryLocateBoundAction(maps, aiClone, out map, out ie, out slot));
            Assert.AreSame(cloak, R_VesselActionHandler.BoundActionAt(maps, map, ie, slot));

            // Not bound at all: nothing to send, so the caller runs it locally as before.
            Assert.IsFalse(R_VesselActionHandler.TryLocateBoundAction(maps, Make<ConsumeBoostActionSO>("ConsumeBoostAction"), out _, out _, out _));
            Assert.IsNull(R_VesselActionHandler.BoundActionAt(maps, 0, InputEvents.Button2Action, 5));
            Assert.IsNull(R_VesselActionHandler.BoundActionAt(maps, 2, InputEvents.Button2Action, 0));
            Assert.IsNull(R_VesselActionHandler.BoundActionAt(maps, 7, InputEvents.Button2Action, 0));
        }

        [Test]
        public void APeerWithoutAuthorityFollowsTheReplicatedStance()
        {
            // The third machine: the press returned early there, so the flag is what starts and ends the held wall.
            Assert.IsTrue(ToggleTranslationModeActionExecutor.NeedsMirror(false, replicatedStance: true, appliedStance: false));
            Assert.IsTrue(ToggleTranslationModeActionExecutor.NeedsMirror(false, replicatedStance: false, appliedStance: true));
            Assert.IsFalse(ToggleTranslationModeActionExecutor.NeedsMirror(false, replicatedStance: true, appliedStance: true));
            // The owner and the server ran the press itself: mirroring would seed a second wall.
            Assert.IsFalse(ToggleTranslationModeActionExecutor.NeedsMirror(true, replicatedStance: true, appliedStance: false));
        }

        [Test]
        public void TheReplicatedMultiplierIsTheSameLerpAtAnIntegerLevel()
        {
            Assert.AreEqual(1f, ElementalScaling.MultiplierAt(0f), 1e-6f, "at rest the base is kept");
            Assert.AreEqual(2f, ElementalScaling.MultiplierAt(1f, atFull: 2f), 1e-6f);
            Assert.AreEqual(1.5f, ElementalScaling.MultiplierAt(0.5f, atFull: 2f), 1e-6f);
            Assert.AreEqual(0.5f, ElementalScaling.MultiplierAt(-1f, atFull: 2f, minMul: 0.5f), 1e-6f, "floored");
            // Off-vessel there is nothing to agree with: the base.
            Assert.AreEqual(1f, ElementalScaling.ReplicatedMultiplier(null, Element.Mass, 2f, 0.5f), 1e-6f);
        }
    }
}
