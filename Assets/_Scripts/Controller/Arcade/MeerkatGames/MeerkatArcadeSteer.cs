using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The one AI hook the Meerkat's arcade games (Sentry, Warren, Cave-in; MEERKAT_GAMES.md) share: it tells each AI
    /// Meerkat WHERE to go, and nothing else. Server-only.
    ///
    /// <para><b>Steering only - the Meerkat's kit is untouched</b> (the LOCKED vessel rule). The hull's own autopilot
    /// (<c>MeerkatSolitonExecutor.Autopilot</c>) already plans a soliton toward whatever its <see cref="AIPilot"/> is
    /// steering for - it bends the reticle, squeezes LT and presses RT through the replicated press path - and
    /// <c>MeerkatSolitonKit</c> already turns Sentinel Mode on for an AI. So a mode only has to choose the point: this
    /// class gives every AI Meerkat on the roster an <see cref="AIPilot.SetExternalTargetProvider"/> that returns its
    /// seat's <see cref="Seat.Steer"/>, and asks the mode's <see cref="Picker"/> for a new one every
    /// <c>retargetSeconds</c> of that seat's AI level. It presses nothing; each mode's generator fails if its picker
    /// or this class ever writes an input.</para>
    ///
    /// <para><b>Difficulty is the seat's AI level, never the intensity</b> (<c>AIDifficulty.cs</c>): every ladder a
    /// picker reads is indexed by <see cref="Seat.Level"/> through <see cref="Rung"/>.</para>
    /// </summary>
    public sealed class MeerkatArcadeSteer
    {
        /// <summary>One AI Meerkat and what it is steering for.</summary>
        public sealed class Seat
        {
            public IPlayer Player;
            /// <summary>The seat's AI level, 1 Easy - 4 Legendary (<see cref="GameDataSO.AIDifficultyFor"/>).</summary>
            public int Level;
            public IVesselStatus Status;
            public AIPilot Autopilot;
            public System.Random Rng;
            /// <summary>The point the autopilot steers for (and so plans its solitons toward).</summary>
            public Vector3 Steer;
            public float NextRetargetAt;
            /// <summary>A mode's own per-seat memory (Cave-in: the plate it is mining and the one it fires at).</summary>
            public int StateA = -1, StateB = -1;
        }

        /// <summary>A mode's choice of where a seat goes next.</summary>
        public delegate Vector3 Picker(Seat seat, Transform self);

        readonly List<Seat> _seats = new();
        GameDataSO _gameData;
        Picker _pick;
        Func<Seat, float> _retargetSeconds;

        public bool IsArmed => _seats.Count > 0;
        public IReadOnlyList<Seat> Seats => _seats;

        /// <summary>
        /// Arms every AI Meerkat on the roster. <paramref name="salt"/> seeds each seat's own random stream (the mode's
        /// <c>GameModes</c> id), so two modes never share a stream and a rematch replays the same choices.
        /// </summary>
        public void Arm(GameDataSO gameData, int salt, Vector3 start, Picker pick, Func<Seat, float> retargetSeconds)
        {
            Disarm();
            if (gameData == null || pick == null) return;
            _gameData = gameData;
            _pick = pick;
            _retargetSeconds = retargetSeconds;

            var players = gameData.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p == null || !p.IsInitializedAsAI) continue;
                var status = p.Vessel?.VesselStatus;
                if (status == null || !status.Transform) continue;
                // TryGetComponent, not VesselStatus.AIPilot: that getter ADDS an autopilot to a hull without one.
                var autopilot = status is Component c && c && c.TryGetComponent(out AIPilot ai) ? ai : null;
                if (autopilot == null) continue;
                // The cards seat only Meerkats; anything else keeps the platform's own steering.
                if (!status.Transform.GetComponentInChildren<MeerkatSolitonExecutor>(true)) continue;

                var seat = new Seat
                {
                    Player = p,
                    Level = (int)gameData.AIDifficultyFor(p),
                    Status = status,
                    Autopilot = autopilot,
                    Rng = new System.Random(unchecked((int)p.PlayerNetId * 7919 + salt)),
                    Steer = start,
                };
                _seats.Add(seat);
                var captured = seat;
                autopilot.SetExternalTargetProvider(() => captured.Steer);
            }
        }

        /// <summary>Hands every autopilot back to the platform's own steering. Safe to call twice.</summary>
        public void Disarm()
        {
            for (int i = 0; i < _seats.Count; i++)
                if (_seats[i].Autopilot) _seats[i].Autopilot.ClearExternalTargetProvider();
            _seats.Clear();
        }

        public void Tick()
        {
            if (_pick == null) return;
            float now = Time.time;
            for (int i = 0; i < _seats.Count; i++)
            {
                var s = _seats[i];
                var self = s.Player?.Vessel?.Transform;
                if (!self || !s.Autopilot || !s.Autopilot.AutoPilotEnabled) continue;
                if (now < s.NextRetargetAt) continue;
                s.NextRetargetAt = now + Mathf.Max(0.1f, _retargetSeconds != null ? _retargetSeconds(s) : 1f);
                s.Steer = _pick(s, self);
            }
        }

        // ------------------------------------------------------------------ shared answers a picker asks

        /// <summary>The rung for AI level <paramref name="level"/> (1 Easy - 4 Legendary, clamped) of a ladder.</summary>
        public static float Rung(float[] ladder, int level)
        {
            if (ladder == null || ladder.Length == 0) return 0f;
            return ladder[Mathf.Clamp(level - 1, 0, ladder.Length - 1)];
        }

        /// <summary>
        /// The nearest rival pilot (another domain, with a hull) within <paramref name="range"/> of
        /// <paramref name="from"/>, or null. A human rival counts as <paramref name="humanFocus"/> times nearer.
        /// </summary>
        public IPlayer NearestRival(Seat seat, Vector3 from, float range, float humanFocus = 1f)
        {
            IPlayer best = null;
            float bestCost = float.MaxValue;
            var players = _gameData != null ? _gameData.Players : null;
            if (players == null) return null;
            for (int i = 0; i < players.Count; i++)
            {
                var c = players[i];
                if (c == null || ReferenceEquals(c, seat.Player) || c.Domain == seat.Player.Domain) continue;
                var tf = c.Vessel?.Transform;
                if (!tf) continue;
                float d = Vector3.Distance(tf.position, from);
                if (d > range) continue;
                float cost = c.IsInitializedAsAI ? d : d / Mathf.Max(1f, humanFocus);
                if (cost >= bestCost) continue;
                bestCost = cost;
                best = c;
            }
            return best;
        }

        /// <summary>Where <paramref name="rival"/> will be <paramref name="seconds"/> from now on its own course.</summary>
        public static Vector3 Led(IPlayer rival, float seconds)
        {
            var tf = rival?.Vessel?.Transform;
            if (!tf) return Vector3.zero;
            var status = rival.Vessel.VesselStatus;
            return tf.position + (status != null ? status.Course * status.Speed * seconds : Vector3.zero);
        }

        /// <summary>The nearest live OMNI crystal to <paramref name="from"/>, or null (elemental crystals are skipped).</summary>
        public static Vector3? NearestOmniCrystal(Vector3 from)
        {
            Vector3? best = null;
            float bestSqr = float.MaxValue;
            var active = Crystal.Active;
            for (int i = 0; i < active.Count; i++)
            {
                var c = active[i];
                if (!c || c.IsEmbedded || c.crystalProperties.IsElemental) continue;
                float d = (c.transform.position - from).sqrMagnitude;
                if (d >= bestSqr) continue;
                bestSqr = d;
                best = c.transform.position;
            }
            return best;
        }

        /// <summary>A point off <paramref name="at"/> by up to <paramref name="error"/> u in a random direction.</summary>
        public static Vector3 Jitter(Seat seat, Vector3 at, float error)
        {
            if (error <= 0f) return at;
            var d = new Vector3((float)(seat.Rng.NextDouble() * 2 - 1), (float)(seat.Rng.NextDouble() * 2 - 1),
                                (float)(seat.Rng.NextDouble() * 2 - 1));
            return at + (d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.right) * (error * (float)seat.Rng.NextDouble());
        }
    }
}
