using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The AI pilot of Harvest: a REAPER that mows a sheaf's stalks with its chain, then turns in through the core.
    /// Server-only.
    ///
    /// <para><b>It presses ONE thing of the hull's, once: A, to leave Mass Mode.</b> Every Thresher spawns in Mass Mode
    /// (THRESHER.md §2b), where the ball steals and never explodes, and the AI pilots never press A - so without this an
    /// AI would score only its chain cuts. The press goes through the replicated path
    /// (<see cref="R_VesselActionHandler.PerformShipControllerActionsReplicated"/>), the same channel a pad writes, on
    /// the input the hull's own binding maps give its <see cref="ThresherActionSO.ThresherControl.MassMode"/> action
    /// (found by <see cref="ThresherActionSO.Control"/>, the sling policy's way). It checks every
    /// <see cref="HarvestSettingsSO.aiMassModeCheckSeconds"/> and presses only while the hull is still building, so a
    /// press lost on the way is pressed again and a press that landed is never undone. Kelpwood's precedent: a mode AI
    /// may press the hull's own button; the kit itself is untouched (the LOCKED vessel rule).</para>
    ///
    /// <para><b>Everything else is steering</b> (<see cref="AIPilot.SetExternalTargetProvider"/>). The hull's own
    /// <c>ThresherSlingAIPolicySO</c> still plants and winches on the corners; a second presser would fight it.</para>
    ///
    /// <para><b>The loop.</b></para>
    /// <list type="number">
    /// <item><b>Take a crystal</b> when a managed omni crystal is within the rung's reach: its windmill (12 s of a
    /// red-hot ball circling the ship) mows and smashes at once.</item>
    /// <item><b>Choose a sheaf</b>: the nearest one not in its own colour (it would score nothing) that it has not
    /// worked in the last <see cref="HarvestSettingsSO.aiRevisitSeconds"/>.</item>
    /// <item><b>Mow</b>: circle the sheaf just outside its reach, the sheaf inside the turn, so the ball swings OUTSIDE
    /// the stalks and the chain crosses them (THRESHER.md §9: a turn throws the ball outward). Every cut banks energy.</item>
    /// <item><b>Strike</b>: turn in through the core and on past it, dragging the ball across it: the first rival prism
    /// it meets spends the bank as one blast.</item>
    /// </list>
    ///
    /// <para><b>What it cannot do</b>: hold its bank on purpose (it blasts on whatever rival prism the ball meets first,
    /// a stalk included), aim a launch (the policy's aimed launch is off by default), or know that a sheaf is already
    /// gone (it remembers only when it worked one). <b>Difficulty is the seat's AI level, never the intensity</b>
    /// (<c>AIDifficulty.cs</c>). Plan step: <c>space-arcade</c> (Docs/AI_SYSTEM/AI_PLAN.md).</para>
    /// </summary>
    public sealed class HarvestReaper
    {
        enum Phase { Choose, Mow, Strike }

        sealed class Reaper
        {
            public IPlayer Player;
            public int Level;
            public IVesselStatus Status;
            public AIPilot Autopilot;
            public ThresherExecutor Thresher;
            public InputEvents? MassModeInput;
            public float NextMassCheckAt;

            public Phase Phase;
            public int Sheaf = -1;
            public float PhaseEndsAt = -1f;
            public float[] WorkedAt;
            public Vector3 Steer;
        }

        readonly List<Reaper> _reapers = new();
        readonly List<SpawnableHarvest.Sheaf> _sheaves = new();
        readonly List<ShipActionSO> _scratch = new(4);
        HarvestSettingsSO _settings;
        Vector3 _centre;

        public bool IsArmed => _reapers.Count > 0;

        /// <summary>Arms every AI pilot on the roster against <paramref name="sheaves"/> (world space).</summary>
        public void Arm(GameDataSO gameData, HarvestSettingsSO settings, Vector3 centre,
                        IReadOnlyList<SpawnableHarvest.Sheaf> sheaves)
        {
            Disarm();
            if (gameData == null || settings == null) return;
            _settings = settings;
            _centre = centre;
            _sheaves.Clear();
            if (sheaves != null) _sheaves.AddRange(sheaves);

            var players = gameData.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p == null || !p.IsInitializedAsAI) continue;
                var status = p.Vessel?.VesselStatus;
                if (status == null) continue;
                // TryGetComponent, not VesselStatus.AIPilot: that getter ADDS an autopilot to a hull without one.
                var autopilot = status is Component c && c && c.TryGetComponent(out AIPilot ai) ? ai : null;
                if (autopilot == null) continue;
                var handler = status.ActionHandler;
                var thresher = handler ? handler.Executors?.Get<ThresherExecutor>() : null;

                var reaper = new Reaper
                {
                    Player = p,
                    Level = (int)gameData.AIDifficultyFor(p),
                    Status = status,
                    Autopilot = autopilot,
                    Thresher = thresher,
                    WorkedAt = new float[_sheaves.Count],
                    Steer = centre,
                };
                for (int s = 0; s < reaper.WorkedAt.Length; s++) reaper.WorkedAt[s] = float.NegativeInfinity;
                _reapers.Add(reaper);
                var captured = reaper;
                autopilot.SetExternalTargetProvider(() => captured.Steer);
            }
        }

        /// <summary>Hands every autopilot back. Safe to call twice.</summary>
        public void Disarm()
        {
            for (int i = 0; i < _reapers.Count; i++)
                if (_reapers[i].Autopilot) _reapers[i].Autopilot.ClearExternalTargetProvider();
            _reapers.Clear();
        }

        public void Tick()
        {
            if (_settings == null) return;
            float now = Time.time;
            for (int i = 0; i < _reapers.Count; i++)
            {
                var r = _reapers[i];
                var self = r.Player?.Vessel?.Transform;
                if (!self || !r.Autopilot || !r.Autopilot.AutoPilotEnabled) continue;

                LeaveMassMode(r, now);

                if (NearestCrystal(r, self.position) is Vector3 crystal)
                {
                    r.Steer = crystal;
                    continue;
                }

                switch (r.Phase)
                {
                    case Phase.Choose:
                        r.Sheaf = ChooseSheaf(r, self.position, now);
                        if (r.Sheaf < 0) { r.Steer = _centre; break; }
                        r.Phase = Phase.Mow;
                        r.PhaseEndsAt = -1f;   // the mow clock starts on arrival
                        r.Steer = MowPoint(r, self);
                        break;

                    case Phase.Mow:
                    {
                        var sheaf = _sheaves[r.Sheaf];
                        float orbit = OrbitRadius(r, sheaf);
                        if (r.PhaseEndsAt < 0f && (self.position - sheaf.Centre).magnitude <= orbit + sheaf.CoreRadius)
                            r.PhaseEndsAt = now + HarvestSettingsSO.Rung(_settings.aiMowSeconds, r.Level);
                        if (r.PhaseEndsAt >= 0f && now >= r.PhaseEndsAt)
                        {
                            r.Phase = Phase.Strike;
                            r.PhaseEndsAt = now + _settings.aiStrikeSeconds;
                            var into = sheaf.Centre - self.position;
                            r.Steer = sheaf.Centre + (into.sqrMagnitude > 1e-4f ? into.normalized : self.forward) *
                                      _settings.aiStrikeOvershoot;
                        }
                        else r.Steer = MowPoint(r, self);
                        break;
                    }

                    case Phase.Strike:
                        // The strike line was fixed when it turned in; hold it.
                        if (now >= r.PhaseEndsAt)
                        {
                            r.WorkedAt[r.Sheaf] = now;
                            r.Phase = Phase.Choose;
                        }
                        break;
                }
            }
        }

        /// <summary>Press A while the hull is still building (it spawns in Mass Mode), at most once a check.</summary>
        void LeaveMassMode(Reaper r, float now)
        {
            if (!r.Thresher || now < r.NextMassCheckAt) return;
            r.NextMassCheckAt = now + _settings.aiMassModeCheckSeconds;
            if (!r.Thresher.InMassMode) return;
            var handler = r.Status.ActionHandler;
            if (!handler || !ResolveMassMode(r, handler)) return;
            handler.PerformShipControllerActionsReplicated(r.MassModeInput.Value);
            // A toggle ignores its release (ThresherActionSO), but every press is paired with one so no input is left held.
            handler.StopShipControllerActionsReplicated(r.MassModeInput.Value);
        }

        /// <summary>The input this hull's binding maps give its Mass Mode action; retried until the maps have filled.</summary>
        bool ResolveMassMode(Reaper r, R_VesselActionHandler handler)
        {
            if (r.MassModeInput.HasValue) return true;
            foreach (InputEvents candidate in System.Enum.GetValues(typeof(InputEvents)))
            {
                _scratch.Clear();
                handler.CollectBoundActions(candidate, _scratch);
                for (int a = 0; a < _scratch.Count; a++)
                {
                    if (_scratch[a] is ThresherActionSO t && t.Control == ThresherActionSO.ThresherControl.MassMode)
                    {
                        r.MassModeInput = candidate;
                        _scratch.Clear();
                        return true;
                    }
                }
            }
            _scratch.Clear();
            return false;
        }

        /// <summary>The nearest managed omni crystal this pilot may take, within its rung's reach; null when none.</summary>
        Vector3? NearestCrystal(Reaper r, Vector3 from)
        {
            float reach = HarvestSettingsSO.Rung(_settings.aiCrystalReach, r.Level);
            float bestSqr = reach * reach;
            Vector3? best = null;
            var crystals = Crystal.Active;
            for (int i = 0; i < crystals.Count; i++)
            {
                var crystal = crystals[i];
                // Manager-spawned only (RampageObjectiveProvider's filter): never a lifeform heart.
                if (!crystal || crystal.CrystalManager == null || !crystal.CanBeCollected(r.Player.Domain)) continue;
                float sqr = (crystal.transform.position - from).sqrMagnitude;
                if (sqr >= bestSqr) continue;
                bestSqr = sqr;
                best = crystal.transform.position;
            }
            return best;
        }

        int ChooseSheaf(Reaper r, Vector3 from, float now)
        {
            float revisit = HarvestSettingsSO.Rung(_settings.aiRevisitSeconds, r.Level);
            int best = -1;
            float bestSqr = float.MaxValue;
            for (int s = 0; s < _sheaves.Count; s++)
            {
                // Its own colour scores nothing for it (and a blast there would only cost its own team).
                if (_sheaves[s].Domain == r.Player.Domain) continue;
                if (now - r.WorkedAt[s] < revisit) continue;
                float sqr = (_sheaves[s].Centre - from).sqrMagnitude;
                if (sqr >= bestSqr) continue;
                bestSqr = sqr;
                best = s;
            }
            if (best < 0 && _sheaves.Count > 0)
            {
                // Everything worked recently: take the least recently worked hostile one.
                float oldest = float.MaxValue;
                for (int s = 0; s < _sheaves.Count; s++)
                {
                    if (_sheaves[s].Domain == r.Player.Domain || r.WorkedAt[s] >= oldest) continue;
                    oldest = r.WorkedAt[s];
                    best = s;
                }
            }
            return best;
        }

        float OrbitRadius(Reaper r, SpawnableHarvest.Sheaf sheaf) =>
            sheaf.Reach + HarvestSettingsSO.Rung(_settings.aiMowStandoff, r.Level);

        /// <summary>A point on the mowing circle a little ahead of the ship, in the plane it is flying in.</summary>
        Vector3 MowPoint(Reaper r, Transform self)
        {
            var sheaf = _sheaves[r.Sheaf];
            Vector3 rel = self.position - sheaf.Centre;
            Vector3 radial = rel.sqrMagnitude > 1e-4f ? rel.normalized : Vector3.right;
            Vector3 tangent = Vector3.ProjectOnPlane(self.forward, radial);
            if (tangent.sqrMagnitude < 1e-4f) tangent = Vector3.Cross(radial, Mathf.Abs(radial.y) < 0.9f ? Vector3.up : Vector3.right);
            tangent.Normalize();
            float lead = _settings.aiMowLeadDegrees * Mathf.Deg2Rad;
            return sheaf.Centre + (radial * Mathf.Cos(lead) + tangent * Mathf.Sin(lead)) * OrbitRadius(r, sheaf);
        }
    }
}
