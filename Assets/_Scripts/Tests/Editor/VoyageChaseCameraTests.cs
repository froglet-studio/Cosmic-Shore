using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Voyages' chase camera (Rev 114 F1): a mode-owned framing on the existing follow camera, read per vessel class
    /// from one settings asset, and the attackers' red light reaching the Ark.
    /// </summary>
    public class VoyageChaseCameraTests
    {
        const string CameraPath = "Assets/Resources/Voyages/VoyageCamera.asset";

        [Test]
        public void ModeFraming_None_LeavesTheOffsetAlone()
        {
            var offset = new Vector3(0f, 6f, -20f);
            Assert.AreEqual(offset, CustomCameraController.ModeFraming.None.Apply(offset));
            Assert.IsTrue(CustomCameraController.ModeFraming.None.IsNone);
        }

        [Test]
        public void ModeFraming_CloserLowerAndToOneSide()
        {
            var framing = new CustomCameraController.ModeFraming { Distance = 0.5f, Height = 0.5f, Side = 0.1f };
            var applied = framing.Apply(new Vector3(0f, 6f, -20f));
            Assert.AreEqual(1f, applied.x, 1e-4f, "side is a share of the follow distance, then scaled with it");
            Assert.AreEqual(1.5f, applied.y, 1e-4f);
            Assert.AreEqual(-10f, applied.z, 1e-4f);
            Assert.IsFalse(framing.IsNone);
        }

        [Test]
        public void VoyageCamera_UsesTheVesselsRow_ElseTheDefault()
        {
            var so = ScriptableObject.CreateInstance<VoyageCameraSO>();
            var fallback = new CustomCameraController.ModeFraming { Distance = 0.8f, Height = 0.6f, Side = 0.1f };
            var rhino = new CustomCameraController.ModeFraming { Distance = 1.2f, Height = 0.7f, Side = 0f };
            so.Set(fallback, new[] { new VoyageCameraSO.VesselRow { Vessel = VesselClassType.Rhino, Framing = rhino } });
            Assert.AreEqual(rhino, so.For(VesselClassType.Rhino));
            Assert.AreEqual(fallback, so.For(VesselClassType.Squirrel));
            Object.DestroyImmediate(so);
        }

        [Test]
        public void ShippedVoyageCamera_IsCloseAndLow()
        {
            var so = AssetDatabase.LoadAssetAtPath<VoyageCameraSO>(CameraPath);
            Assert.IsNotNull(so, $"No voyage camera at {CameraPath}: voyages would fly the vessel's own framing.");
            var d = so.DefaultFraming;
            Assert.Less(d.Distance, 1f, "close behind");
            Assert.Greater(d.Distance, 0.3f);
            Assert.Less(d.Height, 1f, "low");
            Assert.Greater(d.Height, 0f);
            Assert.That(Mathf.Abs(d.Side), Is.LessThan(0.5f), "a little to one side, not a side view");
        }

        [Test]
        public void AttackerLight_ReachesTheArk_WithinItsCap()
        {
            var ark = Vector3.zero;
            Assert.GreaterOrEqual(ArkAttackerMarks.LightRange(new Vector3(40f, 0f, 0f), ark, 4f), 40f);
            Assert.AreEqual(140f, ArkAttackerMarks.LightRange(new Vector3(400f, 0f, 0f), ark, 4f), 1e-4f);
            Assert.AreEqual(8f, ArkAttackerMarks.LightRange(ark, ark, 1f), 1e-4f);
        }
    }
}
