#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Gameplay;
using NUnit.Framework;
using Unity.Services.Multiplayer;

namespace CosmicShore.Tests
{
    /// <summary>
    /// NetProtocol Tests - the version gate that keeps two builds which cannot play together out of each other's
    /// sessions and lobbies (Docs/LAUNCH/READINESS.md §10 blocker 6; Docs/PartySystem/BUGS.md B35).
    ///
    /// WHY THIS MATTERS:
    /// A wrong "compatible" puts an old and a new client in one lobby, and the join dies at the host with
    /// "NetworkConfig mismatch" - the generic bounce this gate exists to remove. A wrong "incompatible" splits players
    /// who could have played. Both directions are pinned, including the rule that a build which publishes NO version
    /// (every build from before this gate) is incompatible, and that the query a session is found by asks for exactly
    /// the values that session was created with.
    /// </summary>
    [TestFixture]
    public class NetProtocolTests
    {
        // ── The decision ─────────────────────────────────────────────────

        [Test]
        public void SameVersion_IsCompatible()
        {
            Assert.AreEqual(ProtocolCompatibility.Same, NetProtocol.Decide("24", "24"));
            Assert.IsTrue(NetProtocol.IsCompatible("24", "24"));
        }

        [Test]
        public void DifferentVersion_IsIncompatible()
        {
            // The negative control of the gate: flip the comparison in Decide and this fails.
            Assert.AreEqual(ProtocolCompatibility.Different, NetProtocol.Decide("24", "25"));
            Assert.AreEqual(ProtocolCompatibility.Different, NetProtocol.Decide("25", "24"));
            Assert.IsFalse(NetProtocol.IsCompatible("24", "25"));
        }

        [Test]
        public void VersionsCompareExactly_NotAsPrefixOrNumberText()
        {
            Assert.IsFalse(NetProtocol.IsCompatible("2", "24"));
            Assert.IsFalse(NetProtocol.IsCompatible("24", "024"));
        }

        [Test]
        public void UnpublishedRemote_OldBuild_IsIncompatible()
        {
            Assert.AreEqual(ProtocolCompatibility.Unpublished, NetProtocol.Decide("24", null));
            Assert.AreEqual(ProtocolCompatibility.Unpublished, NetProtocol.Decide("24", string.Empty));
            Assert.IsFalse(NetProtocol.IsCompatible("24", null));
        }

        [Test]
        public void UnknownLocal_MatchesNobody_EvenAnotherUnknown()
        {
            Assert.AreEqual(ProtocolCompatibility.LocalUnknown, NetProtocol.Decide(NetProtocol.UnknownLocalTag, NetProtocol.UnknownLocalTag));
            Assert.AreEqual(ProtocolCompatibility.LocalUnknown, NetProtocol.Decide(null, "24"));
            Assert.IsFalse(NetProtocol.IsCompatible(NetProtocol.UnknownLocalTag, "24"));
        }

        [Test]
        public void Format_IsInvariantDecimal()
        {
            Assert.AreEqual("24", NetProtocol.Format(24));
            Assert.AreEqual("65535", NetProtocol.Format(ushort.MaxValue));
        }

        // ── A joined session's members ───────────────────────────────────

        [Test]
        public void Members_AllSame_IsCompatible()
        {
            var verdict = NetProtocol.DecideAgainstMembers("24", new[] { "24", "24" }, out int at);
            Assert.AreEqual(ProtocolCompatibility.Same, verdict);
            Assert.AreEqual(-1, at);
        }

        [Test]
        public void Members_OneDifferent_IsIncompatible_AndNamesThatMember()
        {
            var verdict = NetProtocol.DecideAgainstMembers("24", new[] { "24", "23", "24" }, out int at);
            Assert.AreEqual(ProtocolCompatibility.Different, verdict);
            Assert.AreEqual(1, at);
        }

        [Test]
        public void Members_HostFromBeforeTheGate_IsIncompatible()
        {
            var verdict = NetProtocol.DecideAgainstMembers("24", new string[] { null }, out int at);
            Assert.AreEqual(ProtocolCompatibility.Unpublished, verdict);
            Assert.AreEqual(0, at);
        }

        [Test]
        public void Members_None_LeavesTheDecisionToTheJoin()
        {
            Assert.AreEqual(ProtocolCompatibility.NoPeers, NetProtocol.DecideAgainstMembers("24", new string[0], out _));
            Assert.AreEqual(ProtocolCompatibility.NoPeers, NetProtocol.DecideAgainstMembers("24", null, out _));
        }

        // ── Filters and properties ───────────────────────────────────────

        [Test]
        public void Scope_NamesTheLobbyByProtocol()
        {
            Assert.AreEqual("PRESENCE_LOBBY@p24", NetProtocol.Scope("PRESENCE_LOBBY", "24"));
            Assert.AreNotEqual(NetProtocol.Scope("PRESENCE_LOBBY", "24"), NetProtocol.Scope("PRESENCE_LOBBY", "25"));
            // An old build queries the bare game mode: a scoped value never equals it.
            Assert.AreNotEqual("PRESENCE_LOBBY", NetProtocol.Scope("PRESENCE_LOBBY", "24"));
        }

        [Test]
        public void MatchFilters_AskForScopedMode_SeatCount_AndProtocol()
        {
            var f = NetProtocol.MatchFilters("SkimRace", "4", "24");
            Assert.AreEqual(3, f.Count);
            AssertFilter(f, FilterField.StringIndex1, "SkimRace@p24");
            AssertFilter(f, FilterField.StringIndex2, "4");
            AssertFilter(f, NetProtocol.FilterIndex, "24");
            Assert.IsTrue(f.All(o => o.Operation == FilterOperation.Equal));
        }

        [Test]
        public void PresenceFilters_AskForScopedMode_AndProtocol()
        {
            var f = NetProtocol.PresenceFilters("PRESENCE_LOBBY", "24");
            Assert.AreEqual(2, f.Count);
            AssertFilter(f, FilterField.StringIndex1, "PRESENCE_LOBBY@p24");
            AssertFilter(f, NetProtocol.FilterIndex, "24");
        }

        [Test]
        public void MatchSession_IsFoundByItsOwnBuild_AndNotByAnother()
        {
            var props = NetProtocol.MatchSessionProperties("gameMode", "SkimRace", "maxPlayers", "4", "24");
            Assert.IsTrue(Matches(props, NetProtocol.MatchFilters("SkimRace", "4", "24")), "the same build finds its session");
            Assert.IsFalse(Matches(props, NetProtocol.MatchFilters("SkimRace", "4", "25")), "a newer build must not find it");
            Assert.IsFalse(Matches(props, OldBuildMatchFilters("SkimRace", "4")), "a build from before the gate must not find it");
            Assert.IsFalse(Matches(OldBuildMatchProperties("SkimRace", "4"), NetProtocol.MatchFilters("SkimRace", "4", "24")),
                "an old build's session must not be found");
        }

        [Test]
        public void PresenceLobby_IsFoundByItsOwnBuild_AndNotByAnother()
        {
            var props = NetProtocol.PresenceSessionProperties("gameMode", "PRESENCE_LOBBY", "24");
            Assert.IsTrue(Matches(props, NetProtocol.PresenceFilters("PRESENCE_LOBBY", "24")));
            Assert.IsFalse(Matches(props, NetProtocol.PresenceFilters("PRESENCE_LOBBY", "25")));
            // Before the gate the presence query asked only String1 == "PRESENCE_LOBBY".
            var old = new List<FilterOption> { new FilterOption(FilterField.StringIndex1, "PRESENCE_LOBBY", FilterOperation.Equal) };
            Assert.IsFalse(Matches(props, old), "an old build must not join the new lobby");
            var oldLobby = new Dictionary<string, SessionProperty>
            {
                { "gameMode", new SessionProperty("PRESENCE_LOBBY", VisibilityPropertyOptions.Public, PropertyIndex.String1) },
            };
            Assert.IsFalse(Matches(oldLobby, NetProtocol.PresenceFilters("PRESENCE_LOBBY", "24")), "a new build must not join the old lobby");
        }

        [Test]
        public void ProtocolProperty_IsIndexedInItsOwnSlot()
        {
            var p = NetProtocol.SessionProperty("24");
            Assert.AreEqual("24", p.Value);
            Assert.AreEqual(NetProtocol.SessionIndex, p.Index);
            Assert.AreNotEqual(PropertyIndex.String1, p.Index, "String1 is the game mode");
            Assert.AreNotEqual(PropertyIndex.String2, p.Index, "String2 is the seat count");
        }

        // ── Helpers: the service's Equal filter, applied to indexed properties ──

        static void AssertFilter(List<FilterOption> filters, FilterField field, string value)
        {
            var match = filters.Where(o => o.Field == field).ToList();
            Assert.AreEqual(1, match.Count, $"one filter on {field}");
            Assert.AreEqual(value, match[0].Value, $"{field} value");
        }

        static FilterField? FieldOf(PropertyIndex index) => index switch
        {
            PropertyIndex.String1 => FilterField.StringIndex1,
            PropertyIndex.String2 => FilterField.StringIndex2,
            PropertyIndex.String3 => FilterField.StringIndex3,
            PropertyIndex.String4 => FilterField.StringIndex4,
            PropertyIndex.String5 => FilterField.StringIndex5,
            _ => null, // not indexed: no filter can read it
        };

        /// <summary>Every Equal filter must name an indexed property with exactly that value (an absent index never matches).</summary>
        static bool Matches(Dictionary<string, SessionProperty> props, List<FilterOption> filters)
        {
            foreach (var f in filters)
            {
                bool hit = props.Values.Any(p => FieldOf(p.Index) == f.Field && p.Value == f.Value);
                if (!hit) return false;
            }
            return true;
        }

        static List<FilterOption> OldBuildMatchFilters(string mode, string seats) => new()
        {
            new FilterOption(FilterField.StringIndex1, mode, FilterOperation.Equal),
            new FilterOption(FilterField.StringIndex2, seats, FilterOperation.Equal),
        };

        static Dictionary<string, SessionProperty> OldBuildMatchProperties(string mode, string seats) => new()
        {
            { "gameMode",   new SessionProperty(mode,  VisibilityPropertyOptions.Public, PropertyIndex.String1) },
            { "maxPlayers", new SessionProperty(seats, VisibilityPropertyOptions.Public, PropertyIndex.String2) },
        };
    }
}
#endif
