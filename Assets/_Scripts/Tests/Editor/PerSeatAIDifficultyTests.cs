#if UNITY_EDITOR
using CosmicShore.Data;
using CosmicShore.UI;
using CosmicShore.Utility;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Each AI seat carries its own difficulty (Shombith, 2026-10-27: "AI pilot 1 can be Easy while AI pilot 2 is
    /// Hard, in the same Regatta match"). A seat with no level of its own flies the card's; the levels ride beside
    /// the placements, entry i for bot i, through the card config and the launch data, and a kick takes its seat's
    /// level with it so the bots after it keep theirs.
    /// </summary>
    public class PerSeatAIDifficultyTests
    {
        ArcadeGameConfigSO _config;
        GameDataSO _gameData;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<ArcadeGameConfigSO>();
            _gameData = ScriptableObject.CreateInstance<GameDataSO>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_config);
            Object.DestroyImmediate(_gameData);
        }

        [Test]
        public void ForSeat_ASeatsOwnLevelWins_OtherwiseTheCards()
        {
            Assert.AreEqual(AIDifficulty.Easy, AIDifficultyRules.ForSeat(AIDifficulty.Easy, AIDifficulty.Hard));
            Assert.AreEqual(AIDifficulty.Hard, AIDifficultyRules.ForSeat(0, AIDifficulty.Hard), "no level of its own");
            Assert.AreEqual(AIDifficultyRules.Default, AIDifficultyRules.ForSeat(0, 0), "neither set");
            Assert.AreEqual(AIDifficulty.Hard, AIDifficultyRules.ForSeat((AIDifficulty)9, AIDifficulty.Hard),
                "a number a later build added is not a level");
            Assert.IsFalse(AIDifficultyRules.IsSet(0));
            Assert.IsTrue(AIDifficultyRules.IsSet(AIDifficulty.Legendary));
        }

        [Test]
        public void TwoAIs_TwoLevels_InOneMatch()
        {
            _config.AIDifficulty = AIDifficulty.Medium;
            _config.AIDomains.AddRange(new[] { Domains.Jade, Domains.Ruby, Domains.Gold });
            _config.SyncAIVesselSlots();
            _config.AIDifficulties[0] = AIDifficulty.Easy;
            _config.AIDifficulties[1] = AIDifficulty.Hard;

            Assert.AreEqual(AIDifficulty.Easy, _config.AIDifficultyAt(0));
            Assert.AreEqual(AIDifficulty.Hard, _config.AIDifficultyAt(1));
            Assert.AreEqual(AIDifficulty.Medium, _config.AIDifficultyAt(2), "a seat nobody set flies the card's");
            Assert.IsTrue(_config.AnySeatDifficulty());
        }

        [Test]
        public void SyncKeepsOneLevelPerPlacement()
        {
            _config.AIDomains.AddRange(new[] { Domains.Jade, Domains.Ruby });
            _config.SyncAIVesselSlots();
            Assert.AreEqual(2, _config.AIDifficulties.Count);
            Assert.AreEqual(2, _config.AIVessels.Count);

            _config.AIDomains.RemoveAt(1);
            _config.SyncAIVesselSlots();
            Assert.AreEqual(1, _config.AIDifficulties.Count);
        }

        [Test]
        public void AKickTakesItsSeatsLevel_TheBotsAfterItKeepTheirs()
        {
            _config.AIDomains.AddRange(new[] { Domains.Jade, Domains.Ruby, Domains.Gold });
            _config.SyncAIVesselSlots();
            _config.AIDifficulties[0] = AIDifficulty.Easy;
            _config.AIDifficulties[1] = AIDifficulty.Hard;
            _config.AIDifficulties[2] = AIDifficulty.Legendary;

            _config.RemoveAISeat(1);

            CollectionAssert.AreEqual(new[] { Domains.Jade, Domains.Gold }, _config.AIDomains);
            CollectionAssert.AreEqual(new[] { AIDifficulty.Easy, AIDifficulty.Legendary }, _config.AIDifficulties);
            Assert.AreEqual(2, _config.AIVessels.Count);
        }

        [Test]
        public void LaunchData_SeatLevelsReachTheSpawner()
        {
            _gameData.RequestedAIDifficulty = AIDifficulty.Medium;
            _gameData.SetRequestedAIDifficulties(new AIDifficulty[] { AIDifficulty.Easy, 0, AIDifficulty.Hard });

            Assert.AreEqual(AIDifficulty.Easy, _gameData.RequestedAIDifficultyAt(0));
            Assert.AreEqual(AIDifficulty.Medium, _gameData.RequestedAIDifficultyAt(1), "0 = the match's level");
            Assert.AreEqual(AIDifficulty.Hard, _gameData.RequestedAIDifficultyAt(2));
            Assert.AreEqual(AIDifficulty.Medium, _gameData.RequestedAIDifficultyAt(3), "a backfill seat past the list");
            Assert.AreEqual(AIDifficulty.Medium, _gameData.AIDifficultyFor(null), "an unstamped pilot flies the match's");

            _gameData.SetRequestedAIDifficulties(null);
            Assert.AreEqual(AIDifficulty.Medium, _gameData.RequestedAIDifficultyAt(0));
        }
    }
}
#endif
