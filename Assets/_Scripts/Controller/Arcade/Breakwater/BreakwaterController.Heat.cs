using System.Collections.Generic;
using System.Globalization;
using CosmicShore.Data;
using CosmicShore.UI;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Breakwater's heat (<see cref="BreakwaterHeat"/>): the LOCAL pilot's stations graded by the verb they chose and
    /// called, in the world first. A thread through the eye throws a shock ring off the eye; a hot thread (the
    /// afterburner lit) throws a bigger one; a streak of eyes is called; every lap is split; a lap flown through every
    /// eye runs a chase of shock rings round the circuit. One start hint.
    ///
    /// <para><b>Local and unscored.</b> Every peer builds its own course, so a flare belongs to the one viewer who
    /// earned it, and nothing here touches the race's score or the network. The grade is read from the local hull's own
    /// motion through the station it is flying at, the same plane test the race credits. The vessel is untouched, and
    /// the start gate (the lead-in) is not graded: it has no eye.</para>
    ///
    /// <para>Every word is a toast template on <c>GameToastConfig_Breakwater</c> (430-436), and every sound an
    /// <see cref="FMODUnity.EventReference"/> below, shipped EMPTY.</para>
    /// </summary>
    public partial class BreakwaterController
    {
        [Header("Heat (BreakwaterHeat): the local pilot's stations, graded and called")]
        [Tooltip("Grade and call the local pilot's stations (shock rings, streaks, lap splits, the start hint). Off = " +
                 "the race exactly as before, with no heat.")]
        [SerializeField] bool heatOn = true;

        [Tooltip("How far an eye thread's shock ring grows, as a multiple of the eye.")]
        [SerializeField, Min(1f)] float eyeFlarePunch = 2.2f;

        [Tooltip("How far a hot thread's shock ring grows (the afterburner lit through the eye), as a multiple of the eye.")]
        [SerializeField, Min(1f)] float hotEyeFlarePunch = 3.6f;

        [Tooltip("Seconds a shock ring takes to grow and vanish.")]
        [SerializeField, Min(0.05f)] float flareSeconds = 0.45f;

        [Tooltip("Seconds between the rings of a clean lap's chase, in course order.")]
        [SerializeField, Min(0f)] float chaseStagger = 0.06f;

        [Tooltip("The most shock rings standing at once (the oldest is cut short past it).")]
        [SerializeField, Min(1)] int maxFlares = 24;

        [Header("Heat audio (FMOD; every slot shipped empty)")]
        [Tooltip("A thread through the eye. Shipped empty (FMOD convention).")]
        [SerializeField] FMODUnity.EventReference eyeThreadEvent;
        [Tooltip("A thread through the eye with the afterburner lit. Shipped empty (FMOD convention).")]
        [SerializeField] FMODUnity.EventReference hotEyeThreadEvent;
        [Tooltip("A pass through a door that was fired or sawn open. Shipped empty (FMOD convention).")]
        [SerializeField] FMODUnity.EventReference doorPassEvent;
        [Tooltip("A streak of eyes called (3, 7, 14, then every 14). Shipped empty (FMOD convention).")]
        [SerializeField] FMODUnity.EventReference streakCalledEvent;
        [Tooltip("A lap completed. Shipped empty (FMOD convention).")]
        [SerializeField] FMODUnity.EventReference lapCompleteEvent;
        [Tooltip("A lap that beat this race's best. Shipped empty (FMOD convention).")]
        [SerializeField] FMODUnity.EventReference bestLapEvent;
        [Tooltip("A lap flown through every eye (the chase). Shipped empty (FMOD convention).")]
        [SerializeField] FMODUnity.EventReference cleanLapEvent;
        [Tooltip("A port missed: the station's plane crossed outside it. Shipped empty (FMOD convention).")]
        [SerializeField] FMODUnity.EventReference missedPortEvent;

        sealed class Flare
        {
            public Transform Root;
            public float StartAt;
            public float Punch;
        }

        // The flares stand under scene objects of their own and go with the scene, as the arena does.
        readonly List<Flare> _flares = new();
        int _heatThreaded = -1;            // crossings the local pilot has made, as graded here (-1 = not started)
        int _streak;
        bool _lapAllEyes = true;
        float _lapStartedAt;
        float _bestLap = -1f;
        bool _hintPosted, _eyeTaught, _hotTaught;
        Vector3 _heatPrev;
        bool _hasHeatPrev;

        void ResetHeatCounters()
        {
            _heatThreaded = -1;
            _streak = 0;
            _lapAllEyes = true;
            _bestLap = -1f;
            _hintPosted = _eyeTaught = _hotTaught = _hasHeatPrev = false;
        }

        /// <summary>Every frame (the controller's LateUpdate): the flares, then the local pilot's crossing.</summary>
        void TickHeat()
        {
            TickFlares();
            if (!heatOn || _course.Count == 0 || gameData == null || !gameData.IsTurnRunning)
            {
                _hasHeatPrev = false;
                // A second turn in the same scene starts its heat afresh (the flares finish on their own).
                if (_heatThreaded >= 0 && gameData != null && !gameData.IsTurnRunning) ResetHeatCounters();
                return;
            }
            var local = gameData.LocalPlayer;
            var status = local?.Vessel?.VesselStatus;
            var tf = local?.Vessel?.Transform;
            if (status == null || !tf) { _hasHeatPrev = false; return; }

            int credited = local.RoundStats?.SwitchesThreaded ?? 0;
            if (_heatThreaded < 0)
            {
                _heatThreaded = credited;
                _lapStartedAt = Time.time;
            }

            // The crossing FIRST, against the station the heat is on: the race credits a thread in its own Update, which
            // may run before this LateUpdate, and resyncing first would test this frame's crossing against the NEXT one.
            Vector3 pos = tf.position;
            if (_hasHeatPrev && _heatThreaded < RaceLength)
            {
                int ring = RingIndexFor(_heatThreaded);
                var gate = _course[ring];
                float offset = BreakwaterHeat.CrossingOffset(_heatPrev, pos, gate.Position, gate.Axis);
                if (BreakwaterHeat.IsAnAttempt(offset, gate.Radius))
                    OnLocalCrossing(local, BreakwaterHeat.Grade(offset, gate.Radius, status.IsBoosting), ring);
            }
            _heatPrev = pos;
            _hasHeatPrev = true;

            // Then follow the race: never more than one crossing ahead of its count, never behind it.
            if (credited < _heatThreaded - 1 || credited > _heatThreaded)
            {
                if (credited < _heatThreaded) _streak = 0;
                _heatThreaded = credited;
            }

            if (!_hintPosted)
            {
                _hintPosted = true;
                GameToastAPI.Post(GameToastSituation.BreakwaterHint, local.Domain);
            }
        }

        void OnLocalCrossing(IPlayer local, BreakwaterThreadGrade grade, int ring)
        {
            bool isStation = ring >= LeadInGates;      // the start gate is an open hoop: counted, never graded
            if (grade == BreakwaterThreadGrade.Missed)
            {
                if (!isStation) return;
                _streak = 0;
                _lapAllEyes = false;
                Play(missedPortEvent);
                return;
            }

            _heatThreaded++;
            if (isStation) CallStation(local, grade, ring);

            int lap = BreakwaterHeat.LapEndedBy(_heatThreaded, _rings.Count, LeadInGates);
            if (_heatThreaded == LeadInGates) _lapStartedAt = Time.time;      // the laps start at the start gate
            if (lap <= 0) return;
            float split = Time.time - _lapStartedAt;
            _lapStartedAt = Time.time;
            string lapText = lap.ToString(CultureInfo.InvariantCulture), splitText = split.ToString("0.0", CultureInfo.InvariantCulture);
            if (_bestLap > 0f && split < _bestLap)
            {
                GameToastAPI.Post(GameToastSituation.BreakwaterBestLap, local.Domain, lapText, splitText);
                Play(bestLapEvent);
            }
            else
            {
                GameToastAPI.Post(GameToastSituation.BreakwaterLapSplit, local.Domain, lapText, splitText);
                Play(lapCompleteEvent);
            }
            if (_bestLap < 0f || split < _bestLap) _bestLap = split;
            if (_lapAllEyes)
            {
                GameToastAPI.Post(GameToastSituation.BreakwaterCleanLap, local.Domain, lapText);
                Play(cleanLapEvent);
                for (int i = LeadInGates; i < _rings.Count; i++)
                    FlareEye(i, hotEyeFlarePunch, (i - LeadInGates) * chaseStagger);
            }
            _lapAllEyes = true;
        }

        void CallStation(IPlayer local, BreakwaterThreadGrade grade, int ring)
        {
            _streak = BreakwaterHeat.StreakAfter(_streak, grade);
            if (grade == BreakwaterThreadGrade.Door)
            {
                _lapAllEyes = false;
                Play(doorPassEvent);
                return;
            }

            bool hot = grade == BreakwaterThreadGrade.HotEye;
            FlareEye(ring, hot ? hotEyeFlarePunch : eyeFlarePunch, 0f);
            Play(hot ? hotEyeThreadEvent : eyeThreadEvent);
            if (hot && !_hotTaught)
            {
                _hotTaught = _eyeTaught = true;
                GameToastAPI.Post(GameToastSituation.BreakwaterHotThread, local.Domain);
            }
            else if (!_eyeTaught)
            {
                _eyeTaught = true;
                GameToastAPI.Post(GameToastSituation.BreakwaterEyeThread, local.Domain);
            }
            if (BreakwaterHeat.IsCalled(_streak))
            {
                GameToastAPI.Post(GameToastSituation.BreakwaterStreak, local.Domain,
                                  _streak.ToString(CultureInfo.InvariantCulture));
                Play(streakCalledEvent);
            }
        }

        /// <summary>A shock ring off station <paramref name="ring"/>'s eye: a switch ring the size of the eye (the same
        /// ring the race draws, so its colour is the colour set's) that grows to <paramref name="punch"/> times its size
        /// and vanishes.</summary>
        void FlareEye(int ring, float punch, float delay)
        {
            if (ring < 0 || ring >= _course.Count) return;
            while (_flares.Count >= maxFlares)
            {
                if (_flares[0].Root) Destroy(_flares[0].Root.gameObject);
                _flares.RemoveAt(0);
            }
            var gate = _course[ring];
            var go = new GameObject("BreakwaterFlare");
            go.transform.SetPositionAndRotation(gate.Position,
                Quaternion.LookRotation(gate.Axis.sqrMagnitude > 1e-6f ? gate.Axis : Vector3.forward));
            ToyFactory.AddSwitchRing(go.transform, BreakwaterCourseSettings.EyeRadius, gameData ? gameData.ThemeManagerData : null);
            go.transform.localScale = delay > 0f ? Vector3.zero : Vector3.one;
            _flares.Add(new Flare { Root = go.transform, StartAt = Time.time + delay, Punch = punch });
        }

        void TickFlares()
        {
            float now = Time.time;
            for (int i = _flares.Count - 1; i >= 0; i--)
            {
                var flare = _flares[i];
                if (!flare.Root) { _flares.RemoveAt(i); continue; }
                float t = (now - flare.StartAt) / flareSeconds;
                if (t < 0f) continue;
                if (t >= 1f)
                {
                    Destroy(flare.Root.gameObject);
                    _flares.RemoveAt(i);
                    continue;
                }
                // Out to the punch, and gone: the ring grows the whole time and shrinks to nothing over its last third.
                float grow = Mathf.Lerp(1f, flare.Punch, 1f - (1f - t) * (1f - t));
                float fade = t < 0.67f ? 1f : Mathf.InverseLerp(1f, 0.67f, t);
                flare.Root.localScale = Vector3.one * (grow * fade);
            }
        }

        static void Play(FMODUnity.EventReference sound)
        {
            if (!sound.IsNull) CosmicShore.Core.AudioSystem.Instance.PlaySFXEvent(sound);
        }
    }
}
