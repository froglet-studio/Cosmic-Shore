#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The parity harness's game half (Assets/_Scripts/Utility/Replay): the replay format round
    /// trips through JsonUtility, UnityEngine.Random is reproducible per seed (what the random
    /// goldens assert), DeterministicSession hands every site a reproducible stream and resets
    /// every harness static, ReplayPlayer writes a recorded frame into IInputStatus and applies
    /// its InputEvents, and ReplayRecorder snapshots the status and listens before a frame runs.
    ///
    /// SCOPE (what edit mode can honestly prove): Soap's ScriptableEvent.Raise() early-returns on
    /// !Application.isPlaying, so a press or release the player raises is never DELIVERED here
    /// (the trap ElementalComebackSystemTests documents). These tests assert the player's own
    /// edge bookkeeping (IsHolding) and the recorder's subscription (IsListeningTo) instead;
    /// the edges reaching a vessel is the in-editor verification step.
    /// </summary>
    [TestFixture]
    public class ReplayFileTests
    {
        /// <summary>IInputStatus with plain storage and real SOAP event assets, so the player's writes and raises can be read back.</summary>
        sealed class FakeInputStatus : IInputStatus
        {
            public event Action<bool> OnToggleInputPaused { add { } remove { } }

            public ScriptableEventInputEvents OnButtonPressed { get; } = ScriptableObject.CreateInstance<ScriptableEventInputEvents>();
            public ScriptableEventInputEvents OnButtonReleased { get; } = ScriptableObject.CreateInstance<ScriptableEventInputEvents>();
            public InputController InputController { get; set; }

            public float XSum { get; set; }
            public float YSum { get; set; }
            public float XDiff { get; set; }
            public float YDiff { get; set; }
            public float Throttle { get; set; }
            public float LeftTriggerAnalog { get; set; }
            public float RightTriggerAnalog { get; set; }

            public bool Idle { get; set; }
            public bool Paused { get; set; }
            public bool IsGyroEnabled { get; set; }
            public bool InvertYEnabled { get; set; }
            public bool InvertThrottleEnabled { get; set; }
            public bool OneTouchLeft { get; set; }
            public bool CommandStickControls { get; set; }
            public InputDeviceType ActiveInputDevice { get; set; }

            public Vector2 RightJoystickHome { get; set; }
            public Vector2 LeftJoystickHome { get; set; }
            public Vector2 RightClampedPosition { get; set; }
            public Vector2 LeftClampedPosition { get; set; }
            public Vector2 RightJoystickStart { get; set; }
            public Vector2 LeftJoystickStart { get; set; }
            public Vector2 RightNormalizedJoystickPosition { get; set; }
            public Vector2 LeftNormalizedJoystickPosition { get; set; }
            public Vector2 EasedRightJoystickPosition { get; set; }
            public Vector2 EasedLeftJoystickPosition { get; set; }
            public Vector2 SingleTouchValue { get; set; }
            public Vector3 ThreeDPosition { get; set; }

            public Quaternion GetGyroRotation() => Quaternion.identity;
            public void ResetForReplay() { }

            public void Destroy()
            {
                UnityEngine.Object.DestroyImmediate(OnButtonPressed);
                UnityEngine.Object.DestroyImmediate(OnButtonReleased);
            }
        }

        UnityEngine.Random.State _savedRandomState;

        [SetUp]
        public void SetUp() => _savedRandomState = UnityEngine.Random.state;

        [TearDown]
        public void TearDown()
        {
            DeterministicSession.Reset();
            UnityEngine.Random.state = _savedRandomState;
        }

        static ReplayFile SampleReplay() => new ReplayFile
        {
            scene = "Bootstrap",
            seed = 1337,
            frames = 4200,
            checkpointEvery = 30,
            record = "2400-4200:300",
            @do = new[] { "1500:arcade SkimRace", "2280:hold W 600" },
            status = new[]
            {
                new ReplayStatusFrame
                {
                    f = 2300, XSum = 0.25f, YSum = -0.5f, XDiff = 0.5f, YDiff = 0.125f, Throttle = 1f,
                    LeftTriggerAnalog = 0f, RightTriggerAnalog = 1f,
                    EasedLeftJoystickPosition = new Vector2(0.75f, -0.25f), EasedRightJoystickPosition = new Vector2(-0.5f, 0.5f),
                    LeftNormalizedJoystickPosition = new Vector2(1f, -0.5f), RightNormalizedJoystickPosition = new Vector2(-1f, 0.25f),
                    pressed = new[] { nameof(InputEvents.Button1Action) }, released = Array.Empty<string>(),
                },
                new ReplayStatusFrame
                {
                    f = 2301, XSum = 0f, YSum = 0f, XDiff = 0.5f, YDiff = 0f, Throttle = 0f,
                    pressed = Array.Empty<string>(), released = new[] { nameof(InputEvents.Button1Action) },
                },
            },
        };

        #region Format

        [Test]
        public void ReplayFile_RoundTrips_ThroughJsonUtility()
        {
            var original = SampleReplay();

            var copy = ReplayFile.FromJson(original.ToJson());

            Assert.AreEqual(original.version, copy.version);
            Assert.AreEqual(original.scene, copy.scene);
            Assert.AreEqual(original.seed, copy.seed);
            Assert.AreEqual(original.frames, copy.frames);
            Assert.AreEqual(original.checkpointEvery, copy.checkpointEvery);
            Assert.AreEqual(original.record, copy.record);
            CollectionAssert.AreEqual(original.Do, copy.Do);
            Assert.AreEqual(original.Status.Length, copy.Status.Length);
            for (int i = 0; i < original.Status.Length; i++)
            {
                var a = original.Status[i];
                var b = copy.Status[i];
                Assert.AreEqual(a.f, b.f, $"frame {i} f");
                Assert.AreEqual(a.XSum, b.XSum, 1e-6f, $"frame {i} XSum");
                Assert.AreEqual(a.YSum, b.YSum, 1e-6f, $"frame {i} YSum");
                Assert.AreEqual(a.XDiff, b.XDiff, 1e-6f, $"frame {i} XDiff");
                Assert.AreEqual(a.YDiff, b.YDiff, 1e-6f, $"frame {i} YDiff");
                Assert.AreEqual(a.Throttle, b.Throttle, 1e-6f, $"frame {i} Throttle");
                Assert.AreEqual(a.LeftTriggerAnalog, b.LeftTriggerAnalog, 1e-6f, $"frame {i} LeftTriggerAnalog");
                Assert.AreEqual(a.RightTriggerAnalog, b.RightTriggerAnalog, 1e-6f, $"frame {i} RightTriggerAnalog");
                Assert.AreEqual(a.EasedLeftJoystickPosition, b.EasedLeftJoystickPosition, $"frame {i} EasedLeftJoystickPosition");
                Assert.AreEqual(a.EasedRightJoystickPosition, b.EasedRightJoystickPosition, $"frame {i} EasedRightJoystickPosition");
                Assert.AreEqual(a.LeftNormalizedJoystickPosition, b.LeftNormalizedJoystickPosition, $"frame {i} LeftNormalizedJoystickPosition");
                Assert.AreEqual(a.RightNormalizedJoystickPosition, b.RightNormalizedJoystickPosition, $"frame {i} RightNormalizedJoystickPosition");
                CollectionAssert.AreEqual(a.Pressed, b.Pressed, $"frame {i} pressed");
                CollectionAssert.AreEqual(a.Released, b.Released, $"frame {i} released");
            }
        }

        [Test]
        public void ReplayFile_ParsesTheSpecsExample()
        {
            const string json = "{ \"version\": 1, \"scene\": \"Bootstrap\", \"seed\": 1337, \"frames\": 4200, \"checkpointEvery\": 30,"
                + " \"record\": \"2400-4200:300\", \"do\": [\"1500:arcade SkimRace\", \"2280:hold W 600\"],"
                + " \"status\": [ { \"f\": 2300, \"XSum\": 0.0, \"YSum\": 0.0, \"Throttle\": 1.0, \"pressed\": [\"Button1Action\"], \"released\": [] } ] }";

            var file = ReplayFile.FromJson(json);

            Assert.AreEqual(1, file.version);
            Assert.AreEqual("Bootstrap", file.scene);
            Assert.AreEqual(1337, file.seed);
            Assert.AreEqual(4200, file.frames);
            Assert.AreEqual(30, file.checkpointEvery);
            Assert.AreEqual("2400-4200:300", file.record);
            Assert.IsTrue(file.HasRecordSpec);
            CollectionAssert.AreEqual(new[] { "1500:arcade SkimRace", "2280:hold W 600" }, file.Do);
            Assert.AreEqual(1, file.Status.Length);
            Assert.AreEqual(2300, file.Status[0].f);
            Assert.AreEqual(1f, file.Status[0].Throttle);
            CollectionAssert.AreEqual(new[] { "Button1Action" }, file.Status[0].Pressed);
            CollectionAssert.IsEmpty(file.Status[0].Released);
            // A frame written before the stick vectors existed (this example, the committed
            // replays) still reads, with the sticks at zero: what the player wrote before.
            Assert.AreEqual(Vector2.zero, file.Status[0].EasedLeftJoystickPosition);
            Assert.AreEqual(Vector2.zero, file.Status[0].RightNormalizedJoystickPosition);
        }

        [Test]
        public void ReplayFile_RejectsAnotherVersion()
        {
            Assert.Throws<InvalidDataException>(() => ReplayFile.FromJson("{\"version\":2,\"scene\":\"Bootstrap\"}"));
        }

        [Test]
        public void ReplayFile_SavesAndLoads()
        {
            string path = Path.Combine(Path.GetTempPath(), "cosmic-shore-replay-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                SampleReplay().Save(path);
                var loaded = ReplayFile.Load(path);
                Assert.AreEqual(1337, loaded.seed);
                Assert.AreEqual(2, loaded.Status.Length);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        #endregion

        #region Random

        static (float[] value, int[] range, Vector3[] sphere) Draw(int seed)
        {
            UnityEngine.Random.InitState(seed);
            var value = new float[ParityProbe.RandomDraws];
            var range = new int[ParityProbe.RandomDraws];
            var sphere = new Vector3[ParityProbe.RandomDraws];
            for (int i = 0; i < value.Length; i++) value[i] = UnityEngine.Random.value;
            for (int i = 0; i < range.Length; i++) range[i] = UnityEngine.Random.Range(0, 1000);
            for (int i = 0; i < sphere.Length; i++) sphere[i] = UnityEngine.Random.onUnitSphere;
            return (value, range, sphere);
        }

        [Test]
        public void Random_SequencePerSeed_IsReproducible([Values(0, 1, 42, 1337, 20261006)] int seed)
        {
            var first = Draw(seed);
            var second = Draw(seed);

            CollectionAssert.AreEqual(first.value, second.value, "value");
            CollectionAssert.AreEqual(first.range, second.range, "range");
            CollectionAssert.AreEqual(first.sphere, second.sphere, "onUnitSphere");
            foreach (int r in first.range) Assert.That(r, Is.InRange(0, 999));
        }

        [Test]
        public void Random_DifferentSeeds_Diverge()
        {
            var a = Draw(1);
            var b = Draw(2);
            CollectionAssert.AreNotEqual(a.value, b.value);
        }

        [Test]
        public void RandomGolden_WritesTheThreeSeriesAndRestoresTheState()
        {
            string dir = Path.Combine(Path.GetTempPath(), "cosmic-shore-random-golden-" + Guid.NewGuid().ToString("N"));
            try
            {
                UnityEngine.Random.InitState(7);
                float next = UnityEngine.Random.value;
                UnityEngine.Random.InitState(7);

                string path = ParityProbe.WriteRandomGolden(dir, 42);

                Assert.AreEqual(Path.Combine(dir, "random_42.json"), path);
                string json = File.ReadAllText(path);
                StringAssert.StartsWith("{\"seed\":42,\"value\":[", json);
                StringAssert.Contains("],\"range\":[", json);
                StringAssert.Contains("],\"onUnitSphere\":[[", json);
                Assert.AreEqual(ParityProbe.RandomDraws, json.Split(new[] { "],[" }, StringSplitOptions.None).Length,
                    "onUnitSphere carries RandomDraws triples");
                Assert.AreEqual(next, UnityEngine.Random.value, "the Random state in force before the write is restored");
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }

        #endregion

        #region DeterministicSession

        static int[] Take(System.Random rng, int n)
        {
            var values = new int[n];
            for (int i = 0; i < n; i++) values[i] = rng.Next();
            return values;
        }

        [Test]
        public void DeterministicSession_NewRandom_IsReproducibleForASite_AndDistinctAcrossSites()
        {
            DeterministicSession.Begin(42);
            Assert.IsTrue(DeterministicSession.IsActive);
            Assert.AreEqual(42, DeterministicSession.Seed);
            var aFirst = Take(DeterministicSession.NewRandom("AIPilot"), 8);
            var aSecond = Take(DeterministicSession.NewRandom("AIPilot"), 8);
            var b = Take(DeterministicSession.NewRandom("ToyShuffle"), 8);

            DeterministicSession.Begin(42);
            var aAgain = Take(DeterministicSession.NewRandom("AIPilot"), 8);
            var aSecondAgain = Take(DeterministicSession.NewRandom("AIPilot"), 8);

            CollectionAssert.AreEqual(aFirst, aAgain, "the first request from a site repeats under the same seed");
            CollectionAssert.AreEqual(aSecond, aSecondAgain, "so does the second");
            CollectionAssert.AreNotEqual(aFirst, aSecond, "two requests from one site do not share a stream");
            CollectionAssert.AreNotEqual(aFirst, b, "two sites do not share a stream");

            DeterministicSession.Begin(43);
            CollectionAssert.AreNotEqual(aFirst, Take(DeterministicSession.NewRandom("AIPilot"), 8), "another seed is another stream");
        }

        [Test]
        public void DeterministicSession_Inactive_StillHandsOutARandom()
        {
            DeterministicSession.End();
            Assert.IsFalse(DeterministicSession.IsActive);
            Assert.IsNotNull(DeterministicSession.NewRandom("ProfileModal"));
        }

        [Test]
        public void DeterministicSession_Reset_StopsTheReplayAndTheRecording_AndEndsTheSession()
        {
            DeterministicSession.Begin(42);
            ReplayPlayer.Start(SampleReplay());
            ReplayRecorder.Start();
            Assert.IsTrue(DeterministicSession.IsActive && ReplayPlayer.Active && ReplayRecorder.Recording);

            DeterministicSession.Reset();

            Assert.IsFalse(DeterministicSession.IsActive, "the session ends");
            Assert.IsFalse(ReplayPlayer.Active, "the replay no longer owns the input strategy slot");
            Assert.IsNull(ReplayPlayer.Current);
            Assert.IsFalse(ReplayRecorder.Recording, "the recording is dropped");
        }

        [Test]
        public void DeterministicSession_ResetRunsAtSubsystemRegistration()
        {
            // Domain reload is disabled for Play mode in this project, so this attribute is the
            // only thing that keeps a capture's last replay from owning the next Play's input.
            var reset = typeof(DeterministicSession).GetMethod("ResetStatics", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(reset, "DeterministicSession.ResetStatics");
            var attribute = reset.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>();
            Assert.IsNotNull(attribute, "ResetStatics carries [RuntimeInitializeOnLoadMethod]");
            Assert.AreEqual(RuntimeInitializeLoadType.SubsystemRegistration, attribute.loadType);
        }

        [Test]
        public void DeterministicSession_SiteSeed_IsStableAndNonNegative()
        {
            Assert.AreEqual(DeterministicSession.SiteSeed(42, "AIPilot", 0), DeterministicSession.SiteSeed(42, "AIPilot", 0));
            Assert.AreNotEqual(DeterministicSession.SiteSeed(42, "AIPilot", 0), DeterministicSession.SiteSeed(42, "AIPilot", 1));
            Assert.AreNotEqual(DeterministicSession.SiteSeed(42, "AIPilot", 0), DeterministicSession.SiteSeed(42, "ToyShuffle", 0));
            Assert.GreaterOrEqual(DeterministicSession.SiteSeed(-5, "x", 3), 0);
        }

        #endregion

        #region ReplayPlayer

        [Test]
        public void ReplayPlayer_WritesRecordedFrames_AppliesEdges_AndHoldsTheLast()
        {
            var status = new FakeInputStatus();
            try
            {
                var player = ReplayPlayer.Start(SampleReplay());
                Assert.IsTrue(ReplayPlayer.Active);
                Assert.AreSame(player, ReplayPlayer.Current);

                var strategy = player.Bind(status);
                strategy.OnStrategyActivated();
                Assert.AreEqual(InputDeviceType.Keyboard, status.ActiveInputDevice);

                strategy.ProcessInput();
                Assert.AreEqual(0.25f, status.XSum);
                Assert.AreEqual(-0.5f, status.YSum);
                Assert.AreEqual(0.5f, status.XDiff);
                Assert.AreEqual(0.125f, status.YDiff);
                Assert.AreEqual(1f, status.Throttle);
                Assert.AreEqual(1f, status.RightTriggerAnalog);
                Assert.AreEqual(new Vector2(0.75f, -0.25f), status.EasedLeftJoystickPosition, "single-stick hulls steer from this");
                Assert.AreEqual(new Vector2(-0.5f, 0.5f), status.EasedRightJoystickPosition);
                Assert.AreEqual(new Vector2(1f, -0.5f), status.LeftNormalizedJoystickPosition);
                Assert.AreEqual(new Vector2(-1f, 0.25f), status.RightNormalizedJoystickPosition);
                Assert.IsTrue(player.IsHolding(InputEvents.Button1Action), "frame 0's recorded press is applied");
                Assert.AreEqual(1, player.Index);

                strategy.ProcessInput();
                Assert.AreEqual(0f, status.XSum);
                Assert.AreEqual(0f, status.Throttle);
                Assert.AreEqual(Vector2.zero, status.EasedLeftJoystickPosition, "the second frame's sticks replace the first's");
                Assert.IsFalse(player.IsHolding(InputEvents.Button1Action), "frame 1's recorded release is applied");
                Assert.IsTrue(player.Finished);

                status.XSum = 9f;
                status.EasedLeftJoystickPosition = Vector2.one;
                strategy.ProcessInput();
                Assert.AreEqual(0f, status.XSum, "the last frame is held once the recording ends");
                Assert.AreEqual(Vector2.zero, status.EasedLeftJoystickPosition, "so are its sticks");
                // Edges are applied only on the path that also advances the index, so an index
                // that stays put proves the held frame's edges were not applied again.
                Assert.AreEqual(2, player.Index, "holding does not advance, and does not re-apply edges");

                ReplayPlayer.Stop();
                Assert.IsFalse(ReplayPlayer.Active);
                Assert.IsNull(ReplayPlayer.Current);
            }
            finally
            {
                ReplayPlayer.Stop();
                status.Destroy();
            }
        }

        [Test]
        public void ReplayPlayer_Stop_ReleasesAButtonStillHeld()
        {
            var status = new FakeInputStatus();
            try
            {
                var file = SampleReplay();
                file.status = new[] { file.status[0] };
                var player = ReplayPlayer.Start(file);
                player.Bind(status).ProcessInput();
                Assert.IsTrue(player.IsHolding(InputEvents.Button1Action), "the one frame presses Button1 and never releases it");

                ReplayPlayer.Stop();

                Assert.IsFalse(player.IsHolding(InputEvents.Button1Action), "Stop releases what the replay still held");
                Assert.IsFalse(ReplayPlayer.Active);
            }
            finally
            {
                ReplayPlayer.Stop();
                status.Destroy();
            }
        }

        [Test]
        public void ReplayPlayer_StartFromFile_ReadsTheReplay()
        {
            string path = Path.Combine(Path.GetTempPath(), "cosmic-shore-replay-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                SampleReplay().Save(path);
                var player = ReplayPlayer.StartFromFile(path);
                Assert.AreEqual(2, player.FrameCount);
                Assert.AreEqual(1337, player.File.seed);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        #endregion

        #region ReplayRecorder

        [Test]
        public void ReplayRecorder_ListensBeforeTheFirstFrame_AndCapturesFloatsAndSticks()
        {
            var status = new FakeInputStatus();
            try
            {
                ReplayRecorder.Start(new ReplayFile { scene = "Bootstrap", seed = 9 });
                Assert.IsFalse(ReplayRecorder.IsListeningTo(status), "Start has no status to listen to");

                // InputController calls Listen before the strategy runs, so an edge the strategy
                // raises during the first recorded frame is heard.
                ReplayRecorder.Listen(status);
                Assert.IsTrue(ReplayRecorder.IsListeningTo(status), "subscribed before the first ProcessInput");

                status.XSum = 0.5f;
                status.Throttle = 1f;
                status.EasedLeftJoystickPosition = new Vector2(0.3f, -0.6f);
                status.RightNormalizedJoystickPosition = new Vector2(-0.2f, 0.4f);
                ReplayRecorder.Capture(status);
                Assert.AreEqual(1, ReplayRecorder.FrameCount);

                var file = ReplayRecorder.Stop();
                Assert.IsFalse(ReplayRecorder.Recording);
                Assert.IsFalse(ReplayRecorder.IsListeningTo(status), "Stop unsubscribes");
                Assert.AreEqual(9, file.seed);
                Assert.AreEqual(1, file.Status.Length);
                Assert.AreEqual(1, file.frames, "a header with no frame count takes the recorded length");
                var frame = file.Status[0];
                Assert.AreEqual(0.5f, frame.XSum);
                Assert.AreEqual(1f, frame.Throttle);
                Assert.AreEqual(new Vector2(0.3f, -0.6f), frame.EasedLeftJoystickPosition);
                Assert.AreEqual(new Vector2(-0.2f, 0.4f), frame.RightNormalizedJoystickPosition);
                CollectionAssert.IsEmpty(frame.Pressed);
            }
            finally
            {
                ReplayRecorder.Stop();
                status.Destroy();
            }
        }

        [Test]
        public void ReplayRecorder_IsInertUntilStarted()
        {
            var status = new FakeInputStatus();
            try
            {
                ReplayRecorder.Stop();
                ReplayRecorder.Listen(status);
                ReplayRecorder.Capture(status);
                Assert.IsFalse(ReplayRecorder.IsListeningTo(status));
                Assert.AreEqual(0, ReplayRecorder.FrameCount);
                Assert.IsNull(ReplayRecorder.Stop(), "nothing was recording");
            }
            finally
            {
                status.Destroy();
            }
        }

        #endregion

        #region ParityProbe helpers

        [Test]
        public void ParityProbe_Json_EscapesLikeTheChannelFormat()
        {
            Assert.AreEqual("\"scene:Menu_Main\"", ParityProbe.Json("scene:Menu_Main"));
            Assert.AreEqual("\"a\\\"b\\\\c\"", ParityProbe.Json("a\"b\\c"));
            Assert.AreEqual("\"\\u00e9\"", ParityProbe.Json("\u00e9"));
            Assert.AreEqual("null", ParityProbe.Json(null));
            Assert.IsFalse(ParityProbe.Active, "nothing here begins a probe");
        }

        [Test]
        public void ParityProbe_GameEvents_AreTheSpecsEleven()
        {
            CollectionAssert.AreEqual(new[]
            {
                "OnLaunchGame", "OnSessionStarted", "OnInitializeGame", "OnMiniGameRoundStarted", "OnMiniGameTurnStarted",
                "OnMiniGameTurnEnd", "OnMiniGameRoundEnd", "OnMiniGameEnd", "OnWinnerCalculated", "OnResetForReplay", "OnSessionEnded",
            }, ParityProbe.GameEvents);
            foreach (var name in ParityProbe.GameEvents)
                Assert.IsNotNull(typeof(GameDataSO).GetField(name), $"GameDataSO.{name} is the field the probe hooks");
        }

        #endregion
    }
}
#endif
