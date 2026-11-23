using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Plays a recorded prism world (<see cref="TheaterWorld"/>, Theater P1) as pictures of prisms:
    /// one GPU-instanced box per standing prism, coloured by its domain, no GameObjects, no colliders,
    /// nothing the game can touch. A replay's world is a picture of what was there, never mass.
    ///
    /// <para><b>Seeking.</b> <see cref="Advance"/> applies events in time order up to the time asked
    /// for. Forward is incremental; a time earlier than the last one applied (a rewind, a loop) clears
    /// the world and replays the log from the start, one pass over plain structs.</para>
    ///
    /// <para><b>Drawing.</b> Standing prisms are kept in one list of matrices per (domain, shielded)
    /// with swap-remove, and <see cref="Render"/> draws each list with
    /// <c>Graphics.RenderMeshInstanced</c> in slices of <see cref="MaxPerCall"/>. Shielded prisms are
    /// drawn lighter, the way the game lifts a shielded prism toward white.</para>
    /// </summary>
    public sealed class TheaterWorldPlayback
    {
        public const int MaxPerCall = 1023;
        const float ShieldLift = 0.45f;

        sealed class Bucket
        {
            public byte Domain;
            public bool Shielded;
            public Material Material;
            public readonly List<Matrix4x4> Matrices = new();
            public readonly List<int> Ids = new();
        }

        struct Standing
        {
            public int Bucket;
            public int Index;
            public Vector3 Scale;
        }

        readonly Dictionary<int, Standing> _standing = new();
        readonly Dictionary<int, int> _bucketByKey = new();
        readonly List<Bucket> _buckets = new();

        TheaterWorld _world;
        Material _template;
        bool _ownsTemplate;
        Mesh _mesh;
        int _cursor;
        float _applied = float.NegativeInfinity;
        int _layer;
        bool _drawFailed;

        public bool IsPlaying => _world != null;

        /// <summary>Prisms standing at the time last applied.</summary>
        public int StandingCount => _standing.Count;

        /// <summary>
        /// Start playing <paramref name="world"/>. <paramref name="material"/> is tinted per domain
        /// (copied, never edited); null builds a lit material at the first <see cref="Render"/>.
        /// False when there is nothing to play.
        /// </summary>
        public bool Begin(TheaterWorld world, Material material, int layer = 0)
        {
            Stop();
            if (world == null || world.Events.Count == 0) return false;

            _world = world;
            _template = material;
            _layer = layer;
            Reset();
            return true;
        }

        /// <summary>Prisms standing now with <paramref name="domain"/>, for the overlay and the tests.</summary>
        public int StandingIn(byte domain)
        {
            int n = 0;
            for (int i = 0; i < _buckets.Count; i++)
                if (_buckets[i].Domain == domain) n += _buckets[i].Matrices.Count;
            return n;
        }

        /// <summary>Where the prism recorded as <paramref name="id"/> stands now; false when it does not.</summary>
        public bool TryGetStanding(int id, out Vector3 position)
        {
            position = default;
            if (!_standing.TryGetValue(id, out var s)) return false;
            position = _buckets[s.Bucket].Matrices[s.Index].GetColumn(3);
            return true;
        }

        public void Stop()
        {
            for (int i = 0; i < _buckets.Count; i++)
                if (_buckets[i].Material) Object.Destroy(_buckets[i].Material);
            _buckets.Clear();
            _bucketByKey.Clear();
            _standing.Clear();
            if (_ownsTemplate && _template) Object.Destroy(_template);
            _ownsTemplate = false;
            _template = null;
            _mesh = null;
            _drawFailed = false;
            _world = null;
            _cursor = 0;
            _applied = float.NegativeInfinity;
        }

        /// <summary>Bring the world to <paramref name="time"/> (the recording's own clock).</summary>
        public void Advance(float time)
        {
            if (_world == null) return;
            if (time < _applied) Reset();
            _applied = time;

            var events = _world.Events;
            while (_cursor < events.Count && events[_cursor].Time <= time)
            {
                Apply(events[_cursor]);
                _cursor++;
            }
        }

        /// <summary>Draw what stands. Call once a frame while playing.</summary>
        public void Render()
        {
            if (_world == null || !PrepareDrawing()) return;
            for (int b = 0; b < _buckets.Count; b++)
            {
                var bucket = _buckets[b];
                int count = bucket.Matrices.Count;
                if (count == 0) continue;
                if (!bucket.Material) bucket.Material = MaterialFor(bucket.Domain, bucket.Shielded);

                var rp = new RenderParams(bucket.Material)
                {
                    layer = _layer,
                    shadowCastingMode = ShadowCastingMode.Off,
                    receiveShadows = false,
                    worldBounds = new Bounds(Vector3.zero, Vector3.one * 1e7f),
                };
                for (int start = 0; start < count; start += MaxPerCall)
                    Graphics.RenderMeshInstanced(rp, _mesh, 0, bucket.Matrices, Mathf.Min(MaxPerCall, count - start), start);
            }
        }

        /// <summary>The box and the base material, once. Says so once when there is no shader to draw with.</summary>
        bool PrepareDrawing()
        {
            if (_drawFailed) return false;
            if (!_mesh) _mesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            if (!_template)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Unlit");
                if (shader)
                {
                    _template = new Material(shader) { name = "TheaterWorld (runtime)" };
                    _ownsTemplate = true;
                }
            }
            if (_mesh && _template) return true;

            _drawFailed = true;
            CSDebug.LogWarning("[Theater] No cube mesh or URP shader to draw the recorded world with; the replay shows vessels only.");
            return false;
        }

        void Reset()
        {
            for (int i = 0; i < _buckets.Count; i++)
            {
                _buckets[i].Matrices.Clear();
                _buckets[i].Ids.Clear();
            }
            _standing.Clear();
            _cursor = 0;
            _applied = float.NegativeInfinity;
        }

        void Apply(in TheaterWorldEvent e)
        {
            switch (e.Kind)
            {
                case TheaterWorldEventKind.Born:
                case TheaterWorldEventKind.Restored:
                    Remove(e.Id);
                    Add(e);
                    break;
                case TheaterWorldEventKind.Gone:
                    Remove(e.Id);
                    break;
                case TheaterWorldEventKind.Moved:
                    if (!_standing.TryGetValue(e.Id, out var s)) break;
                    _buckets[s.Bucket].Matrices[s.Index] = Matrix4x4.TRS(e.Position, e.Rotation, s.Scale);
                    break;
            }
        }

        void Add(in TheaterWorldEvent e)
        {
            int b = BucketFor(e.Domain, (e.Flags & (TheaterWorld.FlagShielded | TheaterWorld.FlagSuperShielded)) != 0);
            var bucket = _buckets[b];
            _standing[e.Id] = new Standing { Bucket = b, Index = bucket.Matrices.Count, Scale = e.Scale };
            bucket.Matrices.Add(Matrix4x4.TRS(e.Position, e.Rotation, e.Scale));
            bucket.Ids.Add(e.Id);
        }

        void Remove(int id)
        {
            if (!_standing.TryGetValue(id, out var s)) return;
            _standing.Remove(id);

            var bucket = _buckets[s.Bucket];
            int last = bucket.Matrices.Count - 1;
            if (s.Index != last)
            {
                int movedId = bucket.Ids[last];
                bucket.Matrices[s.Index] = bucket.Matrices[last];
                bucket.Ids[s.Index] = movedId;
                var moved = _standing[movedId];
                moved.Index = s.Index;
                _standing[movedId] = moved;
            }
            bucket.Matrices.RemoveAt(last);
            bucket.Ids.RemoveAt(last);
        }

        int BucketFor(byte domain, bool shielded)
        {
            int key = domain * 2 + (shielded ? 1 : 0);
            if (_bucketByKey.TryGetValue(key, out int index)) return index;

            index = _buckets.Count;
            _buckets.Add(new Bucket { Domain = domain, Shielded = shielded });
            _bucketByKey[key] = index;
            return index;
        }

        Material MaterialFor(byte domain, bool shielded)
        {
            var colorSet = PrismLit.ColorSet;
            Color color = colorSet != null ? colorSet.GetDomainSignalColor((Domains)domain) : Color.white;
            if (shielded) color = Color.Lerp(color, Color.white, ShieldLift);

            var material = new Material(_template) { name = $"TheaterWorld ({(Domains)domain}{(shielded ? ", shielded" : string.Empty)})", enableInstancing = true };
            if (material.HasProperty(BaseColorId)) material.SetColor(BaseColorId, color);
            if (material.HasProperty(ColorId)) material.SetColor(ColorId, color);
            return material;
        }

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
    }
}
