using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CosmicShore.Engine;
using CosmicShore.Engine.Networking;
using CosmicShore.Engine.SceneManagement;
using CosmicShore.Engine.Soap;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;

namespace CosmicShore.Tests;

// ─────────────────────────────────────────────────────────────────────────────
// PartyInviteController — the LIVE invite-transition orchestration driven
// through the fake lobby/session/transition seams (HcsLobby / FakeLobbyService /
// FakePartySessionService from HostConnectionServiceTests + a counting
// transition fake): singleton lifecycle, the _transitioning duplicate guard,
// the accept happy path (shutdown → HCS accept → connect → client-ready
// watchdog → OnPartyJoinCompleted), the connect-failure bounce-to-solo-menu,
// host-loss recovery idempotence, the leave-lobby sequence, the un-carried
// HostConnectionService.LeavePartyAsync body, and the un-carried
// RefreshAsync IsTransitioning entry guard.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Counting transition fake — lets tests fail the Netcode connect step.</summary>
sealed class PicFakeTransition : INetworkTransitionService
{
    public int ShutdownCalls, ClearCalls, ConnectWaits;
    public bool ConnectResult = true;
    public Task<bool> ShutdownAsync(float timeoutSeconds, CancellationToken ct)
    { ShutdownCalls++; return Task.FromResult(true); }
    public Task<bool> WaitForClientConnectionAsync(float timeoutSeconds, CancellationToken ct)
    { ConnectWaits++; return Task.FromResult(ConnectResult); }
    public Task<bool> WaitForSceneSyncAsync(string sceneName, float timeoutSeconds, CancellationToken ct)
        => Task.FromResult(true);
    public void ClearStaleReferences() => ClearCalls++;
}

public class PartyInviteControllerTests : IDisposable
{
    readonly GameLoop loop = new(nameof(PartyInviteControllerTests));
    readonly List<string> scenesAnnounced = new();

    public PartyInviteControllerTests()
    {
        NetworkManager.Singleton = null;
        SceneManager.sceneLoaded += RecordScene;
    }

    void RecordScene(Scene scene, LoadSceneMode mode) => scenesAnnounced.Add(scene.name);

    public void Dispose()
    {
        SceneManager.sceneLoaded -= RecordScene;
        // Clear the DontDestroyOnLoad singletons so the next test's Awake takes them.
        typeof(PartyInviteController).GetProperty("Instance")!.SetValue(null, null);
        typeof(HostConnectionService).GetProperty("Instance")!.SetValue(null, null);
        NetworkManager.Singleton = null;
        loop.Dispose();
    }

    static void Set(object target, string field, object value)
    {
        for (Type t = target.GetType(); t != null; t = t.BaseType)
        {
            var f = t.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (f == null) continue;
            f.SetValue(target, value);
            return;
        }
        throw new InvalidOperationException($"Field '{field}' not found on {target.GetType().Name}.");
    }

    sealed class Rig
    {
        public PartyInviteController Pic;
        public HostConnectionService Hcs;
        public HostConnectionDataSO Conn;
        public GameDataSO GameData;
        public FakeLobbyService Lobby;
        public FakePartySessionService Party;
        public PicFakeTransition Transition;
    }

    /// <summary>
    /// Builds the upstream pairing: HostConnectionService + PartyInviteController on
    /// their persistent GameObjects, sharing connectionData / gameData / the one
    /// transition service — exactly the DI arrangement AppManager wires in-game.
    /// </summary>
    Rig MakeRig()
    {
        var conn = ScriptableObject.CreateInstance<HostConnectionDataSO>();
        conn.OnHostConnectionEstablished = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        conn.OnHostConnectionLost = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        conn.OnlinePlayers = ScriptableObject.CreateInstance<ScriptableListPartyPlayerData>();
        conn.PartyMembers = ScriptableObject.CreateInstance<ScriptableListPartyPlayerData>();
        conn.OnPartyMemberJoined = ScriptableObject.CreateInstance<ScriptableEventPartyPlayerData>();
        conn.OnPartyMemberLeft = ScriptableObject.CreateInstance<ScriptableEventPartyPlayerData>();
        conn.OnPartyMemberKicked = ScriptableObject.CreateInstance<ScriptableEventPartyPlayerData>();
        conn.OnInviteReceived = ScriptableObject.CreateInstance<ScriptableEventPartyInviteData>();
        conn.OnInviteSent = ScriptableObject.CreateInstance<ScriptableEventPartyPlayerData>();
        conn.OnPartyJoinCompleted = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        conn.OnInviteResolved = ScriptableObject.CreateInstance<ScriptableEventNoParam>();
        conn.LocalPlayerId = "local";
        conn.LocalDisplayName = "Me";
        conn.LocalAvatarId = 1;

        var authVar = ScriptableObject.CreateInstance<AuthenticationDataVariable>();
        authVar.Value = new AuthenticationData
        {
            PlayerId = "local",
            IsSignedIn = false, // Start()'s HandleSignedInEvent early-returns — flows driven directly
            OnSignedIn = ScriptableObject.CreateInstance<ScriptableEventNoParam>(),
        };

        var gameData = ScriptableObject.CreateInstance<GameDataSO>();
        gameData.OnClientReady = ScriptableObject.CreateInstance<ScriptableEventNoParam>();

        var lobby = new FakeLobbyService();
        var party = new FakePartySessionService();
        var transition = new PicFakeTransition();
        var writer = new LobbyPropertyWriter();
        var bus = new SoapPartyEventBus(conn);

        var hcsGo = new GameObject("host-connection-service");
        var hcs = hcsGo.AddComponent<HostConnectionService>();
        Set(hcs, "connectionData", conn);
        Set(hcs, "authenticationDataVariable", authVar);
        Set(hcs, "_gameData", gameData);
        Set(hcs, "_lobbyService", lobby);
        Set(hcs, "_partySessionService", party);
        Set(hcs, "_memberService", new PartyMemberService(conn, bus));
        Set(hcs, "_networkTransition", transition);
        Set(hcs, "_acceptanceService", new AcceptanceSignalService());
        Set(hcs, "_inviteService", new InviteService());
        Set(hcs, "_propertyWriter", writer);
        Set(hcs, "_eventBus", bus);
        Set(hcs, "_scheduler", new LobbyRefreshScheduler(3f));

        var picGo = new GameObject("party-invite-controller");
        var pic = picGo.AddComponent<PartyInviteController>();
        Set(pic, "connectionData", conn);
        Set(pic, "gameData", gameData);
        Set(pic, "_sceneNames", ScriptableObject.CreateInstance<SceneNameListSO>()); // default "Menu_Main"
        Set(pic, "_networkTransition", transition);

        loop.Tick(1f / 60f); // Awake (both singletons) + Start (HCS signed-out → idle)

        return new Rig
        {
            Pic = pic, Hcs = hcs, Conn = conn, GameData = gameData,
            Lobby = lobby, Party = party, Transition = transition,
        };
    }

    sealed class TickDriver : MonoBehaviour
    {
        public Action Action;
        void Update() { var a = Action; Action = null; a?.Invoke(); }
    }

    /// <summary>
    /// Start an async flow INSIDE a tick (the C4/C6 sync-context discipline)
    /// and pump frames until it settles.
    /// </summary>
    Task Run(Func<Task> entry, Action betweenTicks = null)
    {
        Task flow = null;
        var driver = new GameObject("flow-driver").AddComponent<TickDriver>();
        driver.Action = () => flow = entry();
        loop.Tick(1f / 60f);
        betweenTicks?.Invoke();
        for (int i = 0; i < 600 && !flow.IsCompleted; i++) loop.Tick(1f / 60f);
        Assert.True(flow.IsCompleted, "flow should settle");
        return flow;
    }

    static void SetTransitioning(PartyInviteController pic, bool value)
        => typeof(PartyInviteController)
            .GetField("_transitioning", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(pic, value);

    [Fact]
    public void Awake_TakesSingleton_AndOnDestroyReleasesIt()
    {
        var rig = MakeRig();
        Assert.Same(rig.Pic, PartyInviteController.Instance);
        Assert.False(rig.Pic.IsTransitioning);

        CosmicShore.Engine.Object.Destroy(rig.Pic.gameObject);
        loop.Tick(1f / 60f);
        Assert.Null(PartyInviteController.Instance);
    }

    [Fact]
    public void AcceptInvite_WhileTransitioning_IsIgnored()
    {
        var rig = MakeRig();
        SetTransitioning(rig.Pic, true);

        Run(() => rig.Pic.AcceptInviteAsync(new PartyInviteData("host-9", "sess-9", "Host", 3)));

        // Duplicate guard returns before the try/finally — no teardown, state untouched.
        Assert.Equal(0, rig.Transition.ShutdownCalls);
        Assert.True(rig.Pic.IsTransitioning);
    }

    [Fact]
    public void AcceptInvite_HappyPath_JoinsPartyAndRaisesJoinCompleted()
    {
        var rig = MakeRig();
        int joinCompleted = 0;
        rig.Conn.OnPartyJoinCompleted.OnRaised += () => joinCompleted++;

        // The flow parks on the client-ready watchdog after the (fake-instant)
        // shutdown/accept/connect steps; a later frame raises OnClientReady the
        // way ClientPlayerVesselInitializer.InitializePair does in-game.
        var readyRaiser = new GameObject("ready-raiser").AddComponent<TickDriver>();
        Run(() => rig.Pic.AcceptInviteAsync(new PartyInviteData("host-9", "sess-9", "Host", 3)),
            betweenTicks: () => readyRaiser.Action = () => rig.GameData.InvokeClientReady());

        Assert.Equal(1, joinCompleted);
        Assert.False(rig.Pic.IsTransitioning);
        Assert.Equal(1, rig.Transition.ShutdownCalls);   // step 1 only — no recovery ran
        Assert.Equal(1, rig.Transition.ClearCalls);
        Assert.False(rig.Conn.IsPartyHost);              // we joined THEIR party
        Assert.Contains(rig.Conn.PartyMembers, m => m.PlayerId == "host-9");
        Assert.Empty(scenesAnnounced);                   // success path never reloads Menu_Main
    }

    [Fact]
    public void AcceptInvite_ConnectFailure_BouncesToSoloMenu()
    {
        var rig = MakeRig();
        rig.Transition.ConnectResult = false;
        int joinCompleted = 0;
        rig.Conn.OnPartyJoinCompleted.OnRaised += () => joinCompleted++;

        Run(() => rig.Pic.AcceptInviteAsync(new PartyInviteData("host-9", "sess-9", "Host", 3)));

        // Bounce = RecoverFromFailedTransitionAsync: leave → shutdown → Menu_Main
        // announce → EnsurePartySessionAsync (which recreates the solo session and
        // performs its own NM shutdown) — three shutdowns total with step 1's.
        Assert.Equal(0, joinCompleted);
        Assert.Contains("Menu_Main", scenesAnnounced);
        Assert.Equal(3, rig.Transition.ShutdownCalls);
        Assert.True(rig.Conn.IsPartyHost);               // solo party restored
        Assert.False(rig.Pic.IsTransitioning);
    }

    [Fact]
    public void HandleHostLoss_IsIdempotentWhileTransitioning_ThenRecoversOnce()
    {
        var rig = MakeRig();

        // Mid-transition (the OnClientDisconnect + OnTransportFailure double-fire):
        // the second entrant must be a no-op.
        SetTransitioning(rig.Pic, true);
        Run(() => rig.Pic.HandleHostLossAsync("transport failure"));
        Assert.Equal(0, rig.Transition.ShutdownCalls);
        Assert.Empty(scenesAnnounced);
        SetTransitioning(rig.Pic, false);

        Run(() => rig.Pic.HandleHostLossAsync("host left"));

        Assert.Contains("Menu_Main", scenesAnnounced);
        Assert.Equal(2, rig.Transition.ShutdownCalls);   // recovery + EnsurePartySession
        Assert.True(rig.Conn.IsPartyHost);
        Assert.False(rig.Pic.IsTransitioning);
    }

    [Fact]
    public void LeaveParty_RunsTheFullColdBootSequence()
    {
        var rig = MakeRig();

        Run(() => rig.Pic.LeavePartyAndReturnToMenuAsync());

        // leave own session → NM shutdown → Menu_Main announce → solo session ensured.
        Assert.Contains("Menu_Main", scenesAnnounced);
        Assert.Equal(2, rig.Transition.ShutdownCalls);   // leave flow + EnsurePartySession
        Assert.True(rig.Transition.ClearCalls >= 1);
        Assert.True(rig.Conn.IsPartyHost);
        Assert.False(rig.Pic.IsTransitioning);
        Assert.Null(rig.GameData.LocalPlayer);           // runtime data reset
    }

    [Fact]
    public void HcsLeaveParty_RoutesThroughTheController()
    {
        // The un-carried HostConnectionService.LeavePartyAsync body: resolve the
        // invite, fire member-left locally, bounded joined_party clear, then the
        // controller's full leave sequence.
        var rig = MakeRig();
        rig.Conn.PartyMembers.Add(new PartyPlayerData("host-9", "Host", 3));
        int resolved = 0, left = 0;
        rig.Conn.OnInviteResolved.OnRaised += () => resolved++;
        rig.Conn.OnPartyMemberLeft.OnRaised += _ => left++;

        Run(() => rig.Hcs.LeavePartyAsync());

        Assert.True(resolved >= 1);
        Assert.Equal(1, left);                           // remote member cleared with events
        Assert.Contains("Menu_Main", scenesAnnounced);   // controller leave flow ran
        Assert.False(rig.Pic.IsTransitioning);
    }

    [Fact]
    public void HcsLeaveParty_WithoutController_ReturnsBeforeAnyStateMutation()
    {
        var rig = MakeRig();
        typeof(PartyInviteController).GetProperty("Instance")!.SetValue(null, null);
        int resolved = 0;
        rig.Conn.OnInviteResolved.OnRaised += () => resolved++;

        Run(() => rig.Hcs.LeavePartyAsync());

        Assert.Equal(0, resolved);                       // null-controller branch is FIRST
        Assert.Empty(scenesAnnounced);
        Assert.Equal(0, rig.Transition.ShutdownCalls);
    }

    [Fact]
    public void Refresh_SkipsTheTick_WhileControllerIsTransitioning()
    {
        // The un-carried RefreshAsync entry guard: mid party-transition the
        // presence tick is skipped (and the error counter cleared) so transport
        // churn can't escalate to a throwaway reconnect.
        var rig = MakeRig();
        rig.Lobby.Lobby.Roster.Add(new HcsPlayer { Id = "guest-1" });

        SetTransitioning(rig.Pic, true);
        RunRefresh(rig.Hcs);
        Assert.Empty(rig.Conn.OnlinePlayers);            // tick skipped

        SetTransitioning(rig.Pic, false);
        RunRefresh(rig.Hcs);
        Assert.Single(rig.Conn.OnlinePlayers);           // scan loop resumes
        Assert.Equal("guest-1", rig.Conn.OnlinePlayers[0].PlayerId);
    }

    void RunRefresh(HostConnectionService svc)
    {
        Task refresh = null;
        var driver = new GameObject("refresh-driver").AddComponent<TickDriver>();
        driver.Action = () => refresh =
            (Task)typeof(HostConnectionService)
                .GetMethod("RefreshAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(svc, null);
        loop.Tick(1f / 60f);
        for (int i = 0; i < 120 && !refresh.IsCompleted; i++) loop.Tick(1f / 60f);
        Assert.True(refresh.IsCompleted, "refresh cycle should settle");
    }
}
