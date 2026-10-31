using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// One <b>Ark escort</b>, played (<see cref="ArkEscortSO"/>, Docs/Voyages/ARK_ESCORT.md): the squad deploys, the Ark
    /// sails the escort's worlds in order, and the escort ends when the Ark clears the last world or falls. The sea is the
    /// Arkway's (<see cref="ArkwayRun"/> with a route); the difficulty reaches it only through numbers the corridor
    /// already has: each world's population (<see cref="Cell.RuntimePopulationScale"/>), who controls the world when the
    /// Ark arrives (<see cref="Cell.SetReplicatedStartingController"/>), the reactor's regrowth and the Ark's speed.
    ///
    /// <para>Every AI seat escorts (<see cref="ArkEscortPilot"/>): it hunts the hostile creatures nearest the Ark, and
    /// holds a station beside it when there are none.</para>
    ///
    /// <para>Every run writes an <see cref="ArkEscortReport"/> (each world's hull and threat, how it ended), raised on
    /// <see cref="Finished"/>; the escort sim (<c>ArkEscortSim</c>) logs it, so the tiers are tuned from what happened.</para>
    /// </summary>
    public sealed class ArkEscortRun : MonoBehaviour
    {
        const float BannerSeconds = 5f;
        const float SampleSeconds = 1f;
        const float LeashCheckSeconds = 2f;
        const float WingmateAbeam = 140f;
        const float EndAfterSurvivedSeconds = 4f;

        /// <summary>Raised once per escort as it ends, with what happened.</summary>
        public static event System.Action<ArkEscortReport> Finished;

        VoyageToyDefinitionSO _def;
        ArkEscortSO _escort;
        ToyContext _context;
        Transform _root;
        System.Action _onEnded;

        ArkwayRun _sea;
        CellConveyor _corridor;
        ArkwayConfig _cfg;
        ArkReactorSO _reactor;

        SavedSquad _squad;
        int _difficulty;
        bool _aiLocal;
        bool _active, _survived, _fallen;
        float _endAt = -1f;
        float _nextSample, _nextLeash;
        System.Random _rng;

        readonly List<IVessel> _wingmates = new();
        readonly HashSet<IVesselStatus> _seeded = new();
        readonly List<ArkEscortPilot> _pilots = new();
        readonly Dictionary<Cell, int> _legOf = new();
        readonly List<Cell> _cells = new();
        readonly List<CellRuntimeDataSO> _killFeeds = new();

        /// <summary>A creature died to an active force in one of the escort's worlds (a squad kill, mostly).</summary>
        void OnFaunaKilled(string killer)
        {
            if (!_active || _leg < 0) return;
            _report.LegReports[_leg].Kills++;
            _report.Kills++;
        }

        /// <summary>A hull prism fell to something other than a creature: in the escort that is the squad's own fire
        /// (a rocket's blast, a gun's round, a blast cone through the hull), the number the roles' lane tests exist to
        /// keep at zero.</summary>
        void OnHullPrismLost(Prism prism)
        {
            if (!_active || !prism || prism.DestroyedByCreature) return;
            _report.FriendlyFire++;
            if (_leg >= 0) _report.LegReports[_leg].FriendlyFire++;
            string who = string.IsNullOrEmpty(prism.DestroyerName) ? "(unnamed)" : prism.DestroyerName;
            _friendlyFireBy[who] = _friendlyFireBy.TryGetValue(who, out int n) ? n + 1 : 1;
        }

        readonly Dictionary<string, int> _friendlyFireBy = new();

        void WriteFriendlyFireBy()
        {
            _report.FriendlyFireBy.Clear();
            foreach (var kv in _friendlyFireBy) _report.FriendlyFireBy.Add($"{kv.Key}:{kv.Value}");
            _report.FriendlyFireBy.Sort((a, b) => Count(b).CompareTo(Count(a)));
            static int Count(string s) => int.TryParse(s.Substring(s.LastIndexOf(':') + 1), out int c) ? c : 0;
        }

        ArkEscortReport _report;
        int _leg = -1;
        ArkEscortContext _context_;

        /// <summary>True from setting sail until the escort ends.</summary>
        public bool IsActive => _active;

        /// <summary>What the escort's seats share: threats ranked, claims, the lane-clear test. Null between escorts.</summary>
        public ArkEscortContext Context => _context_;

        /// <summary>The world the Ark is in or bound for, or null.</summary>
        public Cell CurrentCell => _corridor ? _corridor.CurrentCell : null;

        /// <summary>Half the Ark hull's length: the sphere a weapon's lane keeps clear.</summary>
        public float HullHalfLength => _cfg != null ? Mathf.Max(30f, _cfg.ArkHullLength) * 0.5f : 55f;

        /// <summary>The difficulty sailing now (1-9), or 0.</summary>
        public int Difficulty => _active ? _difficulty : 0;

        /// <summary>The live Ark, or null.</summary>
        public Ark Ark => _sea ? _sea.Ark : null;

        /// <summary>The worlds the escort has stood that are still standing (the AI hunts in these).</summary>
        public IReadOnlyList<Cell> Cells => _cells;

        public void Configure(VoyageToyDefinitionSO definition, ArkEscortSO escort, ToyContext context,
            Transform toyboxRoot, System.Action onEnded)
        {
            _def = definition;
            _escort = escort;
            _context = context;
            _root = toyboxRoot;
            _onEnded = onEnded;
        }

        // ── Set sail ─────────────────────────────────────────────────────────

        /// <summary>
        /// Deploy <paramref name="squad"/> and sail the escort at <paramref name="difficulty"/> (1-9).
        /// <paramref name="aiLocal"/> hands the local pilot's own seat to the AI too (the sim).
        /// </summary>
        public bool SetSail(IVesselStatus local, SavedSquad squad, int difficulty, bool aiLocal)
        {
            if (_active || local?.Transform == null || !_def || !_escort || squad == null) return false;
            var tier = _escort.TierFor(difficulty);
            if (tier == null || _escort.Legs.Count == 0)
            {
                CSDebug.LogWarning("[ArkEscort] The escort authors no tiers or no worlds - it cannot sail.");
                return false;
            }

            _squad = squad;
            _difficulty = Mathf.Clamp(difficulty, ArkEscortSO.MinDifficulty, ArkEscortSO.MaxDifficulty);
            _aiLocal = aiLocal;
            _rng = new System.Random(_difficulty * 7919 + System.Environment.TickCount);
            _survived = _fallen = false;
            _endAt = -1f;
            _leg = -1;
            _legOf.Clear();
            _cells.Clear();
            _seeded.Clear();
            _friendlyFireBy.Clear();

            EnsureSea();
            _cfg = _def.WithArk ? _def.WithArk.BuildConfig(_escort.VoyageName) : new ArkwayConfig { DisplayName = _escort.VoyageName };
            _cfg.Cells = _escort.Worlds(_difficulty);
            _cfg.ArkSpeed *= tier.ArkSpeed;
            if (_reactor) Destroy(_reactor);
            _reactor = _cfg.ArkReactor ? _cfg.ArkReactor.WithRegrowSeconds(tier.RegrowSeconds) : null;
            if (_reactor) _cfg.ArkReactor = _reactor;

            var route = Route();
            if (route.Count == 0) return false;

            _sea.Configure(_cfg, _context, _context?.Container, _corridor, OnSeaEnded);
            _corridor.PrepareCell = PrepareCell;
            _sea.SetRoute(route);

            _report = new ArkEscortReport
            {
                Difficulty = _difficulty,
                Legs = _escort.Legs.Count,
                Squad = SquadName(squad),
                StartedAt = Time.time,
            };
            for (int i = 0; i < _escort.Legs.Count; i++)
                _report.LegReports.Add(new ArkEscortReport.LegReport
                {
                    Leg = i,
                    Name = _escort.Legs[i].Name,
                    Population = _escort.PopulationFor(i, _difficulty),
                    World = _escort.WorldFor(i, _difficulty) ? _escort.WorldFor(i, _difficulty).name : string.Empty,
                });

            _active = true;
            _context_?.Uninstall();
            _context_ = new ArkEscortContext(this);
            _context_.Install();
            Deploy(local, VoyageRun.LiveDomain(_context));
            _sea.Begin(local);
            return true;
        }

        /// <summary>The route: every leg's world in order (the corridor takes the escort's list as authored).</summary>
        List<int> Route()
        {
            _cfg.RouteWorldsAsAuthored = true;
            var route = new List<int>();
            for (int i = 0; i < _escort.Legs.Count; i++)
            {
                if (!_escort.WorldFor(i, _difficulty))
                {
                    CSDebug.LogWarning($"[ArkEscort] Leg '{_escort.Legs[i].Name}' names no world - the escort refuses to sail.");
                    return new List<int>();
                }
                route.Add(i);
            }
            return route;
        }

        /// <summary>Each world the corridor stands, tuned for this leg before its spawners start.</summary>
        void PrepareCell(Cell cell, int routePosition)
        {
            if (!cell || routePosition < 0 || routePosition >= _escort.Legs.Count) return;
            var leg = _escort.Legs[routePosition];
            var tier = _escort.TierFor(_difficulty);
            cell.RuntimePopulationScale = _escort.PopulationFor(routePosition, _difficulty);
            bool hostile = leg.Hostile && _difficulty >= leg.HostileFrom && _rng.NextDouble() < tier.HostileShare;
            if (hostile)
                cell.SetReplicatedStartingController(CellControlRules.Opposing(VoyageRun.LiveDomain(_context)));
            _legOf[cell] = routePosition;
            _cells.Add(cell);
            if (cell.RuntimeData)
            {
                cell.RuntimeData.OnFaunaKilled.OnRaised -= OnFaunaKilled;
                cell.RuntimeData.OnFaunaKilled.OnRaised += OnFaunaKilled;
                _killFeeds.Add(cell.RuntimeData);
            }
            _report.LegReports[routePosition].Hostile = hostile;
        }

        void Deploy(IVesselStatus local, Domains domain)
        {
            int mySeat = Mathf.Clamp(_squad.MySeat, 0, SavedSquad.SlotCount - 1);
            SquadDeployment.Deploy(_squad, mySeat);

            var mine = _squad.GetSlot(mySeat);
            var swapper = _context?.VesselInitializer;
            if (swapper && VesselRequirement.IsConcrete(mine.Vessel) && mine.Vessel != local.VesselType)
                swapper.RequestSwap(mine.Vessel);

            if (!swapper || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
            var t = local.Transform;
            var prefabs = swapper.VesselPrefabContainer;
            for (int seat = 0; seat < SavedSquad.SlotCount; seat++)
            {
                if (seat == mySeat) continue;
                var slot = _squad.GetSlot(seat);
                if (!VesselRequirement.IsConcrete(slot.Vessel)) continue;
                // A hull with no registered prefab (Falcon, Termite, Shrike) cannot be released: the seat sails empty,
                // said once here rather than as a companion-spawn error with no seat named.
                if (prefabs && !prefabs.TryGetShipPrefab(slot.Vessel, out _, reportMissing: false))
                {
                    CSDebug.LogWarning($"[ArkEscort] Seat {seat + 1} names {slot.Vessel}, which has no vessel prefab - it sails empty.");
                    continue;
                }
                int seated = seat;
                var pose = new Pose(t.position + t.right * (WingmateAbeam * (seat < mySeat ? -1f : 1f)) - t.forward * 60f,
                    t.rotation);
                swapper.RequestSpawnAiCompanion(slot.Vessel, domain, pose, vessel =>
                {
                    if (!_active) { swapper.DespawnAiCompanion(vessel); return; }
                    _wingmates.Add(vessel);
                    SquadDeployment.Seat(vessel.VesselStatus, seated);
                    Escort(vessel, seated);
                });
            }
        }

        /// <summary>Hand <paramref name="vessel"/>'s AI the escort's objective.</summary>
        void Escort(IVessel vessel, int seat)
        {
            if (vessel?.VesselStatus is not Component c || !c) return;
            var pilot = c.GetComponent<ArkEscortPilot>();
            if (!pilot) pilot = c.gameObject.AddComponent<ArkEscortPilot>();
            pilot.Arm(this, seat);
            if (!_pilots.Contains(pilot)) _pilots.Add(pilot);
        }

        // ── The sea ──────────────────────────────────────────────────────────

        void OnUnderway()
        {
            Banner(TemplateText.Fill(_escort.DepartLine, "legs", _escort.Legs.Count.ToString(),
                "difficulty", _difficulty.ToString()));
            BringWingmates(force: true);
            _report.HullTotal = Ark ? Ark.TotalCount : 0;
            _report.UnderwayAt = Time.time;
            if (Ark)
            {
                Ark.HullPrismLost -= OnHullPrismLost;
                Ark.HullPrismLost += OnHullPrismLost;
            }
            // The first world was entered behind the veil, before the hull was laid: its entry is the moment it opens.
            if (_leg >= 0)
            {
                var r = _report.LegReports[_leg];
                r.HullAtEntry = r.HullMin = Ark ? Ark.AliveCount : 0;
                r.EnteredAt = Time.time;
            }
        }

        void OnRouteEnded()
        {
            if (!_active || _fallen) return;
            // The Ark holds at the last world's core: it made it through.
            _survived = true;
            CloseLeg(_leg);
            Banner(TemplateText.Fill(_escort.SurvivedLine, "difficulty", _difficulty.ToString(),
                "hull", Ark ? Mathf.RoundToInt(Ark.HealthFraction * 100f).ToString() : "0"));
            _endAt = Time.time + EndAfterSurvivedSeconds;
        }

        void OnArkFallen()
        {
            if (!_active) return;
            _fallen = true;
            CloseLeg(_leg);
            int leg = Mathf.Max(0, _leg);
            _report.FallenLeg = leg;
            Banner(TemplateText.Fill(_escort.FallenLine, "name", _escort.Legs[leg].Name,
                "leg", (leg + 1).ToString(), "legs", _escort.Legs.Count.ToString()));
        }

        void OnSeaEnded()
        {
            if (!_active) return;
            Finish(_survived ? ArkEscortReport.Outcome.Survived
                : _fallen ? ArkEscortReport.Outcome.Fallen : ArkEscortReport.Outcome.Abandoned);
        }

        void Update()
        {
            if (!_active || !_sea) return;
            SeedSeats();
            if (!_sea.IsRunning) return;

            if (_endAt > 0f && Time.time >= _endAt)
            {
                _endAt = -1f;
                _sea.End(returnToCell: true, "the Ark made it through");
                return;
            }

            TrackLeg();
            _context_?.Tick();
            CheckBreach();
            if (!_active || !_sea.IsRunning) return;
            if (Time.time >= _nextSample)
            {
                _nextSample = Time.time + SampleSeconds;
                Sample();
            }
            if (Time.unscaledTime >= _nextLeash)
            {
                _nextLeash = Time.unscaledTime + LeashCheckSeconds;
                BringWingmates(force: false);
            }
        }

        /// <summary>The leg is the world whose core the Ark is bound for.</summary>
        void TrackLeg()
        {
            var cell = _corridor ? _corridor.CurrentCell : null;
            if (!cell || !_legOf.TryGetValue(cell, out int leg) || leg == _leg) return;
            CloseLeg(_leg);
            _leg = leg;
            // The seats' pilots follow the Ark into the world: their crystal lists point at it from here.
            foreach (var p in _pilots)
                if (p) p.OnLegChanged(cell);
            var r = _report.LegReports[leg];
            r.EnteredAt = Time.time;
            r.HullAtEntry = Ark ? Ark.AliveCount : 0;
            r.HullMin = r.HullAtEntry;
            var authored = _escort.Legs[leg];
            Banner(TemplateText.Fill(authored.Arrival, "name", authored.Name, "leg", (leg + 1).ToString(),
                "legs", _escort.Legs.Count.ToString()));
        }

        /// <summary>Below the tier's breach line the Ark is overrun: the escort is lost, as if it had fallen.</summary>
        void CheckBreach()
        {
            var ark = Ark;
            if (!ark || _fallen || _survived || _leg < 0 || ark.TotalCount == 0) return;
            float line = _escort.TierFor(_difficulty)?.BreachHull ?? 0f;
            if (line <= 0f || ark.HealthFraction >= line) return;
            _fallen = true;
            _report.Overrun = true;
            _report.HullMinFraction = Mathf.Min(_report.HullMinFraction, ark.HealthFraction);
            CloseLeg(_leg);
            _report.FallenLeg = _leg;
            var authored = _escort.Legs[_leg];
            Banner(TemplateText.Fill(_escort.OverrunLine, "name", authored.Name, "leg", (_leg + 1).ToString(),
                "legs", _escort.Legs.Count.ToString(), "hull", Mathf.RoundToInt(ark.HealthFraction * 100f).ToString()));
            _sea.End(returnToCell: true, "the Ark was overrun");
        }

        void CloseLeg(int leg)
        {
            if (leg < 0 || leg >= _report.LegReports.Count) return;
            var r = _report.LegReports[leg];
            if (r.ExitedAt > 0f) return;
            r.ExitedAt = Time.time;
            r.HullAtExit = Ark ? Ark.AliveCount : 0;
            r.Reached = true;
        }

        void Sample()
        {
            var ark = Ark;
            if (!ark || _leg < 0) return;
            var r = _report.LegReports[_leg];
            r.HullMin = Mathf.Min(r.HullMin, ark.AliveCount);
            int near = CountThreats(ark.Position, ArkEscortPilot.GuardRadius, null);
            r.PeakThreats = Mathf.Max(r.PeakThreats, near);
            r.ThreatSeconds += near > 0 ? SampleSeconds : 0f;
            // How many of the squad are actually with the Ark (the escort AI's leash and hunting, measured).
            int with = 0;
            float g2 = ArkEscortPilot.GuardRadius * ArkEscortPilot.GuardRadius;
            var local = LocalVessel();
            if (local?.Transform && (local.Transform.position - ark.Position).sqrMagnitude <= g2) with++;
            foreach (var w in _wingmates)
            {
                var s = Alive(w);
                if (s?.Transform && (s.Transform.position - ark.Position).sqrMagnitude <= g2) with++;
            }
            r.EscortSamples++;
            r.EscortNear += with;
            _report.Wingmates = Mathf.Max(_report.Wingmates, _wingmates.Count);
            _report.Pilots = Mathf.Max(_report.Pilots, _pilots.Count);
            _report.HullMinFraction = Mathf.Min(_report.HullMinFraction, ark.HealthFraction);
            _report.Samples.Add(new ArkEscortReport.Sample
            {
                T = Time.time - _report.StartedAt,
                Leg = _leg,
                Hull = ark.AliveCount,
                Threats = near,
            });
        }

        /// <summary>Live hostile creatures (another domain than the Ark's) within <paramref name="radius"/> of
        /// <paramref name="at"/>, nearest first into <paramref name="into"/> when given.</summary>
        public int CountThreats(Vector3 at, float radius, List<Fauna> into)
        {
            var ark = Ark;
            if (!ark) return 0;
            into?.Clear();
            int n = 0;
            float r2 = radius * radius;
            for (int c = _cells.Count - 1; c >= 0; c--)
            {
                var cell = _cells[c];
                if (!cell) { _cells.RemoveAt(c); continue; }
                var fauna = cell.LiveFauna;
                for (int i = 0; i < fauna.Count; i++)
                {
                    var f = fauna[i];
                    if (!f || f.Domain == ark.Domain || !f.IsAlivePrey) continue;
                    if ((f.transform.position - at).sqrMagnitude > r2) continue;
                    n++;
                    into?.Add(f);
                }
            }
            if (into is { Count: > 1 })
                into.Sort((a, b) => (a.transform.position - at).sqrMagnitude.CompareTo((b.transform.position - at).sqrMagnitude));
            return n;
        }

        void SeedSeats()
        {
            var local = LocalVessel();
            if (local != null && _seeded.Add(local))
            {
                SquadDeployment.SeedElement(local, _def.SeedElementLevel);
                if (_aiLocal && local.Vessel != null)
                {
                    local.Vessel.ToggleAIPilot(true);
                    Escort(local.Vessel, Mathf.Clamp(_squad.MySeat, 0, SavedSquad.SlotCount - 1));
                }
            }
            for (int i = _wingmates.Count - 1; i >= 0; i--)
            {
                var status = Alive(_wingmates[i]);
                if (status == null) { _wingmates.RemoveAt(i); continue; }
                if (_seeded.Add(status)) SquadDeployment.SeedElement(status, _def.SeedElementLevel);
            }
        }

        /// <summary>An AI wingmate that has ranged past the leash is brought back to the Ark's flank.</summary>
        void BringWingmates(bool force)
        {
            var ark = Ark;
            if (!ark) return;
            // An escort's leash is its guard: a wingmate that strays past it is no use to the Ark. A seat's role may
            // let its own leash out (the Squirrel's nucleus run), by ArkEscortPilot.Leash.
            float leash = Mathf.Min(_def.WingmateLeash, ArkEscortPilot.GuardRadius * EscortGeneRegistry.LeashFactor);
            int n = _wingmates.Count + (_aiLocal ? 1 : 0);
            for (int i = 0; i < n; i++)
            {
                var vessel = i < _wingmates.Count ? _wingmates[i] : _context?.GameData?.LocalPlayer?.Vessel;
                var status = Alive(vessel);
                if (status == null || !status.Transform) continue;
                float seatLeash = leash;
                if (status is Component sc && sc && sc.TryGetComponent(out ArkEscortPilot escortPilot))
                    seatLeash = escortPilot.Leash(leash);
                if (!force && (status.Transform.position - ark.Position).sqrMagnitude <= seatLeash * seatLeash) continue;
                var side = i % 2 == 0 ? -1f : 1f;
                var at = ark.Position + ark.transform.right * (side * WingmateAbeam * 2f) - ark.Forward * (WingmateAbeam * (1 + i));
                vessel.SetPose(new Pose(at, Quaternion.LookRotation(ark.Forward, Vector3.up)));
            }
        }

        // ── The end ──────────────────────────────────────────────────────────

        /// <summary>End the escort now (the player left freestyle, or gave it up).</summary>
        public void GiveUp(string reason)
        {
            if (!_active) return;
            CSDebug.LogVerbose(CSLogChannel.ToyBox, $"[ArkEscort] Ended: {reason}.");
            if (_sea && _sea.IsRunning)
            {
                _sea.End(returnToCell: true, reason); // OnSeaEnded finishes
                return;
            }
            Finish(ArkEscortReport.Outcome.Abandoned);
        }

        void Finish(ArkEscortReport.Outcome outcome)
        {
            _active = false;
            _report.Result = outcome;
            _report.EndedAt = Time.time;
            _report.ReachedLegs = 0;
            foreach (var r in _report.LegReports)
                if (r.Reached) _report.ReachedLegs++;
            WriteFriendlyFireBy();

            if (_corridor) _corridor.PrepareCell = null;
            foreach (var feed in _killFeeds)
                if (feed) feed.OnFaunaKilled.OnRaised -= OnFaunaKilled;
            _killFeeds.Clear();
            if (Ark) Ark.HullPrismLost -= OnHullPrismLost;
            foreach (var p in _pilots)
                if (p) p.Disarm();
            _pilots.Clear();
            _context_?.Uninstall();
            _context_ = null;
            SquadDeployment.Recall();
            var swapper = _context?.VesselInitializer;
            foreach (var w in _wingmates)
                if (swapper && Alive(w) != null) swapper.DespawnAiCompanion(w);
            _wingmates.Clear();
            _seeded.Clear();
            _cells.Clear();
            _legOf.Clear();
            if (_aiLocal) LocalVessel()?.Vessel?.ToggleAIPilot(false);

            CSDebug.LogVerbose(CSLogChannel.ToyBox, $"[ArkEscort] {_report.Summary()}");
            Finished?.Invoke(_report);
            _onEnded?.Invoke();
        }

        // ── Plumbing ─────────────────────────────────────────────────────────

        void EnsureSea()
        {
            if (_sea) return;
            var corridor = new GameObject("ArkEscortCellConveyor");
            corridor.transform.SetParent(_root, false);
            _corridor = corridor.AddComponent<CellConveyor>();
            var sea = new GameObject("ArkEscortArkwayRun");
            sea.transform.SetParent(_root, false);
            _sea = sea.AddComponent<ArkwayRun>();
            _sea.Underway += OnUnderway;
            _sea.RouteEnded += OnRouteEnded;
            _sea.ArkFallen += OnArkFallen;
        }

        static string SquadName(SavedSquad squad)
        {
            var names = new string[SavedSquad.SlotCount];
            for (int i = 0; i < names.Length; i++) names[i] = squad.GetSlot(i).Vessel.ToString();
            return string.Join("+", names);
        }

        IVesselStatus LocalVessel() => Alive(_context?.GameData?.LocalPlayer?.Vessel);

        static IVesselStatus Alive(IVessel vessel)
        {
            if (vessel == null) return null;
            if (vessel is Object uo && !uo) return null;
            return vessel.VesselStatus;
        }

        void Banner(string line) => _sea?.ShowBanner(line, BannerSeconds);

        void OnDestroy()
        {
            if (_sea)
            {
                _sea.Underway -= OnUnderway;
                _sea.RouteEnded -= OnRouteEnded;
                _sea.ArkFallen -= OnArkFallen;
            }
            if (_active) SquadDeployment.Recall();
            if (_reactor) Destroy(_reactor);
            _onEnded = null;
        }
    }
}
