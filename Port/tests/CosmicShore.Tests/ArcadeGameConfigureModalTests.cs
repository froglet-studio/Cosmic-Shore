using System;
using System.Collections.Generic;
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
// Arc F 2b-iii(b) — the ported ArcadeGameConfigureModal, headless, on the SOLO
// path (no ArcadeConfigSyncManager, no NetworkManager): the exact configuration
// the menushell host proves visually. Covers the host flow contract:
// SetSelectedGame opens Screen 1 with game defaults synced into config +
// GameDataSO; the per-game domain minimum (Joust-style MinDomainsAllowed=2)
// drives the DC default; OnConfirmConfiguration commits once (guarded) and
// lands on Screen 2 with back-navigation removed; OnStartGameClicked with no
// sync manager launches directly — GameDataSO carries scene/mode/counts/DC and
// OnLaunchGame fires exactly once; OnCloseModal resets config + re-arms the
// commit guard; intensity picks clamp to the game's range; ship cycling wraps
// and broadcasts the class; ShouldLocalPlayerLaunch's authority truth table;
// tile visibility hides the Blue sentinel and dims tiles outside DC.
// ─────────────────────────────────────────────────────────────────────────────

public class ArcadeGameConfigureModalTests : IDisposable
{
    readonly GameLoop loop;

    readonly ArcadeGameConfigureModal modal;
    readonly ArcadeGameConfigSO config;
    readonly GameDataSO gameData;
    readonly IntVariable shipClassBroadcast;
    readonly GameObject configurationView;
    readonly GameObject gameDetailView;
    readonly Button startGameButton;
    readonly GameObject waitingLabel;
    readonly Button confirmButton;
    readonly GameObject backButton;
    readonly List<DomainInfoData> tiles = new();

    public ArcadeGameConfigureModalTests()
    {
        loop = new GameLoop(nameof(ArcadeGameConfigureModalTests));

        // Scene transcription (Menu_Main): the AudioSystem singleton the modal
        // family plays its open/close/confirm cues through, and the loadout store
        // ArcadeExploreView.Start initializes before any game can be selected.
        var audio = new GameObject("AudioSystem").AddComponent<AudioSystem>();
        LoadoutSystem.Init();

        gameData = ScriptableObject.CreateInstance<GameDataSO>();
        gameData.OnLaunchGame = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        gameData.SelectedIntensity = ScriptableObject.CreateInstance<IntVariable>();
        gameData.SelectedPlayerCount = ScriptableObject.CreateInstance<IntVariable>();
        gameData.selectedVesselClass = ScriptableObject.CreateInstance<VesselClassTypeVariable>();
        gameData.VesselClassSelectedIndex = ScriptableObject.CreateInstance<IntVariable>();

        config = ScriptableObject.CreateInstance<ArcadeGameConfigSO>();
        shipClassBroadcast = ScriptableObject.CreateInstance<IntVariable>();

        // Wire-then-activate: fields land before Awake/OnEnable/Start run.
        var modalGo = new GameObject("ArcadeGameConfigureModal", typeof(RectTransform));
        modalGo.SetActive(false);
        modalGo.AddComponent<CanvasGroup>();
        modal = modalGo.AddComponent<ArcadeGameConfigureModal>();

        configurationView = new GameObject("ConfigurationDetailView");
        configurationView.transform.SetParent(modalGo.transform, false);
        gameDetailView = new GameObject("GameDetailView");
        gameDetailView.transform.SetParent(modalGo.transform, false);

        startGameButton = MakeButton(modalGo.transform, "StartGameButton");
        confirmButton = MakeButton(modalGo.transform, "ConfirmConfigurationButton");
        waitingLabel = new GameObject("WaitingForOthersLabel");
        waitingLabel.transform.SetParent(modalGo.transform, false);
        backButton = new GameObject("BackFromGameSelectButton");
        backButton.transform.SetParent(modalGo.transform, false);

        foreach (var domain in new[] { Domains.Blue, Domains.Jade, Domains.Ruby, Domains.Gold })
            tiles.Add(MakeTile(modalGo.transform, domain));

        SetField(modal, "config", config);
        SetField(modal, "gameData", gameData);
        SetField(modal, "shipClassTypeVariable", shipClassBroadcast);
        SetField(modal, "configurationDetailView", configurationView);
        SetField(modal, "gameDetailView", gameDetailView);
        SetField(modal, "startGameButton", startGameButton);
        SetField(modal, "waitingForOthersLabel", waitingLabel);
        SetField(modal, "confirmConfigurationButton", confirmButton);
        SetField(modal, "backFromGameSelectButton", backButton);
        SetField(modal, "domainInfoItems", tiles);
        SetBaseField(modal, "audioSystem", audio);

        modalGo.SetActive(true);
        loop.Tick(1f / 60f); // Start: canvas hidden, config reset
    }

    public void Dispose() => loop.Dispose();

    static void SetField(object target, string name, object value)
    {
        FieldInfo field = null;
        for (var t = target.GetType(); t != null && field == null; t = t.BaseType)
            field = t.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
        (field ?? throw new MissingFieldException(target.GetType().Name, name)).SetValue(target, value);
    }

    static void SetBaseField(object target, string name, object value) => SetField(target, name, value);

    static void Invoke(object target, string method, params object[] args)
    {
        MethodInfo mi = null;
        for (var t = target.GetType(); t != null && mi == null; t = t.BaseType)
            mi = t.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance);
        (mi ?? throw new MissingMethodException(target.GetType().Name, method)).Invoke(target, args);
    }

    static Button MakeButton(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.AddComponent<Button>();
    }

    static DomainInfoData MakeTile(Transform parent, Domains domain)
    {
        var go = new GameObject($"Tile_{domain}", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var button = go.AddComponent<Button>();
        var tile = go.AddComponent<DomainInfoData>();
        SetField(tile, "domain", domain);
        SetField(tile, "button", button);
        return tile;
    }

    static SO_ArcadeGame MakeGame(
        string displayName = "HEX RACE",
        GameModes mode = GameModes.HexRace,
        string sceneName = "MinigameHexRace",
        bool isMultiplayer = true,
        int minPlayers = 1, int maxPlayers = 12,
        int minDomains = 1, int maxDomains = 3,
        int minIntensity = 1, int maxIntensity = 4,
        params VesselClassType[] vesselClasses)
    {
        var game = ScriptableObject.CreateInstance<SO_ArcadeGame>();
        game.DisplayName = displayName;
        game.Description = $"{displayName} description";
        game.Mode = mode;
        game.SceneName = sceneName;
        game.IsMultiplayer = isMultiplayer;
        game.MinPlayersAllowed = minPlayers;
        game.MaxPlayersAllowed = maxPlayers;
        game.MinDomainsAllowed = minDomains;
        game.MaxDomainsAllowed = maxDomains;
        game.MinIntensity = minIntensity;
        game.MaxIntensity = maxIntensity;
        game.Vessels = new List<SO_Vessel>();
        foreach (var vesselClass in vesselClasses)
        {
            var vessel = ScriptableObject.CreateInstance<SO_Vessel>();
            vessel.Class = vesselClass;
            vessel.Name = vesselClass.ToString();
            game.Vessels.Add(vessel);
        }
        return game;
    }

    void OpenModalWith(SO_ArcadeGame game)
    {
        // Mirrors ArcadeExploreView.SelectGame — the host entry point.
        modal.ModalWindowIn();
        modal.SetSelectedGame(game);
    }

    [Fact]
    public void SetSelectedGame_OpensScreen1_WithGameDefaultsSynced()
    {
        var game = MakeGame(minPlayers: 2, minIntensity: 2, maxIntensity: 3);
        OpenModalWith(game);

        Assert.True(configurationView.activeSelf);
        Assert.False(gameDetailView.activeSelf);
        Assert.Same(game, config.SelectedGame);
        Assert.Equal(2, config.Intensity);                 // clamped to MinIntensity
        Assert.Equal(2, config.PlayerCount);               // max(MinPlayersAllowed, 1 human)
        Assert.Equal(2, gameData.SelectedIntensity.Value); // SyncGameDataConfig
        Assert.Equal(2, gameData.SelectedPlayerCount.Value);
        Assert.True(startGameButton.gameObject.activeSelf);
        Assert.False(waitingLabel.activeSelf);
        Assert.True(confirmButton.interactable);
    }

    [Fact]
    public void DomainCountDefault_RespectsPerGameMinimum()
    {
        OpenModalWith(MakeGame());
        Assert.Equal(1, config.DomainCount); // DefaultDomainCount for a min-1 mode

        OpenModalWith(MakeGame(displayName: "JOUST", mode: GameModes.MultiplayerJoust,
                               sceneName: "MinigameJoust_Gameplay", minDomains: 2));
        Assert.Equal(2, config.DomainCount); // Joust-style opposing-team floor
    }

    [Fact]
    public void ConfirmConfiguration_LandsOnScreen2_CommitsOnce()
    {
        OpenModalWith(MakeGame());

        modal.OnConfirmConfiguration();

        Assert.False(configurationView.activeSelf);
        Assert.True(gameDetailView.activeSelf);
        Assert.False(confirmButton.interactable);  // spam-click defense
        Assert.False(backButton.activeSelf);       // commit-once flow has no back path

        // Second click short-circuits at the guard (no throw, state unchanged).
        modal.OnConfirmConfiguration();
        Assert.True(gameDetailView.activeSelf);
        Assert.False(confirmButton.interactable);
    }

    [Fact]
    public void StartGame_NoSyncManager_LaunchesDirectly_AndSyncsGameData()
    {
        int launches = 0;
        gameData.OnLaunchGame.OnRaised += () => launches++;

        var game = MakeGame(minPlayers: 4, minDomains: 2);
        OpenModalWith(game);
        modal.OnConfirmConfiguration();
        modal.OnStartGameClicked();

        Assert.Equal(1, launches);
        Assert.Equal("MinigameHexRace", gameData.SceneName);
        Assert.Equal(GameModes.HexRace, gameData.GameMode);
        Assert.True(gameData.IsMultiplayerMode);
        Assert.Equal(4, gameData.SelectedPlayerCount.Value);       // ConfigurePlayerCounts total
        Assert.Equal(3, gameData.RequestedAIBackfillCount);        // 4 desired − 1 human
        Assert.Equal(2, gameData.RequestedDomainCount);
        Assert.Null(config.SelectedGame);                          // runtime state cleared
        Assert.False(startGameButton.gameObject.activeSelf);       // ready-up UI engaged
        Assert.True(waitingLabel.activeSelf);

        // ModalWindowOut hides via the 0.5s disable coroutine.
        for (int i = 0; i < 35; i++) loop.Tick(1f / 60f);
        Assert.Equal(0f, modal.GetComponent<CanvasGroup>().alpha);
    }

    [Fact]
    public void CloseModal_ResetsConfig_AndReArmsCommitGuard()
    {
        var game = MakeGame();
        OpenModalWith(game);
        modal.OnConfirmConfiguration();
        Assert.False(confirmButton.interactable);

        modal.OnCloseModal();
        Assert.Null(config.SelectedGame);
        Assert.True(confirmButton.interactable); // guard re-armed

        // A fresh session can commit again.
        OpenModalWith(game);
        Assert.True(configurationView.activeSelf);
        modal.OnConfirmConfiguration();
        Assert.True(gameDetailView.activeSelf);
    }

    [Fact]
    public void IntensitySelection_ClampsToGameRange_AndSyncs()
    {
        OpenModalWith(MakeGame(minIntensity: 1, maxIntensity: 3));

        Invoke(modal, "HandleIntensitySelected", 7);
        Assert.Equal(3, config.Intensity); // clamped to MaxIntensity
        Assert.Equal(3, gameData.SelectedIntensity.Value);

        Invoke(modal, "HandleIntensitySelected", 2);
        Assert.Equal(2, config.Intensity);
        Assert.Equal(2, gameData.SelectedIntensity.Value);
    }

    [Fact]
    public void PlayerCountChange_ReboundsDomainCount()
    {
        OpenModalWith(MakeGame(minDomains: 1, maxDomains: 3));
        Invoke(modal, "HandlePlayerCountSelected", 6);
        Invoke(modal, "HandleDomainCountChanged", 3);
        Assert.Equal(3, config.DomainCount);

        // PC drops to 2 → DC bound (DC <= PC) shrinks → DC re-clamped.
        Invoke(modal, "HandlePlayerCountSelected", 2);
        Assert.Equal(2, config.PlayerCount);
        Assert.Equal(2, config.DomainCount);
    }

    [Fact]
    public void ShipCycling_DefaultsToDolphin_WrapsAndBroadcastsClass()
    {
        OpenModalWith(MakeGame(vesselClasses: new[] { VesselClassType.Manta, VesselClassType.Dolphin }));

        Assert.Equal(VesselClassType.Dolphin, config.SelectedShip.Class); // rule 3: Dolphin default
        Assert.Equal(VesselClassType.Dolphin, gameData.selectedVesselClass.Value);

        modal.OnNextShipClicked();
        Assert.Equal(VesselClassType.Manta, config.SelectedShip.Class);
        Assert.Equal(VesselClassType.Manta, gameData.selectedVesselClass.Value);
        Assert.Equal((int)VesselClassType.Manta, shipClassBroadcast.Value);
        Assert.Equal((int)VesselClassType.Manta, gameData.VesselClassSelectedIndex.Value);

        modal.OnNextShipClicked(); // wraps
        Assert.Equal(VesselClassType.Dolphin, config.SelectedShip.Class);

        modal.OnPreviousShipClicked(); // wraps backwards
        Assert.Equal(VesselClassType.Manta, config.SelectedShip.Class);
    }

    [Fact]
    public void TileVisibility_HidesBlueSentinel_DimsOutsideDomainCount()
    {
        OpenModalWith(MakeGame()); // DC defaults to 1 → only Jade active

        var blue = tiles[0]; var jade = tiles[1]; var ruby = tiles[2]; var gold = tiles[3];
        Assert.False(blue.gameObject.activeSelf); // Blue is the "no team" sentinel — never shown
        Assert.True(jade.gameObject.activeSelf);
        Assert.True(jade.Button.interactable);
        Assert.False(ruby.Button.interactable);   // outside ActiveDomains[0..DC-1]
        Assert.False(gold.Button.interactable);

        Invoke(modal, "HandlePlayerCountSelected", 6);
        Invoke(modal, "HandleDomainCountChanged", 3);
        Assert.True(ruby.Button.interactable);
        Assert.True(gold.Button.interactable);
        Assert.False(blue.gameObject.activeSelf);
    }

    [Fact]
    public void ShouldLocalPlayerLaunch_AuthorityTruthTable()
    {
        // (a) no sync manager — legacy solo path always launches.
        Assert.True(ArcadeGameConfigureModal.ShouldLocalPlayerLaunch(null, hasSyncManager: false));
        Assert.True(ArcadeGameConfigureModal.ShouldLocalPlayerLaunch(null, hasSyncManager: true));

        // (b) sync manager + not in a multi-human party (just self) — launches.
        var solo = ScriptableObject.CreateInstance<HostConnectionDataSO>();
        solo.PartyMembers = ScriptableObject.CreateInstance<ScriptableListPartyPlayerData>();
        solo.PartyMembers.Add(new PartyPlayerData("p1", "Self", 0));
        Assert.True(ArcadeGameConfigureModal.ShouldLocalPlayerLaunch(solo, hasSyncManager: true));

        // (c) multi-human party — only the party host holds launch authority.
        var party = ScriptableObject.CreateInstance<HostConnectionDataSO>();
        party.PartyMembers = ScriptableObject.CreateInstance<ScriptableListPartyPlayerData>();
        party.PartyMembers.Add(new PartyPlayerData("p1", "Host", 0));
        party.PartyMembers.Add(new PartyPlayerData("p2", "Client", 0));
        party.IsPartyHost = true;
        Assert.True(ArcadeGameConfigureModal.ShouldLocalPlayerLaunch(party, hasSyncManager: true));
        party.IsPartyHost = false;
        Assert.False(ArcadeGameConfigureModal.ShouldLocalPlayerLaunch(party, hasSyncManager: true));
    }
}
