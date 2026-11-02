#if UNITY_EDITOR
using CosmicShore.Gameplay;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The two invite bugs from the multiplayer test of 2026-11-02 (plan item 2).
    ///
    /// (a) A re-invite did nothing. A re-click on a pending invite only refreshed the local timeout - no network
    ///     write - and even a re-published line read as a duplicate to the invitee (same host, same session). So an
    ///     invitee who missed the first invite never saw the second (the phone's first two invites failed this way).
    ///     Now each SEND carries a serial, a re-click after a few seconds goes out again, and a line with a new
    ///     serial from the same host surfaces as new.
    /// (b) After every invite expired the host stayed Inviting for the rest of the session. The state machine
    ///     allowed Inviting -> InParty ("all invites cancelled") but nothing took that transition.
    /// </summary>
    [TestFixture]
    public class InviteResendTests
    {
        // ── (a) the line carries a send serial ─────────────────────────────

        [Test]
        public void EachSend_GetsANewSerial()
        {
            var invites = new InviteService();
            invites.AddOrRefresh("p2", "sess", "me", "Me", 0, float.MaxValue);
            int first = InviteService.ParseSerial(invites.SerializeAll());

            invites.AddOrRefresh("p2", "sess", "me", "Me", 0, float.MaxValue);   // the re-click that goes out again
            int second = InviteService.ParseSerial(invites.SerializeAll());

            Assert.Greater(first, 0);
            Assert.AreNotEqual(first, second, "a re-send must look different to the invitee");
            Assert.AreEqual(1, invites.OutgoingCount, "still one invite for one target");
        }

        [Test]
        public void TimeoutRefresh_KeepsTheSerial()
        {
            var invites = new InviteService();
            invites.AddOrRefresh("p2", "sess", "me", "Me", 0, 10f);
            string before = invites.SerializeAll();
            invites.RefreshTimeout("p2", 70f);
            Assert.AreEqual(before, invites.SerializeAll(), "a refresh is not a send; the invitee must not see a new invite");
        }

        [Test]
        public void SixFieldLine_StillParses_AndAFiveFieldLineIsSerialZero()
        {
            var parsed = InviteService.ParseLine("p2|host|sess|Host|3|7");
            Assert.IsTrue(parsed.HasValue);
            Assert.AreEqual("p2", parsed.Value.targetId);
            Assert.AreEqual("sess", parsed.Value.invite.PartySessionId);
            Assert.AreEqual(3, parsed.Value.invite.HostAvatarId);
            Assert.AreEqual(7, InviteService.ParseSerial("p2|host|sess|Host|3|7"));

            Assert.IsTrue(InviteService.ParseLine("p2|host|sess|Host|3").HasValue, "an older build's line");
            Assert.AreEqual(0, InviteService.ParseSerial("p2|host|sess|Host|3"));
            Assert.AreEqual(0, InviteService.ParseSerial("p2|host|sess|Host|3|x"));
            Assert.AreEqual(0, InviteService.ParseSerial(""));
        }

        [Test]
        public void Reinvite_IsANewSerialFromTheSameHost()
        {
            Assert.IsTrue(HostConnectionService.IsReinvite(true, "host", 1, "host", 2));
            Assert.IsFalse(HostConnectionService.IsReinvite(true, "host", 2, "host", 2), "the same line read again");
            Assert.IsFalse(HostConnectionService.IsReinvite(true, "host", 1, "other", 2), "another host is a new invite by the old rules");
            Assert.IsFalse(HostConnectionService.IsReinvite(false, null, 0, "host", 2), "nothing surfaced yet: the old rules raise it");
            Assert.IsFalse(HostConnectionService.IsReinvite(true, "host", 0, "host", 0), "older builds keep the old duplicate rules");
            Assert.IsFalse(HostConnectionService.IsReinvite(true, "host", 0, "host", 3), "no serial on record to compare with");
        }

        [Test]
        public void Resend_OnlyAfterTheDebounce()
        {
            Assert.IsFalse(HostConnectionService.ShouldResendInvite(100f, 101f), "a double-click stays local");
            Assert.IsTrue(HostConnectionService.ShouldResendInvite(100f, 106f));
            Assert.IsTrue(HostConnectionService.ShouldResendInvite(float.NegativeInfinity, 1f), "never sent from here");
        }

        // ── (b) the last invite cleared returns the host to InParty ────────

        [Test]
        public void LastInviteCleared_WhileInviting_ReturnsToInParty() =>
            Assert.AreEqual(PartyState.InParty, HostConnectionService.StateAfterInviteCleared(PartyState.Inviting, 0));

        [Test]
        public void InviteCleared_WithOthersStillPending_StaysInviting() =>
            Assert.IsNull(HostConnectionService.StateAfterInviteCleared(PartyState.Inviting, 1));

        [TestCase(PartyState.InParty)]
        [TestCase(PartyState.HostingParty)]
        [TestCase(PartyState.JoiningParty)]
        [TestCase(PartyState.Reconnecting)]
        public void InviteCleared_OutsideInviting_ChangesNothing(PartyState state) =>
            Assert.IsNull(HostConnectionService.StateAfterInviteCleared(state, 0));

        [Test]
        public void TheReturnTransition_IsOneTheStateMachineAllows()
        {
            var sm = new PartyStateMachine();
            Assert.IsTrue(sm.TryTransition(PartyState.InPresenceLobby));
            Assert.IsTrue(sm.TryTransition(PartyState.HostingParty));
            Assert.IsTrue(sm.TryTransition(PartyState.InParty));
            Assert.IsTrue(sm.TryTransition(PartyState.Inviting));
            Assert.IsTrue(sm.TryTransition(PartyState.InParty), "Inviting -> InParty: all invites cleared");
        }
    }
}
#endif
