#if UNITY_EDITOR
using CosmicShore.UI;
using NUnit.Framework;
using Status = CosmicShore.UI.OnlineInfoEntry.Status;
using JoinMode = CosmicShore.UI.OnlineInfoEntry.JoinMode;
using Group = CosmicShore.UI.FriendsListPanel.OnlineGroupKind;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Rev 40 E, the friends tray (Docs/MENU_REDESIGN_B.md section 107): one verb per online row in one slot, rows
    /// grouped by that verb, and the party card's summary line built from authored words.
    /// </summary>
    [TestFixture]
    public class PartyTrayTests
    {
        // ── One verb per row ───────────────────────────────────────────────────────────────────

        [Test]
        public void ASoloPilotInTheMenu_RowSaysInvite_ButJoinStaysForTheProfileCard()
        {
            // Every pilot in the menu hosts their own eager party, so the old row offered INVITE and JOIN side by side.
            // The row now shows INVITE alone; Join stays on its state so the profile card can offer it (2026-10-31).
            Assert.AreEqual((true, JoinMode.Join), FriendsListPanel.OneVerb(Status.Online, JoinMode.Join));
            Assert.AreEqual((true, false, false), KitOnlineRow.VisibleParts(true, true, true, false, preferInvite: true),
                "the row's one button is INVITE");
            Assert.AreEqual((false, false, true), KitOnlineRow.VisibleParts(true, true, true, true, preferInvite: true),
                "an invite out still shows only Cancel");
        }

        [Test]
        public void APilotInAnotherParty_IsJoined_NotInvited()
        {
            Assert.AreEqual((false, JoinMode.Join), FriendsListPanel.OneVerb(Status.InLobby, JoinMode.Join));
            Assert.AreEqual((false, JoinMode.JoinDisabled), FriendsListPanel.OneVerb(Status.LobbyFull, JoinMode.JoinDisabled));
        }

        [Test]
        public void APilotInAMatch_IsSpectated()
        {
            Assert.AreEqual((false, JoinMode.Spectate), FriendsListPanel.OneVerb(Status.InMatch, JoinMode.Spectate));
        }

        [Test]
        public void TheRowShowsOneButton_CancelThenJoinThenInvite()
        {
            Assert.AreEqual((false, false, true), KitOnlineRow.VisibleParts(true, true, true, true), "an invite out: only Cancel");
            Assert.AreEqual((false, true, false), KitOnlineRow.VisibleParts(true, true, true, false), "INVITE and SPECTATE overlapped");
            Assert.AreEqual((true, false, false), KitOnlineRow.VisibleParts(true, true, false, false));
            Assert.AreEqual((false, false, false), KitOnlineRow.VisibleParts(true, false, false, false), "nothing to do: no button");
            Assert.AreEqual((true, true, false), KitOnlineRow.VisibleParts(false, true, true, false), "off: every verb, as before");
        }

        // ── Groups ─────────────────────────────────────────────────────────────────────────────

        [Test]
        public void RowsAreGroupedByWhatCanBeDoneWithThem()
        {
            Assert.AreEqual(Group.InTheMenu, FriendsListPanel.GroupFor(Status.Online, false));
            Assert.AreEqual(Group.JoinTheirParty, FriendsListPanel.GroupFor(Status.InLobby, false));
            Assert.AreEqual(Group.JoinTheirParty, FriendsListPanel.GroupFor(Status.LobbyFull, false));
            Assert.AreEqual(Group.InAMatch, FriendsListPanel.GroupFor(Status.InMatch, false));
            Assert.AreEqual(Group.YouInvited, FriendsListPanel.GroupFor(Status.Online, true), "an invite out wins over the status");
        }

        // ── The party card's summary ───────────────────────────────────────────────────────────

        const string Template = "{host} - {seats}";

        string Summary(string host, int open) =>
            ArcadeLobbyList.PartySummary(Template, host, open, "1 seat open", "{count} seats open", "Party full");

        [Test]
        public void TheSummary_SaysWhoHostsAndTheOpenSeats()
        {
            Assert.AreEqual("You host - 1 seat open", Summary("You host", 1));
            Assert.AreEqual("Kai hosts - 3 seats open", Summary("Kai hosts", 3));
            Assert.AreEqual("You host - Party full", Summary("You host", 0));
        }

        [Test]
        public void AnEmptyTemplate_ShowsNothing()
        {
            Assert.AreEqual(string.Empty,
                ArcadeLobbyList.PartySummary(string.Empty, "You host", 2, "1 seat open", "{count} seats open", "Party full"),
                "a line is deleted by blanking its field (CLAUDE.md, player-facing text)");
        }
    }
}
#endif
