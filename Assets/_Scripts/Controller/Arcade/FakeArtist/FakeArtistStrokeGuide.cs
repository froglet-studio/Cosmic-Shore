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
        const float DimmedDotScale = 0.65f;

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

        // Markers of the CURRENT stroke, index-aligned with its dots (retired entries -> null).
        readonly List<GameObject> _dotMarkers = new();
        // Per-marker animation target: each marker eases toward its stored scale (bloom-in
        // to full for the current dot, to DimmedDotScale for the rest) - a single shared
        // bloomer target would drag dimmed dots back to full size.
        readonly Dictionary<Transform, float> _markerTargets = new();
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
            ClearStrokeMarkers();
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
            StrokesCompletedCount = 0;
            _running = true;
            ObjectiveRelay.Active = this;
            SpawnStrokeMarkers();
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

            // Latched the current dot.
            RetireMarkerForDot(_dotIndex);

            if (_dotIndex == 0)
                SetPen(false); // pen-down: the stroke starts here

            _dotIndex++;

            if (_dotIndex >= dots.Length)
                CompleteStroke();
            else
                HighlightCurrentDot();
        }

        void CompleteStroke()
        {
            SetPen(true);
            ClearStrokeMarkers();

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

            SpawnStrokeMarkers();
        }

        // ── Markers ─────────────────────────────────────────────────────────

        void SpawnStrokeMarkers()
        {
            ClearStrokeMarkers();
            _dotIndex = 0;

            var dots = _strokeDots[_strokeIndex];
            float ringR = RingRadius;

            for (int i = 0; i < dots.Length; i++)
            {
                bool isStart = i == 0;
                bool isEnd = i == dots.Length - 1;

                // The fake artist's degraded guide: the deal already reduced their
                // strokes to [start, end], so every received dot renders.
                var tangent = TangentAt(dots, i);
                var root = new GameObject($"Dot_{_strokeIndex}_{i}");
                root.transform.SetParent(transform, false);
                root.transform.SetPositionAndRotation(dots[i],
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
                _dotMarkers.Add(root);
            }

            HighlightCurrentDot();
        }

        /// <summary>Eases every live marker toward its stored target scale (continuity of motion).</summary>
        void AnimateMarkers()
        {
            foreach (var marker in _dotMarkers)
            {
                if (marker == null) continue;
                var t = marker.transform;
                float target = _markerTargets.TryGetValue(t, out var s) ? s : 1f;
                t.localScale = Vector3.Lerp(t.localScale, Vector3.one * target, Time.deltaTime * BloomLerpSpeed);
            }
        }

        void HighlightCurrentDot()
        {
            for (int i = 0; i < _dotMarkers.Count; i++)
            {
                var marker = _dotMarkers[i];
                if (marker == null) continue;
                _markerTargets[marker.transform] = i == _dotIndex ? 1f : DimmedDotScale;
            }
        }

        void RetireMarkerForDot(int dotIndex)
        {
            if (dotIndex < 0 || dotIndex >= _dotMarkers.Count) return;
            var marker = _dotMarkers[dotIndex];
            _dotMarkers[dotIndex] = null;
            if (marker != null)
            {
                _markerTargets.Remove(marker.transform);
                ToyFactory.ScaleOutAndDestroy(marker, MarkerDespawnSeconds).Forget();
            }
        }

        void ClearStrokeMarkers()
        {
            for (int i = 0; i < _dotMarkers.Count; i++)
            {
                if (_dotMarkers[i] != null)
                {
                    _markerTargets.Remove(_dotMarkers[i].transform);
                    ToyFactory.ScaleOutAndDestroy(_dotMarkers[i], MarkerDespawnSeconds).Forget();
                }
            }
            _dotMarkers.Clear();
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
