using System.Collections.Generic;
using System.Globalization;
using CosmicShore.Data;
using CosmicShore.UI;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Brittle Star Time's heat (<see cref="BrittleStarTimeHeat"/>): the LOCAL pilot's threads graded by the speed they
    /// carried and called, in the world first. A flat-out thread (through the heart at full Speed) throws a shock ring
    /// off the ring; a clean shift (a let-go ring taken at its corner's speed) throws one and lights its let-go gate; a
    /// streak of carried threads is called; every lap is split; a lap carried end to end runs a chase of shock rings
    /// round the circuit. A start hint per rung, and one reminder for a dark start (the recorded I1 pilot crawled 41 s
    /// to ring 1 before ever lighting Speed Mode).
    ///
    /// <para><b>Local and unscored.</b> Every peer builds its own course, so a flare belongs to the one viewer who earned
    /// it, and nothing here touches the race's score or the network. The grade is read from the local hull's own motion
    /// through the ring it is flying at, the same plane test the race credits. The vessel is untouched.</para>
    ///
    /// <para>Every word is a toast template on <c>GameToastConfig_BrittleStarTime</c> (410-419, authored by
    /// <c>author_brittle_star_time_assets.py</c>), and every sound an <see cref="FMODUnity.EventReference"/> below,
    /// shipped EMPTY.</para>
    /// </summary>
    public partial class BrittleStarTimeController
    {
        [Header("Heat (BrittleStarTimeHeat): the local pilot's threads, graded and called")]
        [Tooltip("Grade and call the local pilot's threads (shock rings, streaks, lap splits, hints). Off = the race " +
                 "exactly as before, with no heat.")]
        [SerializeField] bool heatOn = true;

        [Tooltip("How far a flat-out thread's shock ring grows, as a multiple of its mouth.")]
        [SerializeField, Min(1f)] float flatOutFlarePunch = 2.4f;

        [Tooltip("How far a clean shift's shock ring grows, as a multiple of its mouth.")]
        [SerializeField, Min(1f)] float cleanShiftFlarePunch = 1.8f;

        [Tooltip("Seconds a shock ring takes to grow and vanish.")]
        [SerializeField, Min(0.05f)] float flareSeconds = 0.45f;

        [Tooltip("Seconds between the rings of a clean lap's chase, in course order.")]
        [SerializeField, Min(0f)] float chaseStagger = 0.06f;

        [Tooltip("The most shock rings standing at once (the oldest is cut short past it).")]
        [SerializeField, Min(1)] int maxFlares = 24;

        [Header("Heat audio (FMOD; every slot shipped empty)")]
        [Tooltip("A flat-out thread: through the heart at full Speed. Shipped empty (FMOD convention).")]
        [SerializeField] FMODUnity.EventReference flatOutThreadEvent;
        [Tooltip("A clean shift: a let-go ring taken at its corner's speed. Shipped empty (FMOD convention).")]
        [SerializeField] FMODUnity.EventReference cleanShiftEvent;
        [Tooltip("A streak of carried threads called (3, 8, 16, then every 16). Shipped empty (FMOD convention).")]
        [SerializeField] FMODUnity.EventReference streakCalledEvent;
        [Tooltip("A lap completed. Shipped empty (FMOD convention).")]
        [SerializeField] FMODUnity.EventReference lapCompleteEvent;
        [Tooltip("A lap that beat this race's best. Shipped empty (FMOD convention).")]
        [SerializeField] FMODUnity.EventReference bestLapEvent;
        [Tooltip("A lap carried end to end (the chase). Shipped empty (FMOD convention).")]
        [SerializeField] FMODUnity.EventReference cleanLapEvent;
        [Tooltip("A mouth missed: the ring's plane crossed outside it. Shipped empty (FMOD convention).")]
        [SerializeField] FMODUnity.EventReference missedMouthEvent;
        [Tooltip("A dark start: Speed Mode never lit, no ring yet. Shipped empty (FMOD convention).")]
        [SerializeField] FMODUnity.EventReference speedDarkEvent;

        sealed class Flare
        {
            public Transform Root;
            public float StartAt;
            public float Punch;
        }

        readonly List<Flare> _flares = new();
        int _heatThreaded = -1;            // threads the local pilot has made, as graded here (-1 = not started)
        int _streak;
        bool _lapAllCarried = true;
        float _lapStartedAt, _turnStartedAt;
        float _bestLap = -1f;
        bool _hintPosted, _darkPosted, _flatOutTaught, _shiftTaught, _everLit;
        Vector3 _heatPrev;
        bool _hasHeatPrev;
        BrittleStarExecutor _localStar;
        Transform _localStarHull;

        void ClearHeat()
        {
            for (int i = 0; i < _flares.Count; i++)
                if (_flares[i].Root) Destroy(_flares[i].Root.gameObject);
            _flares.Clear();
            ResetHeatCounters();
        }

        void ResetHeatCounters()
        {
            _heatThreaded = -1;
            _streak = 0;
            _lapAllCarried = true;
            _bestLap = -1f;
            _hintPosted = _darkPosted = _flatOutTaught = _shiftTaught = _everLit = _hasHeatPrev = false;
        }

        /// <summary>The start hint for this rung: Speed Mode (I1, I2), the posts (I3), the roll (I4).</summary>
        GameToastSituation StartHint => Intensity switch
        {
            3 => GameToastSituation.BrittleStarTimeLetGoHint,
            4 => GameToastSituation.BrittleStarTimeRollHint,
            _ => GameToastSituation.BrittleStarTimeSpeedHint,
        };

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
                _lapStartedAt = _turnStartedAt = Time.time;
            }

            // The crossing FIRST, against the ring the heat is on: the race credits a thread in its own Update, which may
            // run before this LateUpdate, and resyncing first would test this frame's crossing against the NEXT ring.
            Vector3 pos = tf.position;
            if (_hasHeatPrev && _heatThreaded < RaceLength)
            {
                int ring = RingIndexFor(_heatThreaded);
                var gate = _course[ring];
                float offset = BrittleStarTimeHeat.CrossingOffset(_heatPrev, pos, gate.Position, gate.Axis);
                // A crossing far out on the ring's plane is not an attempt at it (an apex ring's plane runs along the leg
                // into it, so drift there crosses it again and again): neither a thread nor a miss.
                if (BrittleStarTimeHeat.IsAnAttempt(offset, gate.Radius))
                    OnLocalCrossing(local, BrittleStarTimeHeat.Grade(_course, ring, offset, status.Speed, TopSpeed), ring);
            }
            _heatPrev = pos;
            _hasHeatPrev = true;

            // Then follow the race: never more than one thread ahead of its count, never behind it.
            if (credited < _heatThreaded - 1 || credited > _heatThreaded)
            {
                if (credited < _heatThreaded) _streak = 0;
                _heatThreaded = credited;
            }

            if (!_hintPosted)
            {
                _hintPosted = true;
                GameToastAPI.Post(StartHint, local.Domain);
            }

            if (_localStarHull != tf)
            {
                _localStarHull = tf;
                _localStar = tf.GetComponentInChildren<BrittleStarExecutor>(true);
            }
            if (_localStar && _localStar.IsOn(BrittleStarExecutor.Mode.Speed)) _everLit = true;
            if (!_darkPosted && BrittleStarTimeHeat.SpeedStillDark(Time.time - _turnStartedAt, credited, _everLit))
            {
                _darkPosted = true;
                GameToastAPI.Post(GameToastSituation.BrittleStarTimeSpeedDark, local.Domain);
                Play(speedDarkEvent);
            }
        }

        void OnLocalCrossing(IPlayer local, BrittleStarThreadGrade grade, int ring)
        {
            if (grade == BrittleStarThreadGrade.Missed)
            {
                _streak = 0;
                _lapAllCarried = false;
                Play(missedMouthEvent);
                return;
            }

            _heatThreaded++;
            _streak = BrittleStarTimeHeat.StreakAfter(_streak, grade);
            if (grade < BrittleStarThreadGrade.FlatOut) _lapAllCarried = false;
            if (grade == BrittleStarThreadGrade.FlatOut)
            {
                FlareRing(ring, flatOutFlarePunch, 0f);
                Play(flatOutThreadEvent);
                if (!_flatOutTaught)
                {
                    _flatOutTaught = true;
                    GameToastAPI.Post(GameToastSituation.BrittleStarTimeFlatOut, local.Domain);
                }
            }
            else if (grade == BrittleStarThreadGrade.CleanShift)
            {
                FlareRing(ring, cleanShiftFlarePunch, 0f);
                LightLetGoGate(ring);
                Play(cleanShiftEvent);
                if (!_shiftTaught)
                {
                    _shiftTaught = true;
                    GameToastAPI.Post(GameToastSituation.BrittleStarTimeCleanShift, local.Domain);
                }
            }
            if (BrittleStarTimeHeat.IsCalled(_streak) && grade >= BrittleStarThreadGrade.FlatOut)
            {
                GameToastAPI.Post(GameToastSituation.BrittleStarTimeStreak, local.Domain,
                                  _streak.ToString(CultureInfo.InvariantCulture));
                Play(streakCalledEvent);
            }

            int lap = BrittleStarTimeHeat.LapEndedBy(_heatThreaded, _rings.Count);
            if (lap <= 0) return;
            float split = Time.time - _lapStartedAt;
            _lapStartedAt = Time.time;
            string lapText = lap.ToString(CultureInfo.InvariantCulture), splitText = split.ToString("0.0", CultureInfo.InvariantCulture);
            if (_bestLap > 0f && split < _bestLap)
            {
                GameToastAPI.Post(GameToastSituation.BrittleStarTimeBestLap, local.Domain, lapText, splitText);
                Play(bestLapEvent);
            }
            else
            {
                GameToastAPI.Post(GameToastSituation.BrittleStarTimeLapSplit, local.Domain, lapText, splitText);
                Play(lapCompleteEvent);
            }
            if (_bestLap < 0f || split < _bestLap) _bestLap = split;
            if (_lapAllCarried)
            {
                GameToastAPI.Post(GameToastSituation.BrittleStarTimeCleanLap, local.Domain, lapText);
                Play(cleanLapEvent);
                for (int i = 0; i < _rings.Count; i++) FlareRing(i, flatOutFlarePunch, i * chaseStagger);
            }
            _lapAllCarried = true;
        }

        /// <summary>A shock ring off ring <paramref name="ring"/>: a copy of its mouth (the same switch ring the race draws,
        /// so its colour is the colour set's) that grows to <paramref name="punch"/> times its size and vanishes.</summary>
        void FlareRing(int ring, float punch, float delay)
        {
            if (ring < 0 || ring >= _course.Count) return;
            while (_flares.Count >= maxFlares)
            {
                if (_flares[0].Root) Destroy(_flares[0].Root.gameObject);
                _flares.RemoveAt(0);
            }
            var gate = _course[ring];
            var go = new GameObject("BrittleStarTimeFlare");
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
