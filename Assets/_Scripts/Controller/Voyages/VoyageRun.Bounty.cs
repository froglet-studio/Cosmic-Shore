using System.Collections.Generic;
using System.Threading;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.UI;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Bounty (Docs/Voyages/MILESTONE_2.md section 7; the Voyage Codex; Frogma Rev 137, approved by Shombith 2026-11-24): a
    /// named voyage that hunts a named rival crew. The squad sails its cells to where the crew stands; there the crew is
    /// met, and the warrant is served by beating it before it makes port (<see cref="VoyageBountyFight"/>):
    /// <list type="bullet">
    /// <item>A crew that sails its own Ark (<see cref="RivalCrew.SailsArk"/>) brings her: a smaller Ark in the crew's
    /// domain, sailing for its port across the cell, her escorts the crew's hulls. Her escorts wall her hull (it is
    /// super-shielded); silenced escorts cannot wall, so with enough of them silenced at once her hull is open. Break it to
    /// her break line and her hold bursts as loose crystals and the crew turns back.</item>
    /// <item>A runaway crew has no Ark: three hulls and nothing to lose. Silence all three at the same moment and they
    /// rout, dropping their haul as loose crystals.</item>
    /// </list>
    /// The crew's hulls are AI vessels released by the host the way the rivals at a stop are, so they build and fight by
    /// the rules every AI plays (PvP is petals only); nothing here touches a vessel. The fight's end resolves the job: the
    /// warrant served is the job done (the crew is caught: its grudge grows, it is off the sea a while).
    /// </summary>
    public sealed partial class VoyageRun
    {
        VoyageBountyFight _bounty;
        RivalCrew _bountyCrew;
        Domains _bountyDomain;
        Vector3 _bountyCentre;
        float _bountyLeaveAt = -1f;
        bool _bountyWasOpen;
        Ark _rivalArk;
        CancellationTokenSource _rivalArkCts;
        bool _rivalWalled;
        readonly List<VesselClassType> _bountyHulls = new();
        /// <summary>The crew's hulls by escort slot (null until the host's spawn lands; a slot whose hull is gone after it
        /// landed walls nothing).</summary>
        readonly List<IVessel> _bountyEscorts = new();
        readonly List<bool> _bountyLanded = new();
        readonly List<float> _bountySilence = new();
        readonly VoyageRivalFix _bountyFix = new();
        readonly VoyageRivalBlows _bountyBlows = new();
        readonly List<Prism> _rivalPlates = new();

        /// <summary>The silence a hull that left the sea reads as (it walls nothing, and the plate shows it down).</summary>
        const float GoneSilence = 99f;

        /// <summary>A named voyage that hunts a crew: a Bounty.</summary>
        static bool IsBounty(VoyageJob job) =>
            job is { IsNamed: true, Kind: JobKind.Bounty, IsReturn: false } && !string.IsNullOrEmpty(job.RivalId);

        /// <summary>The hunted crew is in sight and the fight is on.</summary>
        bool BountyOn => _bounty is { State: VoyageBountyFight.Outcome.Fighting } && !_fallen;

        /// <summary>The Ark reached where the hunted crew stands (from <see cref="OnRouteEnded"/>): the crew is met.</summary>
        void BeginBounty(VoyageJob job)
        {
            ClearBounty();
            var ark = Ark;
            if (!_def || !ark) return;
            _bountyCrew = Content?.Rival(job.RivalId);
            if (_bountyCrew == null)
                CSDebug.LogWarning($"[Voyage] Bounty '{job.VoyageId}' names rival '{job.RivalId}', which the content does not " +
                                   "hold - nobody is met, and the crew counts as gone when the clock runs out.");
            bool sailsArk = _bountyCrew is { SailsArk: true };
            _bounty = new VoyageBountyFight(_def.Bounty(VoyageSea.DifficultyOf(job)), sailsArk);
            _bountyDomain = RivalDomain(_bountyCrew, ark.Domain);
            _bountyCentre = ark.Position + ark.transform.right * _def.BountyDistance + Vector3.up * 30f;

            var prefabs = _context?.VesselInitializer ? _context.VesselInitializer.VesselPrefabContainer : null;
            foreach (var name in _bountyCrew?.Hulls ?? new List<string>())
            {
                if (_bountyHulls.Count >= 3) break;
                if (!System.Enum.TryParse(name, true, out VesselClassType hull) || !VesselRequirement.IsConcrete(hull)) continue;
                if (prefabs && !prefabs.TryGetShipPrefab(hull, out _, reportMissing: false)) continue;
                _bountyHulls.Add(hull);
                _bountyEscorts.Add(null);
                _bountyLanded.Add(false);
                _bountySilence.Add(0f);
            }

            if (sailsArk) LayRivalArk(ark).Forget();
            SendBountyCrew(ark);
            VesselSilence.Changed += OnBountySilence;
            FillBountyFix();
            VoyageScreenToasts.Rival(_sea, Lines, _bountyFix);
        }

        /// <summary>The crew's colour: its own, or another when it is the squad's (a crew of your colour would fight beside
        /// you, and this one is wanted).</summary>
        static Domains RivalDomain(RivalCrew crew, Domains squad)
        {
            var d = crew?.Domain ?? Domains.Ruby;
            if (d != squad && d is Domains.Jade or Domains.Ruby or Domains.Gold) return d;
            return squad == Domains.Ruby ? Domains.Gold : Domains.Ruby;
        }

        /// <summary>The host releases the crew's hulls round where they are met, facing the squad's Ark, as AI vessels in
        /// the crew's domain (the rivals at a stop come the same way). They go back with the stop's other rivals.</summary>
        void SendBountyCrew(Ark ark)
        {
            var swapper = _context?.VesselInitializer;
            if (_following || !swapper || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
            for (int i = 0; i < _bountyHulls.Count; i++)
            {
                int slot = i;
                var at = _bountyCentre + Quaternion.AngleAxis(120f * i, ark.Forward) * Vector3.up * _def.BountySpread;
                var pose = new Pose(at, Quaternion.LookRotation(ark.Position - at, Vector3.up));
                swapper.RequestSpawnAiCompanion(_bountyHulls[i], _bountyDomain, pose, vessel =>
                {
                    if (!_active || _bounty == null || _arrival == null) { swapper.DespawnAiCompanion(vessel); return; }
                    _rivalVessels.Add(vessel);
                    if (slot < _bountyEscorts.Count)
                    {
                        _bountyEscorts[slot] = vessel;
                        _bountyLanded[slot] = true;
                    }
                });
            }
        }

        /// <summary>
        /// A crew that sails an Ark brings her (every machine lays its own, from the same numbers): a smaller Ark in the
        /// crew's domain, drawn as the squad's model, no reactor (what breaks stays broken), sailing across the cell for its
        /// port at the pace that takes the whole clock. Its plates' losses are credited to whoever broke them.
        /// </summary>
        async UniTaskVoid LayRivalArk(Ark squad)
        {
            if (_arkCfg == null || !_arkCfg.PrismPrefab) return;
            _rivalArkCts = new CancellationTokenSource();
            var ct = _rivalArkCts.Token;
            var heading = squad.Forward;
            var at = _bountyCentre - heading * (_def.BountyDistance * 0.5f);
            var rival = Ark.Create(_root, at, Quaternion.LookRotation(heading, Vector3.up));
            _rivalArk = rival;
            rival.UseModel(_arkCfg.ArkModel, ToyFactory.Theme(_context));
            float travel = _def.BountyDistance * 2f;
            float speed = travel / Mathf.Max(10f, _bounty?.Plan.EscapeSeconds ?? 90f);
            try
            {
                await rival.LayHullAsync(_arkCfg.PrismPrefab, _bountyDomain, Mathf.Max(30f, _arkCfg.ArkHullLength * _def.RivalArkScale),
                    speed, ct);
            }
            catch (System.OperationCanceledException)
            {
                return;
            }
            if (!rival || _rivalArk != rival || !BountyOn) return;
            rival.HullPrismLost += OnRivalPlateLost;
            rival.SetSpeedProfile(speed, speed, 0f);
            rival.SetDestination(at + heading * travel);
            rival.SetUnderway(true);
            _rivalWalled = false;
            Wall(true);
        }

        void OnRivalPlateLost(Prism prism, PrismKind kind)
        {
            if (prism && !prism.DestroyedByCreature) _bountyBlows.Add(prism.DestroyerName);
        }

        /// <summary>One of the crew's hulls was silenced: the squad seat nearest it struck the blow (silence carries no
        /// attacker; the hit that silenced it was flown close).</summary>
        void OnBountySilence(string player)
        {
            if (!BountyOn || _bounty.SailsArk || string.IsNullOrEmpty(player)) return;
            for (int i = 0; i < _bountyEscorts.Count; i++)
            {
                var status = Alive(_bountyEscorts[i]);
                if (status == null || status.PlayerName != player) continue;
                _bountyBlows.Add(NearestSeat(status.Transform.position));
                return;
            }
        }

        string NearestSeat(Vector3 at)
        {
            string best = null;
            float bestSq = float.MaxValue;
            void Try(IVesselStatus s)
            {
                if (s == null || !s.Transform) return;
                float d = (s.Transform.position - at).sqrMagnitude;
                if (d < bestSq) { bestSq = d; best = s.PlayerName; }
            }
            Try(LocalVessel());
            foreach (var w in _wingmates) Try(Alive(w));
            foreach (var (status, _) in OtherHumans()) Try(status);
            return best;
        }

        /// <summary>Her escorts wall her hull: every live plate super-shielded while walled, every shield off while open
        /// (silenced escorts cannot wall).</summary>
        void Wall(bool walled)
        {
            if (!_rivalArk || _rivalWalled == walled) return;
            _rivalWalled = walled;
            _rivalArk.GetLiveHullPrisms(_rivalArk.Position, _rivalPlates);
            foreach (var plate in _rivalPlates)
            {
                if (!plate) continue;
                if (walled) plate.ActivateSuperShield();
                else plate.DeactivateShields();
            }
            _rivalPlates.Clear();
        }

        /// <summary>Every frame while the crew is in sight: each hull's silence, her hull, the wall, the screens, the end.</summary>
        void TickBounty()
        {
            if (_bounty == null) return;
            if (_bounty.State != VoyageBountyFight.Outcome.Fighting)
            {
                if (_bountyLeaveAt > 0f && Time.time >= _bountyLeaveAt) SendCrewHome();
                return;
            }
            if (_fallen || _state.AtSea) return;
            for (int i = 0; i < _bountyEscorts.Count; i++)
            {
                var status = Alive(_bountyEscorts[i]);
                // A hull that landed and is gone walls nothing; one not landed yet stands guard.
                _bountySilence[i] = status != null ? VesselSilence.RemainingSeconds(status.PlayerName)
                    : _bountyLanded[i] ? GoneSilence : 0f;
            }
            bool decided = _bounty.Tick(Time.deltaTime, _bountySilence, _rivalArk ? _rivalArk.HealthFraction : 1f);
            if (_rivalArk && _rivalArk.TotalCount > 0) Wall(!_bounty.Open);
            FillBountyFix();

            // Rev 137 ArkOpen: her hull opened.
            if (_bounty.Open && !_bountyWasOpen) VoyageScreenToasts.Rival(_sea, Lines, _bountyFix);
            _bountyWasOpen = _bounty.Open;
            if (decided) EndBounty();
        }

        void FillBountyFix()
        {
            if (_bounty == null) return;
            _bounty.Fill(_bountyFix, _bountyCrew, _bountyHulls, _bountySilence);
            // Rev 137 Runaway: a runaway crew raids your drift; a rival Ark races you for the same water.
            if (_bounty.State == VoyageBountyFight.Outcome.Fighting && !_bounty.SailsArk) _bountyFix.Act = VoyageRivalAct.Attacking;
            _bountyFix.BoundFor = PlaceName(_state?.Current?.To ?? _state?.At);
            _bountyFix.Cell = Mathf.Max(1, LegCell(out _));
            _bountyFix.Bearing = 90f;
            _rivals.Clear();
            if (_bounty.State is VoyageBountyFight.Outcome.Fighting or VoyageBountyFight.Outcome.Routed
                or VoyageBountyFight.Outcome.Broken) _rivals.Add(_bountyFix);
        }

        /// <summary>The fight is decided: what they carried spills (beaten), the crew turns back or runs, and the job
        /// resolves on it; the end board asks what next.</summary>
        void EndBounty()
        {
            var job = _state.Current;
            bool beaten = _bounty.Beaten;
            int spilled = beaten ? _bounty.Plan.Hold : 0;
            Vector3 spillAt = _rivalArk ? _rivalArk.Position : EscortsCentre();
            if (spilled > 0)
                SpillLoose(CurrentCell ? CurrentCell.transform : _root, spillAt, spilled, _def.BountySpread,
                    new System.Random(VoyageRules.Hash(_state.Day, job?.VoyageId ?? string.Empty, "bounty")));
            if (_rivalArk)
            {
                Wall(false);
                // She turns back for her port of origin when broken; she sails on into port when she got away.
                if (_bounty.State == VoyageBountyFight.Outcome.Broken)
                    _rivalArk.SetDestination(_rivalArk.Position - _rivalArk.Forward * _def.BountyDistance * 3f);
            }
            if (Lines != null)
            {
                if (_bounty.State == VoyageBountyFight.Outcome.Escaped)
                    VoyageToasts.Show(_sea, ArkwayVoyageHud.Mood.Threat, TemplateText.Fill(Lines.ToastRivalEscaped, "name", _bountyFix.Name),
                        Lines.ToastRivalEscapedSub);
                else
                {
                    VoyageScreenToasts.Rival(_sea, Lines, _bountyFix, spilled);
                    if (_bounty.State == VoyageBountyFight.Outcome.Broken)
                        VoyageToasts.Show(_sea, ArkwayVoyageHud.Mood.Good,
                            TemplateText.Fill(Lines.ToastRivalTurnBack, "name", _bountyFix.Name), Lines.ToastRivalTurnBackSub);
                }
            }
            _bountyLeaveAt = Time.time + _def.BountyLeaveSeconds;
            VesselSilence.Changed -= OnBountySilence;
            if (job == null) return;

            _held = beaten;
            var r = VoyageRules.Resolve(_state, Content, beaten ? JobOutcome.Succeeded : JobOutcome.Failed);
            Chronicle(r);
            Relic(r);
            SaveState();
            VoyageHarbour.RaiseChanged();

            var board = new VoyageScoreboard
            {
                Board = VoyageScoreboard.Kind.Bounty,
                Succeeded = beaten,
                To = PlaceName(job.To),
                Name = job.Title ?? string.Empty,
                Rival = _bountyFix.Name,
                BountyEnd = _bounty.State,
                Cells = _profile?.Count ?? job.Cells,
                Difficulty = VoyageSea.DifficultyOf(job),
                Seconds = Time.time - _legStartedAt,
                Hull = Ark ? Ark.HealthFraction : 1f,
                Breach = job.BreachHull,
                Pay = beaten ? job.Pay : 0,
                Following = _following,
                Quote = PilotSays(beaten ? Lines.ScoreQuoteMet : Lines.ScoreQuoteMissed),
            };
            AddRows(board);
            foreach (var row in board.Rows) row.RivalHull = _bountyBlows.Share(row.Name);
            TellScreens(board, job);
            if (!_following)
            {
                var next = beaten ? VoyageRaid.Venture(_state, Content, job) : null;
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

        Vector3 EscortsCentre()
        {
            Vector3 sum = Vector3.zero;
            int n = 0;
            foreach (var v in _bountyEscorts)
            {
                var s = Alive(v);
                if (s == null || !s.Transform) continue;
                sum += s.Transform.position;
                n++;
            }
            return n > 0 ? sum / n : _bountyCentre;
        }

        /// <summary>The beaten or escaped crew leaves: its hulls go back to the host, its Ark sails out of sight.</summary>
        void SendCrewHome()
        {
            _bountyLeaveAt = -1f;
            var swapper = _context?.VesselInitializer;
            foreach (var v in _bountyEscorts)
            {
                if (v == null || Alive(v) == null) continue;
                _rivalVessels.Remove(v);
                if (swapper) swapper.DespawnAiCompanion(v);
            }
            _rivals.Clear();
            RetireRivalArk();
        }

        void RetireRivalArk()
        {
            _rivalArkCts?.Cancel();
            _rivalArkCts?.Dispose();
            _rivalArkCts = null;
            var rival = _rivalArk;
            _rivalArk = null;
            _rivalWalled = false;
            if (!rival) return;
            rival.HullPrismLost -= OnRivalPlateLost;
            rival.RetireAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        /// <summary>The stop is cleared (the hulls go back with its other rivals in <see cref="ClearStop"/>).</summary>
        void ClearBounty()
        {
            VesselSilence.Changed -= OnBountySilence;
            RetireRivalArk();
            _bounty = null;
            _bountyCrew = null;
            _bountyLeaveAt = -1f;
            _bountyWasOpen = false;
            _bountyHulls.Clear();
            _bountyEscorts.Clear();
            _bountyLanded.Clear();
            _bountySilence.Clear();
            _bountyBlows.Clear();
            _rivals.Clear();
        }

        /// <summary>The objective box at a Bounty: on the way the crew to reach; met, the fight's state and the clock.</summary>
        void FillBountyObjective(VoyageJob job, ref VoyageObjective objective)
        {
            objective.Goal = VoyageObjective.Kind.Bounty;
            objective.To = PlaceName(job.To);
            objective.Name = job.Title;
            objective.Breach = job.BreachHull;
            objective.Hostile = objective.Cell > 0 && LegHostile(objective.Cell - 1);
            var crew = _bountyCrew ?? Content?.Rival(job.RivalId);
            objective.Rival = crew?.Name ?? string.Empty;
            objective.RivalArk = crew is { SailsArk: true } ? crew.ArkName : string.Empty;
            if (_bounty == null || _state.AtSea) return;
            objective.RivalMet = true;
            objective.RivalSilenced = _bounty.Silenced;
            objective.RivalEscorts = _bounty.Escorts;
            objective.RivalHull = _bounty.Hull;
            objective.RivalBreakAt = _bounty.Plan.BreakAt;
            objective.RivalOpen = _bounty.Open;
            objective.RivalOpenSeconds = _bountyFix.OpenSeconds;
            objective.RivalLeft = _bounty.Left;
        }
    }
}
