using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// One Scarab Scramble scoring hoop: a domain-neutral ring any live ball can thread from
    /// EITHER direction. Built at runtime by <see cref="ScarabScrambleController"/> on every
    /// peer (the visual is per-peer local, like the Astro League arena); detection runs on the
    /// SERVER only and reports to the controller, which owns attribution and scoring.
    ///
    /// Detection is the shipped segment-crossing math (the shape <c>AstroLeagueGoal</c> and
    /// <c>ScarabSwitch</c> both use) run against <see cref="AstroLeagueBall.Live"/> — a
    /// crossing of the mouth plane whose intersection lands inside the mouth radius. Per-ball
    /// last positions live in a dictionary because this is a MULTI-ball mode: the scalar
    /// last-pos state that was fine for Astro League's single match ball is exactly what
    /// SCARAB.md §4.5.3 flags as meaningless with a population.
    ///
    /// A teleport guard drops any segment longer than the ball could have flown in one tick
    /// (a fresh forge popping into existence across the arena must not read as a crossing).
    /// </summary>
    public class ScarabScrambleHoop : MonoBehaviour
    {
        ScarabScrambleController _controller;
        Vector3 _axis = Vector3.forward;
        float _mouthRadius = 50f;
        bool _serverDetects;

        GameObject _ring;
        Renderer _ringRenderer;
        MaterialPropertyBlock _mpb;
        Color _restColor;
        float _flareUntil;
        float _flareSeconds = 1.2f;
        bool _flaring;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        readonly Dictionary<AstroLeagueBall, Vector3> _lastBallPos = new();
        static readonly List<AstroLeagueBall> s_stale = new();

        public Vector3 Axis => _axis;
        public float MouthRadius => _mouthRadius;

        /// <summary>Build the hoop at its pose. Runs on every peer; only the server detects.</summary>
        public void Configure(ScarabScrambleController controller, Vector3 axis,
                              float mouthRadius, Color accent, float flareSeconds,
                              bool serverDetects)
        {
            _controller = controller;
            _axis = axis.sqrMagnitude > 1e-6f ? axis.normalized : Vector3.forward;
            _mouthRadius = Mathf.Max(1f, mouthRadius);
            _flareSeconds = Mathf.Max(0.1f, flareSeconds);
            _serverDetects = serverDetects;
            _restColor = accent;

            // The ring body is built in the parent's local XY plane facing local +Z, so the
            // root must FACE the detection axis or the visual and the scoring plane disagree —
            // a hoop you'd thread edge-on. Up-hint swaps away from world-up for the central
            // (axis = up) layout, where LookRotation's default hint degenerates.
            Vector3 upHint = Mathf.Abs(Vector3.Dot(_axis, Vector3.up)) > 0.99f
                ? Vector3.forward
                : Vector3.up;
            transform.rotation = Quaternion.LookRotation(_axis, upHint);

            // The toy shape language: a hoop IS "a portal you thread" (the same ring the
            // Scarab's own switches and the freestyle gates wear), so a brand-new player who
            // has flown the menu already knows what to do with it.
            if (_ring == null)
                _ring = ToyFactory.AddRingBody(transform, _mouthRadius, accent);
            if (_ringRenderer == null && _ring != null)
                _ringRenderer = _ring.GetComponentInChildren<Renderer>();
            _mpb ??= new MaterialPropertyBlock();

            // Continuity of existence: the hoop BLOOMS in rather than popping into the court.
            // Photons only — detection runs at the full mouth radius from frame one (gameplay
            // state goes final at the start; the visual grows into it, the platform shape).
            transform.localScale = Vector3.zero;
            _bloomTimer = BloomSeconds;
        }

        const float BloomSeconds = 0.9f;
        float _bloomTimer;

        void UpdateBloom()
        {
            if (_bloomTimer <= 0f) return;
            _bloomTimer -= Time.deltaTime;
            float u = 1f - Mathf.Clamp01(_bloomTimer / BloomSeconds);
            float s = u * u * (3f - 2f * u); // smoothstep — settles without a snap
            transform.localScale = Vector3.one * Mathf.Max(0.001f, s);
        }

        /// <summary>Every peer: flare the ring after a goal (feedback, not state).</summary>
        public void Flare()
        {
            _flareUntil = Time.time + _flareSeconds;
            _flaring = true;
        }

        void Update()
        {
            UpdateBloom();
            UpdateFlare();
            if (_serverDetects) DetectCrossings();
        }

        void UpdateFlare()
        {
            // ToyFactory's accent materials are SHARED per colour ("nothing mutates these"),
            // so the flare rides a MaterialPropertyBlock — per-hoop tint, no material writes.
            if (!_flaring || _ringRenderer == null) return;

            float t = Mathf.Clamp01((_flareUntil - Time.time) / _flareSeconds);
            _mpb.SetColor(BaseColorId, Color.Lerp(_restColor, Color.white, t * t));
            _ringRenderer.SetPropertyBlock(_mpb);

            if (t <= 0f) _flaring = false; // settled back at rest — stop writing
        }

        void DetectCrossings()
        {
            if (_controller == null) return;

            var live = AstroLeagueBall.Live;
            for (int i = 0; i < live.Count; i++)
            {
                var ball = live[i];
                if (ball == null) continue;
                if (ball.IsHidden || ball.IsFrozen)
                {
                    _lastBallPos.Remove(ball);
                    continue;
                }

                Vector3 cur = ball.transform.position;
                if (!_lastBallPos.TryGetValue(ball, out var prev))
                {
                    _lastBallPos[ball] = cur;
                    continue; // two samples make a segment
                }
                _lastBallPos[ball] = cur;

                // Teleport guard: a segment longer than the ball's own top speed allows in a
                // frame is a spawn/reset jump, not flight (the AstroLeagueGoal guard).
                float maxStep = ball.MaxSpeed * Time.deltaTime * 2f + 5f;
                if ((cur - prev).sqrMagnitude > maxStep * maxStep) continue;

                if (!CrossedMouth(prev, cur)) continue;

                _controller.HandleHoopThreadedServer(this, ball);
                // The ball is spent by the handler; its dictionary entry falls out via the
                // stale sweep below once it despawns.
            }

            PruneStale(live);
        }

        /// <summary>Did the segment prev→cur cross the hoop plane INSIDE the mouth? Direction
        /// agnostic — threading a hoop backwards is still threading it.</summary>
        bool CrossedMouth(Vector3 prev, Vector3 cur)
        {
            Vector3 c = transform.position;
            float dPrev = Vector3.Dot(prev - c, _axis);
            float dCur = Vector3.Dot(cur - c, _axis);
            if (dPrev * dCur > 0f) return false;   // both samples on one side — no crossing
            if (Mathf.Approximately(dPrev, dCur)) return false;

            float t = Mathf.Clamp01(dPrev / (dPrev - dCur));
            Vector3 hit = Vector3.Lerp(prev, cur, t);
            Vector3 rel = hit - c;
            Vector3 lateral = rel - Vector3.Dot(rel, _axis) * _axis;
            return lateral.sqrMagnitude <= _mouthRadius * _mouthRadius;
        }

        void PruneStale(List<AstroLeagueBall> live)
        {
            if (_lastBallPos.Count == 0) return;
            s_stale.Clear();
            foreach (var kv in _lastBallPos)
                if (kv.Key == null || !live.Contains(kv.Key))
                    s_stale.Add(kv.Key);
            for (int i = 0; i < s_stale.Count; i++)
                _lastBallPos.Remove(s_stale[i]);
        }

    }
}
