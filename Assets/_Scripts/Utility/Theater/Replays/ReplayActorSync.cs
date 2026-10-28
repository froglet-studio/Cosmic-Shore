using System.Collections.Generic;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Stands the match's crystals (and, per category, its plants, creatures and cytoplasm shards) where the
    /// recording has them, every frame of an in-scene replay (<see cref="ReplayMatchDirector"/>, <c>Docs/REPLAYS.md</c>
    /// §6.4). The scene's own actors are kept - their colliders, effects and the vessels' hits on them stay the
    /// game's - and only their placement is taken over, so what the viewer sees is where each one was.
    /// <list type="bullet">
    /// <item><b>Crystals</b> pair by crystal id (a manager hands them out in the same order every run). A jump goes
    /// through the crystal's own manager (<c>RelocateCrystal</c>) so the cell's slots, its bloom and everyone reading
    /// the cell's items agree; every frame after that sets the pose and scale directly.</item>
    /// </list>
    /// </summary>
    public sealed class ReplayActorSync
    {
        readonly Dictionary<int, List<TheaterActorTrack>> _crystals = new();
        readonly List<Crystal> _scratch = new();
        float _jumpSqr = 8f * 8f;

        public bool IsActive { get; private set; }

        /// <summary>Index <paramref name="actors"/> for playback. False (and idle) when there is nothing recorded.</summary>
        public bool Begin(TheaterActors actors, float jumpDistance = 8f)
        {
            _crystals.Clear();
            IsActive = false;
            if (actors == null || actors.IsEmpty) return false;
            _jumpSqr = jumpDistance * jumpDistance;

            for (int i = 0; i < actors.Tracks.Count; i++)
            {
                var track = actors.Tracks[i];
                if (track.Poses.Count == 0) continue;
                if (track.Kind != TheaterActorKind.Crystal) continue;
                if (!_crystals.TryGetValue(track.Key, out var list)) _crystals[track.Key] = list = new List<TheaterActorTrack>();
                list.Add(track);
            }
            foreach (var list in _crystals.Values) list.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));
            IsActive = true;
            return true;
        }

        /// <summary>Place everything for recording time <paramref name="t"/>. Call after the vessels are driven.</summary>
        public void Apply(float t)
        {
            if (!IsActive) return;
            ApplyCrystals(t);
        }

        public void End()
        {
            IsActive = false;
            _crystals.Clear();
        }

        void ApplyCrystals(float t)
        {
            // A copy: a relocation can re-enable a crystal's models, which must not edit the list mid-walk.
            _scratch.Clear();
            var active = Crystal.Active;
            for (int i = 0; i < active.Count; i++) _scratch.Add(active[i]);

            for (int i = 0; i < _scratch.Count; i++)
            {
                var crystal = _scratch[i];
                if (!crystal || crystal.Id <= 0 || crystal.IsEmbedded) continue;
                if (!_crystals.TryGetValue(crystal.Id, out var tracks)) continue;
                if (!TheaterActorTrack.TryEvaluateStays(tracks, t, out var position, out var rotation, out var scale)) continue;

                var transform = crystal.transform;
                if ((transform.position - position).sqrMagnitude > _jumpSqr && crystal.CrystalManager)
                    crystal.CrystalManager.RelocateCrystal(crystal.Id, position);
                transform.SetPositionAndRotation(position, rotation);
                transform.localScale = scale;
            }
        }
    }
}
