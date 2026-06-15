using System;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    public enum ControllerMappingPresetId
    {
        XboxWindows = 0,
        AppleMfi = 1,
        NintendoSwitch = 2,
        SteamDeck = 3,
        AndroidGeneric = 4,
        Custom = 100,
    }

    public enum GamepadVectorSource
    {
        None = 0,
        LeftStick = 1,
        RightStick = 2,
        Dpad = 3,
    }

    public enum GamepadButtonSource
    {
        None = 0,
        ButtonSouth = 1,
        ButtonEast = 2,
        ButtonWest = 3,
        ButtonNorth = 4,
        LeftShoulder = 5,
        RightShoulder = 6,
        LeftTrigger = 7,
        RightTrigger = 8,
        Select = 9,
        Start = 10,
        LeftStickPress = 11,
        RightStickPress = 12,
        DpadUp = 13,
        DpadDown = 14,
        DpadLeft = 15,
        DpadRight = 16,
    }

    [Serializable]
    public class ControllerMappingProfile
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public ControllerMappingPresetId presetId = ControllerMappingPresetId.XboxWindows;
        public string displayName = "Xbox / Windows";

        public GamepadVectorSource leftStick = GamepadVectorSource.LeftStick;
        public GamepadVectorSource rightStick = GamepadVectorSource.RightStick;

        public GamepadButtonSource button1 = GamepadButtonSource.ButtonSouth;
        public GamepadButtonSource button2 = GamepadButtonSource.ButtonEast;
        public GamepadButtonSource button3 = GamepadButtonSource.ButtonWest;
        public GamepadButtonSource flip = GamepadButtonSource.RightShoulder;
        public GamepadButtonSource throttle = GamepadButtonSource.RightShoulder;
        public GamepadButtonSource leftTrigger = GamepadButtonSource.LeftTrigger;
        public GamepadButtonSource rightTrigger = GamepadButtonSource.RightTrigger;

        public bool invertLeftStickX;
        public bool invertLeftStickY;
        public bool invertRightStickX;
        public bool invertRightStickY;

        public float stickDeadzone = 0.08f;
        public float buttonPressPoint = 0.05f;

        public ControllerMappingProfile Clone()
        {
            return JsonUtility.FromJson<ControllerMappingProfile>(JsonUtility.ToJson(this));
        }
    }
}
