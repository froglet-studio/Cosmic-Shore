using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A ribbon of prisms as a racer reasons about it: a centre line with an arc-length parameter,
    /// a frame at every point (the plates' own right / up), and the envelope a hull must clear.
    ///
    /// <para><b>Built from the prisms, never from the waypoints that laid them.</b> The ribbon a
    /// pilot sees is the thing to follow, and the prisms are its ground truth: a waypoint list is a
    /// recipe (Catmull-Rom for one intensity, straight lines for another) and re-deriving it would be
    /// a second copy of the track generator that drifts the day the generator changes. Consecutive
    /// prisms are 12 u apart, so the chord between two centres departs from the curve they sit on by
    /// hundredths of a unit on any track in the game.</para>
    ///
    /// <para><b>The arc parameter is UNWRAPPED.</b> A racer on its third lap is at <c>s ≈ 2L</c>, not
    /// back at zero, so "how far ahead is that crystal" and "is this key behind me" are plain
    /// subtraction instead of modular arithmetic at every call site. <see cref="Wrap"/> is applied
    /// only where the prisms are indexed.</para>
    ///
    /// Pure: no Unity object, no allocation after construction. The harness compiles this file
    /// verbatim (Tools/Build/squirrel_ai_harness).
    /// </summary>
    public sealed class SkimRoute
    {
        readonly Vector3[] _pos;
        readonly Vector3[] _fwd;
        readonly Vector3[] _right;
        readonly Vector3[] _up;
        readonly Vector3[] _semi;
        readonly float[] _start;   // arc length at prism i
        readonly float[] _seg;     // chord from prism i to the next

        /// <summary>Length of one lap, closing chord included when <see cref="Closed"/>.</summary>
        public float Length { get; }
        public bool Closed { get; }
        public int Count => _pos.Length;
        /// <summary>Mean arc between consecutive prisms — how many prisms a skim passes per unit
        /// travelled, which is what the boost economy is paid in.</summary>
        public float MeanSpacing => Length / Mathf.Max(1, Closed ? _pos.Length : _pos.Length - 1);

        public SkimRoute(IReadOnlyList<SkimRoutePrism> prisms, bool closed)
        {
            if (prisms == null || prisms.Count < 2)
                throw new ArgumentException("A route needs at least two prisms.", nameof(prisms));

            int n = prisms.Count;
            _pos = new Vector3[n];
            _fwd = new Vector3[n];
            _right = new Vector3[n];
            _up = new Vector3[n];
            _semi = new Vector3[n];
            _start = new float[n];
            _seg = new float[n];
            Closed = closed;

            for (int i = 0; i < n; i++)
            {
                var p = prisms[i];
                _pos[i] = p.Position;
                _fwd[i] = p.Rotation * Vector3.forward;
                _right[i] = p.Rotation * Vector3.right;
                _up[i] = p.Rotation * Vector3.up;
                _semi[i] = p.ShellSemiAxes;
            }

            float s = 0f;
            for (int i = 0; i < n; i++)
            {
                _start[i] = s;
                bool last = i == n - 1;
                _seg[i] = last && !closed ? 0f : Vector3.Distance(_pos[i], _pos[last ? 0 : i + 1]);
                s += _seg[i];
            }
            Length = Mathf.Max(1e-3f, s);
        }

        /// <summary>Arc position folded into one lap, [0, Length).</summary>
        public float Wrap(float s)
        {
            if (!Closed) return Mathf.Clamp(s, 0f, Length);
            float w = s - Mathf.Floor(s / Length) * Length;
            return w >= Length ? 0f : w;
        }

        /// <summary>The prism whose segment contains lap-local arc <paramref name="local"/>, and how
        /// far along that segment (0..1).</summary>
        int Locate(float local, out float t)
        {
            int lo = 0, hi = _pos.Length - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) >> 1;
                if (_start[mid] <= local) lo = mid; else hi = mid - 1;
            }
            t = _seg[lo] > 1e-6f ? Mathf.Clamp01((local - _start[lo]) / _seg[lo]) : 0f;
            return lo;
        }

        int Next(int i) => i + 1 < _pos.Length ? i + 1 : (Closed ? 0 : i);

        /// <summary>Centre-line point, travel direction and the plates' own frame at arc
        /// <paramref name="s"/>. <paramref name="right"/> x <paramref name="up"/> x
        /// <paramref name="tangent"/> is right-handed in the same sense Unity's LookRotation is
        /// (right = up x forward), so an offset expressed in it lands where the plates put it.</summary>
        public void Frame(float s, out Vector3 position, out Vector3 tangent, out Vector3 right, out Vector3 up)
        {
            int i = Locate(Wrap(s), out float t);
            int j = Next(i);
            position = Vector3.LerpUnclamped(_pos[i], _pos[j], t);
            tangent = Vector3.LerpUnclamped(_fwd[i], _fwd[j], t).normalized;
            if (tangent.sqrMagnitude < 0.5f) tangent = _fwd[i];
            up = Vector3.LerpUnclamped(_up[i], _up[j], t);
            up = (up - tangent * Vector3.Dot(up, tangent)).normalized;
            if (up.sqrMagnitude < 0.5f) up = _up[i];
            right = Vector3.Cross(up, tangent).normalized;
        }

        public Vector3 Position(float s)
        {
            int i = Locate(Wrap(s), out float t);
            return Vector3.LerpUnclamped(_pos[i], _pos[Next(i)], t);
        }

        /// <summary>
        /// Half-width and half-thickness of the ribbon's envelope around arc <paramref name="s"/>:
        /// the largest shell semi-axes of every prism whose along-track reach, plus
        /// <paramref name="alongPad"/>, covers that point. The pad is how a hull's LENGTH enters a
        /// clearance question that is otherwise asked of a cross-section — a waypoint marker is twice
        /// a plate in every axis, and the hull is over it for its whole length, not for an instant.
        /// </summary>
        public void Envelope(float s, float alongPad, out float halfWidth, out float halfHeight)
        {
            float local = Wrap(s);
            int i = Locate(local, out _);
            halfWidth = 0f;
            halfHeight = 0f;
            int n = _pos.Length;
            for (int k = -2; k <= 3; k++)
            {
                int idx = i + k;
                if (Closed) idx = ((idx % n) + n) % n;
                else if (idx < 0 || idx >= n) continue;

                float d = Mathf.Abs(local - _start[idx]);
                if (Closed) d = Mathf.Min(d, Length - d);
                if (d > _semi[idx].z + alongPad) continue;
                if (_semi[idx].x > halfWidth) halfWidth = _semi[idx].x;
                if (_semi[idx].y > halfHeight) halfHeight = _semi[idx].y;
            }
        }

        /// <summary>
        /// Arc position of the closest centre-line point to <paramref name="p"/>, searched only in
        /// <c>[hint - back, hint + ahead]</c> and returned UNWRAPPED in the hint's lap. A local search
        /// is not an optimisation: a closed ribbon passes close to itself (the intensity-2 knot comes
        /// within a few hundred units of its own far side), and a global nearest point would teleport
        /// the racer's idea of where it is onto the wrong strand.
        /// </summary>
        public float Project(Vector3 p, float hint, float back, float ahead)
        {
            int n = _pos.Length;
            int u0 = UnwrappedIndex(hint - back);
            int u1 = UnwrappedIndex(hint + ahead) + 1;
            float bestD = float.MaxValue;
            float bestS = hint;
            for (int u = u0; u <= u1; u++)
            {
                int lap = FloorDiv(u, n);
                int j = u - lap * n;
                if (!Closed && (j < 0 || j >= n - 1)) continue;
                int k = Next(j);
                Vector3 a = _pos[j], ab = _pos[k] - a;
                float len2 = ab.sqrMagnitude;
                float t = len2 > 1e-8f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / len2) : 0f;
                float d = (a + ab * t - p).sqrMagnitude;
                if (d < bestD)
                {
                    bestD = d;
                    bestS = lap * Length + _start[j] + t * _seg[j];
                }
            }
            return bestS;
        }

        /// <summary>Closest centre-line arc to <paramref name="p"/> over the WHOLE ribbon, in
        /// [0, Length). For picking up the racer once, at spawn — never per frame (see
        /// <see cref="Project"/>).</summary>
        public float ProjectGlobal(Vector3 p)
        {
            int n = _pos.Length;
            float bestD = float.MaxValue, bestS = 0f;
            int segs = Closed ? n : n - 1;
            for (int j = 0; j < segs; j++)
            {
                int k = Next(j);
                Vector3 a = _pos[j], ab = _pos[k] - a;
                float len2 = ab.sqrMagnitude;
                float t = len2 > 1e-8f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / len2) : 0f;
                float d = (a + ab * t - p).sqrMagnitude;
                if (d < bestD) { bestD = d; bestS = _start[j] + t * _seg[j]; }
            }
            return bestS;
        }

        int UnwrappedIndex(float s)
        {
            if (!Closed) return Locate(Mathf.Clamp(s, 0f, Length), out _);
            int lap = Mathf.FloorToInt(s / Length);
            return lap * _pos.Length + Locate(s - lap * Length, out _);
        }

        static int FloorDiv(int a, int b) => a >= 0 ? a / b : -((-a + b - 1) / b);
    }
}
