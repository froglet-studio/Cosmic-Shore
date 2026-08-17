#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using CosmicShore.Gameplay;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Pins the party/presence WIRE FORMAT.
    ///
    /// <para>
    /// These strings are not internal names - they are the property keys two
    /// clients use to understand each other through UGS. A player on build N has
    /// to interoperate with a player on build N+1, so renaming the C# constant is
    /// free but changing its VALUE silently breaks every cross-version party: the
    /// reader looks up a key the writer no longer writes, finds nothing, and
    /// falls back to a default that looks like ordinary staleness. There is no
    /// error to notice.
    /// </para>
    ///
    /// <para>
    /// The failure mode is real and has shipped: <c>presenceState</c> existed in
    /// exactly one of the five files that declared keys, so a converge migration
    /// rebuilt the player record without it and peers rendered "CONNECTING…"
    /// forever (Docs/PresenceSystem/BUGS.md B11/B14). Consolidating the keys into
    /// <see cref="PartyLobbyKeys"/> makes that structurally impossible; this test
    /// makes changing one of them a deliberate act.
    /// </para>
    ///
    /// <para>
    /// If a value here genuinely must change, it is a breaking protocol change:
    /// update this test in the same commit and say so in the message.
    /// </para>
    /// </summary>
    [TestFixture]
    public class PartyLobbyKeysTests
    {
        static readonly Dictionary<string, string> ExpectedWireFormat = new()
        {
            { nameof(PartyLobbyKeys.DisplayName),           "displayName" },
            { nameof(PartyLobbyKeys.AvatarId),              "avatarId" },
            { nameof(PartyLobbyKeys.PartyCount),            "partyCount" },
            { nameof(PartyLobbyKeys.PartyMax),              "partyMax" },
            { nameof(PartyLobbyKeys.MatchName),             "matchName" },
            { nameof(PartyLobbyKeys.PresenceState),         "presenceState" },
            { nameof(PartyLobbyKeys.InvitePayloads),        "invite_payloads" },
            { nameof(PartyLobbyKeys.JoinedParty),           "joined_party" },
            { nameof(PartyLobbyKeys.AcceptedInvite),        "accepted_invite" },
            { nameof(PartyLobbyKeys.GameMode),              "gameMode" },
            { nameof(PartyLobbyKeys.PresenceLobbyGameMode), "PRESENCE_LOBBY" },
            { nameof(PartyLobbyKeys.PendingSessionId),      "PENDING" },
        };

        [Test]
        public void WireFormat_ValuesAreFrozen()
        {
            foreach (var kv in ExpectedWireFormat)
            {
                var field = typeof(PartyLobbyKeys).GetField(
                    kv.Key, BindingFlags.Public | BindingFlags.Static);

                Assert.IsNotNull(field, $"PartyLobbyKeys.{kv.Key} should exist.");
                Assert.AreEqual(kv.Value, field.GetValue(null),
                    $"PartyLobbyKeys.{kv.Key} is a wire value shared with other builds. " +
                    "Changing it breaks cross-version parties silently - the reader just " +
                    "sees a missing key and falls back to a default.");
            }
        }

        /// <summary>
        /// Catches a key added to the class without being added to the frozen
        /// table above - i.e. a new wire value that nobody deliberately froze.
        /// </summary>
        [Test]
        public void WireFormat_TableCoversEveryKey()
        {
            var declared = typeof(PartyLobbyKeys)
                .GetFields(BindingFlags.Public | BindingFlags.Static);

            foreach (var f in declared)
            {
                Assert.IsTrue(ExpectedWireFormat.ContainsKey(f.Name),
                    $"PartyLobbyKeys.{f.Name} is a new wire value with no entry in this " +
                    "test's frozen table. Add it - the table is what makes an accidental " +
                    "value change loud.");
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // The forwarding constants the other services declare must not drift.
        // They exist so ~100 call sites stayed untouched during consolidation,
        // and (for the HostConnectionService pair) because PartyInviteSystemTests
        // reflects on them by name.
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void HostConnectionService_ForwardingConstants_MatchTheOwner()
        {
            AssertForwards(typeof(HostConnectionService), "DISPLAY_NAME_KEY",    PartyLobbyKeys.DisplayName);
            AssertForwards(typeof(HostConnectionService), "AVATAR_ID_KEY",       PartyLobbyKeys.AvatarId);
            AssertForwards(typeof(HostConnectionService), "INVITE_PAYLOADS_KEY", PartyLobbyKeys.InvitePayloads);
            AssertForwards(typeof(HostConnectionService), "PRESENCE_STATE_KEY",  PartyLobbyKeys.PresenceState);
            AssertForwards(typeof(HostConnectionService), "JOINED_PARTY_KEY",    PartyLobbyKeys.JoinedParty);
        }

        [Test]
        public void PartyMemberService_ForwardingConstants_MatchTheOwner()
        {
            AssertForwards(typeof(PartyMemberService), "DISPLAY_NAME_KEY", PartyLobbyKeys.DisplayName);
            AssertForwards(typeof(PartyMemberService), "AVATAR_ID_KEY",    PartyLobbyKeys.AvatarId);
        }

        [Test]
        public void AcceptanceSignalService_ForwardingConstants_MatchTheOwner()
        {
            AssertForwards(typeof(AcceptanceSignalService), "ACCEPTED_INVITE_KEY", PartyLobbyKeys.AcceptedInvite);
            AssertForwards(typeof(AcceptanceSignalService), "INVITE_PAYLOADS_KEY", PartyLobbyKeys.InvitePayloads);
        }

        static void AssertForwards(System.Type type, string constName, string expected)
        {
            var field = type.GetField(constName,
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

            Assert.IsNotNull(field, $"{type.Name}.{constName} should exist.");
            Assert.AreEqual(expected, field.GetValue(null),
                $"{type.Name}.{constName} must forward to PartyLobbyKeys, not carry its own literal.");
        }
    }
}
#endif
