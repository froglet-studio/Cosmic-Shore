using System.Collections.Generic;
using System.Globalization;
using CosmicShore.Data;
using CosmicShore.UI;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Redline's heat (<see cref="RedlineHeat"/>): the LOCAL pilot's threads graded and called, in the world first. A
    /// clean thread (through the heart of the mouth) throws a shock ring off the gate, a redhot one (clean at 80% of
    /// full Soar) a bigger one; a streak of clean threads is called; every lap is split and a lap threaded clean end to
    /// end runs a chase of shock rings round the whole circuit; the race's one redline moment (97% of a Time-10 Manta's
    /// full Soar) flares every ring at once.
    ///
    /// <para><b>Local and unscored.</b> Every peer builds its own course, so a flare belongs to the one viewer who earned
    /// it, and nothing here touches the race's score or the network: the grade is read from the local hull's own motion
    /// through the gate it is flying at, the same plane test the race credits (<see cref="RaceGateRing.PlaneCrossing"/>).
    /// The vessel is untouched.</para>
    ///
    /// <para>Every word is a toast template on <c>GameToastConfig_Redline</c> (360-366, authored by
    /// <c>author_redline_assets.py</c>), and every sound an <see cref="FMODUnity.EventReference"/> below, shipped EMPTY.</para>
    /// </summary>
    public partial class RedlineController
    {
        [Header("Heat (RedlineHeat): the local pilot's threads, graded and called")]
        [Tooltip("Grade and call the local pilot's threads (the shock rings, the streak, the lap splits, the redline). " +
                 "Off = the race exactly as before, with no heat.")]
        [SerializeField] bool heatOn = true;

        [Tooltip("How far a clean thread's shock ring grows, as a multiple of its mouth.")]
        [SerializeField, Min(1f)] float cleanFlarePunch = 1.6f;

        [Tooltip("How far a redhot thread's shock ring grows, as a multiple of its mouth.")]
        [SerializeField, Min(1f)] float redhotFlarePunch = 2.6f;

        [Tooltip("Seconds a shock ring takes to grow and vanish.")]
        [SerializeField, Min(0.05f)] float flareSeconds = 0.45f;

        [Tooltip("Seconds between the rings of a chase (a clean lap, the redline), in course order.")]
        [SerializeField, Min(0f)] float chaseStagger = 0.06f;

        [Tooltip("The most shock rings standing at once (the oldest is cut short past it).")]
        [SerializeField, Min(1)] int maxFlares = 24;

        [Header("Heat audio (FMOD; every slot shipped empty)")]
        [Tooltip("A thread through the heart of the mouth. Shipped empty (FMOD convention).")]
        [SerializeField] FMODUnity.EventReference cleanThreadEvent;
        [Tooltip("A clean thread at near-full Soar. Shipped empty (FMOD convention).")]
        [SerializeField] FMODUnity.EventReference redhotThreadEvent;
        [Tooltip("A streak of clean threads called (3, 8, 16, then every 16). Shipped empty (FMOD convention).")]
        [SerializeField] FMODUnity.EventReference streakCalledEvent;
        [Tooltip("A lap completed. Shipped empty (FMOD convention).")]
        [SerializeField] FMODUnity.EventReference lapCompleteEvent;
        [Tooltip("A lap that beat this race's best. Shipped empty (FMOD convention).")]
        [SerializeField] FMODUnity.EventReference bestLapEvent;
        [Tooltip("A lap threaded clean end to end (the chase). Shipped empty (FMOD convention).")]
        [SerializeField] FMODUnity.EventReference cleanLapEvent;
        [Tooltip("A mouth missed: the gate's plane crossed outside it. Shipped empty (FMOD convention).")]
        [SerializeField] FMODUnity.EventReference missedMouthEvent;
        [Tooltip("Soar went dark (Time fell below 4). Shipped empty (FMOD convention).")]
        [SerializeField] FMODUnity.EventReference soarDarkEvent;
        [Tooltip("The redline reached, once a race. Shipped empty (FMOD convention).")]
        [SerializeField] FMODUnity.EventReference redlineEvent;

        sealed class Flare
        {
            public Transform Root;
            public float StartAt;
            public float Punch;
        }

        readonly List<Flare> _flares = new();
        int _heatThreaded = -1;            // threads the local pilot has made, as graded here (-1 = not started)
        int _streak;
        bool _lapAllClean = true;
        float _lapStartedAt;
        float _bestLap = -1f;
        bool _hintPosted, _redlineCalled, _soarWasDark;
        Vector3 _heatPrev;
        bool _hasHeatPrev;

        void ResetHeat()
        {
            ClearHeat();
            _heatThreaded = -1;
            _streak = 0;
            _lapAllClean = true;
            _bestLap = -1f;
            _redlineCalled = _soarWasDark = _hasHeatPrev = false;
        }

        void ClearHeat()
        {
            for (int i = 0; i < _flares.Count; i++)
                if (_flares[i].Root) Destroy(_flares[i].Root.gameObject);
            _flares.Clear();
        }

        void TickHeat()
        {
            TickFlares();
            if (!heatOn || _course.Count == 0 || gameData == null || !gameData.IsTurnRunning) { _hasHeatPrev = false; return; }
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
            // Never more than one thread ahead of the race's own count (a crossing the race did not credit), never behind
            // it (a rollback, or a thread credited before this saw it): the heat follows the race.
            if (credited < _heatThreaded - 1 || credited > _heatThreaded)
            {
                if (credited < _heatThreaded) _streak = 0;
                _heatThreaded = credited;
            }

            if (!_hintPosted)
            {
                _hintPosted = true;
                GameToastAPI.Post(GameToastSituation.RedlineHint, local.Domain);
            }

            int timeLevel = status.ResourceSystem != null ? status.ResourceSystem.GetLevel(Element.Time) : 0;
            Vector3 pos = tf.position;
            if (_hasHeatPrev && _heatThreaded < RaceLength)
            {
                var gate = _course[RingIndexFor(_heatThreaded)];
                float offset = RedlineHeat.CrossingOffset(_heatPrev, pos, gate.Position, gate.Axis);
                if (!float.IsPositiveInfinity(offset))
                    OnLocalCrossing(local, RedlineHeat.Grade(offset, gate.Radius, status.Speed, timeLevel),
                                    RingIndexFor(_heatThreaded));
            }
            _heatPrev = pos;
            _hasHeatPrev = true;

            if (!_redlineCalled && status.Speed >= RedlineHeat.RedlineSpeed)
            {
                _redlineCalled = true;
                GameToastAPI.Post(GameToastSituation.RedlineRedline, local.Domain,
                                  Mathf.RoundToInt(status.Speed).ToString(CultureInfo.InvariantCulture));
                Play(redlineEvent);
                Chase(redhotFlarePunch, 0f);
            }

            bool dark = timeLevel < RedlineCourse.MantaSoarRelockBelowLevel;
            if (dark && !_soarWasDark)
            {
                GameToastAPI.Post(GameToastSituation.RedlineSoarDark, local.Domain);
                Play(soarDarkEvent);
            }
            _soarWasDark = dark;
        }

        void OnLocalCrossing(IPlayer local, RedlineThreadGrade grade, int ring)
        {
            if (grade == RedlineThreadGrade.Missed)
            {
                _streak = 0;
                _lapAllClean = false;
                Play(missedMouthEvent);
                return;
            }

            _heatThreaded++;
            _streak = RedlineHeat.StreakAfter(_streak, grade);
            if (grade < RedlineThreadGrade.Clean) _lapAllClean = false;
            if (grade == RedlineThreadGrade.Redhot) { FlareRing(ring, redhotFlarePunch, 0f); Play(redhotThreadEvent); }
            else if (grade == RedlineThreadGrade.Clean) { FlareRing(ring, cleanFlarePunch, 0f); Play(cleanThreadEvent); }
            if (RedlineHeat.IsCalled(_streak))
            {
                GameToastAPI.Post(GameToastSituation.RedlineStreak, local.Domain, _streak.ToString(CultureInfo.InvariantCulture));
                Play(streakCalledEvent);
            }

            int lap = RedlineHeat.LapEndedBy(_heatThreaded, _rings.Count);
            if (lap <= 0) return;
            float split = Time.time - _lapStartedAt;
            _lapStartedAt = Time.time;
            string lapText = lap.ToString(CultureInfo.InvariantCulture), splitText = split.ToString("0.0", CultureInfo.InvariantCulture);
            if (_bestLap > 0f && split < _bestLap)
            {
                GameToastAPI.Post(GameToastSituation.RedlineBestLap, local.Domain, lapText, splitText);
                Play(bestLapEvent);
            }
            else
            {
                GameToastAPI.Post(GameToastSituation.RedlineLapSplit, local.Domain, lapText, splitText);
                Play(lapCompleteEvent);
            }
            if (_bestLap < 0f || split < _bestLap) _bestLap = split;
            if (_lapAllClean)
            {
                GameToastAPI.Post(GameToastSituation.RedlineCleanLap, local.Domain, lapText);
                Play(cleanLapEvent);
                Chase(cleanFlarePunch, chaseStagger);
            }
            _lapAllClean = true;
        }

        /// <summary>Every ring of the lap flares in course order, <paramref name="stagger"/> s apart (0: all at once).</summary>
        void Chase(float punch, float stagger)
        {
            for (int i = 0; i < _rings.Count; i++) FlareRing(i, punch, i * stagger);
        }

        /// <summary>A shock ring off gate <paramref name="ring"/>: a copy of its mouth that grows to <paramref name="punch"/>
        /// times its size and vanishes (the continuity law: grown, never popped, and gone by scale).</summary>
        void FlareRing(int ring, float punch, float delay)
        {
            if (ring < 0 || ring >= _course.Count) return;
            while (_flares.Count >= maxFlares)
            {
                if (_flares[0].Root) Destroy(_flares[0].Root.gameObject);
                _flares.RemoveAt(0);
            }
            var gate = _course[ring];
            var go = new GameObject("RedlineFlare");
            go.transform.SetParent(transform, false);
            go.transform.SetPositionAndRotation(gate.Position,
                Quaternion.LookRotation(gate.Axis.sqrMagnitude > 1e-6f ? gate.Axis : Vector3.forward));
            ToyFactory.AddSwitchRing(go.transform, gate.Radius, gameData ? gameData.ThemeManagerData : null);
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
