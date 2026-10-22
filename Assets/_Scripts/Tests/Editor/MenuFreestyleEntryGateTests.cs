#if UNITY_EDITOR
using CosmicShore.Gameplay;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// <see cref="MenuCrystalClickHandler"/>'s entry gate: freestyle cannot be ENTERED while the boot veil
    /// (<see cref="EnvironmentLoadVeil"/>) still covers the menu, so a press during the Garland intro through a
    /// route the cover and Home's double tap do not stop (the pad's Select, Submit, the toybox modals) does not
    /// launch the player into the HyperSea. Once the veil is down nothing is gated.
    /// </summary>
    public class MenuFreestyleEntryGateTests
    {
        [Test]
        public void BehindTheBootVeil_EntryIsRefused() =>
            Assert.IsFalse(MenuCrystalClickHandler.EntryAllowed(bootVeilCovering: true));

        [Test]
        public void OnceTheMenuIsRevealed_EntryIsAllowed() =>
            Assert.IsTrue(MenuCrystalClickHandler.EntryAllowed(bootVeilCovering: false));
    }
}
#endif
