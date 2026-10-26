using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Lays Switchback's course features (<see cref="SwitchbackFeatures"/>): the skim walls of intensity 3 and the
    /// danger prisms of intensity 4. <see cref="SwitchbackController"/> instantiates it in <c>OnCourseRaised</c>, hands it
    /// the course with <see cref="SetCourse"/>, then calls <c>Spawn(intensity)</c> - the same shape as Breakwater's
    /// stations. Every peer receives the same broadcast course, so every peer lays the same prisms.
    ///
    /// <para>Everything is <see cref="Domains.Blue"/>, as in Breakwater: a feature of the course hands nobody a domain.</para>
    /// </summary>
    public class SpawnableSwitchbackFeatures : CellEnvironmentSpawnableBase
    {
        /// <summary>Never consulted (the layout is a pure function of the course); the base contract requires it.</summary>
        protected override int DefaultSeed => 45;

        /// <summary>Never baked: the layout comes from a course generated per match.</summary>
        protected override bool BakesLayout => false;

        /// <summary>Intensity 3 lays about 200 wall prisms, intensity 4 about 60 danger prisms.</summary>
        protected override int LayCapacity => 512;

        readonly List<RaceGate> _course = new(32);
        int _layoutIntensity = 1;
        Vector3 _centre;
        float _clearRadius;

        /// <summary>
        /// The course and intensity to lay for, and the sphere (the cell's nucleus) no feature may sit in. Must run
        /// before <c>Spawn</c>.
        /// </summary>
        public void SetCourse(IReadOnlyList<RaceGate> course, int intensity, Vector3 centre, float clearRadius)
        {
            _course.Clear();
            if (course != null)
                for (int i = 0; i < course.Count; i++)
                    _course.Add(course[i]);
            _layoutIntensity = intensity;
            _centre = centre;
            _clearRadius = clearRadius;
            // A new course must rebuild: folded into the hash below, and invalidated outright (as Breakwater does).
            InvalidateCache();
        }

        protected override void BuildEnvironment()
        {
            var prisms = SwitchbackFeatures.Layout(_course, _layoutIntensity, _centre, _clearRadius);
            for (int i = 0; i < prisms.Count; i++)
                Emit(prisms[i].Position, prisms[i].Rotation, prisms[i].Scale, Domains.Blue, prisms[i].Kind);
        }

        protected override int BuildParameterHash()
        {
            int hash = System.HashCode.Combine(nameof(SpawnableSwitchbackFeatures),
                /* layout revision - bump to invalidate caches after a code-level change */ 1,
                _course.Count, _layoutIntensity, _centre, _clearRadius);
            for (int i = 0; i < _course.Count; i++)
                hash = System.HashCode.Combine(hash, _course[i].Position, _course[i].Axis, _course[i].Radius);
            return hash;
        }
    }
}
