using CosmicShore.UI;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Scarab's Charge card shows the punch's volume white while the blast is ready, the volume with the claimed
    /// prisms cut out for a beat as it lands, then the volume grey for the recharge
    /// (<see cref="ScarabHUDView.BlastPartsFor"/>, the states sheet of Tools/Build/icon_kit/specs/scarab.py). A slip
    /// here shows both volumes at once, or none, or a ready card in grey - "can I punch" is the one question it answers.
    /// </summary>
    public class ScarabBlastCardTests
    {
        [Test]
        public void Ready_ShowsTheWholeVolumeLit()
        {
            var p = ScarabHUDView.BlastPartsFor(true, false);
            Assert.IsTrue(p.Field);
            Assert.IsFalse(p.Shattered);
            Assert.IsFalse(p.Spent);
        }

        [Test]
        public void Landing_ShowsOnlyTheShatteredVolumeLit()
        {
            var p = ScarabHUDView.BlastPartsFor(false, true);
            Assert.IsFalse(p.Field);
            Assert.IsTrue(p.Shattered);
            Assert.IsFalse(p.Spent);
        }

        [Test]
        public void Recharging_ShowsTheWholeVolumeGrey()
        {
            var p = ScarabHUDView.BlastPartsFor(false, false);
            Assert.IsTrue(p.Field);
            Assert.IsFalse(p.Shattered);
            Assert.IsTrue(p.Spent);
        }

        [Test]
        public void ReadyIgnoresALandingBeat()
        {
            var p = ScarabHUDView.BlastPartsFor(true, true);
            Assert.IsTrue(p.Field);
            Assert.IsFalse(p.Shattered);
            Assert.IsFalse(p.Spent);
        }
    }
}
