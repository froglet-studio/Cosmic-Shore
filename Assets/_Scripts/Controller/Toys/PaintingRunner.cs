using System;
using System.Collections.Generic;
using System.Threading;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The multi-stroke "fly by numbers" runner. A painting is a world-anchored monument-in-progress:
    /// every stroke renders as a ghost line tinted its domain colour, the current stroke opens with a
    /// <b>start gate</b> (a ring the vessel flies through, which requests the stroke's domain via the
    /// server-authoritative pick RPC so the trail recolours), and the vessel's own trail paints each
    /// stroke point-to-point. Between strokes the trail spawner is pen-up'd inside the painting's
    /// "studio zone" so transit flight never scribbles across the artwork — and it is ALWAYS restored
    /// when the player leaves the zone, exits freestyle, benches the run, or the runner dies.
    ///
    /// Toy-faithful: no score, no timer, no fail state. Progress is stroke-granular, resumable
    /// in-session and across sessions (<see cref="PaintingProgressStore"/>), and finishing simply
    /// celebrates and offers a fresh canvas. The painted prisms are conserved mass like any trail.
    /// </summary>
    public class PaintingRunner : MonoBehaviour
    {
        enum RunPhase { AwaitingGate = 0, Painting = 1, Celebrating = 2 }
        enum GhostStyle { Pending = 0, Active = 1, Done = 2 }

        /// <summary>Raised on stroke completion, bench toggles, and celebration — drives the toy's label.</summary>
        public event Action ProgressChanged;

        /// <summary>Raised once, just before the runner destroys itself after the celebration.</summary>
        public event Action Finished;

        const float BloomSeconds = 1.4f;
        const float GateDespawnSeconds = 0.45f;
        const float MarkerPulseHz = 1.4f;

        /// <summary>Everything the runner knows about one stroke — one array, no index juggling.</summary>
        class StrokeInfo
        {
            public Vector3[] Points;      // world space
            public string Name;
            public Domains Domain;
            public float Reach;           // adaptive advance distance
            public Color BaseColor;       // resolved once at Begin — no per-frame theme lookups
            public LineRenderer Ghost;
            public GhostStyle Style;
        }

        ToyContext _context;
        ToyDefinitionSO _toyDefinition;
        PaintingDefinitionSO _painting;
        Quaternion _rotation;

        StrokeInfo[] _strokes;
        float _bloom;                  // 0→1 on begin; swells then falls to 0 during the farewell fade

        int _strokeIndex;              // == strokes completed while AwaitingGate
        int _pointIndex;
        RunPhase _phase;
        bool _benched;
        bool _wasEngaged;

        GameObject _gate;
        bool _gateBenchEasing;
        GameObject _marker;
        Renderer _markerRenderer;
        bool _markerHiding;
        LineRenderer _guide;
        float _markerBaseRadius;

        Vector3 _zoneCenter;
        float _zoneSqrRadius;

        VesselPrismController _pennedController;

        public bool IsCelebrating => _phase == RunPhase.Celebrating;
        public bool IsBenched => _benched;
        public int StrokesCompleted => Mathf.Min(_strokeIndex, StrokeCount);
        public int StrokeCount => _strokes?.Length ?? 0;

        StrokeInfo CurrentStroke => _strokes[_strokeIndex];

        /// <summary>
        /// Build the painting at a world anchor and start (or resume) the run.
        /// <paramref name="resumeFromStroke"/> strokes are shown as already done.
        /// </summary>
        public void Begin(PaintingDefinitionSO painting, ToyDefinitionSO toyDefinition, ToyContext context,
            Vector3 origin, Quaternion rotation, int resumeFromStroke)
        {
            _painting = painting;
            _toyDefinition = toyDefinition;
            _context = context;
            _rotation = rotation;

            // PaintingDefinitionSO.Strokes is already filtered to drawable strokes — using it
            // verbatim keeps this count identical to the one PaintingToy and the progress store see.
            var source = painting.Strokes;
            _strokes = new StrokeInfo[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                var s = source[i];
                var world = new Vector3[s.points.Count];
                for (int p = 0; p < s.points.Count; p++)
                    world[p] = origin + rotation * s.points[p];

                // Per-stroke reach: tighten on fine detail so a tight balcony ring can't be
                // cleared from its centre, keep the authored threshold on broad strokes.
                float minSeg = float.MaxValue;
                for (int p = 1; p < world.Length; p++)
                    minSeg = Mathf.Min(minSeg, Vector3.Distance(world[p - 1], world[p]));

                _strokes[i] = new StrokeInfo
                {
                    Points = world,
                    Name = string.IsNullOrEmpty(s.name) ? $"Stroke {i + 1}" : s.name,
                    Domain = s.domain,
                    Reach = Mathf.Clamp(minSeg * 0.7f, 4f, _painting.ReachThreshold),
                    BaseColor = ToyFactory.DomainAccentColor(context, s.domain),
                };
            }

            if (_strokes.Length == 0)
            {
                CSDebug.LogWarning($"[PaintingRunner] '{painting.DisplayName}' has no drawable strokes.");
                Destroy(gameObject);
                return;
            }

            // Studio zone: pen-up between strokes applies only inside this sphere.
            Bounds local = painting.LocalBounds;
            _zoneCenter = origin + rotation * local.center;
            float zoneRadius = local.extents.magnitude * 1.3f + 120f;
            _zoneSqrRadius = zoneRadius * zoneRadius;

            // Ghost blueprint: one line per stroke, tinted its domain.
            _strokeIndex = Mathf.Clamp(resumeFromStroke, 0, _strokes.Length - 1);
            if (resumeFromStroke >= _strokes.Length) _strokeIndex = 0; // stale "complete" state — fresh canvas
            for (int i = 0; i < _strokes.Length; i++)
            {
                var ghost = ToyFactory.CreateLine($"Ghost_{i}", transform, 1f, true);
                ghost.positionCount = _strokes[i].Points.Length;
                ghost.SetPositions(_strokes[i].Points);
                _strokes[i].Ghost = ghost;
                _strokes[i].Style = i < _strokeIndex ? GhostStyle.Done : GhostStyle.Pending;
            }
            _strokes[_strokeIndex].Style = GhostStyle.Active;

            _guide = ToyFactory.CreateLine("Guide", transform, 0.9f, true);
            _guide.positionCount = 2;
            _guide.enabled = false;

            _phase = RunPhase.AwaitingGate;
            _pointIndex = 0;
            SpawnGate(_strokeIndex);

            _bloom = 0f;
            ApplyAllGhostStyles();
            BenchOtherRunners(); // a fresh run takes the brush
            BloomIn(this.GetCancellationTokenOnDestroy()).Forget();
        }

        /// <summary>Put the brush down / pick it back up (the toy toggles this on re-activation).</summary>
        public void ToggleBench() => SetBenched(!_benched);

        public void SetBenched(bool benched)
        {
            if (_phase == RunPhase.Celebrating || _benched == benched) return;
            _benched = benched;
            if (benched)
            {
                // Release the pen NOW — waiting for this runner's next Update could clobber a
                // pause another runner (the new brush holder) sets in the meantime.
                RestorePen();
                _gateBenchEasing = true;
            }
            else
            {
                BenchOtherRunners();
            }
            ProgressChanged?.Invoke();
        }

        /// <summary>
        /// Only one painting run holds the brush at a time — neighbouring studio zones can overlap,
        /// and two engaged runners would fight over the trail spawner's pen state.
        /// </summary>
        void BenchOtherRunners()
        {
            foreach (var other in FindObjectsByType<PaintingRunner>(FindObjectsSortMode.None))
                if (other != this && !other.IsBenched && !other.IsCelebrating)
                    other.SetBenched(true);
        }

        void Update()
        {
            EaseTransitions();

            if (_phase == RunPhase.Celebrating || _strokes == null) return;

            var vessel = ResolveVessel();
            bool freestyle = _context?.IsFreestyleActive == null || _context.IsFreestyleActive();
            Vector3 shipPos = vessel != null ? vessel.Transform.position : Vector3.zero;
            bool inZone = vessel != null && (shipPos - _zoneCenter).sqrMagnitude <= _zoneSqrRadius;
            bool engaged = vessel != null && freestyle && inZone && !_benched;

            ApplyPen(vessel, engaged);

            // Re-engaging mid-stroke (back from a bench, a menu trip, or a detour through the
            // Domain Changer) re-asserts the stroke's colour — the gate only fired once.
            if (engaged && !_wasEngaged && _phase == RunPhase.Painting)
                RequestStrokeDomain(CurrentStroke.Domain);
            _wasEngaged = engaged;

            if (!engaged)
            {
                if (_guide) _guide.enabled = false;
                return;
            }

            if (_phase == RunPhase.Painting)
                UpdatePainting(shipPos);
            else
                UpdateAwaitingGate(shipPos);

            PulseMarker();
        }

        /// <summary>
        /// Continuity-law eases that must run even while benched/disengaged: the gate shrinks
        /// away and regrows on bench toggles, and the marker shrinks out instead of blinking off.
        /// </summary>
        void EaseTransitions()
        {
            if (_gate && _gateBenchEasing)
            {
                var t = _gate.transform;
                Vector3 target = _benched ? Vector3.zero : Vector3.one;
                t.localScale = Vector3.Lerp(t.localScale, target, Time.deltaTime * 7f);
                if (!_benched && (t.localScale - target).sqrMagnitude < 1e-4f)
                {
                    t.localScale = target;
                    _gateBenchEasing = false;
                }
            }

            if (_markerHiding && _marker && _marker.activeSelf)
            {
                var t = _marker.transform;
                t.localScale = Vector3.Lerp(t.localScale, Vector3.zero, Time.deltaTime * 9f);
                if (t.localScale.x < 0.05f)
                {
                    _marker.SetActive(false);
                    _markerHiding = false;
                }
            }
        }

        void OnDestroy()
        {
            // The one hard rule: never leave the player's trail spawner paused behind us.
            RestorePen();
        }

        // ── Phase updates ────────────────────────────────────────────────────

        void UpdateAwaitingGate(Vector3 shipPos)
        {
            if (!_gate) return;
            DrawGuide(shipPos, _gate.transform.position, 0.35f);
        }

        void UpdatePainting(Vector3 shipPos)
        {
            var stroke = CurrentStroke;
            Vector3 target = stroke.Points[_pointIndex];

            DrawGuide(shipPos, target, 0.6f);

            if ((shipPos - target).sqrMagnitude > stroke.Reach * stroke.Reach) return;

            _pointIndex++;
            if (_pointIndex >= stroke.Points.Length)
            {
                CompleteStroke();
                return;
            }
            MoveMarker(stroke.Points[_pointIndex], stroke.BaseColor, stroke.Reach);
        }

        void DrawGuide(Vector3 from, Vector3 to, float alpha)
        {
            if (!_guide) return;
            _guide.enabled = true;
            Color c = CurrentStroke.BaseColor;
            c.a = alpha;
            _guide.startColor = _guide.endColor = c;
            _guide.SetPosition(0, from);
            _guide.SetPosition(1, to);
        }

        void CompleteStroke()
        {
            SetGhostStyle(_strokeIndex, GhostStyle.Done);
            _strokeIndex++;
            PaintingProgressStore.SetStrokesCompleted(_painting.PaintingId, _strokeIndex, _strokes.Length);

            HideMarker();
            if (_strokeIndex >= _strokes.Length)
            {
                ProgressChanged?.Invoke();
                Celebrate();
                return;
            }

            _phase = RunPhase.AwaitingGate;
            _pointIndex = 0;
            SetGhostStyle(_strokeIndex, GhostStyle.Active);
            SpawnGate(_strokeIndex);
            ProgressChanged?.Invoke();
        }

        void Celebrate()
        {
            _phase = RunPhase.Celebrating;
            _benched = false;
            RestorePen();
            DespawnGate();
            HideMarker();
            if (_guide) _guide.enabled = false;

            PaintingProgressStore.MarkCompleted(_painting.PaintingId, _strokes.Length);
            ProgressChanged?.Invoke();
            CelebrateAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        // ── Gate ─────────────────────────────────────────────────────────────

        void SpawnGate(int strokeIndex)
        {
            DespawnGate();

            var stroke = _strokes[strokeIndex];
            Vector3 pos = stroke.Points[0];
            Vector3 dir = (stroke.Points[1] - stroke.Points[0]).sqrMagnitude > 1e-4f
                ? (stroke.Points[1] - stroke.Points[0]).normalized
                : _rotation * Vector3.forward;

            float ringRadius = Mathf.Clamp(stroke.Reach * 1.2f, 14f, 36f);

            var root = ToyFactory.CreateBareRoot($"Gate_{stroke.Name}", transform, pos, pos + dir, ringRadius);
            AddRing(root.transform, ringRadius, stroke.BaseColor);
            ToyFactory.AddLabel(root.transform, $"{strokeIndex + 1}/{_strokes.Length}  {stroke.Name}",
                stroke.BaseColor, ringRadius * 1.5f);

            var toy = root.AddComponent<SwapToy>();
            toy.Activated += OnGateActivated;
            toy.Initialize(_toyDefinition, _context, default);
            _gate = root;
            _gateBenchEasing = _benched; // spawned while benched → start hidden-bound
        }

        void OnGateActivated(SwapToy toy)
        {
            if (_phase != RunPhase.AwaitingGate || _benched) return;

            var stroke = CurrentStroke;
            RequestStrokeDomain(stroke.Domain);

            _phase = RunPhase.Painting;
            _pointIndex = 1; // the gate sits on point 0 — flying it consumes the stroke's start
            MoveMarker(stroke.Points[_pointIndex], stroke.BaseColor, stroke.Reach);

            var gate = _gate;
            _gate = null;
            if (gate) ScaleOutAndDestroy(gate, GateDespawnSeconds, this.GetCancellationTokenOnDestroy()).Forget();
        }

        void DespawnGate()
        {
            if (!_gate) return;
            ScaleOutAndDestroy(_gate, GateDespawnSeconds, this.GetCancellationTokenOnDestroy()).Forget();
            _gate = null;
        }

        void RequestStrokeDomain(Domains domain)
        {
            var gameData = _context?.GameData;
            if (!gameData) return;
            var lp = gameData.LocalPlayer;
            if (lp == null || lp.Domain == domain) return;

            // Degrade gracefully: if the session's domain count excludes this colour, paint in the
            // current colour rather than spamming a pick the server would reject.
            if (!GameDataSO.IsActiveDomain(domain, gameData.RequestedDomainCount)) return;

            if (lp is Player p && p.IsOwner)
                p.RequestSetDomain_ServerRpc(domain);
        }

        // ── Pen (trail spawner) management ───────────────────────────────────

        void ApplyPen(IVesselStatus vessel, bool engaged)
        {
            // Pen-up ONLY while transiting between strokes inside the studio zone. Painting a
            // stroke, leaving the zone, exiting freestyle, or benching all restore normal spawning.
            var desired = engaged && _phase == RunPhase.AwaitingGate && vessel != null
                ? vessel.VesselPrismController
                : null;

            if (ReferenceEquals(_pennedController, desired)) return;

            RestorePen();
            _pennedController = desired;
            if (_pennedController) _pennedController.SetSpawnerPaused(true);
        }

        void RestorePen()
        {
            if (_pennedController) _pennedController.SetSpawnerPaused(false);
            _pennedController = null;
        }

        // ── Visuals ──────────────────────────────────────────────────────────

        void SetGhostStyle(int index, GhostStyle style)
        {
            _strokes[index].Style = style;
            ApplyGhostStyle(_strokes[index]);
        }

        void ApplyAllGhostStyles()
        {
            for (int i = 0; i < _strokes.Length; i++)
                ApplyGhostStyle(_strokes[i]);
        }

        void ApplyGhostStyle(StrokeInfo stroke)
        {
            var lr = stroke.Ghost;
            if (!lr) return;

            Color c = stroke.BaseColor;
            float width;
            switch (stroke.Style)
            {
                case GhostStyle.Active:
                    c.a = 0.55f;
                    width = 1.7f;
                    break;
                case GhostStyle.Done:
                    // Dimmed solid — across sessions this is the "memory" of already-painted strokes.
                    c = new Color(c.r * 0.65f, c.g * 0.65f, c.b * 0.65f, 0.30f);
                    width = 1.1f;
                    break;
                default:
                    c.a = 0.13f;
                    width = 0.9f;
                    break;
            }

            c.a *= _bloom;
            lr.startColor = lr.endColor = c;
            lr.startWidth = lr.endWidth = width;
        }

        void MoveMarker(Vector3 pos, Color color, float reach)
        {
            _markerBaseRadius = Mathf.Max(3.5f, reach * 0.35f);
            if (!_marker)
            {
                _marker = new GameObject("NextPoint");
                _marker.transform.SetParent(transform, false);
                var body = ToyFactory.AddSphereBody(_marker.transform, 0.5f, color);
                _markerRenderer = body.GetComponent<MeshRenderer>();
            }
            else if (_markerRenderer && _markerRenderer.sharedMaterial)
            {
                // AddSphereBody gives the marker its own material instance, so this tint is private.
                _markerRenderer.sharedMaterial.color = color;
            }

            _markerHiding = false;
            _marker.SetActive(true);
            _marker.transform.position = pos;
            _marker.transform.localScale = Vector3.zero; // pop back up in PulseMarker — nothing snaps
        }

        void HideMarker() => _markerHiding = true; // EaseTransitions shrinks it out — nothing blinks off

        void PulseMarker()
        {
            if (!_marker || !_marker.activeSelf || _markerHiding) return;
            float pulse = 1f + 0.22f * Mathf.Sin(Time.time * MarkerPulseHz * Mathf.PI * 2f);
            float target = _markerBaseRadius * 2f * pulse;
            float current = _marker.transform.localScale.x;
            // Ease toward the pulsing size — doubles as the grow-in after MoveMarker zeroes it.
            _marker.transform.localScale = Vector3.one * Mathf.Lerp(current, target, Time.deltaTime * 8f);
        }

        IVesselStatus ResolveVessel()
        {
            var vs = _context?.GameData?.LocalPlayer?.Vessel?.VesselStatus;
            if (vs == null || (vs is UnityEngine.Object o && !o)) return null;
            return vs;
        }

        // ── Async transitions (continuity law: nothing pops in or out) ──────

        async UniTaskVoid BloomIn(CancellationToken ct)
        {
            float elapsed = 0f;
            while (elapsed < BloomSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                _bloom = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / BloomSeconds));
                ApplyAllGhostStyles();
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
            _bloom = 1f;
            ApplyAllGhostStyles();
        }

        async UniTaskVoid CelebrateAsync(CancellationToken ct)
        {
            // Swell: every stroke flashes bright in its own colour…
            float elapsed = 0f;
            const float swell = 0.6f;
            while (elapsed < swell)
            {
                elapsed += Time.unscaledDeltaTime;
                _bloom = 1f + Mathf.Sin(Mathf.Clamp01(elapsed / swell) * Mathf.PI) * 0.9f;
                ApplyAllGhostStyles();
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            await UniTask.Delay(900, ignoreTimeScale: true, cancellationToken: ct);

            // …then the blueprint fades away and only the painted prisms remain.
            elapsed = 0f;
            const float fade = 2.4f;
            while (elapsed < fade)
            {
                elapsed += Time.unscaledDeltaTime;
                _bloom = Mathf.Lerp(1f, 0f, Mathf.Clamp01(elapsed / fade));
                ApplyAllGhostStyles();
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            Finished?.Invoke();
            if (this) Destroy(gameObject);
        }

        static async UniTaskVoid ScaleOutAndDestroy(GameObject go, float seconds, CancellationToken ct)
        {
            if (!go) return;
            Vector3 start = go.transform.localScale;
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                if (!go) return;
                elapsed += Time.unscaledDeltaTime;
                go.transform.localScale = Vector3.LerpUnclamped(start, Vector3.zero,
                    Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / seconds)));
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
            if (go) Destroy(go);
        }

        /// <summary>A flat ring in the local XY plane — the gate the vessel flies through.</summary>
        static void AddRing(Transform parent, float radius, Color color)
        {
            var lr = ToyFactory.CreateLine("Ring", parent, 2.2f, false);
            const int segs = 28;
            lr.loop = true;
            lr.positionCount = segs;
            for (int i = 0; i < segs; i++)
            {
                float a = i / (float)segs * Mathf.PI * 2f;
                lr.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
            }
            lr.startColor = lr.endColor = color;

            // A soft hub so the gate reads at distance.
            var hub = ToyFactory.AddSphereBody(parent, radius * 0.16f, color);
            hub.name = "Hub";
        }
    }
}
