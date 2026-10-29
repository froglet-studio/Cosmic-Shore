using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Lays Lantern Run's lanterns (<see cref="LanternRunCourse.Lanterns"/>): a small ball of neutral prisms past each
    /// lantern ring, for the Angler to paint and throw its school at. <see cref="LanternRunController"/> instantiates it in
    /// <c>OnCourseRaised</c>, hands it the course with <see cref="SetCourse"/>, then calls <c>Spawn(intensity)</c> -
    /// Switchback's features shape. Every peer receives the same broadcast course, so every peer lays the same prisms.
    ///
    /// <para>Everything is <see cref="Domains.Blue"/>: a lantern belongs to nobody, so every Angler's brush paints it and
    /// every creature may eat it.</para>
    /// </summary>
    public class SpawnableLanternRunLanterns : CellEnvironmentSpawnableBase
    {
        /// <summary>Never consulted (the layout is a pure function of the course); the base contract requires it.</summary>
        protected override int DefaultSeed => 75;

        /// <summary>Never baked: the layout comes from a course generated per match.</summary>
        protected override bool BakesLayout => false;

        /// <summary>Eight lanterns of eighteen prisms at most.</summary>
        protected override int LayCapacity => LanternRunCourse.LanternPrisms * LanternRunCourse.GatesPerLap;

        readonly List<RaceGate> _course = new(16);
        int _layoutIntensity = 1;
        Vector3 _centre;
        float _clearRadius;

        /// <summary>The ring set (one lap), the intensity, and the sphere (the nucleus) no lantern may sit in.</summary>
        public void SetCourse(IReadOnlyList<RaceGate> course, int intensity, Vector3 centre, float clearRadius)
        {
            _course.Clear();
            if (course != null)
                for (int i = 0; i < course.Count; i++)
                    _course.Add(course[i]);
            _layoutIntensity = intensity;
            _centre = centre;
            _clearRadius = clearRadius;
            InvalidateCache();
        }

        protected override void BuildEnvironment()
        {
            var prisms = LanternRunCourse.Lanterns(_course, _layoutIntensity, _centre, _clearRadius);
            for (int i = 0; i < prisms.Count; i++)
                Emit(prisms[i].Position, prisms[i].Rotation, prisms[i].Scale, Domains.Blue, prisms[i].Kind);
        }

        protected override int BuildParameterHash()
        {
            int hash = System.HashCode.Combine(nameof(SpawnableLanternRunLanterns), 1,
                _course.Count, _layoutIntensity, _centre, _clearRadius);
            for (int i = 0; i < _course.Count; i++)
                hash = System.HashCode.Combine(hash, _course[i].Position, _course[i].Axis, _course[i].Radius);
            return hash;
        }
    }
}
