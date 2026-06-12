using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Engine.Networking;
using CosmicShore.Engine.Soap;
using CosmicShore.Gameplay;
using CosmicShore.Utility;

namespace CosmicShore.Tests;

// V10 coverage: GameDataSO ported verbatim. Exercises the player-count pipeline
// (ConfigurePlayerCounts), the active-domain machinery (ActiveDomains order,
// IsActiveDomain clamping, server-synced per-domain metric sums), domain crystal
// aggregation (SumCrystalsCollectedByDomain), AddPlayer/LocalPlayer/RemovePlayerData
// bookkeeping, SetResults winner derivation, and golf-rule sorting/reset behavior.
// BuildHumanCounts is commented out under PORT Deviation (V10) until the concrete
// Player NetworkBehaviour ports — its coverage lands with that restore.
public class GameDataSOTests
{
    // ── test doubles ────────────────────────────────────────────────────────

    /// <summary>Minimal IPlayer for GameDataSO bookkeeping; records lifecycle calls.</summary>
    class StubPlayer : IPlayer
    {
        public StubPlayer(string name, Domains domain, bool isLocalUser = false)
        {
            Name = name;
            IsLocalUser = isLocalUser;
            RoundStats = new RoundStats { Name = name, Domain = domain };
        }

        public int ResetForPlayCalls;
        public int StartPlayerCalls;
        public int DestroyPlayerCalls;
        public readonly List<Pose> PosesSet = new();

        public Domains Domain => RoundStats.Domain;
        public string Name { get; }
        public int AvatarId => 0;
        public string PlayerUUID => Name;
        public IVessel Vessel => null;
        public InputController InputController => null;
        public IInputStatus InputStatus => null;
        public IRoundStats RoundStats { get; }
        public bool IsActive => true;
        public bool IsInitializedAsAI => false;
        public bool IsMultiplayerOwner => IsLocalUser;
        public bool IsNetworkOwner => IsLocalUser;
        public bool IsNetworkClient => !IsLocalUser;
        public bool IsLocalUser { get; }
        public ulong PlayerNetId => 0;
        public ulong VesselNetId => 0;
        public ulong OwnerClientNetId => 0;
        public Transform Transform => null;

        public void InitializeForSinglePlayerMode(IPlayer.InitializeData data, IVessel vessel) { }
        public void InitializeForMultiplayerMode(IVessel vessel) { }
        public void ToggleGameObject(bool toggle) { }
        public void DestroyPlayer() => DestroyPlayerCalls++;
        public void StartPlayer() => StartPlayerCalls++;
        public void ResetForPlay() => ResetForPlayCalls++;
        public void SetPoseOfVessel(Pose pose) => PosesSet.Add(pose);
        public void ChangeVessel(IVessel vessel) { }
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    static GameDataSO MakeGameData()
    {
        NetworkManager.Singleton = null; // offline / single-process default
        var data = ScriptableObject.CreateInstance<GameDataSO>();
        data.SelectedPlayerCount = ScriptableObject.CreateInstance<IntVariable>();
        return data;
    }

    static RoundStats MakeStats(string name, Domains domain, int crystals = 0, float score = 0f)
        => new RoundStats { Name = name, Domain = domain, CrystalsCollected = crystals, Score = score };

    // ── ConfigurePlayerCounts ───────────────────────────────────────────────

    [Fact]
    public void ConfigurePlayerCounts_StoresTotalAndComputesAIBackfill()
    {
        var data = MakeGameData();

        data.ConfigurePlayerCounts(totalDesired: 4, humanCount: 1);

        Assert.Equal(4, data.SelectedPlayerCount.Value);
        Assert.Equal(3, data.RequestedAIBackfillCount);
    }

    [Fact]
    public void ConfigurePlayerCounts_SoloFullParty_NoBackfill()
    {
        var data = MakeGameData();

        data.ConfigurePlayerCounts(totalDesired: 1, humanCount: 1);

        Assert.Equal(1, data.SelectedPlayerCount.Value);
        Assert.Equal(0, data.RequestedAIBackfillCount);
    }

    [Fact]
    public void ConfigurePlayerCounts_MoreHumansThanTotal_ClampsBackfillToZero()
    {
        var data = MakeGameData();

        data.ConfigurePlayerCounts(totalDesired: 2, humanCount: 4);

        Assert.Equal(2, data.SelectedPlayerCount.Value);
        Assert.Equal(0, data.RequestedAIBackfillCount);
    }

    // ── Active-domain machinery ─────────────────────────────────────────────

    [Fact]
    public void ActiveDomains_AreJadeRubyGold_InTieBreakOrder_BlueAbsent()
    {
        Assert.Equal(new[] { Domains.Jade, Domains.Ruby, Domains.Gold }, GameDataSO.ActiveDomains);
        Assert.DoesNotContain(Domains.Blue, GameDataSO.ActiveDomains);
    }

    [Theory]
    [InlineData(Domains.Jade, 1, true)]
    [InlineData(Domains.Ruby, 1, false)]
    [InlineData(Domains.Ruby, 2, true)]
    [InlineData(Domains.Gold, 2, false)]
    [InlineData(Domains.Gold, 3, true)]
    [InlineData(Domains.Blue, 3, false)] // Blue is the neutral sentinel, never active
    [InlineData(Domains.Jade, 0, true)]  // dc clamps up to 1
    [InlineData(Domains.Ruby, 0, false)]
    [InlineData(Domains.Gold, 99, true)] // dc clamps down to 3
    public void IsActiveDomain_HonorsDomainCountSlice_WithClamping(Domains d, int dc, bool expected)
        => Assert.Equal(expected, GameDataSO.IsActiveDomain(d, dc));

    [Fact]
    public void DomainMetricSums_RoundTripPerActiveDomain_AndRaiseOnChangeOnly()
    {
        var data = MakeGameData();
        int raises = 0;
        data.OnDomainMetricSumsChanged += () => raises++;

        data.SetDomainMetricSum(Domains.Jade, 5);
        data.SetDomainMetricSum(Domains.Ruby, 2);
        data.SetDomainMetricSum(Domains.Jade, 5); // unchanged — no raise
        data.SetDomainMetricSum(Domains.Blue, 9); // not an active domain — ignored

        Assert.Equal(5, data.GetDomainMetricSum(Domains.Jade));
        Assert.Equal(2, data.GetDomainMetricSum(Domains.Ruby));
        Assert.Equal(0, data.GetDomainMetricSum(Domains.Gold));
        Assert.Equal(0, data.GetDomainMetricSum(Domains.Blue));
        Assert.Equal(2, raises);
    }

    // ── Domain crystal aggregation ──────────────────────────────────────────

    [Fact]
    public void SumCrystalsCollectedByDomain_SumsTeammates_PerDomain()
    {
        var data = MakeGameData();
        data.RoundStatsList.Add(MakeStats("A", Domains.Jade, crystals: 3));
        data.RoundStatsList.Add(MakeStats("B", Domains.Jade, crystals: 4));
        data.RoundStatsList.Add(MakeStats("C", Domains.Ruby, crystals: 2));

        Assert.Equal(7, data.SumCrystalsCollectedByDomain(Domains.Jade));
        Assert.Equal(2, data.SumCrystalsCollectedByDomain(Domains.Ruby));
        Assert.Equal(0, data.SumCrystalsCollectedByDomain(Domains.Gold));
    }

    [Fact]
    public void CalculateDomainStats_SumsScoresPerDomain_AndSorts()
    {
        var data = MakeGameData();
        data.RoundStatsList.Add(MakeStats("A", Domains.Jade, score: 10f));
        data.RoundStatsList.Add(MakeStats("B", Domains.Jade, score: 5f));
        data.RoundStatsList.Add(MakeStats("C", Domains.Ruby, score: 20f));

        data.CalculateDomainStats(golfRules: false);

        Assert.Equal(2, data.DomainStatsList.Count);
        Assert.Equal(Domains.Ruby, data.DomainStatsList[0].Domain); // 20 ranks above 15
        Assert.Equal(20f, data.DomainStatsList[0].Score);
        Assert.Equal(Domains.Jade, data.DomainStatsList[1].Domain);
        Assert.Equal(15f, data.DomainStatsList[1].Score);
    }

    // ── AddPlayer / LocalPlayer ─────────────────────────────────────────────

    [Fact]
    public void AddPlayer_RegistersPlayerAndRoundStats_AndAssignsSpawnPose()
    {
        var data = MakeGameData();
        var player = new StubPlayer("Pilot", Domains.Jade);
        string addedName = null;
        Domains addedDomain = default;
        data.OnPlayerAdded += (name, domain) => { addedName = name; addedDomain = domain; };

        data.AddPlayer(player);

        Assert.Contains(player, data.Players);
        Assert.Contains(player.RoundStats, (IEnumerable<IRoundStats>)data.RoundStatsList);
        Assert.Equal(1, player.ResetForPlayCalls);
        Assert.Single(player.PosesSet); // Singleton null → offline path assigns a pose
        Assert.Equal("Pilot", addedName);
        Assert.Equal(Domains.Jade, addedDomain);
        Assert.Null(data.LocalPlayer); // not the local user
    }

    [Fact]
    public void AddPlayer_LocalUser_SetsLocalPlayerAndLocalRoundStats()
    {
        var data = MakeGameData();
        var remote = new StubPlayer("Remote", Domains.Ruby);
        var local = new StubPlayer("Local", Domains.Jade, isLocalUser: true);

        data.AddPlayer(remote);
        data.AddPlayer(local);

        Assert.Same(local, data.LocalPlayer);
        Assert.Same(local.RoundStats, data.LocalRoundStats);
        Assert.True(data.IsLocalDomain(Domains.Jade));
        Assert.False(data.IsLocalDomain(Domains.Ruby));
    }

    [Fact]
    public void AddPlayer_DuplicateName_NotAddedTwice()
    {
        var data = MakeGameData();
        var first = new StubPlayer("Pilot", Domains.Jade);
        var second = new StubPlayer("Pilot", Domains.Ruby);

        data.AddPlayer(first);
        data.AddPlayer(second);

        Assert.Single(data.Players);
        Assert.Single(data.RoundStatsList);
        Assert.Same(first, data.Players[0]);
    }

    [Fact]
    public void AddPlayer_DrawsFromConfiguredSpawnPoses()
    {
        using var loop = new GameLoop();
        var data = MakeGameData();
        var spawn = new GameObject("spawn").transform;
        spawn.position = new Vector3(1f, 2f, 3f);
        data.SetSpawnPositions(new[] { spawn });
        var player = new StubPlayer("Pilot", Domains.Jade);

        data.AddPlayer(player);

        Assert.Single(data.SpawnPoses);
        Assert.Equal(new Vector3(1f, 2f, 3f), player.PosesSet[0].position);
    }

    [Fact]
    public void RemovePlayerData_RemovesPlayerAndStats_AndFixesLocalPlayer()
    {
        var data = MakeGameData();
        var local = new StubPlayer("Local", Domains.Jade, isLocalUser: true);
        var other = new StubPlayer("Other", Domains.Ruby);
        data.AddPlayer(local);
        data.AddPlayer(other);

        bool removed = data.RemovePlayerData("Local");

        Assert.True(removed);
        Assert.Single(data.Players);
        Assert.Single(data.RoundStatsList);
        Assert.Same(other, data.LocalPlayer); // falls back to remaining player
        Assert.False(data.RemovePlayerData("Nobody"));
    }

    // ── Results / winner derivation ─────────────────────────────────────────

    [Fact]
    public void SetResults_DerivesWinnerFromTopRow_AndReusesListInstance()
    {
        var data = MakeGameData();
        var listRef = data.Results;

        data.SetResults(new[]
        {
            new ScoreResult(1, "Alice", Domains.Ruby, 84f, "01:24:00", null),
            new ScoreResult(2, "Bob", Domains.Jade, 10003f, "3 Crystals Left", null),
        });

        Assert.Same(listRef, data.Results);
        Assert.Equal(2, data.Results.Count);
        Assert.Equal("Alice", data.WinnerName);
        Assert.Equal(Domains.Ruby, data.WinnerDomain);
    }

    // ── Sorting / reset ─────────────────────────────────────────────────────

    [Fact]
    public void SortRoundStats_GolfRulesAscending_PointsDescending()
    {
        var data = MakeGameData();
        data.RoundStatsList.Add(MakeStats("High", Domains.Jade, score: 100f));
        data.RoundStatsList.Add(MakeStats("Low", Domains.Ruby, score: 1f));

        data.SortRoundStats(golfRules: true);
        Assert.Equal("Low", data.RoundStatsList[0].Name);
        Assert.True(data.IsGolfRules);

        data.SortRoundStats(golfRules: false);
        Assert.Equal("High", data.RoundStatsList[0].Name);
        Assert.False(data.IsGolfRules);
    }

    [Fact]
    public void ResetRuntimeData_ClearsRuntimeState_ButPreservesPreLaunchConfig()
    {
        var data = MakeGameData();
        data.ConfigurePlayerCounts(totalDesired: 6, humanCount: 2);
        data.RequestedDomainCount = 2;
        data.AddPlayer(new StubPlayer("Pilot", Domains.Jade, isLocalUser: true));
        data.SetDomainMetricSum(Domains.Jade, 5);
        data.SetResults(new[] { new ScoreResult(1, "Pilot", Domains.Jade, 1f, "1", null) });
        data.CrystalTargetCount = 39;
        data.JoustTargetCount = 7;

        data.ResetRuntimeData();

        Assert.Empty(data.Players);
        Assert.Empty(data.RoundStatsList);
        Assert.Null(data.LocalPlayer);
        Assert.Null(data.LocalRoundStats);
        Assert.Equal("", data.WinnerName);
        Assert.Equal(Domains.Blue, data.WinnerDomain);
        Assert.Empty(data.Results);
        Assert.Equal(0, data.CrystalTargetCount);
        Assert.Equal(0, data.JoustTargetCount);
        Assert.Equal(0, data.GetDomainMetricSum(Domains.Jade));
        // Pre-launch config survives until ResetAllData
        Assert.Equal(4, data.RequestedAIBackfillCount);
        Assert.Equal(2, data.RequestedDomainCount);
    }
}
