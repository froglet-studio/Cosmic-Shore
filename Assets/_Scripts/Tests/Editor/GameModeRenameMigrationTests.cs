#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Core;
using CosmicShore.Data;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Guards the cloud-save rename map. The failure this exists to catch is silent by nature:
    /// a wrong entry does not throw, it just fails to match, and the player's unlocks and bests
    /// quietly read as zero.
    /// </summary>
    public class GameModeRenameMigrationTests
    {
        static IEnumerable<string> ModeNames() => Enum.GetNames(typeof(GameModes));

        // ── The map against the enum ────────────────────────────────────────────────────

        [Test]
        public void EveryTarget_IsARealGameModesMember()
        {
            var names = ModeNames().ToHashSet();
            foreach (var pair in GameModeRenameMigration.Map)
                Assert.Contains(pair.Value, names.ToList(),
                    $"'{pair.Key}' migrates to '{pair.Value}', which is not a GameModes member. " +
                    "A typo here silently drops that mode's progress instead of moving it.");
        }

        [Test]
        public void NoSource_IsStillAGameModesMember()
        {
            var names = ModeNames().ToHashSet();
            foreach (var pair in GameModeRenameMigration.Map)
                Assert.IsFalse(names.Contains(pair.Key),
                    $"'{pair.Key}' is still a live GameModes member but the map renames it away. " +
                    "Either the enum rename was reverted or the map entry is stale; both make the " +
                    "migration eat live data.");
        }

        [Test]
        public void Map_HasNoChains()
        {
            // A -> B where B -> C would leave the result depending on dictionary order.
            foreach (var pair in GameModeRenameMigration.Map)
                Assert.IsFalse(GameModeRenameMigration.Map.ContainsKey(pair.Value),
                    $"'{pair.Key}' -> '{pair.Value}', but '{pair.Value}' is itself renamed. " +
                    "Chained renames are order-dependent; collapse them to one hop.");
        }

        // ── Resolution ──────────────────────────────────────────────────────────────────

        [Test]
        public void Resolve_RenamesAKnownName_AndLeavesEverythingElse()
        {
            Assert.AreEqual("SkimRace", GameModeRenameMigration.Resolve("HexRace"));
            Assert.AreEqual("Scurry", GameModeRenameMigration.Resolve("MultiplayerCrystalCapture"));
            Assert.AreEqual("Rampage", GameModeRenameMigration.Resolve("Rampage"));
            Assert.AreEqual("NotAMode", GameModeRenameMigration.Resolve("NotAMode"));
            Assert.IsNull(GameModeRenameMigration.Resolve(null));
        }

        [Test]
        public void Resolve_IsIdempotent()
        {
            foreach (var pair in GameModeRenameMigration.Map)
                Assert.AreEqual(pair.Value, GameModeRenameMigration.Resolve(pair.Value),
                    "Running the migration twice must be a no-op.");
        }

        [Test]
        public void CompositeKey_RenamesOnlyTheModeHalf()
        {
            Assert.AreEqual("SkimRace:3", GameModeRenameMigration.ResolveCompositeKey("HexRace:3"));
            Assert.AreEqual("Rampage:1", GameModeRenameMigration.ResolveCompositeKey("Rampage:1"));
            // An intensity that happens to spell a mode name must not be touched.
            Assert.AreEqual("SkimRace:HexRace",
                GameModeRenameMigration.ResolveCompositeKey("HexRace:HexRace"));
        }

        // ── Appliers ────────────────────────────────────────────────────────────────────

        [Test]
        public void MigrateNames_MovesUnlocksAndKeepsTheRest()
        {
            var unlocked = new List<string> { "HexRace", "Rampage", "Ribcage" };
            GameModeRenameMigration.MigrateNames(unlocked);

            CollectionAssert.AreEquivalent(
                new[] { "SkimRace", "Rampage", "PeelTheCage" }, unlocked);
        }

        [Test]
        public void MigrateNames_DoesNotDuplicate_WhenBothNamesArePresent()
        {
            // A save half-migrated by an interrupted session.
            var unlocked = new List<string> { "HexRace", "SkimRace" };
            GameModeRenameMigration.MigrateNames(unlocked);

            Assert.AreEqual(1, unlocked.Count, "The list is a set - IsUnlocked is a Contains.");
            Assert.AreEqual("SkimRace", unlocked[0]);
        }

        [Test]
        public void MigrateKeys_ReKeysAndPrefersTheNewKeyOnCollision()
        {
            var stats = new Dictionary<string, int>
            {
                ["HexRace:1"] = 5,      // stale
                ["SkimRace:1"] = 9,     // already migrated - this is the live one
                ["Rampage:2"] = 3,
            };
            GameModeRenameMigration.MigrateKeys(stats, composite: true);

            Assert.AreEqual(2, stats.Count);
            Assert.AreEqual(9, stats["SkimRace:1"], "The new key is the one anything still writes to.");
            Assert.AreEqual(3, stats["Rampage:2"]);
            Assert.IsFalse(stats.ContainsKey("HexRace:1"));
        }

        [Test]
        public void ProgressionMigration_MovesEveryKeyedField()
        {
            var data = new GameModeProgressionData
            {
                UnlockedModes = new List<string> { "HexRace", "Tournament" },
                CompletedQuests = new List<string> { "Ribcage" },
                BestStats = new Dictionary<string, float> { ["NucleusRush"] = 12f },
                MaxUnlockedIntensity = new Dictionary<string, int> { ["MultiplayerJoust"] = 4 },
                IntensityPlayCounts = new Dictionary<string, int> { ["CellularDuel:2"] = 7 },
            };

            GameModeRenameMigration.Migrate(data);

            CollectionAssert.AreEquivalent(new[] { "SkimRace", "Maelstrom" }, data.UnlockedModes);
            CollectionAssert.AreEquivalent(new[] { "PeelTheCage" }, data.CompletedQuests);
            Assert.AreEqual(12f, data.BestStats["BroodRush"]);
            Assert.AreEqual(4, data.MaxUnlockedIntensity["Joust"]);
            Assert.AreEqual(7, data.IntensityPlayCounts["DuelForTheCell:2"]);
        }

        [Test]
        public void ModeStatsMigration_PreservesTheRecord()
        {
            var data = new ModeStatsCloudData();
            data.Modes["HexRace:3"] = new ModeRecord { GamesPlayed = 11, GamesWon = 4, BestScore = 42f };

            GameModeRenameMigration.Migrate(data);

            Assert.IsFalse(data.Modes.ContainsKey("HexRace:3"));
            var record = data.Modes["SkimRace:3"];
            Assert.AreEqual(11, record.GamesPlayed);
            Assert.AreEqual(4, record.GamesWon);
            Assert.AreEqual(42f, record.BestScore);
        }

        [Test]
        public void Migrate_OnAlreadyMigratedData_ChangesNothing()
        {
            var data = new GameModeProgressionData
            {
                UnlockedModes = new List<string> { "SkimRace", "Maelstrom", "Rampage" },
            };

            GameModeRenameMigration.Migrate(data);
            GameModeRenameMigration.Migrate(data);

            CollectionAssert.AreEquivalent(
                new[] { "SkimRace", "Maelstrom", "Rampage" }, data.UnlockedModes);
        }
    }
}
#endif
