using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using ActionMap = System.Collections.Generic.Dictionary<CosmicShore.Data.InputEvents, System.Collections.Generic.List<CosmicShore.Gameplay.ShipActionSO>>;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The autopilot lookup (<c>R_VesselActionHandler.TryGetInputForAction</c> /
    /// <c>TryGetBoundAction</c>) must hand out the control the vessel's ACTIVE device actually
    /// binds — the one the press gate (<c>HasAction</c>) then accepts.
    ///
    /// <para>It did not. It swept shared → touch → gamepad whatever the device, and the Squirrel binds
    /// its drift and its Boost Ring ONLY in the two device override maps (touch: 12 / 11, pad: 2 / 1).
    /// So the lookup answered with the touch controls, and on a PC — Gamepad, Keyboard, DualMouse and
    /// MouseKeyboard all resolve against the pad overrides — the press was refused: the Skim Race AI
    /// could never drift or lay a ring on Windows, and nothing said so.</para>
    ///
    /// <para>The invariant pinned here is the agreement itself: for every device, whatever the lookup
    /// returns is a control whose press runs the action it named. The handler's internal statics are
    /// tested over explicit maps so no vessel, player or input status has to exist.</para>
    /// </summary>
    [TestFixture]
    public class DeviceAwareActionLookupTests
    {
        const string SquirrelPrefabPath = "Assets/_Prefabs/Spacevessels/Squirrel.prefab";

        static IEnumerable<InputDeviceType> AllDevices =>
            Enum.GetValues(typeof(InputDeviceType)).Cast<InputDeviceType>();

        readonly List<Object> _made = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _made)
                if (o) Object.DestroyImmediate(o);
            _made.Clear();
        }

        T Make<T>() where T : ShipActionSO
        {
            var so = ScriptableObject.CreateInstance<T>();
            _made.Add(so);
            return so;
        }

        static ActionMap Map(params (InputEvents input, ShipActionSO[] actions)[] entries)
        {
            var map = new ActionMap();
            foreach (var (input, actions) in entries) map[input] = new List<ShipActionSO>(actions);
            return map;
        }

        static InputEvents AssertResolvesAndPresses<T>(ActionMap shared, ActionMap overrides, string context)
            where T : class
        {
            Assert.IsTrue(R_VesselActionHandler.TryFindPressableAction<T>(shared, overrides, out var action, out var input),
                $"{context}: no control found for {typeof(T).Name}.");
            Assert.IsTrue(R_VesselActionHandler.TryGetPressedActions(shared, overrides, input, out var pressed),
                $"{context}: the lookup returned {input}, which the press gate REFUSES - the bug this pins.");
            Assert.IsTrue(pressed.Contains(action as ShipActionSO),
                $"{context}: pressing {input} runs something other than the {typeof(T).Name} the lookup named.");
            return input;
        }

        // ── The shape that shipped broken: an ability bound only in the device overrides ──────────

        [Test]
        public void OverrideOnlyAbility_ResolvesToTheActiveDevicesOwnControl_OnEveryDevice()
        {
            var drift = Make<DriftActionSO>();
            var shared = Map();
            var touch = Map((InputEvents.OnlyLeftStickAction, new ShipActionSO[] { drift }));
            var pad = Map((InputEvents.LeftStickAction, new ShipActionSO[] { drift }));

            foreach (var device in AllDevices)
            {
                var overrides = R_VesselActionHandler.OverridesFor(device, touch, pad);
                var input = AssertResolvesAndPresses<DriftActionSO>(shared, overrides, device.ToString());
                var expected = device == InputDeviceType.Touch
                    ? InputEvents.OnlyLeftStickAction
                    : InputEvents.LeftStickAction;
                Assert.AreEqual(expected, input, $"{device}: wrong control for the drift.");
            }
        }

        [Test]
        public void TheTouchControl_IsRefusedOnEveryPcDevice()
        {
            // Why the old device-blind answer was worse than none: the control it handed out is not
            // bound at all on a PC, so the AI's press was dropped silently.
            var drift = Make<DriftActionSO>();
            var touch = Map((InputEvents.OnlyLeftStickAction, new ShipActionSO[] { drift }));
            var pad = Map((InputEvents.LeftStickAction, new ShipActionSO[] { drift }));

            foreach (var device in AllDevices.Where(d => d != InputDeviceType.Touch))
            {
                var overrides = R_VesselActionHandler.OverridesFor(device, touch, pad);
                Assert.IsFalse(R_VesselActionHandler.TryGetPressedActions(Map(), overrides, InputEvents.OnlyLeftStickAction, out _),
                    $"{device}: the touch control resolved - this test no longer describes the press gate.");
            }
        }

        [Test]
        public void AbilityBoundOnlyForAnotherDevice_IsNotHandedOut()
        {
            // A control from the other device's map either does nothing here or fires whatever the
            // shared map puts on it. Pressing a wrong ability is worse than pressing nothing.
            var drift = Make<DriftActionSO>();
            var ring = Make<SquirrelTubeActionSO>();
            var shared = Map((InputEvents.OnlyLeftStickAction, new ShipActionSO[] { ring }));
            var touch = Map((InputEvents.OnlyLeftStickAction, new ShipActionSO[] { drift }));

            var padOverrides = R_VesselActionHandler.OverridesFor(InputDeviceType.Gamepad, touch, Map());
            Assert.IsFalse(R_VesselActionHandler.TryFindPressableAction<DriftActionSO>(shared, padOverrides, out _, out _),
                "On a pad the drift is not bound; the lookup must say so rather than hand out the touch control " +
                "(which on this vessel fires the ring).");
        }

        // ── The shared map and how an override shadows it ─────────────────────────────────────────

        [Test]
        public void SharedBinding_ShadowedByTheActiveOverride_IsNotReturned()
        {
            var drift = Make<DriftActionSO>();
            var ring = Make<SquirrelTubeActionSO>();
            var shared = Map((InputEvents.LeftStickAction, new ShipActionSO[] { drift }));
            var pad = Map((InputEvents.LeftStickAction, new ShipActionSO[] { ring }));

            var padOverrides = R_VesselActionHandler.OverridesFor(InputDeviceType.Gamepad, Map(), pad);
            Assert.IsFalse(R_VesselActionHandler.TryFindPressableAction<DriftActionSO>(shared, padOverrides, out _, out _),
                "Pressing LeftStickAction on a pad runs the override (the ring), not the shared drift.");

            var touchOverrides = R_VesselActionHandler.OverridesFor(InputDeviceType.Touch, Map(), pad);
            Assert.AreEqual(InputEvents.LeftStickAction,
                AssertResolvesAndPresses<DriftActionSO>(shared, touchOverrides, "Touch"),
                "Touch has no override on that control, so the shared drift is what a press runs.");
        }

        [Test]
        public void EmptyOverrideEntry_DoesNotShadowTheSharedBinding()
        {
            // The press falls back to the shared list when the override list is empty; so must the lookup.
            var drift = Make<DriftActionSO>();
            var shared = Map((InputEvents.LeftStickAction, new ShipActionSO[] { drift }));
            var pad = Map((InputEvents.LeftStickAction, Array.Empty<ShipActionSO>()));

            var overrides = R_VesselActionHandler.OverridesFor(InputDeviceType.Gamepad, Map(), pad);
            Assert.AreEqual(InputEvents.LeftStickAction,
                AssertResolvesAndPresses<DriftActionSO>(shared, overrides, "Gamepad, empty override entry"));
        }

        [Test]
        public void SharedOnlyVessel_ResolvesTheSameControlOnEveryDevice()
        {
            // Every hull but the Squirrel binds its autopilot abilities in the shared map (Butterfly,
            // Dolphin, Scarab): the device-aware lookup must give them exactly the answer they had.
            var drift = Make<DriftActionSO>();
            var shared = Map((InputEvents.LeftStickAction, new ShipActionSO[] { drift }));

            foreach (var device in AllDevices)
            {
                var overrides = R_VesselActionHandler.OverridesFor(device, Map(), Map());
                Assert.AreEqual(InputEvents.LeftStickAction,
                    AssertResolvesAndPresses<DriftActionSO>(shared, overrides, device.ToString()));
            }
        }

        [Test]
        public void NoDeviceYet_ResolvesAgainstTheSharedMapAlone()
        {
            // Before there is an input status (GetActiveOverrides returns null) a press runs the
            // shared map alone, so that is all the lookup may promise: an override-only ability is
            // not pressable yet, a shared one is.
            var drift = Make<DriftActionSO>();
            Assert.IsFalse(R_VesselActionHandler.TryFindPressableAction<DriftActionSO>(Map(), null, out _, out _));

            var shared = Map((InputEvents.LeftStickAction, new ShipActionSO[] { drift }));
            Assert.AreEqual(InputEvents.LeftStickAction, AssertResolvesAndPresses<DriftActionSO>(shared, null, "no device"));
        }

        // ── The shipped asset ──────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Builds a handler map from the prefab's SERIALIZED list, the way <c>Initialize</c> builds
        /// the runtime one, minus the per-action <c>Initialize(vesselStatus)</c> (there is no vessel).
        /// </summary>
        static ActionMap SerializedMap(SerializedObject handler, string field)
        {
            var list = handler.FindProperty(field);
            Assert.IsNotNull(list, $"R_VesselActionHandler.{field} is gone - update this test with the rename.");
            var map = new ActionMap();
            for (int i = 0; i < list.arraySize; i++)
            {
                var entry = list.GetArrayElementAtIndex(i);
                var input = (InputEvents)entry.FindPropertyRelative("InputEvent").intValue;
                if (!map.TryGetValue(input, out var actions)) map[input] = actions = new List<ShipActionSO>();
                var refs = entry.FindPropertyRelative("ShipActions");
                for (int j = 0; j < refs.arraySize; j++)
                    if (refs.GetArrayElementAtIndex(j).objectReferenceValue is ShipActionSO so) actions.Add(so);
            }
            return map;
        }

        [Test]
        public void ShippedSquirrel_DriftAndBoostRing_ArePressableOnEveryDevice()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SquirrelPrefabPath);
            Assert.IsNotNull(prefab, $"{SquirrelPrefabPath} not found.");
            var handler = prefab.GetComponentInChildren<R_VesselActionHandler>(true);
            Assert.IsNotNull(handler, "The Squirrel has no R_VesselActionHandler.");

            var so = new SerializedObject(handler);
            var shared = SerializedMap(so, "_inputEventShipActions");
            var touch = SerializedMap(so, "_touchActionOverrides");
            var pad = SerializedMap(so, "_gamepadActionOverrides");

            foreach (var device in AllDevices)
            {
                var overrides = R_VesselActionHandler.OverridesFor(device, touch, pad);
                AssertResolvesAndPresses<DriftActionSO>(shared, overrides, $"Squirrel drift, {device}");
                AssertResolvesAndPresses<SquirrelTubeActionSO>(shared, overrides, $"Squirrel Boost Ring, {device}");
            }
        }
    }
}
