using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Shrike's sling for an AUTOPILOT in Regatta, after Thornline's aim (<c>ThornlineAIPlan</c>). The Shrike cruises
    /// at 18 u/s; its speed is the Void Sling, which tethers to the first block on its axis within the sling's range and
    /// flies there in one second, docking on it. Its own autopilot slings at "a far target within 20 degrees" and holds
    /// the lance whenever two rival blocks sit on its axis, so on Regatta's braided rails it lanced and crawled
    /// (11-17 of 24 gates in 400 s, 2026-11-19).
    ///
    /// <para>This coach picks a block just PAST the next ring whose chord from the hull threads the ring's mouth (any
    /// block: a rail, a mouth prism, a course feature) and points the nose at it. While it coaches a hull, the hull's
    /// autopilot stays in Time mode and slings only at an anchor that threads the ring
    /// (<see cref="ShrikeExecutor.AiSlingAccept"/>). When the ring is out of the sling's reach, or nothing past it can
    /// be reached through the mouth, it takes the block that brings the hull nearest the ring, short of its plane.</para>
    ///
    /// <para>Inputs only: the aim is a steering point AIPilot flies at, the press is the hull's own autopilot through
    /// its action handler. The kit and its numbers are untouched, and a hull with no <see cref="ShrikeExecutor"/> is
    /// never touched.</para>
    /// </summary>
    [Serializable]
    public sealed class RegattaShrikeSling
    {
        [Tooltip("How far past the next ring's plane (u) the coach looks for a block to sling to. Regatta's legs run " +
                 "461-858 u, so this stays well short of the ring after.")]
        [Min(10f)] public float LookPast = 220f;

        [Tooltip("When nothing within LookPast can be reached through the mouth, the coach looks this far (u) past the plane.")]
        [Min(10f)] public float LookPastFar = 700f;

        [Tooltip("Two rings for one reload only when the nose is already this close (degrees) to the line: a long, fine aim " +
                 "costs more than the reload it saves.")]
        [Range(0f, 180f)] public float SkipMaxDegrees = 15f;

        [Tooltip("Score bonus (u) for a super-shielded anchor: docked on one the sling reloads x2.5 instead of x1.6.")]
        [Min(0f)] public float ShieldedBonus = 150f;

        [Tooltip("Boxed in on a block: a sling out must reach at least this far (u).")]
        [Min(0f)] public float EscapeDistance = 150f;

        [Tooltip("Boxed in on a block: a sling out may lose at most this much ground (u) on the ring.")]
        [Min(0f)] public float EscapeLoss = 300f;

        [Tooltip("A sling's chord must cross the ring's plane at least this far (u) inside the mouth's edge, on top of " +
                 "the hull's radius.")]
        [Min(0f)] public float MouthMargin = 6f;

        [Tooltip("Short of the ring: a sling must bring the hull at least this much (u) nearer the ring to be worth a " +
                 "reload.")]
        [Min(0f)] public float MinProgress = 120f;

        [Tooltip("Short of the ring: how far either side of the line to the ring (u) the coach looks for a block.")]
        [Min(10f)] public float FallbackWidth = 500f;

        [Tooltip("Seconds between the coach's picks for one pilot (the pick looks along up to MaxChecks lines).")]
        [Min(0.02f)] public float RepickSeconds = 0.2f;

        [Tooltip("How many of the best-scored blocks the coach checks for a clear line each pick.")]
        [Min(1)] public int MaxChecks = 16;

        // Blocks this near the hull are the one it docks on and its neighbours.
        const float MinChord = 40f;

        sealed class PilotState
        {
            public ShrikeExecutor Executor;
            public Transform HullTransform;
            public Func<Vector3, bool> Accept;
            public RaceGate Gate, After;
            public bool HasGate, HasAfter, Through, Escape;
            public Vector3 Hull;
            public int GateIndex = -1;
            public float NextPick;
            public bool HasTarget;
            public Vector3 Target;
        }

        readonly Dictionary<IPlayer, PilotState> _pilots = new();
        readonly Dictionary<Transform, ShrikeExecutor> _executors = new();
        readonly List<Prism> _found = new();
        readonly List<(float score, Vector3 point)> _scored = new();
        readonly List<Vector3> _tried = new();
        const float CheckSpacing = 30f;

        /// <summary>
        /// The steering point for an AI Shrike bound for <paramref name="gate"/> (the <paramref name="gateIndex"/>-th
        /// threading); false for any other hull or a human, and when no block helps (the race line then flies it).
        /// </summary>
        public bool TryAim(IPlayer pilot, IVesselStatus status, RaceGate gate, int gateIndex, RaceGate? after, out Vector3 target)
        {
            target = default;
            // TryGetComponent, not VesselStatus.AIPilot: that getter ADDS an autopilot to a human's hull.
            if (pilot == null || status == null || !status.Transform || status is not Component statusComponent ||
                !statusComponent.TryGetComponent(out AIPilot autopilot) || !autopilot.AutoPilotEnabled) { Release(pilot); return false; }
            var executor = ExecutorOf(status.Transform);
            if (!executor || !executor.Config) { Release(pilot); return false; }

            if (!_pilots.TryGetValue(pilot, out var state))
            {
                state = new PilotState { Executor = executor };
                state.Accept = point => Accepts(state, point);
                _pilots[pilot] = state;
            }
            state.Executor = executor;
            state.HullTransform = status.Transform;

            if (gateIndex != state.GateIndex)
            {
                state.GateIndex = gateIndex;
                state.NextPick = 0f;
            }
            state.Gate = gate;
            state.HasAfter = after.HasValue;
            if (after.HasValue) state.After = after.Value;
            state.HasGate = true;
            state.Hull = status.Transform.position;

            float now = Time.time;
            if (now >= state.NextPick || !state.HasTarget)
            {
                state.NextPick = now + RepickSeconds;
                state.HasTarget = Pick(state, executor, out state.Target);
            }
            // Nothing worth a sling from here (boxed in on a block, every line blocked): hand the hull back to its own
            // autopilot until there is, so its lance clears the way and its own sling rule gets it moving.
            executor.AiSlingAccept = state.HasTarget ? state.Accept : null;
            target = state.Target;
            return state.HasTarget;
        }

        /// <summary>
        /// The block to point the nose at: the best-scored blocks past the ring that thread its mouth, each checked with
        /// the hull's own sling rule (<see cref="ShrikeExecutor.PreviewSlingAnchor"/>) so a nearer block on the same line
        /// never hides it; else the reachable block that brings the hull nearest the ring, short of its plane.
        /// </summary>
        bool Pick(PilotState state, ShrikeExecutor executor, out Vector3 target)
        {
            target = default;
            var index = PrismSpatialIndex.Instance;
            if (!index || !index.IsAvailable) return false;
            var gate = state.Gate;
            float reach = executor.Config.SlingRange - 10f;

            // Two rings for one reload: a block just past the ring after next, through both mouths.
            if (state.HasAfter)
            {
                var after = state.After;
                _scored.Clear();
                index.QuerySegment(after.Position, after.Position + after.Axis * LookPast, after.Radius + 40f, _found);
                for (int i = 0; i < _found.Count; i++)
                {
                    var prism = _found[i];
                    if (!prism || prism.destroyed) continue;
                    Vector3 c = prism.transform.position;
                    float d = (c - state.Hull).magnitude;
                    if (d < MinChord || d > reach || !ThreadsBoth(state, c)) continue;
                    if (Vector3.Angle(state.HullTransform.forward, c - state.Hull) > SkipMaxDegrees) continue;
                    Threads(state, after, c, out float offset, out float past);
                    _scored.Add((past - 2f * offset, c));
                }
                state.Through = TakeFirstVisible(state, executor, out target, Aim.Through);
                if (state.Through) return true;
            }

            // Just past the mouth first; if nothing there can be reached through it, further down the next leg.
            for (int pass = 0; pass < 2; pass++)
            {
                float from = pass == 0 ? 0f : LookPast, to = pass == 0 ? LookPast : LookPastFar;
                _scored.Clear();
                index.QuerySegment(gate.Position + gate.Axis * from, gate.Position + gate.Axis * to, gate.Radius + 40f, _found);
                for (int i = 0; i < _found.Count; i++)
                {
                    var prism = _found[i];
                    if (!prism || prism.destroyed) continue;
                    Vector3 c = prism.transform.position;
                    float d = (c - state.Hull).magnitude;
                    if (d < MinChord || d > reach) continue;
                    if (!Threads(state, c, out float offset, out float past)) continue;
                    _scored.Add((pass == 0 ? past - 2f * offset : -past - 2f * offset, c));
                }
                state.Through = TakeFirstVisible(state, executor, out target, Aim.Through);
                if (state.Through) return true;
            }

            _scored.Clear();
            Vector3 toGate = gate.Position - state.Hull;
            float gap = toGate.magnitude;
            Vector3 far = state.Hull + toGate * (Mathf.Min(reach, gap) / Mathf.Max(1f, gap));
            index.QuerySegment(state.Hull, far, FallbackWidth, _found);
            for (int i = 0; i < _found.Count; i++)
            {
                var prism = _found[i];
                if (!prism || prism.destroyed) continue;
                Vector3 c = prism.transform.position;
                float d = (c - state.Hull).magnitude;
                if (d < MinChord || d > reach || !ShortOfRing(state, c, out float progress)) continue;
                _scored.Add((progress, c));
            }
            state.Escape = false;
            if (TakeFirstVisible(state, executor, out target, Aim.Short)) return true;

            // Boxed in (docked in a cluster whose every short line catches a neighbour): any block well clear of the
            // cluster that does not lose much ground, then on from there.
            if (!executor.Docked) return false;
            _scored.Clear();
            index.QuerySegment(state.Hull, far, FallbackWidth, _found);
            for (int i = 0; i < _found.Count; i++)
            {
                var prism = _found[i];
                if (!prism || prism.destroyed) continue;
                Vector3 c = prism.transform.position;
                float d = (c - state.Hull).magnitude;
                if (d < EscapeDistance || d > reach || !Escapes(state, c, out float progress)) continue;
                _scored.Add((progress, c));
            }
            state.Escape = TakeFirstVisible(state, executor, out target, Aim.Escape);
            return state.Escape;
        }

        enum Aim { Through, Short, Escape }

        bool Escapes(PilotState state, Vector3 point, out float progress)
        {
            var gate = state.Gate;
            progress = (state.Hull - gate.Position).magnitude - (point - gate.Position).magnitude;
            return Vector3.Dot(point - gate.Position, gate.Axis) < 0f && (point - state.Hull).magnitude >= EscapeDistance &&
                   progress >= -EscapeLoss;
        }

        /// <summary>Of the best-scored candidates, the one whose sling would really anchor where the coach accepts,
        /// favouring a super-shielded anchor (the sling reloads faster docked on one).</summary>
        bool TakeFirstVisible(PilotState state, ShrikeExecutor executor, out Vector3 target, Aim aim)
        {
            target = default;
            _found.Clear();
            _scored.Sort((x, y) => y.score.CompareTo(x.score));
            _tried.Clear();
            bool any = false;
            float best = float.MinValue;
            for (int i = 0; i < _scored.Count && _tried.Count < MaxChecks; i++)
            {
                var (score, c) = _scored[i];
                // Spread the checks: a block beside one already tried is usually hidden by the same block.
                bool near = false;
                for (int k = 0; k < _tried.Count && !near; k++) near = (_tried[k] - c).sqrMagnitude < CheckSpacing * CheckSpacing;
                if (near) continue;
                _tried.Add(c);
                if (!executor.PreviewSlingAnchor(c - state.Hull, out var anchor, out var hit)) continue;
                bool ok = aim switch
                {
                    Aim.Through => Threads(state, hit, out _, out _),
                    Aim.Short => ShortOfRing(state, hit, out _),
                    _ => Escapes(state, hit, out _),
                };
                if (!ok) continue;
                if (anchor && anchor.prismProperties is { IsSuperShielded: true }) score += ShieldedBonus;
                if (score <= best) continue;
                best = score;
                target = hit;
                any = true;
            }
            return any;
        }

        /// <summary>Hand every coached hull back to its own autopilot (the whistle, a despawn).</summary>
        public void ReleaseAll()
        {
            foreach (var state in _pilots.Values)
                if (state.Executor) state.Executor.AiSlingAccept = null;
            _pilots.Clear();
            _executors.Clear();
        }

        void Release(IPlayer pilot)
        {
            if (pilot == null || !_pilots.TryGetValue(pilot, out var state)) return;
            if (state.Executor) state.Executor.AiSlingAccept = null;
            _pilots.Remove(pilot);
        }

        /// <summary>The autopilot's question: is a sling to <paramref name="point"/> worth the reload?</summary>
        bool Accepts(PilotState state, Vector3 point)
        {
            if (!state.HasGate || !state.Executor || !state.HullTransform) return false;
            state.Hull = state.HullTransform.position;
            if (Threads(state, point, out _, out _)) return true;
            if (state.Through) return false;
            return ShortOfRing(state, point, out _) || state.Escape && Escapes(state, point, out _);
        }

        /// <summary>Does the straight flight from the hull to <paramref name="point"/> cross the ring's plane inside its
        /// mouth? <paramref name="past"/>: how far past the plane the point lies.</summary>
        bool Threads(PilotState state, Vector3 point, out float offset, out float past) =>
            Threads(state, state.Gate, point, out offset, out past);

        bool Threads(PilotState state, RaceGate gate, Vector3 point, out float offset, out float past)
        {
            offset = past = 0f;
            float h = Vector3.Dot(state.Hull - gate.Position, gate.Axis);
            past = Vector3.Dot(point - gate.Position, gate.Axis);
            if (h >= 0f || past <= 0f) return false;
            float t = h / (h - past);
            Vector3 cross = Vector3.Lerp(state.Hull, point, t);
            offset = (cross - gate.Position).magnitude;
            float hullRadius = state.Executor && state.Executor.Config ? state.Executor.Config.HullRadius : 0f;
            return offset <= gate.Radius - hullRadius - MouthMargin;
        }

        /// <summary>Through the next ring AND the one after it: one reload for two rings.</summary>
        bool ThreadsBoth(PilotState state, Vector3 point) =>
            state.HasAfter && Threads(state, point, out _, out _) && Threads(state, state.After, point, out _, out _);

        /// <summary>Does <paramref name="point"/> lie short of the ring's plane and bring the hull nearer the ring?</summary>
        bool ShortOfRing(PilotState state, Vector3 point, out float progress)
        {
            var gate = state.Gate;
            float gap = (state.Hull - gate.Position).magnitude;
            progress = gap - (point - gate.Position).magnitude;
            return Vector3.Dot(point - gate.Position, gate.Axis) < 0f && progress >= Mathf.Min(MinProgress, 0.4f * gap);
        }

        ShrikeExecutor ExecutorOf(Transform hull)
        {
            if (!hull) return null;
            if (_executors.TryGetValue(hull, out var cached) && cached) return cached;
            var found = hull.GetComponentInChildren<ShrikeExecutor>(true);
            _executors[hull] = found;
            return found;
        }
    }
}
