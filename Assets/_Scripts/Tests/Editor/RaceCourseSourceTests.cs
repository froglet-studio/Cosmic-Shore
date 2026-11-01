#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// <b>The match and the preview must build the SAME course.</b>
    ///
    /// <para>A gate race's course comes from a <see cref="RaceCourseSource"/>. The match builds its
    /// source from the scene's serialized knobs; the arcade card's preview - which never loads the
    /// scene - builds one from the shipped defaults (<see cref="RaceCourseSource.For"/>). The two
    /// agree only while every gate-race scene authors exactly those defaults, and nothing about a
    /// scene retune would tell the author the preview had just drifted. So this reads the SCENE
    /// FILES and holds the pair together: a knob a scene serializes must equal the default, and a
    /// knob it does not serialize keeps the C# initializer, which is the default by construction
    /// (every initializer reads the source's constant).</para>
    ///
    /// <para>Retuning a course is still one edit, just in the right place: change the source's
    /// <c>Default*</c> constant and every scene that does not override it follows.</para>
    ///
    /// <para>The pure-move proof (same seed, bit-identical course before and after the
    /// extraction) is <c>Tools/Build/race_course_source_harness/run.sh</c>, which compiles the
    /// shipped sources outside the editor and runs them against the pre-extraction bodies.</para>
    /// </summary>
    public class RaceCourseSourceTests
    {
        const string SceneFolder = "_Scenes/Multiplayer Scenes";

        static readonly (GameModes mode, string scene, string controller)[] Races =
        {
            (GameModes.Switchback, "MinigameSwitchback", "Controller/Arcade/Switchback/SwitchbackController.cs"),
            (GameModes.Headlong,   "MinigameHeadlong",   "Controller/Arcade/Headlong/HeadlongController.cs"),
            (GameModes.Redline,    "MinigameRedline",    "Controller/Arcade/Redline/RedlineController.cs"),
            (GameModes.Breakwater, "MinigameBreakwater", "Controller/Arcade/Breakwater/BreakwaterController.cs"),
            (GameModes.Skein,      "MinigameSkein",      "Controller/Arcade/Skein/SkeinController.cs"),
            (GameModes.Regatta,    "MinigameRegatta",    "Controller/Arcade/Regatta/RegattaController.cs"),
            (GameModes.Waystation, "MinigameWaystation", "Controller/Arcade/Waystation/WaystationController.cs"),
        };

        [Test]
        public void EveryGateRaceModeHasASourceAndAScene()
        {
            var covered = new HashSet<GameModes>();
            foreach (var r in Races) covered.Add(r.mode);

            foreach (var mode in RaceCourseSource.GateRaceModes)
            {
                Assert.IsTrue(covered.Contains(mode),
                    $"{mode} has a course source but this test does not read its scene - add it to Races.");
                var source = RaceCourseSource.For(mode);
                Assert.IsNotNull(source, $"RaceCourseSource.For({mode}) returned null.");
                Assert.AreEqual(mode, source.Mode, $"RaceCourseSource.For({mode}) answered for another mode.");
            }
        }

        [Test]
        public void EverySceneAuthorsTheSourceDefaults()
        {
            foreach (var (mode, scene, controller) in Races)
            {
                var knobs = ReadControllerBlock(scene, controller);
                var source = RaceCourseSource.For(mode);

                AssertKnob(knobs, "courseOuterRadius", source.OuterRadius, mode);
                AssertKnob(knobs, "courseInnerRadiusFallback", source.InnerRadiusFallback, mode);
                AssertKnob(knobs, "innerRadiusNucleusFactor", source.InnerRadiusNucleusFactor, mode);

                switch (source)
                {
                    case SwitchbackCourseSource s: AssertKnob(knobs, "firstGateDistance", s.FirstGateDistance, mode); break;
                    case HeadlongCourseSource h:   AssertKnob(knobs, "laps", h.Laps, mode); break;
                    case RedlineCourseSource rl:   AssertKnob(knobs, "laps", rl.Laps, mode); break;
                    case BreakwaterCourseSource b: AssertKnob(knobs, "lapsFallback", b.LapsFallback, mode); break;
                }
            }
        }

        /// <summary>
        /// The preview's lap clock closes a lap exactly where the match's fold wraps: once per
        /// pass over the lapped rings, after the lead-in, and the last lap is the race's end.
        /// </summary>
        [Test]
        public void PreviewLapsCloseWhereTheFoldWraps()
        {
            for (int rings = 1; rings <= 20; rings++)
            for (int lead = 0; lead <= 2; lead++)
            for (int laps = 1; laps <= 4; laps++)
            {
                if (lead >= rings) continue;
                int race = GateRaceController.RaceLengthFor(rings, lead, laps);
                int closed = 0;
                for (int t = 1; t <= race; t++)
                {
                    bool boundary = ModePreviewGateCourse.IsLapBoundary(t, rings, lead, race);
                    if (boundary) closed++;

                    // A lap closes on the threading that sends the fold back to the first lapped
                    // ring - or, on the last one, finishes the race.
                    bool wraps = t < race &&
                                 GateRaceController.RingIndexFor(t, rings, lead, laps) == lead &&
                                 t > lead;
                    Assert.AreEqual(wraps || t == race, boundary,
                        $"rings={rings} lead={lead} laps={laps} t={t}");
                }
                Assert.AreEqual(laps, closed, $"rings={rings} lead={lead} laps={laps}: laps closed");
            }
        }

        static void AssertKnob(Dictionary<string, string> knobs, string key, float expected, GameModes mode)
        {
            // Absent = the C# initializer, which reads the same constant the source does.
            if (!knobs.TryGetValue(key, out var raw)) return;

            Assert.IsTrue(float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float got),
                $"{mode}: scene value '{raw}' for {key} is not a number.");
            Assert.AreEqual(expected, got, 1e-4f,
                $"{mode}: the scene serializes {key} = {got} but the preview builds its course with " +
                $"{expected}. Change the source's Default constant rather than the scene, so the match " +
                "and the arcade card's preview stay one course.");
        }

        /// <summary>The top-level fields of the scene's controller MonoBehaviour, found by the
        /// controller script's guid.</summary>
        static Dictionary<string, string> ReadControllerBlock(string scene, string controllerScript)
        {
            string scenePath = Path.Combine(Application.dataPath, SceneFolder, scene + ".unity");
            string metaPath = Path.Combine(Application.dataPath, "_Scripts", controllerScript + ".meta");
            Assert.IsTrue(File.Exists(scenePath), $"Scene not found: {scenePath}");
            Assert.IsTrue(File.Exists(metaPath), $"Controller meta not found: {metaPath}");

            string guid = null;
            foreach (var line in File.ReadAllLines(metaPath))
                if (line.StartsWith("guid: ")) { guid = line.Substring(6).Trim(); break; }
            Assert.IsNotNull(guid, $"No guid in {metaPath}");

            var result = new Dictionary<string, string>();
            string text = File.ReadAllText(scenePath);
            string[] blocks = text.Split(new[] { "\n--- !u!" }, System.StringSplitOptions.None);
            int matches = 0;
            foreach (var block in blocks)
            {
                if (!block.Contains($"m_Script: {{fileID: 11500000, guid: {guid}, type: 3}}")) continue;
                matches++;
                foreach (var raw in block.Split('\n'))
                {
                    string line = raw.TrimEnd('\r');
                    // Top-level scalar fields only: two-space indent, "key: value".
                    if (line.Length < 4 || line[0] != ' ' || line[1] != ' ' || line[2] == ' ') continue;
                    int colon = line.IndexOf(": ", System.StringComparison.Ordinal);
                    if (colon < 0) continue;
                    result[line.Substring(2, colon - 2)] = line.Substring(colon + 2).Trim();
                }
            }

            Assert.AreEqual(1, matches, $"{scene}: expected exactly one {Path.GetFileName(controllerScript)} " +
                                        $"component, found {matches}.");
            return result;
        }
    }
}
#endif
