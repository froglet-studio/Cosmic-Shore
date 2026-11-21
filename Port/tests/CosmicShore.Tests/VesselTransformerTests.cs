using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Gameplay;

namespace CosmicShore.Tests;

// V8: VesselTransformer — the flight model. Throttle/boost speed composition,
// course/drift decoupling, throttle + velocity modifiers, pose controls, reset.
public class VesselTransformerTests
{
    static (VesselTransformer xf, StubVessel vessel, StubVesselStatus status, InputStatus input, GameObject go)
        Make()
    {
        var inputGo = new GameObject("input");
        var input = inputGo.AddComponent<InputStatus>();

        var status = new StubVesselStatus { InputStatus = input };
        var vessel = new StubVessel { VesselStatus = status };

        var go = new GameObject("vessel");
        var xf = go.AddComponent<VesselTransformer>();
        xf.Initialize(vessel);
        xf.ResetTransformer();
        xf.ToggleActive(true);
        return (xf, vessel, status, input, go);
    }

    static void Run(GameLoop loop, int frames)
    {
        for (int i = 0; i < frames; i++) loop.Tick(1f / 60f);
    }

    [Fact]
    public void MoveShip_ThrottleConvergesOnScalerPlusMinimum_AndWritesStatus()
    {
        using var loop = new GameLoop();
        var (xf, _, status, input, go) = Make();
        input.XDiff = 1f;

        Run(loop, 600); // LERP 1.5/s → ~10s converges

        // target = XDiff * 50 * 1 + 10 = 60
        Assert.InRange(status.Speed, 58f, 60.5f);
        Assert.InRange(go.transform.position.z, 100f, 600f); // advanced along +forward
        Assert.Equal(0f, go.transform.position.x, 2);
        Assert.Equal(Vector3.forward, status.Course);        // gripped: course == nose
    }

    [Fact]
    public void MoveShip_BoostMultipliesThrottleTerm()
    {
        using var loop = new GameLoop();
        var (xf, _, status, input, _) = Make();
        input.XDiff = 1f;
        status.IsBoosting = true;
        status.BoostMultiplier = 2f;

        Run(loop, 600);

        // target = 1 * 50 * 2 + 10 = 110
        Assert.InRange(status.Speed, 105f, 110.5f);
    }

    [Fact]
    public void ModifyThrottle_Slow_SetsIsSlowed_ThenExpiresAndRestores()
    {
        using var loop = new GameLoop();
        var (xf, vessel, status, input, _) = Make();
        input.XDiff = 0.5f;

        xf.ModifyThrottle(0.4f, 0.5f); // 60% slow for half a second
        Run(loop, 6);

        Assert.True(status.IsSlowed);
        Assert.True(vessel.SlowedAdds > 0);
        Assert.True(xf.SpeedMultiplier < 1f);

        Run(loop, 40); // expire (0.5s + margin)

        Assert.False(status.IsSlowed);
        Assert.True(vessel.SlowedRemoves > 0);
        Assert.Equal(1f, xf.SpeedMultiplier, 2);
    }

    [Fact]
    public void ModifyVelocity_ShiftsPositionOffAxis_ThenExpires()
    {
        using var loop = new GameLoop();
        var (xf, _, _, input, go) = Make();
        input.XDiff = 0f;

        xf.ModifyVelocity(new Vector3(40f, 0f, 0f), 0.4f);
        Run(loop, 12);
        float xDuringPush = go.transform.position.x;
        Assert.True(xDuringPush > 0.5f); // nudged sideways

        Run(loop, 30); // expires
        float xAfter = go.transform.position.x;
        Run(loop, 30);
        Assert.Equal(xAfter, go.transform.position.x, 1); // no further sideways motion
    }

    [Fact]
    public void AnalogDrift_FullTrigger_ScalesRotationAndDamping()
    {
        using var loop = new GameLoop();
        var (xf, _, status, input, _) = Make();
        input.ActiveInputDevice = InputDeviceType.Gamepad;
        float basePitch = xf.PitchScaler;

        xf.BeginDrift(rotMult: 1.5f, dampTarget: 0.05f, isSharp: false);
        status.IsDrifting = true;
        input.LeftTriggerAnalog = 1f; // trigger sum 1 → full single drift
        Run(loop, 2);

        Assert.Equal(basePitch * 1.5f, xf.PitchScaler, 2);
        Assert.Equal(0.05f, xf.DriftDamping, 3);
        Assert.True(xf.IsDriftActive);
    }

    [Fact]
    public void AnalogDrift_DecouplesCourseFromNose_WhileTurning()
    {
        using var loop = new GameLoop();
        var (xf, _, status, input, go) = Make();
        input.ActiveInputDevice = InputDeviceType.Gamepad;
        input.XDiff = 1f;
        Run(loop, 120); // get moving straight

        xf.BeginDrift(rotMult: 1f, dampTarget: 0.05f, isSharp: false);
        status.IsDrifting = true;
        input.LeftTriggerAnalog = 1f;
        input.XSum = 1f; // hard yaw while drifting
        Run(loop, 30);

        float align = Vector3.Dot(status.Course, go.transform.forward);
        Assert.True(align < 0.995f, $"course should lag the nose while drifting (dot {align})");
    }

    [Fact]
    public void EndDrift_OnGamepad_RestoresBaseImmediately()
    {
        using var loop = new GameLoop();
        var (xf, _, status, input, _) = Make();
        input.ActiveInputDevice = InputDeviceType.Gamepad;
        float basePitch = xf.PitchScaler;

        xf.BeginDrift(1.5f, 0.05f, isSharp: false);
        status.IsDrifting = true;
        input.LeftTriggerAnalog = 1f;
        Run(loop, 2);
        Assert.NotEqual(basePitch, xf.PitchScaler);

        xf.EndDrift(isSharp: false);
        status.IsDrifting = false;
        input.LeftTriggerAnalog = 0f;
        Run(loop, 2);

        Assert.Equal(basePitch, xf.PitchScaler, 2);
        Assert.Equal(0f, xf.DriftDamping, 3);
        Assert.False(xf.IsDriftActive);
    }

    [Fact]
    public void SetPose_PlacesShip_AndRotationInputsAccumulateFromIt()
    {
        using var loop = new GameLoop();
        var (xf, _, _, input, go) = Make();
        var pose = new Pose(new Vector3(5f, 6f, 7f), Quaternion.AngleAxis(90f, Vector3.up));

        xf.SetPose(pose);

        Assert.Equal(5f, go.transform.position.x, 3);
        Assert.Equal(7f, go.transform.position.z, 3);
        // nose now +x: with no input the rotation holds
        input.XDiff = 1f;
        Run(loop, 240);
        Assert.True(go.transform.position.x > 50f, "should fly along the posed heading");
    }

    [Fact]
    public void ResetTransformer_RestoresDefaults()
    {
        using var loop = new GameLoop();
        var (xf, _, status, input, go) = Make();
        input.XDiff = 1f;
        Run(loop, 120);
        xf.ModifyThrottle(3f, 5f);
        xf.BeginDrift(2f, 0.1f, isSharp: true);

        xf.ResetTransformer();

        Assert.Equal(xf.DefaultMinimumSpeed, xf.MinimumSpeed, 3);
        Assert.Equal(xf.DefaultThrottleScaler, xf.ThrottleScaler, 3);
        Assert.Equal(1f, xf.SpeedMultiplier, 3);
        Assert.False(xf.IsDriftActive);
        Assert.Equal(Quaternion.identity, go.transform.rotation);
    }

    [Fact]
    public void Update_Inactive_OrStationary_DoesNotMove()
    {
        using var loop = new GameLoop();
        var (xf, _, status, input, go) = Make();
        input.XDiff = 1f;

        xf.ToggleActive(false);
        Run(loop, 30);
        Assert.Equal(0f, go.transform.position.z, 3);

        xf.ToggleActive(true);
        status.IsStationary = true;
        Run(loop, 30);
        Assert.Equal(0f, go.transform.position.z, 3);
    }
}
