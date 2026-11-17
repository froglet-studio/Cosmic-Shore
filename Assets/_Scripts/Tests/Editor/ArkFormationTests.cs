using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The squad's places beside the Ark (Shombith, 2026-11-17: "two besides the ark and one on top of the ark"): seat 1
    /// to port, seat 2 to starboard, seat 3 over the back, each clear of the hull's own extent; and the launch from the
    /// hangar starts on the pad, leaves the doors straight ahead and ends in the slot, nose forward.
    /// </summary>
    public class ArkFormationTests
    {
        // The ArkReverse model's extent, roughly: wide and flat.
        static readonly Bounds Hull = new(new Vector3(0f, -2f, 3f), new Vector3(196f, 40f, 170f));

        [Test]
        public void Seats_AreOneEachSide_AndOneOnTop()
        {
            Assert.AreEqual(ArkFormation.Slot.Port, ArkFormation.SlotOf(0));
            Assert.AreEqual(ArkFormation.Slot.Starboard, ArkFormation.SlotOf(1));
            Assert.AreEqual(ArkFormation.Slot.Top, ArkFormation.SlotOf(2));
            Assert.AreEqual(ArkFormation.Slot.Port, ArkFormation.SlotOf(3), "A seat past the third wraps round.");
        }

        [Test]
        public void Slots_ClearTheHull_SideBySideWithIt()
        {
            var s = new ArkFormationSettings();
            var port = ArkFormation.SlotLocal(0, Hull, s);
            var starboard = ArkFormation.SlotLocal(1, Hull, s);
            var top = ArkFormation.SlotLocal(2, Hull, s);

            Assert.That(port.x, Is.EqualTo(Hull.min.x - s.AbeamMargin).Within(1e-3f), "Port is not clear of the hull's side.");
            Assert.That(starboard.x, Is.EqualTo(Hull.max.x + s.AbeamMargin).Within(1e-3f));
            Assert.That(top.y, Is.EqualTo(Hull.max.y + s.AboveMargin).Within(1e-3f), "Top is not clear of the hull's back.");
            Assert.That(port.y, Is.EqualTo(Hull.center.y).Within(1e-3f), "A side slot is not level with the hull.");
            Assert.That(top.x, Is.EqualTo(Hull.center.x).Within(1e-3f), "The top slot is not over the hull's middle.");
            // Side by side: all three level along the hull, beside its middle, never strung out behind it.
            Assert.That(port.z, Is.EqualTo(starboard.z).Within(1e-3f));
            Assert.That(port.z, Is.EqualTo(top.z).Within(1e-3f));
            Assert.IsTrue(port.z > Hull.min.z && port.z < Hull.max.z, "The slots are not alongside the hull.");
            Assert.IsFalse(Hull.Contains(port) || Hull.Contains(starboard) || Hull.Contains(top), "A slot is inside the hull.");
        }

        [Test]
        public void SlotPose_SailsWithTheArk_FacingItsHeading()
        {
            var go = new GameObject("Ark");
            try
            {
                go.transform.SetPositionAndRotation(new Vector3(100f, 20f, -50f), Quaternion.Euler(0f, 70f, 0f));
                var pose = ArkFormation.SlotPose(go.transform, 1, Hull, new ArkFormationSettings());
                Assert.Greater(Vector3.Dot(pose.position - go.transform.position, go.transform.right), Hull.max.x,
                    "Starboard is not off the Ark's right side.");
                Assert.Greater(Vector3.Dot(pose.rotation * Vector3.forward, go.transform.forward), 0.999f,
                    "A slot does not face the Ark's heading.");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Launch_LeavesThePad_OutTheDoors_IntoTheSlot()
        {
            var pad = new Vector3(0f, -18f, 64f);
            var exit = new Vector3(0f, -17.5f, 126f);
            var slot = new Vector3(-128f, -2f, 20f);
            const float share = 0.3f;

            Assert.That(Vector3.Distance(ArkFormation.LaunchPath(pad, exit, slot, share, 0f, out _), pad), Is.LessThan(1e-3f));
            Assert.That(Vector3.Distance(ArkFormation.LaunchPath(pad, exit, slot, share, share, out _), exit), Is.LessThan(1e-2f));
            Assert.That(Vector3.Distance(ArkFormation.LaunchPath(pad, exit, slot, share, 1f, out var end), slot), Is.LessThan(1e-3f));
            Assert.That(end.magnitude, Is.LessThan(1e-3f), "The launch does not settle into its slot.");

            // Straight out of the hangar: no turn before the doors.
            var half = ArkFormation.LaunchPath(pad, exit, slot, share, share * 0.5f, out var outbound);
            Assert.That(half.x, Is.EqualTo(0f).Within(1e-3f));
            Assert.Greater(outbound.z, 0f, "The vessel is not flying out of the doors.");
        }

        [Test]
        public void Launch_FacesForward_EvenFallingBackIntoItsSlot()
        {
            var facing = ArkFormation.LaunchFacing(new Vector3(-10f, 0f, -40f), 0f);
            Assert.Greater((facing * Vector3.forward).z, 0f, "A vessel falling back into its slot flies backwards.");
            Assert.Less((facing * Vector3.forward).x, 0f, "It does not lean the way it is moving.");
            Assert.That(Quaternion.Angle(ArkFormation.LaunchFacing(new Vector3(5f, 0f, -5f), 1f), Quaternion.identity),
                Is.LessThan(0.01f), "A settled vessel does not face the Ark's heading.");
        }
    }
}
