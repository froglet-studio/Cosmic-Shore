using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Prisma;
using Prisma.Workspace;

namespace CosmicShore.Tests;

/// <summary>
/// board.json has more than one writer: Prisma.exe keeps the board in memory, and every
/// prisma-mcp an agent runs loads, adds and saves on its own. These tests pin the two ways that
/// used to lose cards (a stale in-memory board saved over an agent's suggestion, and a board that
/// could not be read coming back empty and then being saved over), plus the format promises:
/// unknown fields and states survive, and an old board.json still loads.
/// </summary>
public class PrismaBoardStoreTests : IDisposable
{
    readonly string _dir = Path.Combine(Path.GetTempPath(), "prisma-board-" + Guid.NewGuid().ToString("N"));
    string File_ => PrismaBoard.FileIn(_dir);

    public PrismaBoardStoreTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { try { Directory.Delete(_dir, true); } catch (IOException) { } }

    static List<string> Titles(PrismaBoard b) => b.Items.Select(i => i.Title).OrderBy(t => t).ToList();

    // ------------------------------------------------------------------ bug 1: stale in-memory board

    [Fact]
    public void AnAgentSuggestion_SurvivesTheAppSavingItsOlderCopy()
    {
        var app = PrismaBoard.Load(_dir);            // Prisma.exe at startup
        app.Add(PrismaBoard.Kind.Task, "App card A");
        app.Save(_dir);

        var agent = PrismaBoard.Load(_dir);          // prisma_board_suggest: load, add, save
        var s = agent.Add(PrismaBoard.Kind.Task, "Agent suggestion", source: "agent", state: PrismaBoard.Status.Suggested, criterion: "a test passes");
        agent.Save(_dir);
        Assert.Equal("T-2", s.Id);

        app.Add(PrismaBoard.Kind.Task, "App card B");  // the app never re-read the file
        app.Save(_dir);

        var disk = PrismaBoard.Load(_dir);
        Assert.Equal(new[] { "Agent suggestion", "App card A", "App card B" }, Titles(disk));
        Assert.Equal(disk.Items.Count, disk.Items.Select(i => i.Id).Distinct().Count());
        Assert.Equal("T-2", disk.Items.Single(i => i.Title == "Agent suggestion").Id);  // the stored card keeps its key
        var b = disk.Items.Single(i => i.Title == "App card B");
        Assert.Equal("T-3", b.Id);                                                      // ours moved past it
        Assert.Contains(b.Notes, n => n.Contains("renumbered from T-2"));
        Assert.Contains(app.Items, i => i.Title == "Agent suggestion");                  // the app sees it too
        Assert.Equal(4, disk.NextTask);
    }

    [Fact]
    public void Refresh_PullsInAnotherWritersCards_WithoutWriting()
    {
        var app = PrismaBoard.Load(_dir);
        app.Add(PrismaBoard.Kind.Bug, "Crash in hangar");
        app.Save(_dir);
        Assert.False(app.Refresh(_dir));             // nothing changed since our own save

        var agent = PrismaBoard.Load(_dir);
        agent.Add(PrismaBoard.Kind.Task, "Suggested by agent", source: "agent", state: PrismaBoard.Status.Suggested);
        agent.Save(_dir);
        var written = File.ReadAllText(File_);

        Assert.True(app.Refresh(_dir));
        Assert.Contains(app.Items, i => i.Title == "Suggested by agent");
        Assert.Equal(written, File.ReadAllText(File_));
    }

    [Fact]
    public void EditsToDifferentCards_BothSurvive_AndTheAppsObjectsStayLive()
    {
        var seed = PrismaBoard.Load(_dir);
        seed.Add(PrismaBoard.Kind.Task, "One");
        seed.Add(PrismaBoard.Kind.Task, "Two");
        seed.Save(_dir);

        var app = PrismaBoard.Load(_dir);
        var agent = PrismaBoard.Load(_dir);
        var appTwo = app.Items.Single(i => i.Title == "Two");

        app.Move(app.Items.Single(i => i.Title == "One"), PrismaBoard.Status.Doing, "started");
        agent.Move(agent.Items.Single(i => i.Title == "Two"), PrismaBoard.Status.Done, "done elsewhere");
        agent.Save(_dir);
        app.Save(_dir);

        var disk = PrismaBoard.Load(_dir);
        Assert.Equal(PrismaBoard.Status.Doing, disk.Items.Single(i => i.Title == "One").State);
        Assert.Equal(PrismaBoard.Status.Done, disk.Items.Single(i => i.Title == "Two").State);
        Assert.Equal(PrismaBoard.Status.Done, appTwo.State);   // the object the UI holds was updated in place
        Assert.Same(appTwo, app.Items.Single(i => i.Title == "Two"));
    }

    [Fact]
    public void BothSidesEditTheSameCard_TheLaterEditWins()
    {
        var seed = PrismaBoard.Load(_dir);
        seed.Add(PrismaBoard.Kind.Task, "Shared");
        seed.Save(_dir);
        var a = PrismaBoard.Load(_dir);
        var b = PrismaBoard.Load(_dir);

        var ca = a.Items[0]; ca.Title = "A's title"; ca.Updated = DateTime.UtcNow.AddMinutes(-1);
        var cb = b.Items[0]; cb.Title = "B's title"; cb.Updated = DateTime.UtcNow;
        b.Save(_dir);
        a.Save(_dir);   // a saves last but edited first

        Assert.Equal("B's title", PrismaBoard.Load(_dir).Items.Single().Title);
    }

    [Fact]
    public void ACardTheOtherWriterDeleted_StaysDeleted_UnlessWeEditedIt()
    {
        var seed = PrismaBoard.Load(_dir);
        seed.Add(PrismaBoard.Kind.Task, "Gone");
        seed.Add(PrismaBoard.Kind.Task, "Kept");
        seed.Save(_dir);
        var app = PrismaBoard.Load(_dir);
        var other = PrismaBoard.Load(_dir);
        other.Items.Clear();
        other.Save(_dir);

        app.Items.Single(i => i.Title == "Kept").Title = "Kept (edited)";
        app.Save(_dir);

        Assert.Equal(new[] { "Kept (edited)" }, Titles(PrismaBoard.Load(_dir)));
    }

    [Fact]
    public void ManyWritersAtOnce_LoseNothing_AndNeverShareAKey()
    {
        const int writers = 4, each = 15;
        Parallel.For(0, writers, w =>
        {
            for (int n = 0; n < each; n++)
            {
                var b = PrismaBoard.Load(_dir);
                b.Add(PrismaBoard.Kind.Task, $"w{w}-{n}");
                b.Save(_dir);
            }
        });
        var disk = PrismaBoard.Load(_dir);
        Assert.Equal(writers * each, disk.Items.Count);
        Assert.Equal(writers * each, disk.Items.Select(i => i.Id).Distinct().Count());
        Assert.Equal(writers * each, disk.Items.Select(i => i.Uid).Distinct().Count());
        Assert.Empty(Directory.GetFiles(_dir, "*.tmp"));
    }

    // ------------------------------------------------------------------ bug 2: unreadable board

    [Fact]
    public void AnUnreadableBoard_IsCopiedAside_AndNeverWiped()
    {
        const string garbage = "{ \"Items\": [ { \"Id\": \"T-1\", \"Title\": \"half a file";
        File.WriteAllText(File_, garbage);

        var b = PrismaBoard.Load(_dir);
        Assert.NotNull(b.LoadError);
        Assert.Empty(b.Items);
        Assert.NotNull(b.Preserved);
        Assert.Equal(garbage, File.ReadAllText(b.Preserved!));

        b.Add(PrismaBoard.Kind.Task, "New after the failure");
        b.Save(_dir);
        Assert.Equal(garbage, File.ReadAllText(b.Preserved!));                // the copy is untouched
        Assert.Single(Directory.GetFiles(_dir, "board.json.corrupt-*"));        // and made once, not again on save
        Assert.Equal("New after the failure", PrismaBoard.Load(_dir).Items.Single().Title);

        var again = PrismaBoard.Load(_dir);                                   // the old silent path would have returned empty here
        Assert.Null(again.LoadError);
    }

    [Fact]
    public void ABoardThatTurnsUnreadable_BeforeASave_IsCopiedAsideFirst()
    {
        var app = PrismaBoard.Load(_dir);
        app.Add(PrismaBoard.Kind.Task, "Mine");
        app.Save(_dir);
        File.WriteAllText(File_, "not json at all");                          // something else broke the file

        app.Add(PrismaBoard.Kind.Task, "Mine too");
        app.Save(_dir);

        var copy = Assert.Single(Directory.GetFiles(_dir, "board.json.corrupt-*"));
        Assert.Equal("not json at all", File.ReadAllText(copy));
        Assert.Equal(new[] { "Mine", "Mine too" }, Titles(PrismaBoard.Load(_dir)));
    }

    [Fact]
    public void AMissingBoard_IsEmpty_WithoutAnError()
    {
        var b = PrismaBoard.Load(_dir);
        Assert.Null(b.LoadError);
        Assert.Empty(b.Items);
        Assert.False(File.Exists(File_));
    }

    // ------------------------------------------------------------------ format

    [Fact]
    public void UnknownFieldsAndStates_FromANewerBuild_RoundTrip()
    {
        File.WriteAllText(File_, """
        {
          "Schema": 7,
          "Workspace": { "id": "froglet", "members": ["yash"] },
          "Items": [
            { "Id": "T-1", "Uid": "u1", "Type": "Note", "Title": "From the future", "State": "Blocked",
              "Mood": "happy", "Reactions": [1, 2, 3], "Created": "2026-10-01T10:00:00Z", "Updated": "2026-10-01T10:00:00Z" },
            { "Id": "T-2", "Uid": "u2", "Type": "Task", "Title": "Today's card", "State": "Todo",
              "Created": "2026-10-01T10:00:00Z", "Updated": "2026-10-01T10:00:00Z" }
          ],
          "NextBug": 1,
          "NextTask": 3
        }
        """);
        var b = PrismaBoard.Load(_dir);
        Assert.Null(b.LoadError);
        var future = b.Items.Single(i => i.Uid == "u1");
        Assert.Equal(PrismaBoard.Status.Todo, future.State);       // shown, not hidden
        Assert.False(future.StateKnown);
        Assert.Equal(PrismaBoard.Kind.Task, future.Type);

        b.Move(b.Items.Single(i => i.Uid == "u2"), PrismaBoard.Status.Doing);  // a save for another reason
        b.Save(_dir);

        var root = JsonDocument.Parse(File.ReadAllText(File_)).RootElement;
        Assert.Equal("froglet", root.GetProperty("Workspace").GetProperty("id").GetString());
        Assert.Equal(7, root.GetProperty("Schema").GetInt32());
        var stored = root.GetProperty("Items").EnumerateArray().Single(e => e.GetProperty("Uid").GetString() == "u1");
        Assert.Equal("Blocked", stored.GetProperty("State").GetString());
        Assert.Equal("Note", stored.GetProperty("Type").GetString());
        Assert.Equal("happy", stored.GetProperty("Mood").GetString());
        Assert.Equal(3, stored.GetProperty("Reactions").GetArrayLength());

        // Moving the unknown-state card is the user's choice; then it takes a state this build knows.
        var again = PrismaBoard.Load(_dir);
        again.Move(again.Items.Single(i => i.Uid == "u1"), PrismaBoard.Status.Done);
        again.Save(_dir);
        Assert.Contains("\"Done\"", File.ReadAllText(File_));
        Assert.Contains("\"Mood\": \"happy\"", File.ReadAllText(File_));
    }

    /// <summary>board.json exactly as the builds before this change wrote it.</summary>
    const string OldBoard = """
    {
      "Items": [
        {
          "Id": "B-1", "Type": "Bug", "Title": "NullReferenceException in ScoreTracker", "Detail": "seen in 3 runs",
          "Source": "tracks", "State": "Doing", "Priority": 1,
          "Created": "2026-10-02T08:00:00Z", "Updated": "2026-10-03T09:30:00Z",
          "IssueKey": "exception|ScoreTracker", "Milestone": null,
          "Notes": [ "2026-10-03 09:30 accepted" ],
          "Criterion": "Not seen again in 3 runs through Menu_Main (Prisma checks this after every run)",
          "CriterionMet": null
        },
        {
          "Id": "T-1", "Type": "Task", "Title": "Write the board doc", "Detail": "",
          "Source": "user", "State": 3, "Priority": 2,
          "Created": "2026-10-04T08:00:00Z", "Updated": "2026-10-04T08:00:00Z",
          "IssueKey": null, "Milestone": null, "Notes": [], "Criterion": "", "CriterionMet": "2026-10-05T10:00:00Z"
        }
      ],
      "NextBug": 2,
      "NextTask": 2
    }
    """;

    [Fact]
    public void AnOldBoard_Loads_GetsStableUids_AndKeepsEveryField()
    {
        File.WriteAllText(File_, OldBoard);
        var a = PrismaBoard.Load(_dir);
        var b = PrismaBoard.Load(_dir);
        Assert.Null(a.LoadError);
        Assert.Equal(2, a.Items.Count);
        Assert.All(a.Items, i => Assert.False(string.IsNullOrEmpty(i.Uid)));
        Assert.Equal(a.Items.Select(i => i.Uid), b.Items.Select(i => i.Uid));    // every reader derives the same Uid

        var bug = a.Items.Single(i => i.Id == "B-1");
        Assert.Equal(PrismaBoard.Kind.Bug, bug.Type);
        Assert.Equal(PrismaBoard.Status.Doing, bug.State);
        Assert.Equal(1, bug.Priority);
        Assert.Equal("exception|ScoreTracker", bug.IssueKey);
        Assert.Single(bug.Notes);
        Assert.Null(bug.Scheduled);
        Assert.Empty(bug.Tags);
        var task = a.Items.Single(i => i.Id == "T-1");
        Assert.Equal(PrismaBoard.Status.Done, task.State);                        // a numeric state reads too
        Assert.NotNull(task.CriterionMet);

        // Two writers on the old file: their backfilled cards are the same cards, not duplicates.
        a.Add(PrismaBoard.Kind.Task, "From a");
        b.Add(PrismaBoard.Kind.Task, "From b");
        a.Save(_dir);
        b.Save(_dir);
        var disk = PrismaBoard.Load(_dir);
        Assert.Equal(4, disk.Items.Count);
        Assert.Equal(PrismaBoard.CurrentSchema, disk.Schema);
        Assert.Equal(new[] { "B-1", "T-1", "T-2", "T-3" }, disk.Items.Select(i => i.Id).OrderBy(x => x));
    }

    // The model of the builds before this change, to prove they can still read what this one writes.
    sealed class OldItem
    {
        public string Id { get; set; } = "";
        public PrismaBoard.Kind Type { get; set; }
        public string Title { get; set; } = "";
        public PrismaBoard.Status State { get; set; } = PrismaBoard.Status.Todo;
        public int Priority { get; set; } = 2;
        public List<string> Notes { get; set; } = new();
        public string Criterion { get; set; } = "";
    }
    sealed class OldBoardModel { public List<OldItem> Items { get; set; } = new(); public int NextBug { get; set; } = 1; public int NextTask { get; set; } = 1; }

    [Fact]
    public void WhatThisBuildWrites_StillReadsInTheOldBuildsModel()
    {
        var b = PrismaBoard.Load(_dir);
        var it = BoardCommands.AddFromQuickAdd(b, "Fix trail tomorrow 3pm #vfx !1 ~45m", new DateTime(2026, 10, 9, 10, 0, 0))!;
        it.Checklist.Add(new PrismaBoard.ChecklistEntry { Text = "repro" });
        b.Add(PrismaBoard.Kind.Bug, "Plain bug");
        b.Save(_dir);

        var old = JsonSerializer.Deserialize<OldBoardModel>(File.ReadAllText(File_),
            new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } })!;
        Assert.Equal(2, old.Items.Count);
        Assert.Equal("Fix trail", old.Items[0].Title);
        Assert.Equal(1, old.Items[0].Priority);
        Assert.Equal(PrismaBoard.Kind.Bug, old.Items[1].Type);
        Assert.Equal(2, old.NextTask);
        var text = File.ReadAllText(File_);
        Assert.DoesNotContain("\"Due\"", text);           // unused fields are left out, not written as null
        Assert.Contains("\"Scheduled\": \"2026-10-10\"", text);
    }

    [Fact]
    public void TheMemoryStore_BehavesLikeTheFile()
    {
        var store = new MemoryBoardStore();
        var app = PrismaBoard.Load(store);
        app.Add(PrismaBoard.Kind.Task, "App");
        app.Save(store);
        var agent = PrismaBoard.Load(store);
        agent.Add(PrismaBoard.Kind.Task, "Agent");
        agent.Save(store);
        app.Add(PrismaBoard.Kind.Task, "App 2");
        app.Save(store);
        Assert.Equal(new[] { "Agent", "App", "App 2" }, Titles(PrismaBoard.Load(store)));

        store.Replace("garbage");
        var broken = PrismaBoard.Load(store);
        Assert.NotNull(broken.LoadError);
        Assert.Equal("garbage", Assert.Single(store.Preserved));
    }
}
