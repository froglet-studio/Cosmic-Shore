using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>Scene access for <see cref="SkimRaceCourse"/>, kept out of the pure geometry class
    /// so the geometry compiles (and is tested) without the scene types.</summary>
    public static class SkimRaceCourseSource
    {
        /// <summary>
        /// Builds the course from the live Skim Race track: the first
        /// <see cref="SpawnableWaypointTrack"/> in the loaded scenes, its laid prisms in lay order.
        /// Returns false until the track has been spawned (the seed arrives over the network a
        /// moment after the scene loads), so callers simply retry.
        /// </summary>
        public static bool TryBuildFromScene(out SkimRaceCourse course)
        {
            course = null;
            var track = Object.FindAnyObjectByType<SpawnableWaypointTrack>();
            if (track == null) return false;

            var trails = track.GetTrails();
            if (trails == null || trails.Count == 0) return false;

            var pts = new List<Vector3>(1024);
            var nrm = new List<Vector3>(1024);
            var rot = new List<Quaternion>(1024);
            var half = new List<Vector3>(1024);
            for (int t = 0; t < trails.Count; t++)
            {
                var list = trails[t]?.TrailList;
                if (list == null) continue;
                for (int i = 0; i < list.Count; i++)
                {
                    var prism = list[i];
                    if (prism == null) continue;
                    var tr = prism.transform;
                    pts.Add(tr.position);
                    nrm.Add(tr.up);
                    rot.Add(tr.rotation);
                    // A shield's shell reaches 1.5 x the leaf (CIRCUMSCRIBING_SCALE 3 on half-extents);
                    // a bare prism's box reaches 0.5 x. The track is super-shielded in Skim Race.
                    var props = prism.prismProperties;
                    bool shelled = props != null && (props.IsSuperShielded || props.IsShielded);
                    half.Add(tr.lossyScale * (shelled ? 1.5f : 0.5f));
                }
            }

            if (pts.Count < 3) return false;
            course = new SkimRaceCourse(pts, nrm, rot, half);
            return true;
        }
    }
}
