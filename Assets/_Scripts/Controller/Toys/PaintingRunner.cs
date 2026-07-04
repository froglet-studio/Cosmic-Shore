using System;
using System.Collections.Generic;
using System.Threading;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
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
        enum RunPhase { AwaitingGate, Painting, Celebrating }
        enum GhostStyle { Pending, Active, Done }

        /// <summary>Raised on stroke completion, bench toggles, and celebration — drives the toy's label.</summary>
        public event Action ProgressChanged;

        const float BloomSeconds = 1.4f;
        const float GateDespawnSeconds = 0.45f;
        const float MarkerPulseHz = 1.4f;

        ToyContext _context;
        ToyDefinitionSO _toyDefinition;
        PaintingDefinitionSO _painting;
        Quaternion _rotation;

        Vector3[][] _strokes;          // world-space points per stroke
        string[] _strokeNames;
        Domains[] _strokeDomains;
        float[] _strokeReach;          // adaptive per-stroke advance distance

        LineRenderer[] _ghosts;
        GhostStyle[] _ghostStyles;
        float _bloom;                  // 0→1 on begin; falls back to 0 during the farewell fade

        int _strokeIndex;              // == strokes completed while AwaitingGate
        int _pointIndex;
        RunPhase _phase;
        bool _benched;

        GameObject _gate;
        GameObject _marker;
        LineRenderer _guide;
        float _markerBaseRadius;

        Vector3 _zoneCenter;
        float _zoneSqrRadius;

        VesselPrismController _pennedController;

        public bool IsCelebrating => _phase == RunPhase.Celebrating;
        public bool IsBenched => _benched;
        public int StrokesCompleted => Mathf.Min(_strokeIndex, StrokeCount);
        public int StrokeCount => _strokes?.Length ?? 0;

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

            // Resolve + filter (a stroke needs at least a line to fly).
            var source = painting.Strokes;
            var strokes = new List<Vector3[]>();
            var names = new List<string>();
            var domains = new List<Domains>();
            foreach (var s in source)
            {
                if (s?.points == null || s.points.Count < 2) continue;
                var world = new Vector3[s.points.Count];
                for (int i = 0; i < s.points.Count; i++)
                    world[i] = origin + rotation * s.points[i];
                strokes.Add(world);
                names.Add(string.IsNullOrEmpty(s.name) ? $"Stroke {strokes.Count}" : s.name);
                domains.Add(s.domain);
            }

            _strokes = strokes.ToArray();
            _strokeNames = names.ToArray();
            _strokeDomains = domains.ToArray();

            if (_strokes.Length == 0)
            {
                CosmicShore.Utility.CSDebug.LogWarning(
                    $"[PaintingRunner] '{painting.DisplayName}' has no drawable strokes.");
                Destroy(gameObject);
                return;
            }

            // Per-stroke reach: tighten on fine detail so a tight balcony ring can't be cleared
            // from its centre, keep the authored threshold on broad strokes.
            _strokeReach = new float[_strokes.Length];
            for (int i = 0; i < _strokes.Length; i++)
            {
                float minSeg = float.MaxValue;
                var pts = _strokes[i];
                for (int p = 1; p < pts.Length; p++)
                    minSeg = Mathf.Min(minSeg, Vector3.Distance(pts[p - 1], pts[p]));
                _strokeReach[i] = Mathf.Clamp(minSeg * 0.7f, 6f, _painting.ReachThreshold);
            }

            // Studio zone: pen-up between strokes applies only inside this sphere.
            Bounds local = painting.LocalBounds;
            _zoneCenter = origin + rotation * local.center;
            float zoneRadius = local.extents.magnitude * 1.3f + 120f;
            _zoneSqrRadius = zoneRadius * zoneRadius;

            // Ghost blueprint: one line per stroke, tinted its domain.
            _ghosts = new LineRenderer[_strokes.Length];
            _ghostStyles = new GhostStyle[_strokes.Length];
            _strokeIndex = Mathf.Clamp(resumeFromStroke, 0, _strokes.Length - 1);
            if (resumeFromStroke >= _strokes.Length) _strokeIndex = 0; // stale "complete" state — fresh canvas
            for (int i = 0; i < _strokes.Length; i++)
            {
                _ghosts[i] = MakeLine($"Ghost_{i}", transform, 1f, true);
                _ghosts[i].positionCount = _strokes[i].Length;
                _ghosts[i].SetPositions(_strokes[i]);
                _ghostStyles[i] = i < _strokeIndex ? GhostStyle.Done : GhostStyle.Pending;
            }
            _ghostStyles[_strokeIndex] = GhostStyle.Active;

            _guide = MakeLine("Guide", transform, 0.9f, true);
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
            if (!benched) BenchOtherRunners();
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
            if (_phase == RunPhase.Celebrating || _strokes == null) return;

            var vessel = ResolveVessel();
            bool freestyle = _context?.IsFreestyleActive == null || _context.IsFreestyleActive();
            Vector3 shipPos = vessel != null ? vessel.Transform.position : Vector3.zero;
            bool inZone = vessel != null && (shipPos - _zoneCenter).sqrMagnitude <= _zoneSqrRadius;
            bool engaged = vessel != null && freestyle && inZone && !_benched;

            ApplyPen(vessel, engaged);

            if (_gate && _gate.activeSelf == _benched)
                _gate.SetActive(!_benched);

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

        void OnDestroy()
        {
            // The one hard rule: never leave the player's trail spawner paused behind us.
            RestorePen();
        }

        // ── Phase updates ────────────────────────────────────────────────────

        void UpdateAwaitingGate(Vector3 shipPos)
        {
            if (!_gate) return;
            if (_guide)
            {
                _guide.enabled = true;
                Color c = DomainColor(_strokeDomains[_strokeIndex]);
                SetLineColor(_guide, new Color(c.r, c.g, c.b, 0.35f));
                _guide.SetPosition(0, shipPos);
                _guide.SetPosition(1, _gate.transform.position);
            }
        }

        void UpdatePainting(Vector3 shipPos)
        {
            var pts = _strokes[_strokeIndex];
            Vector3 target = pts[_pointIndex];
            float reach = _strokeReach[_strokeIndex];

            if (_guide)
            {
                _guide.enabled = true;
                Color c = DomainColor(_strokeDomains[_strokeIndex]);
                SetLineColor(_guide, new Color(c.r, c.g, c.b, 0.6f));
                _guide.SetPosition(0, shipPos);
                _guide.SetPosition(1, target);
            }

            if ((shipPos - target).sqrMagnitude > reach * reach) return;

            _pointIndex++;
            if (_pointIndex >= pts.Length)
            {
                CompleteStroke();
                return;
            }
            MoveMarker(pts[_pointIndex], DomainColor(_strokeDomains[_strokeIndex]), reach);
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

            var pts = _strokes[strokeIndex];
            Vector3 pos = pts[0];
            Vector3 dir = pts.Length > 1 && (pts[1] - pts[0]).sqrMagnitude > 1e-4f
                ? (pts[1] - pts[0]).normalized
                : _rotation * Vector3.forward;

            Color c = DomainColor(_strokeDomains[strokeIndex]);
            float ringRadius = Mathf.Clamp(_strokeReach[strokeIndex] * 1.2f, 14f, 36f);

            var root = ToyFactory.CreateBareRoot($"Gate_{_strokeNames[strokeIndex]}", transform,
                pos, pos + dir, ringRadius);
            AddRing(root.transform, ringRadius, c);
            ToyFactory.AddLabel(root.transform, GateLabel(strokeIndex), c, ringRadius * 1.5f);

            var toy = root.AddComponent<SwapToy>();
            toy.Activated += OnGateActivated;
            toy.Initialize(_toyDefinition, _context, default);
            _gate = root;
        }

        string GateLabel(int strokeIndex)
            => $"{strokeIndex + 1}/{_strokes.Length}  {_strokeNames[strokeIndex]}";

        void OnGateActivated(SwapToy toy)
        {
            if (_phase != RunPhase.AwaitingGate || _benched) return;

            RequestStrokeDomain(_strokeDomains[_strokeIndex]);

            _phase = RunPhase.Painting;
            var pts = _strokes[_strokeIndex];
            _pointIndex = 1; // the gate sits on point 0 — flying it consumes the stroke's start
            MoveMarker(pts[_pointIndex], DomainColor(_strokeDomains[_strokeIndex]), _strokeReach[_strokeIndex]);

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
            _ghostStyles[index] = style;
            ApplyGhostStyle(index);
        }

        void ApplyAllGhostStyles()
        {
            for (int i = 0; i < _ghosts.Length; i++)
                ApplyGhostStyle(i);
        }

        void ApplyGhostStyle(int index)
        {
            var lr = _ghosts[index];
            if (!lr) return;

            Color c = DomainColor(_strokeDomains[index]);
            float width;
            switch (_ghostStyles[index])
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
            SetLineColor(lr, c);
            lr.startWidth = lr.endWidth = width;
        }

        void MoveMarker(Vector3 pos, Color color, float reach)
        {
            _markerBaseRadius = Mathf.Max(3.5f, reach * 0.35f);
            if (!_marker)
            {
                _marker = new GameObject("NextPoint");
                _marker.transform.SetParent(transform, false);
                ToyFactory.AddSphereBody(_marker.transform, 0.5f, color);
            }
            else
            {
                var renderer = _marker.GetComponentInChildren<MeshRenderer>();
                if (renderer && renderer.sharedMaterial) renderer.sharedMaterial.color = color;
            }

            _marker.SetActive(true);
            _marker.transform.position = pos;
            _marker.transform.localScale = Vector3.zero; // pop back up in PulseMarker — nothing snaps
        }

        void HideMarker()
        {
            if (_marker) _marker.SetActive(false);
        }

        void PulseMarker()
        {
            if (!_marker || !_marker.activeSelf) return;
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

        Color DomainColor(Domains d)
        {
            var tm = _context?.GameData ? _context.GameData.ThemeManagerData : null;
            if (tm) return tm.GetDomainUIColor(d);
            return d switch
            {
                Domains.Jade => new Color(0.15f, 0.95f, 0.55f),
                Domains.Ruby => new Color(1.00f, 0.20f, 0.45f),
                Domains.Gold => new Color(1.00f, 0.80f, 0.15f),
                _ => Color.gray,
            };
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

        // ── Line building ────────────────────────────────────────────────────

        static LineRenderer MakeLine(string name, Transform parent, float width, bool worldSpace)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = worldSpace;
            lr.positionCount = 0;
            lr.startWidth = lr.endWidth = width;
            lr.numCapVertices = 4;
            lr.numCornerVertices = 4;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
            if (shader) lr.material = new Material(shader);
            return lr;
        }

        static void SetLineColor(LineRenderer lr, Color c)
        {
            // Vertex colours only — Sprites/Default multiplies vertex × material tint, so also
            // setting the material colour would square the alpha and wash the ghosts out.
            lr.startColor = lr.endColor = c;
        }

        /// <summary>A flat ring in the local XY plane — the gate the vessel flies through.</summary>
        static void AddRing(Transform parent, float radius, Color color)
        {
            var lr = MakeLine("Ring", parent, 2.2f, false);
            const int segs = 28;
            lr.loop = true;
            lr.positionCount = segs;
            for (int i = 0; i < segs; i++)
            {
                float a = i / (float)segs * Mathf.PI * 2f;
                lr.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
            }
            SetLineColor(lr, color);

            // A soft hub so the gate reads at distance.
            var hub = ToyFactory.AddSphereBody(parent, radius * 0.16f, color);
            hub.name = "Hub";
        }
    }
}
