using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Engine.Soap;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;

namespace CosmicShore.Tests;

// V18: AIPilot (autopilot toggle, cell-item target selection, steering math into
// IInputStatus, skill-scaled parameters, ability cadence) and AICinematicBehavior
// (pure input-writing end-game behaviors). Also re-verifies the V18 restores:
// IVesselStatus.AutoPilotEnabled (#10c slice) and the autopilot guards in
// R_VesselActionHandler (button mute) and DriftTrailActionExecutor (altitude mute).

// Concrete CellItem (the original is abstract) for target-selection tests.
class TestCellItem : CellItem { }

// IPlayer whose Vessel/Domain are settable — UpdatePlayerTarget needs enemy players
// with live vessels (the shared StubPlayer pins Vessel to null).
class SeekStubPlayer : IPlayer
{
    public Domains Domain { get; set; } = Domains.Ruby;
    public string Name { get; set; } = "seek-stub";
    public int AvatarId => 0;
    public string PlayerUUID => Name;
    public IVessel Vessel { get; set; }
    public InputController InputController => null;
    public IInputStatus InputStatus => null;
    public IRoundStats RoundStats => null;
    public bool IsActive => true;
    public bool IsInitializedAsAI => false;
    public bool IsMultiplayerOwner => false;
    public bool IsNetworkOwner => false;
    public bool IsNetworkClient => false;
    public bool IsLocalUser => false;
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

public class AIPilotTests
{
    static readonly System.Reflection.BindingFlags Priv =
        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;

    static void Set(object target, string field, object value)
        => target.GetType().GetField(field, Priv)!.SetValue(target, value);

    static T Get<T>(object target, string field)
        => (T)target.GetType().GetField(field, Priv)!.GetValue(target)!;

    // ----------------------------------------------------------------- pilot rig

    sealed class PilotRig
    {
        public AIPilot Pilot;
        public GameObject GameObject;
        public CellRuntimeDataSO CellData;
        public ScriptableEventNoParam CellItemsUpdated;
        public StubVessel Vessel;
        public StubVesselStatus Status;
        public InputStatus Input;
        public StubPlayer Player;
    }

    /// <summary>
    /// Fully-assembled AIPilot on its own GameObject (configure-before-activation:
    /// OnEnable subscribes to the serialized SOAP event, fail-loud policy).
    /// </summary>
    static PilotRig MakePilot(bool initialize = true)
    {
        var rig = new PilotRig
        {
            CellItemsUpdated = ScriptableObject.CreateInstance<ScriptableEventNoParam>(),
            CellData = ScriptableObject.CreateInstance<CellRuntimeDataSO>(),
        };

        var inputGo = new GameObject("input");
        rig.Input = inputGo.AddComponent<InputStatus>();

        rig.Player = new StubPlayer { Name = "ai-tester", Domain = Domains.Jade };
        rig.Status = new StubVesselStatus { InputStatus = rig.Input, Player = rig.Player };

        var go = new GameObject("aiVessel");
        go.SetActive(false);
        rig.Pilot = go.AddComponent<AIPilot>();
        Set(rig.Pilot, "cellData", rig.CellData);
        Set(rig.Pilot, "OnCellItemsUpdated", rig.CellItemsUpdated);
        Set(rig.Pilot, "abilities", new List<AIAbility>());
        go.SetActive(true);

        rig.GameObject = go;
        rig.Vessel = new StubVessel { Transform = go.transform, VesselStatus = rig.Status };
        rig.Status.Vessel = rig.Vessel;

        if (initialize) rig.Pilot.Initialize(rig.Vessel);
        return rig;
    }

    static TestCellItem MakeItem(ItemType type, Domains domain, Vector3 position)
    {
        var go = new GameObject($"item {type} {domain}");
        go.transform.position = position;
        var item = go.AddComponent<TestCellItem>();
        item.ItemType = type;
        item.ownDomain = domain;
        return item;
    }

    // ----------------------------------------------------------------- restore surface

    [Fact]
    public void IVesselStatus_AIMembers_AreRestored()
    {
        // #10c V18 slice: the three members are back on the interface, verbatim types.
        Assert.Equal(typeof(AIPilot), typeof(IVesselStatus).GetProperty("AIPilot")!.PropertyType);
        Assert.Equal(typeof(AICinematicBehavior), typeof(IVesselStatus).GetProperty("AICinematicBehavior")!.PropertyType);
        var autoPilot = typeof(IVesselStatus).GetProperty("AutoPilotEnabled");
        Assert.NotNull(autoPilot);
        Assert.Equal(typeof(bool), autoPilot!.PropertyType);
        Assert.False(autoPilot.GetGetMethod()!.IsAbstract); // default interface member
    }

    // ----------------------------------------------------------------- toggle

    [Fact]
    public void StartStopAIPilot_TogglesAutoPilotEnabled()
    {
        using var loop = new GameLoop();
        var rig = MakePilot();

        Assert.False(rig.Pilot.AutoPilotEnabled);
        rig.Pilot.StartAIPilot();
        Assert.True(rig.Pilot.AutoPilotEnabled);
        rig.Pilot.StopAIPilot();
        Assert.False(rig.Pilot.AutoPilotEnabled);
    }

    [Fact]
    public void Update_WhileDisabledOrStationary_WritesNoInput()
    {
        using var loop = new GameLoop();
        var rig = MakePilot();
        Set(rig.Pilot, "_targetPosition", new Vector3(100f, 0f, 0f));

        loop.Tick(1f / 60f); // autopilot off
        Assert.Equal(0f, rig.Input.XSum);
        Assert.Equal(0f, rig.Input.XDiff);

        rig.Pilot.StartAIPilot();
        rig.Status.IsStationary = true;
        loop.Tick(1f / 60f); // stationary gate
        Assert.Equal(0f, rig.Input.XSum);
        Assert.Equal(0f, rig.Input.XDiff);
    }

    // ----------------------------------------------------------------- steering math

    [Fact]
    public void Update_SteersTowardTarget_DualStick()
    {
        using var loop = new GameLoop();
        var rig = MakePilot();
        // Target hard right of a +Z-facing vessel at the origin: cross(forward, desired)
        // = +Y, distance² caps the Asin argument at 1 → 90° → full-rail yaw inputs.
        Set(rig.Pilot, "_targetPosition", new Vector3(100f, 0f, 0f));
        rig.Pilot.StartAIPilot();

        loop.Tick(1f / 60f);

        Assert.Equal(1f, rig.Input.XSum);
        Assert.Equal(0f, rig.Input.YSum);
        Assert.Equal(1f, rig.Input.YDiff);
        Assert.Equal(0.6f, rig.Input.XDiff, 3); // throttle == defaultThrottle on first frame
    }

    [Fact]
    public void Update_SteersTowardTarget_SingleStick()
    {
        using var loop = new GameLoop();
        var rig = MakePilot();
        rig.Status.IsSingleStickControls = true;
        Set(rig.Pilot, "_targetPosition", new Vector3(100f, 0f, 0f));
        rig.Pilot.StartAIPilot();

        loop.Tick(1f / 60f);

        Assert.Equal(1f, rig.Input.EasedLeftJoystickPosition.x);
        Assert.Equal(0f, rig.Input.EasedLeftJoystickPosition.y);
        Assert.Equal(0f, rig.Input.XDiff); // dual-stick throttle untouched
    }

    [Fact]
    public void Update_AlignedWithDriftEnabled_LocksCourseAndSteersAwayFromTarget()
    {
        using var loop = new GameLoop();
        var rig = MakePilot();
        Set(rig.Pilot, "drift", true);
        Set(rig.Pilot, "_targetPosition", new Vector3(100f, 0f, 0f));
        rig.Status.Course = new Vector3(1f, 0f, 0f); // already looking at the crystal
        rig.Pilot.StartAIPilot();

        loop.Tick(1f / 60f);

        // Drift entry: course locked onto the crystal, steering target inverted.
        Assert.Equal(new Vector3(1f, 0f, 0f), rig.Status.Course);
        Assert.Equal(-1f, rig.Input.XSum);
        Assert.Equal(-1f, rig.Input.YDiff);
    }

    [Fact]
    public void Update_RamEnabled_FullThrottleWhenLookingAtCrystal()
    {
        using var loop = new GameLoop();
        var rig = MakePilot();
        Set(rig.Pilot, "ram", true);
        Set(rig.Pilot, "_targetPosition", new Vector3(100f, 0f, 0f));
        rig.Status.Course = new Vector3(1f, 0f, 0f);
        rig.Status.IsDrifting = true; // already drifting: stays in the inverted branch
        rig.Pilot.StartAIPilot();

        loop.Tick(1f / 60f);

        Assert.Equal(1f, rig.Input.XDiff); // ram overrides the throttle ramp
    }

    // ----------------------------------------------------------------- skill scaling

    [Fact]
    public void SkillLevel_LerpsParameterRanges_AndConfigureClampsSkill()
    {
        using var loop = new GameLoop();
        var rig = MakePilot(initialize: false);
        Set(rig.Pilot, "defaultThrottleLow", 0.2f);
        Set(rig.Pilot, "defaultThrottleHigh", 1.0f);
        Set(rig.Pilot, "defaultAggressivenessLow", 0.01f);
        Set(rig.Pilot, "defaultAggressivenessHigh", 0.05f);

        rig.Pilot.ConfigureForGameMode(null, false, 0.5f);
        Assert.Equal(0.6f, rig.Pilot.defaultThrottle, 4);
        Assert.Equal(0.03f, rig.Pilot.defaultAggressiveness, 4);

        rig.Pilot.ConfigureForGameMode(null, false, 7f); // clamps to 1
        Assert.Equal(1.0f, rig.Pilot.defaultThrottle, 4);
        Assert.Equal(0.05f, rig.Pilot.defaultAggressiveness, 4);

        rig.Pilot.ConfigureForGameMode(null, false, -3f); // clamps to 0
        Assert.Equal(0.2f, rig.Pilot.defaultThrottle, 4);
        Assert.Equal(0.01f, rig.Pilot.defaultAggressiveness, 4);
    }

    [Fact]
    public void Initialize_SeedsThrottleFromSkill_SoFirstFrameUsesIt()
    {
        using var loop = new GameLoop();
        var rig = MakePilot(initialize: false);
        Set(rig.Pilot, "defaultThrottleLow", 0.2f);
        Set(rig.Pilot, "defaultThrottleHigh", 1.0f);
        rig.Pilot.ConfigureForGameMode(null, false, 1f);
        rig.Pilot.Initialize(rig.Vessel);

        Set(rig.Pilot, "_targetPosition", new Vector3(100f, 0f, 0f));
        rig.Pilot.StartAIPilot();
        loop.Tick(1f / 60f);

        Assert.Equal(1.0f, rig.Input.XDiff, 3); // max-skill throttle
    }

    // ----------------------------------------------------------------- target selection

    [Fact]
    public void UpdateCellContent_PicksNearestEligibleItem_FilteringByDomain()
    {
        using var loop = new GameLoop();
        var rig = MakePilot(); // pilot domain: Jade

        var ownDebuff     = MakeItem(ItemType.Debuff, Domains.Jade, new Vector3(0f, 0f, 5f));  // own debuff: skipped
        var foreignBuff   = MakeItem(ItemType.Buff,   Domains.Ruby, new Vector3(0f, 0f, 6f));  // foreign buff: skipped
        var foreignDebuff = MakeItem(ItemType.Debuff, Domains.Ruby, new Vector3(0f, 0f, 50f)); // disguised: eligible
        var blueBuff      = MakeItem(ItemType.Buff,   Domains.Blue, new Vector3(0f, 0f, 20f)); // neutral: eligible, nearer

        rig.CellData.CellItems.AddRange(new CellItem[] { ownDebuff, foreignBuff, foreignDebuff, blueBuff });
        rig.CellItemsUpdated.Raise();

        Assert.Equal(new Vector3(0f, 0f, 20f), Get<Vector3>(rig.Pilot, "_targetPosition"));
    }

    [Fact]
    public void UpdateCellContent_BlueDomainPilot_ChasesForeignBuffsFreely()
    {
        using var loop = new GameLoop();
        var rig = MakePilot();
        rig.Player.Domain = Domains.Blue; // Menu_Main autopilot before domain pick

        var foreignBuff = MakeItem(ItemType.Buff, Domains.Ruby, new Vector3(0f, 0f, 7f));
        rig.CellData.CellItems.Add(foreignBuff);
        rig.CellItemsUpdated.Raise();

        Assert.Equal(new Vector3(0f, 0f, 7f), Get<Vector3>(rig.Pilot, "_targetPosition"));
    }

    [Fact]
    public void UpdateCellContent_NoEligibleItems_FallsBackToCellCenter()
    {
        using var loop = new GameLoop();
        var rig = MakePilot();

        var cellGo = new GameObject("cell");
        cellGo.SetActive(false); // data anchor only; Cell.Awake needs full scene wiring
        cellGo.transform.position = new Vector3(0f, 0f, 200f);
        rig.CellData.Cell = cellGo.AddComponent<Cell>();

        rig.CellData.CellItems.Add(MakeItem(ItemType.Debuff, Domains.Jade, new Vector3(0f, 0f, 5f))); // own debuff only
        rig.CellItemsUpdated.Raise();

        Assert.Equal(new Vector3(0f, 0f, 200f), Get<Vector3>(rig.Pilot, "_targetPosition"));
    }

    [Fact]
    public void SeekPlayers_TargetsNearestEnemyVessel_IgnoringTeammates()
    {
        using var loop = new GameLoop();
        var rig = MakePilot();

        var gameData = ScriptableObject.CreateInstance<GameDataSO>();
        IVessel MakeVesselAt(Vector3 pos)
        {
            var go = new GameObject("enemyVessel");
            go.transform.position = pos;
            return new StubVessel { Transform = go.transform };
        }
        gameData.Players.Add(new SeekStubPlayer { Domain = Domains.Jade, Vessel = MakeVesselAt(new Vector3(0f, 0f, 1f)) });  // teammate
        gameData.Players.Add(new SeekStubPlayer { Domain = Domains.Ruby, Vessel = MakeVesselAt(new Vector3(0f, 0f, 30f)) }); // nearest enemy
        gameData.Players.Add(new SeekStubPlayer { Domain = Domains.Gold, Vessel = MakeVesselAt(new Vector3(0f, 0f, 80f)) }); // far enemy
        gameData.Players.Add(new SeekStubPlayer { Domain = Domains.Ruby, Vessel = null });                                   // vessel-less: skipped

        rig.Pilot.ConfigureForGameMode(gameData, shouldSeekPlayers: true, skill: 1f);
        rig.Pilot.StartAIPilot(); // UpdatePlayerTarget runs synchronously to its first yield

        Assert.Equal(new Vector3(0f, 0f, 30f), Get<Vector3>(rig.Pilot, "_targetPosition"));

        // Cell-item updates are ignored while seeking players.
        rig.CellData.CellItems.Add(MakeItem(ItemType.Buff, Domains.Blue, new Vector3(0f, 0f, 2f)));
        rig.CellItemsUpdated.Raise();
        Assert.Equal(new Vector3(0f, 0f, 30f), Get<Vector3>(rig.Pilot, "_targetPosition"));

        rig.Pilot.StopAIPilot(); // while-guard ends the seek coroutine on next resume
    }

    // ----------------------------------------------------------------- ability cadence

    [Fact]
    public void Abilities_CloneOnInitialize_ThenCycleStartStopWhileEnabled()
    {
        using var loop = new GameLoop();
        var rig = MakePilot(initialize: false);
        var asset = ScriptableObject.CreateInstance<RecordingAction>();
        Set(rig.Pilot, "abilities", new List<AIAbility>
        {
            new() { Ability = asset, Duration = 1f, Cooldown = 1f }
        });

        rig.Pilot.Initialize(rig.Vessel);

        var abilities = Get<List<AIAbility>>(rig.Pilot, "abilities");
        var inst = Assert.IsType<RecordingAction>(abilities[0].Ability);
        Assert.NotSame(asset, inst);                       // runtime clone, asset untouched
        Assert.Contains("[AI:ai-tester]", inst.name);
        Assert.Equal(1, inst.Initializes);
        Assert.Same(rig.Status, inst.LastInitialized);
        Assert.Equal(0, asset.Initializes);

        rig.Pilot.StartAIPilot();
        loop.Run(13, 0.25f); // 3.25s: past the 3s warm-up → first StartAction
        Assert.Equal(1, inst.Starts);
        Assert.Equal(0, inst.Stops);

        loop.Run(4, 0.25f);  // 4.25s: past Duration → StopAction
        Assert.Equal(1, inst.Stops);

        loop.Run(4, 0.25f);  // 5.25s: past Cooldown → second StartAction
        Assert.Equal(2, inst.Starts);

        rig.Pilot.StopAIPilot(); // while-guard ends the cycle at the next resume
        loop.Run(8, 0.25f);
        Assert.Equal(2, inst.Stops);   // in-flight iteration finishes its stop...
        Assert.Equal(2, inst.Starts);  // ...but no new start is issued
    }

    // ----------------------------------------------------------------- cinematic behavior

    static (AICinematicBehavior beh, StubVesselStatus status, InputStatus input) MakeCinematic()
    {
        var inputGo = new GameObject("input");
        var input = inputGo.AddComponent<InputStatus>();
        var status = new StubVesselStatus { InputStatus = input };
        var go = new GameObject("cinematic");
        var beh = go.AddComponent<AICinematicBehavior>();
        beh.Initialize(status, null);
        return (beh, status, input);
    }

    [Fact]
    public void Cinematic_MoveForward_FullThrottleCenteredSteering()
    {
        using var loop = new GameLoop();
        var (beh, _, input) = MakeCinematic();
        input.XSum = 0.7f; // residue that MoveForward must zero out

        beh.StartCinematicBehavior(AICinematicBehaviorType.MoveForward);
        loop.Tick(1f / 60f);

        Assert.Equal(1f, input.XDiff);
        Assert.Equal(0f, input.XSum);
        Assert.Equal(0f, input.YSum);
    }

    [Fact]
    public void Cinematic_MoveForward_SingleStick_CentersJoystick()
    {
        using var loop = new GameLoop();
        var (beh, status, input) = MakeCinematic();
        status.IsSingleStickControls = true;
        input.EasedLeftJoystickPosition = new Vector2(0.5f, -0.5f);

        beh.StartCinematicBehavior(AICinematicBehaviorType.MoveForward);
        loop.Tick(1f / 60f);

        Assert.Equal(0f, input.EasedLeftJoystickPosition.x);
        Assert.Equal(0f, input.EasedLeftJoystickPosition.y);
    }

    [Fact]
    public void Cinematic_UnimplementedBehaviors_FallBackToMoveForward()
    {
        using var loop = new GameLoop();
        var (beh, _, input) = MakeCinematic();

        beh.StartCinematicBehavior(AICinematicBehaviorType.BarrelRoll);
        loop.Tick(1f / 60f);

        Assert.Equal(1f, input.XDiff);
    }

    [Fact]
    public void Cinematic_Drift_HalfThrottle()
    {
        using var loop = new GameLoop();
        var (beh, _, input) = MakeCinematic();

        beh.StartCinematicBehavior(AICinematicBehaviorType.Drift);
        loop.Tick(1f / 60f);

        Assert.Equal(0.5f, input.XDiff);
    }

    [Fact]
    public void Cinematic_Loop_SteersAroundCircleAtCruiseThrottle()
    {
        using var loop = new GameLoop();
        var (beh, _, input) = MakeCinematic();

        beh.StartCinematicBehavior(AICinematicBehaviorType.Loop);
        loop.Run(2, 1f / 60f); // elapsed > 0 → loop offset swings toward +X

        Assert.Equal(0.8f, input.XDiff);
        Assert.True(input.XSum > 0f, $"expected positive yaw into the loop, got {input.XSum}");
    }

    [Fact]
    public void Cinematic_Stop_FreezesInputWrites()
    {
        using var loop = new GameLoop();
        var (beh, _, input) = MakeCinematic();

        beh.StartCinematicBehavior(AICinematicBehaviorType.MoveForward);
        loop.Tick(1f / 60f);
        Assert.Equal(1f, input.XDiff);

        beh.StopCinematicBehavior();
        input.XDiff = 0f;
        loop.Tick(1f / 60f);
        Assert.Equal(0f, input.XDiff); // no further writes after stop
    }

    // ----------------------------------------------------------------- restored guards

    /// <summary>Bare pilot suitable for driving IVesselStatus.AutoPilotEnabled.</summary>
    static AIPilot MakeBarePilot()
    {
        var go = new GameObject("guardPilot");
        go.SetActive(false);
        var pilot = go.AddComponent<AIPilot>();
        Set(pilot, "cellData", ScriptableObject.CreateInstance<CellRuntimeDataSO>());
        Set(pilot, "OnCellItemsUpdated", ScriptableObject.CreateInstance<ScriptableEventNoParam>());
        Set(pilot, "abilities", new List<AIAbility>());
        go.SetActive(true);
        return pilot;
    }

    [Fact]
    public void RestoredGuard_ActionHandler_MutesButtonEventsWhileAutopilotEngaged()
    {
        using var loop = new GameLoop();

        var inputGo = new GameObject("input");
        var input = inputGo.AddComponent<InputStatus>();
        input.ActiveInputDevice = InputDeviceType.Keyboard;
        var status = new StubVesselStatus
        {
            Player = new StubPlayer { Name = "guard", IsLocalUser = false },
            InputStatus = input,
        };

        var action = ScriptableObject.CreateInstance<RecordingAction>();
        var pressed = ScriptableObject.CreateInstance<ScriptableEventInputEvents>();
        var released = ScriptableObject.CreateInstance<ScriptableEventInputEvents>();

        var go = new GameObject("handler");
        var handler = go.AddComponent<R_VesselActionHandler>();
        Set(handler, "_inputEventShipActions", new List<InputEventShipActionMapping>
        {
            new() { InputEvent = InputEvents.Button1Action, ShipActions = new List<ShipActionSO> { action } },
        });
        Set(handler, "_touchActionOverrides", new List<InputEventShipActionMapping>());
        Set(handler, "_gamepadActionOverrides", new List<InputEventShipActionMapping>());
        Set(handler, "_resourceEventClassActions", new List<ResourceEventShipActionMapping>());
        Set(handler, "_onButtonPressed", pressed);
        Set(handler, "_onButtonReleased", released);
        Set(handler, "onAbilityExecuted", ScriptableObject.CreateInstance<ScriptableEventAbilityStats>());
        handler.Initialize(status);
        handler.ToggleSubscription(true);

        var pilot = MakeBarePilot();
        status.AIPilot = pilot; // restored chain: IVesselStatus.AutoPilotEnabled → AIPilot

        pilot.StartAIPilot();
        pressed.Raise(InputEvents.Button1Action);
        released.Raise(InputEvents.Button1Action);
        Assert.Equal(0, action.Starts); // muted while the AI is flying
        Assert.Equal(0, action.Stops);

        pilot.StopAIPilot();
        pressed.Raise(InputEvents.Button1Action);
        released.Raise(InputEvents.Button1Action);
        Assert.Equal(1, action.Starts); // human input flows again
        Assert.Equal(1, action.Stops);
    }

    [Fact]
    public void RestoredGuard_DriftAltitudeEvents_MutedWhileAutopilotEngaged()
    {
        using var loop = new GameLoop();

        var vesselGo = new GameObject("driftVessel"); // identity rotation → forward = +Z
        var vessel = new StubVessel { Transform = vesselGo.transform };
        var status = new StubVesselStatus { Vessel = vessel, Course = new Vector3(0f, 0f, 0.25f) };
        vessel.VesselStatus = status;

        var go = new GameObject("executor");
        go.SetActive(false); // wire serialized fields before OnEnable subscribes
        var registry = go.AddComponent<ActionExecutorRegistry>();
        var exec = go.AddComponent<DriftTrailActionExecutor>();
        Set(exec, "OnMiniGameTurnEnd", ScriptableObject.CreateInstance<ScriptableEventNoParam>());
        Set(registry, "_executors", new List<ShipActionExecutorBase> { exec });
        go.SetActive(true);
        registry.InitializeAll(status);

        var so = ScriptableObject.CreateInstance<DriftTrailActionSO>();
        var values = new List<float>();
        exec.OnChangeDriftAltitude += v => values.Add(v);

        var pilot = MakeBarePilot();
        status.AIPilot = pilot;
        pilot.StartAIPilot();

        so.StartAction(registry, status);
        loop.Run(4, 0.05f);
        Assert.Empty(values);           // loop runs, altitude events muted

        so.StopAction(registry, status);
        Assert.Empty(values);           // level-flight signal muted too

        pilot.StopAIPilot();            // autopilot off: events flow again
        so.StartAction(registry, status);
        Assert.Single(values);          // sync first pass fires immediately
        Assert.Equal(0.25f, values[0], 4);
        so.StopAction(registry, status);
        Assert.Equal(1f, values[^1]);
    }
}
