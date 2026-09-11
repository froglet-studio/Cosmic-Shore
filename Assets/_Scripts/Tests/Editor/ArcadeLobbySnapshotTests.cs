using System.Reflection;
using CosmicShore.Gameplay;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// <see cref="ArcadeConfigSyncManager.LobbySnapshot"/> is the whole open arcade lobby as one
    /// replicated value, and it is the shape CLAUDE.md warns about under "a DTO is a second place
    /// every field has to be added": a field added to the struct and forgotten in
    /// <c>NetworkSerialize</c> never crosses the wire, and one forgotten in <c>Equals</c> never
    /// triggers a write at all - because <c>NotifyRosterChanged</c> and
    /// <c>RepublishHumanCount</c> both compare before assigning, and a NetworkVariable itself
    /// only dirties on a value change. Either omission fails SILENTLY and only on a second
    /// machine, which is the whole reason this is asserted by reflection over the real fields
    /// rather than by a list somebody has to remember to extend.
    ///
    /// <para><c>HumanCount</c> is the field that prompted it: guests were drawing AI avatars for
    /// humans they could not count.</para>
    ///
    /// <para><b>What this cannot prove:</b> that a field was added to <c>NetworkSerialize</c>.
    /// Driving that needs a Netcode <c>BufferSerializer</c>, which needs a live network context -
    /// so the serializer stays a read-it-yourself step when a field is added here. The equality
    /// half is the one worth automating anyway: a field missing from <c>Equals</c> means the
    /// write never happens at all, so the serializer never gets the chance to be wrong.</para>
    /// </summary>
    public class ArcadeLobbySnapshotTests
    {
        static FieldInfo[] SnapshotFields =>
            typeof(ArcadeConfigSyncManager.LobbySnapshot)
                .GetFields(BindingFlags.Public | BindingFlags.Instance);

        /// <summary>Bump one field at a time; Equals has to notice every single one.</summary>
        [Test]
        public void EveryFieldParticipatesInEquality()
        {
            foreach (var field in SnapshotFields)
            {
                object mutated = new ArcadeConfigSyncManager.LobbySnapshot();
                if (field.FieldType == typeof(int)) field.SetValue(mutated, 7);
                else if (field.FieldType == typeof(bool)) field.SetValue(mutated, true);
                else Assert.Fail($"LobbySnapshot.{field.Name} is a {field.FieldType.Name}; teach " +
                                 "this test how to mutate it (and check NetworkSerialize handles it).");

                var baseline = new ArcadeConfigSyncManager.LobbySnapshot();
                Assert.IsFalse(baseline.Equals((ArcadeConfigSyncManager.LobbySnapshot)mutated),
                    $"LobbySnapshot.Equals ignores '{field.Name}'. A change to it would never dirty " +
                    "the NetworkVariable, so no guest would ever see it.");
            }
        }

        /// <summary>
        /// A guest reads its human head-count off the lobby (see
        /// <c>ArcadeGameConfigureModal.CurrentPartyHumanCount</c>), so an open lobby that carries
        /// zero humans would send it back to the stale presence list this replaces.
        /// </summary>
        [Test]
        public void HumanCountSurvivesARosterEdit()
        {
            var lobby = new ArcadeConfigSyncManager.LobbySnapshot
            {
                IsOpen = true, PlayerCount = 4, DomainCount = 3, HumanCount = 3,
            };

            lobby.SetPlacedAiDomains(new[] { 1, 2 });

            Assert.AreEqual(3, lobby.HumanCount,
                "SetPlacedAiDomains must not disturb the human count - NotifyRosterChanged edits " +
                "a copy of the live snapshot precisely so the host's count is carried through.");
            Assert.AreEqual(2, lobby.AiCount);
            CollectionAssert.AreEqual(new[] { 1, 2 }, lobby.PlacedAiDomains());
        }

        /// <summary>Placements past the four replicated slots truncate rather than corrupt.</summary>
        [Test]
        public void PlacedAiTruncatesToTheReplicatedSlots()
        {
            var lobby = new ArcadeConfigSyncManager.LobbySnapshot();
            lobby.SetPlacedAiDomains(new[] { 1, 2, 3, 1, 2 });
            Assert.AreEqual(ArcadeConfigSyncManager.LobbySnapshot.MaxAiSlots, lobby.AiCount);
            Assert.AreEqual(4, lobby.PlacedAiDomains().Length);
        }
    }
}
