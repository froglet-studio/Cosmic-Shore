using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Prisma;

namespace CosmicShore.Tests;

/// <summary>
/// Prisma's run memory and board: session reports fold into tracks (with the CPU, allocation and
/// GC figures), and a bug that came from the tracks is verified by the tracks - its acceptance
/// check passes when the problem stays away for three runs through its scene, and a relapse
/// reopens it.
/// </summary>
public class PrismaTracksBoardTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "prisma-tracks-" + Guid.NewGuid().ToString("N"));
    int _n;
    DateTime _clock = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    public PrismaTracksBoardTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { try { Directory.Delete(_dir, true); } catch (IOException) { } }

    string Report(bool withError, double gcMsPerFrame = 0.05)
    {
        _clock = _clock.AddMinutes(10);
        var report = new
        {
            kind = "prisma-session",
            version = 1,
            startedUtc = _clock.ToString("O"),
            seconds = 60.0,
            exit = "quit",
            frames = new { presented = 3600, p50Ms = 8.0, p95Ms = 12.0 },
            cpu = new { simP50Ms = 3.0, simP95Ms = 5.5, renderP50Ms = 2.0, renderP95Ms = 4.0 },
            memory = new { kbPerFrameP50 = 24.0, steadyGcPauseMsPerFrame = gcMsPerFrame, steadyGen2PerMin = 0.5 },
            scenes = new[] { new { name = "Menu_Main" } },
            perScene = new[] { new { scene = "Menu_Main", frames = 3000, p50Ms = 8.0, p95Ms = 12.0 } },
            counts = new { errors = withError ? 1 : 0, exceptions = 0, warnings = 0 },
            errors = withError ? new[] { new { count = 2, message = "[Hangar] Missing vessel 42" } } : Array.Empty<object>(),
        };
        var path = Path.Combine(_dir, $"run{++_n}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(report));
        return path;
    }

    [Fact]
    public void Ingest_CarriesCpuAndMemory_IntoTheRunAndTheBrief()
    {
        var t = new PrismaTracks();
        var r = t.Ingest(Report(false))!.Run;
        Assert.Equal(5.5, r.SimP95);
        Assert.Equal(4.0, r.RenderP95);
        Assert.Equal(24.0, r.AllocKBPerFrame);
        Assert.Equal(0.5, r.Gen2PerMin);
        Assert.Contains("cpu p95 sim 5.5 / render 4.0 ms", t.Memory());
        Assert.DoesNotContain(t.Issues.Values, i => i.Area == "gc");
    }

    [Fact]
    public void SteadyGcPauses_OverAMillisecondPerFrame_AreAPerfProblem()
    {
        var t = new PrismaTracks();
        t.Ingest(Report(false, gcMsPerFrame: 2.4));
        var gc = Assert.Single(t.Issues.Values, i => i.Area == "gc");
        Assert.Equal("perf", gc.Kind);
        t.Ingest(Report(false, gcMsPerFrame: 3.1));
        Assert.Single(t.Issues.Values, i => i.Area == "gc"); // the same problem across runs, not a new one each time
    }

    [Fact]
    public void ATrackedBug_IsVerifiedByTheTracks_AndReopensWhenItComesBack()
    {
        var t = new PrismaTracks();
        var b = new PrismaBoard();
        t.Ingest(Report(true));
        var suggested = Assert.Single(b.Suggest(t));
        Assert.Equal(PrismaBoard.Status.Suggested, suggested.State);
        Assert.Contains("3 runs through Menu_Main", suggested.Criterion);

        b.Move(suggested, PrismaBoard.Status.Doing);
        t.Ingest(Report(false));
        t.Ingest(Report(false));
        Assert.Empty(b.Verify(t));                    // two clean runs: not yet
        t.Ingest(Report(false));
        var (item, met) = Assert.Single(b.Verify(t)); // the third: the check passes
        Assert.True(met);
        Assert.NotNull(item.CriterionMet);
        Assert.Equal(PrismaBoard.Status.Doing, item.State); // moving to DONE stays the user's call

        b.Move(item, PrismaBoard.Status.Done);
        t.Ingest(Report(true));
        var (back, metAgain) = Assert.Single(b.Verify(t));
        Assert.False(metAgain);
        Assert.Null(back.CriterionMet);
        Assert.Equal(PrismaBoard.Status.Todo, back.State);   // a relapse reopens the card
    }

    [Fact]
    public void MilestoneSuggestions_CarryTheCheckpointsExitCriterion()
    {
        var b = new PrismaBoard();
        var cps = new List<(string, string, string, List<string>, string)>
        {
            ("C0", "Foundations", "done", new List<string>(), "x"),
            ("C1", "Parity harness", "todo", new List<string> { "C0" }, "Scoreboard runs headless and reports per-subsystem deltas"),
        };
        var task = Assert.Single(b.Suggest(new PrismaTracks(), cps));
        Assert.Equal("C1", task.Milestone);
        Assert.Equal("Scoreboard runs headless and reports per-subsystem deltas", task.Criterion);
    }
}
