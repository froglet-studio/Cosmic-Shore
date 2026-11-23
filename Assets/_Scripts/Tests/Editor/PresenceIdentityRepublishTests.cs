using CosmicShore.Gameplay;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The boot's presence join already carries the player's name and avatar, so the republish after it runs only
    /// when they changed during the join (<see cref="HostConnectionService.IdentityDiffers"/>). The unneeded
    /// republish was a lobby read and write landing on the party session's create, which UGS then refused as
    /// rate-limited on most measured boots (2026-11-21).
    /// </summary>
    public class PresenceIdentityRepublishTests
    {
        [Test]
        public void TheIdentityTheJoinCarried_IsNotRepublished() =>
            Assert.IsFalse(HostConnectionService.IdentityDiffers("ys1", "3", "ys1", 3));

        [Test]
        public void AnEmptyLocalName_ShowsAsPilot_AsTheJoinWritesIt() =>
            Assert.IsFalse(HostConnectionService.IdentityDiffers("Pilot", "0", "", 0));

        [Test]
        public void ANameThatArrivedDuringTheJoin_IsRepublished() =>
            Assert.IsTrue(HostConnectionService.IdentityDiffers("Pilot", "3", "ys1", 3));

        [Test]
        public void AnAvatarThatChanged_IsRepublished() =>
            Assert.IsTrue(HostConnectionService.IdentityDiffers("ys1", "3", "ys1", 7));

        [Test]
        public void ALobbyWithoutTheKeys_IsRepublished() =>
            Assert.IsTrue(HostConnectionService.IdentityDiffers(null, null, "ys1", 3));
    }
}
