using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.UI;
using CosmicShore.Utility;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Hold the Core (Docs/Voyages/MILESTONE_2.md §6; the Voyage Codex): a named voyage whose work is to hold sails its
    /// cells to a contested core, the Ark anchors under the nucleus, and the hold clock (<see cref="VoyageCoreHold"/>) runs
    /// only while the squad's domain holds more of the nucleus than any other. Waves of the other domain's fauna come at
    /// the Ark (the assault clock, inside each wave's window); from difficulty 3 the other domain's crew comes to retake the
    /// core with wave 2, AI hulls that build and fight by the rules every AI plays. The hold is won when the clock runs
    /// out, lost when the core turns and stays theirs, or when the Ark falls (as anywhere). Nothing here touches a vessel.
    /// </summary>
    public sealed partial class VoyageRun
    {
        static readonly Domains[] s_coreDomains = { Domains.Jade, Domains.Ruby, Domains.Gold };

        VoyageCoreHold _core;
        float _coreStartedAt;
        float _coreWaveUntil = -1f;
        bool _coreWasYours;
        bool _coreWasTurning;

        /// <summary>A named voyage whose work is to hold: Hold the Core.</summary>
        static bool IsCoreHold(VoyageJob job) => job is { IsNamed: true, Verb: WorkVerb.Hold, IsReturn: false };

        /// <summary>A wave's fauna may assault the Ark at the core now.</summary>
        bool CoreWaveOn => _core is { State: VoyageCoreHold.Outcome.Holding } && Time.time < _coreWaveUntil && !_fallen;

        /// <summary>The Ark anchored under the core (from <see cref="OnRouteEnded"/>): the clock starts.</summary>
        void BeginCore(VoyageJob job)
        {
            _core = _def ? new VoyageCoreHold(_def.CoreHold(VoyageSea.DifficultyOf(job))) : null;
            _coreStartedAt = Time.time;
            _coreWaveUntil = -1f;
            _coreWasYours = _coreWasTurning = false;
            if (_core == null || Lines == null) return;
            VoyageToasts.Show(_sea, ArkwayVoyageHud.Mood.Event, TemplateText.Fill(Lines.ToastCoreAnchor, "to", PlaceName(job.To)),
                Lines.ToastCoreAnchorSub);
        }

        void ClearCore()
        {
            _core = null;
            _coreWaveUntil = -1f;
        }

        /// <summary>Every frame at a core: the nucleus's volumes into the clock, its toasts, its waves, its end.</summary>
        void TickCore()
        {
            if (_core is not { State: VoyageCoreHold.Outcome.Holding } || _fallen || _state.AtSea) return;
            var cell = CurrentCell;
            var ark = Ark;
            if (!cell || !ark) return;
            var squad = ark.Domain;
            float yours = cell.GetControlVolume(squad), theirs = 0f;
            foreach (var d in s_coreDomains)
                if (d != squad) theirs = Mathf.Max(theirs, cell.GetControlVolume(d));

            int began = _core.Tick(Time.deltaTime, yours, theirs);
            if (Lines != null)
            {
                if (_core.Yours && !_coreWasYours)
                    VoyageToasts.Show(_sea, ArkwayVoyageHud.Mood.Good, Lines.ToastCoreYours,
                        TemplateText.Fill(Lines.ToastCoreYoursSub, "left", VoyageHudColors.Clock(_core.Left)));
                bool turning = _core.Turned > 0f;
                if (turning && !_coreWasTurning)
                    VoyageToasts.Show(_sea, ArkwayVoyageHud.Mood.Threat, Lines.ToastCoreTurning,
                        TemplateText.Fill(Lines.ToastCoreTurningSub, "seconds", Mathf.CeilToInt(_core.TurnedLeft).ToString()));
                _coreWasTurning = turning;
            }
            _coreWasYours = _core.Yours;
            if (began > 0) BeginWave(began, ark, cell);

            if (_core.State == VoyageCoreHold.Outcome.Held) EndCore(true);
            else if (_core.State == VoyageCoreHold.Outcome.Turned) EndCore(false);
        }

        void BeginWave(int wave, Ark ark, Cell cell)
        {
            _coreWaveUntil = Time.time + (_def ? _def.CoreWaveSeconds : 25f);
            if (Lines != null)
                VoyageToasts.Show(_sea, ArkwayVoyageHud.Mood.Threat,
                    TemplateText.Fill(Lines.ToastCoreWave, "wave", wave.ToString(), "waves", _core.Plan.Waves.ToString()),
                    Lines.ToastCoreWaveSub);
            if (wave == 2 && _core.Plan.RetakeHulls > 0) SendRetake(_core.Plan.RetakeHulls, ark, cell);
        }

        /// <summary>
        /// The other domain's crew comes to retake the core: AI hulls of the domain that leads the rest of the nucleus (or
        /// another domain than the squad's), released by the host the way the rivals at a stop are, so they build and fight
        /// by the rules the AI already plays. They go back with the stop's other rivals.
        /// </summary>
        void SendRetake(int hulls, Ark ark, Cell cell)
        {
            var swapper = _context?.VesselInitializer;
            if (_following || !_def || !swapper || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
            var squad = ark.Domain;
            Domains rival = squad == Domains.Ruby ? Domains.Jade : Domains.Ruby;
            float most = 0f;
            foreach (var d in s_coreDomains)
            {
                if (d == squad) continue;
                float v = cell.GetControlVolume(d);
                if (v > most) { most = v; rival = d; }
            }
            var prefabs = swapper.VesselPrefabContainer;
            var list = _def.CoreRetakeHulls;
            int sent = 0;
            for (int i = 0; i < list.Count && sent < hulls; i++)
            {
                var hull = list[i];
                if (!VesselRequirement.IsConcrete(hull)) continue;
                if (prefabs && !prefabs.TryGetShipPrefab(hull, out _, reportMissing: false)) continue;
                var at = ark.Position + Quaternion.AngleAxis(120f * sent, Vector3.up) * ark.Forward * _def.CoreRetakeDistance
                         + Vector3.up * (30f * sent);
                var pose = new Pose(at, Quaternion.LookRotation(ark.Position - at, Vector3.up));
                swapper.RequestSpawnAiCompanion(hull, rival, pose, vessel =>
                {
                    if (!_active || _arrival == null || _core == null) { swapper.DespawnAiCompanion(vessel); return; }
                    _rivalVessels.Add(vessel);
                });
                sent++;
            }
            if (sent > 0 && Lines != null)
                VoyageToasts.Show(_sea, ArkwayVoyageHud.Mood.Threat, Lines.ToastCoreRetake,
                    TemplateText.Fill(Lines.ToastCoreRetakeSub, "count", sent.ToString()));
        }

        /// <summary>The hold is decided: the job resolves on it, and the end board asks what next.</summary>
        void EndCore(bool held)
        {
            var job = _state.Current;
            if (job == null || _core == null) return;
            _held = held;
            _coreWaveUntil = -1f;
            var r = VoyageRules.Resolve(_state, Content, held ? JobOutcome.Succeeded : JobOutcome.Failed);
            Chronicle(r);
            Relic(r);
            SaveState();
            VoyageHarbour.RaiseChanged();

            var board = new VoyageScoreboard
            {
                Board = VoyageScoreboard.Kind.CoreHold,
                Succeeded = held,
                To = PlaceName(job.To),
                Name = job.Title ?? string.Empty,
                Cells = _profile?.Count ?? job.Cells,
                Difficulty = VoyageSea.DifficultyOf(job),
                Seconds = Time.time - _legStartedAt,
                Held = _core.Held,
                HoldGoal = _core.Plan.HoldSeconds,
                Wave = Mathf.Max(1, _core.Wave),
                Waves = _core.Plan.Waves,
                Hull = Ark ? Ark.HealthFraction : 1f,
                Breach = job.BreachHull,
                Pay = held ? job.Pay : 0,
                Following = _following,
                Quote = PilotSays(held ? Lines.ScoreQuoteMet : Lines.ScoreQuoteMissed),
            };
            AddRows(board);
            TellScreens(board, job);
            if (!_following)
            {
                var next = held ? VoyageRaid.Venture(_state, Content, job) : null;
                if (next != null)
                {
                    board.NextCells = next.Cells;
                    board.NextDifficulty = VoyageSea.DifficultyOf(next);
                    board.Venture = () => SailOn(next);
                }
                board.Finish = SailHome;
            }
            VoyageScoreboard.Show(board);
        }

        /// <summary>The objective box at a Hold the Core voyage: on the way the core to reach, at the core the clock.</summary>
        void FillCoreObjective(VoyageJob job, ref VoyageObjective objective)
        {
            objective.Goal = VoyageObjective.Kind.CoreHold;
            objective.To = PlaceName(job.To);
            objective.Name = job.Title;
            objective.Breach = job.BreachHull;
            objective.Hostile = objective.Cell > 0 && LegHostile(objective.Cell - 1);
            var plan = _core?.Plan ?? (_def ? _def.CoreHold(VoyageSea.DifficultyOf(job)) : default);
            objective.HoldGoal = plan.HoldSeconds;
            objective.Waves = Mathf.Max(1, plan.Waves);
            if (_core == null || _state.AtSea) return;
            objective.AtCore = true;
            objective.Held = _core.Held;
            objective.Share = _core.Share;
            objective.CoreYours = _core.Yours;
            objective.Wave = Mathf.Max(1, _core.Wave);
            objective.TurnedLeft = _core.TurnedLeft;
        }
    }
}
