using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CosmicShore.Engine;
using CosmicShore.Engine.Audio;
using CosmicShore.Engine.Audio.Fmod;

namespace CosmicShore.Tests;

/// <summary>
/// ROADMAP C1 / C4: an event no loaded bank carries does not start in Prisma, as it does not in
/// Unity (FMOD's RuntimeManager throws EventNotFoundException, FmodSafe reports once and plays
/// nothing). Which events the banks carry is read from the strings bank's own GUID index, because
/// GUIDs.txt is only as current as the last manual export (measured 2026-10-10: the Bootstrap
/// music {03de9ea9-9b51-400a-b0cb-8bcc12a12697} is in Master.strings.bank and not in GUIDs.txt).
/// </summary>
public class FmodBankGateTests : IDisposable
{
    readonly List<string> _events = new();
    readonly CapturingLogSink _log = new();
    readonly ILogSink _previousSink;
    readonly List<string> _files = new();

    public FmodBankGateTests()
    {
        RuntimeManager.ResetForTests();
        FmodGuids.Clear();
        AudioStats.Missing.Clear();
        RuntimeManager.EventRecorded = (kind, name) => _events.Add(kind + ":" + name);
        _previousSink = Debug.Sink;
        Debug.Sink = _log;
    }

    public void Dispose()
    {
        Debug.Sink = _previousSink;
        RuntimeManager.EventRecorded = null;
        FmodBackend.Current = null;
        FmodGuids.Clear();
        RuntimeManager.ResetForTests();
        foreach (var f in _files) File.Delete(f);
    }

    IEnumerable<string> Warnings => _log.Entries.Where(e => e.Type == LogType.Warning).Select(e => e.Message);

    static readonly Guid Known = new("403f6740-1170-4180-b62b-63f6b1c0a2b2");
    static readonly Guid Stale = new("03de9ea9-9b51-400a-b0cb-8bcc12a12697");
    static readonly Guid Retired = new("73e86f10-09e1-45ee-b1ee-9b901a807110");

    /// <summary>A strings bank's GUID index around the given GUIDs: random bytes, the sorted array, random bytes.</summary>
    static byte[] SyntheticStringsBank(IEnumerable<Guid> carried, bool leadingTableTail = true)
    {
        var rng = new System.Random(7);
        var bytes = new List<byte>();
        var noise = new byte[200]; rng.NextBytes(noise); bytes.AddRange(noise);
        var guids = new List<Guid>(carried);
        // Pad to well past the minimum with v4 GUIDs that sort after the given ones.
        for (int i = 0; i < 12; i++)
        {
            var b = new byte[16]; rng.NextBytes(b);
            b[3] = (byte)(0xF0 | (i & 0x0F)); b[7] = (byte)(0x40 | (b[7] & 0x0F)); b[8] = (byte)(0x80 | (b[8] & 0x3F));
            guids.Add(new Guid(b));
        }
        guids.Sort((a, b) => BitConverter.ToUInt32(a.ToByteArray(), 0).CompareTo(BitConverter.ToUInt32(b.ToByteArray(), 0)));
        // The slot before the array in a real bank: the tail of an offset table, Data1 zero, no version nibble.
        if (leadingTableTail) bytes.AddRange(new byte[] { 0, 0, 0, 0, 0xe7, 0x03, 0x00, 0x73, 0x18, 0, 0, 0, 0xb3, 0, 0x10, 0 });
        foreach (var g in guids) bytes.AddRange(g.ToByteArray());
        var tail = new byte[300]; rng.NextBytes(tail); bytes.AddRange(tail);
        return bytes.ToArray();
    }

    string TempFile(string name, byte[] content)
    {
        var path = Path.Combine(Path.GetTempPath(), "bankgate-" + Guid.NewGuid().ToString("N") + "-" + name);
        File.WriteAllBytes(path, content);
        _files.Add(path);
        return path;
    }

    /// <summary>GUIDs.txt naming Known and Retired; a strings bank carrying Known but neither Stale nor Retired.</summary>
    void LoadSyntheticBuild()
    {
        Assert.Equal(2, FmodGuids.Load(TempFile("GUIDs.txt", System.Text.Encoding.ASCII.GetBytes(
            $"{{{Known}}} event:/SFX/Known\n{{{Retired}}} event:/SFX/Loops/Retired\n"))));
        Assert.True(FmodGuids.LoadStringsBank(TempFile("Master.strings.bank", SyntheticStringsBank(new[] { Known }))) >= 13);
    }

    static EventReference Ref(Guid g, string path = null) => new() { Guid = FmodGuids.FromSystem(g), Path = path };

    [Fact]
    public void TheGuidIndexIsTheSortedArrayAndNotTheTableTailAroundIt()
    {
        var carried = new[] { Known, Stale };
        var index = FmodGuids.ReadGuidIndex(SyntheticStringsBank(carried));
        Assert.Equal(14, index.Count);
        Assert.Contains(FmodGuids.FromSystem(Known), index);
        Assert.Contains(FmodGuids.FromSystem(Stale), index);
        Assert.DoesNotContain(new GUID { Data2 = 0x7300_03e7, Data3 = 0x18, Data4 = 0x0010_00b3 }, index);
        Assert.Equal(14, FmodGuids.ReadGuidIndex(SyntheticStringsBank(carried, leadingTableTail: false)).Count);
        Assert.Empty(FmodGuids.ReadGuidIndex(new byte[64]));           // too short to be an index
        Assert.Equal(Known, FmodGuids.ToSystem(FmodGuids.FromSystem(Known)));
    }

    [Fact]
    public void TheProjectsStringsBankIndexesWhatItsGuidTableNamesAndMore()
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(ThisFile())!);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Cosmic Shore", "Build", "GUIDs.txt"))) dir = dir.Parent;
        Assert.NotNull(dir);
        string build = Path.Combine(dir!.FullName, "Cosmic Shore", "Build");
        string bank = Path.Combine(build, "Desktop", "Master.strings.bank");
        Assert.True(FmodGuids.Load(Path.Combine(build, "GUIDs.txt")) > 60);
        int indexed = FmodGuids.LoadStringsBank(bank);
        Assert.True(indexed >= 60, $"{indexed} GUIDs indexed");
        var (listed, carried) = FmodGuids.StringsBankCoverage();
        // Every GUIDs.txt entry but the retired ones is in the bank: the index is read right.
        Assert.True(carried * 10 >= listed * 9, $"{carried} of {listed} GUIDs.txt entries carried");
        // The parser agrees with the raw bytes about the two references the harness turned on.
        var raw = File.ReadAllBytes(bank);
        foreach (var g in new[] { Stale, Retired, Known })
        {
            bool inFile = raw.AsSpan().IndexOf(g.ToByteArray()) >= 0;
            Assert.Equal(inFile, FmodGuids.BankCarries(FmodGuids.FromSystem(g)));
        }
    }

    static string ThisFile([System.Runtime.CompilerServices.CallerFilePath] string path = "") => path;

    [Fact]
    public void AReferenceNoLoadedBankCarriesDoesNotStartAndIsReportedOnce()
    {
        LoadSyntheticBuild();
        var stale = Ref(Stale, "event:/Music/Music");

        var ex = Assert.Throws<EventNotFoundException>(() => RuntimeManager.CreateInstance(stale));
        Assert.Equal("event:/Music/Music", ex.Path);
        Assert.Equal(FmodGuids.FromSystem(Stale), ex.Guid);
        Assert.Contains("Event not found", ex.Message);
        Assert.Throws<EventNotFoundException>(() => RuntimeManager.CreateInstance(stale));   // a retrying caller
        Assert.Throws<EventNotFoundException>(() => RuntimeManager.GetEventDescription(stale));

        Assert.Empty(_events);                                   // no "fmod" line, nothing started
        Assert.Equal(0, RuntimeManager.StartedTotal);
        var warning = Assert.Single(Warnings);                   // once per GUID, naming the stale path
        Assert.Contains("{03de9ea9-9b51-400a-b0cb-8bcc12a12697}", warning);
        Assert.Contains("'event:/Music/Music'", warning);
        Assert.Contains("Master.strings.bank", warning);
        Assert.Contains("event:/Music/Music", AudioStats.Missing);
    }

    [Fact]
    public void AReferenceTheBankCarriesStartsAndIsNamedFromTheTableOrItsOwnPath()
    {
        LoadSyntheticBuild();
        RuntimeManager.CreateInstance(Ref(Known, "event:/SFX/Known")).start();
        RuntimeManager.CreateInstance(Ref(Known)).start();                         // GUID-only: named by GUIDs.txt
        RuntimeManager.GetEventDescription(Ref(Known)).getPath(out var path);
        Assert.Equal("event:/SFX/Known", path);
        Assert.Equal(new[] { "fmod:event:/SFX/Known", "fmod:event:/SFX/Known" }, _events);
        Assert.Empty(Warnings);
    }

    [Fact]
    public void APathOnlyReferenceIsRefusedOnlyThroughAGuidTheTableKnows()
    {
        LoadSyntheticBuild();
        // GUIDs.txt maps this path to a GUID the bank no longer carries: refused, as getEvent(path) would be.
        Assert.Throws<EventNotFoundException>(() => RuntimeManager.CreateInstance("event:/SFX/Loops/Retired"));
        Assert.Contains(Warnings, w => w.Contains("'event:/SFX/Loops/Retired'"));
        // A path the table does not know cannot be judged from the file: it plays, as before.
        RuntimeManager.CreateInstance("event:/SFX/Unlisted").start();
        Assert.Equal(new[] { "fmod:event:/SFX/Unlisted" }, _events);
    }

    [Fact]
    public void WithNoStringsBankNothingIsRefused()
    {
        Assert.Equal(1, FmodGuids.Load(TempFile("GUIDs.txt", System.Text.Encoding.ASCII.GetBytes($"{{{Known}}} event:/SFX/Known\n"))));
        Assert.False(FmodGuids.StringsBankLoaded);
        Assert.Null(FmodGuids.BankCarries(Ref(Stale, "event:/Music/Music")));
        RuntimeManager.CreateInstance(Ref(Stale, "event:/Music/Music")).start();   // the lenient model of before
        Assert.Equal(new[] { "fmod:event:/Music/Music" }, _events);
        Assert.Empty(Warnings);
    }

    sealed class Banks : IFmodBackend
    {
        public readonly HashSet<GUID> Carried = new();
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
        public bool? HasEvent(EventReference r) => Carried.Contains(r.Guid);
    }

    [Fact]
    public void TheRuntimesOwnAnswerOutranksTheStringsBankIndex()
    {
        LoadSyntheticBuild();
        var banks = new Banks();
        banks.Carried.Add(FmodGuids.FromSystem(Stale));                 // a newer bank build than the file on disk
        FmodBackend.Current = banks;
        RuntimeManager.CreateInstance(Ref(Stale, "event:/Music/Music")).start();
        Assert.Throws<EventNotFoundException>(() => RuntimeManager.CreateInstance(Ref(Known, "event:/SFX/Known")));
        Assert.Equal(new[] { "fmod:event:/Music/Music" }, _events);
        Assert.Contains(Warnings, w => w.Contains("the loaded banks") && w.Contains("'event:/SFX/Known'"));
    }
}
