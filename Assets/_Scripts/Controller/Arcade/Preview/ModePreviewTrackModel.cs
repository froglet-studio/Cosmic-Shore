using System.Collections.Generic;
using CosmicShore.Data;
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

        // Jade / Ruby / Gold - Blue is the "no team" sentinel and never appears in a live arena.
        static readonly Domains[] PlayableDomains = { Domains.Jade, Domains.Ruby, Domains.Gold };
    }
}
