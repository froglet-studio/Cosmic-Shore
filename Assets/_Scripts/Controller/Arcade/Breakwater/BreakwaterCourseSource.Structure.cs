using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    // The station half of Breakwater's course source, in its own file so the course half stays
    // compilable outside the editor (Tools/Build/race_course_source_harness compiles the course
    // sources and has no SpawnableBreakwater).
    public sealed partial class BreakwaterCourseSource
    {
        /// <summary>
        /// Hang the stations on the course: one <see cref="BreakwaterStation"/> per ring (the
        /// three values are the same, so the conversion is total), and the spawn pads the shoals
        /// keep clear of. Must run before <c>Spawn</c>, which runs the generation that reads both.
        /// </summary>
        public override bool PoseCourseStructure(Component structure, IReadOnlyList<RaceGate> course,
                                                 Vector3 cellCentre)
        {
            if (structure is not SpawnableBreakwater arena || course == null) return false;

            var stations = new List<BreakwaterStation>(course.Count);
            for (int i = 0; i < course.Count; i++)
                stations.Add(new BreakwaterStation(course[i].Position, course[i].Axis, course[i].Radius));
            arena.SetCourse(stations);
            arena.SetSpawnPads(BreakwaterCourse.SpawnPadRing(cellCentre,
                                                             BreakwaterCourseSettings.DefaultSpawnRingRadius));
            return true;
        }
    }
}
