using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The per-mode intro cameras (Docs/IntroCameras/README.md): the shot maths the Intro Cam Lab
    /// moves are ported onto, the no-repeat family pick, and the shipped library asset.
    /// </summary>
    public class IntroCameraTests
    {
        const string LibraryPath = "Assets/Resources/IntroCameraLibrary.asset";

        static IntroCameraScene Scene(float scale = 1f, IReadOnlyList<Vector3> crystals = null,
                                      IReadOnlyList<Pose> gates = null)
            => new(Vector3.zero, 240f * scale, scale, null, null, crystals, gates, null);

        [Test]
        public void EveryEaseStartsAtZeroAndEndsAtOne()
        {
            foreach (IntroEase e in System.Enum.GetValues(typeof(IntroEase)))
            {
                Assert.AreEqual(0f, IntroCameraMath.Ease(e, 0f), 1e-3f, e.ToString());
                Assert.AreEqual(1f, IntroCameraMath.Ease(e, 1f), 1e-3f, e.ToString());
            }
        }

        [Test]
        public void VertigoHoldsTheSubjectsSize()
        {
            // Visible width at the subject = 2 d tan(fov / 2) must not change as the dolly moves.
            float w0 = 2f * 34f * Mathf.Tan(74f * 0.5f * Mathf.Deg2Rad);
            foreach (float d in new[] { 40f, 70f, 125f })
            {
                float fov = IntroCameraMath.VertigoFov(34f, 74f, d);
                Assert.AreEqual(w0, 2f * d * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad), 0.01f, $"d = {d}");
            }
        }

        [Test]
        public void OrbitRadiusScalesWithTheCell()
        {
            var shot = IntroShot.Create(IntroShotKind.Orbit, 2f, IntroEase.Linear)
                .WithOrbit(default, 100f, 100f, 20f, 20f, 0f, 90f).WithLens(60f, 60f);
            var pose = IntroCameraMath.Evaluate(shot, 0.5f, 0f, Scene(scale: 4f));
            var flat = new Vector3(pose.Position.x, 0f, pose.Position.z);
            Assert.AreEqual(400f, flat.magnitude, 0.01f, "Arena-space radius is artifact units times the cell scale");
            Assert.AreEqual(80f, pose.Position.y, 0.01f);
            Assert.AreEqual(Vector3.zero, pose.LookAt);
        }

        [Test]
        public void MissingAnchorsFallBackToTheCentre()
        {
            var scene = Scene();
            Assert.AreEqual(Vector3.zero, scene.AnchorPosition(IntroAnchor.Rival, 0));
            Assert.AreEqual(Vector3.zero, scene.AnchorPosition(IntroAnchor.Crystal, 3));
            Assert.AreEqual(Vector3.zero, scene.AnchorPosition(IntroAnchor.Landmark, -1));
        }

        [Test]
        public void GatePathFallsBackToARingWhenThereIsNoCourse()
        {
            var path = Scene().PathFor(IntroPathSource.Gates, 150f, 0f);
            Assert.IsTrue(path.Closed);
            Assert.AreEqual(150f, path.PointAt(0.37f).magnitude, 1.5f);
        }

        [Test]
        public void GatePathRunsThroughTheGatesInOrder()
        {
            var gates = new List<Pose>();
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * Mathf.PI * 2f;
                gates.Add(new Pose(new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 100f, Quaternion.identity));
            }
            var path = Scene(gates: gates).PathFor(IntroPathSource.Gates, 150f, 0f);
            Assert.IsTrue(path.Closed, "a loop whose ends meet is flown as a loop");
            Assert.Less((path.PointAt(0f) - gates[0].position).magnitude, 0.01f);
            Assert.Less((path.PointAt(0.999f) - path.PointAt(0.001f)).magnitude, 2f, "no seam where the loop closes");
        }

        [Test]
        public void CutsVisitEachAnchorInTurn()
        {
            var crystals = new[] { new Vector3(10, 0, 0), new Vector3(50, 0, 0), new Vector3(90, 0, 0) };
            var scene = Scene(crystals: crystals);
            var shot = JsonUtility.FromJson<IntroShot>(
                "{\"kind\":4,\"duration\":3,\"cutAnchor\":5,\"cutCount\":3,\"cutDistance\":30,\"cutPush\":6,\"fov0\":50,\"fov1\":50}");
            for (int i = 0; i < 3; i++)
                Assert.AreEqual(crystals[i], IntroCameraMath.Evaluate(shot, (i + 0.5f) / 3f, 0f, scene).LookAt);
        }

        [Test]
        public void PickNeverRepeatsTheLastIntro()
        {
            for (int previous = 0; previous < 4; previous++)
            {
                var hit = new HashSet<int>();
                for (int k = 0; k <= 100; k++)
                {
                    int i = IntroCameraLibrarySO.PickIndex(4, previous, k / 100f);
                    Assert.AreNotEqual(previous, i);
                    Assert.That(i, Is.InRange(0, 3));
                    hit.Add(i);
                }
                Assert.AreEqual(3, hit.Count, "every other intro can come up");
            }
            Assert.AreEqual(0, IntroCameraLibrarySO.PickIndex(1, 0, 0.7f), "a family of one plays its only intro");
        }

        [Test]
        public void ShippedLibraryCoversEveryFamilyModeWithWordsAndIntros()
        {
            var lib = AssetDatabase.LoadAssetAtPath<IntroCameraLibrarySO>(LibraryPath);
            Assert.IsNotNull(lib, $"{LibraryPath} is missing; run Tools/Build/author_intro_cameras.py");
            Assert.AreSame(lib, Resources.Load<IntroCameraLibrarySO>(IntroCameraLibrarySO.ResourcePath));

            var modes = lib.Families.SelectMany(f => f.Modes).ToList();
            Assert.AreEqual(35, modes.Count);
            Assert.AreEqual(modes.Count, modes.Distinct().Count(), "a mode is in two families");
            foreach (var family in lib.Families)
            {
                Assert.IsNotEmpty(family.Sequences, family.Name);
                foreach (var s in family.Sequences)
                {
                    Assert.IsNotEmpty(s.Shots, s.Name);
                    Assert.Contains(s.DesignedFor, family.Modes.ToList(), s.Name);
                    Assert.That(s.Duration, Is.InRange(5f, 12f), s.Name);
                }
                foreach (var mode in family.Modes)
                {
                    var words = lib.WordsFor(mode);
                    Assert.IsNotNull(words, mode.ToString());
                    Assert.IsNotEmpty(words.Title, mode.ToString());
                    Assert.IsTrue(lib.TryPick(mode, out var picked), mode.ToString());
                    Assert.Contains(picked, family.Sequences.ToList(), mode.ToString());
                }
            }
            Assert.IsNull(lib.FamilyFor(GameModes.Tandava), "a mode outside every family keeps the orbit");
        }
    }
}
