using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The AI pilot of Stingherd: a DROVER that drives its herd into rival hulls. Server-only.
    ///
    /// <para><b>Input-only - the Jacks' kit is untouched.</b> The drover is the hull's race coach
    /// (<see cref="IJacksRaceCoach"/>, the seam Decoy Run built): each cycle it answers is a <see cref="JacksRacePlan"/>
    /// - the two jacks run round the herd to the far side from the chosen rival and stand there, a ring behind it, the
    /// way a person's two thumbs would (a Strömbom drive: the herders repel the herd, so it moves away from them, toward
    /// the rival). Then the jacks take off (the hull jumps to halfway between them, behind the herd) and, after the
    /// seat-swap cooldown a person waits out too, the next cycle herds again from there. The hull is never flown far,
    /// because a herd does not follow a flying hull: it is moved only by the take-offs, which stay behind the herd.
    /// Between cycles the hull's own <see cref="AIPilot"/> steers at the rival
    /// (<see cref="AIPilot.SetExternalTargetProvider"/>), which turns the board toward it, so the rival lies on the
    /// board the herd is drawn to. It lays no decoy and flies to none.</para>
    ///
    /// <para><b>Danger Share does the scoring.</b> A danger creature of the herd that touches a rival hull stings it (a
    /// Strike, <c>JacksExecutor.UpdateContacts</c>) and is spent; at Charge 5 (Attack) the herd also goes for rivals in
    /// range by itself and every creature bites. The drover only decides where the herd goes.</para>
    ///
    /// <para><b>Difficulty is the seat's AI level, never the intensity</b> (<c>AIDifficulty.cs</c>).</para>
    /// </summary>
    public sealed class StingherdDrover : IJacksRaceCoach
    {
        sealed class Drover
        {
            public IPlayer Player;
            public int Level;
            public IVesselStatus Status;
            public AIPilot Autopilot;
            public JacksExecutor Jacks;
            public System.Random Rng;

            public IPlayer Rival;
            public float NextRetargetAt;
            public Vector3 AimError;
        }

        readonly List<Drover> _drovers = new();
        readonly Dictionary<JacksExecutor, Drover> _byJacks = new();
        StingherdSettingsSO _settings;
        GameDataSO _gameData;
        Vector3 _centre;

        public bool IsArmed => _drovers.Count > 0;

        /// <summary>Coaches every AI Jacks on the roster; the arena's centre is where a drover with no rival looks.</summary>
        public void Arm(GameDataSO gameData, StingherdSettingsSO settings, Vector3 centre)
        {
            Disarm();
            if (gameData == null || settings == null) return;
            _gameData = gameData;
            _settings = settings;
            _centre = centre;

            var players = gameData.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p == null || !p.IsInitializedAsAI) continue;
                var status = p.Vessel?.VesselStatus;
                if (status == null || !status.Transform) continue;
                // TryGetComponent, not VesselStatus.AIPilot: that getter ADDS an autopilot to a hull without one.
                var autopilot = status is Component c && c && c.TryGetComponent(out AIPilot ai) ? ai : null;
                var jacks = status.Transform.GetComponentInChildren<JacksExecutor>(true);
                if (!jacks) continue;   // the card seats only the Jacks; anything else flies the platform's steering

                var drover = new Drover
                {
                    Player = p,
                    Level = (int)gameData.AIDifficultyFor(p),
                    Status = status,
                    Autopilot = autopilot,
                    Jacks = jacks,
                    Rng = new System.Random(unchecked((int)p.PlayerNetId * 7919 + 117)),
                };
                _drovers.Add(drover);
                _byJacks[jacks] = drover;
                jacks.RaceCoach = this;
                if (autopilot)
                {
                    var captured = drover;
                    autopilot.SetExternalTargetProvider(() => SteerFor(captured));
                }
            }
        }

        /// <summary>Hands every hull back to its shipped AI rhythm. Safe to call twice.</summary>
        public void Disarm()
        {
            for (int i = 0; i < _drovers.Count; i++)
            {
                var d = _drovers[i];
                if (d.Jacks && ReferenceEquals(d.Jacks.RaceCoach, this)) d.Jacks.RaceCoach = null;
                if (d.Autopilot) d.Autopilot.ClearExternalTargetProvider();
            }
            _drovers.Clear();
            _byJacks.Clear();
        }

        /// <summary>Re-chooses rivals on each drover's own clock.</summary>
        public void Tick()
        {
            if (_settings == null) return;
            float now = Time.time;
            for (int i = 0; i < _drovers.Count; i++)
            {
                var d = _drovers[i];
                if (!d.Jacks || now < d.NextRetargetAt) continue;
                d.NextRetargetAt = now + StingherdSettingsSO.Rung(_settings.aiRetargetSeconds, d.Level);
                d.Rival = PickRival(d);
                float err = StingherdSettingsSO.Rung(_settings.aiAimError, d.Level);
                d.AimError = new Vector3(Signed(d.Rng), Signed(d.Rng), Signed(d.Rng)).normalized * err;
            }
        }

        static float Signed(System.Random rng) => (float)rng.NextDouble() * 2f - 1f;

        /// <summary>The nearest rival hull to the herd, an AI rival counted <see cref="StingherdSettingsSO.aiHumanFocus"/>
        /// times further away than it is.</summary>
        IPlayer PickRival(Drover d)
        {
            Vector3 from = HerdOrJacks(d.Jacks);
            IPlayer best = null;
            float bestCost = float.MaxValue;
            var players = _gameData.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p == null || p == d.Player || p.Domain == d.Status.Domain) continue;
                var tf = p.Vessel?.Transform;
                if (!tf) continue;
                float cost = (tf.position - from).magnitude * (p.IsInitializedAsAI ? _settings.aiHumanFocus : 1f);
                if (cost >= bestCost) continue;
                bestCost = cost;
                best = p;
            }
            return best;
        }

        static Vector3 HerdOrJacks(JacksExecutor jacks) => jacks.HerdCount > 0 ? jacks.HerdCentre : jacks.JacksCentre;

        bool TryRivalAim(Drover d, out Vector3 aim)
        {
            aim = default;
            var tf = d.Rival?.Vessel?.Transform;
            if (!tf) return false;
            aim = tf.position + d.AimError;
            return true;
        }

        /// <summary>Between cycles the hull's autopilot turns toward the rival (so the rival lies on the board); mid-cycle it
        /// holds its heading, because the hull is held on the board.</summary>
        Vector3 SteerFor(Drover d)
        {
            var tf = d.Status?.Transform;
            if (!tf) return _centre;
            if (d.Jacks && d.Jacks.RaceCycleBusy) return tf.position + tf.forward * 200f;
            return TryRivalAim(d, out var aim) ? aim : _centre;
        }

        /// <summary>
        /// One drive: the jacks go round the herd (via a point beside it, so neither runs through it) to stand behind it,
        /// on the far side from the rival, <see cref="StingherdSettingsSO.aiStandOffRings"/> rings past its spread and
        /// <see cref="StingherdSettingsSO.aiFlankRings"/> rings either side of the line. No decoy, no travel.
        /// </summary>
        public bool TryNextCycle(JacksExecutor jacks, out JacksRacePlan plan)
        {
            plan = default;
            if (_settings == null || !jacks || !_byJacks.TryGetValue(jacks, out var d)) return false;
            if (!TryRivalAim(d, out var aim)) return false;

            Vector3 up = jacks.RaceBoardUp;
            Vector3 herd = HerdOrJacks(jacks);
            Vector3 toward = Vector3.ProjectOnPlane(aim - herd, up);
            if (toward.sqrMagnitude < 1f) return false;   // the herd is on the rival already: let it sting
            toward.Normalize();
            Vector3 side = Vector3.Cross(up, toward);

            float ring = Mathf.Max(1f, jacks.Ring);
            float spread = jacks.HerdCount > 0 ? jacks.HerdSpread : 0f;
            float back = spread + ring * StingherdSettingsSO.Rung(_settings.aiStandOffRings, d.Level);
            float flank = ring * StingherdSettingsSO.Rung(_settings.aiFlankRings, d.Level);
            float wide = spread + ring;   // the way round: beside the herd, clear of its rim

            Vector3 drive = herd - toward * back;
            plan = new JacksRacePlan
            {
                Jack0Via = herd + side * wide, Jack0To = drive + side * flank,
                Jack1Via = herd - side * wide, Jack1To = drive - side * flank,
                Jack0Lays = -1, Jack1Lays = -1, FirstTravel = -1, SecondTravel = -1,
            };
            return true;
        }
    }
}
