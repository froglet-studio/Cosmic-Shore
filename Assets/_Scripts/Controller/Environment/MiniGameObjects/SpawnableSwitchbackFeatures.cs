using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Lays Switchback's Blast Run prisms (<see cref="SwitchbackBlastRun"/>): the doors and block fields of every
    /// blocked gate. <see cref="SwitchbackController"/> instantiates it in <c>OnCourseRaised</c>, hands it the layout
    /// with <see cref="SetLayout"/>, then calls <c>Spawn(intensity)</c> - the same shape as Breakwater's stations.
    /// Every peer receives the same broadcast course, so every peer lays the same prisms.
    ///
    /// <para>Everything is <see cref="Domains.Blue"/>, as in Breakwater: a feature of the course hands nobody a domain.</para>
    /// </summary>
    public class SpawnableSwitchbackFeatures : CellEnvironmentSpawnableBase
    {
        /// <summary>Never consulted (the layout is a pure function of the course); the base contract requires it.</summary>
        protected override int DefaultSeed => 45;

        /// <summary>Never baked: the layout comes from a course generated per match.</summary>
        protected override bool BakesLayout => false;

        /// <summary>The list's starting size: Blast Run lays up to about 3400 prisms (<see cref="SwitchbackBlastRun.LayCapacity"/>).</summary>
        protected override int LayCapacity => SwitchbackBlastRun.LayCapacity;

        readonly List<RaceGate> _course = new(32);
        int _layoutIntensity = 1;
        SwitchbackFeatureLayout _layout;
        Vector3 _centre;
        float _clearRadius;

        /// <summary>
        /// The course, intensity and Blast Run layout to lay (the controller already computed the layout: its runtime
        /// needs the same one to move the movers), and the sphere (the cell's nucleus) kept clear. Must run before
        /// <c>Spawn</c>.
        /// </summary>
        public void SetLayout(IReadOnlyList<RaceGate> course, int intensity, SwitchbackFeatureLayout layout,
                              Vector3 centre, float clearRadius)
        {
            _course.Clear();
            if (course != null)
                for (int i = 0; i < course.Count; i++)
                    _course.Add(course[i]);
            _layoutIntensity = intensity;
            _layout = layout;
            _centre = centre;
            _clearRadius = clearRadius;
            // A new course must rebuild: folded into the hash below, and invalidated outright (as Breakwater does).
            InvalidateCache();
        }

        protected override void BuildEnvironment()
        {
            if (_layout == null) return;
            var prisms = _layout.Prisms;
            for (int i = 0; i < prisms.Count; i++)
                Emit(prisms[i].Position, prisms[i].Rotation, prisms[i].Scale, Domains.Blue, prisms[i].Kind);
        }

        protected override int BuildParameterHash()
        {
            int hash = System.HashCode.Combine(nameof(SpawnableSwitchbackFeatures),
                /* layout revision - bump to invalidate caches after a code-level change */ 5,
                _course.Count, _layoutIntensity, _centre, _clearRadius, _layout != null ? _layout.Prisms.Count : -1);
            for (int i = 0; i < _course.Count; i++)
                hash = System.HashCode.Combine(hash, _course[i].Position, _course[i].Axis, _course[i].Radius);
            return hash;
        }
    }
}
