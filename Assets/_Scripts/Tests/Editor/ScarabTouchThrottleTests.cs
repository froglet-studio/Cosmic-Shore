using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Scarab's throttle by device (SCARAB.md §3.11, Garrett 2026-10-21): on a phone the set-and-hold
    /// slider (<c>InputStatus.Throttle</c>) is the throttle; every other device keeps the right trigger's
    /// depth, and the pad's binary right-bumper feed into <c>Throttle</c> stays ignored there.
    /// </summary>
    public class ScarabTouchThrottleTests
    {
        GameObject _go;
        InputStatus _input;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("ScarabTouchThrottleTests");
            _input = _go.AddComponent<InputStatus>();
            _input.Throttle = 0.7f;
            _input.RightTriggerAnalog = 0.2f;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_go);

        [Test]
        public void Touch_ReadsTheSlider()
        {
            _input.ActiveInputDevice = InputDeviceType.Touch;
            Assert.AreEqual(0.7f, ScarabVesselTransformer.PilotThrottle01(_input), 1e-6f);
            Assert.IsTrue(ScarabVesselTransformer.ThrottleIsSetAndHold(_input));
        }

        [TestCase(InputDeviceType.Gamepad)]
        [TestCase(InputDeviceType.Keyboard)]
        [TestCase(InputDeviceType.MouseKeyboard)]
        [TestCase(InputDeviceType.DualMouse)]
        public void OtherDevices_ReadTheRightTrigger(InputDeviceType device)
        {
            _input.ActiveInputDevice = device;
            Assert.AreEqual(0.2f, ScarabVesselTransformer.PilotThrottle01(_input), 1e-6f);
            Assert.IsFalse(ScarabVesselTransformer.ThrottleIsSetAndHold(_input));
        }

        [Test]
        public void Throttle_IsClampedToZeroOne()
        {
            _input.ActiveInputDevice = InputDeviceType.Touch;
            _input.Throttle = 1.5f;
            Assert.AreEqual(1f, ScarabVesselTransformer.PilotThrottle01(_input), 1e-6f);
            _input.Throttle = -0.5f;
            Assert.AreEqual(0f, ScarabVesselTransformer.PilotThrottle01(_input), 1e-6f);
        }

        [Test]
        public void NoInput_IsNoThrottle()
        {
            Assert.AreEqual(0f, ScarabVesselTransformer.PilotThrottle01(null));
            Assert.IsFalse(ScarabVesselTransformer.ThrottleIsSetAndHold(null));
        }
    }
}
