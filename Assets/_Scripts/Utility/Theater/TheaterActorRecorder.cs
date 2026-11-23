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
        readonly List<GameObject> _lifeForms = new();
        readonly HashSet<GameObject> _notActors = new();
        bool _attached;
        readonly Dictionary<SnowChanger, TheaterShardField> _fields = new();

        public TheaterActorRecorder() : this(TheaterActorThinning.Default) { }
        public TheaterActorRecorder(TheaterActorThinning thin) { _thin = thin; }

        /// <summary>Start listening for what a census cannot see (shards dying limbs add). Idempotent.</summary>
        public void Attach()
        {
            if (_attached) return;
            _attached = true;
            RecordShardFields();
            SnowChanger.MorphedShardAdded += HandleMorphedShard;
        }

        /// <summary>
        /// Every cell's cytoplasm as it stands now (Go): each shard's position. Fields are named by their cell's config
        /// and its order among the enabled cells of that config, as playback matches them.
        /// </summary>
        void RecordShardFields()
        {
            _fields.Clear();
            var cells = Cell.ActiveCellsSnapshot;
            for (int c = 0; c < cells.Count; c++)
            {
                var cell = cells[c];
                var cytoplasm = cell ? cell.Cytoplasm : null;
                if (!cytoplasm || _fields.ContainsKey(cytoplasm)) continue;
                var field = new TheaterShardField { Cell = FieldCellName(cell), Index = FieldIndex(cells, c) };
                cytoplasm.CopyShardPositions(field.Positions);
                _fields[cytoplasm] = field;
                Actors.Shards.Add(field);
            }
        }

        void HandleMorphedShard(SnowChanger field, Vector3 position, Quaternion rotation, float startTime, float duration)
        {
            if (!field || !_fields.TryGetValue(field, out var record)) return;
            float now = Time.time;
            record.Morphs.Add(new TheaterShardMorph
            {
                Time = now, Position = position, Rotation = rotation, StartOffset = startTime - now, Duration = duration
            });
        }

        /// <summary>The name a cell's shard field is recorded under: its config's name.</summary>
        public static string FieldCellName(Cell cell) => cell && cell.Config ? cell.Config.name : string.Empty;

        /// <summary>How many enabled cells before <paramref name="at"/> share its config: the field's index within that name.</summary>
        public static int FieldIndex(IReadOnlyList<Cell> cells, int at)
        {
            string name = FieldCellName(cells[at]);
            int index = 0;
            for (int i = 0; i < at; i++)
                if (cells[i] && cells[i].Cytoplasm && FieldCellName(cells[i]) == name) index++;
            return index;
        }

        /// <summary>Stop listening and close every open track, so each span ends at its last look.</summary>
        public void Detach()
        {
            foreach (var track in _live.Values) track.Close();
            _live.Clear();
            _notActors.Clear();
            if (_attached) SnowChanger.MorphedShardAdded -= HandleMorphedShard;
            _fields.Clear();
            _attached = false;
        }

        /// <summary>One census at <paramref name="now"/> (the vessel recorder's <c>Time.time</c>).</summary>
        public void Sample(float now)
        {
            _seen.Clear();
            SampleCrystals(now);
            SampleLifeForms(now);

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

        void SampleLifeForms(float now)
        {
            CollectLifeForms(_lifeForms);
            for (int i = 0; i < _lifeForms.Count; i++)
            {
                var go = _lifeForms[i];
                if (_live.TryGetValue(go, out var known))
                {
                    _seen.Add(go);
                    var tr = go.transform;
                    known.Observe(now, tr.position, tr.rotation, tr.localScale, _thin);
                    continue;
                }
                if (_notActors.Contains(go)) continue;
                if (!TryClassify(go, out var kind, out int domain)) { _notActors.Add(go); continue; }
                Observe(go, kind, 0, SpeciesName(go.name), domain, go.transform, now);
            }
        }

        /// <summary>
        /// Every live plant and creature root in the enabled cells: what the spawners registered
        /// (<c>Cell.SpawnedLifeForms</c>) and the lineage-registered fauna (<c>Cell.LiveFauna</c>), once each,
        /// active ones only. The recorder and the replay walk the scene the same way, so they see the same actors.
        /// </summary>
        public static void CollectLifeForms(List<GameObject> into)
        {
            into.Clear();
            s_dedupe.Clear();
            var cells = Cell.ActiveCellsSnapshot;
            for (int c = 0; c < cells.Count; c++)
            {
                var cell = cells[c];
                if (!cell) continue;
                var spawned = cell.SpawnedLifeForms;
                for (int i = 0; i < spawned.Count; i++) Add(spawned[i], into);
                var fauna = cell.LiveFauna;
                for (int i = 0; i < fauna.Count; i++) if (fauna[i]) Add(fauna[i].gameObject, into);
            }
        }

        static readonly HashSet<GameObject> s_dedupe = new();

        static void Add(GameObject go, List<GameObject> into)
        {
            if (!go || !go.activeInHierarchy) return;
            if (s_dedupe.Add(go)) into.Add(go);
        }

        /// <summary>A plant (<c>Flora</c> on the root) or a creature (<c>Fauna</c> on the root); anything else is not recorded.</summary>
        public static bool TryClassify(GameObject go, out TheaterActorKind kind, out int domain)
        {
            if (go.TryGetComponent(out Flora flora)) { kind = TheaterActorKind.Flora; domain = (int)flora.Domain; return true; }
            if (go.TryGetComponent(out Fauna fauna)) { kind = TheaterActorKind.Fauna; domain = (int)fauna.Domain; return true; }
            kind = default;
            domain = 0;
            return false;
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
