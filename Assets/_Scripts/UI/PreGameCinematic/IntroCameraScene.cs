using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// The arena as an intro sees it: the cell centre and size, the vessels, crystals, race gates
    /// and the biggest pieces of geometry, gathered ONCE when the intro starts. Shots name these
    /// anchors (<see cref="IntroAnchor"/>) instead of coordinates, so one intro plays in every
    /// arena of its family. Any anchor a scene lacks falls back to the centre.
    /// </summary>
    public sealed class IntroCameraScene
    {
        public struct Pilot
        {
            public Transform Transform;
            /// <summary>Null when the vessel has no status yet.</summary>
            public Domains? Domain;
        }

        public Vector3 Center { get; }
        public float Radius { get; }
        /// <summary>Arena units to world units: the cell radius over the library's reference radius.</summary>
        public float Scale { get; }
        public Transform Player { get; }

        readonly List<Pilot> _pilots;          // nearest the player first; the player is index 0
        readonly List<Transform> _rivals;      // nearest first
        readonly List<Transform> _teammates;   // nearest first, the player excluded
        readonly List<Vector3> _crystals;      // nearest the centre first
        readonly List<Pose> _gates;            // course order; rotation looks down the course
        readonly List<Vector3> _landmarks;     // biggest first
        readonly Dictionary<long, IntroPath> _paths = new();

        public int PilotCount => _pilots.Count;
        public int CrystalCount => _crystals.Count;
        public int GateCount => _gates.Count;
        public int LandmarkCount => _landmarks.Count;

        public IntroCameraScene(Vector3 center, float radius, float scale, Transform player,
                                IReadOnlyList<Pilot> pilots, IReadOnlyList<Vector3> crystals,
                                IReadOnlyList<Pose> gates, IReadOnlyList<Vector3> landmarks)
        {
            Center = center;
            Radius = radius;
            Scale = Mathf.Max(0.01f, scale);
            Player = player;

            Vector3 from = player ? player.position : center;
            Domains? own = null;
            _pilots = new List<Pilot>();
            if (pilots != null)
                for (int i = 0; i < pilots.Count; i++)
                {
                    if (!pilots[i].Transform) continue;
                    _pilots.Add(pilots[i]);
                    if (pilots[i].Transform == player) own = pilots[i].Domain;
                }
            _pilots.Sort((a, b) => (a.Transform.position - from).sqrMagnitude.CompareTo((b.Transform.position - from).sqrMagnitude));

            _rivals = new List<Transform>();
            _teammates = new List<Transform>();
            foreach (var p in _pilots)
            {
                if (p.Transform == player) continue;
                if (own.HasValue && p.Domain == own) _teammates.Add(p.Transform);
                else _rivals.Add(p.Transform);
            }

            _crystals = crystals != null ? new List<Vector3>(crystals) : new List<Vector3>();
            _crystals.Sort((a, b) => (a - center).sqrMagnitude.CompareTo((b - center).sqrMagnitude));
            _gates = gates != null ? new List<Pose>(gates) : new List<Pose>();
            _landmarks = landmarks != null ? new List<Vector3>(landmarks) : new List<Vector3>();
        }

        /// <summary>
        /// Reads the live arena: the cell nearest the pilot, <paramref name="vessels"/>, every
        /// <see cref="Crystal"/>, every <see cref="RaceGateRing"/>, and the largest mesh renderers.
        /// One pass when the intro starts; nothing here runs per frame.
        /// </summary>
        public static IntroCameraScene Gather(Vector3 fallbackCenter, Transform player,
                                              IReadOnlyList<IVessel> vessels, float referenceRadius, float scaleMultiplier)
        {
            Vector3 probe = player ? player.position : fallbackCenter;
            var cell = Cell.FindNearestActiveCell(probe);
            Vector3 center = cell ? cell.transform.position : fallbackCenter;
            float radius = cell ? cell.MembraneRadius : 0f;

            var pilots = new List<Pilot>();
            var roots = new HashSet<Transform>();
            if (vessels != null)
                for (int i = 0; i < vessels.Count; i++)
                {
                    var v = vessels[i];
                    if (v == null || !(v is Object o && o) || !v.Transform) continue;
                    var status = v.VesselStatus;
                    pilots.Add(new Pilot { Transform = v.Transform, Domain = status != null ? status.Domain : (Domains?)null });
                    roots.Add(v.Transform.root);
                }
            if (player && !roots.Contains(player.root))
            {
                pilots.Add(new Pilot { Transform = player, Domain = null });
                roots.Add(player.root);
            }

            var crystals = new List<Vector3>();
            foreach (var c in Object.FindObjectsByType<Crystal>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (c) crystals.Add(c.transform.position);

            var rings = new List<RaceGateRing>(Object.FindObjectsByType<RaceGateRing>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
            rings.Sort((a, b) => a.Index.CompareTo(b.Index));
            var gates = new List<Pose>(rings.Count);
            foreach (var r in rings)
                gates.Add(new Pose(r.transform.position, Quaternion.LookRotation(r.Axis.sqrMagnitude > 1e-6f ? r.Axis : Vector3.forward)));

            if (radius <= 0f) radius = EstimateRadius(center, pilots, crystals, gates, referenceRadius);
            var landmarks = FindLandmarks(center, radius, roots);

            float scale = radius / Mathf.Max(1f, referenceRadius) * Mathf.Max(0.01f, scaleMultiplier);
            return new IntroCameraScene(center, radius, scale, player, pilots, crystals, gates, landmarks);
        }

        static float EstimateRadius(Vector3 center, List<Pilot> pilots, List<Vector3> crystals, List<Pose> gates, float fallback)
        {
            float far = 0f;
            foreach (var p in pilots) far = Mathf.Max(far, (p.Transform.position - center).magnitude);
            foreach (var c in crystals) far = Mathf.Max(far, (c - center).magnitude);
            foreach (var g in gates) far = Mathf.Max(far, (g.position - center).magnitude);
            return far > 1f ? far * 1.2f : fallback;
        }

        // The biggest pieces of arena geometry: wrecks, cacti, posts. Skips vessels, the membrane
        // and anything as big as the cell, and anything too small to frame; keeps eight, apart.
        static List<Vector3> FindLandmarks(Vector3 center, float radius, HashSet<Transform> vesselRoots)
        {
            const int Keep = 8;
            float min = radius * 0.03f, max = radius * 0.6f, apart = radius * 0.12f;
            var found = new List<(Vector3 pos, float size)>();
            foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!r || !r.enabled || vesselRoots.Contains(r.transform.root)) continue;
                var b = r.bounds;
                float extent = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                if (extent < min || extent > max) continue;
                if ((b.center - center).sqrMagnitude > radius * radius) continue;
                found.Add((b.center, b.size.x * b.size.y * b.size.z));
            }
            found.Sort((a, b) => b.size.CompareTo(a.size));

            var keep = new List<Vector3>(Keep);
            foreach (var f in found)
            {
                bool near = false;
                foreach (var k in keep) if ((k - f.pos).sqrMagnitude < apart * apart) { near = true; break; }
                if (near) continue;
                keep.Add(f.pos);
                if (keep.Count == Keep) break;
            }
            return keep;
        }

        /// <summary>World position of an anchor, without its offset.</summary>
        public Vector3 AnchorPosition(IntroAnchor anchor, int index) =>
            TryAnchor(anchor, index, out var p, out _) ? p : Center;

        public Vector3 Resolve(in IntroPoint point)
        {
            if (!TryAnchor(point.Anchor, point.Index, out var pos, out var rot)) { pos = Center; rot = Quaternion.identity; }
            return pos + OffsetToWorld(point.Offset, point.Space, rot);
        }

        public Vector3 OffsetToWorld(Vector3 offset, IntroOffsetSpace space, Quaternion anchorRotation) => space switch
        {
            IntroOffsetSpace.Units => offset,
            IntroOffsetSpace.Local => anchorRotation * (offset * Scale),
            IntroOffsetSpace.LocalUnits => anchorRotation * offset,
            _ => offset * Scale,
        };

        bool TryAnchor(IntroAnchor anchor, int index, out Vector3 position, out Quaternion rotation)
        {
            rotation = Quaternion.identity;
            position = Center;
            switch (anchor)
            {
                case IntroAnchor.Center:
                    return true;
                case IntroAnchor.Player:
                    if (!Player) return false;
                    position = Player.position; rotation = Player.rotation;
                    return true;
                case IntroAnchor.Rival:
                    return TryTransform(_rivals, index, out position, out rotation);
                case IntroAnchor.Teammate:
                    return TryTransform(_teammates, index, out position, out rotation);
                case IntroAnchor.Pilots:
                    if (_pilots.Count == 0) return false;
                    if (index < 0)
                    {
                        var sum = Vector3.zero;
                        foreach (var p in _pilots) sum += p.Transform.position;
                        position = sum / _pilots.Count;
                        rotation = _pilots[0].Transform.rotation;
                        return true;
                    }
                    var t = _pilots[Wrap(index, _pilots.Count)].Transform;
                    position = t.position; rotation = t.rotation;
                    return true;
                case IntroAnchor.Crystal:
                    return TryPoint(_crystals, index, out position);
                case IntroAnchor.Gate:
                    if (_gates.Count == 0) return false;
                    if (index < 0)
                    {
                        var sum = Vector3.zero;
                        foreach (var g in _gates) sum += g.position;
                        position = sum / _gates.Count;
                        return true;
                    }
                    var gate = _gates[Wrap(index, _gates.Count)];
                    position = gate.position; rotation = gate.rotation;
                    return true;
                case IntroAnchor.Landmark:
                    return TryPoint(_landmarks, index, out position);
            }
            return false;
        }

        static bool TryTransform(List<Transform> list, int index, out Vector3 position, out Quaternion rotation)
        {
            position = default; rotation = Quaternion.identity;
            if (list.Count == 0) return false;
            var t = list[Wrap(Mathf.Max(0, index), list.Count)];
            if (!t) return false;
            position = t.position; rotation = t.rotation;
            return true;
        }

        static bool TryPoint(List<Vector3> list, int index, out Vector3 position)
        {
            position = default;
            if (list.Count == 0) return false;
            if (index < 0)
            {
                var sum = Vector3.zero;
                foreach (var p in list) sum += p;
                position = sum / list.Count;
                return true;
            }
            position = list[Wrap(index, list.Count)];
            return true;
        }

        static int Wrap(int i, int n) => ((i % n) + n) % n;

        /// <summary>
        /// The line a Path shot flies. Gates and crystals need at least three points; with fewer it
        /// is a ring round the centre instead, so the shot still flies.
        /// </summary>
        public IntroPath PathFor(IntroPathSource source, float ringRadius, float ringHeight)
        {
            long key = ((long)source << 48) ^ ((long)Mathf.RoundToInt(ringRadius * 10f) << 20) ^ Mathf.RoundToInt(ringHeight * 10f);
            if (_paths.TryGetValue(key, out var cached)) return cached;

            IntroPath path = null;
            if (source == IntroPathSource.Gates && _gates.Count >= 3)
            {
                var pts = new List<Vector3>(_gates.Count);
                float spacing = 0f;
                for (int i = 0; i < _gates.Count; i++)
                {
                    pts.Add(_gates[i].position);
                    if (i > 0) spacing += (_gates[i].position - _gates[i - 1].position).magnitude;
                }
                spacing /= _gates.Count - 1;
                bool closed = (pts[0] - pts[pts.Count - 1]).magnitude < spacing * 2f;
                path = new IntroPath(pts, closed);
            }
            else if (source == IntroPathSource.Crystals && _crystals.Count >= 3)
            {
                var pts = new List<Vector3>(_crystals);
                var c = Center;
                pts.Sort((a, b) => Mathf.Atan2(a.z - c.z, a.x - c.x).CompareTo(Mathf.Atan2(b.z - c.z, b.x - c.x)));
                path = new IntroPath(pts, true);
            }
            path ??= IntroPath.Ring(Center, ringRadius * Scale, ringHeight * Scale, 48);
            _paths[key] = path;
            return path;
        }
    }

    /// <summary>A Catmull-Rom line sampled by arc length, so a shot flies it at an even pace.</summary>
    public sealed class IntroPath
    {
        readonly Vector3[] _samples;
        readonly float[] _cumulative;
        public bool Closed { get; }
        public float Length { get; }

        public IntroPath(IReadOnlyList<Vector3> points, bool closed, int samplesPerSegment = 24)
        {
            Closed = closed;
            int n = points.Count;
            int segments = closed ? n : n - 1;
            var list = new List<Vector3>(segments * samplesPerSegment + 1);
            for (int s = 0; s < segments; s++)
            {
                Vector3 p0 = points[Index(s - 1, n, closed)], p1 = points[Index(s, n, closed)],
                        p2 = points[Index(s + 1, n, closed)], p3 = points[Index(s + 2, n, closed)];
                for (int k = 0; k < samplesPerSegment; k++)
                    list.Add(CatmullRom(p0, p1, p2, p3, k / (float)samplesPerSegment));
            }
            list.Add(closed ? points[0] : points[n - 1]);
            _samples = list.ToArray();
            _cumulative = new float[_samples.Length];
            for (int i = 1; i < _samples.Length; i++)
                _cumulative[i] = _cumulative[i - 1] + (_samples[i] - _samples[i - 1]).magnitude;
            Length = _cumulative[_cumulative.Length - 1];
        }

        public static IntroPath Ring(Vector3 center, float radius, float height, int points)
        {
            var pts = new List<Vector3>(points);
            for (int i = 0; i < points; i++)
            {
                float a = i / (float)points * Mathf.PI * 2f;
                pts.Add(center + new Vector3(Mathf.Cos(a) * radius, height, Mathf.Sin(a) * radius));
            }
            return new IntroPath(pts, true, 4);
        }

        static int Index(int i, int n, bool closed) => closed ? ((i % n) + n) % n : Mathf.Clamp(i, 0, n - 1);

        static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * p1 + (p2 - p0) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (3f * p1 - p0 - 3f * p2 + p3) * t3);
        }

        float Wrap(float u) => Closed ? u - Mathf.Floor(u) : Mathf.Clamp01(u);

        public Vector3 PointAt(float u)
        {
            if (_samples.Length == 1 || Length <= 0f) return _samples[0];
            float d = Wrap(u) * Length;
            int hi = System.Array.BinarySearch(_cumulative, d);
            if (hi >= 0) return _samples[hi];
            hi = Mathf.Clamp(~hi, 1, _samples.Length - 1);
            int lo = hi - 1;
            float span = _cumulative[hi] - _cumulative[lo];
            return Vector3.Lerp(_samples[lo], _samples[hi], span > 0f ? (d - _cumulative[lo]) / span : 0f);
        }

        public Vector3 TangentAt(float u)
        {
            const float du = 0.002f;
            Vector3 a = PointAt(Closed ? u - du : Mathf.Max(0f, u - du)), b = PointAt(Closed ? u + du : Mathf.Min(1f, u + du));
            var d = b - a;
            return d.sqrMagnitude > 1e-10f ? d.normalized : Vector3.forward;
        }
    }
}
