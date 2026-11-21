#if UNITY_EDITOR
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using CosmicShore.Gameplay;
using CosmicShore.Utility;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The warp field (Docs/WARP_FIELD.md): the radial field's law, and the runtime's no-field and
    /// ownership rules. The crystal wormhole's use of it is gated by CrystalWormholeTests. The runtime half is the safety net for
    /// every consumer — a field-free session must read EXACTLY 1 everywhere, or every vessel in
    /// the game would change size.
    /// </summary>
    public class WarpFieldTests
    {

        RadialWarp _field;
        readonly object _owner = new();

        [SetUp]
        public void SetUp()
        {
            _field = ScriptableObject.CreateInstance<RadialWarp>();
            var so = new SerializedObject(_field);
            so.FindProperty("referenceRadius").floatValue = 1000f;
            so.FindProperty("exponent").floatValue = 1f;
            so.FindProperty("minScale").floatValue = 0.01f;
            so.FindProperty("maxScale").floatValue = 1f;
            so.FindProperty("easeSeconds").floatValue = 0f;   // instant, so edit-mode time does not matter
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown()
        {
            WarpFieldRuntime.Release(_owner);
            _ = WarpFieldRuntime.IsActive;   // retire the instant ease-out
            Object.DestroyImmediate(_field);
        }

        [Test]
        public void Radial_IsProportionalNearTheCentre_SoftlyFlooredAndCapped_WithNoCrease()
        {
            Assert.AreEqual(0.1f, _field.ScaleAt(new Vector3(0f, 100f, 0f)), 0.002f, "not proportional to distance near the centre");
            Assert.AreEqual(0.01f, _field.ScaleAt(Vector3.zero), 1e-6f, "the centre is not the floor");
            Assert.AreEqual(1f, _field.ScaleAt(new Vector3(0f, 0f, 5000f)), 1e-3f, "not saturating toward maxScale far out");
            // Smooth: the slope never jumps (no crease at the floor or the cap — no interface).
            float prevSlope = float.NaN, worst = 0f;
            for (float r = 1f; r < 4000f; r += 1f)
            {
                float slope = _field.ScaleAt(new Vector3(r + 1f, 0f, 0f)) - _field.ScaleAt(new Vector3(r, 0f, 0f));
                if (!float.IsNaN(prevSlope)) worst = Mathf.Max(worst, Mathf.Abs(slope - prevSlope));
                prevSlope = slope;
            }
            // A hard clamp at the floor or cap jumps the slope by ~1/referenceRadius (1e-3 here); the soft
            // law only curves (worst ~1e-4 where the floor turns over).
            Assert.Less(worst, 3e-4f, "the radial field has a crease");
        }

        [Test]
        public void Runtime_WithNoField_IsExactlyOne()
        {
            Assert.IsFalse(WarpFieldRuntime.IsActive);
            Assert.AreEqual(1f, WarpFieldRuntime.ScaleAt(new Vector3(3f, 0f, 0f)));
        }

        [Test]
        public void Runtime_ReadsTheFieldAroundItsCentre_AndOnlyItsOwnerReleasesIt()
        {
            var centre = new GameObject("WarpCentre").transform;
            try
            {
                centre.position = new Vector3(500f, 0f, 0f);
                WarpFieldRuntime.Activate(_owner, _field, centre);
                Assert.IsTrue(WarpFieldRuntime.IsActive);
                Assert.AreEqual(_field.ScaleAt(new Vector3(100f, 0f, 0f)), WarpFieldRuntime.ScaleAt(new Vector3(600f, 0f, 0f)), 1e-5f,
                    "the field is not measured from its centre.");

                WarpFieldRuntime.Release(new object());
                Assert.IsTrue(WarpFieldRuntime.IsActive, "a stranger released the field.");

                WarpFieldRuntime.Release(_owner);
                Assert.IsFalse(WarpFieldRuntime.IsActive, "the owner's instant release left the field on.");
                Assert.AreEqual(1f, WarpFieldRuntime.ScaleAt(new Vector3(600f, 0f, 0f)));
            }
            finally
            {
                Object.DestroyImmediate(centre.gameObject);
            }
        }
    }
}
#endif
