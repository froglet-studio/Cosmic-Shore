using System.Collections.Generic;
using System.Linq;
using CosmicShore.Engine.Networking;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The lobby rules every session backend shares (<see cref="SessionRules"/>), without sockets.
    /// </summary>
    public class SessionRulesTests
    {
        static SessionRecord Session(string host, long now, int max = 0, bool locked = false)
        {
            var rec = SessionRules.NewSession(new SessionOptions { MaxPlayers = max, IsLocked = locked }, host, now);
            return rec;
        }

        [Fact]
        public void Join_RefusesAGoneHost_ALockedSession_AndAFullOne()
        {
            long now = SessionRules.NowTicks;
            Assert.Equal(SessionError.SessionNotFound, SessionRules.Join(null, "x", "bob", null, now)?.Error);

            var stale = Session("alice", now - SessionRules.PlayerTimeout.Ticks * 2);
            Assert.Equal(SessionError.SessionNotFound, SessionRules.Join(stale, stale.Id, "bob", null, now)?.Error);

            var locked = Session("alice", now, locked: true);
            Assert.Equal("Session is locked.", SessionRules.Join(locked, locked.Id, "bob", null, now)?.Message);

            var full = Session("alice", now, max: 1);
            Assert.Equal("Session is full.", SessionRules.Join(full, full.Id, "bob", null, now)?.Message);

            var open = Session("alice", now, max: 2);
            Assert.Null(SessionRules.Join(open, open.Id, "bob", new Dictionary<string, PropRecord> { ["n"] = new() { Value = "Bob" } }, now));
            Assert.Equal(new[] { "alice", "bob" }, open.Players.Select(p => p.Id));
            // Joining again only refreshes the entry.
            Assert.Null(SessionRules.Join(open, open.Id, "bob", null, now));
            Assert.Equal(2, open.Players.Count);
        }

        [Fact]
        public void Refresh_DropsSilentPlayers_AndMigratesAGoneHost()
        {
            long now = SessionRules.NowTicks;
            var rec = Session("alice", now - SessionRules.PlayerTimeout.Ticks * 2);
            rec.Players.Add(new PlayerRecord { Id = "bob", LastSeen = now });
            Assert.True(SessionRules.Refresh(rec, "bob", now));
            Assert.Equal("bob", rec.HostPlayerId);
            Assert.Equal(new[] { "bob" }, rec.Players.Select(p => p.Id));
            Assert.False(SessionRules.Refresh(rec, "carol", now));
        }

        [Fact]
        public void Leave_HandsTheSessionOn_AndReportsWhenItIsEmpty()
        {
            long now = SessionRules.NowTicks;
            var rec = Session("alice", now);
            rec.Players.Add(new PlayerRecord { Id = "bob", LastSeen = now });
            Assert.False(SessionRules.Leave(rec, "alice"));
            Assert.Equal("bob", rec.HostPlayerId);
            Assert.True(SessionRules.Leave(rec, "bob"));
        }

        [Fact]
        public void Listed_HonoursPrivacyLocksAndEveryFilter()
        {
            long now = SessionRules.NowTicks;
            var rec = SessionRules.NewSession(new SessionOptions
            {
                SessionProperties = new Dictionary<string, SessionProperty>
                {
                    ["gameMode"] = new("presence", VisibilityPropertyOptions.Public, PropertyIndex.String1),
                    ["maxPlayers"] = new("4", VisibilityPropertyOptions.Public, PropertyIndex.String2),
                },
            }, "alice", now);
            var presence = new FilterOption(FilterField.StringIndex1, "presence", FilterOperation.Equal);
            Assert.True(SessionRules.Listed(rec, new[] { presence }, now));
            Assert.False(SessionRules.Listed(rec, new[] { presence, new FilterOption(FilterField.StringIndex2, "4", FilterOperation.NotEqual) }, now));
            Assert.False(SessionRules.Listed(rec, new[] { new FilterOption(FilterField.StringIndex1, "match", FilterOperation.Equal) }, now));
            rec.IsPrivate = true;
            Assert.False(SessionRules.Listed(rec, null, now));
        }
    }
}
