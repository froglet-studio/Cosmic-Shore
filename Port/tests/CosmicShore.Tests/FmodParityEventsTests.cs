using System;
using System.Collections.Generic;
using System.IO;
using CosmicShore.Engine;
using CosmicShore.Engine.Audio;
using CosmicShore.Engine.Audio.Fmod;

namespace CosmicShore.Tests;

// ROADMAP C4: the FMOD channel the parity harness writes (RuntimeManager.EventRecorded) is the
// whole sequence Unity's would be - starts and restarts, explicit stops with their mode, and
// mixer snapshots (FMOD and Unity AudioMixer) as their own kind - and names every event by path.
public class FmodParityEventsTests : IDisposable
{
    readonly List<string> _events = new();

    public FmodParityEventsTests()
    {
        RuntimeManager.ResetForTests();
        RuntimeManager.EventRecorded = (kind, name) => _events.Add(kind + ":" + name);
    }

    public void Dispose()
    {
        RuntimeManager.EventRecorded = null;
        FmodBackend.Current = null;
        FmodGuids.Clear();
        RuntimeManager.ResetForTests();
    }

    sealed class Descriptions : IFmodBackend
    {
        public readonly HashSet<string> Looping = new();
        public readonly HashSet<string> Snapshots = new();
        public void Create(EventInstanceState s, EventReference r) { }
        public void Start(EventInstanceState s) { }
        public void Stop(EventInstanceState s, STOP_MODE m) { }
        public void Release(EventInstanceState s) { }
        public void SetVolume(EventInstanceState s, float v) { }
        public void SetPaused(EventInstanceState s, bool p) { }
        public void Set3DAttributes(EventInstanceState s, in ATTRIBUTES_3D a) { }
        public void SetParameter(EventInstanceState s, string n, float v, bool i) { }
        public PLAYBACK_STATE? GetPlaybackState(EventInstanceState s) => null;
        public void SetBus(string p, float v, bool m, bool pa) { }
        public void SetVca(string p, float v) { }
        public void SetGlobalParameter(string n, float v) { }
        public void SetListener(int i, in ATTRIBUTES_3D a) { }
        public void Update() { }
        public bool? IsOneshot(string path) => !Looping.Contains(path);
        public bool? IsSnapshot(string path) => Snapshots.Contains(path);
    }

    [Fact]
    public void StartRestartStopAndSnapshotsAreRecordedInCallOrder()
    {
        var loop = RuntimeManager.CreateInstance("event:/SFX/Loops/Goal");
        loop.start();
        loop.start();                                    // FMOD restarts a playing instance
        loop.stop(STOP_MODE.ALLOWFADEOUT);
        loop.release();

        var snap = RuntimeManager.CreateInstance("snapshot:/Pause");
        snap.start();
        snap.stop(STOP_MODE.IMMEDIATE);

        var mixer = new AudioMixer { name = "Main_AudioMixer" };
        var paused = new AudioMixerSnapshot { name = "Paused" };
        mixer.AddSnapshot(paused);
        paused.TransitionTo(0.1f);

        Assert.Equal(new[]
        {
            "fmod:event:/SFX/Loops/Goal",
            "fmod:event:/SFX/Loops/Goal",
            "fmod-stop:event:/SFX/Loops/Goal|ALLOWFADEOUT",
            "fmod-snapshot:start:snapshot:/Pause",
            "fmod-snapshot:stop:snapshot:/Pause",
            "fmod-snapshot:start:mixer:Main_AudioMixer/Paused",
        }, _events);
        Assert.Equal(2, RuntimeManager.StartedByPath["event:/SFX/Loops/Goal"]);
    }

    [Fact]
    public void AGuidOnlyReferenceIsNamedFromTheBuildsGuidTable()
    {
        var file = Path.Combine(Path.GetTempPath(), "guids-" + Guid.NewGuid().ToString("N") + ".txt");
        try
        {
            File.WriteAllText(file, "{ddcf0e06-466d-4a76-a442-d98541b57b7e} bank:/Master\n{403f6740-1170-4180-b62b-63f6b1c0a2b2} event:/SFX/Test\n\nnot a line\n");
            Assert.Equal(2, FmodGuids.Load(file));
            var guid = FmodGuids.FromSystem(new Guid("403f6740-1170-4180-b62b-63f6b1c0a2b2"));
            Assert.Equal(0x403f6740, guid.Data1);

            var i = RuntimeManager.CreateInstance(new EventReference { Guid = guid });
            i.start();
            Assert.Equal(new[] { "fmod:event:/SFX/Test" }, _events);
            Assert.Equal("event:/SFX/Test", RuntimeManager.GetEventDescription(new EventReference { Guid = guid }).getPath(out var p) == RESULT.OK ? p : null);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void TheProjectsGuidTableNamesASerializedPrefabReference()
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(ThisFile())!);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Cosmic Shore", "Build", "GUIDs.txt"))) dir = dir.Parent;
        Assert.NotNull(dir);
        Assert.True(FmodGuids.Load(Path.Combine(dir!.FullName, "Cosmic Shore", "Build", "GUIDs.txt")) > 60);

        // TeamCrystal.prefab's emitter serializes this GUID beside Path: event:/SFX/Loops/Goal.
        var serialized = new GUID { Data1 = 1077877568, Data2 = 1099752816, Data3 = -161258058, Data4 = -163585613 };
        Assert.Equal("event:/SFX/Loops/Goal", FmodGuids.PathOf(serialized));
    }

    static string ThisFile([System.Runtime.CompilerServices.CallerFilePath] string path = "") => path;

    [Fact]
    public void DescriptionsComeFromTheRuntimeWhenOneIsInstalled()
    {
        var d = RuntimeManager.GetEventDescription("event:/SFX/Loops/Goal");
        d.isOneshot(out bool silentOneshot);
        Assert.True(silentOneshot);                      // the silent model cannot know

        var backend = new Descriptions();
        backend.Looping.Add("event:/SFX/Loops/Goal");
        backend.Snapshots.Add("event:/Mix/Calm");
        FmodBackend.Current = backend;
        d.isOneshot(out bool oneshot);
        Assert.False(oneshot);

        var calm = RuntimeManager.CreateInstance("event:/Mix/Calm");
        calm.start();
        Assert.Equal(new[] { "fmod-snapshot:start:event:/Mix/Calm" }, _events);
    }

    [Fact]
    public void AnEmitterDestroyedWithoutAStopTriggerLeavesItsLoopPlaying()
    {
        using var loop = new GameLoop();
        var backend = new Descriptions();
        backend.Looping.Add("event:/SFX/Loops/Goal");
        FmodBackend.Current = backend;
        var go = new GameObject("Crystal");
        var e = go.AddComponent<StudioEventEmitter>();
        e.EventReference = new EventReference { Path = "event:/SFX/Loops/Goal" };
        e.Play();
        CosmicShore.Engine.Object.DestroyImmediate(go);
        Assert.Equal(new[] { "fmod:event:/SFX/Loops/Goal" }, _events);   // no stop: the original only detaches
    }
}
