using System.Collections.Generic;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Records where every crystal (and, per category, every plant, creature and cytoplasm shard) stands, on the
    /// vessel recorder's own samples (<c>Docs/REPLAYS.md</c> §6.4). Each sample is a census: every actor present gets
    /// looked at once and its track keeps the pose when it changed; an actor no longer present has its track closed,
    /// so the replay knows when it left. Nothing here reaches into the game's logic - it reads transforms.
    /// </summary>
    public sealed class TheaterActorRecorder
    {
        public TheaterActors Actors { get; } = new();

        readonly TheaterActorThinning _thin;
        readonly Dictionary<Object, TheaterActorTrack> _live = new();
        readonly HashSet<Object> _seen = new();
        readonly List<Object> _gone = new();
        bool _attached;

        public TheaterActorRecorder() : this(TheaterActorThinning.Default) { }
        public TheaterActorRecorder(TheaterActorThinning thin) { _thin = thin; }

        /// <summary>Start listening for what a census cannot see (shards dying limbs add). Idempotent.</summary>
        public void Attach()
        {
            if (_attached) return;
            _attached = true;
        }

        /// <summary>Stop listening and close every open track, so each span ends at its last look.</summary>
        public void Detach()
        {
            foreach (var track in _live.Values) track.Close();
            _live.Clear();
            _attached = false;
        }

        /// <summary>One census at <paramref name="now"/> (the vessel recorder's <c>Time.time</c>).</summary>
        public void Sample(float now)
        {
            _seen.Clear();
            SampleCrystals(now);

            _gone.Clear();
            foreach (var pair in _live)
                if (!_seen.Contains(pair.Key)) _gone.Add(pair.Key);
            for (int i = 0; i < _gone.Count; i++)
            {
                _live[_gone[i]].Close();
                _live.Remove(_gone[i]);
            }
        }

        void SampleCrystals(float now)
        {
            var crystals = Crystal.Active;
            for (int i = 0; i < crystals.Count; i++)
            {
                var crystal = crystals[i];
                // Manager crystals only: a heart crystal (id 0, embedded) rides its plant or creature.
                if (!crystal || crystal.Id <= 0 || crystal.IsEmbedded) continue;
                Observe(crystal, TheaterActorKind.Crystal, crystal.Id, "Crystal", (int)crystal.ownDomain,
                        crystal.transform, now);
            }
        }

        void Observe(Object key, TheaterActorKind kind, int id, string name, int domain, Transform transform, float now)
        {
            if (!_seen.Add(key)) return;
            if (!_live.TryGetValue(key, out var track))
            {
                track = new TheaterActorTrack { Kind = kind, Key = id, Name = name ?? string.Empty, Domain = domain };
                _live[key] = track;
                Actors.Tracks.Add(track);
            }
            track.Observe(now, transform.position, transform.rotation, transform.localScale, _thin);
        }

        /// <summary>A prefab's species name: its object name without Unity's "(Clone)" suffix.</summary>
        public static string SpeciesName(string objectName)
        {
            if (string.IsNullOrEmpty(objectName)) return string.Empty;
            const string clone = "(Clone)";
            int at = objectName.IndexOf(clone, System.StringComparison.Ordinal);
            return (at >= 0 ? objectName.Substring(0, at) : objectName).Trim();
        }
    }
}
