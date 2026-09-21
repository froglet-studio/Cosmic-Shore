#if UNITY_EDITOR
using System;
using System.IO;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The screenshot director's camera placement is a pure function of (concept, subject pose,
    /// velocity, random stream), which is the whole reason it lives apart from the MonoBehaviour
    /// that renders: these run thousands of rolls of every SHIPPED concept in edit mode, with no
    /// camera, no scene and no play mode.
    ///
    /// The properties worth locking are the ones a bad shot shares with a broken one — a camera
    /// inside the hull, a camera aimed away from the subject, a NaN pose from a vessel flying
    /// straight up — because every one of those produces a file, and a folder of black rectangles
    /// looks exactly like a folder of shots nobody has looked at yet.
    /// </summary>
    public class ScreenshotFramingTests
    {
        static ScreenshotConcept Concept(
            float azimuth, float elevation, float distance, bool worldAligned = false) =>
            new ScreenshotConcept
            {
                name = "Test",
                azimuthDegrees = new Vector2(azimuth, azimuth),
                elevationDegrees = new Vector2(elevation, elevation),
                distance = new Vector2(distance, distance),
                fieldOfView = new Vector2(60f, 60f),
                rollDegrees = Vector2.zero,
                aimLeadSeconds = Vector2.zero,
                framingPitchDegrees = Vector2.zero,
            };

        [Test]
        public void Solve_PlacesCameraAtTheRequestedDistance()
        {
            var shot = ScreenshotFraming.Solve(
                Concept(180f, 0f, 25f), Vector3.zero, Vector3.forward, Vector3.forward, 50f,
                new System.Random(1));

            Assert.AreEqual(25f, Vector3.Distance(shot.Position, Vector3.zero), 0.001f);
        }

        [Test]
        public void Solve_AzimuthZeroIsAheadOfTheSubject_AndOneEightyIsBehind()
        {
            var ahead = ScreenshotFraming.Solve(
                Concept(0f, 0f, 20f), Vector3.zero, Vector3.forward, Vector3.forward, 0f, new System.Random(2));
            var behind = ScreenshotFraming.Solve(
                Concept(180f, 0f, 20f), Vector3.zero, Vector3.forward, Vector3.forward, 0f, new System.Random(2));

            Assert.Greater(ahead.Position.z, 0f, "azimuth 0 must put the camera ahead of the subject");
            Assert.Less(behind.Position.z, 0f, "azimuth 180 must put the camera behind the subject");
        }

        [Test]
        public void Solve_PositiveElevationPutsTheCameraAbove()
        {
            var shot = ScreenshotFraming.Solve(
                Concept(180f, 30f, 20f), Vector3.zero, Vector3.forward, Vector3.forward, 0f, new System.Random(3));

            Assert.Greater(shot.Position.y, 0f);
        }

        [Test]
        public void Solve_WorldAlignedIgnoresTheSubjectsHeading()
        {
            var concept = Concept(90f, 10f, 30f, worldAligned: true);

            var flyingNorth = ScreenshotFraming.Solve(
                concept, Vector3.zero, Vector3.forward, Vector3.forward, 40f, new System.Random(4));
            var flyingEast = ScreenshotFraming.Solve(
                concept, Vector3.zero, Vector3.right, Vector3.right, 40f, new System.Random(4));

            // Same seed, same world-aligned concept: the camera stands in the same place regardless
            // of which way the ship is pointing. That is what makes it read as planted in the arena.
            Assert.AreEqual(flyingNorth.Position.x, flyingEast.Position.x, 0.001f);
            Assert.AreEqual(flyingNorth.Position.z, flyingEast.Position.z, 0.001f);
        }

        [Test]
        public void Solve_VesselAlignedFollowsTheSubjectsHeading()
        {
            var concept = Concept(180f, 0f, 20f);

            var flyingNorth = ScreenshotFraming.Solve(
                concept, Vector3.zero, Vector3.forward, Vector3.forward, 0f, new System.Random(5));
            var flyingEast = ScreenshotFraming.Solve(
                concept, Vector3.zero, Vector3.right, Vector3.right, 0f, new System.Random(5));

            Assert.Less(flyingNorth.Position.z, -1f, "behind a north-bound ship is -Z");
            Assert.Less(flyingEast.Position.x, -1f, "behind an east-bound ship is -X");
        }

        [Test]
        public void Solve_HonoursTheHullRadiusFloor_SoTheCameraIsNeverInsideTheShip()
        {
            // A concept asking for 2u on a vessel whose hull measures 12u across.
            var shot = ScreenshotFraming.Solve(
                Concept(180f, 0f, 2f), Vector3.zero, Vector3.forward, Vector3.forward, 0f,
                new System.Random(6), minimumDistance: 12f);

            Assert.GreaterOrEqual(Vector3.Distance(shot.Position, Vector3.zero), 12f);
        }

        [Test]
        public void Solve_AimsAtTheSubject_AcrossEveryShippedConcept()
        {
            var config = ScriptableObject.CreateInstance<ScreenshotDirectorConfigSO>();
            config.ApplyDefaults();
            var rng = new System.Random(7);

            foreach (var concept in config.concepts)
            {
                for (int i = 0; i < 250; i++)
                {
                    var subject = new Vector3(
                        (float)rng.NextDouble() * 400f - 200f,
                        (float)rng.NextDouble() * 400f - 200f,
                        (float)rng.NextDouble() * 400f - 200f);
                    var course = RandomDirection(rng);
                    float speed = (float)rng.NextDouble() * 300f;

                    var shot = ScreenshotFraming.Solve(concept, subject, course, course, speed, rng);

                    Vector3 toSubject = (subject - shot.Position).normalized;
                    float alignment = Vector3.Dot(shot.Rotation * Vector3.forward, toSubject);

                    // The aim lead deliberately pushes the subject off centre, so this is "the
                    // subject is in front of the camera", not "dead centre". A shot that fails this
                    // is pointed at empty space.
                    Assert.Greater(alignment, 0.5f,
                        $"'{concept.name}' aimed away from its subject (alignment {alignment:F2})");
                }
            }

            ScriptableObject.DestroyImmediate(config);
        }

        [Test]
        public void Solve_SurvivesDegenerateFlightStates()
        {
            var concept = Concept(120f, 15f, 30f);
            var rng = new System.Random(8);

            var cases = new (string label, Vector3 forward, Vector3 course, float speed)[]
            {
                ("stationary, no course", Vector3.forward, Vector3.zero, 0f),
                ("climbing straight up", Vector3.up, Vector3.up, 120f),
                ("diving straight down", Vector3.down, Vector3.down, 120f),
                ("zero forward AND zero course", Vector3.zero, Vector3.zero, 0f),
            };

            foreach (var (label, forward, course, speed) in cases)
            {
                var shot = ScreenshotFraming.Solve(concept, Vector3.zero, forward, course, speed, rng);

                Assert.IsFalse(float.IsNaN(shot.Position.x) || float.IsNaN(shot.Position.y) || float.IsNaN(shot.Position.z),
                    $"{label}: NaN position");
                Assert.IsFalse(float.IsNaN(shot.Rotation.x) || float.IsNaN(shot.Rotation.y) ||
                               float.IsNaN(shot.Rotation.z) || float.IsNaN(shot.Rotation.w),
                    $"{label}: NaN rotation");
                Assert.AreEqual(30f, Vector3.Distance(shot.Position, Vector3.zero), 0.01f, $"{label}: wrong distance");
            }
        }

        /// <summary>
        /// The defect this suite was written and immediately earned its keep on: lead room is
        /// expressed in SECONDS of the subject's travel, which is a world distance, so on a fast
        /// vessel it can exceed the camera's own distance many times over and aim the shot at empty
        /// space. A 300 u/s vessel with a 0.35s lead is 105u ahead of an 8u chase camera.
        /// </summary>
        [Test]
        public void Solve_ClampsLeadRoom_SoAFastSubjectCannotLeaveTheFrame()
        {
            var concept = Concept(180f, 0f, 8f);
            concept.aimLeadSeconds = new Vector2(0.35f, 0.35f);

            var shot = ScreenshotFraming.Solve(
                concept, Vector3.zero, Vector3.forward, Vector3.forward, 300f, new System.Random(10));

            Vector3 toSubject = (Vector3.zero - shot.Position).normalized;
            float alignment = Vector3.Dot(shot.Rotation * Vector3.forward, toSubject);

            // Unclamped this is ~0.075 — the ship is 85 degrees off axis, i.e. not in the picture.
            Assert.Greater(alignment, 0.9f,
                "lead room must be a fraction of the shot, not of raw speed");
        }

        static Vector3 RandomDirection(System.Random rng)
        {
            // Deterministic unit vector, so a failure here reproduces exactly. Marsaglia's method.
            double z = rng.NextDouble() * 2d - 1d;
            double theta = rng.NextDouble() * Math.PI * 2d;
            double r = Math.Sqrt(Math.Max(0d, 1d - z * z));
            return new Vector3((float)(r * Math.Cos(theta)), (float)(r * Math.Sin(theta)), (float)z);
        }

        [Test]
        public void Sample_IsOrderInsensitive()
        {
            var rng = new System.Random(9);
            for (int i = 0; i < 100; i++)
            {
                float value = ScreenshotFraming.Sample(new Vector2(210f, 150f), rng);
                Assert.GreaterOrEqual(value, 150f);
                Assert.LessOrEqual(value, 210f);
            }
        }
    }

    public class ScreenshotDirectorConfigTests
    {
        ScreenshotDirectorConfigSO _config;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<ScreenshotDirectorConfigSO>();
            _config.ApplyDefaults();
        }

        [TearDown]
        public void TearDown() => ScriptableObject.DestroyImmediate(_config);

        [Test]
        public void Defaults_ShipAUsableLibrary()
        {
            Assert.IsNotEmpty(_config.concepts);
            foreach (var concept in _config.concepts)
                Assert.IsTrue(concept.IsUsable, $"'{concept.name}' ships unusable");
        }

        /// <summary>
        /// The library's one band that is NOT free to move: every solo concept sits inside the
        /// vessel vision band's near edge, where a hull still renders as itself, except the ONE
        /// that crosses it on purpose (Docs/VESSEL_VISION.md).
        ///
        /// <para>This is the check that caught the 1.5x zoom-out pass: scaling Static Tracking
        /// Cam's ceiling arithmetically took it to 165, past the 150 edge, which would have handed
        /// a shot of the SHIP a flat domain-coloured silhouette with nothing saying why. The edge
        /// is read from the SHIPPED asset rather than from the C# field initializer, because a
        /// number read off an initializer is not the number the game runs on — the vision band's
        /// own docs record that exact trap.</para>
        /// </summary>
        [Test]
        public void Defaults_KeepEverySoloConceptInsideTheVisionBand_ExceptTheOneThatSaysOtherwise()
        {
            var vision = Resources.Load<VesselVisionShadingConfigSO>("VesselVisionShadingConfig");
            if (vision == null || !vision.Enabled)
                Assert.Ignore("No shipped vision-band asset to measure against.");

            float nearEdge = vision.NearFadeStart;
            var crossers = new System.Collections.Generic.List<string>();

            foreach (var concept in _config.concepts)
            {
                if (concept.framing != ScreenshotFramingKind.Solo) continue;   // a pair's band is a FLOOR
                if (Mathf.Max(concept.distance.x, concept.distance.y) > nearEdge)
                    crossers.Add(concept.name);
            }

            Assert.That(crossers.Count, Is.EqualTo(1),
                $"exactly one solo concept may photograph a banded hull; these do: " +
                string.Join(", ", crossers));
            Assert.That(crossers[0].ToLowerInvariant(), Does.Contain("banded"),
                $"'{crossers[0]}' crosses the vision band at {nearEdge}u without saying so in its " +
                "name — either cap it or name it, so a reader of the library can tell the " +
                "deliberate silhouette shot from an accident.");
        }

        [Test]
        public void PickConcept_NeverDrawsAZeroWeightConcept()
        {
            foreach (var concept in _config.concepts) concept.weight = 0f;
            _config.concepts[2].weight = 1f;

            var rng = new System.Random(11);
            for (int i = 0; i < 500; i++)
                Assert.AreSame(_config.concepts[2], _config.PickConcept(rng));
        }

        [Test]
        public void PickConcept_ReturnsNullWhenNothingIsUsable()
        {
            foreach (var concept in _config.concepts) concept.weight = 0f;
            Assert.IsNull(_config.PickConcept(new System.Random(12)));
        }

        [Test]
        public void PickConcept_ReachesEveryWeightedConcept()
        {
            var seen = new System.Collections.Generic.HashSet<string>();
            var rng = new System.Random(13);
            for (int i = 0; i < 5000; i++) seen.Add(_config.PickConcept(rng).name);

            Assert.AreEqual(_config.concepts.Count, seen.Count, "some concept is unreachable");
        }

        [Test]
        public void BuildFileName_CarriesTheConceptAndIsPathSafe()
        {
            string name = _config.BuildFileName("Sidecar (port)", new DateTime(2026, 9, 21, 14, 5, 9));

            Assert.IsTrue(name.Contains("Sidecar"), "the concept name is how a folder is triaged");
            Assert.IsTrue(name.EndsWith(".png"));
            Assert.AreEqual(-1, name.IndexOfAny(Path.GetInvalidFileNameChars()));
        }

        [Test]
        public void ResolveOutputFolder_UsesAnAbsolutePathAsGiven_AndAlwaysReturnsOne()
        {
            Assert.IsNotEmpty(_config.ResolveOutputFolder());

            string absolute = Path.Combine(Path.GetTempPath(), "cosmic-shore-shots");
            _config.outputFolder = absolute;
            Assert.AreEqual(absolute, _config.ResolveOutputFolder());
        }

        [Test]
        public void ResolveOutputFolder_DefaultsToTheRepositorysOwnGitIgnoredRecordingsFolder()
        {
            // The default has to be resolved per machine, because the whole point is that one
            // number is right in every clone. Edit-mode tests run in the Editor, where
            // Application.dataPath is "<repo>/Assets" - so the parent IS the repository root.
            string resolved = _config.ResolveOutputFolder();

            Assert.IsTrue(Path.IsPathRooted(resolved));
            Assert.AreEqual(ScreenshotDirectorConfigSO.DefaultFolderName,
                new DirectoryInfo(resolved).Name,
                "captures land in the folder .gitignore already excludes, so they are never pushed");

            string repoRoot = Directory.GetParent(Application.dataPath)?.FullName;
            Assert.AreEqual(repoRoot, new DirectoryInfo(resolved).Parent?.FullName);
        }

        [Test]
        public void ResolveOutputFolder_HangsARelativePathOffTheDefaultRoot()
        {
            string root = _config.ResolveOutputFolder();
            _config.outputFolder = "Runs/Tuesday";

            string resolved = _config.ResolveOutputFolder();
            Assert.IsTrue(resolved.StartsWith(root), $"'{resolved}' should sit under '{root}'");
            Assert.IsTrue(Path.IsPathRooted(resolved));
        }
    }

    /// <summary>
    /// The two-shot's whole promise is one geometric fact — the camera sits on the perpendicular
    /// bisector plane of the two subjects — and these assert the three properties that fall out of
    /// it, over randomized pairs rather than one hand-picked arrangement.
    /// </summary>
    public class ScreenshotPairFramingTests
    {
        static ScreenshotConcept PairConcept() => new ScreenshotConcept
        {
            name = "Duo", framing = ScreenshotFramingKind.Pair,
            azimuthDegrees = new Vector2(0f, 360f),
            elevationDegrees = new Vector2(-80f, 80f),   // must be IGNORED
            distance = new Vector2(20f, 90f),
            fieldOfView = new Vector2(30f, 75f),
            rollDegrees = new Vector2(-10f, 10f),
            aimLeadSeconds = new Vector2(0f, 2f),        // must be IGNORED
            framingPitchDegrees = new Vector2(-5f, 5f),
        };

        [Test]
        public void SolvePair_PutsTheCameraExactlyEquidistantFromBothSubjects()
        {
            var rng = new System.Random(20260921);
            var concept = PairConcept();

            for (int i = 0; i < 400; i++)
            {
                Vector3 a = RandomPoint(rng, 400f);
                Vector3 b = a + RandomDirection(rng) * Mathf.Lerp(10f, 30f, (float)rng.NextDouble());
                Vector3 flow = RandomDirection(rng);

                var shot = ScreenshotFraming.SolvePair(concept, a, b, flow, 4f, rng, aspect: 16f / 9f);

                float da = Vector3.Distance(shot.Position, a);
                float db = Vector3.Distance(shot.Position, b);

                // Relative, because the absolute distances run to a couple of hundred units.
                Assert.That(Mathf.Abs(da - db) / Mathf.Max(da, db), Is.LessThan(1e-4f),
                    $"equidistance is the promise: {da} vs {db}");
            }
        }

        [Test]
        public void SolvePair_LaysBothSubjectsSymmetricallyAboutTheFrameCentre()
        {
            var rng = new System.Random(77);
            var concept = PairConcept();
            concept.rollDegrees = Vector2.zero;
            concept.framingPitchDegrees = Vector2.zero;

            for (int i = 0; i < 400; i++)
            {
                Vector3 a = RandomPoint(rng, 250f);
                Vector3 b = a + RandomDirection(rng) * Mathf.Lerp(10f, 30f, (float)rng.NextDouble());

                var shot = ScreenshotFraming.SolvePair(concept, a, b, RandomDirection(rng), 4f, rng, aspect: 16f / 9f);

                // Into camera space: mirrored x, equal y and z is exactly "framed the same way".
                Quaternion inverse = Quaternion.Inverse(shot.Rotation);
                Vector3 la = inverse * (a - shot.Position);
                Vector3 lb = inverse * (b - shot.Position);

                Assert.That(la.z, Is.EqualTo(lb.z).Within(1e-3f), "equal depth");
                Assert.That(la.y, Is.EqualTo(lb.y).Within(1e-3f), "equal height in frame");
                Assert.That(la.x, Is.EqualTo(-lb.x).Within(1e-3f), "mirrored about the centre line");
                Assert.That(la.z, Is.GreaterThan(0f), "both in front of the lens");
            }
        }

        [Test]
        public void SolvePair_PullsBackFarEnoughThatBothSubjectsAreInsideTheFrame()
        {
            var rng = new System.Random(4242);
            var concept = PairConcept();
            concept.distance = new Vector2(0f, 0f);   // force the fit to be the only thing holding it back
            const float aspect = 16f / 9f;

            for (int i = 0; i < 400; i++)
            {
                Vector3 a = RandomPoint(rng, 200f);
                Vector3 b = a + RandomDirection(rng) * Mathf.Lerp(10f, 30f, (float)rng.NextDouble());
                const float radius = 5f;

                var shot = ScreenshotFraming.SolvePair(concept, a, b, RandomDirection(rng), radius, rng, aspect: aspect);

                Quaternion inverse = Quaternion.Inverse(shot.Rotation);
                float halfV = shot.FieldOfView * 0.5f * Mathf.Deg2Rad;
                float halfH = Mathf.Atan(Mathf.Tan(halfV) * aspect);

                foreach (Vector3 subject in new[] { a, b })
                {
                    Vector3 local = inverse * (subject - shot.Position);
                    // The hull's own extent has to clear the edge too, not just its origin.
                    float angle = Mathf.Atan2(Mathf.Abs(local.x) + radius, local.z);
                    Assert.That(angle, Is.LessThanOrEqualTo(halfH + 1e-3f),
                        "a subject outside the frame is a photograph of one ship");
                }
            }
        }

        [Test]
        public void SolvePair_IgnoresElevationAndAimLead_BecauseBothWouldBreakThePromise()
        {
            // Same seed, same rolls: the only difference is the two fields the solve must not read.
            var concept = PairConcept();
            var neutered = PairConcept();
            neutered.elevationDegrees = Vector2.zero;
            neutered.aimLeadSeconds = Vector2.zero;

            Vector3 a = new Vector3(5f, -2f, 11f);
            Vector3 b = a + new Vector3(12f, 6f, -9f).normalized * 22f;
            Vector3 flow = new Vector3(0.3f, 0.1f, 1f).normalized;

            var withFields = ScreenshotFraming.SolvePair(concept, a, b, flow, 4f, new System.Random(9), aspect: 1.5f);
            var without = ScreenshotFraming.SolvePair(neutered, a, b, flow, 4f, new System.Random(9), aspect: 1.5f);

            Assert.That(Vector3.Distance(withFields.Position, without.Position), Is.LessThan(1e-4f),
                "elevation must not move a pair camera off the bisector plane");
            Assert.That(Quaternion.Angle(withFields.Rotation, without.Rotation), Is.LessThan(1e-3f),
                "aim lead must not swing a pair camera off the midpoint");
        }

        [Test]
        public void SolvePair_SurvivesATailChase_WhereTheFlowIsParallelToTheSeparation()
        {
            // The degenerate case that actually happens: one ship directly behind the other, so
            // the pair defines no "ahead" to measure the vantage from.
            var concept = PairConcept();
            Vector3 heading = Vector3.forward;
            Vector3 a = Vector3.zero;
            Vector3 b = heading * 18f;

            var shot = ScreenshotFraming.SolvePair(concept, a, b, heading, 4f, new System.Random(1), aspect: 1.6f);

            Assert.IsFalse(float.IsNaN(shot.Position.x) || float.IsNaN(shot.Rotation.x), "no NaN basis");
            Assert.That(Vector3.Distance(shot.Position, a), Is.EqualTo(Vector3.Distance(shot.Position, b)).Within(1e-3f));
        }

        [Test]
        public void SolvePair_CollapsesToASoloShotWhenTheTwoSubjectsCoincide()
        {
            var shot = ScreenshotFraming.SolvePair(
                PairConcept(), Vector3.zero, Vector3.zero, Vector3.forward, 4f, new System.Random(3));

            Assert.IsFalse(float.IsNaN(shot.Position.x), "coincident subjects must not produce NaN");
            Assert.That(shot.Distance, Is.GreaterThanOrEqualTo(ScreenshotFraming.MinimumDistance));
        }

        static Vector3 RandomDirection(System.Random rng)
        {
            var v = new Vector3(
                (float)rng.NextDouble() * 2f - 1f,
                (float)rng.NextDouble() * 2f - 1f,
                (float)rng.NextDouble() * 2f - 1f);
            return v.sqrMagnitude < 1e-6f ? Vector3.forward : v.normalized;
        }

        static Vector3 RandomPoint(System.Random rng, float spread) => RandomDirection(rng) * ((float)rng.NextDouble() * spread);
    }
}
#endif
