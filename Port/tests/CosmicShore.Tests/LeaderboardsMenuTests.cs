using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Engine.UI;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;

namespace CosmicShore.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// Leaderboards unit (2026-07-10; re-synced 2026-10-13 when the port followed
// upstream in retiring the backend this screen used to read) — the ported
// LeaderboardsMenu. Upstream's screen has no backend now: every fetch clears the
// board and renders its empty state. Covers: the screen flow (OnScreenEnter →
// game buttons → dropdown options with the >1-vessel "Any" rule → the fetch
// leaves the board empty), game switching, and the row renderer the next
// backend will feed (1-based rank, nameless-pilot fallback, golf display
// sign-flip, the local-player highlight by the UGS player id, and no highlight
// for a row without a player id). The renderer is reached through its private
// PopulateGameHighScores, because upstream no longer has a public path that
// puts rows on the board.
// ─────────────────────────────────────────────────────────────────────────────

public class LeaderboardsMenuTests : IDisposable
{
    readonly GameLoop loop;

    readonly LeaderboardsMenu screen;
    readonly TMP_Dropdown dropdown;
    readonly Transform gameRow;
    readonly GameObject board;
    readonly SO_ArcadeGame raceGame;   // 2 vessels → "Any" option; normal scoring
    readonly SO_ArcadeGame joustGame;  // 1 vessel → no "Any"; golf scoring

    const string LocalPlayerId = "pilot-local-test";

    public LeaderboardsMenuTests()
    {
        loop = new GameLoop(nameof(LeaderboardsMenuTests));

        SO_Vessel MakeVessel(string name, VesselClassType cls)
        {
            var vessel = ScriptableObject.CreateInstance<SO_Vessel>();
            vessel.Name = name;
            vessel.Class = cls;
            return vessel;
        }

        raceGame = ScriptableObject.CreateInstance<SO_ArcadeGame>();
        raceGame.DisplayName = "ALPHA RACE";
        raceGame.Mode = GameModes.HexRace;
        raceGame.Vessels = new List<SO_Vessel>
        {
            MakeVessel("Dolphin", VesselClassType.Dolphin),
            MakeVessel("Manta", VesselClassType.Manta),
        };

        joustGame = ScriptableObject.CreateInstance<SO_ArcadeGame>();
        joustGame.DisplayName = "BETA JOUST";
        joustGame.Mode = GameModes.MultiplayerJoust;
        joustGame.GolfScoring = true;
        joustGame.Vessels = new List<SO_Vessel> { MakeVessel("Rhino", VesselClassType.Rhino) };

        var gameList = ScriptableObject.CreateInstance<SO_GameList>();
        gameList.Games = new List<SO_ArcadeGame> { raceGame, joustGame };

        // The [Inject]ed UGS auth variable: the highlight reads its PlayerId.
        var authData = ScriptableObject.CreateInstance<AuthenticationDataVariable>();
        authData.Value = new AuthenticationData { PlayerId = LocalPlayerId };

        // Wire-then-activate rig in the menushell's shape.
        var panelGo = new GameObject("PortPanel", typeof(RectTransform));
        panelGo.SetActive(false);

        var rowGo = new GameObject("GameSelectionContainer", typeof(RectTransform));
        rowGo.transform.SetParent(panelGo.transform, false);
        gameRow = rowGo.transform;
        for (int i = 0; i < 3; i++)
        {
            var slot = new GameObject($"GameSelect_{i}", typeof(RectTransform));
            slot.transform.SetParent(gameRow, false);
            slot.AddComponent<Image>();
            slot.AddComponent<Button>().transition = Selectable.Transition.None;
        }

        board = new GameObject("HighScoresContainer", typeof(RectTransform));
        board.transform.SetParent(panelGo.transform, false);
        for (int i = 0; i < 6; i++)
        {
            var row = new GameObject($"ScoreRow_{i}", typeof(RectTransform));
            row.transform.SetParent(board.transform, false);
            for (int c = 0; c < 3; c++)
            {
                var cell = new GameObject(c == 0 ? "Rank" : c == 1 ? "Pilot" : "Score", typeof(RectTransform));
                cell.transform.SetParent(row.transform, false);
                cell.AddComponent<TextMeshProUGUI>();
            }
        }

        var dropdownGo = new GameObject("ShipClassSelection", typeof(RectTransform));
        dropdownGo.transform.SetParent(panelGo.transform, false);
        dropdown = dropdownGo.AddComponent<TMP_Dropdown>();
        dropdown.transition = Selectable.Transition.None;

        panelGo.AddComponent<MenuAudio>(); // [RequireComponent] partner
        screen = panelGo.AddComponent<LeaderboardsMenu>();
        SetField(screen, "allGames", gameList);
        SetField(screen, "authenticationDataVariable", authData);
        SetField(screen, "GameSelectionContainer", gameRow);
        SetField(screen, "HighScoresContainer", board);
        SetField(screen, "ShipClassSelection", dropdown);

        panelGo.SetActive(true);
        loop.Tick(1f / 60f); // Start
    }

    public void Dispose() => loop.Dispose();

    void Tick(int frames = 1)
    {
        for (int i = 0; i < frames; i++)
            loop.Tick(1f / 60f);
    }

    void EnterAndSettle()
    {
        screen.OnScreenEnter();
        Tick(3); // SelectGameCoroutine + SelectShipTypeCoroutine (both end-of-frame)
    }

    TMP_Text Cell(int row, int column)
        => board.transform.GetChild(row).GetChild(column).GetComponent<TMP_Text>();

    bool RowActive(int row) => board.transform.GetChild(row).gameObject.activeSelf;

    /// <summary>Puts rows on the board through the screen's own renderer (see the header).</summary>
    void Render(params LeaderboardsMenu.LeaderboardEntry[] rows)
    {
        var entries = (List<LeaderboardsMenu.LeaderboardEntry>)typeof(LeaderboardsMenu)
            .GetField("LeaderboardEntriesV2", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(screen)!;
        entries.Clear();
        entries.AddRange(rows);
        typeof(LeaderboardsMenu)
            .GetMethod("PopulateGameHighScores", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(screen, null);
    }

    [Fact]
    public void ScreenEnter_PopulatesTheSelectors_AndLeavesTheBoardEmpty()
    {
        // Rows left on from an earlier render must be cleared by the fetch.
        Render(new LeaderboardsMenu.LeaderboardEntry("VELA", "pilot-vela", 130, 0, null));
        Assert.True(RowActive(0));

        EnterAndSettle();

        // Game 0 selected → 2 vessels → the "Any" option leads the dropdown.
        Assert.Equal(new[] { "Any", "Dolphin", "Manta" },
            dropdown.options.Select(o => o.text).ToArray());

        // No backend: the fetch clears the list and the board shows its empty state.
        for (int i = 0; i < board.transform.childCount; i++)
            Assert.False(RowActive(i));
    }

    [Fact]
    public void SelectGame_SwitchesTheDropdown_AndTheBoardStaysEmpty()
    {
        EnterAndSettle();

        screen.SelectGame(1);
        Tick(2); // SelectShipTypeCoroutine (end-of-frame)

        // One vessel → no "Any".
        Assert.Equal(new[] { "Rhino" }, dropdown.options.Select(o => o.text).ToArray());
        for (int i = 0; i < board.transform.childCount; i++)
            Assert.False(RowActive(i));
    }

    [Fact]
    public void RowRenderer_RanksNamesScores_AndHighlightsTheUgsPlayer()
    {
        EnterAndSettle();

        Render(
            new LeaderboardsMenu.LeaderboardEntry("VELA", "pilot-vela", 130, 0, null),
            new LeaderboardsMenu.LeaderboardEntry("", "pilot-anon", 118, 1, null),
            new LeaderboardsMenu.LeaderboardEntry("YOU", LocalPlayerId, 104, 2, null),
            new LeaderboardsMenu.LeaderboardEntry("GHOST", null, 90, 3, null));

        // Rank is 1-based off Position; rows beyond the list stay off.
        Assert.Equal("1", Cell(0, 0).text);
        Assert.Equal("VELA", Cell(0, 1).text);
        Assert.Equal("130", Cell(0, 2).text);
        Assert.False(RowActive(4));

        // Nameless fallback shrinks the font.
        Assert.Equal("[NAMELESS PILOT]", Cell(1, 1).text);
        Assert.Equal(14, Cell(1, 1).fontSize);
        Assert.Equal(18, Cell(0, 1).fontSize);

        // The local pilot's row (matched on the UGS player id) is cyan; others white,
        // including a row with no player id at all.
        Assert.Equal(new Color(.1f, .7f, .7f), Cell(2, 1).color);
        Assert.Equal(Color.white, Cell(0, 1).color);
        Assert.Equal(Color.white, Cell(3, 1).color);
    }

    [Fact]
    public void RowRenderer_FlipsGolfScoresForDisplay()
    {
        EnterAndSettle();
        screen.SelectGame(1); // the golf-scored game
        Tick(2);

        // Golf scores are stored negated; the display flips them positive.
        Render(new LeaderboardsMenu.LeaderboardEntry("CRUX", "pilot-crux", -42, 0, null));
        Assert.Equal("CRUX", Cell(0, 1).text);
        Assert.Equal("42", Cell(0, 2).text);
        Assert.False(RowActive(1));
    }

    static void SetField(object target, string name, object value)
    {
        FieldInfo field = null;
        for (var t = target.GetType(); t != null && field == null; t = t.BaseType)
            field = t.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
        (field ?? throw new MissingFieldException(target.GetType().Name, name)).SetValue(target, value);
    }
}
