using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using CosmicShore.Data;
using CosmicShore.Utility;
using CosmicShore.Gameplay;

namespace CosmicShore.Tests
{
    /// <summary>
    /// GameDataSO Tests — Validates the central game state container.
    ///
    /// WHY THIS MATTERS:
    /// GameDataSO is the single most important runtime data object. It connects
    /// MiniGame controllers, GameManager, StatsManager, multiplayer setup, and UI.
    /// It manages player lists, round stats, domain stats, sorting, spawn positions,
    /// and winner calculation. Bugs here cascade to every game mode.
    /// </summary>
    [TestFixture]
    public class GameDataSOTests
    {
        /// <summary>
        /// Minimal IRoundStats implementation for testing without Netcode dependencies.
        /// </summary>
        class MockRoundStats : IRoundStats
        {
            public event Action<IRoundStats> OnAnyStatChanged;
            public event Action OnScoreChanged;
            public event Action<IRoundStats> OnBlocksCreatedChanged;
            public event Action<IRoundStats> OnBlocksDestroyedChanged;
            public event Action<IRoundStats> OnBlocksRestoredChanged;
            public event Action<IRoundStats> OnPrismsStolenChanged;
            public event Action<IRoundStats> OnPrismsRemainingChanged;
            public event Action<IRoundStats> OnFriendlyPrismsDestroyedChanged;
            public event Action<IRoundStats> OnHostilePrismsDestroyedChanged;
            public event Action<IRoundStats> OnVolumeCreatedChanged;
            public event Action<IRoundStats> OnTotalVolumeDestroyedChanged;
            public event Action<IRoundStats> OnFriendlyVolumeDestroyedChanged;
            public event Action<IRoundStats> OnHostileVolumeDestroyedChanged;
            public event Action<IRoundStats> OnVolumeRestoredChanged;
            public event Action<IRoundStats> OnVolumeStolenChanged;
            public event Action<IRoundStats> OnVolumeRemainingChanged;
            public event Action<IRoundStats> OnCrystalsCollectedChanged;
            public event Action<IRoundStats> OnOmniCrystalsCollectedChanged;
            public event Action<IRoundStats> OnElementalCrystalsCollectedChanged;
            public event Action<IRoundStats> OnChargeCrystalValueChanged;
            public event Action<IRoundStats> OnMassCrystalValueChanged;
            public event Action<IRoundStats> OnSpaceCrystalValueChanged;
            public event Action<IRoundStats> OnTimeCrystalValueChanged;
            public event Action<IRoundStats> OnSkimmerShipCollisionsChanged;
            public event Action<IRoundStats> OnJoustCollisionChanged;
            public event Action<IRoundStats> OnFullSpeedStraightAbilityActiveTimeChanged;
            public event Action<IRoundStats> OnRightStickAbilityActiveTimeChanged;
            public event Action<IRoundStats> OnLeftStickAbilityActiveTimeChanged;
            public event Action<IRoundStats> OnFlipAbilityActiveTimeChanged;
            public event Action<IRoundStats> OnButton1AbilityActiveTimeChanged;
            public event Action<IRoundStats> OnButton2AbilityActiveTimeChanged;
            public event Action<IRoundStats> OnButton3AbilityActiveTimeChanged;

            public string Name { get; set; }
            public Domains Domain { get; set; }
            public float Score { get; set; }
            public int BlocksCreated { get; set; }
            public int BlocksDestroyed { get; set; }
            public int BlocksRestored { get; set; }
            public int PrismStolen { get; set; }
            public int PrismsRemaining { get; set; }
            public int FriendlyPrismsDestroyed { get; set; }
            public int HostilePrismsDestroyed { get; set; }
            public float VolumeCreated { get; set; }
            public float TotalVolumeDestroyed { get; set; }
            public float VolumeRestored { get; set; }
            public float VolumeStolen { get; set; }
            public float VolumeRemaining { get; set; }
            public float FriendlyVolumeDestroyed { get; set; }
            public float HostileVolumeDestroyed { get; set; }
            public int CrystalsCollected { get; set; }
            public int OmniCrystalsCollected { get; set; }
            public int ElementalCrystalsCollected { get; set; }
            public float ChargeCrystalValue { get; set; }
            public float MassCrystalValue { get; set; }
            public float SpaceCrystalValue { get; set; }
            public float TimeCrystalValue { get; set; }
            public int SkimmerShipCollisions { get; set; }
            public int JoustCollisions { get; set; }
            public float FullSpeedStraightAbilityActiveTime { get; set; }
            public float RightStickAbilityActiveTime { get; set; }
            public float LeftStickAbilityActiveTime { get; set; }
            public float FlipAbilityActiveTime { get; set; }
            public float Button1AbilityActiveTime { get; set; }
            public float Button2AbilityActiveTime { get; set; }
            public float Button3AbilityActiveTime { get; set; }
        }

        GameDataSO _gameData;

        [SetUp]
        public void SetUp()
        {
            _gameData = ScriptableObject.CreateInstance<GameDataSO>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_gameData);
        }

        #region ResetRuntimeData

        [Test]
        public void ResetRuntimeData_ClearsAllLists()
        {
            _gameData.Players.Add(null); // Just to make lists non-empty
            _gameData.RoundStatsList.Add(new MockRoundStats());
            _gameData.DomainStatsList.Add(new DomainStats { Domain = Domains.Jade, Score = 10f });

            _gameData.ResetRuntimeData();

            Assert.AreEqual(0, _gameData.Players.Count, "Players should be cleared.");
            Assert.AreEqual(0, _gameData.Vessels.Count, "Vessels should be cleared.");
            Assert.AreEqual(0, _gameData.RoundStatsList.Count, "RoundStatsList should be cleared.");
            Assert.AreEqual(0, _gameData.DomainStatsList.Count, "DomainStatsList should be cleared.");
            Assert.AreEqual(0, _gameData.SlowedShipTransforms.Count, "SlowedShipTransforms should be cleared.");
        }

        [Test]
        public void ResetRuntimeData_ResetsCounters()
        {
            _gameData.RoundsPlayed = 5;
            _gameData.TurnsTakenThisRound = 3;
            _gameData.RequestedAIBackfillCount = 2;

            _gameData.ResetRuntimeData();

            Assert.AreEqual(0, _gameData.RoundsPlayed);
            Assert.AreEqual(0, _gameData.TurnsTakenThisRound);
            Assert.AreEqual(0, _gameData.RequestedAIBackfillCount);
        }

        [Test]
        public void ResetRuntimeData_ClearsLocalPlayerReference()
        {
            _gameData.ResetRuntimeData();

            Assert.IsNull(_gameData.LocalPlayer);
            Assert.IsNull(_gameData.LocalRoundStats);
        }

        [Test]
        public void ResetRuntimeData_SetsTurnRunningToFalse()
        {
            _gameData.ResetRuntimeData();

            Assert.IsFalse(_gameData.IsTurnRunning);
        }

        #endregion

        #region SortRoundStats

        [Test]
        public void SortRoundStats_HighScoreFirst_SortsDescending()
        {
            _gameData.RoundStatsList.Add(new MockRoundStats { Name = "Low", Score = 10f });
            _gameData.RoundStatsList.Add(new MockRoundStats { Name = "High", Score = 100f });
            _gameData.RoundStatsList.Add(new MockRoundStats { Name = "Mid", Score = 50f });

            _gameData.SortRoundStats(golfRules: false);

            Assert.AreEqual("High", _gameData.RoundStatsList[0].Name);
            Assert.AreEqual("Mid", _gameData.RoundStatsList[1].Name);
            Assert.AreEqual("Low", _gameData.RoundStatsList[2].Name);
        }

        [Test]
        public void SortRoundStats_GolfRules_SortsAscending()
        {
            _gameData.RoundStatsList.Add(new MockRoundStats { Name = "High", Score = 100f });
            _gameData.RoundStatsList.Add(new MockRoundStats { Name = "Low", Score = 10f });
            _gameData.RoundStatsList.Add(new MockRoundStats { Name = "Mid", Score = 50f });

            _gameData.SortRoundStats(golfRules: true);

            Assert.AreEqual("Low", _gameData.RoundStatsList[0].Name);
            Assert.AreEqual("Mid", _gameData.RoundStatsList[1].Name);
            Assert.AreEqual("High", _gameData.RoundStatsList[2].Name);
        }

        [Test]
        public void SortRoundStats_EmptyList_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _gameData.SortRoundStats(golfRules: false));
        }

        [Test]
        public void SortRoundStats_SingleItem_RemainsUnchanged()
        {
            _gameData.RoundStatsList.Add(new MockRoundStats { Name = "Solo", Score = 42f });

            _gameData.SortRoundStats(golfRules: false);

            Assert.AreEqual("Solo", _gameData.RoundStatsList[0].Name);
            Assert.AreEqual(42f, _gameData.RoundStatsList[0].Score);
        }

        [Test]
        public void SortRoundStats_TiedScores_DoesNotThrow()
        {
            _gameData.RoundStatsList.Add(new MockRoundStats { Name = "A", Score = 50f });
            _gameData.RoundStatsList.Add(new MockRoundStats { Name = "B", Score = 50f });

            Assert.DoesNotThrow(() => _gameData.SortRoundStats(golfRules: false));
            Assert.AreEqual(2, _gameData.RoundStatsList.Count);
        }

        #endregion

        #region CalculateDomainStats

        [Test]
        public void CalculateDomainStats_AggregatesScoresByDomain()
        {
            _gameData.RoundStatsList.Add(new MockRoundStats { Name = "P1", Domain = Domains.Jade, Score = 30f });
            _gameData.RoundStatsList.Add(new MockRoundStats { Name = "P2", Domain = Domains.Jade, Score = 20f });
            _gameData.RoundStatsList.Add(new MockRoundStats { Name = "P3", Domain = Domains.Ruby, Score = 100f });

            _gameData.CalculateDomainStats(golfRules: false);

            Assert.AreEqual(2, _gameData.DomainStatsList.Count,
                "Should have 2 domains (Jade and Ruby).");

            // With golfRules: false, highest score is first.
            Assert.AreEqual(Domains.Ruby, _gameData.DomainStatsList[0].Domain);
            Assert.AreEqual(100f, _gameData.DomainStatsList[0].Score);
            Assert.AreEqual(Domains.Jade, _gameData.DomainStatsList[1].Domain);
            Assert.AreEqual(50f, _gameData.DomainStatsList[1].Score);
        }

        [Test]
        public void CalculateDomainStats_GolfRules_LowestScoreFirst()
        {
            _gameData.RoundStatsList.Add(new MockRoundStats { Name = "P1", Domain = Domains.Jade, Score = 5f });
            _gameData.RoundStatsList.Add(new MockRoundStats { Name = "P2", Domain = Domains.Ruby, Score = 100f });

            _gameData.CalculateDomainStats(golfRules: true);

            Assert.AreEqual(Domains.Jade, _gameData.DomainStatsList[0].Domain,
                "With golf rules, lowest score domain should be first.");
        }

        [Test]
        public void CalculateDomainStats_EmptyRoundStats_ProducesEmptyDomainStats()
        {
            _gameData.CalculateDomainStats(golfRules: false);

            Assert.AreEqual(0, _gameData.DomainStatsList.Count);
        }

        [Test]
        public void CalculateDomainStats_ClearsPreviousResults()
        {
            _gameData.DomainStatsList.Add(new DomainStats { Domain = Domains.Gold, Score = 999f });

            _gameData.RoundStatsList.Add(new MockRoundStats { Name = "P1", Domain = Domains.Jade, Score = 10f });
            _gameData.CalculateDomainStats(golfRules: false);

            Assert.AreEqual(1, _gameData.DomainStatsList.Count,
                "Previous domain stats should be cleared before recalculating.");
            Assert.AreEqual(Domains.Jade, _gameData.DomainStatsList[0].Domain);
        }

        [Test]
        public void CalculateDomainStats_MultiplePlayers_SameDomain_SumsCorrectly()
        {
            _gameData.RoundStatsList.Add(new MockRoundStats { Name = "P1", Domain = Domains.Gold, Score = 10f });
            _gameData.RoundStatsList.Add(new MockRoundStats { Name = "P2", Domain = Domains.Gold, Score = 15f });
            _gameData.RoundStatsList.Add(new MockRoundStats { Name = "P3", Domain = Domains.Gold, Score = 25f });

            _gameData.CalculateDomainStats(golfRules: false);

            Assert.AreEqual(1, _gameData.DomainStatsList.Count);
            Assert.AreEqual(50f, _gameData.DomainStatsList[0].Score, 0.001f);
        }

        #endregion

        #region SortDomainStats

        [Test]
        public void SortDomainStats_HighScoreFirst_SortsDescending()
        {
            _gameData.DomainStatsList.Add(new DomainStats { Domain = Domains.Jade, Score = 10f });
            _gameData.DomainStatsList.Add(new DomainStats { Domain = Domains.Ruby, Score = 50f });

            _gameData.SortDomainStats(golfRules: false);

            Assert.AreEqual(Domains.Ruby, _gameData.DomainStatsList[0].Domain);
        }

        [Test]
        public void SortDomainStats_GolfRules_SortsAscending()
        {
            _gameData.DomainStatsList.Add(new DomainStats { Domain = Domains.Ruby, Score = 50f });
            _gameData.DomainStatsList.Add(new DomainStats { Domain = Domains.Jade, Score = 10f });

            _gameData.SortDomainStats(golfRules: true);

            Assert.AreEqual(Domains.Jade, _gameData.DomainStatsList[0].Domain);
        }

        #endregion

        #region GetControllingTeamStatsBasedOnVolumeRemaining

        [Test]
        public void GetControllingTeam_ReturnsTeamWithHighestVolume()
        {
            _gameData.RoundStatsList.Add(new MockRoundStats { Domain = Domains.Jade, VolumeRemaining = 100f });
            _gameData.RoundStatsList.Add(new MockRoundStats { Domain = Domains.Ruby, VolumeRemaining = 200f });

            var (team, volume) = _gameData.GetControllingTeamStatsBasedOnVolumeRemaining();

            Assert.AreEqual(Domains.Ruby, team);
            Assert.AreEqual(200f, volume);
        }

        [Test]
        public void GetControllingTeam_EmptyList_ReturnsJadeWithZero()
        {
            var (team, volume) = _gameData.GetControllingTeamStatsBasedOnVolumeRemaining();

            Assert.AreEqual(Domains.Jade, team, "Default team should be Jade.");
            Assert.AreEqual(0f, volume, "Default volume should be 0.");
        }

        #endregion

        #region GetTotalVolume

        [Test]
        public void GetTotalVolume_SumsAllPlayerVolumes()
        {
            _gameData.RoundStatsList.Add(new MockRoundStats { VolumeRemaining = 100f });
            _gameData.RoundStatsList.Add(new MockRoundStats { VolumeRemaining = 50f });
            _gameData.RoundStatsList.Add(new MockRoundStats { VolumeRemaining = 25f });

            float total = _gameData.GetTotalVolume();

            Assert.AreEqual(175f, total, 0.001f);
        }

        [Test]
        public void GetTotalVolume_EmptyList_ReturnsZero()
        {
            Assert.AreEqual(0f, _gameData.GetTotalVolume());
        }

        #endregion

        #region GetTeamVolumes

        [Test]
        public void GetTeamVolumes_ReturnsVolumePerTeam()
        {
            _gameData.RoundStatsList.Add(new MockRoundStats { Domain = Domains.Jade, VolumeRemaining = 10f });
            _gameData.RoundStatsList.Add(new MockRoundStats { Domain = Domains.Ruby, VolumeRemaining = 20f });
            _gameData.RoundStatsList.Add(new MockRoundStats { Domain = Domains.Blue, VolumeRemaining = 30f });
            _gameData.RoundStatsList.Add(new MockRoundStats { Domain = Domains.Gold, VolumeRemaining = 40f });

            Vector4 volumes = _gameData.GetTeamVolumes();

            Assert.AreEqual(10f, volumes.x, 0.001f, "Jade volume (x)");
            Assert.AreEqual(20f, volumes.y, 0.001f, "Ruby volume (y)");
            Assert.AreEqual(30f, volumes.z, 0.001f, "Blue volume (z)");
            Assert.AreEqual(40f, volumes.w, 0.001f, "Gold volume (w)");
        }

        [Test]
        public void GetTeamVolumes_MissingTeams_ReturnsZeroForThem()
        {
            _gameData.RoundStatsList.Add(new MockRoundStats { Domain = Domains.Jade, VolumeRemaining = 10f });

            Vector4 volumes = _gameData.GetTeamVolumes();

            Assert.AreEqual(10f, volumes.x, 0.001f, "Jade should have volume.");
            Assert.AreEqual(0f, volumes.y, 0.001f, "Ruby should be 0 (no player).");
            Assert.AreEqual(0f, volumes.z, 0.001f, "Blue should be 0 (no player).");
            Assert.AreEqual(0f, volumes.w, 0.001f, "Gold should be 0 (no player).");
        }

        #endregion

        #region RemovePlayerData

        [Test]
        public void RemovePlayerData_NullName_ReturnsFalse()
        {
            Assert.IsFalse(_gameData.RemovePlayerData(null));
        }

        [Test]
        public void RemovePlayerData_EmptyName_ReturnsFalse()
        {
            Assert.IsFalse(_gameData.RemovePlayerData(string.Empty));
        }

        [Test]
        public void RemovePlayerData_NonExistentPlayer_ReturnsFalse()
        {
            Assert.IsFalse(_gameData.RemovePlayerData("Ghost"));
        }

        #endregion

        #region TryGetRoundStats

        [Test]
        public void TryGetRoundStats_ExistingPlayer_ReturnsTrueAndStats()
        {
            _gameData.RoundStatsList.Add(new MockRoundStats { Name = "Player1", Score = 42f });

            bool found = _gameData.TryGetRoundStats("Player1", out var stats);

            Assert.IsTrue(found);
            Assert.AreEqual(42f, stats.Score);
        }

        [Test]
        public void TryGetRoundStats_NonExistentPlayer_ReturnsFalse()
        {
            bool found = _gameData.TryGetRoundStats("Nobody", out var stats);

            Assert.IsFalse(found);
            Assert.IsNull(stats);
        }

        #endregion

        #region TryGetWinner

        [Test]
        public void TryGetWinner_EmptyRoundStats_ReturnsFalse()
        {
            bool result = _gameData.TryGetWinner(out _, out _);

            Assert.IsFalse(result);
        }

        #endregion

        #region GetSortedListInDecendingOrderBasedOnVolumeRemaining

        [Test]
        public void GetSortedListDescendingByVolume_ReturnsSortedCopy()
        {
            _gameData.RoundStatsList.Add(new MockRoundStats { Name = "A", VolumeRemaining = 10f });
            _gameData.RoundStatsList.Add(new MockRoundStats { Name = "B", VolumeRemaining = 50f });
            _gameData.RoundStatsList.Add(new MockRoundStats { Name = "C", VolumeRemaining = 30f });

            var sorted = _gameData.GetSortedListInDecendingOrderBasedOnVolumeRemaining();

            Assert.AreEqual("B", sorted[0].Name, "Highest volume first.");
            Assert.AreEqual("C", sorted[1].Name);
            Assert.AreEqual("A", sorted[2].Name, "Lowest volume last.");
        }

        [Test]
        public void GetSortedListDescendingByVolume_DoesNotModifyOriginal()
        {
            _gameData.RoundStatsList.Add(new MockRoundStats { Name = "A", VolumeRemaining = 10f });
            _gameData.RoundStatsList.Add(new MockRoundStats { Name = "B", VolumeRemaining = 50f });

            _gameData.GetSortedListInDecendingOrderBasedOnVolumeRemaining();

            // Original list should remain in insertion order.
            Assert.AreEqual("A", _gameData.RoundStatsList[0].Name);
        }

        #endregion

        #region StartTurn / InvokeGameTurnConditionsMet

        [Test]
        public void StartTurn_SetsTurnRunningToTrue()
        {
            _gameData.StartTurn();

            Assert.IsTrue(_gameData.IsTurnRunning);
        }

        [Test]
        public void InvokeGameTurnConditionsMet_SetsTurnRunningToFalse()
        {
            _gameData.StartTurn();
            _gameData.InvokeGameTurnConditionsMet();

            Assert.IsFalse(_gameData.IsTurnRunning);
        }

        #endregion

        #region DomainStats Struct

        [Test]
        public void DomainStats_DefaultValues()
        {
            var stats = new DomainStats();

            Assert.AreEqual(default(Domains), stats.Domain);
            Assert.AreEqual(0f, stats.Score);
        }

        [Test]
        public void DomainStats_CanBeAssigned()
        {
            var stats = new DomainStats
            {
                Domain = Domains.Gold,
                Score = 123.45f
            };

            Assert.AreEqual(Domains.Gold, stats.Domain);
            Assert.AreEqual(123.45f, stats.Score, 0.001f);
        }

        #endregion

        #region SetSpawnPositions

        [Test]
        public void SetSpawnPositions_NullArray_DoesNotThrow()
        {
            // SetSpawnPositions with null should log error but not crash.
            Assert.DoesNotThrow(() => _gameData.SetSpawnPositions(null));
        }

        [Test]
        public void SetSpawnPositions_ValidArray_SetsSpawnPoses()
        {
            var go1 = new GameObject("Spawn1");
            var go2 = new GameObject("Spawn2");
            go1.transform.position = new Vector3(1, 2, 3);
            go2.transform.position = new Vector3(4, 5, 6);

            _gameData.SetSpawnPositions(new[] { go1.transform, go2.transform });

            Assert.IsNotNull(_gameData.SpawnPoses);
            Assert.AreEqual(2, _gameData.SpawnPoses.Length);
            Assert.AreEqual(new Vector3(1, 2, 3), _gameData.SpawnPoses[0].position);
            Assert.AreEqual(new Vector3(4, 5, 6), _gameData.SpawnPoses[1].position);

            UnityEngine.Object.DestroyImmediate(go1);
            UnityEngine.Object.DestroyImmediate(go2);
        }

        #endregion
    }
}
