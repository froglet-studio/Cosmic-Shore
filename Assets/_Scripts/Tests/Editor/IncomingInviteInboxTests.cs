#if UNITY_EDITOR
using System.Collections.Generic;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// IncomingInviteInbox tests - the recipient-side list behind the invite popup, the friends
    /// panel's Requests rows and the party panel's unread badge (R13 item 2, "an invite you miss
    /// is gone forever").
    ///
    /// WHY THIS MATTERS:
    /// The service used to remember ONE incoming invite, so two friends inviting at once took
    /// turns being "new" on every refresh tick (the popup bounced between them and the sound
    /// replayed each tick), the panel could only rehydrate the latest one, and declining one
    /// sender's invite dropped the other's. None of that needs a lobby to assert: the inbox is
    /// pure, with a hand-set clock, so every shape a two-machine play test cannot reliably
    /// produce is pinned here.
    /// </summary>
    [TestFixture]
    public class IncomingInviteInboxTests
    {
        const float Lifetime = 60f;

        static PartyInviteData Invite(string sender, string session = null, string name = null, int avatar = 0)
            => new(sender, session ?? $"session-{sender}", name ?? sender, avatar);

        static IncomingInviteInbox NewInbox(float lifetime = Lifetime) => new(lifetime);

        static List<string> PendingSenders(IncomingInviteInbox inbox, float now)
        {
            var list = new List<PartyInviteData>();
            inbox.CopyPending(list, now);
            var ids = new List<string>();
            foreach (var i in list) ids.Add(i.HostPlayerId);
            return ids;
        }

        /// <summary>One refresh tick's sweep where every listed sender still has a line for us.</summary>
        static int Sweep(IncomingInviteInbox inbox, float now, List<PartyInviteData> withdrawn, params string[] stillInviting)
        {
            var set = new HashSet<string>(stillInviting);
            return inbox.Sweep(set.Contains, _ => false, now, withdrawn);
        }

        #region Observe

        [Test]
        public void FirstInvite_IsNew_AndPendingAndUnseen()
        {
            var inbox = NewInbox();

            Assert.AreEqual(IncomingInviteInbox.Observation.New, inbox.Observe(Invite("a"), 10f, false));

            Assert.AreEqual(1, inbox.PendingCount(10f));
            Assert.AreEqual(1, inbox.UnseenCount(10f));
            Assert.IsTrue(inbox.IsPending("a", 10f));
        }

        [Test]
        public void SameSenderSameSession_IsDuplicate_NotRaisedAgain()
        {
            var inbox = NewInbox();
            inbox.Observe(Invite("a"), 10f, false);

            Assert.AreEqual(IncomingInviteInbox.Observation.Duplicate, inbox.Observe(Invite("a"), 11.5f, false));
            Assert.AreEqual(IncomingInviteInbox.Observation.Duplicate, inbox.Observe(Invite("a"), 13f, false));
            Assert.AreEqual(1, inbox.PendingCount(13f));
        }

        [Test]
        public void TwoSenders_BothStayPending_AndNeitherIsRaisedTwice()
        {
            // The measured defect: with a single cached invite the two lines alternated as "new"
            // on every tick. Three ticks with both lines present must raise each exactly once.
            var inbox = NewInbox();
            int raised = 0;

            for (int tick = 0; tick < 3; tick++)
            {
                float now = 10f + tick * 1.5f;
                if (inbox.Observe(Invite("a"), now, false) == IncomingInviteInbox.Observation.New) raised++;
                if (inbox.Observe(Invite("b"), now, false) == IncomingInviteInbox.Observation.New) raised++;
            }

            Assert.AreEqual(2, raised, "each sender's invite is raised once");
            Assert.AreEqual(2, inbox.PendingCount(13f));
            CollectionAssert.AreEqual(new[] { "a", "b" }, PendingSenders(inbox, 13f), "oldest first");
        }

        [Test]
        public void SameSenderNewSession_IsANewInvite_ReplacingTheOld()
        {
            var inbox = NewInbox();
            inbox.Observe(Invite("a", "s1"), 10f, false);
            inbox.MarkAllSeen();

            Assert.AreEqual(IncomingInviteInbox.Observation.New, inbox.Observe(Invite("a", "s2"), 30f, false));

            Assert.AreEqual(1, inbox.PendingCount(30f), "one record per sender");
            Assert.IsTrue(inbox.TryGetPending("a", 30f, out var current));
            Assert.AreEqual("s2", current.PartySessionId);
            Assert.AreEqual(1, inbox.UnseenCount(30f), "a new invite is unread again");
            Assert.AreEqual(Lifetime, inbox.SecondsRemaining("a", 30f), 0.001f, "and its clock restarted");
        }

        [Test]
        public void LineForTheSessionWeAreAlreadyIn_IsSuppressed_NotPending()
        {
            var inbox = NewInbox();

            Assert.AreEqual(IncomingInviteInbox.Observation.Suppressed, inbox.Observe(Invite("a"), 10f, alreadyInSenderSession: true));

            Assert.AreEqual(0, inbox.PendingCount(10f));
            Assert.AreEqual(0, inbox.UnseenCount(10f));
            Assert.AreEqual(IncomingInviteInbox.Observation.Refreshed, inbox.Observe(Invite("a"), 11f, false),
                "a later tick's same line only refreshes the resolved record");
        }

        #endregion

        #region Resolve

        [Test]
        public void ResolveOne_KeepsTheOtherSendersInvite()
        {
            var inbox = NewInbox();
            inbox.Observe(Invite("a"), 10f, false);
            inbox.Observe(Invite("b"), 11f, false);

            Assert.IsTrue(inbox.Resolve("a"));

            Assert.IsFalse(inbox.IsPending("a", 12f));
            Assert.IsTrue(inbox.IsPending("b", 12f));
            Assert.AreEqual(1, inbox.PendingCount(12f));
            Assert.IsFalse(inbox.Resolve("a"), "already resolved");
            Assert.IsFalse(inbox.Resolve("nobody"));
        }

        [Test]
        public void ResolveAll_EndsEverything_AndReportsWhetherAnyWasOpen()
        {
            var inbox = NewInbox();
            inbox.Observe(Invite("a"), 10f, false);
            inbox.Observe(Invite("b"), 11f, false);

            Assert.IsTrue(inbox.ResolveAll());
            Assert.AreEqual(0, inbox.PendingCount(12f));
            Assert.IsNull(inbox.Newest(12f));
            Assert.IsFalse(inbox.ResolveAll(), "nothing was open the second time");
        }

        [Test]
        public void ResolvedSenderSeenAgain_IsRefreshedSilently_WithTheNewSessionId()
        {
            // The accept flow: the line goes PENDING -> real session id after we accepted, and
            // the record must follow it without re-raising the popup.
            var inbox = NewInbox();
            inbox.Observe(Invite("a", "PENDING"), 10f, false);
            inbox.ResolveAll();

            Assert.AreEqual(IncomingInviteInbox.Observation.Refreshed, inbox.Observe(Invite("a", "real-id"), 12f, false));
            Assert.AreEqual(0, inbox.PendingCount(12f));
        }

        [Test]
        public void Clear_ForgetsEverything()
        {
            var inbox = NewInbox();
            inbox.Observe(Invite("a"), 10f, false);
            inbox.Clear();

            Assert.AreEqual(0, inbox.PendingCount(10f));
            Assert.AreEqual(IncomingInviteInbox.Observation.New, inbox.Observe(Invite("a"), 11f, false), "a fresh record after the reload");
        }

        #endregion

        #region Lifetime

        [Test]
        public void Expiry_EndsAnInviteAfterItsLifetime()
        {
            var inbox = NewInbox();
            inbox.Observe(Invite("a"), 10f, false);

            Assert.IsTrue(inbox.IsPending("a", 10f + Lifetime - 0.01f));
            Assert.AreEqual(0.01f, inbox.SecondsRemaining("a", 10f + Lifetime - 0.01f), 0.001f);

            Assert.IsFalse(inbox.IsPending("a", 10f + Lifetime));
            Assert.AreEqual(0f, inbox.SecondsRemaining("a", 10f + Lifetime));
            Assert.AreEqual(0, inbox.UnseenCount(10f + Lifetime), "an expired invite is not unread either");
        }

        [Test]
        public void ZeroLifetime_NeverExpires()
        {
            var inbox = NewInbox(0f);
            inbox.Observe(Invite("a"), 10f, false);

            Assert.IsTrue(inbox.IsPending("a", 1e6f));
            Assert.IsTrue(float.IsPositiveInfinity(inbox.SecondsRemaining("a", 1e6f)));
        }

        [Test]
        public void SecondsRemaining_CountsFromFirstObservation_NotFromTheLatestDuplicate()
        {
            // The panel's row clock: re-opening the panel 40 s in shows 20 s left, not a fresh 60.
            var inbox = NewInbox();
            inbox.Observe(Invite("a"), 10f, false);
            inbox.Observe(Invite("a"), 50f, false);

            Assert.AreEqual(20f, inbox.SecondsRemaining("a", 50f), 0.001f);
        }

        #endregion

        #region Seen (the badge)

        [Test]
        public void MarkAllSeen_ClearsUnread_KeepsPending_AndALaterInviteIsUnreadAgain()
        {
            var inbox = NewInbox();
            inbox.Observe(Invite("a"), 10f, false);
            inbox.Observe(Invite("b"), 11f, false);
            Assert.AreEqual(2, inbox.UnseenCount(11f));

            inbox.MarkAllSeen();

            Assert.AreEqual(0, inbox.UnseenCount(11f));
            Assert.AreEqual(2, inbox.PendingCount(11f), "seen is not answered");

            inbox.Observe(Invite("c"), 12f, false);
            Assert.AreEqual(1, inbox.UnseenCount(12f));
        }

        [Test]
        public void UnseenCount_ExcludesResolved()
        {
            var inbox = NewInbox();
            inbox.Observe(Invite("a"), 10f, false);
            inbox.Observe(Invite("b"), 11f, false);
            inbox.Resolve("a");

            Assert.AreEqual(1, inbox.UnseenCount(11f));
        }

        #endregion

        #region Sweep

        [Test]
        public void Sweep_LineGoneOneTick_KeepsTheInvite()
        {
            // A single stale lobby snapshot must not flicker a live invite.
            var inbox = NewInbox();
            inbox.Observe(Invite("a"), 10f, false);
            var withdrawn = new List<PartyInviteData>();

            Assert.AreEqual(0, Sweep(inbox, 11.5f, withdrawn));
            Assert.IsTrue(inbox.IsPending("a", 11.5f));
            Assert.IsEmpty(withdrawn);
        }

        [Test]
        public void Sweep_LineGoneTwoTicks_WithdrawsTheOpenInvite()
        {
            var inbox = NewInbox();
            inbox.Observe(Invite("a"), 10f, false);
            inbox.Observe(Invite("b"), 10f, false);
            var withdrawn = new List<PartyInviteData>();

            Sweep(inbox, 11.5f, withdrawn, "b");
            Assert.AreEqual(1, Sweep(inbox, 13f, withdrawn, "b"));

            Assert.AreEqual(1, withdrawn.Count);
            Assert.AreEqual("a", withdrawn[0].HostPlayerId);
            Assert.IsFalse(inbox.IsPending("a", 13f));
            Assert.IsTrue(inbox.IsPending("b", 13f), "the other sender's line is still there");
        }

        [Test]
        public void Sweep_ALinePresentResetsTheMissCount()
        {
            var inbox = NewInbox();
            inbox.Observe(Invite("a"), 10f, false);
            var withdrawn = new List<PartyInviteData>();

            Sweep(inbox, 11.5f, withdrawn);          // miss 1
            Sweep(inbox, 13f, withdrawn, "a");       // back
            Sweep(inbox, 14.5f, withdrawn);          // miss 1 again, not 2

            Assert.IsTrue(inbox.IsPending("a", 14.5f));
            Assert.IsEmpty(withdrawn);
        }

        [Test]
        public void Sweep_HeldInvite_IsLeftAlone()
        {
            // A join to it is in flight, or we are inside that sender's party.
            var inbox = NewInbox();
            inbox.Observe(Invite("a"), 10f, false);
            var withdrawn = new List<PartyInviteData>();

            for (int tick = 0; tick < 4; tick++)
                inbox.Sweep(_ => false, invite => invite.HostPlayerId == "a", 11.5f + tick * 1.5f, withdrawn);

            Assert.IsTrue(inbox.IsPending("a", 17f));
            Assert.IsEmpty(withdrawn);
        }

        [Test]
        public void Sweep_ResolvedRecordWhoseLineIsGone_IsDroppedSilently_SoTheNextLineIsANewInvite()
        {
            // The "re-invite after a decline" fix: once the declined invite's line is gone, a
            // later line from the same sender (even the same session id) is raised again.
            var inbox = NewInbox();
            inbox.Observe(Invite("a", "s1"), 10f, false);
            inbox.Resolve("a");
            var withdrawn = new List<PartyInviteData>();

            Sweep(inbox, 11.5f, withdrawn);
            Assert.AreEqual(0, Sweep(inbox, 13f, withdrawn), "nothing on screen was waiting on it");
            Assert.IsEmpty(withdrawn);

            Assert.AreEqual(IncomingInviteInbox.Observation.New, inbox.Observe(Invite("a", "s1"), 20f, false));
            Assert.IsTrue(inbox.IsPending("a", 20f));
        }

        [Test]
        public void Sweep_ExpiredWhileTheLineLingers_IsReportedOnce_AndNotRaisedAgainWhileItLingers()
        {
            var inbox = NewInbox();
            inbox.Observe(Invite("a"), 10f, false);
            var withdrawn = new List<PartyInviteData>();

            Assert.AreEqual(1, Sweep(inbox, 10f + Lifetime + 1f, withdrawn, "a"));
            Assert.AreEqual("a", withdrawn[0].HostPlayerId);

            withdrawn.Clear();
            Assert.AreEqual(0, Sweep(inbox, 10f + Lifetime + 2.5f, withdrawn, "a"), "reported once");
            Assert.AreEqual(IncomingInviteInbox.Observation.Refreshed, inbox.Observe(Invite("a"), 10f + Lifetime + 4f, false),
                "the lingering line is not a new invite");

            // Once the line is gone, the record goes with it and the sender can invite afresh.
            Sweep(inbox, 10f + Lifetime + 5.5f, withdrawn);
            Sweep(inbox, 10f + Lifetime + 7f, withdrawn);
            Assert.AreEqual(IncomingInviteInbox.Observation.New, inbox.Observe(Invite("a"), 10f + Lifetime + 8.5f, false));
        }

        #endregion

        #region Order

        [Test]
        public void NewestAndOldest_FollowArrivalOrder_SkippingResolved()
        {
            var inbox = NewInbox();
            inbox.Observe(Invite("a"), 10f, false);
            inbox.Observe(Invite("b"), 11f, false);
            inbox.Observe(Invite("c"), 12f, false);

            Assert.AreEqual("c", inbox.Newest(12f).Value.HostPlayerId);
            Assert.AreEqual("a", inbox.Oldest(12f).Value.HostPlayerId);

            inbox.Resolve("c");
            inbox.Resolve("a");
            Assert.AreEqual("b", inbox.Newest(12f).Value.HostPlayerId);
            Assert.AreEqual("b", inbox.Oldest(12f).Value.HostPlayerId);
            CollectionAssert.AreEqual(new[] { "b" }, PendingSenders(inbox, 12f));
        }

        #endregion

        #region The popup's detail line and the badge's label

        [Test]
        public void Notice_OneInvite_SaysWhereItWaitsAndForHowLong()
        {
            Assert.AreEqual("Also in your friends list for 60 s", PartyInviteNotificationPanel.Notice.Compose(1, 60f));
            Assert.AreEqual("Also in your friends list for 58 s", PartyInviteNotificationPanel.Notice.Compose(1, 57.2f), "rounded up");
            Assert.AreEqual("Also in your friends list for 1 s", PartyInviteNotificationPanel.Notice.Compose(1, 0.2f));
        }

        [Test]
        public void Notice_SeveralInvites_CountsThem()
        {
            Assert.AreEqual("2 invites waiting. Also in your friends list for 60 s", PartyInviteNotificationPanel.Notice.Compose(2, 60f));
            Assert.AreEqual("3 invites waiting. Also in your friends list", PartyInviteNotificationPanel.Notice.Compose(3, 0f));
        }

        [Test]
        public void Notice_UnknownLifetime_LeavesTheSecondsOff()
        {
            Assert.AreEqual("Also in your friends list", PartyInviteNotificationPanel.Notice.Compose(1, 0f));
            Assert.AreEqual("Also in your friends list", PartyInviteNotificationPanel.Notice.Compose(0, float.PositiveInfinity));
        }

        [Test]
        public void Notice_IsAscii()
        {
            // The UI font carries 97 glyphs; anything else is tofu.
            foreach (char c in PartyInviteNotificationPanel.Notice.Compose(2, 42f))
                Assert.LessOrEqual((int)c, 126, $"non-ASCII character {(int)c}");
        }

        [Test]
        public void BadgeLabel_EmptyForZero_NumberUpToCap_PlusPastIt()
        {
            Assert.AreEqual(string.Empty, PartyInviteBadge.Label.Format(0, 9));
            Assert.AreEqual(string.Empty, PartyInviteBadge.Label.Format(-1, 9));
            Assert.AreEqual("1", PartyInviteBadge.Label.Format(1, 9));
            Assert.AreEqual("9", PartyInviteBadge.Label.Format(9, 9));
            Assert.AreEqual("9+", PartyInviteBadge.Label.Format(12, 9));
            Assert.AreEqual("1+", PartyInviteBadge.Label.Format(2, 0), "a cap below 1 is treated as 1");
        }

        #endregion
    }
}
#endif
