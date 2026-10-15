using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Skein's course: the cable's rings, read off the ARENA authored on the cell config.
    /// The request's seed and shell are IGNORED by design - a course that is a property of the
    /// arena can only disagree with a seed or a shell invented elsewhere. The gate count IS
    /// honoured: it is the authored race length.
    /// </summary>
    public sealed class SkeinCourseSource : RaceCourseSource
    {
        public override GameModes Mode => GameModes.Skein;

        /// <summary>
        /// The start collar at spine arc 0 - the same point <c>SkeinController</c> lines a match
        /// up on. It is on the SPINE, whose radii are the same at every intensity, so the fallback
        /// settings give the identical collar when no arena resolves.
        /// </summary>
        public override bool TryStartLine(in RaceCourseRequest request, IReadOnlyList<RaceGate> course,
                                          out Vector3 target, out Vector3 axis)
        {
            var settings = TryResolveArena(request.Config, out var arena, out _)
                ? arena.CourseSettings
                : SkeinCourseSettings.ForIntensity(1);
            SkeinCourse.StartPose(settings, out target, out axis);
            return axis.sqrMagnitude > 1e-6f;
        }

        public override int AuthoredGateTarget(int intensity)
        {
            var overrides = EndConditionOverridesSO.Instance;
            return overrides != null
                ? overrides.GetSkeinRingTarget()
                : EndConditionOverridesSO.DefaultSkeinRingTarget;
        }

        /// <summary>
        /// The retry mirrors <c>SpawnableSkein.BuildEnvironment</c> exactly - same base seed, same
        /// 7919 stride, same six attempts - because the arena takes the first seed that lays a
        /// full course and the rings have to land on the same one.
        /// </summary>
        public override List<RaceGate> Build(in RaceCourseRequest request, out string failure)
        {
            if (!TryResolveArena(request.Config, out var arena, out failure)) return null;

            // The AUTHORED target drives the course, so the finish line and the number of rings
            // laid cannot drift - the platform reads the same number for the monitor's target.
            //
            // But the ARENA picks its seed with its OWN ring count (SkeinCourseSettings'), because
            // BuildAll rejects a seed whose walk lays a different number of rings. A count edited
            // in the End Game Conditions window must therefore not choose the seed here, or the
            // two can land on different re-rolls and hang rings on rails the arena never laid. So
            // the arena's count picks the seed and the authored count is walked on THAT seed. At
            // the shipped count the two are the same number and this is the one pass it was.
            var arenaSettings = arena.CourseSettings;
            var settings = arenaSettings;
            settings.GateCount = Mathf.Max(3, request.GateCount);

            for (int attempt = 0; attempt < 6; attempt++)
            {
                int cableSeed = unchecked(arena.CableSeed + attempt * 7919);
                var build = SkeinCourse.BuildAll(cableSeed, arenaSettings);
                if (build == null) continue;

                if (settings.GateCount != arenaSettings.GateCount)
                {
                    build = SkeinCourse.BuildAll(cableSeed, settings);
                    if (build == null)
                    {
                        failure =
                            $"The arena's cable (seed {cableSeed}) cannot carry the authored " +
                            $"{settings.GateCount}-ring course; it was built for " +
                            $"{arenaSettings.GateCount}. Set Skein back to " +
                            $"{arenaSettings.GateCount} in FrogletTools > Game Modes > End Game " +
                            "Conditions, or re-measure Tools/Build/skein_budget.py at the new count.";
                        return null;
                    }
                }

                // The generator works about the ORIGIN and Cell parents the environment container
                // at localPosition zero, so the cable's frame is the CELL's.
                var course = new List<RaceGate>(build.Gates.Count);
                for (int i = 0; i < build.Gates.Count; i++)
                {
                    var g = build.Gates[i];
                    course.Add(new RaceGate(g.Position, g.Axis, g.Radius));
                }
                failure = null;
                return course;
            }

            failure =
                $"Could not lay a {settings.GateCount}-ring cable at N={settings.StrandCount} " +
                "in six seeds - this one will not fix itself by waiting. Check " +
                "Tools/Build/skein_budget.py against these settings.";
            return null;
        }

        /// <summary>The cable's authored settings, off the cell config.</summary>
        public static bool TryResolveArena(CellConfigDataSO config, out SpawnableSkein arena, out string why)
        {
            arena = null;
            if (config == null)
            {
                why = "No Cell whose config is knowable - the cable's settings live on the cell " +
                      "config. A Skein cell must be IntensityWise (its choice is derivable from " +
                      "the intensity the server already holds); a Random multi-config cell " +
                      "cannot answer before it rolls, by design.";
                return false;
            }

            arena = config.EnvironmentPrefab as SpawnableSkein;
            if (arena == null)
            {
                why = $"The cell config '{config.name}' authors no SpawnableSkein " +
                      "EnvironmentPrefab, so there is no cable to hang rings on.";
                return false;
            }

            why = null;
            return true;
        }
    }
}
