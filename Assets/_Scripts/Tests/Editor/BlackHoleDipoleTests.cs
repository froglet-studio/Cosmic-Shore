#if UNITY_EDITOR
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using CosmicShore.Gameplay;
using CosmicShore.Utility;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The black/white hole DIPOLE (Docs/BLACK_HOLE.md §12): a source is the sink with its GM negated —
    /// it pushes where the sink pulls, never captures, and drags its frame the other way — and the
    /// Black Hole cell lays the pair with a wormhole between them and a warp pole at each. Physics
    /// from the shipped pure functions; wiring from the shipped assets.
    /// </summary>
    public class BlackHoleDipoleTests
    {
        const string PrefabPath = "Assets/_Prefabs/Spawnables/SpawnableBlackHole.prefab";
        const string CellConfigPath = "Assets/_SO_Assets/Cell Configs/Black Hole Cell/Black Hole Cell Config.asset";

        static BlackHolePhysics.Well Well(float gm, float frameDrag = 0f) => new()
        {
            Position = float3.zero,
            GM = gm,
            Horizon = BlackHolePhysics.Horizon.Of(2f),
            InfluenceRadius = 60f,
            SpinAxis = new float3(0f, 1f, 0f),
            FrameDrag = frameDrag,
        };

        [Test]
        public void Source_PushesExactlyWhereTheSinkPulls()
        {
            var p = new float3(7f, 3f, -2f);
            var pull = BlackHolePhysics.Acceleration(p, Well(2000f));
            var push = BlackHolePhysics.Acceleration(p, Well(-2000f));
            Assert.Less(math.dot(pull, p), 0f, "the sink does not pull inward.");
            Assert.Greater(math.dot(push, p), 0f, "the source does not push outward.");
            Assert.AreEqual(0f, math.length(pull + push), 1e-4f, "the source is not the sink's acceleration negated.");
        }

        [Test]
        public void Source_NeverCaptures_AndDrivesOutWhatIsInsideIt()
        {
            var wells = new BlackHolePhysics.NativeWells();
            wells.Add(Well(-2000f));
            var prm = new BlackHolePhysics.StepParams { ReleaseDamping = 1.5f, ReleaseSpeed = 0.75f, MaxSubsteps = 8 };

            // A body inside the source's horizon, falling inward (one carried through the throat).
            var p = new float3(1f, 0f, 0f);
            var v = new float3(-3f, 0f, 0f);
            for (int i = 0; i < 120; i++)
            {
                var verdict = BlackHolePhysics.Step(ref p, ref v, in wells, in prm, 1f / 60f, out int by);
                Assert.AreNotEqual(BlackHolePhysics.Verdict.Captured, verdict, "a white hole captured a body.");
                Assert.AreEqual(-1, by);
            }
            Assert.Greater(math.length(p), 2f, "a body inside the source's horizon was not driven out of it.");
        }

        [Test]
        public void Source_DragsItsFrameTheOtherWay()
        {
            var p = new float3(5f, 0f, 0f);
            var sinkFrame = BlackHolePhysics.FrameVelocity(p, Well(2000f, 50f));
            var sourceFrame = BlackHolePhysics.FrameVelocity(p, Well(-2000f, -50f));
            Assert.Greater(math.length(sinkFrame), 0f, "the sink's frame does not turn.");
            Assert.AreEqual(0f, math.length(sinkFrame + sourceFrame), 1e-5f, "the source's frame is not the sink's reversed.");
        }

        [Test]
        public void BlackHoleCell_LaysADipoleWithAWormholeAndClearsTheToyRing()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, $"{PrefabPath} is missing.");
            Assert.IsTrue(prefab.TryGetComponent<SpawnableBlackHole>(out var hole), "the prefab has no SpawnableBlackHole.");
            Assert.IsTrue(hole.IsDipole, "the Black Hole cell's hole is not a dipole.");

            var so = new SerializedObject(hole);
            Assert.IsTrue(so.FindProperty("seatWormhole").boolValue, "the dipole seats no wormhole.");
            Assert.IsNotNull(so.FindProperty("wormholeMaterial").objectReferenceValue,
                "the wormhole has no material — the mouths would draw nothing and the sink keeps its black shadow.");
            Assert.IsNotNull(so.FindProperty("gameData").objectReferenceValue,
                "the wormhole has no GameData — its mouths would carry no one.");

            // Both poles' shrinking reach stays clear of the toys (a horizontal ring) and the pole
            // switches (straight up and down), all at ~0.82 of a ~1200 membrane: ~984 out.
            var config = AssetDatabase.LoadAssetAtPath<CellConfigDataSO>(CellConfigPath);
            Assert.IsInstanceOf<RadialWarp>(config.WarpField);
            float reach = ((RadialWarp)config.WarpField).ReferenceRadius;
            const float ToyRadius = 984f;
            var source = hole.SourceOffset;
            float nearestToy = Mathf.Min(
                Mathf.Sqrt(source.y * source.y + Mathf.Pow(ToyRadius - new Vector2(source.x, source.z).magnitude, 2f)),
                Mathf.Min((source - Vector3.up * ToyRadius).magnitude, (source + Vector3.up * ToyRadius).magnitude));
            Assert.Greater(nearestToy, reach, "the source's warp reach covers a toy or a pole switch.");
            Assert.Greater(ToyRadius, reach, "the sink's warp reach covers the toy ring.");
            Assert.Greater(source.magnitude, 2f * BlackHoleRegistry.Config.InfluenceRadius(hole.Strength),
                "the two holes' influence spheres overlap — the source would push mass back into the sink.");
        }

        [Test]
        public void Shaders_CarryThePolarityAndTheThroat()
        {
            string shader = System.IO.File.ReadAllText("Assets/_Graphics/Materials/Graphs/BlackHoleLens.shader");
            Assert.IsTrue(shader.Contains("BlackHoleLensTraceSigned("),
                "the lens traces every hole as a black hole again — a white hole would focus light and cast a shadow.");
            Assert.IsTrue(shader.Contains("_BHThroat"),
                "the lens no longer treats a seated mouth as solid — the mouth would be smeared into the Einstein ring.");
            string warp = System.IO.File.ReadAllText("Assets/_Graphics/Materials/Graphs/PrismGravityWarp.hlsl");
            Assert.IsTrue(warp.Contains("abs(k)"),
                "the tidal warp skips negative slots again — a white hole would not flatten what it pushes.");
        }

        [Test]
        public void WarpField_ReadsEveryPole_AndTheNearestWins()
        {
            var field = ScriptableObject.CreateInstance<RadialWarp>();
            var so = new SerializedObject(field);
            so.FindProperty("referenceRadius").floatValue = 100f;
            so.FindProperty("minScale").floatValue = 0.01f;
            so.FindProperty("easeSeconds").floatValue = 0f;
            so.ApplyModifiedPropertiesWithoutUndo();
            var centre = new GameObject("Sink").transform;
            var pole = new GameObject("Source").transform;
            pole.position = new Vector3(0f, 500f, 0f);
            var owner = new object();
            try
            {
                WarpFieldRuntime.Activate(owner, field, centre);
                WarpFieldRuntime.AddPole(pole);
                Assert.AreEqual(0.1f, WarpFieldRuntime.ScaleAt(new Vector3(0f, 10f, 0f)), 1e-4f, "the sink's pole is not read.");
                Assert.AreEqual(0.2f, WarpFieldRuntime.ScaleAt(new Vector3(0f, 480f, 0f)), 1e-4f, "the source's pole is not read.");
                Assert.AreEqual(1f, WarpFieldRuntime.ScaleAt(new Vector3(0f, 250f, 0f)), 1e-4f, "between the poles is not unwarped.");

                // A pole whose transform is gone keeps shaping the field until the field ends.
                Object.DestroyImmediate(pole.gameObject);
                Assert.AreEqual(0.2f, WarpFieldRuntime.ScaleAt(new Vector3(0f, 480f, 0f)), 1e-4f,
                    "a destroyed pole stopped shaping the field — every vessel near it would pop to full size.");
            }
            finally
            {
                WarpFieldRuntime.Release(owner);
                _ = WarpFieldRuntime.IsActive;
                if (pole) Object.DestroyImmediate(pole.gameObject);
                Object.DestroyImmediate(centre.gameObject);
                Object.DestroyImmediate(field);
            }
        }
    }
}
#endif
