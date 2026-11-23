using CosmicShore.Gameplay;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// A crystal pickup's vessel chain runs once per machine (<see cref="VesselImpactor.RouteCrystalPickup(bool, bool)"/>).
    /// A spawned copy of another pilot's vessel used to run it from its own contact AND from the
    /// owner's broadcast, which left a fading ghost of the omni crystal on every watcher's screen
    /// when a Squirrel collected it.
    /// </summary>
    public class VesselCrystalPickupRouteTests
    {
        [Test]
        public void Offline_RunsLocally() =>
            Assert.AreEqual(VesselImpactor.CrystalPickupRoute.Local,
                VesselImpactor.RouteCrystalPickup(isSpawned: false, isOwner: false));

        [Test]
        public void Owner_Broadcasts() =>
            Assert.AreEqual(VesselImpactor.CrystalPickupRoute.Broadcast,
                VesselImpactor.RouteCrystalPickup(isSpawned: true, isOwner: true));

        [Test]
        public void RemoteCopy_LeavesItToTheOwnersBroadcast() =>
            Assert.AreEqual(VesselImpactor.CrystalPickupRoute.OwnerBroadcasts,
                VesselImpactor.RouteCrystalPickup(isSpawned: true, isOwner: false));
    }
}
