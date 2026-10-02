using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The race, in a racing card's preview window: the mode's own rings stood in the satellite
    /// arena, threaded in order by the one pilot flying it.
    ///
    /// <para><b>The same course the match lays.</b> The rings come from the mode's
    /// <see cref="RaceCourseSource"/> - the object <c>GateRaceController</c> builds its own course
    /// from - with the shipped defaults (<see cref="RaceCourseSource.For"/>). The seed is
    /// <see cref="ModePreviewPlantingModel.StableSeed"/> of the mode's name, the same seed the
    /// card art is rendered from, so at intensity 2 the rings you fly are the ones on the card.</para>
    ///
    /// <para><b>Strictly local, and it writes nothing it does not own.</b> A preview runs in
    /// Menu_Main beside a party that may be connected, so a crossing is counted HERE, on
    /// <see cref="Threaded"/>, and never on <c>RoundStats.SwitchesThreaded</c>: that stat lives on
    /// the persistent, replicated Player and is the match's scoring token. The session feeds this
    /// count to the objective box instead.</para>
    ///
    /// <para><b>Detection is the match's, verbatim in shape</b>: one segment test per frame against
    /// the pilot's NEXT ring only (<see cref="RaceGateRing.CrossedMouth"/>), a teleport threads
    /// nothing (<c>VesselTransformer.TeleportCount</c>, compared rather than measured), and an
    /// implausibly long step is a respawn rather than a flight. A finished race starts again from
    /// gate 0 - a preview is a place you keep flying, so the course is a loop you can practise.</para>
    ///
    /// <para>Mass is untouched: rings are markers (one renderer each, zero colliders), and they
    /// bloom in and wither out rather than popping (continuity of existence).</para>
    /// </summary>
    public sealed class ModePreviewGateCourse : MonoBehaviour
    {
        /// <summary>Longest believable single-frame step, units per second. Generous on purpose:
        /// it has to pass every hull that can fly a gate race (a Time-10 Manta is ~1,400 u/s) and
        /// exists only to reject a respawn or an eject.</summary>
        const float MaxPlausibleSpeed = 1600f;

        const float BloomSeconds = 0.9f;
        const float RetireSeconds = 0.4f;

        /// <summary>A gate threaded: (ring index, gates threaded this preview).</summary>
        public event Action<int, int> OnGateThreaded;

        /// <summary>A lap closed: (lap number within the race, 1-based; seconds that lap took).</summary>
        public event Action<int, float> OnLapCompleted;

        /// <summary>The whole race closed: (seconds it took). The course then starts again.</summary>
        public event Action<float> OnRaceCompleted;

        /// <summary>Gates threaded over this component's life - the objective box's count, read as a
        /// delta from a baseline. Monotonic: never reset, not even by a re-raise.</summary>
        public int Threaded { get; private set; }

        /// <summary>True while rings are standing.</summary>
        public bool IsRaised => _rings.Count > 0;

        /// <summary>Threadings that finish one race of this course.</summary>
        public int RaceLength { get; private set; }

        /// <summary>Rings standing (fewer than <see cref="RaceLength"/> on a lapped course).</summary>
        public int RingCount => _rings.Count;

        readonly List<RaceGate> _course = new();
        readonly List<RaceGate> _localCourse = new();
        Pose _startPose;
        bool _hasStartPose;

        /// <summary>The source this course was built from, while raised.</summary>
        public RaceCourseSource Source => _source;

        /// <summary>The course in the CELL's frame (no arena offset) - what a structure hung on the
        /// course is posed with.</summary>
        public IReadOnlyList<RaceGate> CellLocalCourse => _localCourse;

        /// <summary>
        /// Where a pilot starts this race, in world space: <see cref="RaceCourseSource.StartLineStandoff"/>
        /// behind the source's start line, pointed through it. False when the course is down or
        /// the source has no line.
        /// </summary>
        public bool TryGetStartPose(out Pose pose)
        {
            pose = _startPose;
            return _hasStartPose && IsRaised;
        }
        /// <summary>Laps closed for this component's whole life - monotonic like
        /// <see cref="Threaded"/>, so a drill condition can baseline it.</summary>
        public int LapsCompleted { get; private set; }

        /// <summary>
        /// The ring the local pilot must thread next: its centre, in world space. False while no
        /// course stands.
        /// </summary>
        public bool TryGetNextGate(out Vector3 position)
        {
            position = default;
            if (_litRing < 0 || _litRing >= _rings.Count || !_rings[_litRing]) return false;
            position = _rings[_litRing].transform.position;
            return true;
        }

        readonly List<RaceGateRing> _rings = new();
        RaceCourseSource _source;
        int _intensity;
        Transform _root;

        IVessel _pilot;
        Vector3 _lastPosition;
        bool _hasLastPosition;
        int _lastTeleports;

        int _raceThreaded;      // threadings in the CURRENT race - wraps to 0 at the finish
        int _litRing = -1;
        float _raceStart;
        float _lapStart;
        int _lap;

        /// <summary>
        /// Stand the course for <paramref name="mode"/> around <paramref name="origin"/> (the
        /// satellite cell's centre). False - and nothing stood - for a mode that is not a gate
        /// race, or when the course cannot be built (an arena-built mode whose config carries no
        /// arena); the reason is logged once.
        /// </summary>
        public bool Raise(GameModes mode, int intensity, CellConfigDataSO config, float nucleusRadius,
                          Vector3 origin, ThemeManagerDataContainerSO theme)
        {
            Strike();

            _source = RaceCourseSource.For(mode);
            if (_source == null) return false;

            _intensity = Mathf.Clamp(intensity, 1, 4);
            _source.ResolveShell(nucleusRadius, out float inner, out float outer);
            int seed = ModePreviewPlantingModel.StableSeed(mode.ToString()) & 0x7FFFFFFF;
            int target = _source.AuthoredGateTarget(_intensity);

            var request = new RaceCourseRequest(seed, _intensity, target, inner, outer, config);
            var course = _source.Build(request, out string failure);
            if (course == null || course.Count == 0)
            {
                CSDebug.LogWarning($"[ModePreview] {mode}: no gate course in the preview - {failure}");
                _source = null;
                return false;
            }

            _localCourse.AddRange(course);
            _hasStartPose = false;
            if (_source.TryStartLine(request, course, out var lineTarget, out var lineAxis))
            {
                Vector3 axisN = lineAxis.normalized;
                Vector3 up = Mathf.Abs(Vector3.Dot(axisN, Vector3.up)) > 0.98f ? Vector3.forward : Vector3.up;
                _startPose = new Pose(origin + lineTarget - axisN * _source.StartLineStandoff,
                                      Quaternion.LookRotation(axisN, up));
                _hasStartPose = true;
            }

            _root = new GameObject($"ModePreviewGateCourse ({mode})").transform;
            _root.SetParent(transform, false);

            for (int i = 0; i < course.Count; i++)
            {
                var gate = new RaceGate(course[i].Position + origin, course[i].Axis, course[i].Radius);
                _course.Add(gate);

                var go = new GameObject($"Gate_{i + 1:00}");
                go.transform.SetParent(_root, false);
                var ring = go.AddComponent<RaceGateRing>();
                ring.Build(i, gate, theme, BloomSeconds, RaceGateRing.FindCoincident(_course, _rings, i));
                _rings.Add(ring);
            }

            // Threaded is NOT reset: it counts for this component's whole life and is read as a
            // delta by the objective runner, so re-standing the course (an intensity nudge) must
            // not make the count run backwards under a baseline taken before it.
            RaceLength = _source.RaceLengthFor(_rings.Count, _intensity);
            ResetRace();

            CSDebug.LogVerbose(CSLogChannel.ArcadeLaunch,
                $"[ModePreview] {mode}: {_rings.Count} rings, race of {RaceLength}, intensity {_intensity}.");
            return true;
        }

        /// <summary>
        /// The vessel whose crossings count - the local pilot's, from the moment they take the
        /// stick. Null stops counting (tapping out) without taking the rings down, so tapping
        /// back in resumes the same course. Re-tracking restarts the race clock.
        /// </summary>
        public void Track(IVessel pilot)
        {
            _pilot = pilot;
            _hasLastPosition = false;
            if (pilot != null) ResetRace();
        }

        /// <summary>Take the rings down (they wither). Idempotent.</summary>
        public void Strike()
        {
            for (int i = 0; i < _rings.Count; i++)
                if (_rings[i]) _rings[i].Retire(RetireSeconds);

            // The root outlives its rings by the wither; each ring destroys itself when done.
            if (_root) Destroy(_root.gameObject, RetireSeconds + 0.1f);
            _root = null;

            _rings.Clear();
            _course.Clear();
            _localCourse.Clear();
            _hasStartPose = false;
            _pilot = null;
            _hasLastPosition = false;
            _litRing = -1;
            RaceLength = 0;
            _source = null;
        }

        void OnDestroy() => Strike();

        void ResetRace()
        {
            _raceThreaded = 0;
            _lap = 0;
            _raceStart = _lapStart = Time.unscaledTime;
            LightNext();
        }

        void Update()
        {
            if (_rings.Count == 0 || _pilot == null) return;

            // A destroyed vessel still passes `!= null` through the interface - ask the object.
            if (_pilot is UnityEngine.Object uo && !uo) { _pilot = null; return; }
            var t = _pilot.Transform;
            if (!t) return;

            Vector3 cur = t.position;
            int teleports = TeleportCountOf(_pilot);
            if (!_hasLastPosition)
            {
                _lastPosition = cur;
                _lastTeleports = teleports;
                _hasLastPosition = true;
                return;
            }

            Vector3 prev = _lastPosition;
            _lastPosition = cur;

            // A jump threads nothing on the line between its ends (see GateRaceController).
            if (teleports != _lastTeleports) { _lastTeleports = teleports; return; }

            // Unscaled, like the rest of the preview: the menu is free to touch timeScale.
            float maxStep = MaxPlausibleSpeed * Mathf.Max(Time.unscaledDeltaTime, Time.deltaTime) * 2f + 5f;
            if ((cur - prev).sqrMagnitude > maxStep * maxStep) return;

            int ringIndex = _source.RingIndexFor(_raceThreaded, _rings.Count, _intensity);
            if (ringIndex < 0 || ringIndex >= _rings.Count) return;

            var ring = _rings[ringIndex];
            if (!ring || !ring.CrossedMouth(prev, cur)) return;

            _raceThreaded++;
            Threaded++;
            OnGateThreaded?.Invoke(ringIndex, Threaded);

            if (IsLapBoundary(_raceThreaded, _rings.Count, _source.LeadInGates, RaceLength))
            {
                float now = Time.unscaledTime;
                _lap++;
                LapsCompleted++;
                OnLapCompleted?.Invoke(_lap, now - _lapStart);
                _lapStart = now;
            }

            if (_raceThreaded >= RaceLength)
            {
                OnRaceCompleted?.Invoke(Time.unscaledTime - _raceStart);
                ResetRace();
                return;
            }

            LightNext();
        }

        /// <summary>
        /// A lap closes each time the lapped tail has been flown once more: after the lead-in plus
        /// a whole number of passes over the lapped rings. An open chain's only lap is the race.
        /// </summary>
        public static bool IsLapBoundary(int threadedInRace, int ringCount, int leadIn, int raceLength)
        {
            int lead = Mathf.Clamp(leadIn, 0, ringCount);
            int lapped = ringCount - lead;
            if (lapped <= 0) return threadedInRace >= raceLength;
            int past = threadedInRace - lead;
            return past > 0 && past % lapped == 0;
        }

        void LightNext()
        {
            int next = _rings.Count > 0 ? _source.RingIndexFor(_raceThreaded, _rings.Count, _intensity) : -1;
            if (next == _litRing) return;

            if (_litRing >= 0 && _litRing < _rings.Count && _rings[_litRing])
                _rings[_litRing].SetIsNextForLocalPilot(false);
            if (next >= 0 && next < _rings.Count && _rings[next])
                _rings[next].SetIsNextForLocalPilot(true);

            _litRing = next;
        }

        static int TeleportCountOf(IVessel vessel)
        {
            var transformer = vessel?.VesselStatus?.VesselTransformer;
            return transformer ? transformer.TeleportCount : 0;
        }
    }
}
