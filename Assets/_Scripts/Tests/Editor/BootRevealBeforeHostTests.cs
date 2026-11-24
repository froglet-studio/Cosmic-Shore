using CosmicShore.Core;
using CosmicShore.Gameplay;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The boot no longer waits for the Relay host to build and show the menu (2026-11-21, the 41% "Creating
    /// session" wait): the boot world lays while nothing listens (<see cref="Cell.BuildsBeforeHost"/>), and the menu
    /// is revealed with input held once it stands (<see cref="SceneLoader.RevealsBootMenuBeforeHost"/>).
    /// </summary>
    public class BootRevealBeforeHostTests
    {
        [Test]
        public void TheBootWorld_LaysWhileNothingListens() =>
            Assert.IsTrue(Cell.BuildsBeforeHost(bootWorld: true, networkListening: false));

        [Test]
        public void TheBootWorld_WaitsForTheVessel_OnceAHostListens() =>
            Assert.IsFalse(Cell.BuildsBeforeHost(bootWorld: true, networkListening: true));

        [Test]
        public void AGameWorld_NeverLaysBeforeTheHost() =>
            Assert.IsFalse(Cell.BuildsBeforeHost(bootWorld: false, networkListening: false));

        [Test]
        public void TheBootMenu_IsRevealedBeforeTheHost() =>
            Assert.IsTrue(SceneLoader.RevealsBootMenuBeforeHost(true, menuReturn: false, networkListening: false,
                localPlayerReady: false, worldCovering: true));

        [Test]
        public void AReturnFromAGame_KeepsTheOrdinaryReveal() =>
            Assert.IsFalse(SceneLoader.RevealsBootMenuBeforeHost(true, menuReturn: true, networkListening: false,
                localPlayerReady: false, worldCovering: true));

        [Test]
        public void AListeningHost_KeepsTheOrdinaryReveal() =>
            Assert.IsFalse(SceneLoader.RevealsBootMenuBeforeHost(true, menuReturn: false, networkListening: true,
                localPlayerReady: false, worldCovering: true));

        [Test]
        public void AMenuWithoutABootWorld_KeepsTheOrdinaryReveal() =>
            Assert.IsFalse(SceneLoader.RevealsBootMenuBeforeHost(true, menuReturn: false, networkListening: false,
                localPlayerReady: false, worldCovering: false));

        [Test]
        public void SwitchedOff_KeepsTheOrdinaryReveal() =>
            Assert.IsFalse(SceneLoader.RevealsBootMenuBeforeHost(false, menuReturn: false, networkListening: false,
                localPlayerReady: false, worldCovering: true));
    }
}
