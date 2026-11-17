using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Spearline's AI (SPEARLINE.md §3): the READ and the STROKE, both through the AI's input.
    ///
    /// <para><b>The read</b> (<see cref="SpearlineSeals.Read"/>): for the AI's next sealed ring, the crossing nearest the
    /// ring's centre (where the gate steering aims anyway) whose model hole opens the seal with one of the three strokes
    /// it can make from its current roll: the bill to the left, to the right, or stood up. The gate steering is moved
    /// there through <see cref="AILaneOffset"/>, plus a fixed lane error by difficulty; an Easy seat sometimes does not
    /// read at all and crosses the centre with a right-hand stroke (<c>aiMissReadByDifficulty</c>).</para>
    ///
    /// <para><b>The stroke</b> (<see cref="SwordCoach"/>): the coach owns the AI's sword hand
    /// (<see cref="SwordfishSwordAutopilot.Coach"/>) inside <c>aiCoachOwnsTriggersWithin</c> of a sealed ring, holds the
    /// read's trigger from <c>aiStrokeLeadSeconds</c> before the plane until <c>aiStrokeHoldPast</c> beyond it, and
    /// holds both triggers at rest otherwise, so the hand never stands the bill up to energize in front of a pane. A
    /// pass that held is a closed pass like a human's: the worn seal lowers the AI's need, its own cut stays (a seal
    /// does not grow back while a pilot is still flying for it), and the next read starts from the hole it made.</para>
    /// </summary>
    public partial class SpearlineController
    {
        // Per AI: its read of its next seal, refreshed until it is close (server, or wherever the AI's input is written).
        readonly Dictionary<IPlayer, Read> _reads = new();

        sealed class Read
        {
            public int RaceIndex = -1;          // the threading this read is for (ring and lap)
            public float At = -1f;              // when it was last refreshed
            public SpearlineSeals.Plan Plan;
            public int Passes;                  // the pilot's closed passes at this seal when it was read
            public bool Frozen;
        }

        protected override void OnRacerArmed(IPlayer pilot)
        {
            var tf = pilot?.Vessel?.Transform;
            var hand = tf ? tf.GetComponentInChildren<SwordfishSwordAutopilot>(true) : null;
            if (hand) hand.Coach = new SwordCoach(this, pilot);
            _reads.Remove(pilot);
        }

        /// <summary>The AI's sealed ring <paramref name="ringNo"/>: aim through its read crossing (plus the seat's lane
        /// error) rather than the centre.</summary>
        protected override Vector3 AILaneOffset(IPlayer pilot, int ringNo, in RaceGate gate)
        {
            if (!_sealOf.ContainsKey(ringNo)) return Vector3.zero;
            var read = ReadFor(pilot, ringNo);
            if (read == null) return Vector3.zero;
            return read.Plan.Crossing - gate.Position + LaneError(pilot, read.RaceIndex, gate);
        }

        int DifficultyIndex(IPlayer pilot) =>
            Mathf.Clamp((int)(gameData ? gameData.AIDifficultyFor(pilot) : AIDifficulty.Medium) - 1, 0, 3);

        static float ByDifficulty(float[] row, int index, float fallback) =>
            row != null && row.Length > 0 ? row[Mathf.Clamp(index, 0, row.Length - 1)] : fallback;

        int SeatOf(IPlayer pilot)
        {
            var players = gameData.Players;
            for (int i = 0; i < players.Count; i++) if (players[i] == pilot) return i;
            return 0;
        }

        /// <summary>The seat's fixed lane error for one threading, in the ring's plane.</summary>
        Vector3 LaneError(IPlayer pilot, int raceIndex, in RaceGate gate)
        {
            float max = ByDifficulty(aiLaneErrorByDifficulty, DifficultyIndex(pilot), 0f);
            if (max <= 0f) return Vector3.zero;
            int seat = SeatOf(pilot);
            float ang = 2f * Mathf.PI * SpearlineSeals.Hash01(seat, raceIndex, 11);
            float r = max * SpearlineSeals.Hash01(seat, raceIndex, 23);
            SpearlineSeals.Basis(gate.Axis, out var u, out var v);
            return (u * Mathf.Cos(ang) + v * Mathf.Sin(ang)) * r;
        }

        /// <summary>The AI's current read of sealed ring <paramref name="ringNo"/>, refreshed while it is far, frozen
        /// close in. Null when the pilot has no hull or the ring is not its next.</summary>
        Read ReadFor(IPlayer pilot, int ringNo)
        {
            var tf = pilot?.Vessel?.Transform;
            if (!tf || !_sealOf.TryGetValue(ringNo, out var seal)) return null;
            int raceIndex = pilot.RoundStats?.SwitchesThreaded ?? 0;
            if (raceIndex >= RaceLength || RingIndexFor(raceIndex) != ringNo) return null;
            if (!_reads.TryGetValue(pilot, out var read)) _reads[pilot] = read = new Read();
            var gate = _course[ringNo];
            float distance = (tf.position - gate.Position).magnitude;
            int passes = _closedPasses.TryGetValue((pilot, ringNo), out int n) ? n : 0;
            if (read.RaceIndex != raceIndex || read.Passes != passes)
            {
                // A new seal, or a closed pass at this one: read it afresh from what that pass cut.
                read.RaceIndex = raceIndex;
                read.Passes = passes;
                read.Frozen = false;
                read.At = -1f;
            }
            if (distance < aiPlanFreezeDistance)
            {
                if (read.At >= 0f) read.Frozen = true;
            }
            else read.Frozen = false;
            if (read.Frozen || (read.At >= 0f && Time.time - read.At < 0.2f)) return read;
            read.At = Time.time;
            Refresh(read, pilot, tf, gate, seal);
            return read;
        }

        static readonly List<Vector3> s_strokes = new(3);

        void Refresh(Read read, IPlayer pilot, Transform tf, in RaceGate gate, Seal seal)
        {
            Vector3 axis = gate.Axis.normalized;
            Vector3 Flat(Vector3 w)
            {
                Vector3 f = w - axis * Vector3.Dot(w, axis);
                return f.sqrMagnitude > 1e-4f ? f.normalized : Vector3.zero;
            }
            s_strokes.Clear();
            s_strokes.Add(Flat(-tf.right));   // left trigger: the bill out to the left
            s_strokes.Add(Flat(tf.right));    // right trigger
            s_strokes.Add(Flat(tf.up));       // both: the bill stood up

            int lap = Mathf.Max(0, read.RaceIndex) / Mathf.Max(1, _course.Count);
            float miss = ByDifficulty(aiMissReadByDifficulty, DifficultyIndex(pilot), 0f);
            if (SpearlineSeals.Hash01(SeatOf(pilot), read.RaceIndex, 37 + lap) < miss)
            {
                read.Plan = new SpearlineSeals.Plan { Crossing = gate.Position, Stroke = 1 };
                return;
            }

            FillSealState(seal);
            read.Plan = SpearlineSeals.Read(gate, gate.Radius, gate.Position, _sealPositions, seal.Start,
                                            seal.Adjacency, _bufGone, _bufBreaks, _bufCut, s_strokes,
                                            SpearlineSeals.NeedAfter(Settings, read.Passes), 1,
                                            SpearlineSeals.ModelBladeLength, SpearlineSeals.ModelStrokeReach,
                                            _bufCutNow, _bufInHole, _readHole);
        }

        /// <summary>The triggers for one AI this frame (<see cref="ISwordfishSwordCoach"/>): false far from any seal (the
        /// hand decides), at rest while closing on one, and the read's stroke across its plane.</summary>
        bool CoachTriggers(IPlayer pilot, Transform ship, out float left, out float right)
        {
            left = right = 0f;
            if (pilot == null || !ship || _course.Count == 0) return false;
            int raceIndex = pilot.RoundStats?.SwitchesThreaded ?? 0;
            if (raceIndex >= RaceLength) return false;
            int ringNo = RingIndexFor(raceIndex);
            if (!_sealOf.ContainsKey(ringNo)) return false;
            var gate = _course[ringNo];
            Vector3 n = gate.Axis.normalized;
            float side = Vector3.Dot(ship.position - gate.Position, n);
            float distance = Mathf.Abs(side);
            bool inMouth = SpearlineSeals.PlaneDistanceSqr(ship.position, gate.Position, n) <
                           (gate.Radius + 40f) * (gate.Radius + 40f);
            // Near the seal itself, not anywhere in the slab its plane cuts through space.
            if (distance > aiCoachOwnsTriggersWithin ||
                (ship.position - gate.Position).sqrMagnitude > aiCoachOwnsTriggersWithin * aiCoachOwnsTriggersWithin)
                return false;
            var read = ReadFor(pilot, ringNo);
            if (read == null) return false;
            bool closing = Vector3.Dot(ship.forward, -Mathf.Sign(side) * n) > 0.2f;
            float speed = SpeedOf(pilot, ship);
            bool stroke = inMouth && ((closing && distance <= Mathf.Max(40f, aiStrokeLeadSeconds * speed))
                                      || (!closing && distance <= aiStrokeHoldPast));
            if (stroke) Triggers(read.Plan.Stroke, out left, out right);
            return true;
        }

        /// <summary>The trigger pair for a stroke index of <see cref="Refresh"/>'s three: left, right, both.</summary>
        static void Triggers(int stroke, out float left, out float right)
        {
            left = stroke == 0 || stroke == 2 ? 1f : 0f;
            right = stroke == 1 || stroke == 2 ? 1f : 0f;
        }

        readonly Dictionary<IPlayer, (Vector3 at, float time, float speed)> _speedSample = new();

        /// <summary>The hull's speed, from its own last two positions (the coach reads no vessel internals).</summary>
        float SpeedOf(IPlayer pilot, Transform ship)
        {
            float now = Time.time;
            if (_speedSample.TryGetValue(pilot, out var last) && now > last.time)
            {
                float speed = (ship.position - last.at).magnitude / (now - last.time);
                _speedSample[pilot] = (ship.position, now, speed);
                return speed;
            }
            _speedSample[pilot] = (ship.position, now, last.speed);
            return last.speed;
        }

        /// <summary>The coach one AI's sword hand asks every frame (<see cref="SwordfishSwordAutopilot.Coach"/>).</summary>
        sealed class SwordCoach : ISwordfishSwordCoach
        {
            readonly SpearlineController _mode;
            readonly IPlayer _pilot;

            public SwordCoach(SpearlineController mode, IPlayer pilot)
            {
                _mode = mode;
                _pilot = pilot;
            }

            public bool TryHold(Transform ship, out float left, out float right)
            {
                left = right = 0f;
                return _mode && _mode.CoachTriggers(_pilot, ship, out left, out right);
            }
        }
    }
}
