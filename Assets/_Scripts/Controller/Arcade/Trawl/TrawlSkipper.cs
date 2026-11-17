using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The AI pilot of Trawl: a SKIPPER that flies its net through the field's densest hostile mass. Server-only.
    ///
    /// <para><b>Input-only - the Jacks' kit is untouched.</b> The skipper is the hull's race coach
    /// (<see cref="IJacksRaceCoach"/>, the seam Decoy Run built) that never offers a cycle, so an AI Jacks stays in its
    /// netting mode for the whole match instead of its shipped 14 s herd / 6 s net rhythm - which is what a person does
    /// in this game (the herd scores nothing here). The flight is the hull's own <see cref="AIPilot"/>, steered through
    /// <see cref="AIPilot.SetExternalTargetProvider"/> at the densest stand of mass hostile to its domain
    /// (<see cref="Cell.GetExplosionTarget"/>, the query <c>HostileMassObjectiveProvider</c>'s arrow reads, which already
    /// leaves out its own colour and shielded mass: exactly what the net can score) and on past it along the line it came
    /// in on, so the trailing net sweeps the stand. It presses nothing.</para>
    ///
    /// <para><b>The net does the scoring.</b> What crosses the mouth is caught and dragged; a dragged prism that runs into
    /// another destroys both, scored to the net (JACKS.md §1.1, <c>PrismMotion.Annihilate</c>).</para>
    ///
    /// <para><b>Difficulty is the seat's AI level, never the intensity</b> (<c>AIDifficulty.cs</c>).</para>
    /// </summary>
    public sealed class TrawlSkipper : IJacksRaceCoach
    {
        sealed class Skipper
        {
            public IPlayer Player;
            public int Level;
            public IVesselStatus Status;
            public AIPilot Autopilot;
            public JacksExecutor Jacks;
            public System.Random Rng;

            public Vector3 Steer;
            public float NextRetargetAt;
        }

        readonly List<Skipper> _skippers = new();
        TrawlSettingsSO _settings;
        Cell _cell;
        Vector3 _centre;

        public bool IsArmed => _skippers.Count > 0;

        /// <summary>Coaches and steers every AI Jacks on the roster in the field of <paramref name="cell"/>.</summary>
        public void Arm(GameDataSO gameData, TrawlSettingsSO settings, Cell cell, Vector3 centre)
        {
            Disarm();
            if (gameData == null || settings == null) return;
            _settings = settings;
            _cell = cell;
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
                if (autopilot == null) continue;
                var jacks = status.Transform.GetComponentInChildren<JacksExecutor>(true);
                if (!jacks) continue;   // the card seats only the Jacks; anything else flies the platform's steering

                var skipper = new Skipper
                {
                    Player = p,
                    Level = (int)gameData.AIDifficultyFor(p),
                    Status = status,
                    Autopilot = autopilot,
                    Jacks = jacks,
                    Rng = new System.Random(unchecked((int)p.PlayerNetId * 7919 + 118)),
                    Steer = centre,
                };
                _skippers.Add(skipper);
                jacks.RaceCoach = this;
                var captured = skipper;
                autopilot.SetExternalTargetProvider(() => captured.Steer);
            }
        }

        /// <summary>Hands every hull back to its shipped AI rhythm. Safe to call twice.</summary>
        public void Disarm()
        {
            for (int i = 0; i < _skippers.Count; i++)
            {
                var s = _skippers[i];
                if (s.Jacks && ReferenceEquals(s.Jacks.RaceCoach, this)) s.Jacks.RaceCoach = null;
                if (s.Autopilot) s.Autopilot.ClearExternalTargetProvider();
            }
            _skippers.Clear();
        }

        /// <summary>Never a cycle: the AI nets for the whole match (it is flying whenever no cycle is offered).</summary>
        public bool TryNextCycle(JacksExecutor jacks, out JacksRacePlan plan)
        {
            plan = default;
            return false;
        }

        /// <summary>Re-chooses each skipper's run on its own clock.</summary>
        public void Tick()
        {
            if (_settings == null) return;
            float now = Time.time;
            for (int i = 0; i < _skippers.Count; i++)
            {
                var s = _skippers[i];
                var tf = s.Status?.Transform;
                if (!tf || now < s.NextRetargetAt) continue;
                s.NextRetargetAt = now + TrawlSettingsSO.Rung(_settings.aiRetargetSeconds, s.Level);

                Vector3 stand = _cell ? _cell.GetExplosionTarget(s.Status.Domain) : _centre;
                float err = TrawlSettingsSO.Rung(_settings.aiAimError, s.Level);
                stand += new Vector3(Signed(s.Rng), Signed(s.Rng), Signed(s.Rng)).normalized * err;
                Vector3 run = stand - tf.position;
                Vector3 along = run.sqrMagnitude > 1f ? run.normalized : tf.forward;
                s.Steer = stand + along * _settings.aiOvershoot;
            }
        }

        static float Signed(System.Random rng) => (float)rng.NextDouble() * 2f - 1f;
    }
}
