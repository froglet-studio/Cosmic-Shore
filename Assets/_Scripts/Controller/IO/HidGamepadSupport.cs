using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.HID;
using CosmicShore.Utility;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Universal HID -> Gamepad promotion.
    ///
    /// Problem: Unity's Input System only exposes a device as <see cref="Gamepad"/> when it
    /// ships a hand-authored layout for it (Xbox, DualShock, Switch Pro, ...). For ANY other
    /// HID controller — e.g. the SteelSeries Nimbus on macOS, many Bluetooth/MFi pads, and a
    /// long tail of generic USB pads — Unity falls back to a generic <see cref="Joystick"/>.
    /// All of Cosmic Shore's input (flight via <see cref="GamepadInputStrategy"/>, UI via
    /// ControllerButtonPress / ControllerDropdown, and strategy selection in
    /// <see cref="InputController"/>) keys off <c>Gamepad.current</c>, so a Joystick is simply
    /// ignored and the controller appears dead.
    ///
    /// Fix: hook <see cref="InputSystem.onFindLayoutForDevice"/> and, for any HID that Unity
    /// was about to expose as a generic Joystick, parse the device's OWN HID report descriptor
    /// and synthesize a <see cref="Gamepad"/>-derived layout whose control offsets are taken
    /// straight from that descriptor. This is the same mechanism Unity itself uses for its
    /// built-in HID gamepads (see Unity's HID.cs), generalized to read the offsets from the
    /// device instead of hardcoding them — so it is universal across a wide range of pads and
    /// requires no per-device byte tables.
    ///
    /// Standard HID Generic-Desktop / Button usages are mapped onto the Gamepad interface:
    ///   X/Y            -> leftStick
    ///   Z/Rz (or Rx/Ry)-> rightStick
    ///   Hat switch     -> dpad
    ///   Dpad Up/Right/Down/Left usages (pressure dpads like the Nimbus) -> dpad
    ///   Button page 1..N -> buttonSouth, buttonEast, buttonWest, buttonNorth,
    ///                       leftShoulder, rightShoulder, leftTrigger, rightTrigger,
    ///                       select, start, leftStickPress, rightStickPress
    ///
    /// Per-device quirks (axis inversion, non-standard button order) can be added to
    /// <see cref="Quirks"/> without touching the generic path. Axis direction is also
    /// recoverable at runtime via the existing in-game Invert-Y setting, so a wrong guess is
    /// never fatal.
    ///
    /// Verifying a specific controller: enable <see cref="Utility.GamepadDebugger"/> in a scene;
    /// it logs the resolved control map for any promoted pad so offsets can be confirmed.
    /// </summary>
    public static class HidGamepadSupport
    {
        // Generic Desktop usages.
        private const int GD_Joystick = 0x04;
        private const int GD_Gamepad = 0x05;
        private const int GD_MultiAxisController = 0x08;
        private const int GD_X = 0x30;
        private const int GD_Y = 0x31;
        private const int GD_Z = 0x32;
        private const int GD_Rx = 0x33;
        private const int GD_Ry = 0x34;
        private const int GD_Rz = 0x35;
        private const int GD_HatSwitch = 0x39;
        // Some controllers (e.g. SteelSeries Nimbus) expose the dpad as four discrete
        // Generic-Desktop "D-pad" usages rather than a hat switch.
        private const int GD_DpadUp = 0x90;
        private const int GD_DpadDown = 0x91;
        private const int GD_DpadRight = 0x92;
        private const int GD_DpadLeft = 0x93;

        // Tracks layout names we've already generated so re-discovery of the same device
        // model doesn't try to register a duplicate layout.
        private static readonly HashSet<string> s_RegisteredLayouts = new HashSet<string>();

        /// <summary>
        /// When true, every HID device the Input System asks us about is logged with its
        /// interface, product, vendor/product id, matched layout, top-level usage and element
        /// count, plus the promotion decision. Invaluable for diagnosing a controller that
        /// still isn't recognized — leave it on until a pad is confirmed working.
        /// </summary>
        public static bool VerboseLogging = true;

        /// <summary>
        /// When true, auto-installs <see cref="GamepadValueDebugger"/> at startup, which logs the
        /// live values read from <c>Gamepad.current</c> as you move sticks / press buttons. Used
        /// to verify a promoted controller's mapping actually produces correct input. Turn off
        /// once a pad is confirmed working.
        /// </summary>
        public static bool DebugReadValues = true;

        /// <summary>
        /// Optional per-device corrections, keyed by (vendorId, productId). The generic
        /// descriptor-driven mapping is correct for the vast majority of pads; entries here
        /// only exist to override the rare device that reports non-standard ordering or
        /// inverts an axis contrary to the HID spec.
        /// </summary>
        private struct Quirk
        {
            public bool InvertLeftStickY;
            public bool InvertRightStickY;
        }

        private static readonly Dictionary<(int vendor, int product), Quirk> Quirks =
            new Dictionary<(int, int), Quirk>
            {
                // SteelSeries Nimbus (vendorId 0x0111, productId 0x1420). Documented to invert
                // the thumbstick Y axes on macOS contrary to the HID spec. We already invert Y
                // for the standard HID convention below; the Nimbus needs no *extra* flip, so
                // this entry is a no-op placeholder kept as the canonical example of how to add
                // a device-specific correction. Adjust the flags here if on-hardware testing
                // shows a given pad's sticks are reversed.
                { (0x0111, 0x1420), new Quirk { InvertLeftStickY = false, InvertRightStickY = false } },
            };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InitRuntime() => Register();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallValueDebugger()
        {
            if (!DebugReadValues)
                return;

            var go = new GameObject("HidGamepadValueDebugger") { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(go);
            go.AddComponent<GamepadValueDebugger>();
        }

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        private static void InitEditor() => Register();
#endif

        private static bool s_Registered;

        private static void Register()
        {
            if (s_Registered)
                return;
            s_Registered = true;

            // Subscribe once; the handler is invoked by the Input System every time a device
            // is discovered, on both the main thread and during editor domain reloads.
            InputSystem.onFindLayoutForDevice += OnFindLayoutForDevice;

            // onFindLayoutForDevice only fires when a device is (re)discovered. A controller
            // that was already paired/connected before this handler subscribed — the common
            // case in the editor and for Bluetooth pads connected at boot — won't be
            // re-evaluated. Sweep currently-present devices so they get promoted too;
            // registering a matching layout causes the Input System to recreate the device.
            PromoteAlreadyConnectedDevices();
        }

        private static void PromoteAlreadyConnectedDevices()
        {
            try
            {
                // Snapshot first: promoting a device causes the Input System to recreate it,
                // which mutates InputSystem.devices mid-iteration.
                var snapshot = new List<InputDevice>();
                foreach (var d in InputSystem.devices)
                    snapshot.Add(d);

                foreach (var device in snapshot)
                {
                    if (device is Gamepad)
                        continue;
                    var desc = device.description;
                    if (string.IsNullOrEmpty(desc.interfaceName) || desc.interfaceName != "HID")
                        continue;

                    var name = TryBuildAndRegisterLayout(desc, device.layout);
                    if (!string.IsNullOrEmpty(name))
                        UnityEngine.Debug.Log($"[HidGamepadSupport] Registered Gamepad layout '{name}' for " +
                                    $"already-connected device '{desc.product}'. It will be recreated as a Gamepad.");
                }
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning($"[HidGamepadSupport] Sweep of connected devices failed: {e.Message}");
            }
        }

        private static string OnFindLayoutForDevice(ref InputDeviceDescription description,
            string matchedLayout, InputDeviceExecuteCommandDelegate executeDeviceCommand)
        {
            return TryBuildAndRegisterLayout(description, matchedLayout);
        }

        /// <summary>
        /// Core promotion logic shared by the live <see cref="OnFindLayoutForDevice"/> hook and
        /// the connected-device sweep. Returns the generated layout name to use for the device,
        /// or null to leave the Input System's default (Joystick) layout in place.
        /// </summary>
        private static string TryBuildAndRegisterLayout(InputDeviceDescription description, string matchedLayout)
        {
            // Only handle raw HID devices.
            if (string.IsNullOrEmpty(description.interfaceName) ||
                description.interfaceName != "HID")
                return null;

            // If the device is already exposed via a Gamepad-derived layout (XInput,
            // DualShock, Switch Pro, or a previously-generated layout of ours), leave it.
            // NOTE: Unity names auto-generated HID layouts after the *product* (e.g. "Nimbus"),
            // deriving from Joystick — so we must test ancestry, not the literal name.
            bool alreadyGamepad = !string.IsNullOrEmpty(matchedLayout) && IsBasedOn(matchedLayout, "Gamepad");

            if (VerboseLogging)
                LogDevice(description, matchedLayout, alreadyGamepad);

            if (alreadyGamepad)
                return null;

            HID.HIDDeviceDescriptor descriptor;
            try
            {
                if (string.IsNullOrEmpty(description.capabilities))
                {
                    Bail(description, "no HID capabilities JSON");
                    return null;
                }
                descriptor = HID.HIDDeviceDescriptor.FromJson(description.capabilities);
            }
            catch (System.Exception e)
            {
                Bail(description, $"failed to parse HID descriptor: {e.Message}");
                return null;
            }

            if (descriptor.elements == null || descriptor.elements.Length == 0)
            {
                Bail(description, "HID descriptor has no elements");
                return null;
            }

            // A device the Input System already classified as a Joystick is controller-like by
            // definition (Unity only builds Joysticks from Joystick/Gamepad/MultiAxisController
            // usages). Otherwise fall back to inspecting the descriptor's top-level usage. This
            // covers descriptors that don't surface a helpful device-level usage.
            bool joystickFallback = string.IsNullOrEmpty(matchedLayout) ||
                                    matchedLayout == "HID" ||
                                    IsBasedOn(matchedLayout, "Joystick");
            if (!joystickFallback && !IsControllerLike(descriptor))
            {
                Bail(description, $"not controller-like (usagePage=0x{(int)descriptor.usagePage:X2} usage=0x{descriptor.usage:X2})");
                return null;
            }

            var map = BuildControlMap(descriptor);
            // Require at least the primary stick + one button before we claim it's a gamepad;
            // otherwise we'd misrepresent steering wheels, flight sticks, etc.
            if (!map.HasLeftStick || map.ButtonCount == 0)
            {
                Bail(description, $"insufficient controls (leftStick={map.HasLeftStick}, " +
                                  $"rightStick={map.HasRightStick}, buttons={map.ButtonCount}) " +
                                  $"— if this is your pad, the report likely encodes buttons/axes " +
                                  $"in a form the generic mapping didn't catch; paste this log.");
                return null;
            }

            var layoutName = MakeLayoutName(description, descriptor);

            // Already registered this model? Just use it.
            //
            // This early-out is also what keeps us from crashing: RegisterLayoutBuilder with a
            // matcher makes the Input System SYNCHRONOUSLY recreate every device that matches,
            // which re-invokes this very handler (RecreateDevicesUsingLayoutWithInferiorMatch ->
            // TryFindMatchingControlLayout -> OnFindLayoutForDevice). Because we add the name to
            // s_RegisteredLayouts BEFORE calling RegisterLayoutBuilder (below), that re-entrant
            // call lands here and returns the name instead of registering again. Without the
            // mark-before-register ordering this recursed until the stack overflowed and took
            // the editor down with it.
            if (s_RegisteredLayouts.Contains(layoutName))
                return layoutName;

            Quirks.TryGetValue((descriptor.vendorId, descriptor.productId), out var quirk);

            int reportSizeBytes = Mathf.Max(1, descriptor.inputReportSize);
            var capturedMap = map;
            var capturedQuirk = quirk;
            var capturedName = layoutName;
            var displayName = string.IsNullOrEmpty(description.product)
                ? "HID Gamepad"
                : description.product;

            // Match on vendor/product when the device reports them; otherwise fall back to
            // manufacturer/product strings so zero-id Bluetooth pads still resolve uniquely.
            var matcher = new InputDeviceMatcher().WithInterface("HID");
            if (descriptor.vendorId != 0 || descriptor.productId != 0)
            {
                matcher = matcher
                    .WithCapability("vendorId", descriptor.vendorId)
                    .WithCapability("productId", descriptor.productId);
            }
            else
            {
                if (!string.IsNullOrEmpty(description.manufacturer))
                    matcher = matcher.WithManufacturer(description.manufacturer);
                if (!string.IsNullOrEmpty(description.product))
                    matcher = matcher.WithProduct(description.product);
            }

            // Mark BEFORE registering so the re-entrant recreate cascade short-circuits above.
            s_RegisteredLayouts.Add(layoutName);
            try
            {
                InputSystem.RegisterLayoutBuilder(
                    () => BuildLayout(capturedName, displayName, capturedMap, capturedQuirk, reportSizeBytes),
                    capturedName,
                    baseLayout: "Gamepad",
                    matches: matcher);
                UnityEngine.Debug.Log($"[HidGamepadSupport] PROMOTED HID device '{displayName}' " +
                            $"(vendor 0x{descriptor.vendorId:X4}, product 0x{descriptor.productId:X4}) " +
                            $"to Gamepad layout '{layoutName}' — leftStick={map.HasLeftStick}, " +
                            $"rightStick={map.HasRightStick}, buttons={map.ButtonCount}. " +
                            $"It should now appear as Gamepad.current.");
            }
            catch (System.Exception e)
            {
                // Allow a later retry and don't leave a phantom registration behind.
                s_RegisteredLayouts.Remove(layoutName);
                UnityEngine.Debug.LogWarning($"[HidGamepadSupport] Failed to register Gamepad layout for " +
                                   $"'{displayName}': {e.Message}. Falling back to default Joystick.");
                return null;
            }

            return layoutName;
        }

        private static bool IsBasedOn(string layoutName, string baseLayoutName)
        {
            try
            {
                return InputSystem.IsFirstLayoutBasedOnSecond(layoutName, baseLayoutName);
            }
            catch
            {
                // matchedLayout may be a name the registry can't resolve at this instant.
                return false;
            }
        }

        private static void LogDevice(InputDeviceDescription description, string matchedLayout, bool alreadyGamepad)
        {
            int vendorId = 0, productId = 0, usage = 0, usagePage = 0, elementCount = 0;
            try
            {
                if (!string.IsNullOrEmpty(description.capabilities))
                {
                    var d = HID.HIDDeviceDescriptor.FromJson(description.capabilities);
                    vendorId = d.vendorId;
                    productId = d.productId;
                    usage = d.usage;
                    usagePage = (int)d.usagePage;
                    elementCount = d.elements?.Length ?? 0;
                }
            }
            catch { /* best-effort diagnostics only */ }

            UnityEngine.Debug.Log($"[HidGamepadSupport] HID seen: product='{description.product}' " +
                        $"manufacturer='{description.manufacturer}' " +
                        $"vendor=0x{vendorId:X4} product=0x{productId:X4} " +
                        $"usagePage=0x{usagePage:X2} usage=0x{usage:X2} elements={elementCount} " +
                        $"matchedLayout='{matchedLayout}' alreadyGamepad={alreadyGamepad}");
        }

        private static void Bail(InputDeviceDescription description, string reason)
        {
            if (VerboseLogging)
                UnityEngine.Debug.Log($"[HidGamepadSupport] Not promoting '{description.product}': {reason}");
        }

        private static bool IsControllerLike(HID.HIDDeviceDescriptor descriptor)
        {
            // Only promote devices whose top-level usage declares them a controller. This is
            // the same set Unity itself turns into Joysticks, and it deliberately excludes
            // mice (usage 0x02) and keyboards (0x06), which also carry X/Y axes and buttons
            // and would otherwise be mis-promoted to a Gamepad.
            if (descriptor.usagePage != HID.UsagePage.GenericDesktop)
                return false;

            switch (descriptor.usage)
            {
                case GD_Joystick:
                case GD_Gamepad:
                case GD_MultiAxisController:
                    return true;
                default:
                    return false;
            }
        }

        private struct AxisInfo
        {
            public bool Present;
            public int OffsetBits;
            public int SizeBits;
            public int LogicalMin;
            public int LogicalMax;
        }

        private struct ControlMap
        {
            public AxisInfo LeftX, LeftY, RightX, RightY;
            public bool HasLeftStick => LeftX.Present && LeftY.Present;
            public bool HasRightStick => RightX.Present && RightY.Present;

            public bool HasHat;
            public int HatOffsetBits, HatSizeBits;

            // Discrete dpad direction usages (pressure dpads).
            public int DpadUpBit, DpadDownBit, DpadLeftBit, DpadRightBit; // bit offsets, -1 if absent

            // Ordered list of button bit offsets from the Button usage page.
            public List<int> ButtonBitOffsets;
            public int ButtonCount => ButtonBitOffsets?.Count ?? 0;
        }

        private static ControlMap BuildControlMap(HID.HIDDeviceDescriptor descriptor)
        {
            var map = new ControlMap
            {
                ButtonBitOffsets = new List<int>(),
                DpadUpBit = -1,
                DpadDownBit = -1,
                DpadLeftBit = -1,
                DpadRightBit = -1,
            };

            foreach (var element in descriptor.elements)
            {
                if (element.reportType != HID.HIDReportType.Input)
                    continue;

                int logicalMin = element.logicalMin;
                int logicalMax = element.logicalMax;
                SignExtendLogicalRange(ref logicalMin, ref logicalMax, element.reportSizeInBits);

                var axis = new AxisInfo
                {
                    Present = true,
                    OffsetBits = element.reportOffsetInBits,
                    SizeBits = element.reportSizeInBits,
                    LogicalMin = logicalMin,
                    LogicalMax = logicalMax,
                };

                if (element.usagePage == HID.UsagePage.GenericDesktop)
                {
                    switch (element.usage)
                    {
                        case GD_X: map.LeftX = axis; break;
                        case GD_Y: map.LeftY = axis; break;
                        case GD_Z: map.RightX = axis; break;
                        case GD_Rz: map.RightY = axis; break;
                        case GD_Rx: if (!map.RightX.Present) map.RightX = axis; break;
                        case GD_Ry: if (!map.RightY.Present) map.RightY = axis; break;
                        case GD_HatSwitch:
                            map.HasHat = true; map.HatOffsetBits = axis.OffsetBits; map.HatSizeBits = axis.SizeBits; break;
                        case GD_DpadUp: map.DpadUpBit = axis.OffsetBits; break;
                        case GD_DpadDown: map.DpadDownBit = axis.OffsetBits; break;
                        case GD_DpadRight: map.DpadRightBit = axis.OffsetBits; break;
                        case GD_DpadLeft: map.DpadLeftBit = axis.OffsetBits; break;
                    }
                }
                else if (element.usagePage == HID.UsagePage.Button)
                {
                    // Each button element is one bit; collect in report order.
                    map.ButtonBitOffsets.Add(element.reportOffsetInBits);
                }
            }

            return map;
        }

        private static InputControlLayout BuildLayout(string layoutName, string displayName,
            ControlMap map, Quirk quirk, int reportSizeBytes)
        {
            // This closure is invoked by the Input System later, during device creation, OUTSIDE
            // the try/catch around RegisterLayoutBuilder. If it threw, the exception would
            // propagate into native input code. Guard it: on any failure, fall back to a bare
            // Gamepad-derived layout (neutral, never crashes) rather than throwing.
            try
            {
                return BuildLayoutCore(layoutName, displayName, map, quirk, reportSizeBytes);
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogWarning($"[HidGamepadSupport] BuildLayout failed for '{layoutName}': " +
                                             $"{e.Message}. Using a neutral Gamepad layout.");
                return new InputControlLayout.Builder()
                    .WithName(layoutName)
                    .WithDisplayName(displayName)
                    .Extend("Gamepad")
                    .Build();
            }
        }

        private static InputControlLayout BuildLayoutCore(string layoutName, string displayName,
            ControlMap map, Quirk quirk, int reportSizeBytes)
        {
            // A byte the device never writes (one past the input report) is guaranteed to stay
            // zero in the state buffer, so any standard Gamepad control we DON'T map is parked
            // here and reads as "not pressed / centered" instead of garbage from a real byte.
            int deadByte = reportSizeBytes;

            var builder = new InputControlLayout.Builder()
                .WithName(layoutName)
                .WithDisplayName(displayName)
                .WithFormat("HID")
                .Extend("Gamepad");

            // ---- Sticks ----
            // A signed axis parked on the always-zero dead byte reads 0 = centered.
            var deadAxis = new AxisInfo { Present = false, OffsetBits = deadByte * 8, SizeBits = 8, LogicalMin = -128, LogicalMax = 127 };

            AddStick(builder, "leftStick",
                map.HasLeftStick ? map.LeftX : deadAxis,
                map.HasLeftStick ? map.LeftY : deadAxis,
                invertY: !quirk.InvertLeftStickY);

            AddStick(builder, "rightStick",
                map.HasRightStick ? map.RightX : deadAxis,
                map.HasRightStick ? map.RightY : deadAxis,
                invertY: !quirk.InvertRightStickY);

            // ---- Buttons (report order -> gamepad semantic order) ----
            // Order follows the de-facto HID/MFi gamepad convention. Remap via a Quirk if a
            // specific pad disagrees.
            string[] semanticButtons =
            {
                "buttonSouth", "buttonEast", "buttonWest", "buttonNorth",
                "leftShoulder", "rightShoulder", "leftTrigger", "rightTrigger",
                "select", "start", "leftStickPress", "rightStickPress",
            };

            for (int i = 0; i < semanticButtons.Length; i++)
            {
                int bitOffset = i < map.ButtonCount ? map.ButtonBitOffsets[i] : deadByte * 8;
                AddButton(builder, semanticButtons[i], bitOffset);
            }

            // ---- Dpad ----
            if (map.DpadUpBit >= 0 || map.DpadDownBit >= 0 || map.DpadLeftBit >= 0 || map.DpadRightBit >= 0)
            {
                // Discrete (often pressure) dpad usages -> treat each as a button bit.
                AddButton(builder, "dpad/up", map.DpadUpBit >= 0 ? map.DpadUpBit : deadByte * 8);
                AddButton(builder, "dpad/down", map.DpadDownBit >= 0 ? map.DpadDownBit : deadByte * 8);
                AddButton(builder, "dpad/left", map.DpadLeftBit >= 0 ? map.DpadLeftBit : deadByte * 8);
                AddButton(builder, "dpad/right", map.DpadRightBit >= 0 ? map.DpadRightBit : deadByte * 8);
            }
            else if (map.HasHat)
            {
                // Hat switch -> Unity's dpad-from-hat handling. The DiscreteButton processors
                // on the inherited dpad children decode the 0..7 hat value.
                builder.AddControl("dpad")
                    .WithLayout("Dpad")
                    .WithFormat("BIT")
                    .WithByteOffset((uint)(map.HatOffsetBits / 8))
                    .WithBitOffset((uint)(map.HatOffsetBits % 8))
                    .WithSizeInBits((uint)Mathf.Max(4, map.HatSizeBits));
            }
            else
            {
                // No dpad on the device: park it on the dead byte so it reads neutral.
                AddButton(builder, "dpad/up", deadByte * 8);
                AddButton(builder, "dpad/down", deadByte * 8);
                AddButton(builder, "dpad/left", deadByte * 8);
                AddButton(builder, "dpad/right", deadByte * 8);
            }

            return builder.Build();
        }

        private static void AddStick(InputControlLayout.Builder builder, string name,
            AxisInfo x, AxisInfo y, bool invertY)
        {
            // Parent at byte 0 so the absolute child offsets below resolve correctly.
            builder.AddControl(name)
                .WithLayout("Stick")
                .WithByteOffset(0);

            AddAxis(builder, name + "/x", x, invert: false);
            AddAxis(builder, name + "/y", y, invert: invertY);
        }

        private static void AddAxis(InputControlLayout.Builder builder, string name, AxisInfo axis, bool invert)
        {
            // Pick signed vs unsigned format and centering from the descriptor's logical range,
            // mirroring Unity's own HID handling (HID.cs DetermineFormat / isSigned /
            // DetermineAxisNormalizationParameters). This is what makes a signed-byte stick
            // (rest = 0, range -128..127) center correctly instead of reading as a hard
            // deflection — the root cause of "drifts to one side at rest".
            int sizeBits = axis.SizeBits <= 0 ? 8 : axis.SizeBits;
            bool signed = axis.LogicalMin < 0;
            string format = AxisFormat(sizeBits, signed);
            string norm = NormalizationParameters(axis.LogicalMin, axis.LogicalMax, sizeBits);

            string parameters = invert
                ? (string.IsNullOrEmpty(norm) ? "invert" : "invert," + norm)
                : norm;

            var c = builder.AddControl(name)
                .WithFormat(format)
                .WithByteOffset((uint)(axis.OffsetBits / 8))
                .WithBitOffset((uint)(axis.OffsetBits % 8))
                .WithSizeInBits((uint)sizeBits);

            if (!string.IsNullOrEmpty(parameters))
                c.WithParameters(parameters);

            if (VerboseLogging)
                UnityEngine.Debug.Log($"[HidGamepadSupport] axis '{name}': present={axis.Present} " +
                    $"offsetBits={axis.OffsetBits} sizeBits={sizeBits} logicalMin={axis.LogicalMin} " +
                    $"logicalMax={axis.LogicalMax} signed={signed} format={format} invert={invert} " +
                    $"params='{parameters}'");
        }

        // HID encodes a negative Logical Minimum within the field's own bit width. Some
        // descriptors (notably the SteelSeries Nimbus) surface it as a large unsigned value —
        // e.g. logicalMin=129, logicalMax=127 for an 8-bit axis, which actually means
        // -127..127, a signed axis centered at 0. The tell is logicalMin > logicalMax. When we
        // see that, sign-extend the bounds for the field width so the axis is recognized as
        // signed and centers correctly. (Unity's own HID fallback misses this, which is why the
        // Nimbus is erratic as a plain Joystick too.)
        private static void SignExtendLogicalRange(ref int logicalMin, ref int logicalMax, int sizeBits)
        {
            if (sizeBits <= 0 || sizeBits >= 32 || logicalMin <= logicalMax)
                return;

            long range = 1L << sizeBits;
            long signBit = 1L << (sizeBits - 1);
            if (logicalMin >= signBit)
                logicalMin = (int)(logicalMin - range);
            if (logicalMax >= signBit)
                logicalMax = (int)(logicalMax - range);
        }

        private static string AxisFormat(int sizeBits, bool signed)
        {
            switch (sizeBits)
            {
                case 8: return signed ? "SBYT" : "BYTE";
                case 16: return signed ? "SHRT" : "USHT";
                case 32: return signed ? "INT" : "UINT";
                default: return "BIT";
            }
        }

        // Mirrors HID.cs HIDElementDescriptor.DetermineAxisNormalizationParameters: build the
        // normalize processor from the element's logical range so any axis (signed/unsigned,
        // 8/16/32-bit, with an arbitrary center) maps to a clean -1..1 with 0 at rest.
        private static string NormalizationParameters(int logicalMin, int logicalMax, int sizeBits)
        {
            if (logicalMin == 0 && logicalMax == 0)
                return "normalize,normalizeMin=0,normalizeMax=1,normalizeZero=0.5";

            // Signedness is a property of the element (its logicalMin), applied to BOTH bounds —
            // not decided per value (logicalMax is positive even on a signed axis).
            bool signed = logicalMin < 0;
            float min = LogicalToFloat(logicalMin, sizeBits, signed);
            float max = LogicalToFloat(logicalMax, sizeBits, signed);
            if (Mathf.Approximately(0f, min) && Mathf.Approximately(0f, max))
                return null;

            float zero = min + (max - min) / 2.0f;
            return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "normalize,normalizeMin={0},normalizeMax={1},normalizeZero={2}", min, max, zero);
        }

        // Mirrors HID.cs HIDElementDescriptor.minFloatValue/maxFloatValue.
        private static float LogicalToFloat(int logical, int sizeBits, bool signed)
        {
            if (signed)
            {
                long minValue = -(1L << (sizeBits - 1));
                long maxValue = (1L << (sizeBits - 1)) - 1;
                return NormalizedFloat(logical, minValue, maxValue) * 2.0f - 1.0f;
            }
            else
            {
                long maxValue = (1L << sizeBits) - 1;
                return NormalizedFloat(logical, 0, maxValue);
            }
        }

        private static float NormalizedFloat(long value, long min, long max)
        {
            if (max <= min)
                return 0f;
            return Mathf.Clamp01((float)((double)(value - min) / (max - min)));
        }

        private static void AddButton(InputControlLayout.Builder builder, string name, int bitOffset)
        {
            builder.AddControl(name)
                .WithLayout("Button")
                .WithFormat("BIT")
                .WithByteOffset((uint)(bitOffset / 8))
                .WithBitOffset((uint)(bitOffset % 8))
                .WithSizeInBits(1);
        }

        private static string MakeLayoutName(InputDeviceDescription description,
            HID.HIDDeviceDescriptor descriptor)
        {
            if (descriptor.vendorId != 0 || descriptor.productId != 0)
                return $"HIDGamepad::{descriptor.vendorId:X4}-{descriptor.productId:X4}";

            // Zero-id device (some Bluetooth pads): fall back to a name keyed on the
            // manufacturer/product strings so distinct models don't collide.
            var key = $"{description.manufacturer}-{description.product}";
            return $"HIDGamepad::{key.GetHashCode():X8}";
        }
    }
}
