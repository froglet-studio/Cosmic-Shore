using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Voyages Milestone 1 (Shombith, 2026-11-16): the crew's job voyages are locked but a short list (the way home, and
    /// Crystal Run, the one crew job picked to make good first), and the vessel a seat names is the one the pilot flies in the voyage.
    /// </summary>
    public class VoyageMilestoneOneTests
    {
        static VoyageJob Job(string templateId) => new() { TemplateId = templateId };

        [Test]
        public void Jobs_AreLockedByDefault_ButTheWayHomeAndCrystalRun()
        {
            var settings = ScriptableObject.CreateInstance<VoyageToyDefinitionSO>();
            try
            {
                Assert.IsTrue(settings.IsJobOpen(Job(VoyageRules.ReturnJobId)), "Sail home must stay open.");
                Assert.IsTrue(settings.IsJobOpen(Job(VoyageToyDefinitionSO.CrystalRunJobId)), "Crystal Run must stay open.");
                Assert.IsFalse(settings.IsJobOpen(Job("job.hunt.sighting")), "A crew job sails while jobs are locked.");
                Assert.IsFalse(settings.IsJobOpen(null));
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void ShippedSettings_LockEveryJobBarTheOpenList()
        {
            var settings = VoyageToyDefinitionSO.Load();
            Assert.IsNotNull(settings, "No shipped voyage settings.");
            Assert.IsTrue(settings.IsJobOpen(Job(VoyageRules.ReturnJobId)), "The shipped settings lock Sail home.");
            Assert.IsFalse(settings.IsJobOpen(Job("job.not.on.the.list")), "The shipped settings do not lock jobs.");
        }

        [Test]
        public void ALockedJob_DoesNotSetSail()
        {
            Assert.IsFalse(VoyageLaunch.Begin(VoyageLaunchRequest.ForJob(Job("job.not.on.the.list"))));
            Assert.IsNull(VoyageLaunch.Pending, "A refused launch left a pending voyage behind.");
        }

        [Test]
        public void SeatHull_IsTheLocalSeatsVessel()
        {
            var squad = new SavedSquad("s", new SquadSlot(VesselClassType.Sparrow, Element.Charge),
                new SquadSlot(VesselClassType.Squirrel, Element.Mass), new SquadSlot(VesselClassType.Dolphin, Element.Space));
            Assert.AreEqual(VesselClassType.Sparrow, VoyageLaunch.SeatHull(squad), "The leader's own squad flies seat 1.");

            squad.OwnerId = "friend";
            squad.MySeat = 2;
            Assert.AreEqual(VesselClassType.Dolphin, VoyageLaunch.SeatHull(squad), "A joined squad flies the seat the player holds.");
            Assert.AreEqual(default(VesselClassType), VoyageLaunch.SeatHull(null));
        }
    }
}
