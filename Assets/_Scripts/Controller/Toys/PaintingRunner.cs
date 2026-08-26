using System;
using System.Collections.Generic;
using System.Threading;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
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
    /// "studio zone" so transit flight never scribbles across the artwork - and it is ALWAYS restored
    /// when the player leaves the zone, exits freestyle, benches the run, or the runner dies.
    ///
    /// Toy-faithful: no score, no timer, no fail state. Progress is stroke-granular, resumable
    /// in-session and across sessions (<see cref="PaintingProgressStore"/>), and finishing simply
    /// celebrates and offers a fresh canvas. The painted prisms are conserved mass like any trail.
    /// </summary>
    public class PaintingRunner : MonoBehaviour, IObjectiveProvider
    {
        enum RunPhase { AwaitingGate = 0, Painting = 1, Celebrating = 2 }
        enum GhostStyle { Pending = 0, Active = 1, Done = 2 }

        /// <summary>Raised on stroke completion, bench toggles, and celebration - drives the toy's label.</summary>
        public event Action ProgressChanged;

        /// <summary>Raised once, just before the runner destroys itself after the celebration.</summary>
        public event Action Finished;

        const float BloomSeconds = 1.4f;
        const float GateDespawnSeconds = 0.45f;

        /// <summary>Everything the runner knows about one stroke - one array, no index juggling.</summary>
        class StrokeInfo
        {
            public int Index;             // position in the runner's stroke array
            public Vector3[] Points;      // world space
            public string Name;
            public Domains Domain;
            public float Reach;           // adaptive advance distance
            public Color BaseColor;       // resolved once at Begin - no per-frame theme lookups
            public LineRenderer Ghost;
            public GhostStyle Style;
            public List<int> Checkpoints; // sparse ride targets (never on tight curvature)
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
        int _pointIndex;               // index into CurrentStroke.Checkpoints while Painting
        RunPhase _phase;
        bool _benched;
        bool _wasEngaged;

        GameObject _gate;
        bool _gateBenchEasing;
        GameObject _milestone;         // the ONE live ride ring (SphereCollider trigger = its radius)
        StrokeMilestoneTrigger _milestoneTrigger; // cached at spawn - no per-frame TryGetComponent
        int _fadeIndex = -1;           // stroke whose ghost line is easing out (ridden) or back in (done)
        float _lineFade = 1f;

        // The standard off-screen objective arrow points here: the start gate while awaiting it,
        // the current ride ring while painting. A bare transform (not the ring itself) so the
        // pointer survives the ring folding away on disengage.
        Transform _objectiveAnchor;
        static ObjectiveIndicator s_sharedIndicator;                          // ONE arrow for the whole gallery
        static readonly PaintingObjectiveRelay s_objectiveRelay = new();      // routes it to the brush holder

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

            // Checkpoint rhythm scales with the monument, not the stroke: a big painting gets big
            // gaps between ride markers (the curve between them is scenery to carve, not a quiz).
            Bounds localBounds = painting.LocalBounds;
            float checkpointSpacing = Mathf.Max(90f, 0.085f * localBounds.size.magnitude);

            // PaintingDefinitionSO.Strokes is already filtered to drawable strokes - using it
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

                float reach = Mathf.Clamp(minSeg * 0.7f, 4f, _painting.ReachThreshold);
                _strokes[i] = new StrokeInfo
                {
                    Index = i,
                    Points = world,
                    Name = string.IsNullOrEmpty(s.name) ? $"Stroke {i + 1}" : s.name,
                    Domain = s.domain,
                    Reach = reach,
                    BaseColor = ToyFactory.DomainAccentColor(context, s.domain),
                    // Ride targets are SPARSE - spaced by arc, never parked on tight curvature -
                    // so dense reference curves are ridden freely between big forgiving markers.
                    Checkpoints = PaintingStrokeToolkit.RideCheckpoints(world,
                        Mathf.Max(checkpointSpacing, reach * 3f), 28f),
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
            if (resumeFromStroke >= _strokes.Length) _strokeIndex = 0; // stale "complete" state - fresh canvas
            for (int i = 0; i < _strokes.Length; i++)
            {
                var ghost = ToyFactory.CreateLine($"Ghost_{i}", transform, 1f, true);
                ghost.positionCount = _strokes[i].Points.Length;
                ghost.SetPositions(_strokes[i].Points);
                _strokes[i].Ghost = ghost;
                _strokes[i].Style = i < _strokeIndex ? GhostStyle.Done : GhostStyle.Pending;
            }
            _strokes[_strokeIndex].Style = GhostStyle.Active;

            var anchor = new GameObject("ObjectiveAnchor");
            anchor.transform.SetParent(transform, false);
            _objectiveAnchor = anchor.transform;

            _phase = RunPhase.AwaitingGate;
            _pointIndex = 0;
            SpawnGate(_strokeIndex);

            _bloom = 0f;
            ApplyAllGhostStyles();
            BenchOtherRunners(); // a fresh run takes the brush
            s_objectiveRelay.Active = this;
            EnsureObjectiveIndicator();
            BloomIn(this.GetCancellationTokenOnDestroy()).Forget();

            // Coming back from another session / game mode: the completed strokes' prisms were
            // saved as drawing state - regrow them so the monument physically resumes, not just
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
                // Release the pen NOW - waiting for this runner's next Update could clobber a
                // pause another runner (the new brush holder) sets in the meantime.
                RestorePen();
                _gateBenchEasing = true;
                DespawnMilestone();     // the ride ring folds away with the blueprint
                if (s_objectiveRelay.Active == this) s_objectiveRelay.Active = null;
            }
            else
            {
                BenchOtherRunners();
                s_objectiveRelay.Active = this;
                // The gate may have fully folded (and stopped rendering) while benched - regrow it.
                if (_gate && !_gate.activeSelf) _gate.SetActive(true);
                _gateBenchEasing = _gate != null;
                // Re-spawn the ride ring we folded on pause (mid-stroke resume only).
                if (_phase == RunPhase.Painting) SpawnMilestone();
            }
            ProgressChanged?.Invoke();
        }

        /// <summary>
        /// Only one painting run holds the brush at a time - neighbouring studio zones can overlap,
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
            // Domain Changer) re-asserts the stroke's colour - the gate only fired once - and
            // re-arms the ride ring folded on disengage.
            if (engaged && !_wasEngaged && _phase == RunPhase.Painting)
            {
                RequestStrokeDomain(CurrentStroke.Domain);
                if (!_milestone) SpawnMilestone();
            }
            _wasEngaged = engaged;

            if (!engaged)
            {
                // The ride ring must not outlive engagement: the local vessel on lava-lamp
                // autopilot would drift through it and latch a checkpoint nobody rode.
                DespawnMilestone();
                return;
            }

            if (_phase == RunPhase.Painting)
                UpdatePainting(shipPos);
        }

        /// <summary>
        /// Continuity-law eases that must run even while benched/disengaged: the gate shrinks
        /// away and regrows on bench toggles, the blueprint fades, and the ride ring grows/folds.
        /// </summary>
        void EaseTransitions()
        {
            if (_gate && _gateBenchEasing)
            {
                var t = _gate.transform;
                Vector3 target = _benched ? Vector3.zero : Vector3.one;
                t.localScale = Vector3.Lerp(t.localScale, target, Time.deltaTime * 7f);
                if ((t.localScale - target).sqrMagnitude < 1e-4f)
                {
                    t.localScale = target;
                    _gateBenchEasing = false;
                    // Fully folded - stop rendering it (SetBenched(false) reactivates + re-arms).
                    if (_benched) _gate.SetActive(false);
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

            // The ride ring grows in on spawn and folds away while benched (continuity law).
            if (_milestone)
            {
                var mt = _milestone.transform;
                Vector3 target = _benched ? Vector3.zero : Vector3.one;
                if ((mt.localScale - target).sqrMagnitude > 1e-6f)
                {
                    mt.localScale = Vector3.Lerp(mt.localScale, target, Time.deltaTime * 7f);
                    if ((mt.localScale - target).sqrMagnitude < 1e-4f) mt.localScale = target;
                }
            }

            // The ridden stroke's blueprint line eases out (and its "done" memory line eases back
            // in on completion) - continuity law: no instant appear/disappear, even for a line.
            if (_fadeIndex >= 0 && _strokes != null && _fadeIndex < _strokes.Length)
            {
                float fadeTarget = _phase == RunPhase.Painting && _fadeIndex == _strokeIndex ? 0f : 1f;
                if (Mathf.Approximately(_lineFade, fadeTarget))
                {
                    if (fadeTarget >= 1f) _fadeIndex = -1; // fade-in finished - back to plain styles
                }
                else
                {
                    _lineFade = Mathf.MoveTowards(_lineFade, fadeTarget, Time.deltaTime * 3.5f);
                    ApplyGhostStyle(_strokes[_fadeIndex]);
                }
            }
        }

        void OnDestroy()
        {
            // The one hard rule: never leave the player's trail spawner paused behind us.
            RestorePen();
            StopCapture();
            if (s_objectiveRelay.Active == this) s_objectiveRelay.Active = null;
            // A stroke abandoned mid-flight is re-flown fresh next time - drop its buffer.
            if (_painting) PaintingPrismStore.DiscardPending(_painting.PaintingId);
        }

        // ── Phase updates ────────────────────────────────────────────────────

        void UpdatePainting(Vector3 shipPos)
        {
            var stroke = CurrentStroke;
            Vector3 target = stroke.Points[stroke.Checkpoints[_pointIndex]];

            // The milestone ring's sphere trigger is the hit volume; effects run here on the Update
            // tick (never inside the physics callback). A slightly tighter distance check backstops
            // fast passes the physics step might miss.
            bool tripped = _milestoneTrigger && _milestoneTrigger.Tripped;
            float backstop = MilestoneRadius(stroke) * 0.85f;
            if (!tripped && (shipPos - target).sqrMagnitude > backstop * backstop) return;

            AdvanceMilestone();
        }

        // ── Objective indicator (the game's standard off-screen pointer) ─────

        /// <summary>
        /// <see cref="IObjectiveProvider"/>: the standard edge-of-screen arrow points at the start
        /// gate while awaiting it and at the current ride ring while painting - only for the run
        /// that holds the brush, only in freestyle. The arrow hides by itself whenever the target
        /// is already on screen, so the world rings stay the primary guidance.
        /// </summary>
        public bool TryGetObjective(out Transform target)
        {
            target = null;
            if (_benched || _phase == RunPhase.Celebrating || _strokes == null) return false;
            if (_context?.IsFreestyleActive != null && !_context.IsFreestyleActive()) return false;
            if (!_objectiveAnchor) return false;
            target = _objectiveAnchor;
            return true;
        }

        /// <summary>
        /// Lazily stands up the ONE shared <see cref="ObjectiveIndicator"/> for the painting
        /// gallery. It MUST parent under the full-screen Canvas root (the indicator stretches to
        /// its parent and clamps to that rect's edges - a mid-hierarchy container like "Game UI"
        /// is not a full-screen rect, which pins the arrow in a corner). Freestyle-only
        /// visibility comes from the provider, not the parent's CanvasGroup. One-time scene
        /// lookup at run start (activation-rate, same budget as <see cref="BenchOtherRunners"/>)
        /// - mirrors MiniGameHUD.EnsureObjectiveIndicator, which also parents at the canvas root.
        /// </summary>
        static void EnsureObjectiveIndicator()
        {
            if (s_sharedIndicator) return;

            var hud = FindAnyObjectByType<MenuMiniGameHUD>(FindObjectsInactive.Include);
            Canvas canvas = hud ? hud.GetComponentInParent<Canvas>(true) : null;
            if (!canvas) canvas = FindAnyObjectByType<Canvas>();
            if (!canvas) return; // headless/test scene - the toy plays fine without the arrow
            s_sharedIndicator = ObjectiveIndicator.CreateRuntime(canvas.transform, s_objectiveRelay);
        }

        void CompleteStroke()
        {
            DespawnMilestone();
            SetGhostStyle(_strokeIndex, GhostStyle.Done);
            // Persist the stroke's prisms as drawing state (position/orientation/size/domain) -
            // this is what regrows on return and what the share exporter reconstructs.
            PaintingPrismStore.CommitStroke(_painting.PaintingId, _strokeIndex);
            _strokeIndex++;
            PaintingProgressStore.SetStrokesCompleted(_painting.PaintingId, _strokeIndex, _strokes.Length);

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
            DespawnMilestone();
            if (s_objectiveRelay.Active == this) s_objectiveRelay.Active = null;

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
            if (_objectiveAnchor) _objectiveAnchor.position = pos; // the standard arrow points at the gate
        }

        void OnGateActivated(SwapToy toy)
        {
            if (_phase != RunPhase.AwaitingGate || _benched) return;

            var stroke = CurrentStroke;
            RequestStrokeDomain(stroke.Domain);

            _phase = RunPhase.Painting;
            _pointIndex = 1; // checkpoint 0 IS the gate - flying it consumes the stroke's start
            // The ridden stroke's line EASES away (continuity law) - EaseTransitions drives
            // _lineFade toward 0 while this stroke is the fading one.
            int prevFade = _fadeIndex;
            _fadeIndex = stroke.Index;
            _lineFade = 1f;
            if (prevFade >= 0 && prevFade != stroke.Index && prevFade < _strokes.Length)
                ApplyGhostStyle(_strokes[prevFade]); // finalize an interrupted fade at full style
            SpawnMilestone();

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
                domain = gameData.LocalPlayer?.Domain ?? domain; // pick was rejected - record reality

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
        /// (pooled, grow-in animation - nothing pops), streamed over frames so a monument-sized
        /// restore reads as the painting growing back rather than a hitch.
        /// </summary>
        async UniTaskVoid RestorePrismsAsync(CancellationToken ct)
        {
            var records = PaintingPrismStore.GetPrisms(_painting.PaintingId, _strokeIndex);
            if (records.Count == 0) return;

            // Wait for a local vessel - its controller carries the factory channel + owner name.
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
                                   $"prisms of '{_painting.DisplayName}' - a recorded prism pool is unavailable.");
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
            // A scene whose factory lacks the recorded pool returns null - regrow as the generic
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
                    // While AWAITING the gate the next stroke shows faintly (something to aim at);
                    // once you are RIDING it the line eases away entirely - the ride is the rings
                    // and your own trail, not a line rendering. _lineFade carries the ease.
                    c.a = 0.45f;
                    width = 1.1f;
                    break;
                case GhostStyle.Done:
                    // Dimmed solid - across sessions this is the "memory" of already-painted strokes.
                    c = new Color(c.r * 0.65f, c.g * 0.65f, c.b * 0.65f, 0.30f);
                    width = 1.1f;
                    break;
                default:
                    c.a = 0.13f;
                    width = 0.9f;
                    break;
            }

            if (stroke.Index == _fadeIndex) c.a *= _lineFade;
            c.a *= _bloom * _benchVisibility;
            lr.startColor = lr.endColor = c;
            lr.startWidth = lr.endWidth = width;
        }

        // ── Ride milestones - rings you fly THROUGH, sized as their own hit volume ──

        float MilestoneRadius(StrokeInfo stroke) => Mathf.Max(18f, stroke.Reach * 1.8f);

        /// <summary>
        /// The ONE live milestone: a ring gate at the current checkpoint, faced along the local
        /// flight tangent, whose SphereCollider trigger is scaled to the ring radius - flying
        /// through the ring IS the hit test. The final milestone carries the trail-off jack in its
        /// centre; the trail-on cone appears only on the stroke's start gate.
        /// </summary>
        void SpawnMilestone()
        {
            DespawnMilestone();
            var stroke = CurrentStroke;
            var cps = stroke.Checkpoints;
            if (_pointIndex >= cps.Count) return;

            int ptIdx = cps[_pointIndex];
            Vector3 pos = stroke.Points[ptIdx];
            bool isStrokeEnd = _pointIndex == cps.Count - 1;
            float ringR = MilestoneRadius(stroke);
            // The standard arrow tracks the current checkpoint - the anchor (not the ring itself)
            // so the pointer survives the ring folding away on disengage.
            if (_objectiveAnchor) _objectiveAnchor.position = pos;

            _milestone = new GameObject($"Milestone_{_pointIndex}");
            _milestone.transform.SetParent(transform, false);
            _milestone.transform.position = pos;
            Vector3 tangent = stroke.Points[Mathf.Min(ptIdx + 1, stroke.Points.Length - 1)]
                              - stroke.Points[Mathf.Max(ptIdx - 1, 0)];
            _milestone.transform.rotation = tangent.sqrMagnitude > 1e-4f
                ? Quaternion.LookRotation(tangent.normalized, Vector3.up)
                : _rotation;

            var prismMaterial = ToyFactory.DomainPrismMaterial(_context, stroke.Domain);
            // A milestone is a switch like any other: the ring you thread, at the radius of the
            // trigger below that fires it.
            ToyFactory.AddSwitchRing(_milestone.transform, ringR, stroke.BaseColor, prismMaterial);
            if (isStrokeEnd)
                ToyFactory.AddJackBody(_milestone.transform, ringR * 0.45f, stroke.BaseColor, prismMaterial);

            var trigger = _milestone.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = ringR;   // the hit volume IS the ring

            _milestoneTrigger = _milestone.AddComponent<StrokeMilestoneTrigger>();
            _milestone.transform.localScale = Vector3.zero; // EaseTransitions grows it in
        }

        /// <summary>Sweep past the current ring: it folds away, the next one blooms (or the stroke ends).</summary>
        void AdvanceMilestone()
        {
            var passed = _milestone;
            _milestone = null;
            _milestoneTrigger = null;
            if (passed) ToyFactory.ScaleOutAndDestroy(passed, GateDespawnSeconds).Forget();

            _pointIndex++;
            if (_pointIndex >= CurrentStroke.Checkpoints.Count)
            {
                CompleteStroke();
                return;
            }
            SpawnMilestone();
        }

        void DespawnMilestone()
        {
            if (!_milestone) return;
            ToyFactory.ScaleOutAndDestroy(_milestone, GateDespawnSeconds).Forget();
            _milestone = null;
            _milestoneTrigger = null;
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

    /// <summary>
    /// Routes the one shared <see cref="ObjectiveIndicator"/> at whichever runner currently holds
    /// the brush (at most one is unbenched at a time - <see cref="PaintingRunner"/> claims on
    /// begin/unbench and releases on bench/celebrate/destroy).
    /// </summary>
    class PaintingObjectiveRelay : IObjectiveProvider
    {
        public PaintingRunner Active;

        public bool TryGetObjective(out Transform target)
        {
            target = null;
            return Active && Active.TryGetObjective(out target);
        }
    }

    /// <summary>
    /// Trigger on a ride-milestone ring: trips when the LOCAL player's vessel enters its sphere
    /// (scaled to the ring radius). The <see cref="PaintingRunner"/> polls <see cref="Tripped"/>
    /// from Update, keeping all effects out of the physics callback.
    /// </summary>
    class StrokeMilestoneTrigger : MonoBehaviour
    {
        public bool Tripped { get; private set; }

        void OnTriggerEnter(Collider other)
        {
            // Same local-vessel resolution the toy gates use - one rule, one implementation.
            if (Toy.TryGetLocalVessel(other, out _)) Tripped = true;
        }
    }
}
