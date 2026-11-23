using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Turns a <see cref="SpawnableWaypointTrack"/> into the lay list the card's scale model
    /// samples - one <see cref="PrismLay"/> per block the track would actually lay at that
    /// intensity, straight off the track's own <see cref="SpawnableWaypointTrack.GetPreviewBlocks"/>
    /// (the mirror of <c>Spawn</c> that exists precisely so a preview never has to re-derive the
    /// waypoint / spline / spacing math).
    ///
    /// <para>Only the waypoint track needs this translation: every other scene-built spawnable
    /// goes through <see cref="CellMiniatureBuilder.Build"/> like an authored environment does.
    /// The waypoint track is the one whose output is a function of INTENSITY on a single asset -
    /// its four authored waypoint sets are four different circuits - and
    /// <c>GetTrailData()</c> has no intensity to give it.</para>
    /// </summary>
    public static class ModePreviewTrackModel
    {
        /// <summary>
        /// The track's blocks at <paramref name="intensity"/> as lays.
        ///
        /// <para><b>Domains cycle the playable triad per waypoint segment</b>, matching what the
        /// player actually sees: <c>SegmentSpawner</c> paints segments in the active players'
        /// domains, so a live track is never one colour. A marker block opens a new segment
        /// (<see cref="SpawnableWaypointTrack.PreviewBlock.IsMarker"/> is true exactly on each
        /// segment's first block), which is where the cursor advances - the same
        /// "colour is structure" read the planting model uses.</para>
        /// </summary>
        public static List<PrismLay> BuildWaypointLays(SpawnableWaypointTrack track, int intensity)
        {
            var lays = new List<PrismLay>();
            if (!track) return lays;

            int domainCursor = 0;
            bool sawMarker = false;

            foreach (var block in track.GetPreviewBlocks(Mathf.Max(1, intensity)))
            {
                if (block.IsMarker)
                {
                    if (sawMarker) domainCursor++;
                    sawMarker = true;
                }

                var domain = PlayableDomains[domainCursor % PlayableDomains.Length];
                lays.Add(new PrismLay(
                    new SpawnPoint(block.Position, block.Rotation, block.Scale), domain));
            }

            return lays;
        }

        /// <summary>
        /// A gate race's COURSE as lays - each ring drawn as a loop of shards in its own mouth
        /// plane - so the card's scale model shows the race before anybody taps in.
        ///
        /// <para>Twenty-two arcade cards are gate races, and their arena is the course their
        /// controller lays at match start, not anything their cell authors: before this the card
        /// showed a bare membrane and core that looked the same at every intensity, which read as
        /// "no preview" on most of the arcade. The course comes from the SAME source, seed,
        /// intensity, gate target and shell the flight's <see cref="ModePreviewGateCourse.Raise"/>
        /// uses, so the model is the course the tap then flies - and every course setting is
        /// per-intensity (gate count, mouths, corners), so moving the row redraws it.</para>
        ///
        /// <para>Empty when the mode is not a gate race or its source declines (Skein needs its
        /// cell's environment; a source that cannot answer says why on the flight path).</para>
        /// </summary>
        public static List<PrismLay> BuildGateCourseLays(GameModes mode, int intensity,
                                                         CellConfigDataSO config, float nucleusRadius)
        {
            var lays = new List<PrismLay>();
            var source = RaceCourseSource.For(mode);
            if (source == null) return lays;

            int rung = Mathf.Clamp(intensity, 1, 4);
            source.ResolveShell(nucleusRadius, out float inner, out float outer);
            var request = new RaceCourseRequest(CourseSeed(mode), rung, source.AuthoredGateTarget(rung),
                                                inner, outer, config);
            var course = source.Build(request, out _);
            if (course == null) return lays;

            for (int i = 0; i < course.Count; i++)
            {
                var gate = course[i];
                Vector3 axis = gate.Axis.sqrMagnitude > 1e-6f ? gate.Axis.normalized : Vector3.forward;
                Vector3 side = Vector3.Cross(axis, Mathf.Abs(axis.y) > 0.98f ? Vector3.right : Vector3.up).normalized;
                Vector3 up = Vector3.Cross(side, axis);

                float radius = Mathf.Max(1f, gate.Radius);
                float arc = 2f * Mathf.PI * radius / RingShards;
                var scale = new Vector3(radius * RingShardThickness, radius * RingShardThickness, arc * 0.8f);

                // The first ring is the one a pilot threads first: Gold, the colour of "next".
                // The rest share Jade, so the course reads as one line and its start stands out.
                var domain = i == 0 ? Domains.Gold : Domains.Jade;

                for (int k = 0; k < RingShards; k++)
                {
                    float a = k * (2f * Mathf.PI / RingShards);
                    Vector3 radial = side * Mathf.Cos(a) + up * Mathf.Sin(a);
                    Vector3 tangent = Vector3.Cross(axis, radial);
                    lays.Add(new PrismLay(new SpawnPoint(gate.Position + radial * radius,
                                                         Quaternion.LookRotation(tangent, radial), scale),
                                          domain));
                }
            }

            return lays;
        }

        /// <summary>The seed the flight's course uses (<see cref="ModePreviewGateCourse.Raise"/>),
        /// so the model and the flight are one course.</summary>
        public static int CourseSeed(GameModes mode) =>
            ModePreviewPlantingModel.StableSeed(mode.ToString()) & 0x7FFFFFFF;

        const int RingShards = 20;
        const float RingShardThickness = 0.12f;

        // Jade / Ruby / Gold - Blue is the "no team" sentinel and never appears in a live arena.
        static readonly Domains[] PlayableDomains = { Domains.Jade, Domains.Ruby, Domains.Gold };
    }
}
