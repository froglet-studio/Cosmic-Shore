using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    public static class ControllerMappingStore
    {
        private const string PlayerPrefsKey = "ControllerMappingProfileJson";

        private static bool loaded;
        private static ControllerMappingProfile current = ControllerMappingPresets.GetPreset(ControllerMappingPresetId.XboxWindows);

        public static event Action<ControllerMappingProfile> OnMappingChanged;

        public static ControllerMappingProfile Current
        {
            get
            {
                EnsureLoaded();
                return current;
            }
        }

        public static void EnsureLoaded()
        {
            if (loaded)
                return;

            loaded = true;
            LoadFromPlayerPrefs();
        }

        public static ControllerMappingProfile LoadFromPlayerPrefs()
        {
            var json = PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);
            current = Parse(json) ?? ControllerMappingPresets.GetPreset(ControllerMappingPresetId.XboxWindows);
            Sanitize(current);
            return current;
        }

        public static string ExportJson()
        {
            EnsureLoaded();
            return JsonUtility.ToJson(current);
        }

        public static bool ImportJson(string json, bool saveToPlayerPrefs)
        {
            var parsed = Parse(json);
            if (parsed == null)
                return false;

            SetCurrent(parsed, saveToPlayerPrefs);
            return true;
        }

        public static ControllerMappingProfile ApplyPreset(ControllerMappingPresetId presetId, bool saveToPlayerPrefs)
        {
            var profile = ControllerMappingPresets.GetPreset(presetId);
            SetCurrent(profile, saveToPlayerPrefs);
            return profile;
        }

        public static void SaveCustom(ControllerMappingProfile profile, bool saveToPlayerPrefs)
        {
            if (profile == null)
                return;

            profile.presetId = ControllerMappingPresetId.Custom;
            if (string.IsNullOrWhiteSpace(profile.displayName))
                profile.displayName = "Custom";

            SetCurrent(profile, saveToPlayerPrefs);
        }

        public static IReadOnlyList<ControllerMappingPresetId> PresetIds => ControllerMappingPresets.PresetIds;

        private static void SetCurrent(ControllerMappingProfile profile, bool saveToPlayerPrefs)
        {
            loaded = true;
            Sanitize(profile);
            current = profile.Clone();

            if (saveToPlayerPrefs)
            {
                PlayerPrefs.SetString(PlayerPrefsKey, JsonUtility.ToJson(current));
                PlayerPrefs.Save();
            }

            OnMappingChanged?.Invoke(current.Clone());
        }

        private static ControllerMappingProfile Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            try
            {
                return JsonUtility.FromJson<ControllerMappingProfile>(json);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ControllerMappingStore] Failed to parse controller mapping: {ex.Message}");
                return null;
            }
        }

        private static void Sanitize(ControllerMappingProfile profile)
        {
            if (profile == null)
                return;

            profile.schemaVersion = ControllerMappingProfile.CurrentSchemaVersion;

            if (profile.leftStick == GamepadVectorSource.None)
                profile.leftStick = GamepadVectorSource.LeftStick;
            if (profile.rightStick == GamepadVectorSource.None)
                profile.rightStick = GamepadVectorSource.RightStick;

            if (profile.button1 == GamepadButtonSource.None)
                profile.button1 = GamepadButtonSource.ButtonSouth;
            if (profile.button2 == GamepadButtonSource.None)
                profile.button2 = GamepadButtonSource.ButtonEast;
            if (profile.button3 == GamepadButtonSource.None)
                profile.button3 = GamepadButtonSource.ButtonWest;
            if (profile.flip == GamepadButtonSource.None)
                profile.flip = GamepadButtonSource.RightShoulder;
            if (profile.throttle == GamepadButtonSource.None)
                profile.throttle = GamepadButtonSource.RightShoulder;
            if (profile.leftTrigger == GamepadButtonSource.None)
                profile.leftTrigger = GamepadButtonSource.LeftTrigger;
            if (profile.rightTrigger == GamepadButtonSource.None)
                profile.rightTrigger = GamepadButtonSource.RightTrigger;

            profile.stickDeadzone = Mathf.Clamp(profile.stickDeadzone, 0f, 0.75f);
            profile.buttonPressPoint = Mathf.Clamp(profile.buttonPressPoint, 0.01f, 0.95f);
        }
    }

    public static class ControllerMappingPresets
    {
        public static readonly IReadOnlyList<ControllerMappingPresetId> PresetIds = new[]
        {
            ControllerMappingPresetId.XboxWindows,
            ControllerMappingPresetId.AppleMfi,
            ControllerMappingPresetId.NintendoSwitch,
            ControllerMappingPresetId.SteamDeck,
            ControllerMappingPresetId.AndroidGeneric,
        };

        public static ControllerMappingProfile GetPreset(ControllerMappingPresetId presetId)
        {
            switch (presetId)
            {
                case ControllerMappingPresetId.AppleMfi:
                    return Standard("iOS / macOS MFi", ControllerMappingPresetId.AppleMfi);
                case ControllerMappingPresetId.NintendoSwitch:
                    return new ControllerMappingProfile
                    {
                        presetId = ControllerMappingPresetId.NintendoSwitch,
                        displayName = "Nintendo Switch",
                        leftStick = GamepadVectorSource.LeftStick,
                        rightStick = GamepadVectorSource.RightStick,
                        button1 = GamepadButtonSource.ButtonEast,
                        button2 = GamepadButtonSource.ButtonSouth,
                        button3 = GamepadButtonSource.ButtonNorth,
                        flip = GamepadButtonSource.RightShoulder,
                        throttle = GamepadButtonSource.RightShoulder,
                        leftTrigger = GamepadButtonSource.LeftTrigger,
                        rightTrigger = GamepadButtonSource.RightTrigger,
                        stickDeadzone = 0.08f,
                        buttonPressPoint = 0.05f,
                    };
                case ControllerMappingPresetId.SteamDeck:
                    return Standard("Steam Deck", ControllerMappingPresetId.SteamDeck);
                case ControllerMappingPresetId.AndroidGeneric:
                    return Standard("Android Generic", ControllerMappingPresetId.AndroidGeneric);
                case ControllerMappingPresetId.XboxWindows:
                default:
                    return Standard("Xbox / Windows", ControllerMappingPresetId.XboxWindows);
            }
        }

        public static string GetLabel(ControllerMappingPresetId presetId) => GetPreset(presetId).displayName;

        private static ControllerMappingProfile Standard(string label, ControllerMappingPresetId presetId)
        {
            return new ControllerMappingProfile
            {
                presetId = presetId,
                displayName = label,
                leftStick = GamepadVectorSource.LeftStick,
                rightStick = GamepadVectorSource.RightStick,
                button1 = GamepadButtonSource.ButtonSouth,
                button2 = GamepadButtonSource.ButtonEast,
                button3 = GamepadButtonSource.ButtonWest,
                flip = GamepadButtonSource.RightShoulder,
                throttle = GamepadButtonSource.RightShoulder,
                leftTrigger = GamepadButtonSource.LeftTrigger,
                rightTrigger = GamepadButtonSource.RightTrigger,
                stickDeadzone = 0.08f,
                buttonPressPoint = 0.05f,
            };
        }
    }
}
