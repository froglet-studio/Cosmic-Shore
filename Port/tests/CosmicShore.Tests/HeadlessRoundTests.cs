using System.Collections.Generic;
using System.Linq;
using CosmicShore.Cli;
using CosmicShore.Data;
using CosmicShore.Gameplay;

namespace CosmicShore.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// port-m2 — the first full headless game-mode round (AI vs AI), in-process.
//
// HexRaceRound (CosmicShore.Cli) is an orchestration harness over verbatim ported
// systems only: PlayerSpawner.SpawnPlayerAndShip (C6) spawns the AI field, AIPilot
// does the real crystal seeking off CellRuntimeDataSO.OnCellItemsUpdated (V11/V18),
// a Cell (V12) hosts the seeded course, and HexRaceScoringRuleSO ends the race
// domain-aggregated, assigns golf scores, and builds the ranked ScoreResults that
// GameDataSO.SetResults publishes. These tests run a compact round (small crystal
// target) and assert the milestone exit criteria: a winner emerges, golf scores
// follow the rules, and the round is a pure function of its seed.
//
// The rounds run sequentially (assembly-wide test parallelization is disabled —
// GameLoop/Time are process-global) and HexRaceRound cleans its statics, so
// back-to-back runs inside one test are safe.
// ─────────────────────────────────────────────────────────────────────────────

public class HeadlessRoundTests
{
    const int CompactTarget = 3; // small target → short race, full scoring path

    static HexRaceRoundResult RunRound(int players, int seed, int target = CompactTarget)
        => HexRaceRound.Run(new HexRaceRoundOptions
        {
            PlayerCount = players,
            Seed = seed,
            CrystalTarget = target,
        });

    static IEnumerable<string> ClaimLines(HexRaceRoundResult result)
        => result.Transcript.Where(line => line.Contains("claims crystal"));

    // ── a winner emerges through the full pipeline ──────────────────────────

    [Fact]
    public void Round_Completes_WithAWinnerAndRankedResults()
    {
        var result = RunRound(players: 4, seed: 42);

        Assert.True(result.Finished, "round must reach the crystal objective");
        Assert.Empty(result.EngineErrors); // fail loud on any Error/Exception logged mid-round
        Assert.False(string.IsNullOrEmpty(result.WinnerName));
        Assert.NotEqual(Domains.Blue, result.WinnerDomain); // Blue is the "no team" sentinel
        Assert.True(result.FinishTime > 0f);
        Assert.True(result.TotalClaims >= CompactTarget); // at least the winning domain's claims

        // One standings row per player, ranked 1..N, winner's representative on top.
        Assert.Equal(4, result.Standings.Count);
        Assert.Equal(new[] { 1, 2, 3, 4 }, result.Standings.Select(s => s.Rank).ToArray());
        Assert.Equal(result.WinnerName, result.Standings[0].Name);
        Assert.Equal(result.WinnerDomain, result.Standings[0].Domain);

        // The winning domain's summed crystals reached the target (domain-aggregated end).
        int winnerDomainCrystals = result.Standings
            .Where(s => s.Domain == result.WinnerDomain)
            .Sum(s => s.Crystals);
        Assert.Equal(CompactTarget, winnerDomainCrystals);
    }

    // ── golf scoring per HexRaceScoringRuleSO ───────────────────────────────

    [Fact]
    public void Round_AssignsGolfScores_WinnersGetTime_LosersTieOnTeamDeficit()
    {
        var result = RunRound(players: 4, seed: 42);
        Assert.True(result.Finished);

        var winners = result.Standings.Where(s => s.Domain == result.WinnerDomain).ToList();
        var losers = result.Standings.Where(s => s.Domain != result.WinnerDomain).ToList();
        Assert.NotEmpty(winners);
        Assert.NotEmpty(losers);

        // Every winning-domain player scores the shared finish time (a real time, not a sentinel).
        foreach (var w in winners)
        {
            Assert.Equal(result.FinishTime, w.Score);
            Assert.True(GolfScoreSentinels.IsFinishTime(w.Score));
        }

        // Losers carry the encoded team deficit: DnfThreshold + crystals their DOMAIN still needed.
        foreach (var l in losers)
        {
            Assert.True(GolfScoreSentinels.IsHexRaceLoserScore(l.Score));
            int domainCollected = result.Standings.Where(s => s.Domain == l.Domain).Sum(s => s.Crystals);
            int domainRemaining = CompactTarget - domainCollected;
            Assert.Equal(GolfScoreSentinels.EncodeHexRaceLoserScore(domainRemaining), l.Score);
        }

        // Losing teammates on the same domain tie on Score (team deficit, not personal count).
        foreach (var group in losers.GroupBy(l => l.Domain).Where(g => g.Count() > 1))
            Assert.Single(group.Select(l => l.Score).Distinct());

        // Golf rules: standings sort ascending by score — winners always rank ahead of sentinels.
        var scores = result.Standings.Select(s => s.Score).ToArray();
        Assert.Equal(scores.OrderBy(s => s), scores);
        Assert.True(winners.Max(w => w.Rank) < losers.Min(l => l.Rank));
    }

    // ── determinism — the round is a pure function of its seed ──────────────

    [Fact]
    public void Round_SameSeed_ProducesIdenticalTranscript()
    {
        var first = RunRound(players: 4, seed: 42);
        var second = RunRound(players: 4, seed: 42);

        Assert.True(first.Finished && second.Finished);
        Assert.Equal(first.Transcript, second.Transcript); // claim-by-claim, line-identical
        Assert.Equal(first.WinnerName, second.WinnerName);
        Assert.Equal(first.WinnerDomain, second.WinnerDomain);
        Assert.Equal(first.FinishTime, second.FinishTime);
        Assert.Equal(first.FramesSimulated, second.FramesSimulated);
        Assert.Equal(
            first.Standings.Select(s => (s.Rank, s.Name, s.Domain, s.Crystals, s.Score)),
            second.Standings.Select(s => (s.Rank, s.Name, s.Domain, s.Crystals, s.Score)));
    }

    [Fact]
    public void Round_DifferentSeed_ProducesDifferentClaimOrder()
    {
        var first = RunRound(players: 4, seed: 42);
        var second = RunRound(players: 4, seed: 1337);

        Assert.True(first.Finished && second.Finished);

        // A different seed lays a different course → the claim sequence (who/when) differs.
        Assert.NotEqual(ClaimLines(first).ToList(), ClaimLines(second).ToList());
    }

    // ── Arc G: the stepped handle IS the blocking Run ────────────────────────
    // The windowed mode host drives Setup → StepFrame → FinishAndScore directly;
    // this pins that path to the CLI's Run — same world, same claims, same
    // standings, transcript line-identical.

    [Fact]
    public void SteppedHandle_MatchesBlockingRun_Exactly()
    {
        var blocking = RunRound(players: 4, seed: 42);

        var options = new HexRaceRoundOptions
        {
            PlayerCount = 4,
            Seed = 42,
            CrystalTarget = CompactTarget,
        };
        HexRaceRoundResult stepped;
        using (var handle = HexRaceRound.Setup(options))
        {
            while (handle.FramesStepped < handle.Options.MaxFrames)
            {
                if (handle.StepFrame())
                    break;
            }
            handle.FinishAndScore(); // CompleteStepping folds in (unsubscribe + frame stamp)
            stepped = handle.Result;
        }

        Assert.True(stepped.Finished);
        Assert.Empty(stepped.EngineErrors);
        Assert.Equal(blocking.Transcript, stepped.Transcript);
        Assert.Equal(blocking.WinnerName, stepped.WinnerName);
        Assert.Equal(blocking.WinnerDomain, stepped.WinnerDomain);
        Assert.Equal(blocking.FinishTime, stepped.FinishTime);
        Assert.Equal(blocking.FramesSimulated, stepped.FramesSimulated);
        Assert.Equal(blocking.TotalClaims, stepped.TotalClaims);
        Assert.Equal(
            blocking.Standings.Select(s => (s.Rank, s.Name, s.Domain, s.Crystals, s.Score)),
            stepped.Standings.Select(s => (s.Rank, s.Name, s.Domain, s.Crystals, s.Score)));
    }

    [Fact]
    public void SteppedCaptureHandle_MatchesBlockingRun_Exactly()
    {
        CrystalCaptureRoundResult RunBlocking() => CrystalCaptureRound.Run(new CrystalCaptureRoundOptions
        {
            PlayerCount = 4,
            Seed = 42,
            CrystalTarget = CompactTarget,
        });

        var blocking = RunBlocking();

        CrystalCaptureRoundResult stepped;
        using (var handle = CrystalCaptureRound.Setup(new CrystalCaptureRoundOptions
        {
            PlayerCount = 4,
            Seed = 42,
            CrystalTarget = CompactTarget,
        }))
        {
            while (handle.FramesStepped < handle.MaxFrames)
            {
                if (handle.StepFrame())
                    break;
            }
            handle.FinishAndScore();
            stepped = handle.Result;
        }

        Assert.True(stepped.Finished);
        Assert.Empty(stepped.EngineErrors);
        Assert.Equal(blocking.Transcript, stepped.Transcript);
        Assert.Equal(blocking.WinnerName, stepped.WinnerName);
        Assert.Equal(blocking.WinnerDomain, stepped.WinnerDomain);
        Assert.Equal(blocking.WinnerDomainCrystals, stepped.WinnerDomainCrystals);
        Assert.Equal(blocking.FramesSimulated, stepped.FramesSimulated);
        Assert.Equal(blocking.TotalClaims, stepped.TotalClaims);
        Assert.Equal(
            blocking.Standings.Select(s => (s.Rank, s.Name, s.Domain, s.Crystals, s.Score)),
            stepped.Standings.Select(s => (s.Rank, s.Name, s.Domain, s.Crystals, s.Score)));
    }
}
