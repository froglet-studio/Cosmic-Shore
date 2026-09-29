using System.Collections.Generic;
using CosmicShore.Engine;
using CosmicShore.Engine.Audio.Fmod;

namespace CosmicShore.Tests;

// The FMOD surface drives an installed backend (the vendor runtime in the player) in addition to
// its own local state; with no backend it is the silent local-state model the other tests use.
public class FmodBackendTests
{
    sealed class Recorder : IFmodBackend
    {
        public readonly List<string> Calls = new();
        public PLAYBACK_STATE? State = PLAYBACK_STATE.PLAYING;
        public Vector3 LastPosition;
        public void Create(EventInstanceState s, EventReference r) => Calls.Add("create " + r.Path);
        public void Start(EventInstanceState s) => Calls.Add("start");
        public void Stop(EventInstanceState s, STOP_MODE m) => Calls.Add("stop " + m);
        public void Release(EventInstanceState s) => Calls.Add("release");
        public void SetVolume(EventInstanceState s, float v) => Calls.Add($"volume {v}");
        public void SetPaused(EventInstanceState s, bool p) => Calls.Add($"paused {p}");
        public void Set3DAttributes(EventInstanceState s, in ATTRIBUTES_3D a) { LastPosition = a.position; Calls.Add("3d"); }
        public void SetParameter(EventInstanceState s, string n, float v, bool i) => Calls.Add($"param {n}={v}");
        public PLAYBACK_STATE? GetPlaybackState(EventInstanceState s) => State;
        public void SetBus(string p, float v, bool m, bool pa) => Calls.Add($"bus {p} {v} {m}");
        public void SetVca(string p, float v) => Calls.Add($"vca {p} {v}");
        public void SetGlobalParameter(string n, float v) => Calls.Add($"global {n}={v}");
        public void SetListener(int i, in ATTRIBUTES_3D a) => Calls.Add($"listener {i}");
        public void Update() => Calls.Add("update");
    }

    [Fact]
    public void EventCallsReachTheBackend_ParametersByName()
    {
        var rec = new Recorder();
        RuntimeManager.ResetForTests();
        FmodBackend.Current = rec;
        try
        {
            var i = RuntimeManager.CreateInstance("event:/SFX/Test");
            i.setVolume(0.5f);
            i.setParameterByID(PARAMETER_ID.FromName("Intensity"), 2f);
            i.start();
            i.stop(STOP_MODE.ALLOWFADEOUT);
            i.release();
            Assert.Equal(new[] { "create event:/SFX/Test", "volume 0.5", "param Intensity=2", "start", "stop ALLOWFADEOUT", "release" }, rec.Calls);

            RuntimeManager.StudioSystem.setParameterByName("Night", 1f);
            RuntimeManager.GetBus("bus:/SFX").setVolume(0.25f);
            Assert.Contains("global Night=1", rec.Calls);
            Assert.Contains("bus bus:/SFX 0.25 False", rec.Calls);
        }
        finally { FmodBackend.Current = null; RuntimeManager.ResetForTests(); }
    }

    [Fact]
    public void AttachedOneShot_FollowsItsObjectUntilItStops_ThenTheRuntimeUpdates()
    {
        using var loop = new GameLoop();
        var rec = new Recorder();
        RuntimeManager.ResetForTests();
        FmodBackend.Current = rec;
        try
        {
            var go = new GameObject("Emitter");
            RuntimeManager.PlayOneShotAttached(new EventReference { Path = "event:/SFX/Loop" }, go);
            go.transform.position = new Vector3(3, 4, 5);
            RuntimeManager.Update();
            Assert.Equal(new Vector3(3, 4, 5), rec.LastPosition);
            Assert.Equal("update", rec.Calls[^1]);

            rec.State = PLAYBACK_STATE.STOPPED;       // the one-shot finished
            RuntimeManager.Update();
            go.transform.position = new Vector3(9, 9, 9);
            RuntimeManager.Update();
            Assert.Equal(new Vector3(3, 4, 5), rec.LastPosition); // no longer followed
        }
        finally { FmodBackend.Current = null; RuntimeManager.ResetForTests(); }
    }

    [Fact]
    public void LivePlaybackStateComesFromTheRuntime()
    {
        var rec = new Recorder { State = PLAYBACK_STATE.STOPPED };
        RuntimeManager.ResetForTests();
        FmodBackend.Current = rec;
        try
        {
            var i = RuntimeManager.CreateInstance("event:/SFX/Test");
            i.start();
            i.getPlaybackState(out var st);
            Assert.Equal(PLAYBACK_STATE.STOPPED, st); // a finished one-shot, which the local model cannot know
        }
        finally { FmodBackend.Current = null; RuntimeManager.ResetForTests(); }
    }
}
