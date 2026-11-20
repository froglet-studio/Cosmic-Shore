using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Moves a recorded match onto the arena a replay stands for it. The poses were recorded in the
    /// match scene's world, around that scene's cell (<see cref="ReplaySummary.ArenaOrigin"/>); the
    /// replay's arena is a satellite cell parked far from the menu world
    /// (<see cref="ReplayArenaStage"/>), so every pose is offset by the difference before playback.
    ///
    /// <para>The recording is shifted, not the Theater: <see cref="TheaterPlayback"/> keeps reading
    /// positions as it always has, and its cameras frame what they see. The shift is applied to the
    /// copy loaded for this one watch, never to the file.</para>
    /// </summary>
    public static class ReplayStagePlacement
    {
        /// <summary>What to add to every recorded position so the match lands on a stage at <paramref name="stageOrigin"/>.</summary>
        public static Vector3 Offset(Vector3 recordedArenaOrigin, Vector3 stageOrigin) => stageOrigin - recordedArenaOrigin;

        /// <summary>
        /// Add <paramref name="offset"/> to every pose of every track, and to every recorded prism of
        /// its world, and to every recorded crystal, plant, creature and shard. Times, rotations, speeds and scales are untouched.
        /// </summary>
        public static void Shift(TheaterRecording recording, Vector3 offset)
        {
            if (recording == null) return;
            for (int t = 0; t < recording.Tracks.Count; t++)
            {
                var poses = recording.Tracks[t].Poses;
                for (int i = 0; i < poses.Count; i++)
                {
                    var pose = poses[i];
                    pose.Position = pose.Position + offset;
                    poses[i] = pose;
                }
            }
            recording.World?.Shift(offset);
            recording.Actors?.Shift(offset);
        }
    }
}
