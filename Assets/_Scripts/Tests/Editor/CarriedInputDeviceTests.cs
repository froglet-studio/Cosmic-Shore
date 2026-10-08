using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;
using ActionMap = System.Collections.Generic.Dictionary<CosmicShore.Data.InputEvents, System.Collections.Generic.List<CosmicShore.Gameplay.ShipActionSO>>;

namespace CosmicShore.Tests
{
    /// <summary>
    /// An ability press replicates by RE-EXECUTION — every peer resolves the pressed input to actions
    /// itself — and the resolution depends on the pilot's device (Touch reads the touch overrides,
    /// every desktop device the pad overrides). So the press and release RPCs carry the device the
    /// owner resolved with, as ONE byte (<c>R_VesselActionHandler.NoDevice</c> = "no pilot input"),
    /// and each peer resolves with that rather than with its own copy of the pilot's input status.
    ///
    /// <para>These tests pin the encoding: every <see cref="InputDeviceType"/> survives the byte and
    /// resolves exactly as the device itself does, and the sentinel collides with no device. A device
    /// added at 255 (or anything that stops fitting the byte) would otherwise read as "no device" on
    /// every peer, and that vessel's override-only abilities would silently stop replicating.</para>
    ///
    /// <para>What is NOT here: the two-machine behaviour itself (owner and peer run the same actions;
    /// a release stops what its press started). That needs two spawned copies of one vessel and is
    /// covered out of editor by <c>Tools/Build/peer_press_harness</c>, which compiles the shipped
    /// handler, routes its RPCs between an owner copy and a peer copy, and loads every shipped
    /// vessel's maps — and in the editor by the MPPM steps in
    /// <c>R_VesselActions/SQUIRREL_DRIFT.md</c> §11.</para>
    /// </summary>
    [TestFixture]
    public class CarriedInputDeviceTests
    {
        static IEnumerable<InputDeviceType> AllDevices => (InputDeviceType[])Enum.GetValues(typeof(InputDeviceType));

        // Distinct, non-empty maps so AreSame can tell which one a device resolved to.
        readonly ActionMap _touch = new() { [InputEvents.OnlyLeftStickAction] = new List<ShipActionSO>() };
        readonly ActionMap _pad = new() { [InputEvents.LeftStickAction] = new List<ShipActionSO>() };

        [Test]
        public void EveryDevice_FitsTheCarriedByte_AndIsNotTheNoDeviceSentinel()
        {
            foreach (var device in AllDevices)
            {
                int value = (int)device;
                Assert.That(value >= 0 && value < R_VesselActionHandler.NoDevice,
                    $"{device} = {value}: a press carries its device as a byte in 0..{R_VesselActionHandler.NoDevice - 1} " +
                    $"({R_VesselActionHandler.NoDevice} means no pilot input). Renumber the device or widen the RPC.");
            }
        }

        [Test]
        public void EveryDevice_ResolvesThroughTheCarriedByte_ExactlyAsItself()
        {
            foreach (var device in AllDevices)
            {
                var direct = R_VesselActionHandler.OverridesFor(device, _touch, _pad);
                var carried = R_VesselActionHandler.OverridesForCarried((byte)device, _touch, _pad);
                Assert.AreSame(direct, carried, $"{device}: the carried byte resolves to a different override map.");
            }
        }

        [Test]
        public void NoDevice_ResolvesAgainstNoOverrideMap()
        {
            // Before the vessel has pilot input a press runs the shared map alone; the carried form
            // of that state must say the same on every peer.
            Assert.IsNull(R_VesselActionHandler.OverridesForCarried(R_VesselActionHandler.NoDevice, _touch, _pad));
        }

        [Test]
        public void AByteThatNamesNoDevice_ResolvesAgainstNoOverrideMap()
        {
            // A press RPC is client input: a value naming no device must degrade to the shared map,
            // never throw and never land on a device's overrides.
            for (int b = 0; b < R_VesselActionHandler.NoDevice; b++)
            {
                if (Enum.IsDefined(typeof(InputDeviceType), b)) continue;
                Assert.IsNull(R_VesselActionHandler.OverridesForCarried((byte)b, _touch, _pad), $"byte {b}");
            }
        }
    }
}
