using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CosmicShore.Cli;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Engine.Networking;
using CosmicShore.Engine.Soap;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;

namespace CosmicShore.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// Joust arc — overtake jousting on the ported controller chain.
//
// Unit layers: JoustScoringRuleSO (domain-aggregated end condition over
// JoustCollisions sums, golf scores: winning domain shares elapsed time, losers
// a flat sentinel, joust-count tiebreak), the REAL trigger pipeline (a vessel's
// non-trigger contact bubble entering an opposing skimmer's trigger sphere →
// OnTriggerEnter → ImpactCollider routing → SkimmerImpactor.AcceptImpactee →
// verbatim VesselExplosionBySkimmerEffectSO speed/domain checks →
// OnJoustCollision → the StatsManager-shaped increment onto RoundStats), and
// NetworkJoustCollisionTurnMonitor (target resolution through the real
// EndConditionOverrides tool asset + domain-aggregated CheckForEndOfTurn).
//
// Integration layer: JoustRound (CosmicShore.Cli) runs the WHOLE match through
// the real chain — ready → countdown → player-seek AI → trigger-pass jousts →
// SyncJoustResults → GameDataSO.Results — and these tests assert JOUST.md's
// scoring semantics on its result.
//
// Rounds run sequentially (assembly-wide parallelization is disabled — the
// GameLoop/Time are process-global), every spawned NetworkBehaviour is
// despawned and every monitor stopped before its loop is disposed (async-void
// discipline), and the EndConditionOverrides static cache + Resources
// registration are reset by every test that touches them.
// ─────────────────────────────────────────────────────────────────────────────
public class JoustTests
{
    const float Dt = 1f / 60f;

    static void SetField(object target, string field, object value)
    {
        for (var t = target.GetType(); t != null; t = t.BaseType)
        {
            var f = t.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            if (f == null) continue;
            f.SetValue(target, value);
            return;
        }
        throw new MissingFieldException(target.GetType().Name, field);
    }

    static void ResetEndConditionOverrides()
    {
        typeof(EndConditionOverridesSO)
            .GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!
            .SetValue(null, null);
        Resources.Register(EndConditionOverridesSO.ResourcePath, null);
    }

    static GameDataSO MakeGameData()
    {
        NetworkManager.Singleton = null;
        return ScriptableObject.CreateInstance<GameDataSO>();
    }

    static RoundStats MakeStats(string name, Domains domain, int jousts)
        => new RoundStats { Name = name, Domain = domain, JoustCollisions = jousts };

    static JoustScoringRuleSO MakeRule()
    {
        var rule = ScriptableObject.CreateInstance<JoustScoringRuleSO>();
        SetField(rule, "metric", ScoringMetric.Jousts);
        SetField(rule, "golfRules", true);
        return rule;
    }

    // ── JoustScoringRuleSO ──────────────────────────────────────────────────

    [Fact]
    public void ScoringRule_ObjectiveIsDomainAggregated_TeammatesSumToTarget()
    {
        var gameData = MakeGameData();
        gameData.RequestedDomainCount = 2;
        gameData.JoustTargetCount = 3;
        var rule = MakeRule();

        // 1 + 1 Jade vs 2 Ruby — no domain at 3 yet.
        gameData.RoundStatsList.Add(MakeStats("A", Domains.Jade, 1));
        gameData.RoundStatsList.Add(MakeStats("B", Domains.Jade, 1));
        gameData.RoundStatsList.Add(MakeStats("C", Domains.Ruby, 2));
        Assert.False(rule.IsObjectiveReached(gameData, out var winner));
        Assert.Equal(Domains.Blue, winner);

        // Jade's TEAM sum reaches 3 (2+1) — the turn ends for the domain, no individual has 3.
        gameData.RoundStatsList[0].JoustCollisions = 2;
        Assert.True(rule.IsObjectiveReached(gameData, out winner));
        Assert.Equal(Domains.Jade, winner);

        // Remaining is the domain deficit (used by the HUD readout).
        Assert.Equal(0, rule.Remaining(gameData, Domains.Jade));
        Assert.Equal(1, rule.Remaining(gameData, Domains.Ruby));
    }

    [Fact]
    public void ScoringRule_GolfScores_WinnersShareTime_LosersGetSentinel_TiebreakByJousts()
    {
        var gameData = MakeGameData();
        gameData.RequestedDomainCount = 2;
        gameData.JoustTargetCount = 3;
        var rule = MakeRule();

        var finisher = MakeStats("Finisher", Domains.Jade, 2);
        var assist = MakeStats("Assist", Domains.Jade, 1);
        var loser = MakeStats("Loser", Domains.Ruby, 1);
        gameData.RoundStatsList.Add(assist);
        gameData.RoundStatsList.Add(finisher);
        gameData.RoundStatsList.Add(loser);

        Assert.Equal(Domains.Jade, rule.ResolveWinner(gameData));
        gameData.WinnerDomain = Domains.Jade;

        rule.AssignScores(gameData, Domains.Jade, 32.5f);
        Assert.Equal(32.5f, finisher.Score);
        Assert.Equal(32.5f, assist.Score);                              // teammates share the finish time
        Assert.Equal(GolfScoreSentinels.JoustLoserScore, loser.Score);  // losers all tie on the sentinel
        Assert.True(GolfScoreSentinels.IsFinishTime(finisher.Score));
        Assert.False(GolfScoreSentinels.IsFinishTime(loser.Score));

        // Golf ranking: ascending score, JoustCollisions-descending tiebreak orders the
        // finisher above the assist; the loser row shows the DOMAIN's jousts-left line.
        var results = rule.BuildResults(gameData);
        Assert.Equal(new[] { 1, 2, 3 }, results.Select(r => r.Rank).ToArray());
        Assert.Equal("Finisher", results[0].Name);
        Assert.Equal("Assist", results[1].Name);
        Assert.Equal("Loser", results[2].Name);
        Assert.Equal("00:32:50", results[0].ScoreText);      // mm:ss:cs finish time
        Assert.Equal("2 Jousts Left", results[2].ScoreText); // domain deficit, not personal
        Assert.Equal("2 Jousts", results[0].Secondary);      // live metric line
    }

    // ── the real trigger pipeline: contact → effect → RoundStats ───────────

    /// <summary>MonoBehaviour IVessel for the trigger rig (VesselImpactor requires a same-GO IVessel).</summary>
    sealed class TriggerVessel : MonoBehaviour, IVessel
    {
        public event Action OnInitialized { add { } remove { } }
        public event Action OnBeforeDestroyed { add { } remove { } }
        public IVesselStatus VesselStatus { get; set; }
        public bool IsNetworkOwner => false;
        public bool IsNetworkClient => false;
        public ulong PlayerNetId => 0;
        public ulong VesselNetId => 0;
        public ulong OwnerClientNetId => 0;
        public Transform Transform => transform;

        public void BindElementalFloat(string name, Element element) { }
        public void Initialize(IPlayer player) { }
        public void PerformShipControllerActions(InputEvents @event) { }
        public void StopShipControllerActions(InputEvents @event) { }
        public void Teleport(Transform transform) { }
        public void SetResourceLevels(ResourceCollection resources) { }
        public void SetShipUp(float angle) { }
        public void DisableSkimmer() { }
        public void SetBoostMultiplier(float boostMultiplier) { }
        public void SetShipMaterial(Material material) { }
        public void SetBlockSilhouettePrefab(GameObject prefab) { }
        public void SetAOEExplosionMaterial(Material material) { }
        public void SetAOEConicExplosionMaterial(Material material) { }
        public void SetSkimmerMaterial(Material material) { }
        public void SetTrailColors(Color highlightColor, Color coreColor) { }
        public void ToggleAIPilot(bool toggle) { }
        public void StartVessel() { }
        public bool AllowClearPrismInitialization() => false;
        public void DestroyVessel() { }
        public void ResetForPlay() { }
        public void SetPose(Pose pose) { }
        public void SetInitialSpeed(float initialSpeed) { }
        public void ChangePlayer(IPlayer player) { }
        public void ModifyThrottle(float amount, float duration) { }
        public void AddSlowedShipTransformToGameData() { }
        public void RemoveSlowedShipTransformFromGameData() { }
    }

    sealed class JoustStubPlayer : IPlayer
    {
        public JoustStubPlayer(string name, Domains domain)
        {
            Name = name;
            RoundStats = new RoundStats { Name = name, Domain = domain };
        }

        public Domains Domain => RoundStats.Domain;
        public string Name { get; }
        public int AvatarId => 0;
        public string PlayerUUID => Name;
        public IVessel Vessel => null;
        public InputController InputController => null;
        public IInputStatus InputStatus => null;
        public IRoundStats RoundStats { get; }
        public bool IsActive => true;
        public bool IsInitializedAsAI => true;
        public bool IsMultiplayerOwner => true;
        public bool IsNetworkOwner => true;
        public bool IsNetworkClient => false;
        public bool IsLocalUser { get; set; } = true;
        public ulong PlayerNetId => 0;
        public ulong VesselNetId => 0;
        public ulong OwnerClientNetId => 0;
        public Transform Transform => null;

        public void InitializeForSinglePlayerMode(IPlayer.InitializeData data, IVessel vessel) { }
        public void InitializeForMultiplayerMode(IVessel vessel) { }
        public void ToggleGameObject(bool toggle) { }
        public void DestroyPlayer() { }
        public void StartPlayer() { }
        public void ResetForPlay() { }
        public void SetPoseOfVessel(Pose pose) { }
        public void ChangeVessel(IVessel vessel) { }
    }

    /// <summary>One side of the contact: a vessel GO carrying the impactor rig + a skimmer child.</summary>
    sealed class JoustRig
    {
        public GameObject VesselGo;
        public TriggerVessel Vessel;
        public StubVesselStatus Status;
        public JoustStubPlayer Player;
        public VesselImpactor VesselImpactor;
        public GameObject SkimmerGo;
    }

    static JoustRig BuildJoustRig(string name, Domains domain, float speed, Vector3 position,
        SkimmerImpactorDataContainerSO skimmerContainer, float bubbleRadius = 4f, float skimmerRadius = 8f)
    {
        var rig = new JoustRig { Player = new JoustStubPlayer(name, domain) };

        rig.VesselGo = new GameObject($"{name}-vessel");
        rig.VesselGo.transform.position = position;
        rig.Vessel = rig.VesselGo.AddComponent<TriggerVessel>();
        rig.Status = new StubVesselStatus
        {
            Vessel = rig.Vessel,
            Player = rig.Player,
            Speed = speed,
        };
        rig.Player.RoundStats.Domain = domain;
        rig.Vessel.VesselStatus = rig.Status;

        // Vessel side of the contact rig: non-trigger bubble + VesselImpactor (+
        // NetworkVesselImpactor pair, unspawned) + ImpactCollider — the HexRace/Joust shape.
        var bubble = rig.VesselGo.AddComponent<SphereCollider>();
        bubble.radius = bubbleRadius;
        var networkImpactor = rig.VesselGo.AddComponent<NetworkVesselImpactor>();
        rig.VesselImpactor = rig.VesselGo.AddComponent<VesselImpactor>();
        SetField(rig.VesselImpactor, "vesselImpactorDataContainerSO",
            ScriptableObject.CreateInstance<VesselImpactorDataContainerSO>());
        SetField(rig.VesselImpactor, "networkVesselImpactor", networkImpactor);
        SetField(networkImpactor, "vesselImpactor", rig.VesselImpactor);
        var vesselCollider = rig.VesselGo.AddComponent<ImpactCollider>();
        SetField(vesselCollider, "impactorObject", rig.VesselImpactor);

        // Skimmer side: trigger sphere + real Skimmer (initialized with this vessel's
        // status) + SkimmerImpactor carrying the joust-effect container + ImpactCollider.
        rig.SkimmerGo = new GameObject($"{name}-skimmer");
        rig.SkimmerGo.transform.SetParent(rig.VesselGo.transform);
        rig.SkimmerGo.transform.position = position;
        var trigger = rig.SkimmerGo.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = skimmerRadius;
        var skimmer = rig.SkimmerGo.AddComponent<Skimmer>();
        SetField(skimmer, "onSkimmerShipImpact", ScriptableObject.CreateInstance<ScriptableEventString>());
        skimmer.Initialize(rig.Status);
        var skimmerImpactor = rig.SkimmerGo.AddComponent<SkimmerImpactor>();
        SetField(skimmerImpactor, "skimmer", skimmer);
        SetField(skimmerImpactor, "skimmerImpactorDataContainer", skimmerContainer);
        var skimmerCollider = rig.SkimmerGo.AddComponent<ImpactCollider>();
        SetField(skimmerCollider, "impactorObject", skimmerImpactor);

        return rig;
    }

    [Fact]
    public void TriggerPipeline_FasterOpponentSkimmer_ScoresJoustOntoRoundStats()
    {
        using var loop = new GameLoop(nameof(TriggerPipeline_FasterOpponentSkimmer_ScoresJoustOntoRoundStats));
        NetworkManager.Singleton = null;
        var gameData = MakeGameData();

        // The joust effect + its SOAP channel (fail-loud: wired, never null-guarded).
        var onJoustCollision = ScriptableObject.CreateInstance<ScriptableEventString>();
        var effect = ScriptableObject.CreateInstance<VesselExplosionBySkimmerEffectSO>();
        SetField(effect, "OnJoustCollision", onJoustCollision);
        var container = ScriptableObject.CreateInstance<SkimmerImpactorDataContainerSO>();
        SetField(container, "vesselSkimmerEffectsSO", new VesselSkimmerEffectsSO[] { effect });

        // Slow Jade vessel at the origin; fast Ruby vessel far away, its skimmer swept
        // onto the Jade vessel (the overtake). Distinct positions keep the reciprocal
        // pair (Ruby bubble × Jade skimmer) out of range, isolating one contact.
        var slow = BuildJoustRig("Slow", Domains.Jade, speed: 10f, position: Vector3.zero, container);
        var fast = BuildJoustRig("Fast", Domains.Ruby, speed: 30f, position: new Vector3(100f, 0f, 0f), container);
        fast.SkimmerGo.transform.position = new Vector3(5f, 0f, 0f); // overlaps Slow's bubble (4 + 8 > 5)

        gameData.RoundStatsList.Add(slow.Player.RoundStats);
        gameData.RoundStatsList.Add(fast.Player.RoundStats);

        // The StatsManager role (ExecuteJoustCollision): SOAP event → RoundStats increment.
        onJoustCollision.OnRaised += HandleJoust;
        void HandleJoust(string playerName)
        {
            if (gameData.TryGetRoundStats(playerName, out var stats))
                stats.JoustCollisions++;
        }

        try
        {
            loop.Tick(Dt); // trigger pass: Slow's bubble enters Fast's skimmer sphere

            // The joust credits the SKIMMER OWNER (the faster vessel) — JOUST.md §Collision Chain.
            Assert.Equal(1, fast.Player.RoundStats.JoustCollisions);
            Assert.Equal(0, slow.Player.RoundStats.JoustCollisions);
        }
        finally
        {
            onJoustCollision.OnRaised -= HandleJoust;
            loop.Tick(Dt);
        }
    }

    [Fact]
    public void TriggerPipeline_SameDomainOrSlowerSkimmer_NeverScores()
    {
        using var loop = new GameLoop(nameof(TriggerPipeline_SameDomainOrSlowerSkimmer_NeverScores));
        NetworkManager.Singleton = null;

        var onJoustCollision = ScriptableObject.CreateInstance<ScriptableEventString>();
        var effect = ScriptableObject.CreateInstance<VesselExplosionBySkimmerEffectSO>();
        SetField(effect, "OnJoustCollision", onJoustCollision);
        var container = ScriptableObject.CreateInstance<SkimmerImpactorDataContainerSO>();
        SetField(container, "vesselSkimmerEffectsSO", new VesselSkimmerEffectsSO[] { effect });

        int raises = 0;
        onJoustCollision.OnRaised += CountRaise;
        void CountRaise(string _) => raises++;

        try
        {
            // Same-domain overtake: faster teammate's skimmer over a slower teammate — no point
            // (the teammate is buffed by VesselOvertakeBySkimmerEffectSO instead; JOUST.md §11).
            var mateSlow = BuildJoustRig("MateA", Domains.Jade, speed: 10f, position: Vector3.zero, container);
            var mateFast = BuildJoustRig("MateB", Domains.Jade, speed: 30f, position: new Vector3(100f, 0f, 0f), container);
            mateFast.SkimmerGo.transform.position = new Vector3(5f, 0f, 0f);
            loop.Tick(Dt);
            Assert.Equal(0, raises);
            mateSlow.VesselGo.SetActive(false);
            mateFast.VesselGo.SetActive(false);

            // Slower skimmer: the skimmer owner is NOT faster than the vessel it swept — no point.
            var quick = BuildJoustRig("Quick", Domains.Jade, speed: 30f, position: new Vector3(200f, 0f, 0f), container);
            var lagger = BuildJoustRig("Lagger", Domains.Ruby, speed: 10f, position: new Vector3(300f, 0f, 0f), container);
            lagger.SkimmerGo.transform.position = new Vector3(205f, 0f, 0f); // overlaps Quick's bubble
            loop.Tick(Dt);
            Assert.Equal(0, raises);
            quick.VesselGo.SetActive(false);
            lagger.VesselGo.SetActive(false);
        }
        finally
        {
            onJoustCollision.OnRaised -= CountRaise;
            loop.Tick(Dt);
        }
    }

    // ── NetworkJoustCollisionTurnMonitor ────────────────────────────────────

    [Fact]
    public void NetworkMonitor_ResolvesTargetFromToolAsset_AndEndsTurnOnDomainSum()
    {
        using var loop = new GameLoop(nameof(NetworkMonitor_ResolvesTargetFromToolAsset_AndEndsTurnOnDomainSum));
        NetworkManager.Singleton = null;

        // The joust target through the REAL tool surface (End Game Conditions asset).
        ResetEndConditionOverrides();
        var overrides = ScriptableObject.CreateInstance<EndConditionOverridesSO>();
        overrides.joustCount = 2;
        Resources.Register(EndConditionOverridesSO.ResourcePath, overrides);

        var gameData = MakeGameData();
        gameData.RequestedDomainCount = 2;
        gameData.ScoringRule = MakeRule();

        var jade1 = new JoustStubPlayer("Jade1", Domains.Jade);
        var jade2 = new JoustStubPlayer("Jade2", Domains.Jade) { IsLocalUser = false };
        var ruby = new JoustStubPlayer("Ruby1", Domains.Ruby) { IsLocalUser = false };
        gameData.AddPlayer(jade1); // local user → LocalPlayer + LocalRoundStats
        gameData.AddPlayer(jade2);
        gameData.AddPlayer(ruby);

        var displays = new List<string>();
        var displayChannel = ScriptableObject.CreateInstance<ScriptableEventString>();
        displays.Clear();
        displayChannel.OnRaised += displays.Add;

        var go = new GameObject("joust-monitor");
        go.SetActive(false);
        var monitor = go.AddComponent<NetworkJoustCollisionTurnMonitor>();
        SetField(monitor, "gameData", gameData);
        SetField(monitor, "onUpdateTurnMonitorDisplay", displayChannel);
        go.SetActive(true);

        try
        {
            monitor.Spawn(); // IsServer=true — the authoritative end-of-turn check
            monitor.StartMonitor();

            // Target resolved from the tool asset and published for every consumer (R10).
            Assert.Equal(2, monitor.CollisionsNeeded);
            Assert.Equal(2, gameData.JoustTargetCount);
            Assert.Equal("2", displays[^1]); // local DOMAIN remaining

            // One joust by one teammate — domain sum 1, not there yet; HUD shows the
            // domain deficit (aggregated across teammates, not the local player's own).
            jade1.RoundStats.JoustCollisions = 1;
            Assert.False(monitor.CheckForEndOfTurn());
            Assert.Equal("1", displays[^1]);

            // The OTHER teammate's joust completes the DOMAIN sum (1 + 1 = 2).
            jade2.RoundStats.JoustCollisions = 1;
            Assert.True(monitor.CheckForEndOfTurn());
            Assert.Equal("0", displays[^1]);

            // Rule agreement: the objective reports the same winning domain.
            Assert.True(gameData.ScoringRule.IsObjectiveReached(gameData, out var winner));
            Assert.Equal(Domains.Jade, winner);
        }
        finally
        {
            monitor.StopMonitor();
            monitor.Despawn();
            displayChannel.OnRaised -= displays.Add;
            go.SetActive(false);
            loop.Tick(Dt);
            ResetEndConditionOverrides();
        }
    }

    // ── the full match through the real chain (CosmicShore.Cli round) ────────

    static JoustRoundResult RunMatch(int players, int seed)
        => JoustRound.Run(new JoustRoundOptions { PlayerCount = players, Seed = seed });

    [Fact]
    public void Match_Completes_JoustsFlowThroughTriggerPipeline_AndGolfScoresFollowJoustMd()
    {
        var result = RunMatch(players: 4, seed: 42);

        Assert.True(result.Finished, "match must end through the monitor → OnTurnEndedCustom → sync flow");
        Assert.Empty(result.EngineErrors); // fail loud on any Error/Exception logged mid-match
        Assert.NotEqual(Domains.Blue, result.WinnerDomain);
        Assert.False(string.IsNullOrEmpty(result.WinnerName));
        Assert.True(result.TotalJousts >= 3, "jousts must flow through the trigger pipeline to end the match");

        // One standings row per player, ranked 1..N, representative winner on top.
        Assert.Equal(4, result.Standings.Count);
        Assert.Equal(new[] { 1, 2, 3, 4 }, result.Standings.Select(s => s.Rank).ToArray());
        Assert.Equal(result.WinnerName, result.Standings[0].Name);
        Assert.Equal(result.WinnerDomain, result.Standings[0].Domain);

        // Domain-aggregated end: the winning domain's summed jousts reached the target (3).
        int winnerDomainJousts = result.Standings
            .Where(s => s.Domain == result.WinnerDomain)
            .Sum(s => s.Jousts);
        Assert.True(winnerDomainJousts >= 3);

        // Golf semantics (JOUST.md §7): every winning-domain player shares the elapsed-time
        // score (a real finish time); every loser carries the flat 99999 sentinel.
        var winners = result.Standings.Where(s => s.Domain == result.WinnerDomain).ToList();
        var losers = result.Standings.Where(s => s.Domain != result.WinnerDomain).ToList();
        Assert.NotEmpty(winners);
        Assert.NotEmpty(losers);
        foreach (var w in winners)
        {
            Assert.Equal(result.FinishTime, w.Score);
            Assert.True(GolfScoreSentinels.IsFinishTime(w.Score));
        }
        foreach (var l in losers)
        {
            Assert.Equal(GolfScoreSentinels.JoustLoserScore, l.Score);
            Assert.EndsWith("Jousts Left", l.ScoreText); // domain deficit line
        }

        // Golf rules: ascending scores; winners always rank ahead of the sentinel rows,
        // and within the winning domain the joust tiebreak orders finisher above assist.
        var scores = result.Standings.Select(s => s.Score).ToArray();
        Assert.Equal(scores.OrderBy(s => s), scores);
        Assert.True(winners.Max(w => w.Rank) < losers.Min(l => l.Rank));
        for (int i = 1; i < winners.Count; i++)
            Assert.True(winners[i - 1].Jousts >= winners[i].Jousts);
    }

    [Fact]
    public void Match_IsAPureFunctionOfItsSeed()
    {
        var first = RunMatch(players: 4, seed: 42);
        var second = RunMatch(players: 4, seed: 42);

        Assert.True(first.Finished && second.Finished);
        Assert.Equal(first.Transcript, second.Transcript); // joust-by-joust, line-identical
        Assert.Equal(first.WinnerName, second.WinnerName);
        Assert.Equal(first.WinnerDomain, second.WinnerDomain);
        Assert.Equal(first.FinishTime, second.FinishTime);
        Assert.Equal(first.FramesSimulated, second.FramesSimulated);
        Assert.Equal(
            first.Standings.Select(s => (s.Rank, s.Name, s.Domain, s.Jousts, s.Score)),
            second.Standings.Select(s => (s.Rank, s.Name, s.Domain, s.Jousts, s.Score)));

        var third = RunMatch(players: 4, seed: 7);
        Assert.NotEqual(first.Transcript, third.Transcript); // the seed is load-bearing
    }
}
