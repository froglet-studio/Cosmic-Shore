using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Engine.Soap;
using CosmicShore.Engine.UI;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;

namespace CosmicShore.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// Leaderboards unit (2026-07-10) — the ported LeaderboardsMenu + the offline
// lane of the legacy LeaderboardManager (upstream runs its "[PLAYFAB DISABLED]"
// state, so the offline lane IS the live behavior). Covers: the screen flow
// (OnScreenEnter → game buttons → dropdown options with the >1-vessel "Any"
// rule → cached fetch → high-score rows with rank/name/score), the nameless-
// pilot fallback, the local-player highlight by PlayFabAccount.ID, the golf
// display sign-flip, game switching, the OnProfileLoaded refresh, the stat-key
// format, and offline stat accumulation (mode+vessel / mode+ANY / PlayCount).
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
        ClearManagerInstance();
        AuthenticationManager.PlayFabAccount.ID = LocalPlayerId;

        // The offline manager singleton (upstream: scene object with the network
        // variable serialized in; events wired only in the disabled Start).
        var managerGo = new GameObject("LeaderboardManager");
        managerGo.SetActive(false);
        var manager = managerGo.AddComponent<LeaderboardManager>();
        var netVariable = ScriptableObject.CreateInstance<NetworkMonitorDataVariable>();
        netVariable.Value = new NetworkMonitorData
        {
            OnNetworkFound = ScriptableObject.CreateInstance<ScriptableEventNoParam>(),
            OnNetworkLost = ScriptableObject.CreateInstance<ScriptableEventNoParam>(),
        };
        SetField(manager, "_networkMonitorDataVariable", netVariable);
        managerGo.SetActive(true);

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

        // Cached boards (the offline fetch source). HEXRACE_ANY carries the
        // nameless + local-player rows; MULTIPLAYERJOUST_RHINO carries golf
        // scores stored negated (the report-side flip) for the display flip.
        DataAccessor.Save("leaderboard_HEXRACE_ANY.data", new List<LeaderboardManager.LeaderboardEntry>
        {
            new("VELA", "pilot-vela", 130, 0, null),
            new("", "pilot-anon", 118, 1, null),
            new("YOU", LocalPlayerId, 104, 2, null),
        });
        DataAccessor.Save("leaderboard_MULTIPLAYERJOUST_RHINO.data", new List<LeaderboardManager.LeaderboardEntry>
        {
            new("CRUX", "pilot-crux", -42, 0, null),
        });

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
        SetField(screen, "GameSelectionContainer", gameRow);
        SetField(screen, "HighScoresContainer", board);
        SetField(screen, "ShipClassSelection", dropdown);

        panelGo.SetActive(true);
        loop.Tick(1f / 60f); // Start
    }

    public void Dispose()
    {
        loop.Dispose();
        ClearManagerInstance();
        AuthenticationManager.PlayFabAccount.ID = null;
    }

    static void ClearManagerInstance()
        => typeof(SingletonPersistent<LeaderboardManager>)
            .GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)!
            .SetValue(null, null);

    void Tick(int frames = 1)
    {
        for (int i = 0; i < frames; i++)
            loop.Tick(1f / 60f);
    }

    void EnterAndSettle()
    {
        screen.OnScreenEnter();
        Tick(3); // SelectGameCoroutine (end-of-frame) + SelectShipTypeCoroutine (WaitUntil)
    }

    TMP_Text Cell(int row, int column)
        => board.transform.GetChild(row).GetChild(column).GetComponent<TMP_Text>();

    [Fact]
    public void ScreenEnter_PopulatesBoard_FromCachedOfflineLeaderboard()
    {
        EnterAndSettle();

        // Game 0 selected → 2 vessels → the "Any" option leads the dropdown.
        Assert.Equal(new[] { "Any", "Dolphin", "Manta" },
            dropdown.options.Select(o => o.text).ToArray());

        // Three cached rows shown, rank is 1-based off Position.
        Assert.True(board.transform.GetChild(0).gameObject.activeSelf);
        Assert.Equal("1", Cell(0, 0).text);
        Assert.Equal("VELA", Cell(0, 1).text);
        Assert.Equal("130", Cell(0, 2).text);
        Assert.False(board.transform.GetChild(3).gameObject.activeSelf); // beyond the cache

        // Nameless fallback shrinks the font; the local pilot's row is cyan.
        Assert.Equal("[NAMELESS PILOT]", Cell(1, 1).text);
        Assert.Equal(14, Cell(1, 1).fontSize);
        Assert.Equal(new Color(.1f, .7f, .7f), Cell(2, 1).color);
        Assert.Equal(Color.white, Cell(0, 1).color);
    }

    [Fact]
    public void SelectGame_SwitchesDropdown_AndFlipsGolfScoresForDisplay()
    {
        EnterAndSettle();

        screen.SelectGame(1);
        Tick(2); // SelectShipTypeCoroutine's WaitUntil gate

        // One vessel → no "Any"; golf scores stored negated → displayed positive.
        Assert.Equal(new[] { "Rhino" }, dropdown.options.Select(o => o.text).ToArray());
        Assert.Equal("CRUX", Cell(0, 1).text);
        Assert.Equal("42", Cell(0, 2).text);
        Assert.False(board.transform.GetChild(1).gameObject.activeSelf);
    }

    [Fact]
    public void ProfileLoaded_RefreshesTheBoard()
    {
        EnterAndSettle();
        Assert.Equal("130", Cell(0, 2).text);

        // A new cached board lands (e.g. after an online sync elsewhere), then
        // the legacy profile-loaded signal refreshes the view.
        DataAccessor.Save("leaderboard_HEXRACE_ANY.data", new List<LeaderboardManager.LeaderboardEntry>
        {
            new("NOVA", "pilot-nova", 999, 0, null),
        });
        PlayerDataController.RaiseProfileLoadedForTest();

        Assert.Equal("NOVA", Cell(0, 1).text);
        Assert.Equal("999", Cell(0, 2).text);
        Assert.False(board.transform.GetChild(1).gameObject.activeSelf);
    }

    [Fact]
    public void GetGameplayStatKey_IsModeUnderscoreVessel_Uppercased()
    {
        Assert.Equal("HEXRACE_DOLPHIN",
            LeaderboardManager.Instance.GetGameplayStatKey(GameModes.HexRace, VesselClassType.Dolphin));
        Assert.Equal("MULTIPLAYERJOUST_ANY",
            LeaderboardManager.Instance.GetGameplayStatKey(GameModes.MultiplayerJoust, VesselClassType.Any));
    }

    [Fact]
    public void ReportGameplayStatistic_Offline_AccumulatesThreeRows_GolfNegates()
    {
        DataAccessor.Flush("offline_stats.data");

        LeaderboardManager.Instance.ReportGameplayStatistic(
            GameModes.HexRace, VesselClassType.Dolphin, intensity: 2, score: 77, golfScoring: true);

        var stats = DataAccessor.Load<List<StatisticUpdate>>("offline_stats.data");
        Assert.Equal(3, stats.Count);
        Assert.Equal(("HEXRACE_DOLPHIN", -77), (stats[0].StatisticName, stats[0].Value));
        Assert.Equal(("HEXRACE_ANY", -77), (stats[1].StatisticName, stats[1].Value));
        Assert.Equal(("HEXRACE_DOLPHIN_PlayCount", 1), (stats[2].StatisticName, stats[2].Value));

        // A second report APPENDS (the offline lane accumulates until a flush).
        LeaderboardManager.Instance.ReportGameplayStatistic(
            GameModes.HexRace, VesselClassType.Dolphin, intensity: 2, score: 10, golfScoring: false);
        Assert.Equal(6, DataAccessor.Load<List<StatisticUpdate>>("offline_stats.data").Count);

        DataAccessor.Flush("offline_stats.data");
    }

    static void SetField(object target, string name, object value)
    {
        FieldInfo field = null;
        for (var t = target.GetType(); t != null && field == null; t = t.BaseType)
            field = t.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
        (field ?? throw new MissingFieldException(target.GetType().Name, name)).SetValue(target, value);
    }
}
