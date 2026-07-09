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
    /// The multi-stroke "connect the dots" runner. A painting is a world-anchored monument-in-progress:
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
        Vector3 _origin;
        Quaternion _rotation;

        StrokeInfo[] _strokes;
        float _bloom;                  // 0→1 on begin; swells then falls to 0 during the farewell fade
        float _benchVisibility = 1f;   // 1 engaged, eases to 0 while benched so a paused blueprint fades away

        int _strokeIndex;              // == strokes completed while AwaitingGate
        int _pointIndex;
        RunPhase _phase;
        bool _benched;
        bool _wasEngaged;

        GameObject _gate;
        bool _gateBenchEasing;
        GameObject _marker;
        GameObject _markerCone;
        GameObject _markerJack;
        int _markerStrokeIndex = -1;
        bool _markerHiding;
        LineRenderer _guide;
        float _markerBaseRadius;

        Vector3 _zoneCenter;
        float _zoneSqrRadius;

        VesselPrismController _pennedController;
        VesselPrismController _captureController;   // OnBlockSpawned source while painting a stroke
        readonly Trail _restoredTrail = new();       // groups prisms re-grown from a saved painting

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
            _origin = origin;
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

            // Coming back from another session / game mode: the completed strokes' prisms were
            // saved as drawing state — regrow them so the monument physically resumes, not just
            // the counter. (In-session resumes bench/unbench the same runner, so no duplicates.)
            if (_strokeIndex > 0 && PaintingPrismStore.HasPrisms(_painting.PaintingId))
                RestorePrismsAsync(this.GetCancellationTokenOnDestroy()).Forget();
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
                HideMarker();           // the floating next-point spike goes with the blueprint
            }
            else
            {
                BenchOtherRunners();
                // Re-show the next-point marker we hid on pause (mid-stroke resume only).
                if (_phase == RunPhase.Painting) MoveMarker(_strokeIndex, _pointIndex);
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
            ApplyCapture(vessel, engaged);

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

            SettleMarker();
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

            // A paused run's ghost blueprint fades away entirely (nothing lingers in the lava lamp);
            // resuming fades it back. The already-painted prisms are conserved mass and are untouched.
            float benchTarget = _benched ? 0f : 1f;
            if (!Mathf.Approximately(_benchVisibility, benchTarget))
            {
                _benchVisibility = Mathf.MoveTowards(_benchVisibility, benchTarget, Time.deltaTime * 3.5f);
                ApplyAllGhostStyles();
            }
        }

        void OnDestroy()
        {
            // The one hard rule: never leave the player's trail spawner paused behind us.
            RestorePen();
            StopCapture();
            // A stroke abandoned mid-flight is re-flown fresh next time — drop its buffer.
            if (_painting) PaintingPrismStore.DiscardPending(_painting.PaintingId);
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
            MoveMarker(_strokeIndex, _pointIndex);
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
            // Persist the stroke's prisms as drawing state (position/orientation/size/domain) —
            // this is what regrows on return and what the share exporter reconstructs.
            PaintingPrismStore.CommitStroke(_painting.PaintingId, _strokeIndex);
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
            StopCapture();
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

            // Trail-ON gate: cone hub in the stroke domain's prism material (shared shape language).
            _gate = ToyFactory.CreateGate($"Gate_{stroke.Name}", transform, pos, dir, ringRadius,
                stroke.BaseColor, $"{strokeIndex + 1}/{_strokes.Length}  {stroke.Name}",
                hubIsCone: true, ToyFactory.DomainPrismMaterial(_context, stroke.Domain),
                _toyDefinition, _context, OnGateActivated);
            _gateBenchEasing = _benched; // spawned while benched → start hidden-bound
        }

        void OnGateActivated(SwapToy toy)
        {
            if (_phase != RunPhase.AwaitingGate || _benched) return;

            var stroke = CurrentStroke;
            RequestStrokeDomain(stroke.Domain);

            _phase = RunPhase.Painting;
            _pointIndex = 1; // the gate sits on point 0 — flying it consumes the stroke's start
            MoveMarker(_strokeIndex, _pointIndex);

            var gate = _gate;
            _gate = null;
            if (gate) ToyFactory.ScaleOutAndDestroy(gate, GateDespawnSeconds).Forget();
        }

        void DespawnGate()
        {
            if (!_gate) return;
            ToyFactory.ScaleOutAndDestroy(_gate, GateDespawnSeconds).Forget();
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

        // ── Drawing-state capture & restore ──────────────────────────────────

        /// <summary>
        /// While a stroke is being painted, listen to the vessel's block-spawn event and record
        /// every prism laid inside the studio zone as painting-local drawing state. Follows the
        /// vessel across mid-stroke ship swaps (the controller reference is re-resolved).
        /// </summary>
        void ApplyCapture(IVesselStatus vessel, bool engaged)
        {
            var desired = engaged && _phase == RunPhase.Painting && vessel != null
                ? vessel.VesselPrismController
                : null;

            if (ReferenceEquals(_captureController, desired)) return;

            StopCapture();
            _captureController = desired;
            if (_captureController) _captureController.OnBlockSpawned += HandleBlockSpawned;
        }

        void StopCapture()
        {
            if (_captureController) _captureController.OnBlockSpawned -= HandleBlockSpawned;
            _captureController = null;
        }

        void HandleBlockSpawned(Prism prism)
        {
            if (!prism || _phase != RunPhase.Painting || !_captureController) return;

            Vector3 worldPos = prism.transform.position;
            if ((worldPos - _zoneCenter).sqrMagnitude > _zoneSqrRadius) return; // strays aren't artwork

            // Record the stroke's INTENDED domain (when the session allows it): the pick RPC takes
            // a round trip, so recording the live player domain would bake a wrong-colour seam at
            // every stroke start into the saved state / regrown monument / shared reconstruction.
            var gameData = _context?.GameData;
            Domains domain = CurrentStroke.Domain;
            if (gameData && !GameDataSO.IsActiveDomain(domain, gameData.RequestedDomainCount))
                domain = gameData.LocalPlayer?.Domain ?? domain; // pick was rejected — record reality

            var inverse = Quaternion.Inverse(_rotation);
            PaintingPrismStore.RecordPrism(_painting.PaintingId, PaintingPrismRecord.From(
                inverse * (worldPos - _origin),
                inverse * prism.transform.rotation,
                prism.TargetScale,
                domain,
                _captureController.SpawnPrismType));
        }

        /// <summary>
        /// Regrow the saved prisms of every completed stroke through the normal prism factory
        /// (pooled, grow-in animation — nothing pops), streamed over frames so a monument-sized
        /// restore reads as the painting growing back rather than a hitch.
        /// </summary>
        async UniTaskVoid RestorePrismsAsync(CancellationToken ct)
        {
            var records = PaintingPrismStore.GetPrisms(_painting.PaintingId, _strokeIndex);
            if (records.Count == 0) return;

            // Wait for a local vessel — its controller carries the factory channel + owner name.
            VesselPrismController controller = null;
            for (int tries = 0; tries < 100 && controller == null; tries++)
            {
                var vessel = ResolveVessel();
                controller = vessel?.VesselPrismController;
                if (controller == null)
                    await UniTask.Delay(100, ignoreTimeScale: true, cancellationToken: ct);
            }
            if (controller == null || !controller.PrismSpawnChannel) return;

            var channel = controller.PrismSpawnChannel;
            string owner = ResolveVessel()?.PlayerName ?? "Painter";
            int perFrame = Mathf.Max(6, records.Count / 150);

            int missing = 0;
            for (int i = 0; i < records.Count; i++)
            {
                if (!TrySpawnRestoredPrism(channel, records[i], owner)) missing++;
                if ((i + 1) % perFrame == 0)
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
            if (missing > 0)
                CSDebug.LogWarning($"[PaintingRunner] Regrew {records.Count - missing}/{records.Count} " +
                                   $"prisms of '{_painting.DisplayName}' — a recorded prism pool is unavailable.");
        }

        bool TrySpawnRestoredPrism(PrismEventChannelWithReturnSO channel, PaintingPrismRecord record, string owner)
        {
            Vector3 pos = _origin + _rotation * record.Position;
            Quaternion rot = _rotation * record.Rotation;
            var domain = (Domains)record.domain;
            var prismType = (PrismType)record.prismType;

            var ret = channel.RaiseEvent(new PrismEventData
            {
                ownDomain = domain,
                Rotation = rot,
                SpawnPosition = pos,
                Scale = record.Scale,
                PrismType = prismType,
            });
            // A scene whose factory lacks the recorded pool returns null — regrow as the generic
            // Interactive prism rather than leaving holes in the monument.
            if (ret.SpawnedObject == null && prismType != PrismType.Interactive)
                ret = channel.RaiseEvent(new PrismEventData
                {
                    ownDomain = domain,
                    Rotation = rot,
                    SpawnPosition = pos,
                    Scale = record.Scale,
                    PrismType = PrismType.Interactive,
                });
            if (ret.SpawnedObject == null || !ret.SpawnedObject.TryGetComponent(out Prism prism)) return false;

            // Mirror VesselPrismController.CreateBlock's post-spawn setup.
            prism.TargetScale = record.Scale;
            prism.ownerID = owner;
            prism.ChangeTeam(domain);
            prism.waitTime = 0.6f;
            _restoredTrail.Add(prism);
            prism.prismProperties.Index = (ushort)(_restoredTrail.TrailList.Count - 1);
            prism.Initialize(owner);
            return true;
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

            c.a *= _bloom * _benchVisibility;
            lr.startColor = lr.endColor = c;
            lr.startWidth = lr.endWidth = width;
        }

        /// <summary>
        /// Shared trail-toy shape language: intermediate points are CONES whose apex points at the
        /// stroke's next point ("this way next" — the same shape the domain changer wears, since
        /// both change your trail); each stroke's FINAL point — the one that turns the trail off —
        /// is a JACK. Both wear the stroke domain's prism material.
        /// </summary>
        void MoveMarker(int strokeIndex, int pointIndex)
        {
            var stroke = _strokes[strokeIndex];
            Vector3 pos = stroke.Points[pointIndex];
            bool isStrokeEnd = pointIndex == stroke.Points.Length - 1;
            _markerBaseRadius = Mathf.Max(3.5f, stroke.Reach * 0.35f);

            if (!_marker)
            {
                _marker = new GameObject("NextPoint");
                _marker.transform.SetParent(transform, false);
            }
            if (_markerStrokeIndex != strokeIndex || !_markerCone || !_markerJack)
            {
                _markerStrokeIndex = strokeIndex;
                for (int i = _marker.transform.childCount - 1; i >= 0; i--)
                    Destroy(_marker.transform.GetChild(i).gameObject);
                var prismMat = ToyFactory.DomainPrismMaterial(_context, stroke.Domain);
                _markerCone = ToyFactory.AddConeBody(_marker.transform, 0.45f, 1.25f, stroke.BaseColor, prismMat);
                _markerJack = ToyFactory.AddJackBody(_marker.transform, 0.55f, stroke.BaseColor, prismMat);
            }
            _markerCone.SetActive(!isStrokeEnd);
            _markerJack.SetActive(isStrokeEnd);

            if (isStrokeEnd)
            {
                _marker.transform.rotation = _rotation; // jacks are omnidirectional — sit square to the painting
            }
            else
            {
                Vector3 toNext = stroke.Points[pointIndex + 1] - pos;
                _marker.transform.rotation = toNext.sqrMagnitude > 1e-4f
                    ? Quaternion.LookRotation(toNext.normalized, Vector3.up)
                    : _rotation;
            }

            _markerHiding = false;
            _marker.SetActive(true);
            _marker.transform.position = pos;
            _marker.transform.localScale = Vector3.zero; // SettleMarker grows it back in — nothing snaps
        }

        void HideMarker() => _markerHiding = true; // EaseTransitions shrinks it out — nothing blinks off

        void SettleMarker()
        {
            if (!_marker || !_marker.activeSelf || _markerHiding) return;
            // No pulsing: the marker grows in once (from the zero MoveMarker sets) and holds its
            // size — the idle life comes from the body's slow ToyIdleSpin, matching the calm
            // motion language of the game's other pickups.
            float target = _markerBaseRadius * 2f;
            float current = _marker.transform.localScale.x;
            if (Mathf.Abs(current - target) < 0.01f) return;
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

    }
}
