using System;
using System.Collections.Generic;
using CosmicShore.UI;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The local player's private "connect the dots" guide for one Fake Artist round:
    /// their 3 assigned strokes as sequences of ride rings ("dots"). Built CLIENT-SIDE
    /// only for the local player from a targeted ClientRpc payload - remote clients never
    /// construct it, which is what keeps stroke assignments (and the fake artist's
    /// degraded guides) secret without any extra work.
    ///
    /// Honest artists see every dot of the current stroke (rings, current target full
    /// size); the FAKE ARTIST sees only the first and last dot of each stroke - they know
    /// where a stroke starts and ends but must improvise the middle.
    ///
    /// Flow per stroke: pen-up while flying to the start ring (cone hub = "trail on,
    /// this way"), pen-down through the dots, pen-up at the end jack. Dot latching is a
    /// per-frame distance check against the current dot only (no colliders - guides cost
    /// ZERO collider budget). All markers bloom in and scale out (continuity law).
    /// Completion is reported through <see cref="StrokeCompleted"/> /
    /// <see cref="AllStrokesCompleted"/>; the controller relays it to the server.
    /// </summary>
    public class FakeArtistStrokeGuide : MonoBehaviour, IObjectiveProvider
    {
        /// <summary>
        /// Static relay so MiniGameHUD's one ObjectiveIndicator can point at the live
        /// guide (PaintingObjectiveRelay pattern - the HUD is created before the guide).
        /// </summary>
        public sealed class GuideObjectiveRelay : IObjectiveProvider
        {
            public FakeArtistStrokeGuide Active;

            public bool TryGetObjective(out Transform target)
            {
                target = null;
                return Active != null && Active.TryGetObjective(out target);
            }
        }

        public static readonly GuideObjectiveRelay ObjectiveRelay = new();

        const float BloomLerpSpeed = 6f;
        const float MarkerDespawnSeconds = 0.45f;

        /// <summary>Raised once per completed stroke (index into this player's bundle).</summary>
        public event Action<int> StrokeCompleted;

        /// <summary>Raised once when every assigned stroke is complete.</summary>
        public event Action AllStrokesCompleted;

        public int StrokesCompletedCount { get; private set; }
        public int StrokeCount => _strokeDots.Count;
        public bool IsFinished { get; private set; }

        GameDataSO _gameData;
        readonly List<Vector3[]> _strokeDots = new();
        float _reach;
        bool _isImposter;
        Color _accent;
        Material _prismMaterial;

        int _strokeIndex;
        int _dotIndex;              // index of the NEXT dot to latch in the current stroke
        bool _running;
        bool _penHeld;              // true while this guide is holding the pen up

        // Only ONE ring is shown at a time - the immediate NEXT dot to fly to, never the
        // whole stroke's dots. It blooms in on spawn and scales out when latched.
        GameObject _currentMarker;
        Transform _objectiveAnchor;

        /// <summary>
        /// <paramref name="strokeDots"/>: per stroke, the world-space dot positions
        /// (already anchored). The fake artist receives first+last only - the DEAL does
        /// the degrading server-side, so even this client's memory holds no middle dots.
        /// </summary>
        public void Configure(GameDataSO gameData, List<Vector3[]> strokeDots, float reach,
            bool isImposter, Color accent, Material prismMaterial)
        {
            _gameData = gameData;
            _strokeDots.Clear();
            foreach (var dots in strokeDots)
            {
                if (dots != null && dots.Length >= 2)
                    _strokeDots.Add(dots);
            }
            _reach = Mathf.Max(4f, reach);
            _isImposter = isImposter;
            _accent = accent;
            _prismMaterial = prismMaterial;

            _objectiveAnchor = new GameObject("GuideObjectiveAnchor").transform;
            _objectiveAnchor.SetParent(transform, false);
        }

        /// <summary>
        /// Ends the drawing phase early (vote started / timer expired): pen up, markers
        /// fold away, no further latching. Unfinished strokes stay unfinished.
        /// </summary>
        public void EndDrawing()
        {
            if (!_running && IsFinished) return;
            _running = false;
            SetPen(true);
            RetireCurrentMarker();
            if (ObjectiveRelay.Active == this) ObjectiveRelay.Active = null;
        }

        /// <summary>Starts the run (call when the drawing phase begins).</summary>
        public void Begin()
        {
            if (_strokeDots.Count == 0)
            {
                IsFinished = true;
                AllStrokesCompleted?.Invoke();
                return;
            }

            _strokeIndex = 0;
            _dotIndex = 0;
            StrokesCompletedCount = 0;
            _running = true;
            ObjectiveRelay.Active = this;
            SpawnCurrentMarker(); // only the first (start) ring
            SetPen(true); // pen-up until the first start ring
        }

        public bool TryGetObjective(out Transform target)
        {
            target = _objectiveAnchor;
            return _running && !IsFinished && _objectiveAnchor != null;
        }

        float RingRadius => Mathf.Max(18f, _reach * 1.8f);

        void Update()
        {
            AnimateMarkers();

            if (!_running || IsFinished) return;

            var vessel = ResolveLocalVessel();
            if (vessel == null) return;

            var dots = _strokeDots[_strokeIndex];
            var target = dots[Mathf.Min(_dotIndex, dots.Length - 1)];
            if (_objectiveAnchor != null) _objectiveAnchor.position = target;

            float latch = RingRadius;
            if ((vessel.Transform.position - target).sqrMagnitude > latch * latch)
                return;

            // Latched the current dot - fold this ring away.
            RetireCurrentMarker();

            if (_dotIndex == 0)
                SetPen(false); // pen-down: the stroke starts here

            _dotIndex++;

            if (_dotIndex >= dots.Length)
                CompleteStroke();
            else
                SpawnCurrentMarker(); // reveal the NEXT ring only
        }

        void CompleteStroke()
        {
            SetPen(true);
            RetireCurrentMarker();

            int completed = _strokeIndex;
            StrokesCompletedCount = completed + 1;
            _strokeIndex++;
            StrokeCompleted?.Invoke(completed);

            if (_strokeIndex >= _strokeDots.Count)
            {
                IsFinished = true;
                _running = false;
                if (ObjectiveRelay.Active == this) ObjectiveRelay.Active = null;
                // Pen stays UP: a finished artist doesn't doodle over the gallery while
                // others draw. Released when this guide is destroyed (next round's deal).
                AllStrokesCompleted?.Invoke();
                return;
            }

            _dotIndex = 0;
            SpawnCurrentMarker();
        }

        // ── Marker (one ring at a time) ─────────────────────────────────────

        /// <summary>
        /// Show ONLY the current target ring - never the whole stroke's dots. The start
        /// dot carries a cone ("trail on, this way") + label; the end dot carries a jack
        /// ("trail off"). Blooms in from zero scale.
        /// </summary>
        void SpawnCurrentMarker()
        {
            RetireCurrentMarker();

            var dots = _strokeDots[_strokeIndex];
            if (_dotIndex < 0 || _dotIndex >= dots.Length) return;

            bool isStart = _dotIndex == 0;
            bool isEnd = _dotIndex == dots.Length - 1;
            float ringR = RingRadius;

            var tangent = TangentAt(dots, _dotIndex);
            var root = new GameObject($"Ring_{_strokeIndex}_{_dotIndex}");
            root.transform.SetParent(transform, false);
            root.transform.SetPositionAndRotation(dots[_dotIndex],
                tangent.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(tangent) : Quaternion.identity);

            ToyFactory.AddRingBody(root.transform, ringR, _accent, _prismMaterial);
            if (isStart)
            {
                ToyFactory.AddConeBody(root.transform, ringR * 0.22f, ringR * 0.66f, _accent, _prismMaterial);
                ToyFactory.AddLabel(root.transform, $"{_strokeIndex + 1}/{_strokeDots.Count}", _accent, ringR * 1.4f);
            }
            if (isEnd)
                ToyFactory.AddJackBody(root.transform, ringR * 0.45f, _accent, _prismMaterial);

            root.transform.localScale = Vector3.zero;
            _currentMarker = root;
        }

        /// <summary>Blooms the single live ring toward full scale (continuity of motion).</summary>
        void AnimateMarkers()
        {
            if (_currentMarker == null) return;
            var t = _currentMarker.transform;
            t.localScale = Vector3.Lerp(t.localScale, Vector3.one, Time.deltaTime * BloomLerpSpeed);
        }

        void RetireCurrentMarker()
        {
            if (_currentMarker == null) return;
            ToyFactory.ScaleOutAndDestroy(_currentMarker, MarkerDespawnSeconds).Forget();
            _currentMarker = null;
        }

        static Vector3 TangentAt(Vector3[] dots, int i)
        {
            int next = Mathf.Min(i + 1, dots.Length - 1);
            int prev = Mathf.Max(i - 1, 0);
            return dots[next] - dots[prev];
        }

        // ── Pen ─────────────────────────────────────────────────────────────

        void SetPen(bool up)
        {
            var pen = ResolveLocalPen();
            if (pen == null) return;
            pen.SetSpawnerPaused(up);
            _penHeld = up;
        }

        /// <summary>The one hard rule: never leave the local pen held up.</summary>
        void RestorePen()
        {
            if (!_penHeld) return;
            var pen = ResolveLocalPen();
            if (pen != null) pen.SetSpawnerPaused(false);
            _penHeld = false;
        }

        IVesselStatus ResolveLocalVessel()
        {
            var vs = _gameData?.LocalPlayer?.Vessel?.VesselStatus;
            if (vs == null || (vs is UnityEngine.Object o && !o)) return null;
            return vs;
        }

        VesselPrismController ResolveLocalPen() => ResolveLocalVessel()?.VesselPrismController;

        void OnDestroy()
        {
            if (ObjectiveRelay.Active == this) ObjectiveRelay.Active = null;
            RestorePen();
        }
    }
}
