#if UNITY_EDITOR
using CosmicShore.Gameplay;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The home screen's settle gate (<see cref="MenuCrystalClickHandler"/>): freestyle cannot be ENTERED
    /// while a cell is still putting its world together (the Garland grow-in at boot), so a press during
    /// the intro does not launch the player into the HyperSea before the menu has appeared.
    /// </summary>
    public class MenuFreestyleSettleGateTests
    {
        [TestCase(true, false, false, false, TestName = "Built, initialised, not swapping: settled")]
        [TestCase(false, false, false, true, TestName = "Not post-initialised: busy")]
        [TestCase(true, true, false, true, TestName = "Environment build pending: busy")]
        [TestCase(true, false, true, true, TestName = "Swapping its config: busy")]
        public void CellBusy(bool postInitialized, bool buildPending, bool swapping, bool expectedBusy) =>
            Assert.AreEqual(expectedBusy, MenuCrystalClickHandler.IsCellBusy(postInitialized, buildPending, swapping));
    }
}
#endif
