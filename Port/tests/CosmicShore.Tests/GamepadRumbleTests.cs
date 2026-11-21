using System.Collections.Generic;
using CosmicShore.Engine.InputSystem;

// Controller haptics did nothing in Amoebius: Gamepad.SetMotorSpeeds only stored the speeds, and
// GLFW (the player's input) has no rumble. A pad now drives its MotorOutput (XInput on Windows);
// these pin what reaches the device and which XInput slot each GLFW joystick is.

public class GamepadRumbleTests
{
    static (Gamepad pad, List<(float, float)> sent) Pad()
    {
        var pad = new Gamepad();
        var sent = new List<(float, float)>();
        pad.MotorOutput = (low, high) => sent.Add((low, high));
        return (pad, sent);
    }

    [Fact]
    public void SetMotorSpeeds_ReachesTheDevice_Clamped()
    {
        var (pad, sent) = Pad();
        pad.SetMotorSpeeds(0.25f, 0.75f);
        pad.SetMotorSpeeds(-1f, 3f);
        Assert.Equal(new[] { (0.25f, 0.75f), (0f, 1f) }, sent.ToArray());
        Assert.Equal((-1f, 3f), pad.MotorSpeeds); // what the game set, as Unity keeps it
    }

    [Fact]
    public void Pause_StopsTheMotors_Resume_RestoresThem_Reset_ClearsThem()
    {
        var (pad, sent) = Pad();
        pad.SetMotorSpeeds(0.5f, 0.6f);
        pad.PauseHaptics();
        pad.SetMotorSpeeds(0.9f, 0.9f); // while paused: kept, not sent
        pad.ResumeHaptics();
        pad.ResetHaptics();
        Assert.Equal(new[] { (0.5f, 0.6f), (0f, 0f), (0.9f, 0.9f), (0f, 0f) }, sent.ToArray());
        Assert.Equal((0f, 0f), pad.MotorSpeeds);
    }

    [Fact]
    public void MotorStarts_CountsEachRumbleFromStill()
    {
        var (pad, _) = Pad();
        pad.SetMotorSpeeds(0.2f, 0f);
        pad.SetMotorSpeeds(0.4f, 0.1f); // already running
        pad.SetMotorSpeeds(0f, 0f);
        pad.SetMotorSpeeds(0f, 0.3f);
        Assert.Equal(2, pad.MotorStarts);
    }

    [Fact]
    public void APadWithNoOutput_StillKeepsItsSpeeds()
    {
        var pad = new Gamepad();
        pad.SetMotorSpeeds(0.3f, 0.4f);
        Assert.Equal((0.3f, 0.4f), pad.MotorSpeeds);
    }

    [Fact]
    public void XInputJoysticks_TakeTheConnectedSlots_InOrder_OtherPadsGetNone()
    {
        var map = XInputSlots.Assign(new (int, string)[]
        {
            (3, "78696e70757401000000000000000000"),  // second XInput pad GLFW found
            (0, "78696e70757401000000000000000000"),  // first
            (1, "030000004c050000cc09000000000000"),  // a DualShock through DirectInput: no XInput slot
        }, new[] { 2, 0 });
        Assert.Equal(new Dictionary<int, int> { [0] = 0, [3] = 2 }, map);
    }

    [Fact]
    public void MoreXInputJoysticksThanSlots_TheExtraOnesGetNone()
    {
        var map = XInputSlots.Assign(new (int, string)[] { (0, "78696e707574"), (1, "78696E707574") }, new[] { 1 });
        Assert.Equal(new Dictionary<int, int> { [0] = 1 }, map);
    }

    [Fact]
    public void MotorSpeed_ToXInputWord()
    {
        Assert.Equal(0, XInputSlots.ToWord(0f));
        Assert.Equal(65535, XInputSlots.ToWord(1f));
        Assert.Equal(32768, XInputSlots.ToWord(0.5f));
        Assert.Equal(65535, XInputSlots.ToWord(2f));
    }
}
