using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;

namespace CosmicShore.Tests;

// V17: R_VesselActionHandler (input-event → ShipAction dispatch, device-specific override
// resolution, resource-event wiring, input muting) and VesselPrismController (spawn-loop
// gating, prism configuration, gap trails, trail cap, danger mode). Skimmer wait-time and
// the AutoPilotEnabled guard are staged deviations (V16/V18); assertions here only cover
// behavior that survives those deviations.

// ShipHelper-style recording action: counts dispatches and captures the call context.
class RecordingAction : ShipActionSO
{
    public int Starts, Stops, Initializes;
    public IVesselStatus LastInitialized, LastStartStatus, LastStopStatus;
    public ActionExecutorRegistry LastExecutors;

    public override void Initialize(IVesselStatus vs)
    {
        base.Initialize(vs);
        Initializes++;
        LastInitialized = vs;
    }

    public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
    {
        Starts++;
        LastExecutors = execs;
        LastStartStatus = vesselStatus;
    }

    public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
    {
        Stops++;
        LastStopStatus = vesselStatus;
    }
}

// Minimal IPlayer so IVesselStatus default members (PlayerName, Domain, IsLocalUser) resolve.
class StubPlayer : IPlayer
{
    public Domains Domain { get; set; } = Domains.Ruby;
    public string Name { get; set; } = "tester";
    public int AvatarId => 0;
    public string PlayerUUID => "uuid";
    public IVessel Vessel => null;
    public InputController InputController => null;
    public IInputStatus InputStatus => null;
    public IRoundStats RoundStats => null;
    public bool IsActive => true;
    public bool IsInitializedAsAI => false;
    public bool IsMultiplayerOwner => false;
    public bool IsNetworkOwner => false;
    public bool IsNetworkClient => false;
    public bool IsLocalUser { get; set; }
    public ulong PlayerNetId => 0;
    public ulong VesselNetId => 0;
    public ulong OwnerClientNetId => 0;
    public Transform Transform => null;
    public void InitializeForSinglePlayerMode(IPlayer.InitializeData data, IVessel vessel) { }
    public void InitializeForMultiplayerMode(IVessel vessel) { }
    public void ToggleGameObject(bool toggle) { }
    public void DestroyPlayer() { }
    public void StartPlayer() { }
    public void ResetForPlay() { }
    public void ChangeVessel(IVessel vessel) { }
}

// StubVesselStatus with a real ShipTransform: VesselPrismController.CreateBlock evaluates
// ShipTransform.right unconditionally, so the shared double's null transform won't do.
class SpawnerVesselStatus : StubVesselStatus, IVesselStatus
{
    public Transform ShipXf;
    Transform IVesselStatus.ShipTransform => ShipXf;
}

public class VesselActionLayerTests
{
    static readonly System.Reflection.BindingFlags Priv =
        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;

    static void Set(object target, string field, object value)
        => target.GetType().GetField(field, Priv)!.SetValue(target, value);

    static object Get(object target, string field)
        => target.GetType().GetField(field, Priv)!.GetValue(target);

    static T So<T>() where T : ScriptableObject, new() => ScriptableObject.CreateInstance<T>();

    static void Run(GameLoop loop, int frames)
    {
        for (int i = 0; i < frames; i++) loop.Tick(1f / 60f);
    }

    // ----------------------------------------------------------------- handler rig

    sealed class HandlerRig
    {
        public R_VesselActionHandler Handler;
        public StubVesselStatus Status;
        public InputStatus Input;
        public RecordingAction Shared, TouchOverride, GamepadOverride, Resource;
        public ScriptableEventInputEvents Pressed, Released;
        public readonly List<AbilityStats> AbilityRaised = new();
        public readonly List<InputEventBlockPayload> BlockRaised = new();
    }

    static HandlerRig MakeHandler()
    {
        var rig = new HandlerRig();

        var inputGo = new GameObject("input");
        rig.Input = inputGo.AddComponent<InputStatus>();
        rig.Input.ActiveInputDevice = InputDeviceType.Keyboard; // enum default is Touch(0)

        rig.Status = new StubVesselStatus
        {
            Player = new StubPlayer { Name = "tester", IsLocalUser = false },
            InputStatus = rig.Input,
        };

        rig.Shared = So<RecordingAction>();
        rig.TouchOverride = So<RecordingAction>();
        rig.GamepadOverride = So<RecordingAction>();
        rig.Resource = So<RecordingAction>();
        rig.Pressed = So<ScriptableEventInputEvents>();
        rig.Released = So<ScriptableEventInputEvents>();
        var ability = So<ScriptableEventAbilityStats>();
        var blocked = So<ScriptableEventInputEventBlock>();
        ability.OnRaised += s => rig.AbilityRaised.Add(s);
        blocked.OnRaised += p => rig.BlockRaised.Add(p);

        var go = new GameObject("handler");
        rig.Handler = go.AddComponent<R_VesselActionHandler>();
        Set(rig.Handler, "_inputEventShipActions", new List<InputEventShipActionMapping>
        {
            new() { InputEvent = InputEvents.Button1Action, ShipActions = new List<ShipActionSO> { rig.Shared } },
            new() { InputEvent = InputEvents.Button2Action, ShipActions = new List<ShipActionSO> { rig.Shared } },
        });
        Set(rig.Handler, "_touchActionOverrides", new List<InputEventShipActionMapping>
        {
            new() { InputEvent = InputEvents.Button1Action, ShipActions = new List<ShipActionSO> { rig.TouchOverride } },
        });
        Set(rig.Handler, "_gamepadActionOverrides", new List<InputEventShipActionMapping>
        {
            new() { InputEvent = InputEvents.Button2Action, ShipActions = new List<ShipActionSO> { rig.GamepadOverride } },
        });
        Set(rig.Handler, "_resourceEventClassActions", new List<ResourceEventShipActionMapping>
        {
            new() { ResourceEvent = ResourceEvents.AboveHalfAmmo, ClassActions = new List<ShipActionSO> { rig.Resource } },
        });
        Set(rig.Handler, "_onButtonPressed", rig.Pressed);
        Set(rig.Handler, "_onButtonReleased", rig.Released);
        Set(rig.Handler, "onAbilityExecuted", ability);
        Set(rig.Handler, "_onInputEventBlocked", blocked);

        rig.Handler.Initialize(rig.Status);
        return rig;
    }

    [Fact]
    public void Initialize_BindsSharedMappings_AndPerformDispatches()
    {
        using var loop = new GameLoop();
        var rig = MakeHandler();

        // ShipHelper.InitializeShipControlActions initialized every mapped action with the status.
        Assert.True(rig.Shared.Initializes >= 1);
        Assert.Same(rig.Status, rig.Shared.LastInitialized);

        rig.Handler.PerformShipControllerActions(InputEvents.Button1Action);

        Assert.Equal(1, rig.Shared.Starts);
        Assert.Same(rig.Status, rig.Shared.LastStartStatus);
        Assert.Null(rig.Shared.LastExecutors); // _executors intentionally unwired
    }

    [Fact]
    public void StopShipControllerActions_RaisesAbilityStats_WithMeasuredDuration()
    {
        using var loop = new GameLoop();
        var rig = MakeHandler();

        rig.Handler.PerformShipControllerActions(InputEvents.Button1Action);
        Run(loop, 30); // 0.5s of game time
        rig.Handler.StopShipControllerActions(InputEvents.Button1Action);

        Assert.Equal(1, rig.Shared.Stops);
        var stats = Assert.Single(rig.AbilityRaised);
        Assert.Equal("tester", stats.PlayerName);
        Assert.Equal(InputEvents.Button1Action, stats.ControlType);
        Assert.InRange(stats.Duration, 0.45f, 0.55f);
    }

    [Fact]
    public void ButtonEvents_DispatchThroughSubscription_AndFireLocalEvents()
    {
        using var loop = new GameLoop();
        var rig = MakeHandler();
        var started = new List<InputEvents>();
        var stopped = new List<InputEvents>();
        rig.Handler.OnInputEventStarted += ie => started.Add(ie);
        rig.Handler.OnInputEventStopped += ie => stopped.Add(ie);

        rig.Handler.ToggleSubscription(true);
        rig.Pressed.Raise(InputEvents.Button1Action);
        rig.Released.Raise(InputEvents.Button1Action);

        Assert.Equal(1, rig.Shared.Starts);
        Assert.Equal(1, rig.Shared.Stops);
        Assert.Equal(new[] { InputEvents.Button1Action }, started);
        Assert.Equal(new[] { InputEvents.Button1Action }, stopped);

        // Unsubscribed: further raises are ignored.
        rig.Handler.ToggleSubscription(false);
        rig.Pressed.Raise(InputEvents.Button1Action);
        Assert.Equal(1, rig.Shared.Starts);
    }

    [Fact]
    public void ButtonPress_SpawnedOwner_RoutesThroughLocalRpcs()
    {
        using var loop = new GameLoop();
        var rig = MakeHandler();

        rig.Handler.Spawn(isServer: true, isClient: true, isOwner: true);
        rig.Handler.ToggleSubscription(true);
        rig.Pressed.Raise(InputEvents.Button2Action);

        // ServerRpc → ClientRpc execute locally until the transport phase.
        Assert.Equal(1, rig.Shared.Starts);
    }

    [Fact]
    public void DeviceOverrides_TakePrecedence_AndFallBackToSharedMapping()
    {
        using var loop = new GameLoop();
        var rig = MakeHandler();

        // Keyboard device: shared mapping fires.
        rig.Handler.PerformShipControllerActions(InputEvents.Button1Action);
        Assert.Equal(1, rig.Shared.Starts);
        Assert.Equal(0, rig.TouchOverride.Starts);

        // Touch override wins over the shared mapping for the same event.
        rig.Input.ActiveInputDevice = InputDeviceType.Touch;
        rig.Handler.PerformShipControllerActions(InputEvents.Button1Action);
        Assert.Equal(1, rig.Shared.Starts);
        Assert.Equal(1, rig.TouchOverride.Starts);

        // No touch override for Button2 → falls back to shared.
        rig.Handler.PerformShipControllerActions(InputEvents.Button2Action);
        Assert.Equal(2, rig.Shared.Starts);

        // Gamepad override wins for Button2.
        rig.Input.ActiveInputDevice = InputDeviceType.Gamepad;
        rig.Handler.PerformShipControllerActions(InputEvents.Button2Action);
        Assert.Equal(2, rig.Shared.Starts);
        Assert.Equal(1, rig.GamepadOverride.Starts);
    }

    [Fact]
    public void UnmappedEvent_NeitherDispatchesNorRaisesAbilityStats()
    {
        using var loop = new GameLoop();
        var rig = MakeHandler();

        rig.Handler.PerformShipControllerActions(InputEvents.FlipAction);
        rig.Handler.StopShipControllerActions(InputEvents.FlipAction);

        Assert.Equal(0, rig.Shared.Starts);
        Assert.Empty(rig.AbilityRaised);
    }

    [Fact]
    public void MuteInput_BlocksDispatch_ThenUnblocks_RaisingBlockPayloads()
    {
        using var loop = new GameLoop();
        var rig = MakeHandler();

        rig.Handler.MuteInput(InputEvents.Button1Action, 0.2f);

        var startedPayload = Assert.Single(rig.BlockRaised);
        Assert.Equal(InputEvents.Button1Action, startedPayload.Input);
        Assert.True(startedPayload.Started);
        Assert.False(startedPayload.Ended);
        Assert.Equal(0.2f, startedPayload.TotalSeconds);

        rig.Handler.PerformShipControllerActions(InputEvents.Button1Action);
        Assert.Equal(0, rig.Shared.Starts); // muted

        Run(loop, 30); // 0.5s > 0.2s mute window

        Assert.Equal(2, rig.BlockRaised.Count);
        Assert.True(rig.BlockRaised[1].Ended);
        Assert.False(rig.BlockRaised[1].Started);

        rig.Handler.PerformShipControllerActions(InputEvents.Button1Action);
        Assert.Equal(1, rig.Shared.Starts); // unmuted
    }

    [Fact]
    public void ResourceEventMappings_PopulateClassResourceActions()
    {
        using var loop = new GameLoop();
        var rig = MakeHandler();

        var dict = (Dictionary<ResourceEvents, List<ShipActionSO>>)Get(rig.Handler, "_classResourceActions");

        var actions = Assert.Contains(ResourceEvents.AboveHalfAmmo, dict);
        Assert.Equal(new ShipActionSO[] { rig.Resource }, actions);
    }

    // ----------------------------------------------------------------- spawner rig

    sealed class SpawnerRig
    {
        public VesselPrismController Controller;
        public SpawnerVesselStatus Status;
        public readonly List<PrismEventData> Raised = new();
        public readonly List<Prism> Spawned = new();
        public readonly List<Prism> Recycled = new();
    }

    static SpawnerRig MakeSpawner()
    {
        var rig = new SpawnerRig();

        var channel = So<PrismEventChannelWithReturnSO>();
        channel.OnEventReturn += data =>
        {
            rig.Raised.Add(data);
            var prismRig = PrismTestRig.Create("spawned");
            prismRig.GameObject.transform.position = data.SpawnPosition;
            return new PrismReturnEventData { SpawnedObject = prismRig.GameObject };
        };

        var shipGo = new GameObject("ship-transform");
        rig.Status = new SpawnerVesselStatus
        {
            Player = new StubPlayer { Name = "tester", Domain = Domains.Ruby },
            ShipXf = shipGo.transform,
            Speed = 10f,
        };

        var go = new GameObject("prism-controller");
        rig.Controller = go.AddComponent<VesselPrismController>();
        Set(rig.Controller, "_onPrismSpawnedEventChannel", channel);
        Set(rig.Controller, "startDelay", 0f);

        rig.Controller.OnBlockSpawned += p =>
        {
            rig.Spawned.Add(p);
            p.OnReturnToPool += recycled => rig.Recycled.Add(recycled);
        };

        rig.Controller.Initialize(rig.Status);
        return rig;
    }

    [Fact]
    public void SpawnLoop_GatesOnSpeed_AndAttachment()
    {
        using var loop = new GameLoop();
        var rig = MakeSpawner();

        rig.Status.Speed = 0f;
        rig.Controller.StartSpawn();
        Run(loop, 90);
        Assert.Empty(rig.Spawned); // too slow — no trail

        rig.Status.Speed = 10f;
        Run(loop, 90);
        Assert.NotEmpty(rig.Spawned); // fast enough — trail grows

        int count = rig.Spawned.Count;
        rig.Status.IsAttached = true;
        Run(loop, 90);
        Assert.Equal(count, rig.Spawned.Count); // attached — spawning gated off

        // Complete the Forget()-ed spawn loop before the test ends (xunit tracks async void).
        rig.Controller.StopSpawn();
    }

    [Fact]
    public void StopSpawn_HaltsSpawning()
    {
        using var loop = new GameLoop();
        var rig = MakeSpawner();

        rig.Controller.StartSpawn();
        Run(loop, 60);
        Assert.NotEmpty(rig.Spawned);

        int count = rig.Spawned.Count;
        rig.Controller.StopSpawn();
        Run(loop, 90);
        Assert.Equal(count, rig.Spawned.Count);
    }

    [Fact]
    public void CreateBlock_ConfiguresPrism_TeamOwnerScaleTrailAndEvents()
    {
        using var loop = new GameLoop();
        var rig = MakeSpawner();
        var creationArgs = new List<(float xShift, float wavelength, float x, float y, float z)>();
        rig.Controller.OnBlockCreated += (xShift, wavelength, x, y, z) =>
            creationArgs.Add((xShift, wavelength, x, y, z));

        rig.Controller.StartSpawn();
        Run(loop, 10);

        Assert.NotEmpty(rig.Spawned);
        var prism = rig.Spawned[0];

        // Factory event payload carried the domain and the BaseScale-derived scale:
        // x = 10 * XScaler(1) / 2 - |halfGap|(0) = 5, y = 5, z = 5.
        Assert.Equal(Domains.Ruby, rig.Raised[0].ownDomain);
        Assert.Equal(new Vector3(5f, 5f, 5f), rig.Raised[0].Scale);

        // Prism configuration.
        Assert.Equal(new Vector3(5f, 5f, 5f), prism.TargetScale);
        Assert.Equal("tester", prism.ownerID);
        Assert.Equal(Domains.Ruby, prism.Domain);
        Assert.Equal(0.5f, prism.waitTime); // staged skimmer branch → defaultWaitTime path
        Assert.Contains(prism, rig.Controller.Trail.TrailList);
        Assert.Equal(0, prism.prismProperties.Index);
        Assert.Equal((ushort)rig.Controller.Trail.TrailList.Count, rig.Controller.TrailLength);

        // OnBlockCreated reported the geometry of the first block.
        Assert.Equal((0f, 4f, 5f, 5f, 5f), creationArgs[0]);

        rig.Controller.StopSpawn();
    }

    [Fact]
    public void Gap_SpawnsMirroredTrailPair_AndGetLastTwoBlocksReturnsBoth()
    {
        using var loop = new GameLoop();
        var rig = MakeSpawner();
        rig.Controller.Gap = 2f;

        rig.Controller.StartSpawn();
        Run(loop, 10);

        Assert.True(rig.Spawned.Count >= 2);
        var pair = rig.Controller.GetLastTwoBlocks();
        Assert.NotNull(pair);
        Assert.Equal(2, pair.Count);
        Assert.NotSame(pair[0], pair[1]);

        // halfGap ±1 → scale.x = 10/2 - 1 = 4; xShift = ±(4/2 + 1) = ±3 along ShipTransform.right.
        Assert.Equal(new Vector3(4f, 5f, 5f), rig.Raised[0].Scale);
        Assert.Equal(3f, rig.Raised[0].SpawnPosition.x, 3);
        Assert.Equal(-3f, rig.Raised[1].SpawnPosition.x, 3);

        rig.Controller.StopSpawn();
    }

    [Fact]
    public void TrailCap_RecyclesOldestBlocksBackToPool()
    {
        using var loop = new GameLoop();
        var rig = MakeSpawner();
        rig.Controller.SetMaxTrailBlocks(2);

        rig.Controller.StartSpawn();
        Run(loop, 120); // ≥ 4 spawns at 0.4s wavelength delay

        Assert.True(rig.Spawned.Count >= 4);
        Assert.True(rig.Controller.Trail.TrailList.Count <= 2);
        Assert.Equal(rig.Spawned.Count - rig.Controller.Trail.TrailList.Count, rig.Recycled.Count);
        // Oldest-first recycling: the first spawned block was the first recycled.
        Assert.Same(rig.Spawned[0], rig.Recycled[0]);

        rig.Controller.StopSpawn();
    }

    [Fact]
    public void DangerMode_FlagsBlocks_AssignsMaterial_AndRaisesStaticEvent()
    {
        using var loop = new GameLoop();
        var rig = MakeSpawner();
        var dangerMat = new Material(Shader.Find("danger"));
        var dangerOwners = new List<string>();
        void OnDanger(string owner) => dangerOwners.Add(owner);
        VesselPrismController.OnDangerBlockCreated += OnDanger;
        try
        {
            rig.Controller.EnableDangerMode(dangerMat, Vector3.one); // blendSeconds 0 → sharedMaterial swap
            rig.Controller.StartSpawn();
            Run(loop, 10);

            Assert.NotEmpty(rig.Spawned);
            var prism = rig.Spawned[0];
            Assert.True(prism.prismProperties.IsDangerous);
            Assert.True(prism.TryGetComponent<Renderer>(out var rend));
            Assert.Same(dangerMat, rend.sharedMaterial);
            Assert.Contains("tester", dangerOwners);

            // Disable restores normal block creation.
            rig.Controller.DisableDangerMode();
            int flaggedCount = rig.Spawned.Count;
            Run(loop, 30);
            Assert.True(rig.Spawned.Count > flaggedCount);
            Assert.False(rig.Spawned[^1].prismProperties.IsDangerous);
        }
        finally
        {
            rig.Controller.StopSpawn();
            VesselPrismController.OnDangerBlockCreated -= OnDanger;
        }
    }
}
