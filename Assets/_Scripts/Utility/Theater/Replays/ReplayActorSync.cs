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
    /// <item><b>Plants and creatures</b> pair by species (the prefab name) and nearness: a few times a second every
    /// live one without a recorded partner takes the nearest recorded one of its species present at that moment, and
    /// from then on its root stands where that one stood. A live one with no partner (this run grew more, or the
    /// recorded one has not been born yet or is gone) is hidden until one is free; its renderers come back when the
    /// replay ends.</item>
    /// <item><b>Cytoplasm shards</b> pair by field (cell config name and order). Each field is stood at its recorded
    /// positions once, then the shards the match's dying limbs left are added at the moments they were; this run's
    /// own limbs leave none while the replay plays (<c>SnowChanger.ReplayHoldsMorphs</c>). Where a shard points and how
    /// long it is follow from the (replayed) crystal by the shard law.</item>
    /// </list>
    /// </summary>
    public sealed class ReplayActorSync
    {
        readonly Dictionary<int, List<TheaterActorTrack>> _crystals = new();
        readonly List<Crystal> _scratch = new();

        const int DiscoverEveryFrames = 6;
        readonly Dictionary<string, List<TheaterActorTrack>> _species = new();
        readonly Dictionary<GameObject, TheaterActorTrack> _bound = new();
        readonly HashSet<TheaterActorTrack> _taken = new();
        readonly Dictionary<GameObject, string> _speciesOf = new();
        readonly HashSet<GameObject> _notActors = new();
        readonly Dictionary<GameObject, List<Renderer>> _hidden = new();
        readonly List<GameObject> _lifeForms = new();
        readonly List<GameObject> _unbind = new();
        readonly List<Renderer> _renderers = new();
        int _nextDiscovery;
        float _jumpSqr = 8f * 8f;

        sealed class ShardPlayback
        {
            public TheaterShardField Field;
            public SnowChanger Live;
            public bool Placed;
            public int Next;
        }
        readonly List<ShardPlayback> _shards = new();
        readonly List<Cell> _cells = new();

        public bool IsActive { get; private set; }

        /// <summary>Index <paramref name="actors"/> for playback. False (and idle) when there is nothing recorded.</summary>
        public bool Begin(TheaterActors actors, float jumpDistance = 8f)
        {
            End();
            if (actors == null || actors.IsEmpty) return false;
            _jumpSqr = jumpDistance * jumpDistance;

            for (int i = 0; i < actors.Tracks.Count; i++)
            {
                var track = actors.Tracks[i];
                if (track.Poses.Count == 0) continue;
                if (track.Kind != TheaterActorKind.Crystal)
                {
                    string key = SpeciesKey(track.Kind, track.Name);
                    if (!_species.TryGetValue(key, out var species)) _species[key] = species = new List<TheaterActorTrack>();
                    species.Add(track);
                    continue;
                }
                if (!_crystals.TryGetValue(track.Key, out var list)) _crystals[track.Key] = list = new List<TheaterActorTrack>();
                list.Add(track);
            }
            foreach (var list in _crystals.Values) list.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));
            for (int i = 0; i < actors.Shards.Count; i++)
            {
                var field = actors.Shards[i];
                if (field.Positions.Count == 0 && field.Morphs.Count == 0) continue;
                field.SortMorphs();
                _shards.Add(new ShardPlayback { Field = field });
            }
            if (_shards.Count > 0) SnowChanger.ReplayHoldsMorphs = true;
            IsActive = true;
            return true;
        }

        /// <summary>Place everything for recording time <paramref name="t"/>. Call after the vessels are driven.</summary>
        public void Apply(float t)
        {
            if (!IsActive) return;
            ApplyCrystals(t);
            ApplyLifeForms(t);
            ApplyShards(t);
        }

        /// <summary>Stop placing and give every hidden plant and creature its renderers back.</summary>
        public void End()
        {
            IsActive = false;
            _crystals.Clear();
            _species.Clear();
            _bound.Clear();
            _taken.Clear();
            _speciesOf.Clear();
            _notActors.Clear();
            foreach (var pair in _hidden)
                for (int i = 0; i < pair.Value.Count; i++)
                    if (pair.Value[i]) pair.Value[i].enabled = true;
            _hidden.Clear();
            _nextDiscovery = 0;
            if (_shards.Count > 0) SnowChanger.ReplayHoldsMorphs = false;
            _shards.Clear();
        }

        void ApplyShards(float t)
        {
            for (int s = 0; s < _shards.Count; s++)
            {
                var play = _shards[s];
                if (!play.Live && !Find(play)) continue;
                if (!play.Placed)
                {
                    // Positions first; a field not built yet (Initialize) is tried again next frame.
                    if (play.Field.Positions.Count > 0 && !play.Live.ApplyShardPositions(play.Field.Positions)) continue;
                    play.Placed = true;
                }

                var morphs = play.Field.Morphs;
                int due = play.Field.MorphsDue(play.Next, t);
                for (; play.Next < due; play.Next++)
                {
                    var m = morphs[play.Next];
                    // On the live clock: recording time m.Time is (t - m.Time) seconds ago here.
                    float start = PrismClock.Now - (t - m.Time) + m.StartOffset;
                    play.Live.AddRecordedMorphedShard(m.Position, m.Rotation, start, m.Duration);
                }
            }
        }

        /// <summary>The live cytoplasm recorded as <paramref name="play"/>'s field: same cell config, same order.</summary>
        bool Find(ShardPlayback play)
        {
            _cells.Clear();
            var cells = Cell.ActiveCellsSnapshot;
            for (int i = 0; i < cells.Count; i++) _cells.Add(cells[i]);
            for (int i = 0; i < _cells.Count; i++)
            {
                var cell = _cells[i];
                if (!cell || !cell.Cytoplasm) continue;
                if (TheaterActorRecorder.FieldCellName(cell) != play.Field.Cell) continue;
                if (TheaterActorRecorder.FieldIndex(_cells, i) != play.Field.Index) continue;
                play.Live = cell.Cytoplasm;
                return true;
            }
            return false;
        }

        static string SpeciesKey(TheaterActorKind kind, string name) => $"{(byte)kind}:{name}";

        void ApplyLifeForms(float t)
        {
            if (_species.Count == 0) return;
            if (Time.frameCount >= _nextDiscovery)
            {
                _nextDiscovery = Time.frameCount + DiscoverEveryFrames;
                Discover(t);
            }

            _unbind.Clear();
            foreach (var pair in _bound)
            {
                var go = pair.Key;
                if (!go || !pair.Value.TryEvaluate(t, out var position, out var rotation, out var scale))
                {
                    _unbind.Add(go);
                    continue;
                }
                var transform = go.transform;
                transform.SetPositionAndRotation(position, rotation);
                transform.localScale = scale;
                Show(go);
            }
            for (int i = 0; i < _unbind.Count; i++)
            {
                var go = _unbind[i];
                _taken.Remove(_bound[go]);
                _bound.Remove(go);
                if (go) Hide(go);
                else _hidden.Remove(go);
            }
        }

        /// <summary>Partner every live plant and creature that has none with the nearest free recorded one of its species.</summary>
        void Discover(float t)
        {
            TheaterActorRecorder.CollectLifeForms(_lifeForms);
            for (int i = 0; i < _lifeForms.Count; i++)
            {
                var go = _lifeForms[i];
                if (_bound.ContainsKey(go) || _notActors.Contains(go)) continue;
                if (!_speciesOf.TryGetValue(go, out string key))
                {
                    if (!TheaterActorRecorder.TryClassify(go, out var kind, out _)) { _notActors.Add(go); continue; }
                    _speciesOf[go] = key = SpeciesKey(kind, TheaterActorRecorder.SpeciesName(go.name));
                }

                var track = _species.TryGetValue(key, out var candidates)
                    ? TheaterActorTrack.PickNearest(candidates, _taken, go.transform.position, t)
                    : null;
                if (track == null) { Hide(go); continue; }
                _bound[go] = track;
                _taken.Add(track);
            }
        }

        void Hide(GameObject go)
        {
            if (_hidden.ContainsKey(go)) return;
            go.GetComponentsInChildren(true, _renderers);
            var off = new List<Renderer>(_renderers.Count);
            for (int i = 0; i < _renderers.Count; i++)
            {
                if (!_renderers[i].enabled) continue;
                _renderers[i].enabled = false;
                off.Add(_renderers[i]);
            }
            _hidden[go] = off;
        }

        void Show(GameObject go)
        {
            if (!_hidden.TryGetValue(go, out var off)) return;
            for (int i = 0; i < off.Count; i++)
                if (off[i]) off[i].enabled = true;
            _hidden.Remove(go);
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
