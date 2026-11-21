using System;
using System.Collections.Generic;
using System.Reflection;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Engine.Soap;
using CosmicShore.ScriptableObjects;

namespace CosmicShore.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// Progression unit (2026-07-10) — the REAL GameModeProgressionService (replacing
// the everything-unlocked shell). Covers the quest-chain contract: fresh boot
// unlocks only the first mode (first-is-free + EnsureFirstModeUnlocked) with
// Tournament always-unlocked at full intensity; ReportQuestStat completes the
// quest exactly at target (below-target only records the best stat); claiming
// unlocks the NEXT mode in the chain (intensity floor initialized, analytics
// recorded, completed flag cleared); the vessel-hangar feature gate opens only
// when every prior non-placeholder quest is complete; DebugSetProgressToIndex
// re-locks everything past the index; the intensity ladder math
// (GetPlaysRemainingForIntensity over play counts) and the DebugSetMaxIntensity
// clamp + OnIntensityUnlocked fan-out.
// ─────────────────────────────────────────────────────────────────────────────

public class GameModeProgressionServiceTests : IDisposable
{
    readonly GameLoop loop;

    readonly GameModeProgressionService service;
    readonly SO_GameModeQuestList questList;
    readonly SO_GameModeQuestData hexQuest;
    readonly SO_GameModeQuestData joustQuest;
    readonly SO_GameModeQuestData hangarQuest;
    readonly SO_GameModeQuestData captureQuest;
    readonly AnalyticsServiceFacade analytics = new();

    public GameModeProgressionServiceTests()
    {
        loop = new GameLoop(nameof(GameModeProgressionServiceTests));
        ClearInstance();

        SO_GameModeQuestData MakeQuest(GameModes mode, string displayName,
            QuestTargetType targetType, float targetValue, bool placeholder = false)
        {
            var quest = ScriptableObject.CreateInstance<SO_GameModeQuestData>();
            quest.GameMode = mode;
            quest.DisplayName = displayName;
            quest.TargetType = targetType;
            quest.TargetValue = targetValue;
            quest.IsPlaceholder = placeholder;
            quest.PlaysToUnlockIntensity3 = 3;
            quest.PlaysToUnlockIntensity4 = 2;
            return quest;
        }

        hexQuest = MakeQuest(GameModes.HexRace, "HEX RACE", QuestTargetType.CrystalsCollected, 10f);
        joustQuest = MakeQuest(GameModes.MultiplayerJoust, "JOUST", QuestTargetType.JoustsWon, 3f);
        hangarQuest = MakeQuest(GameModes.AstroLeague, "VESSEL HANGAR", QuestTargetType.Placeholder, 0f, placeholder: true);
        captureQuest = MakeQuest(GameModes.MultiplayerCrystalCapture, "CRYSTAL CAPTURE", QuestTargetType.ScoreAbove, 50f);
        questList = ScriptableObject.CreateInstance<SO_GameModeQuestList>();
        questList.Quests = new List<SO_GameModeQuestData> { hexQuest, joustQuest, hangarQuest, captureQuest };

        // A live UGSDataService whose repos exist (Awake) — flipped initialized by
        // reflection so the progression Start takes the immediate ready path.
        var authVar = ScriptableObject.CreateInstance<AuthenticationDataVariable>();
        var vesselList = ScriptableObject.CreateInstance<SO_VesselList>();
        vesselList.VesselList = new List<SO_Vessel>();
        var ugsGo = new GameObject("ugs-data-service");
        ugsGo.SetActive(false);
        var ugs = ugsGo.AddComponent<UGSDataService>();
        SetField(ugs, "vesselList", vesselList);
        SetField(ugs, "_authData", authVar);
        ugsGo.SetActive(true); // Awake: repos created
        typeof(UGSDataService).GetProperty("IsInitialized")!.SetValue(ugs, true);

        var serviceGo = new GameObject("GameModeProgressionService");
        serviceGo.SetActive(false);
        service = serviceGo.AddComponent<GameModeProgressionService>();
        SetField(service, "questList", questList);
        SetField(service, "_ugsDataService", ugs);
        SetField(service, "_analytics", analytics);
        serviceGo.SetActive(true); // Awake: Instance + first-mode unlock
        loop.Tick(1f / 60f);       // Start: ready path → ProgressionData from repo
    }

    public void Dispose()
    {
        loop.Dispose();
        ClearInstance();
    }

    static void ClearInstance()
        => typeof(GameModeProgressionService)
            .GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)!
            .SetValue(null, null);

    [Fact]
    public void FreshBoot_OnlyFirstModeUnlocked_TournamentAlwaysFree()
    {
        Assert.True(service.IsInitialized);
        Assert.True(service.IsGameModeUnlocked(GameModes.HexRace));            // first is free
        Assert.False(service.IsGameModeUnlocked(GameModes.MultiplayerJoust));  // gated
        Assert.False(service.IsGameModeUnlocked(GameModes.MultiplayerCrystalCapture));
        Assert.True(service.IsGameModeUnlocked(GameModes.Tournament));         // always-unlocked meta

        Assert.Equal(2, service.GetMaxUnlockedIntensity(GameModes.HexRace));   // intensity floor
        Assert.Equal(0, service.GetMaxUnlockedIntensity(GameModes.MultiplayerJoust)); // locked mode
        Assert.Equal(4, service.GetMaxUnlockedIntensity(GameModes.Tournament)); // full-intensity meta
        Assert.True(service.IsGameModeInQuestChain(GameModes.HexRace));
        Assert.False(service.IsGameModeInQuestChain(GameModes.Tournament));
    }

    [Fact]
    public void ReportQuestStat_CompletesAtTarget_ClaimUnlocksNextMode()
    {
        SO_GameModeQuestData completed = null;
        service.OnQuestCompleted += q => completed = q;

        service.ReportQuestStat(GameModes.HexRace, 9f); // below target
        Assert.False(service.IsQuestCompleted(GameModes.HexRace));
        Assert.Null(completed);

        service.ReportQuestStat(GameModes.HexRace, 10f); // exactly at target
        Assert.True(service.IsQuestCompleted(GameModes.HexRace));
        Assert.True(hexQuest.IsCompleted);
        Assert.Same(hexQuest, completed);

        service.ClaimQuestAndUnlockNext(GameModes.HexRace);
        Assert.False(service.IsQuestCompleted(GameModes.HexRace)); // claim consumes the completion
        Assert.False(hexQuest.IsCompleted);
        Assert.True(service.IsGameModeUnlocked(GameModes.MultiplayerJoust));
        Assert.Equal(2, service.GetMaxUnlockedIntensity(GameModes.MultiplayerJoust));
        Assert.Equal(GameModes.MultiplayerJoust, analytics.LastModeUnlocked);
        Assert.Equal(1, service.GetClaimedQuestCount());
    }

    [Fact]
    public void VesselHangarGate_OpensWhenEveryPriorQuestCompletes()
    {
        Assert.False(service.IsVesselHangarUnlocked()); // hex + joust incomplete

        service.ProgressionData.MarkQuestCompleted(GameModes.HexRace.ToString());
        Assert.False(service.IsVesselHangarUnlocked()); // joust still incomplete

        service.ProgressionData.MarkQuestCompleted(GameModes.MultiplayerJoust.ToString());
        Assert.True(service.IsVesselHangarUnlocked());  // placeholder hangar quest itself is skipped
    }

    [Fact]
    public void DebugSetProgressToIndex_UnlocksPrefix_LocksTheRest()
    {
        service.DebugSetProgressToIndex(3); // quests 0..2 unlocked

        Assert.True(service.IsGameModeUnlocked(GameModes.HexRace));
        Assert.True(service.IsGameModeUnlocked(GameModes.MultiplayerJoust));
        Assert.True(service.IsGameModeUnlocked(GameModes.AstroLeague));
        Assert.False(service.IsGameModeUnlocked(GameModes.MultiplayerCrystalCapture));
        Assert.Equal(2, service.GetClaimedQuestCount());

        service.ResetAllProgress();
        Assert.True(service.IsGameModeUnlocked(GameModes.HexRace)); // first re-unlocked
        Assert.False(service.IsGameModeUnlocked(GameModes.MultiplayerJoust));
        Assert.Equal(0, service.GetClaimedQuestCount());
    }

    [Fact]
    public void IntensityLadder_PlayCountsDriveRemaining_DebugSetClampsAndFires()
    {
        string mode = GameModes.HexRace.ToString();
        Assert.Equal(3, service.GetPlaysRequiredForIntensity(GameModes.HexRace, 3));
        Assert.Equal(2, service.GetPlaysRequiredForIntensity(GameModes.HexRace, 4));

        service.ProgressionData.EnsureIntensityInitialized(mode, 2);
        service.ProgressionData.IncrementIntensityPlayCount(mode, 2);
        service.ProgressionData.IncrementIntensityPlayCount(mode, 2);
        Assert.Equal(2, service.GetIntensityPlayCount(GameModes.HexRace, 2));
        Assert.Equal(1, service.GetPlaysRemainingForIntensity(GameModes.HexRace, 3));

        (GameModes mode, int intensity)? unlocked = null;
        service.OnIntensityUnlocked += (m, i) => unlocked = (m, i);
        service.DebugSetMaxIntensity(GameModes.HexRace, 9); // clamps to the config cap
        Assert.Equal(4, service.GetMaxUnlockedIntensity(GameModes.HexRace));
        Assert.Equal((GameModes.HexRace, 4), unlocked);
        // (analytics record only rides the RecordIntensityPlay gameplay lane, not the debug set)
        Assert.Equal(0, service.GetPlaysRemainingForIntensity(GameModes.HexRace, 4));
    }

    static void SetField(object target, string name, object value)
    {
        FieldInfo field = null;
        for (var t = target.GetType(); t != null && field == null; t = t.BaseType)
            field = t.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
        (field ?? throw new MissingFieldException(target.GetType().Name, name)).SetValue(target, value);
    }
}
