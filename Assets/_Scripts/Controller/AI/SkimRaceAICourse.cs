using System.Collections.Generic;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// What every Skim Race AI shares about the track it races on: the <see cref="SkimRoute"/>
    /// built from the ribbon's own prisms, the set of those prisms (so a racer never mistakes the
    /// ribbon it skims for something to dodge), and one <see cref="SkimRacingLine"/> — a property of
    /// the ribbon and the hull, not of the pilot, solved once by whichever racer gets there first.
    ///
    /// <para><b>Built from the prisms the spawner laid, never from its waypoints.</b> The ribbon a
    /// pilot sees is the thing to follow (the same rule <see cref="SkimRoute"/> records), and the
    /// prisms are its ground truth. It is built LAZILY, on the first frame an AI asks after the
    /// track exists: the seed reaches the server over the network and the track is laid when it
    /// arrives, so "the countdown has ended" is not proof that it has.</para>
    ///
    /// <para><b>Shells, not boxes.</b> Skim Race lays its track super-shielded, and a shielded
    /// prism collides as its shell (the stellated octahedron reaches
    /// <see cref="OctahedronMeshGenerator.CIRCUMSCRIBING_SCALE"/> times the box's half-extents), so
    /// that is the envelope a hull must clear and the plate a skimmer has to reach.</para>
    /// </summary>
    public sealed class SkimRaceAICourse
    {
        /// <summary>A trail shorter than this is not a ribbon anyone could race.</summary>
        const int MinRoutePrisms = 16;

        readonly SegmentSpawner _spawner;
        readonly HashSet<Prism> _trackPrisms = new();
        SkimRoute _route;

        public SkimRaceAICourse(SegmentSpawner spawner) => _spawner = spawner;

        /// <summary>The racing line the first racer solved, shared with the rest.</summary>
        public SkimRacingLine RacingLine { get; private set; }

        /// <summary>
        /// The route, once the track exists. False while it has not been laid — and again once the
        /// spawner is gone, so a racer can never keep flying a route whose prisms were torn down.
        /// </summary>
        public bool TryGetRoute(out SkimRoute route)
        {
            if (_spawner == null)
            {
                _route = null;
                _trackPrisms.Clear();
                RacingLine = null;
            }
            else if (_route == null)
            {
                TryBuild();
            }
            route = _route;
            return route != null;
        }

        /// <summary>True for a prism of the ribbon itself.</summary>
        public bool IsTrackPrism(Prism prism) => _trackPrisms.Contains(prism);

        /// <summary>Keep the first racing line any racer solves, for the others.</summary>
        public void OfferRacingLine(SkimRacingLine line)
        {
            if (RacingLine == null && line != null) RacingLine = line;
        }

        void TryBuild()
        {
            // The waypoint track is one trail holding every prism in lay order; anything else the
            // spawner laid is shorter. The longest trail is the ribbon.
            Trail ribbon = null;
            var trails = _spawner.Trails;
            for (int i = 0; i < trails.Count; i++)
            {
                var trail = trails[i];
                if (trail?.TrailList == null) continue;
                if (ribbon == null || trail.TrailList.Count > ribbon.TrailList.Count) ribbon = trail;
            }
            if (ribbon == null || ribbon.TrailList.Count < MinRoutePrisms) return;

            var prisms = ribbon.TrailList;
            var list = new List<SkimRoutePrism>(prisms.Count);
            _trackPrisms.Clear();
            for (int i = 0; i < prisms.Count; i++)
            {
                var prism = prisms[i];
                if (prism == null) continue;
                var tf = prism.transform;
                Vector3 half = HalfExtents(prism);
                if (IsShelled(prism)) half *= OctahedronMeshGenerator.CIRCUMSCRIBING_SCALE;
                list.Add(new SkimRoutePrism(tf.position, tf.rotation, half, isMarker: false));
                _trackPrisms.Add(prism);
            }
            if (list.Count < MinRoutePrisms) return;

            // The waypoint track closes from its last point back to its first.
            _route = new SkimRoute(list, closed: true);
        }

        /// <summary>True when the prism collides as its shield shell rather than its box.</summary>
        public static bool IsShelled(Prism prism)
        {
            var props = prism.prismProperties;
            return props != null && (props.IsShielded || props.IsSuperShielded);
        }

        /// <summary>
        /// World half-extents of a prism's box, along its own axes, at the size it is GROWING TO:
        /// a prism still blooming in reports a fraction of its final scale, and a racer must plan
        /// around the solid it is about to become rather than the one on screen this frame.
        /// </summary>
        public static Vector3 HalfExtents(Prism prism)
        {
            var tf = prism.transform;
            Vector3 size = tf.TryGetComponent(out BoxCollider box) ? box.size : Vector3.one;
            Vector3 now = Abs(tf.lossyScale);
            Vector3 parent = tf.parent != null ? Abs(tf.parent.lossyScale) : Vector3.one;
            Vector3 final = Vector3.Scale(Abs(prism.TargetScale), parent);
            return Vector3.Scale(size * 0.5f, Vector3.Max(now, final));
        }

        static Vector3 Abs(Vector3 v) => new(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    }
}
