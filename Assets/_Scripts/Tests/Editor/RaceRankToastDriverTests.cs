#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// RaceRankToastDriver Tests - the ranking arithmetic behind SkimRace's "{a} overtook
    /// {b}" and "{a} is the race leader" toasts.
    ///
    /// WHY THIS MATTERS:
    /// The driver polls a ranking twice a second and announces the DELTA against the last
    /// poll, so every rule it has is a comparison between two states - exactly the kind of
    /// logic a play-test confirms only for the cases that happen to occur. Ties are the
    /// sharp edge: a player who merely EQUALS the leader must not be announced as passing
    /// them.
    ///
    /// WHAT PROTECTS TIES IS THE SORT, NOT THE GUARD THAT LOOKS LIKE IT.
    /// CheckOvertakes tests "ahead.CrystalsCollected greater-than behind.CrystalsCollected",
    /// which reads as the tie protection and is in fact UNREACHABLE: BuildRanking orders by
    /// crystals and breaks ties with ThenBy(PreviousRankOf), so two players who are both in
    /// the previous ranking can only swap places if their crystal counts actually differ.
    /// Mutating that comparison to greater-or-equal changes no observable behaviour
    /// (verified by mutation, 2026-09-08); mutating the ThenBy to ThenByDescending breaks
    /// two of these tests. Anyone "simplifying" the sort would silently lose tie protection
    /// while pointing at a guard that never runs.
    /// </summary>
    [TestFixture]
    public class RaceRankToastDriverTests
    {
        private sealed class FakeStats : IRoundStats
        {
        // 40 auto-properties and 39 events, generated from the interface. Only Name,
        // Domain and CrystalsCollected are read by the driver; the rest exist so the
        // stub compiles. A NetworkBehaviour (the real RoundStats) is deliberately NOT
        // used - adding one in an edit-mode test drags network lifecycle into a test
        // about ranking arithmetic.
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
        public int GoalsScored { get; set; }
        public int LifeformsKilled { get; set; }
        public int BulletHitsLanded { get; set; }
        public int MissileHitsLanded { get; set; }
        public int DebuffHitsLanded { get; set; }
        public int CombatPoints { get; set; }
        public int SwitchesThreaded { get; set; }
        public float FullSpeedStraightAbilityActiveTime { get; set; }
        public float RightStickAbilityActiveTime { get; set; }
        public float LeftStickAbilityActiveTime { get; set; }
        public float FlipAbilityActiveTime { get; set; }
        public float Button1AbilityActiveTime { get; set; }
        public float Button2AbilityActiveTime { get; set; }
        public float Button3AbilityActiveTime { get; set; }

#pragma warning disable 67   // never raised: the driver polls, it does not subscribe
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
        public event Action<IRoundStats> OnGoalsScoredChanged;
        public event Action<IRoundStats> OnLifeformsKilledChanged;
        public event Action<IRoundStats> OnBulletHitsLandedChanged;
        public event Action<IRoundStats> OnMissileHitsLandedChanged;
        public event Action<IRoundStats> OnDebuffHitsLandedChanged;
        public event Action<IRoundStats> OnCombatPointsChanged;
        public event Action<IRoundStats> OnSwitchesThreadedChanged;
        public event Action<IRoundStats> OnFullSpeedStraightAbilityActiveTimeChanged;
        public event Action<IRoundStats> OnRightStickAbilityActiveTimeChanged;
        public event Action<IRoundStats> OnLeftStickAbilityActiveTimeChanged;
        public event Action<IRoundStats> OnFlipAbilityActiveTimeChanged;
        public event Action<IRoundStats> OnButton1AbilityActiveTimeChanged;
        public event Action<IRoundStats> OnButton2AbilityActiveTimeChanged;
        public event Action<IRoundStats> OnButton3AbilityActiveTimeChanged;
#pragma warning restore 67
        }

        private GameObject _host;
        private RaceRankToastDriver _driver;
        private GameDataSO _gameData;
        private readonly List<string> _posts = new();

        private static readonly FieldInfo LibraryField =
            typeof(RaceRankToastDriver).GetField("library", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo GameDataField =
            typeof(RaceRankToastDriver).GetField("gameData", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo TurnStarted =
            typeof(RaceRankToastDriver).GetMethod("HandleTurnStarted", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo Evaluate =
            typeof(RaceRankToastDriver).GetMethod("EvaluateRanking", BindingFlags.NonPublic | BindingFlags.Instance);

        [SetUp]
        public void SetUp()
        {
            // The real toast channel: the driver posts through GameToastAPI, which raises
            // this asset. Subscribing to it is what makes these assertions observe the
            // SHIPPED posting path rather than a re-implementation of it.
            var channel = Resources.Load<ScriptableEventGameToastData>(
                "Channels/GameToastChannel");
            Assert.IsNotNull(channel, "Resources/Channels/GameToastChannel is missing - the "
                                      + "toast feed cannot post without it.");
            channel.OnRaised += Record;

            _host = new GameObject("RaceRankToastDriverTests");
            _host.SetActive(false);          // no Awake/Start, so nothing needs injecting
            _driver = _host.AddComponent<RaceRankToastDriver>();
            _gameData = ScriptableObject.CreateInstance<GameDataSO>();
            _gameData.GameMode = GameModes.SkimRace;
            GameDataField.SetValue(_driver, _gameData);

            // The SHIPPED library and the SHIPPED SkimRace config, so these tests also fail
            // if either stops authoring Overtake / NewRaceLeader.
            var library = UnityEditor.AssetDatabase.LoadAssetAtPath<GameToastLibrarySO>(
                "Assets/_SO_Assets/Game Toasts/GameToastLibrary.asset");
            Assert.IsNotNull(library, "GameToastLibrary.asset not found at its shipped path.");
            LibraryField.SetValue(_driver, library);

            StartTurn();
        }

        [TearDown]
        public void TearDown()
        {
            var channel = Resources.Load<ScriptableEventGameToastData>(
                "Channels/GameToastChannel");
            if (channel != null) channel.OnRaised -= Record;
            if (_host != null) UnityEngine.Object.DestroyImmediate(_host);
            if (_gameData != null) UnityEngine.Object.DestroyImmediate(_gameData);
            _posts.Clear();
        }

        private void Record(GameToastData data)
        {
            if (data.Situation == GameToastSituation.Overtake ||
                data.Situation == GameToastSituation.NewRaceLeader)
                _posts.Add($"{data.Situation}({string.Join(",", data.Args)})");
        }

        private void StartTurn()
        {
            TurnStarted.Invoke(_driver, null);
            _posts.Clear();
        }

        /// <summary>One poll of the ranking, as (name, crystals) pairs.</summary>
        private void Poll(params (string name, int crystals)[] state)
        {
            _gameData.RoundStatsList.Clear();
            foreach (var (name, crystals) in state)
                _gameData.RoundStatsList.Add(new FakeStats
                {
                    Name = name, CrystalsCollected = crystals, Domain = Domains.Jade
                });
            Evaluate.Invoke(_driver, null);
        }

        private void AssertPosted(params string[] expected)
        {
            CollectionAssert.AreEqual(expected, _posts,
                $"posted [{string.Join(" | ", _posts)}]");
            _posts.Clear();
        }

        // ---- the SkimRace config still authors both situations -------------------------

        [Test]
        public void TheDriver_IsActiveForSkimRace()
        {
            var active = (bool)typeof(RaceRankToastDriver)
                .GetField("_active", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(_driver);
            Assert.IsTrue(active, "SkimRace's toast config must author Overtake and/or "
                                  + "NewRaceLeader, or these toasts can never fire.");
        }

        [Test]
        public void TheDriver_IsInactiveForAModeAuthoringNeitherSituation()
        {
            _gameData.GameMode = GameModes.Scurry;   // Crystal Capture authors neither
            StartTurn();
            var active = (bool)typeof(RaceRankToastDriver)
                .GetField("_active", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(_driver);
            Assert.IsFalse(active, "A mode that authors neither situation must self-gate off.");
        }

        // ---- leader ---------------------------------------------------------------------

        [Test]
        public void NoLeader_UntilSomebodyScores()
        {
            Poll(("A", 0), ("B", 0));
            AssertPosted();
        }

        [Test]
        public void NoLeader_WhenTheSolePlayerHasNotScored()
        {
            // A SOLE player, so the tie guard cannot be what produces the silence - this is
            // the only shape that reaches the "CrystalsCollected less-or-equal 0" check.
            Poll(("A", 0));
            AssertPosted();
        }

        [Test]
        public void TheFirstCrystal_NamesALeader()
        {
            Poll(("A", 1), ("B", 0));
            AssertPosted("NewRaceLeader(A)");
        }

        [Test]
        public void TheSameLeaderExtending_IsNotAnnouncedAgain()
        {
            Poll(("A", 1), ("B", 0));
            _posts.Clear();
            Poll(("A", 2), ("B", 0));
            AssertPosted();
        }

        [Test]
        public void ATiedTopSpot_HasNoLeader()
        {
            Poll(("A", 2), ("B", 2));
            AssertPosted();
        }

        [Test]
        public void BreakingATieAtTheTop_AnnouncesTheNewLeader()
        {
            Poll(("A", 2), ("B", 2));
            Poll(("A", 2), ("B", 3));
            AssertPosted("NewRaceLeader(B)");
        }

        // ---- overtakes -------------------------------------------------------------------

        [Test]
        public void TheFirstPoll_HasNoHistoryAndAnnouncesNoOvertake()
        {
            Poll(("A", 3), ("B", 2), ("C", 1));
            Assert.IsFalse(_posts.Any(p => p.StartsWith("Overtake")));
        }

        [Test]
        public void PassingSomeone_IsAnnounced()
        {
            Poll(("A", 3), ("B", 2));
            _posts.Clear();
            Poll(("A", 3), ("B", 4));
            AssertPosted("Overtake(B,A)");
        }

        [Test]
        public void MerelyEquallingSomeone_IsNotPassingThem()
        {
            Poll(("A", 3), ("B", 1));
            _posts.Clear();
            Poll(("A", 3), ("B", 3));
            AssertPosted();
        }

        [Test]
        public void BreakingThatTie_IsPassingThem()
        {
            Poll(("A", 3), ("B", 1));
            Poll(("A", 3), ("B", 3));
            _posts.Clear();
            Poll(("A", 3), ("B", 4));
            AssertPosted("Overtake(B,A)");
        }

        [Test]
        public void PassingTwoPlayersAtOnce_PostsTwoToasts_NearestFirst()
        {
            // Ordered by the overtaken player's NEW rank, so the nearest rival is announced
            // first - which is REVERSE chronological (C passed B before it passed A).
            // Pinned as the shipped behaviour, not endorsed: changing it is a feel call.
            Poll(("A", 3), ("B", 2), ("C", 1));
            _posts.Clear();
            Poll(("A", 3), ("B", 2), ("C", 4));
            AssertPosted("Overtake(C,A)", "Overtake(C,B)");
        }

        [Test]
        public void APollThatChangesNothing_IsSilent()
        {
            Poll(("A", 3), ("B", 2));
            _posts.Clear();
            Poll(("A", 3), ("B", 2));
            AssertPosted();
        }

        [Test]
        public void TakingTheLead_PostsBothSituations()
        {
            Poll(("A", 1), ("B", 0));
            _posts.Clear();
            Poll(("A", 1), ("B", 2));
            AssertPosted("NewRaceLeader(B)", "Overtake(B,A)");
        }

        [Test]
        public void ANewTurn_ForgetsTheRankingAndReAnnouncesTheLeader()
        {
            Poll(("A", 1), ("B", 2));
            StartTurn();
            Poll(("A", 1), ("B", 2));
            AssertPosted("NewRaceLeader(B)");
        }
    }
}
#endif
