using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using ActionMap = System.Collections.Generic.Dictionary<CosmicShore.Data.InputEvents, System.Collections.Generic.List<CosmicShore.Gameplay.ShipActionSO>>;

namespace CosmicShore.Tests
{
    /// <summary>
    /// <see cref="AIPilot"/>'s COMMIT loop (line up a crystal, lock the course, swing the nose) must
    /// press a control the hull's ACTIVE device actually binds its drift to.
    ///
    /// <para>It pressed the constant <c>LeftStickAction</c>. The Squirrel binds its drift only in its
    /// device override maps — touch on <c>OnlyLeftStickAction</c>, pad on <c>LeftStickAction</c> — and
    /// an AI's device is the host's (Touch on a handheld), so on Touch the press gate refused every
    /// commit. The Squirrel's <c>drift</c> flag was also off, so its commit loop never ran at all;
    /// it is on now, and on a pad device it holds the left trigger so the drift has depth.</para>
    ///
    /// <para>The commit control is computed here exactly as <c>AIPilot.ResolveCommitControl</c>
    /// computes it — <see cref="AIPilot.CommitControlFrom"/> over the handler's own device-aware
    /// lookup — from the SHIPPED prefabs' serialized maps, so the tests describe what every hull
    /// does today, not a model of it. The last three tests pin the blast radius: the only thing
    /// that moved is the Squirrel.</para>
    /// </summary>
    [TestFixture]
    public class AIPilotCommitControlTests
    {
        const string VesselPrefabFolder = "Assets/_Prefabs/Spacevessels";
        const string Squirrel = "Squirrel";

        static IEnumerable<InputDeviceType> AllDevices =>
            Enum.GetValues(typeof(InputDeviceType)).Cast<InputDeviceType>();

        sealed class Hull
        {
            public string Name;
            public bool Commits;        // AIPilot.drift
            public bool HoldsTrigger;   // AIPilot.holdDriftTrigger
            public ActionMap Shared, Touch, Pad;
        }

        List<Hull> _hulls;

        [OneTimeSetUp]
        public void LoadShippedHulls() => _hulls = LoadHulls();

        /// <summary>Every vessel prefab that carries an AIPilot.</summary>
        static List<Hull> LoadHulls()
        {
            var hulls = new List<Hull>();
            foreach (var guid in AssetDatabase.FindAssets("t:GameObject", new[] { VesselPrefabFolder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;
                var pilot = prefab.GetComponentInChildren<AIPilot>(true);
                if (pilot == null) continue;
                var handler = prefab.GetComponentInChildren<R_VesselActionHandler>(true);
                Assert.IsNotNull(handler, $"{path} has an AIPilot but no R_VesselActionHandler.");

                var pilotSo = new SerializedObject(pilot);
                var handlerSo = new SerializedObject(handler);
                hulls.Add(new Hull
                {
                    Name = prefab.name,
                    Commits = Bool(pilotSo, "drift"),
                    HoldsTrigger = Bool(pilotSo, "holdDriftTrigger"),
                    Shared = DeviceAwareActionLookupTests.SerializedMap(handlerSo, "_inputEventShipActions"),
                    Touch = DeviceAwareActionLookupTests.SerializedMap(handlerSo, "_touchActionOverrides"),
                    Pad = DeviceAwareActionLookupTests.SerializedMap(handlerSo, "_gamepadActionOverrides"),
                });
            }
            Assert.IsNotEmpty(hulls, $"No vessel prefab with an AIPilot under {VesselPrefabFolder}.");
            return hulls;
        }

        static bool Bool(SerializedObject so, string field)
        {
            var p = so.FindProperty(field);
            Assert.IsNotNull(p, $"AIPilot.{field} is gone - update this test with the rename.");
            return p.boolValue;
        }

        Hull ShippedHull(string name)
        {
            var hull = _hulls.FirstOrDefault(h => h.Name == name);
            Assert.IsNotNull(hull, $"No {name} prefab with an AIPilot under {VesselPrefabFolder}.");
            return hull;
        }

        /// <summary>The control the commit loop presses (and releases) on this device.</summary>
        static InputEvents CommitControl(Hull hull, InputDeviceType device, out ActionMap overrides)
        {
            overrides = R_VesselActionHandler.OverridesFor(device, hull.Touch, hull.Pad);
            bool bound = R_VesselActionHandler.TryFindPressableAction<DriftActionSO>(hull.Shared, overrides, out _, out var input);
            return AIPilot.CommitControlFrom(bound, input);
        }

        static void AssertCommitPressRunsADrift(Hull hull, InputDeviceType device)
        {
            var control = CommitControl(hull, device, out var overrides);
            Assert.IsTrue(R_VesselActionHandler.TryGetPressedActions(hull.Shared, overrides, control, out var pressed),
                $"{hull.Name} on {device}: the commit loop presses {control}, which the press gate REFUSES - " +
                "the AI commits, nothing happens.");
            Assert.IsTrue(pressed.Any(a => a is DriftActionSO),
                $"{hull.Name} on {device}: pressing {control} runs {string.Join(", ", pressed.Select(a => a ? a.name : "null"))} " +
                "- no drift, so the course lock the commit loop depends on never happens.");
        }

        // ── The rule ────────────────────────────────────────────────────────────────────────────────

        [Test]
        public void NoDriftBound_FallsBackToTheLeftStickAction()
        {
            Assert.AreEqual(InputEvents.LeftStickAction, AIPilot.DefaultCommitControl,
                "The fallback is the control every non-drift hull has always been pressed on.");
            Assert.AreEqual(AIPilot.DefaultCommitControl, AIPilot.CommitControlFrom(false, InputEvents.OnlyLeftStickAction),
                "With no drift bound the lookup's out parameter is meaningless and must be ignored.");
            Assert.AreEqual(InputEvents.OnlyLeftStickAction, AIPilot.CommitControlFrom(true, InputEvents.OnlyLeftStickAction));
        }

        // ── The Squirrel: the defect ────────────────────────────────────────────────────────────────

        [Test]
        public void ShippedSquirrel_CommitDriftsOnTouchGamepadAndKeyboard()
        {
            var squirrel = ShippedHull(Squirrel);
            Assert.IsTrue(squirrel.Commits,
                "Squirrel.prefab's AIPilot has Drift off, so its commit loop never presses anything.");

            foreach (var device in AllDevices)
            {
                AssertCommitPressRunsADrift(squirrel, device);
                var expected = device == InputDeviceType.Touch
                    ? InputEvents.OnlyLeftStickAction
                    : InputEvents.LeftStickAction;
                Assert.AreEqual(expected, CommitControl(squirrel, device, out _),
                    $"Squirrel on {device}: wrong commit control.");
            }

            // On a pad device the drift's depth is the left trigger (VesselTransformer.GetTriggerSum),
            // and an AI has none of its own: without the hold a Gamepad commit drifts at depth 0.
            Assert.IsTrue(squirrel.HoldsTrigger,
                "Squirrel.prefab's AIPilot has Hold Drift Trigger off: on a Gamepad device its commit drift is inert.");
            Assert.AreEqual(1f, AIPilot.CommitDriftTriggerPull, "A held commit is a FULL trigger pull.");
        }

        [Test]
        public void EveryHullThatCommits_PressesADriftOnEveryDevice()
        {
            // The general contract: turning a hull's Drift flag on is only meaningful if its drift is
            // pressable on every device an AI can be given.
            foreach (var hull in _hulls.Where(h => h.Commits))
                foreach (var device in AllDevices)
                    AssertCommitPressRunsADrift(hull, device);
        }

        // ── The blast radius: nothing but the Squirrel moved ────────────────────────────────────────

        [Test]
        public void CommitControl_MovedOffTheLeftStickAction_OnlyForTheSquirrelOnTouch()
        {
            // The press, its release and the ability-cycler exclusion all use this control, so any
            // hull/device pair that still resolves to LeftStickAction behaves exactly as before. That
            // covers the hulls with a NON-drift ability on LeftStickAction (Butterfly's Fold, Serpent's
            // SniperScope, Sparrow's SkyBurstGun, Urchin's UrchinTrack, the Rhino's pad ShieldSwipeLeft):
            // they bind no drift, take the fallback, and get the press/release they always got.
            var moved = new List<string>();
            foreach (var hull in _hulls)
                foreach (var device in AllDevices)
                {
                    var control = CommitControl(hull, device, out _);
                    if (control != InputEvents.LeftStickAction) moved.Add($"{hull.Name}/{device} -> {control}");
                }

            CollectionAssert.AreEquivalent(new[] { $"{Squirrel}/Touch -> {InputEvents.OnlyLeftStickAction}" }, moved,
                "The AI commit control changed for a hull/device it was not meant to. If a hull's drift was " +
                "deliberately rebound, update this list - and check that hull's AI with Drift on and off.");
        }

        [Test]
        public void TheCommitLoop_RunsOnlyOnTheDolphinAndTheSquirrel()
        {
            var committing = _hulls.Where(h => h.Commits).Select(h => h.Name).ToList();
            CollectionAssert.AreEquivalent(new[] { "Dolphin", Squirrel }, committing,
                "AIPilot's Drift flag changed on a hull. That turns the commit loop on or off for its AI " +
                "in every mode but Skim Race - if deliberate, update this list.");
        }

        [Test]
        public void TheDriftTriggerHold_ReachesOnlyTheSquirrel()
        {
            // The Dolphin's pad-device commit has always drifted at depth 0 (its drift reads
            // LeftTrigger + RightTrigger on a pad and the AI writes neither). It is left as it was;
            // turning its hold on is a behaviour change to fly first, then record here.
            var holding = _hulls.Where(h => h.Commits && h.HoldsTrigger).Select(h => h.Name).ToList();
            CollectionAssert.AreEquivalent(new[] { Squirrel }, holding,
                "AIPilot now writes the left trigger during a commit on a different set of hulls.");
        }
    }
}
