# Branch archive: `claude/intelligent-lovelace-dxtorn`

_Snapshot 2026-10-08. Index: [README](../README.md#index--medium-branches-410-unmerged-commits)_

- **Last commit:** 2026-06-11 by Claude
- **Unmerged commits:** 8
- **Open pull request:** https://github.com/froglet-studio/Cosmic-Shore/pull/543
- **Forked from:** `9aa4b2626` (2026-06-10, Update README.md)
- **Tip:** `2b366cdec`
- **Files touched (4):**
  - `Assets/_Scripts/Controller/IO/GamepadValueDebugger.cs`
  - `Assets/_Scripts/Controller/IO/GamepadValueDebugger.cs.meta`
  - `Assets/_Scripts/Controller/IO/HidGamepadSupport.cs`
  - `Assets/_Scripts/Controller/IO/HidGamepadSupport.cs.meta`

### `9dd194619` — feat(input): universal HID-to-Gamepad promotion for unrecognized controllers

_Claude, 2026-06-10 19:56:09 +0000_

```text
Unity's Input System only exposes a device as Gamepad when it ships a
hand-authored layout (Xbox, DualShock, Switch Pro). Every other HID
controller — the SteelSeries Nimbus on macOS, many Bluetooth/MFi pads,
and generic USB pads — falls back to a generic Joystick. All of Cosmic
Shore's input keys off Gamepad.current (flight via GamepadInputStrategy,
UI via ControllerButtonPress/ControllerDropdown, and strategy selection
in InputController), so a Joystick is silently ignored and the
controller appears dead.

Add HidGamepadSupport: a startup hook on InputSystem.onFindLayoutForDevice
that, for any HID headed to the generic Joystick fallback, parses the
device's own HID report descriptor and synthesizes a Gamepad-derived
layout with control offsets read straight from that descriptor — the
same mechanism Unity uses for its built-in HID gamepads, generalized so
it needs no per-device byte tables and works across a wide range of pads.

Standard Generic-Desktop/Button usages map onto the Gamepad interface
(X/Y -> leftStick, Z/Rz or Rx/Ry -> rightStick, hat or discrete dpad
usages -> dpad, Button page -> face/shoulder/trigger/stick-press in the
de-facto MFi order). Mice and keyboards are excluded by gating on the
device's top-level Joystick/Gamepad/MultiAxisController usage. Unmapped
inherited controls are parked on a never-written report byte so they read
neutral instead of garbage. A per-(vendor,product) Quirks table allows
axis-inversion/button-order overrides without touching the generic path;
the SteelSeries Nimbus (0x0111/0x1420) is included as the canonical entry.
```

```text
 Assets/_Scripts/Controller/IO/HidGamepadSupport.cs      | 448 ++++++++++++++++++++++++++++++++++++++++++++++
 Assets/_Scripts/Controller/IO/HidGamepadSupport.cs.meta |  11 ++
 2 files changed, 459 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 150 of 454 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/IO/HidGamepadSupport.cs b/Assets/_Scripts/Controller/IO/HidGamepadSupport.cs
new file mode 100644
index 000000000..a84b1f91c
--- /dev/null
+++ b/Assets/_Scripts/Controller/IO/HidGamepadSupport.cs
@@ -0,0 +1,448 @@
+using System.Collections.Generic;
+using UnityEngine;
+using UnityEngine.InputSystem;
+using UnityEngine.InputSystem.Layouts;
+using UnityEngine.InputSystem.LowLevel;
+using UnityEngine.InputSystem.HID;
+using CosmicShore.Utility;
+#if UNITY_EDITOR
+using UnityEditor;
+#endif
+
+namespace CosmicShore.Gameplay
+{
+    /// <summary>
+    /// Universal HID -> Gamepad promotion.
+    ///
+    /// Problem: Unity's Input System only exposes a device as <see cref="Gamepad"/> when it
+    /// ships a hand-authored layout for it (Xbox, DualShock, Switch Pro, ...). For ANY other
+    /// HID controller — e.g. the SteelSeries Nimbus on macOS, many Bluetooth/MFi pads, and a
+    /// long tail of generic USB pads — Unity falls back to a generic <see cref="Joystick"/>.
+    /// All of Cosmic Shore's input (flight via <see cref="GamepadInputStrategy"/>, UI via
+    /// ControllerButtonPress / ControllerDropdown, and strategy selection in
+    /// <see cref="InputController"/>) keys off <c>Gamepad.current</c>, so a Joystick is simply
+    /// ignored and the controller appears dead.
+    ///
+    /// Fix: hook <see cref="InputSystem.onFindLayoutForDevice"/> and, for any HID that Unity
+    /// was about to expose as a generic Joystick, parse the device's OWN HID report descriptor
+    /// and synthesize a <see cref="Gamepad"/>-derived layout whose control offsets are taken
+    /// straight from that descriptor. This is the same mechanism Unity itself uses for its
+    /// built-in HID gamepads (see Unity's HID.cs), generalized to read the offsets from the
+    /// device instead of hardcoding them — so it is universal across a wide range of pads and
+    /// requires no per-device byte tables.
+    ///
+    /// Standard HID Generic-Desktop / Button usages are mapped onto the Gamepad interface:
+    ///   X/Y            -> leftStick
+    ///   Z/Rz (or Rx/Ry)-> rightStick
+    ///   Hat switch     -> dpad
+    ///   Dpad Up/Right/Down/Left usages (pressure dpads like the Nimbus) -> dpad
+    ///   Button page 1..N -> buttonSouth, buttonEast, buttonWest, buttonNorth,
+    ///                       leftShoulder, rightShoulder, leftTrigger, rightTrigger,
+    ///                       select, start, leftStickPress, rightStickPress
+    ///
+    /// Per-device quirks (axis inversion, non-standard button order) can be added to
+    /// <see cref="Quirks"/> without touching the generic path. Axis direction is also
+    /// recoverable at runtime via the existing in-game Invert-Y setting, so a wrong guess is
+    /// never fatal.
+    ///
+    /// Verifying a specific controller: enable <see cref="Utility.GamepadDebugger"/> in a scene;
+    /// it logs the resolved control map for any promoted pad so offsets can be confirmed.
+    /// </summary>
+    public static class HidGamepadSupport
+    {
+        // HID usage-page identifiers (USB HID Usage Tables).
+        private const int UsagePageGenericDesktop = 0x01;
+        private const int UsagePageButton = 0x09;
+
+        // Generic Desktop usages.
+        private const int GD_Joystick = 0x04;
+        private const int GD_Gamepad = 0x05;
+        private const int GD_MultiAxisController = 0x08;
+        private const int GD_X = 0x30;
+        private const int GD_Y = 0x31;
+        private const int GD_Z = 0x32;
+        private const int GD_Rx = 0x33;
+        private const int GD_Ry = 0x34;
+        private const int GD_Rz = 0x35;
+        private const int GD_HatSwitch = 0x39;
+        // Some controllers (e.g. SteelSeries Nimbus) expose the dpad as four discrete
+        // Generic-Desktop "D-pad" usages rather than a hat switch.
+        private const int GD_DpadUp = 0x90;
+        private const int GD_DpadDown = 0x91;
+        private const int GD_DpadRight = 0x92;
+        private const int GD_DpadLeft = 0x93;
+
+        // Tracks layout names we've already generated so re-discovery of the same device
+        // model doesn't try to register a duplicate layout.
+        private static readonly HashSet<string> s_RegisteredLayouts = new HashSet<string>();
+
+        /// <summary>
+        /// Optional per-device corrections, keyed by (vendorId, productId). The generic
+        /// descriptor-driven mapping is correct for the vast majority of pads; entries here
+        /// only exist to override the rare device that reports non-standard ordering or
+        /// inverts an axis contrary to the HID spec.
+        /// </summary>
+        private struct Quirk
+        {
+            public bool InvertLeftStickY;
+            public bool InvertRightStickY;
+        }
+
+        private static readonly Dictionary<(int vendor, int product), Quirk> Quirks =
+            new Dictionary<(int, int), Quirk>
+            {
+                // SteelSeries Nimbus (vendorId 0x0111, productId 0x1420). Documented to invert
+                // the thumbstick Y axes on macOS contrary to the HID spec. We already invert Y
+                // for the standard HID convention below; the Nimbus needs no *extra* flip, so
+                // this entry is a no-op placeholder kept as the canonical example of how to add
+                // a device-specific correction. Adjust the flags here if on-hardware testing
+                // shows a given pad's sticks are reversed.
+                { (0x0111, 0x1420), new Quirk { InvertLeftStickY = false, InvertRightStickY = false } },
+            };
+
+        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
+        private static void InitRuntime() => Register();
+
+#if UNITY_EDITOR
+        [InitializeOnLoadMethod]
+        private static void InitEditor() => Register();
+#endif
+
+        private static bool s_Registered;
+
+        private static void Register()
+        {
+            if (s_Registered)
+                return;
+            s_Registered = true;
+
+            // Subscribe once; the handler is invoked by the Input System every time a device
+            // is discovered, on both the main thread and during editor domain reloads.
+            InputSystem.onFindLayoutForDevice += OnFindLayoutForDevice;
+        }
+
+        private static string OnFindLayoutForDevice(ref InputDeviceDescription description,
+            string matchedLayout, InputDeviceExecuteCommandDelegate executeDeviceCommand)
+        {
+            // Only handle raw HID devices.
+            if (string.IsNullOrEmpty(description.interfaceName) ||
+                description.interfaceName != "HID")
+                return null;
+
+            // If Unity already matched this device to a real Gamepad-derived layout
+            // (XInput, DualShock, Switch Pro, or a previously-generated layout of ours),
+            // leave it alone. We only promote devices headed for the generic Joystick/HID
+            // fallback.
+            if (!string.IsNullOrEmpty(matchedLayout) &&
+                matchedLayout != "HID" &&
+                matchedLayout != "Joystick")
+            {
+                return null;
+            }
+
+            HID.HIDDeviceDescriptor descriptor;
+            try
```

</details>

### `ef0a4cca4` — fix(input): promote by layout ancestry, sweep connected devices, add diagnostics

_Claude, 2026-06-10 20:10:55 +0000_

```text
Root cause of the no-op: the early-return guard tested the matched layout
by literal name (`matchedLayout != "Joystick"`). Unity names an
auto-generated HID layout after the *product* (e.g. "Nimbus") and derives
it from Joystick, so matchedLayout was never literally "Joystick" — the
guard bailed and the device was never promoted, leaving it a Joystick.

- Guard on layout ancestry via InputSystem.IsFirstLayoutBasedOnSecond:
  promote anything not already based on Gamepad (covers null/"HID"/
  "Joystick"/product-named Joystick-derived layouts), and skip real
  gamepads (Xbox/DualShock/our own generated layout) — also prevents
  re-entrancy when the device is rebuilt.
- Promote already-connected devices: onFindLayoutForDevice only fires on
  (re)discovery, so a pad paired before the handler subscribed (the common
  editor / boot-time-Bluetooth case) was missed. Sweep InputSystem.devices
  on startup and register layouts for unrecognized HID joysticks; the
  Input System recreates matching devices as Gamepads.
- Add VerboseLogging (default on): logs every HID device's product,
  vendor/product id, usagePage/usage, element count, matchedLayout and the
  promotion decision, so an unrecognized controller can be diagnosed from
  the Console without hardware access here.
- Trust a Joystick-classified device as controller-like even when the
  descriptor's top-level usage is unhelpful.
```

```text
 Assets/_Scripts/Controller/IO/HidGamepadSupport.cs | 115 ++++++++++++++++++++++++++++++++++++++++++++++-----
 1 file changed, 104 insertions(+), 11 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 158 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/IO/HidGamepadSupport.cs b/Assets/_Scripts/Controller/IO/HidGamepadSupport.cs
index a84b1f91c..16cb7a3c7 100644
--- a/Assets/_Scripts/Controller/IO/HidGamepadSupport.cs
+++ b/Assets/_Scripts/Controller/IO/HidGamepadSupport.cs
@@ -76,6 +76,14 @@ namespace CosmicShore.Gameplay
         // model doesn't try to register a duplicate layout.
         private static readonly HashSet<string> s_RegisteredLayouts = new HashSet<string>();
 
+        /// <summary>
+        /// When true, every HID device the Input System asks us about is logged with its
+        /// interface, product, vendor/product id, matched layout, top-level usage and element
+        /// count, plus the promotion decision. Invaluable for diagnosing a controller that
+        /// still isn't recognized — leave it on until a pad is confirmed working.
+        /// </summary>
+        public static bool VerboseLogging = true;
+
         /// <summary>
         /// Optional per-device corrections, keyed by (vendorId, productId). The generic
         /// descriptor-driven mapping is correct for the vast majority of pads; entries here
@@ -119,26 +127,68 @@ namespace CosmicShore.Gameplay
             // Subscribe once; the handler is invoked by the Input System every time a device
             // is discovered, on both the main thread and during editor domain reloads.
             InputSystem.onFindLayoutForDevice += OnFindLayoutForDevice;
+
+            // onFindLayoutForDevice only fires when a device is (re)discovered. A controller
+            // that was already paired/connected before this handler subscribed — the common
+            // case in the editor and for Bluetooth pads connected at boot — won't be
+            // re-evaluated. Sweep currently-present devices so they get promoted too;
+            // registering a matching layout causes the Input System to recreate the device.
+            PromoteAlreadyConnectedDevices();
+        }
+
+        private static void PromoteAlreadyConnectedDevices()
+        {
+            try
+            {
+                foreach (var device in InputSystem.devices)
+                {
+                    if (device is Gamepad)
+                        continue;
+                    var desc = device.description;
+                    if (string.IsNullOrEmpty(desc.interfaceName) || desc.interfaceName != "HID")
+                        continue;
+
+                    var name = TryBuildAndRegisterLayout(desc, device.layout);
+                    if (!string.IsNullOrEmpty(name))
+                        CSDebug.Log($"[HidGamepadSupport] Registered Gamepad layout '{name}' for " +
+                                    $"already-connected device '{desc.product}'. It will be recreated as a Gamepad.");
+                }
+            }
+            catch (System.Exception e)
+            {
+                CSDebug.LogWarning($"[HidGamepadSupport] Sweep of connected devices failed: {e.Message}");
+            }
         }
 
         private static string OnFindLayoutForDevice(ref InputDeviceDescription description,
             string matchedLayout, InputDeviceExecuteCommandDelegate executeDeviceCommand)
+        {
+            return TryBuildAndRegisterLayout(description, matchedLayout);
+        }
+
+        /// <summary>
+        /// Core promotion logic shared by the live <see cref="OnFindLayoutForDevice"/> hook and
+        /// the connected-device sweep. Returns the generated layout name to use for the device,
+        /// or null to leave the Input System's default (Joystick) layout in place.
+        /// </summary>
+        private static string TryBuildAndRegisterLayout(InputDeviceDescription description, string matchedLayout)
         {
             // Only handle raw HID devices.
             if (string.IsNullOrEmpty(description.interfaceName) ||
                 description.interfaceName != "HID")
                 return null;
 
-            // If Unity already matched this device to a real Gamepad-derived layout
-            // (XInput, DualShock, Switch Pro, or a previously-generated layout of ours),
-            // leave it alone. We only promote devices headed for the generic Joystick/HID
-            // fallback.
-            if (!string.IsNullOrEmpty(matchedLayout) &&
-                matchedLayout != "HID" &&
-                matchedLayout != "Joystick")
-            {
+            // If the device is already exposed via a Gamepad-derived layout (XInput,
+            // DualShock, Switch Pro, or a previously-generated layout of ours), leave it.
+            // NOTE: Unity names auto-generated HID layouts after the *product* (e.g. "Nimbus"),
+            // deriving from Joystick — so we must test ancestry, not the literal name.
+            bool alreadyGamepad = !string.IsNullOrEmpty(matchedLayout) && IsBasedOn(matchedLayout, "Gamepad");
+
+            if (VerboseLogging)
+                LogDevice(description, matchedLayout, alreadyGamepad);
+
+            if (alreadyGamepad)
                 return null;
-            }
 
             HID.HIDDeviceDescriptor descriptor;
             try
@@ -155,8 +205,14 @@ namespace CosmicShore.Gameplay
             if (descriptor.elements == null || descriptor.elements.Length == 0)
                 return null;
 
-            // Only promote things that present themselves as a controller of some kind.
-            if (!IsControllerLike(descriptor))
+            // A device the Input System already classified as a Joystick is controller-like by
+            // definition (Unity only builds Joysticks from Joystick/Gamepad/MultiAxisController
+            // usages). Otherwise fall back to inspecting the descriptor's top-level usage. This
+            // covers descriptors that don't surface a helpful device-level usage.
+            bool joystickFallback = string.IsNullOrEmpty(matchedLayout) ||
+                                    matchedLayout == "HID" ||
+                                    IsBasedOn(matchedLayout, "Joystick");
+            if (!joystickFallback && !IsControllerLike(descriptor))
                 return null;
 
             var map = BuildControlMap(descriptor);
@@ -218,6 +274,43 @@ namespace CosmicShore.Gameplay
             return layoutName;
         }
 
+        private static bool IsBasedOn(string layoutName, string baseLayoutName)
+        {
+            try
+            {
+                return InputSystem.IsFirstLayoutBasedOnSecond(layoutName, baseLayoutName);
+            }
+            catch
+            {
+                // matchedLayout may be a name the registry can't resolve at this instant.
+                return false;
+            }
+        }
+
+        private static void LogDevice(InputDeviceDescription description, string matchedLayout, bool alreadyGamepad)
+        {
+            int vendorId = 0, productId = 0, usage = 0, usagePage = 0, elementCount = 0;
+            try
+            {
+                if (!string.IsNullOrEmpty(description.capabilities))
+                {
+                    var d = HID.HIDDeviceDescriptor.FromJson(description.capabilities);
+                    vendorId = d.vendorId;
+                    productId = d.productId;
+                    usage = d.usage;
+                    usagePage = (int)d.usagePage;
+                    elementCount = d.elements?.Length ?? 0;
+                }
+            }
+            catch { /* best-effort diagnostics only */ }
+
+            CSDebug.Log($"[HidGamepadSupport] HID seen: product='{description.product}' " +
+                        $"manufacturer='{description.manufacturer}' " +
```

</details>

### `b15ff075a` — fix(input): make HID promotion diagnostics unconditional + log bail reasons

_Claude, 2026-06-10 20:54:48 +0000_

```text
CSDebug.Log is [Conditional] on UNITY_EDITOR/DEVELOPMENT_BUILD and gated
by a runtime log-level flag, so the promotion diagnostics could be
silently suppressed. Switch all HidGamepadSupport diagnostics to
UnityEngine.Debug.Log/LogWarning so they always surface in the Editor
Console, and log an explicit "Not promoting '<product>': <reason>" line
at every early return (no capabilities, parse failure, no elements,
not controller-like, insufficient controls). This makes an unrecognized
controller diagnosable from the Console alone.
```

```text
 Assets/_Scripts/Controller/IO/HidGamepadSupport.cs | 38 +++++++++++++++++++++++++++++++-------
 1 file changed, 31 insertions(+), 7 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/IO/HidGamepadSupport.cs b/Assets/_Scripts/Controller/IO/HidGamepadSupport.cs
index 16cb7a3c7..1bb5a642c 100644
--- a/Assets/_Scripts/Controller/IO/HidGamepadSupport.cs
+++ b/Assets/_Scripts/Controller/IO/HidGamepadSupport.cs
@@ -150,13 +150,13 @@ namespace CosmicShore.Gameplay
 
                     var name = TryBuildAndRegisterLayout(desc, device.layout);
                     if (!string.IsNullOrEmpty(name))
-                        CSDebug.Log($"[HidGamepadSupport] Registered Gamepad layout '{name}' for " +
+                        UnityEngine.Debug.Log($"[HidGamepadSupport] Registered Gamepad layout '{name}' for " +
                                     $"already-connected device '{desc.product}'. It will be recreated as a Gamepad.");
                 }
             }
             catch (System.Exception e)
             {
-                CSDebug.LogWarning($"[HidGamepadSupport] Sweep of connected devices failed: {e.Message}");
+                UnityEngine.Debug.LogWarning($"[HidGamepadSupport] Sweep of connected devices failed: {e.Message}");
             }
         }
 
@@ -194,16 +194,23 @@ namespace CosmicShore.Gameplay
             try
             {
                 if (string.IsNullOrEmpty(description.capabilities))
+                {
+                    Bail(description, "no HID capabilities JSON");
                     return null;
+                }
                 descriptor = HID.HIDDeviceDescriptor.FromJson(description.capabilities);
             }
-            catch
+            catch (System.Exception e)
             {
+                Bail(description, $"failed to parse HID descriptor: {e.Message}");
                 return null;
             }
 
             if (descriptor.elements == null || descriptor.elements.Length == 0)
+            {
+                Bail(description, "HID descriptor has no elements");
                 return null;
+            }
 
             // A device the Input System already classified as a Joystick is controller-like by
             // definition (Unity only builds Joysticks from Joystick/Gamepad/MultiAxisController
@@ -213,13 +220,22 @@ namespace CosmicShore.Gameplay
                                     matchedLayout == "HID" ||
                                     IsBasedOn(matchedLayout, "Joystick");
             if (!joystickFallback && !IsControllerLike(descriptor))
+            {
+                Bail(description, $"not controller-like (usagePage=0x{(int)descriptor.usagePage:X2} usage=0x{descriptor.usage:X2})");
                 return null;
+            }
 
             var map = BuildControlMap(descriptor);
             // Require at least the primary stick + one button before we claim it's a gamepad;
             // otherwise we'd misrepresent steering wheels, flight sticks, etc.
             if (!map.HasLeftStick || map.ButtonCount == 0)
+            {
+                Bail(description, $"insufficient controls (leftStick={map.HasLeftStick}, " +
+                                  $"rightStick={map.HasRightStick}, buttons={map.ButtonCount}) " +
+                                  $"— if this is your pad, the report likely encodes buttons/axes " +
+                                  $"in a form the generic mapping didn't catch; paste this log.");
                 return null;
+            }
 
             var layoutName = MakeLayoutName(description, descriptor);
             if (!s_RegisteredLayouts.Contains(layoutName))
@@ -259,13 +275,15 @@ namespace CosmicShore.Gameplay
                         baseLayout: "Gamepad",
                         matches: matcher);
                     s_RegisteredLayouts.Add(layoutName);
-                    CSDebug.Log($"[HidGamepadSupport] Promoted HID device '{displayName}' " +
+                    UnityEngine.Debug.Log($"[HidGamepadSupport] PROMOTED HID device '{displayName}' " +
                                 $"(vendor 0x{descriptor.vendorId:X4}, product 0x{descriptor.productId:X4}) " +
-                                $"to Gamepad layout '{layoutName}'.");
+                                $"to Gamepad layout '{layoutName}' — leftStick={map.HasLeftStick}, " +
+                                $"rightStick={map.HasRightStick}, buttons={map.ButtonCount}. " +
+                                $"It should now appear as Gamepad.current.");
                 }
                 catch (System.Exception e)
                 {
-                    CSDebug.LogWarning($"[HidGamepadSupport] Failed to register Gamepad layout for " +
+                    UnityEngine.Debug.LogWarning($"[HidGamepadSupport] Failed to register Gamepad layout for " +
                                        $"'{displayName}': {e.Message}. Falling back to default Joystick.");
                     return null;
                 }
@@ -304,13 +322,19 @@ namespace CosmicShore.Gameplay
             }
             catch { /* best-effort diagnostics only */ }
 
-            CSDebug.Log($"[HidGamepadSupport] HID seen: product='{description.product}' " +
+            UnityEngine.Debug.Log($"[HidGamepadSupport] HID seen: product='{description.product}' " +
                         $"manufacturer='{description.manufacturer}' " +
                         $"vendor=0x{vendorId:X4} product=0x{productId:X4} " +
                         $"usagePage=0x{usagePage:X2} usage=0x{usage:X2} elements={elementCount} " +
                         $"matchedLayout='{matchedLayout}' alreadyGamepad={alreadyGamepad}");
         }
 
+        private static void Bail(InputDeviceDescription description, string reason)
+        {
+            if (VerboseLogging)
+                UnityEngine.Debug.Log($"[HidGamepadSupport] Not promoting '{description.product}': {reason}");
+        }
+
         private static bool IsControllerLike(HID.HIDDeviceDescriptor descriptor)
         {
             // Only promote devices whose top-level usage declares them a controller. This is
```

</details>

### `10081f81a` — diag(input): add zero-setup live gamepad value debugger

_Claude, 2026-06-11 01:02:39 +0000_

```text
Promotion to a Gamepad layout is confirmed working for the Nimbus, but
flight still doesn't respond — so the open question is whether the
generated layout's control offsets actually produce correct values, or
whether the values are fine and the problem is downstream.

Add GamepadValueDebugger: auto-installed at startup (no scene wiring),
it logs the live values read straight from Gamepad.current — stick
positions on movement, trigger analog values, and button presses —
independent of the game's input strategies, pause/autopilot state, or
scene. This isolates "is the controller producing correct input" from
"is the game consuming it":
  - sensible stick values on movement -> offsets correct, issue is downstream
  - nothing/garbage/stuck values -> generated layout offsets are wrong
  - Gamepad.current null while mashing buttons -> state events not reaching Unity

Gated behind HidGamepadSupport.DebugReadValues (default on until the pad
is confirmed working).
```

```text
 Assets/_Scripts/Controller/IO/GamepadValueDebugger.cs      | 106 +++++++++++++++++++++++++++++++++++++++++++
 Assets/_Scripts/Controller/IO/GamepadValueDebugger.cs.meta |  11 +++++
 Assets/_Scripts/Controller/IO/HidGamepadSupport.cs         |  19 ++++++++
 3 files changed, 136 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/IO/GamepadValueDebugger.cs b/Assets/_Scripts/Controller/IO/GamepadValueDebugger.cs
new file mode 100644
index 000000000..e45617677
--- /dev/null
+++ b/Assets/_Scripts/Controller/IO/GamepadValueDebugger.cs
@@ -0,0 +1,106 @@
+using UnityEngine;
+using UnityEngine.InputSystem;
+
+namespace CosmicShore.Gameplay
+{
+    /// <summary>
+    /// Zero-setup runtime diagnostic for controller input. Auto-installs at startup (no scene
+    /// wiring needed) and logs the LIVE values it reads from <c>Gamepad.current</c> — completely
+    /// independent of the game's input strategies, pause state, autopilot, or which scene you're
+    /// in.
+    ///
+    /// Purpose: once a controller is promoted to a Gamepad (see <see cref="HidGamepadSupport"/>),
+    /// this answers the only remaining question — "are the stick/button VALUES actually correct?"
+    ///   - If moving a stick logs sensible values (≈ -1..1, centered at 0) -> the layout offsets
+    ///     are right and any remaining problem is downstream (game not consuming input).
+    ///   - If moving a stick logs nothing or garbage/stuck values -> the generated layout's byte
+    ///     offsets are wrong and need correcting.
+    ///   - If it logs "Gamepad.current == null" while you mash buttons -> the device's state
+    ///     events aren't reaching the Input System at all (a platform/transport issue, not a
+    ///     mapping one).
+    ///
+    /// Turn off by setting <see cref="HidGamepadSupport.DebugReadValues"/> = false (or once the
+    /// controller is confirmed working).
+    /// </summary>
+    [AddComponentMenu("")] // hide from the Add Component menu
+    public class GamepadValueDebugger : MonoBehaviour
+    {
+        private const float StickLogThreshold = 0.12f;   // ignore idle jitter
+        private const float StickLogDelta = 0.10f;        // only log on meaningful change
+        private const float MinLogInterval = 0.20f;       // throttle: at most ~5 logs/sec
+
+        private string _lastDeviceName;
+        private Vector2 _lastLeft, _lastRight;
+        private float _lastLogTime;
+        private bool _loggedNullOnce;
+
+        private void Update()
+        {
+            var pad = Gamepad.current;
+            if (pad == null)
+            {
+                if (!_loggedNullOnce)
+                {
+                    _loggedNullOnce = true;
+                    Debug.Log("[GamepadValueDebugger] Gamepad.current == null. " +
+                              "If a controller is connected, it isn't the active gamepad yet — " +
+                              "press a button on it. If pressing buttons never changes this, its " +
+                              "state events aren't reaching Unity (transport/platform issue).");
+                }
+                return;
+            }
+            _loggedNullOnce = false;
+
+            if (pad.name != _lastDeviceName)
+            {
+                _lastDeviceName = pad.name;
+                Debug.Log($"[GamepadValueDebugger] Active gamepad = '{pad.name}' " +
+                          $"(displayName '{pad.displayName}', layout '{pad.layout}'). " +
+                          "Move the sticks and press buttons; values below are read straight from it.");
+            }
+
+            // Buttons: log on the frame they're pressed.
+            LogButtonIfPressed(pad.buttonSouth, "buttonSouth (A)");
+            LogButtonIfPressed(pad.buttonEast, "buttonEast (B)");
+            LogButtonIfPressed(pad.buttonWest, "buttonWest (X)");
+            LogButtonIfPressed(pad.buttonNorth, "buttonNorth (Y)");
+            LogButtonIfPressed(pad.leftShoulder, "leftShoulder (L1)");
+            LogButtonIfPressed(pad.rightShoulder, "rightShoulder (R1 / boost)");
+            LogButtonIfPressed(pad.leftTrigger, "leftTrigger (L2)");
+            LogButtonIfPressed(pad.rightTrigger, "rightTrigger (R2)");
+            LogButtonIfPressed(pad.startButton, "start");
+            LogButtonIfPressed(pad.selectButton, "select");
+            if (pad.dpad != null)
+            {
+                LogButtonIfPressed(pad.dpad.up, "dpad/up");
+                LogButtonIfPressed(pad.dpad.down, "dpad/down");
+                LogButtonIfPressed(pad.dpad.left, "dpad/left");
+                LogButtonIfPressed(pad.dpad.right, "dpad/right");
+            }
+
+            // Sticks: throttled, on meaningful movement/change.
+            Vector2 l = pad.leftStick.ReadValue();
+            Vector2 r = pad.rightStick.ReadValue();
+
+            bool leftMoved = l.magnitude > StickLogThreshold && (l - _lastLeft).magnitude > StickLogDelta;
+            bool rightMoved = r.magnitude > StickLogThreshold && (r - _lastRight).magnitude > StickLogDelta;
+
+            if ((leftMoved || rightMoved) && Time.unscaledTime - _lastLogTime >= MinLogInterval)
+            {
+                _lastLogTime = Time.unscaledTime;
+                _lastLeft = l;
+                _lastRight = r;
+                Debug.Log($"[GamepadValueDebugger] leftStick={Fmt(l)}  rightStick={Fmt(r)}  " +
+                          $"L2={pad.leftTrigger.ReadValue():F2}  R2={pad.rightTrigger.ReadValue():F2}");
+            }
+        }
+
+        private void LogButtonIfPressed(UnityEngine.InputSystem.Controls.ButtonControl button, string label)
+        {
+            if (button != null && button.wasPressedThisFrame)
+                Debug.Log($"[GamepadValueDebugger] PRESSED {label}");
+        }
+
+        private static string Fmt(Vector2 v) => $"({v.x:F2}, {v.y:F2})";
+    }
+}
diff --git a/Assets/_Scripts/Controller/IO/HidGamepadSupport.cs b/Assets/_Scripts/Controller/IO/HidGamepadSupport.cs
index 1bb5a642c..63a61c907 100644
--- a/Assets/_Scripts/Controller/IO/HidGamepadSupport.cs
+++ b/Assets/_Scripts/Controller/IO/HidGamepadSupport.cs
@@ -84,6 +84,14 @@ namespace CosmicShore.Gameplay
         /// </summary>
         public static bool VerboseLogging = true;
 
+        /// <summary>
+        /// When true, auto-installs <see cref="GamepadValueDebugger"/> at startup, which logs the
+        /// live values read from <c>Gamepad.current</c> as you move sticks / press buttons. Used
+        /// to verify a promoted controller's mapping actually produces correct input. Turn off
+        /// once a pad is confirmed working.
+        /// </summary>
+        public static bool DebugReadValues = true;
+
         /// <summary>
         /// Optional per-device corrections, keyed by (vendorId, productId). The generic
         /// descriptor-driven mapping is correct for the vast majority of pads; entries here
@@ -111,6 +119,17 @@ namespace CosmicShore.Gameplay
         [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
         private static void InitRuntime() => Register();
 
+        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
+        private static void InstallValueDebugger()
+        {
+            if (!DebugReadValues)
+                return;
+
+            var go = new GameObject("HidGamepadValueDebugger") { hideFlags = HideFlags.HideAndDontSave };
+            Object.DontDestroyOnLoad(go);
+            go.AddComponent<GamepadValueDebugger>();
+        }
+
 #if UNITY_EDITOR
         [InitializeOnLoadMethod]
         private static void InitEditor() => Register();
```

</details>

### `f235db3b7` — fix(input): stop infinite recursion that crashed the editor (stack overflow)

_Claude, 2026-06-11 01:21:14 +0000_

```text
The Editor-prev.log crash backtrace showed an unbounded recursion:
RegisterLayoutBuilder(matcher) -> RecreateDevicesUsingLayoutWithInferiorMatch
-> TryFindMatchingControlLayout -> OnFindLayoutForDevice -> TryBuildAndRegisterLayout
-> RegisterLayoutBuilder -> ... until the stack guard tripped and Unity crashed.

Cause: registering the generated layout WITH a matcher makes the Input System
synchronously recreate every matching device, which re-invokes our
onFindLayoutForDevice handler. The dedup marker (s_RegisteredLayouts.Add) ran
AFTER RegisterLayoutBuilder returned, so the re-entrant calls never saw the
layout as already-registered and kept re-registering.

Fixes:
- Add the layout name to s_RegisteredLayouts BEFORE calling RegisterLayoutBuilder,
  and early-return the name when already present, so the recreate cascade
  short-circuits at depth 1 instead of recursing. Remove the marker on failure.
- Wrap the deferred BuildLayout closure in try/catch returning a neutral
  Gamepad-derived layout, so it can never throw into native input code during
  device creation.
- Snapshot InputSystem.devices before the connected-device sweep, since
  promoting a device recreates it and mutates the collection mid-iteration.
```

```text
 Assets/_Scripts/Controller/IO/HidGamepadSupport.cs | 139 +++++++++++++++++++++++++++++++++------------------
 1 file changed, 91 insertions(+), 48 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 166 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/IO/HidGamepadSupport.cs b/Assets/_Scripts/Controller/IO/HidGamepadSupport.cs
index 63a61c907..c74aec96b 100644
--- a/Assets/_Scripts/Controller/IO/HidGamepadSupport.cs
+++ b/Assets/_Scripts/Controller/IO/HidGamepadSupport.cs
@@ -159,7 +159,13 @@ namespace CosmicShore.Gameplay
         {
             try
             {
-                foreach (var device in InputSystem.devices)
+                // Snapshot first: promoting a device causes the Input System to recreate it,
+                // which mutates InputSystem.devices mid-iteration.
+                var snapshot = new List<InputDevice>();
+                foreach (var d in InputSystem.devices)
+                    snapshot.Add(d);
+
+                foreach (var device in snapshot)
                 {
                     if (device is Gamepad)
                         continue;
@@ -257,55 +263,69 @@ namespace CosmicShore.Gameplay
             }
 
             var layoutName = MakeLayoutName(description, descriptor);
-            if (!s_RegisteredLayouts.Contains(layoutName))
+
+            // Already registered this model? Just use it.
+            //
+            // This early-out is also what keeps us from crashing: RegisterLayoutBuilder with a
+            // matcher makes the Input System SYNCHRONOUSLY recreate every device that matches,
+            // which re-invokes this very handler (RecreateDevicesUsingLayoutWithInferiorMatch ->
+            // TryFindMatchingControlLayout -> OnFindLayoutForDevice). Because we add the name to
+            // s_RegisteredLayouts BEFORE calling RegisterLayoutBuilder (below), that re-entrant
+            // call lands here and returns the name instead of registering again. Without the
+            // mark-before-register ordering this recursed until the stack overflowed and took
+            // the editor down with it.
+            if (s_RegisteredLayouts.Contains(layoutName))
+                return layoutName;
+
+            Quirks.TryGetValue((descriptor.vendorId, descriptor.productId), out var quirk);
+
+            int reportSizeBytes = Mathf.Max(1, descriptor.inputReportSize);
+            var capturedMap = map;
+            var capturedQuirk = quirk;
+            var capturedName = layoutName;
+            var displayName = string.IsNullOrEmpty(description.product)
+                ? "HID Gamepad"
+                : description.product;
+
+            // Match on vendor/product when the device reports them; otherwise fall back to
+            // manufacturer/product strings so zero-id Bluetooth pads still resolve uniquely.
+            var matcher = new InputDeviceMatcher().WithInterface("HID");
+            if (descriptor.vendorId != 0 || descriptor.productId != 0)
             {
-                Quirks.TryGetValue((descriptor.vendorId, descriptor.productId), out var quirk);
-
-                int reportSizeBytes = Mathf.Max(1, descriptor.inputReportSize);
-                var capturedMap = map;
-                var capturedQuirk = quirk;
-                var capturedName = layoutName;
-                var displayName = string.IsNullOrEmpty(description.product)
-                    ? "HID Gamepad"
-                    : description.product;
-
-                // Match on vendor/product when the device reports them; otherwise fall back to
-                // manufacturer/product strings so zero-id Bluetooth pads still resolve uniquely.
-                var matcher = new InputDeviceMatcher().WithInterface("HID");
-                if (descriptor.vendorId != 0 || descriptor.productId != 0)
-                {
-                    matcher = matcher
-                        .WithCapability("vendorId", descriptor.vendorId)
-                        .WithCapability("productId", descriptor.productId);
-                }
-                else
-                {
-                    if (!string.IsNullOrEmpty(description.manufacturer))
-                        matcher = matcher.WithManufacturer(description.manufacturer);
-                    if (!string.IsNullOrEmpty(description.product))
-                        matcher = matcher.WithProduct(description.product);
-                }
+                matcher = matcher
+                    .WithCapability("vendorId", descriptor.vendorId)
+                    .WithCapability("productId", descriptor.productId);
+            }
+            else
+            {
+                if (!string.IsNullOrEmpty(description.manufacturer))
+                    matcher = matcher.WithManufacturer(description.manufacturer);
+                if (!string.IsNullOrEmpty(description.product))
+                    matcher = matcher.WithProduct(description.product);
+            }
 
-                try
-                {
-                    InputSystem.RegisterLayoutBuilder(
-                        () => BuildLayout(capturedName, displayName, capturedMap, capturedQuirk, reportSizeBytes),
-                        capturedName,
-                        baseLayout: "Gamepad",
-                        matches: matcher);
-                    s_RegisteredLayouts.Add(layoutName);
-                    UnityEngine.Debug.Log($"[HidGamepadSupport] PROMOTED HID device '{displayName}' " +
-                                $"(vendor 0x{descriptor.vendorId:X4}, product 0x{descriptor.productId:X4}) " +
-                                $"to Gamepad layout '{layoutName}' — leftStick={map.HasLeftStick}, " +
-                                $"rightStick={map.HasRightStick}, buttons={map.ButtonCount}. " +
-                                $"It should now appear as Gamepad.current.");
-                }
-                catch (System.Exception e)
-                {
-                    UnityEngine.Debug.LogWarning($"[HidGamepadSupport] Failed to register Gamepad layout for " +
-                                       $"'{displayName}': {e.Message}. Falling back to default Joystick.");
-                    return null;
-                }
+            // Mark BEFORE registering so the re-entrant recreate cascade short-circuits above.
+            s_RegisteredLayouts.Add(layoutName);
+            try
+            {
+                InputSystem.RegisterLayoutBuilder(
+                    () => BuildLayout(capturedName, displayName, capturedMap, capturedQuirk, reportSizeBytes),
+                    capturedName,
+                    baseLayout: "Gamepad",
+                    matches: matcher);
+                UnityEngine.Debug.Log($"[HidGamepadSupport] PROMOTED HID device '{displayName}' " +
+                            $"(vendor 0x{descriptor.vendorId:X4}, product 0x{descriptor.productId:X4}) " +
+                            $"to Gamepad layout '{layoutName}' — leftStick={map.HasLeftStick}, " +
+                            $"rightStick={map.HasRightStick}, buttons={map.ButtonCount}. " +
+                            $"It should now appear as Gamepad.current.");
+            }
+            catch (System.Exception e)
+            {
+                // Allow a later retry and don't leave a phantom registration behind.
+                s_RegisteredLayouts.Remove(layoutName);
+                UnityEngine.Debug.LogWarning($"[HidGamepadSupport] Failed to register Gamepad layout for " +
+                                   $"'{displayName}': {e.Message}. Falling back to default Joystick.");
+                return null;
             }
 
             return layoutName;
@@ -454,6 +474,29 @@ namespace CosmicShore.Gameplay
 
         private static InputControlLayout BuildLayout(string layoutName, string displayName,
             ControlMap map, Quirk quirk, int reportSizeBytes)
+        {
+            // This closure is invoked by the Input System later, during device creation, OUTSIDE
+            // the try/catch around RegisterLayoutBuilder. If it threw, the exception would
+            // propagate into native input code. Guard it: on any failure, fall back to a bare
+            // Gamepad-derived layout (neutral, never crashes) rather than throwing.
+            try
+            {
+                return BuildLayoutCore(layoutName, displayName, map, quirk, reportSizeBytes);
+            }
+            catch (System.Exception e)
```

</details>

### `08755243b` — fix(input): derive axis format/centering from HID logical range (signed axes)

_Claude, 2026-06-11 01:51:00 +0000_

```text
Controller now drives the ship but the sticks rest off-center ("drifts hard
to one side, only centers at full opposite deflection"). Cause: the generated
layout hardcoded every axis as an UNSIGNED byte normalized with center at 0.5,
but the Nimbus (like many pads) reports SIGNED axes (rest = 0, range
-128..127). Read as unsigned, rest (raw 0) computed to -1 (full deflection)
and physical extreme (+127) to ~0 (center) — exactly the reported symptom.

Mirror Unity's own HID handling (HID.cs DetermineFormat / isSigned /
DetermineAxisNormalizationParameters / minFloatValue / maxFloatValue):
- Capture each axis element's logicalMin/logicalMax (and size) in the control map.
- isSigned = logicalMin < 0 -> pick SBYT/SHRT/INT vs BYTE/USHT/UINT format.
- Build the normalize processor from the logical range so any axis (signed or
  unsigned, any bit width, arbitrary center) maps to a clean -1..1 with 0 at rest.
- Absent/parked stick axes now use a signed format on the always-zero dead byte
  so they read 0 (centered) instead of a hard deflection (latent bug).

Refactor ControlMap to per-axis AxisInfo (offset/size/logicalMin/logicalMax).
```

```text
 Assets/_Scripts/Controller/IO/HidGamepadSupport.cs | 183 ++++++++++++++++++++++++++++++++++-----------------
 1 file changed, 121 insertions(+), 62 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 255 lines)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/IO/HidGamepadSupport.cs b/Assets/_Scripts/Controller/IO/HidGamepadSupport.cs
index c74aec96b..25deddb90 100644
--- a/Assets/_Scripts/Controller/IO/HidGamepadSupport.cs
+++ b/Assets/_Scripts/Controller/IO/HidGamepadSupport.cs
@@ -50,10 +50,6 @@ namespace CosmicShore.Gameplay
     /// </summary>
     public static class HidGamepadSupport
     {
-        // HID usage-page identifiers (USB HID Usage Tables).
-        private const int UsagePageGenericDesktop = 0x01;
-        private const int UsagePageButton = 0x09;
-
         // Generic Desktop usages.
         private const int GD_Joystick = 0x04;
         private const int GD_Gamepad = 0x05;
@@ -394,11 +390,20 @@ namespace CosmicShore.Gameplay
             }
         }
 
+        private struct AxisInfo
+        {
+            public bool Present;
+            public int OffsetBits;
+            public int SizeBits;
+            public int LogicalMin;
+            public int LogicalMax;
+        }
+
         private struct ControlMap
         {
-            public bool HasLeftStick, HasRightStick;
-            public int LeftStickXOffsetBits, LeftStickYOffsetBits, LeftStickBits;
-            public int RightStickXOffsetBits, RightStickYOffsetBits, RightStickBits;
+            public AxisInfo LeftX, LeftY, RightX, RightY;
+            public bool HasLeftStick => LeftX.Present && LeftY.Present;
+            public bool HasRightStick => RightX.Present && RightY.Present;
 
             public bool HasHat;
             public int HatOffsetBits, HatSizeBits;
@@ -422,53 +427,45 @@ namespace CosmicShore.Gameplay
                 DpadRightBit = -1,
             };
 
-            // Track which generic-desktop axes we've consumed so the second pair falls through
-            // to the right stick.
-            bool haveX = false, haveY = false, haveRightX = false, haveRightY = false;
-
             foreach (var element in descriptor.elements)
             {
                 if (element.reportType != HID.HIDReportType.Input)
                     continue;
 
-                int offset = element.reportOffsetInBits;
-                int size = element.reportSizeInBits;
+                var axis = new AxisInfo
+                {
+                    Present = true,
+                    OffsetBits = element.reportOffsetInBits,
+                    SizeBits = element.reportSizeInBits,
+                    LogicalMin = element.logicalMin,
+                    LogicalMax = element.logicalMax,
+                };
 
                 if (element.usagePage == HID.UsagePage.GenericDesktop)
                 {
                     switch (element.usage)
                     {
-                        case GD_X:
-                            map.LeftStickXOffsetBits = offset; map.LeftStickBits = size; haveX = true; break;
-                        case GD_Y:
-                            map.LeftStickYOffsetBits = offset; map.LeftStickBits = size; haveY = true; break;
-                        case GD_Z:
-                            map.RightStickXOffsetBits = offset; map.RightStickBits = size; haveRightX = true; break;
-                        case GD_Rz:
-                            map.RightStickYOffsetBits = offset; map.RightStickBits = size; haveRightY = true; break;
-                        case GD_Rx:
-                            if (!haveRightX) { map.RightStickXOffsetBits = offset; map.RightStickBits = size; haveRightX = true; }
-                            break;
-                        case GD_Ry:
-                            if (!haveRightY) { map.RightStickYOffsetBits = offset; map.RightStickBits = size; haveRightY = true; }
-                            break;
+                        case GD_X: map.LeftX = axis; break;
+                        case GD_Y: map.LeftY = axis; break;
+                        case GD_Z: map.RightX = axis; break;
+                        case GD_Rz: map.RightY = axis; break;
+                        case GD_Rx: if (!map.RightX.Present) map.RightX = axis; break;
+                        case GD_Ry: if (!map.RightY.Present) map.RightY = axis; break;
                         case GD_HatSwitch:
-                            map.HasHat = true; map.HatOffsetBits = offset; map.HatSizeBits = size; break;
-                        case GD_DpadUp: map.DpadUpBit = offset; break;
-                        case GD_DpadDown: map.DpadDownBit = offset; break;
-                        case GD_DpadRight: map.DpadRightBit = offset; break;
-                        case GD_DpadLeft: map.DpadLeftBit = offset; break;
+                            map.HasHat = true; map.HatOffsetBits = axis.OffsetBits; map.HatSizeBits = axis.SizeBits; break;
+                        case GD_DpadUp: map.DpadUpBit = axis.OffsetBits; break;
+                        case GD_DpadDown: map.DpadDownBit = axis.OffsetBits; break;
+                        case GD_DpadRight: map.DpadRightBit = axis.OffsetBits; break;
+                        case GD_DpadLeft: map.DpadLeftBit = axis.OffsetBits; break;
                     }
                 }
-                else if ((int)element.usagePage == UsagePageButton)
+                else if (element.usagePage == HID.UsagePage.Button)
                 {
                     // Each button element is one bit; collect in report order.
-                    map.ButtonBitOffsets.Add(offset);
+                    map.ButtonBitOffsets.Add(element.reportOffsetInBits);
                 }
             }
 
-            map.HasLeftStick = haveX && haveY;
-            map.HasRightStick = haveRightX && haveRightY;
             return map;
         }
 
@@ -510,16 +507,17 @@ namespace CosmicShore.Gameplay
                 .Extend("Gamepad");
 
             // ---- Sticks ----
+            // A signed axis parked on the always-zero dead byte reads 0 = centered.
+            var deadAxis = new AxisInfo { Present = false, OffsetBits = deadByte * 8, SizeBits = 8, LogicalMin = -128, LogicalMax = 127 };
+
             AddStick(builder, "leftStick",
-                map.HasLeftStick ? map.LeftStickXOffsetBits : deadByte * 8,
-                map.HasLeftStick ? map.LeftStickYOffsetBits : deadByte * 8,
-                map.HasLeftStick ? map.LeftStickBits : 8,
+                map.HasLeftStick ? map.LeftX : deadAxis,
+                map.HasLeftStick ? map.LeftY : deadAxis,
                 invertY: !quirk.InvertLeftStickY);
 
             AddStick(builder, "rightStick",
-                map.HasRightStick ? map.RightStickXOffsetBits : deadByte * 8,
-                map.HasRightStick ? map.RightStickYOffsetBits : deadByte * 8,
-                map.HasRightStick ? map.RightStickBits : 8,
+                map.HasRightStick ? map.RightX : deadAxis,
+                map.HasRightStick ? map.RightY : deadAxis,
                 invertY: !quirk.InvertRightStickY);
 
             // ---- Buttons (report order -> gamepad semantic order) ----
@@ -571,35 +569,96 @@ namespace CosmicShore.Gameplay
         }
 
         private static void AddStick(InputControlLayout.Builder builder, string name,
-            int xOffsetBits, int yOffsetBits, int sizeBits, bool invertY)
+            AxisInfo x, AxisInfo y, bool invertY)
         {
             // Parent at byte 0 so the absolute child offsets below resolve correctly.
             builder.AddControl(name)
                 .WithLayout("Stick")
                 .WithByteOffset(0);
 
```

</details>

### `c8ad55256` — diag(input): dump per-axis descriptor (logicalMin/Max/format) + raw stick values

_Claude, 2026-06-11 02:08:36 +0000_

```text
The signed-axis fix flipped the rest-drift from one side to the other, which
means I'm still misreading the Nimbus's axis encoding. Stop guessing and
capture the ground truth:
- Log each generated axis once: offset, size, logicalMin/logicalMax, the
  signed decision, chosen format, and the normalize params string.
- Log the UNPROCESSED leftStick x/y (value out of the state-block format,
  before normalize/invert) alongside the processed value, to reveal whether
  the format choice is correct.

These two together pin down the exact encoding deterministically so the
centering/normalization can be set correctly rather than inferred from
in-game behavior. No mapping logic changed in this commit.
```

```text
 Assets/_Scripts/Controller/IO/GamepadValueDebugger.cs | 7 ++++++-
 Assets/_Scripts/Controller/IO/HidGamepadSupport.cs    | 6 ++++++
 2 files changed, 12 insertions(+), 1 deletion(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/IO/GamepadValueDebugger.cs b/Assets/_Scripts/Controller/IO/GamepadValueDebugger.cs
index e45617677..e3fcc78c0 100644
--- a/Assets/_Scripts/Controller/IO/GamepadValueDebugger.cs
+++ b/Assets/_Scripts/Controller/IO/GamepadValueDebugger.cs
@@ -90,8 +90,13 @@ namespace CosmicShore.Gameplay
                 _lastLogTime = Time.unscaledTime;
                 _lastLeft = l;
                 _lastRight = r;
+                // Unprocessed = value straight out of the state-block format, before normalize/
+                // invert processors. Reveals whether the axis format (signed vs unsigned) is right.
+                float lxu = pad.leftStick.x.ReadUnprocessedValue();
+                float lyu = pad.leftStick.y.ReadUnprocessedValue();
                 Debug.Log($"[GamepadValueDebugger] leftStick={Fmt(l)}  rightStick={Fmt(r)}  " +
-                          $"L2={pad.leftTrigger.ReadValue():F2}  R2={pad.rightTrigger.ReadValue():F2}");
+                          $"L2={pad.leftTrigger.ReadValue():F2}  R2={pad.rightTrigger.ReadValue():F2}  " +
+                          $"| leftStick raw(x,y)=({lxu:F2}, {lyu:F2})");
             }
         }
 
diff --git a/Assets/_Scripts/Controller/IO/HidGamepadSupport.cs b/Assets/_Scripts/Controller/IO/HidGamepadSupport.cs
index 25deddb90..b41b4d553 100644
--- a/Assets/_Scripts/Controller/IO/HidGamepadSupport.cs
+++ b/Assets/_Scripts/Controller/IO/HidGamepadSupport.cs
@@ -604,6 +604,12 @@ namespace CosmicShore.Gameplay
 
             if (!string.IsNullOrEmpty(parameters))
                 c.WithParameters(parameters);
+
+            if (VerboseLogging)
+                UnityEngine.Debug.Log($"[HidGamepadSupport] axis '{name}': present={axis.Present} " +
+                    $"offsetBits={axis.OffsetBits} sizeBits={sizeBits} logicalMin={axis.LogicalMin} " +
+                    $"logicalMax={axis.LogicalMax} signed={signed} format={format} invert={invert} " +
+                    $"params='{parameters}'");
         }
 
         private static string AxisFormat(int sizeBits, bool signed)
```

</details>

### `2b366cdec` — fix(input): sign-extend HID logical min/max (Nimbus reports 129..127 = -127..127)

_Claude, 2026-06-11 02:40:51 +0000_

```text
The descriptor dump revealed the true cause: the Nimbus reports leftStick/x as
logicalMin=129, logicalMax=127 for an 8-bit axis. min > max is nonsensical
unless logicalMin is a negative Logical Minimum (-127) that the HID parser
surfaced as unsigned 129. With logicalMin=129 >= 0 we treated the axis as
UNSIGNED and built a degenerate normalize (min=0.506, max=0.498 — inverted,
~0.008 wide), squashing the whole range into a sliver -> violent off-center drift.

Fix: when logicalMin > logicalMax, sign-extend both bounds for the field width
(129 -> 129-256 = -127). The axis is then correctly signed (-127..127), gets
SBYT format and a centered normalize, so rest reads ~0. This is the same quirk
Unity's own joystick fallback mishandles, which is why the Nimbus is erratic as
a plain Joystick.
```

```text
 Assets/_Scripts/Controller/IO/HidGamepadSupport.cs | 28 ++++++++++++++++++++++++++--
 1 file changed, 26 insertions(+), 2 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/Controller/IO/HidGamepadSupport.cs b/Assets/_Scripts/Controller/IO/HidGamepadSupport.cs
index b41b4d553..110052404 100644
--- a/Assets/_Scripts/Controller/IO/HidGamepadSupport.cs
+++ b/Assets/_Scripts/Controller/IO/HidGamepadSupport.cs
@@ -432,13 +432,17 @@ namespace CosmicShore.Gameplay
                 if (element.reportType != HID.HIDReportType.Input)
                     continue;
 
+                int logicalMin = element.logicalMin;
+                int logicalMax = element.logicalMax;
+                SignExtendLogicalRange(ref logicalMin, ref logicalMax, element.reportSizeInBits);
+
                 var axis = new AxisInfo
                 {
                     Present = true,
                     OffsetBits = element.reportOffsetInBits,
                     SizeBits = element.reportSizeInBits,
-                    LogicalMin = element.logicalMin,
-                    LogicalMax = element.logicalMax,
+                    LogicalMin = logicalMin,
+                    LogicalMax = logicalMax,
                 };
 
                 if (element.usagePage == HID.UsagePage.GenericDesktop)
@@ -612,6 +616,26 @@ namespace CosmicShore.Gameplay
                     $"params='{parameters}'");
         }
 
+        // HID encodes a negative Logical Minimum within the field's own bit width. Some
+        // descriptors (notably the SteelSeries Nimbus) surface it as a large unsigned value —
+        // e.g. logicalMin=129, logicalMax=127 for an 8-bit axis, which actually means
+        // -127..127, a signed axis centered at 0. The tell is logicalMin > logicalMax. When we
+        // see that, sign-extend the bounds for the field width so the axis is recognized as
+        // signed and centers correctly. (Unity's own HID fallback misses this, which is why the
+        // Nimbus is erratic as a plain Joystick too.)
+        private static void SignExtendLogicalRange(ref int logicalMin, ref int logicalMax, int sizeBits)
+        {
+            if (sizeBits <= 0 || sizeBits >= 32 || logicalMin <= logicalMax)
+                return;
+
+            long range = 1L << sizeBits;
+            long signBit = 1L << (sizeBits - 1);
+            if (logicalMin >= signBit)
+                logicalMin = (int)(logicalMin - range);
+            if (logicalMax >= signBit)
+                logicalMax = (int)(logicalMax - range);
+        }
+
         private static string AxisFormat(int sizeBits, bool signed)
         {
             switch (sizeBits)
```

</details>
