using System;
using System.IO;
using System.Linq;
using CosmicShore.Engine;

namespace CosmicShore.Tests;

/// <summary>
/// ROADMAP C1: the engine hands a --replay file to the game's own ReplayPlayer (ParityRun.BeginSession,
/// Port/parity/README.md). The game reads the file with JsonUtility through ReplayFile
/// (Assets/_Scripts/Utility/Replay/ReplayFile.cs, compiled live and out of this test project's
/// reach), so what is pinned here is the engine contract that handoff stands on: JsonUtility reads
/// the version-1 replay shape - the C# keyword field <c>@do</c> under its JSON name, an array of
/// [Serializable] status frames with string arrays inside - from the committed status case, and
/// writes it back. The vessel moving under those frames is proved by running the player
/// (Port/parity/README.md, "A case with status frames").
/// </summary>
public class ReplayHandoffTests
{
    // The same declarations as the game's ReplayStatusFrame and ReplayFile, field for field.
    [Serializable]
    sealed class StatusFrame
    {
        public int f;
        public float XSum, YSum, XDiff, YDiff, Throttle, LeftTriggerAnalog, RightTriggerAnalog;
        public string[] pressed = Array.Empty<string>();
        public string[] released = Array.Empty<string>();
    }

    [Serializable]
    sealed class Replay
    {
        public int version = 1;
        public string scene = "Bootstrap";
        public int seed;
        public int frames;
        public int checkpointEvery = 30;
        public string record = "";
        public string[] @do = Array.Empty<string>();
        public StatusFrame[] status = Array.Empty<StatusFrame>();
    }

    static string ParityDir()
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(ThisFile())!);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Port", "parity", "manifest.json"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "Port", "parity");
    }

    static string ThisFile([System.Runtime.CompilerServices.CallerFilePath] string path = "") => path;

    [Fact]
    public void TheCommittedStatusCaseReadsAsTheGameDeclaresIt()
    {
        var r = JsonUtility.FromJson<Replay>(File.ReadAllText(Path.Combine(ParityDir(), "replays", "skimrace-status.json")));
        Assert.Equal(1, r.version);
        Assert.Equal("Bootstrap", r.scene);
        Assert.Equal(2026, r.seed);
        Assert.Equal(2760, r.frames);
        Assert.Equal(new[] { "1500:arcade SkimRace", "1620:arcade start", "1800:arcade ready" }, r.@do);
        Assert.Equal(600, r.status.Length);
        Assert.Equal(-1f, r.status[0].XSum);
        Assert.Equal(0.5f, r.status[0].XDiff);                          // XDiff is the speed term: 0.5 is a keyboard at rest (cruise)
        Assert.Equal(new[] { "Button1Action" }, r.status[60].pressed);
        Assert.Equal(new[] { "Button1Action" }, r.status[90].released);
        Assert.Empty(r.status[61].pressed);
        // A full-speed stretch (XDiff 1 with the E key, Throttle 1) in the middle; cruise elsewhere.
        Assert.True(r.status.Skip(300).Take(150).All(s => s.XDiff == 1f && s.Throttle == 1f));
        Assert.True(r.status.Take(300).Concat(r.status.Skip(450)).All(s => s.XDiff == 0.5f && s.Throttle == 0f));
        // The held last frame is a keyboard at rest, so nothing is still commanded when the stream ends.
        Assert.Equal((0f, 0f, 0.5f, 0f), (r.status[599].XSum, r.status[599].YSum, r.status[599].XDiff, r.status[599].Throttle));
        // No frame is a full stop (XDiff 0 pins the vessel), and the yaw sweep reaches both sides.
        Assert.True(r.status.All(s => s.XDiff >= 0.5f));
        Assert.True(r.status.Any(s => s.XSum > 0.5f) && r.status.Any(s => s.XSum < -0.5f));
    }

    [Fact]
    public void TheShapeRoundTripsThroughJsonUtility()
    {
        var replay = new Replay
        {
            seed = 7, frames = 120, @do = new[] { "10:key Enter" },
            status = new[] { new StatusFrame { f = 0, XSum = 0.25f, Throttle = 1f, pressed = new[] { "Button2Action" } }, new StatusFrame { f = 1, YSum = -0.5f } },
        };
        string json = JsonUtility.ToJson(replay);
        Assert.Contains("\"do\":[\"10:key Enter\"]", json);                    // @do writes under the keyword's name
        Assert.Contains("\"status\":[{", json);
        var back = JsonUtility.FromJson<Replay>(json);
        Assert.Equal(7, back.seed);
        Assert.Equal(new[] { "10:key Enter" }, back.@do);
        Assert.Equal(2, back.status.Length);
        Assert.Equal(0.25f, back.status[0].XSum);
        Assert.Equal(new[] { "Button2Action" }, back.status[0].pressed);
        Assert.Empty(back.status[1].pressed);
        Assert.Equal(-0.5f, back.status[1].YSum);
    }

    [Fact]
    public void ACaseWithNoStatusFramesStillParsesWithEmptyArrays()
    {
        var r = JsonUtility.FromJson<Replay>(File.ReadAllText(Path.Combine(ParityDir(), "replays", "skimrace-fly.json")));
        Assert.Equal(1337, r.seed);
        Assert.Equal(6, r.@do.Length);
        Assert.NotNull(r.status);
        Assert.Empty(r.status);                                          // the do stream drives the devices
    }
}
