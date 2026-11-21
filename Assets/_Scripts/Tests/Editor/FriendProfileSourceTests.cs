#if UNITY_EDITOR
using CosmicShore.UI;
using NUnit.Framework;
using UnityEngine;
using Status = CosmicShore.UI.OnlineInfoEntry.Status;
using JoinMode = CosmicShore.UI.OnlineInfoEntry.JoinMode;

namespace CosmicShore.Tests
{
    /// <summary>
    /// An online row as the profile card's source (Docs/MENU_REDESIGN_B.md §47.3): the card shows exactly what the row
    /// draws, and the row says when that changes.
    /// </summary>
    public class FriendProfileSourceTests
    {
        GameObject _go;
        OnlineInfoEntry _row;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("OnlineRow");
            _row = _go.AddComponent<OnlineInfoEntry>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_go);

        [Test]
        public void AnEmptyRowHasNoProfile()
        {
            Assert.IsFalse(_row.TryGetProfile(out _));
        }

        [Test]
        public void TheProfileIsWhatTheRowDraws()
        {
            _row.Populate("p1", "Quill", null, Status.InLobby, 2, 4, null, onInvite: _ => { },
                          joinMode: JoinMode.Join, onJoin: _ => { });
            Assert.IsTrue(_row.TryGetProfile(out var p));
            Assert.AreEqual("p1", p.PlayerId);
            Assert.AreEqual("Quill", p.DisplayName);
            Assert.AreEqual(Status.InLobby, p.Status);
            Assert.AreEqual(2, p.PartyMemberCount);
            Assert.AreEqual(4, p.PartyMaxSlots);
            Assert.IsTrue(p.CanInvite);
            Assert.IsFalse(p.InvitePending);
            Assert.AreEqual(JoinMode.Join, p.Join);
        }

        [Test]
        public void AMatchIsNotInvitable()
        {
            _row.Populate("p2", "Wren", null, Status.InMatch, 0, 4, "Skim Race", onInvite: _ => { },
                          joinMode: JoinMode.Spectate, onSpectate: _ => { });
            Assert.IsTrue(_row.TryGetProfile(out var p));
            Assert.IsFalse(p.CanInvite, "a pilot mid-match can be watched, not invited");
            Assert.AreEqual("Skim Race", p.MatchName);
            Assert.AreEqual(JoinMode.Spectate, p.Join);
        }

        [Test]
        public void AStatusChangeIsAnnounced()
        {
            _row.Populate("p3", "Moss", null, Status.Online, 0, 4, null, onInvite: _ => { });
            int changes = 0;
            _row.ProfileChanged += () => changes++;
            _row.SetStatus(Status.InMatch, 0, 4, "Regatta");
            Assert.AreEqual(1, changes);
            Assert.IsTrue(_row.TryGetProfile(out var p));
            Assert.AreEqual(Status.InMatch, p.Status);
            Assert.AreEqual("Regatta", p.MatchName);
        }
    }
}
#endif
