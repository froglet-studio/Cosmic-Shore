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
    /// One voyage, played (Docs/Voyages/SLICE.md §1): the squad deployed, the leg the Ark sails to the job the player
    /// picked in the Voyage window, what stands at the stop, and how the voyage ends. The rules are <see cref="VoyageRules"/> (pure, tested headless);
    /// the sea is the Arkway's (<see cref="ArkwayRun"/> with a route); this class joins the two and owns nothing else.
    ///
    /// <list type="number">
    /// <item><b>Embark.</b> The active squad deploys (<see cref="SquadDeployment"/>): the pilot flies their seat's vessel,
    /// the host releases an AI wingmate for every other seat, and every seat starts levelled in its slot's element.</item>
    /// <item><b>The job.</b> Chosen in the app shell, never in the world: the Voyage window draws one card per job on
    /// offer (<see cref="VoyageRules.Board"/>, one from each seated pilot, and the way home) over the galaxy map, and its
    /// Set sail is <see cref="SetSail"/> (Garrett, 2026-10-28: "the jobs should be the different voyages").</item>
    /// <item><b>The leg.</b> The Ark sails the lane's cells and holds at the destination's own world.</item>
    /// <item><b>The stop.</b> A vault field stands its vaults, a wreck spills what it holds, a core must be held, a port
    /// takes the dispatch. What the squad stows is the job's work; the outcome writes the news the next jobs are made
    /// of, and the banner sends the player back to the Voyage window to choose one.</item>
    /// <item><b>The end.</b> Leaving freestyle at the stop anchors the squad there, finds still aboard and at stake
    /// (<see cref="VoyageRules.Anchor"/>); the next voyage sets out from it. Docking at home extracts: every find is the
    /// player's, every crystal owed is paid. A fallen Ark loses everything aboard (to the raiders, or into a wreck that
    /// is news at the nearest port). Leaving freestyle mid-leg gives the voyage up and loses what was found.</item>
    /// </list>
    ///
    /// <para>Friends: each machine runs its own voyage today; the host's course reaching the party is SLICE.md §4
    /// step 5.</para>
    /// </summary>
    public sealed class VoyageRun : MonoBehaviour
    {
        const float BannerSeconds = 5f;
        const float LeashCheckSeconds = 2f;
        const float WingmateAbeam = 140f;

        VoyageToyDefinitionSO _def;
        VoyageContentSO _content;
        ToyContext _context;
        Transform _root;
        System.Action _onEnded;

        ArkwayRun _sea;
        CellConveyor _corridor;
        ArkwayConfig _arkCfg;
        int _worlds;

        VoyageState _state;
        SavedSquad _squad;
        readonly List<VoyageVault> _vaults = new();
        readonly List<IVessel> _wingmates = new();
        readonly HashSet<IVesselStatus> _seeded = new();
        readonly List<string> _holdMirror = new();

        VoyageRules.Arrival _arrival;
        int _holdKnown;
        int _stowedHere;
        float _heldFor;
        bool _held;
        int _holdLineSecond = -1;
        bool _active, _underway, _fallen, _fallTaken, _docked;
        bool _following;
        VoyageCourse _pendingLeg;
        float _nextLeashCheck;

        /// <summary>True from embarking until the voyage ends (docked, fallen or given up).</summary>
        public bool IsActive => _active;

        /// <summary>The job this voyage sails, or null before one is taken.</summary>
        public VoyageJob CurrentJob => _state?.Current;

        public void Configure(VoyageToyDefinitionSO definition, ToyContext context, Transform toyboxRoot,
            System.Action onEnded)
        {
            _def = definition;
            _context = context;
            _root = toyboxRoot;
            _onEnded = onEnded;
            _content = definition ? definition.Content : VoyageContentSO.Load();
        }

        VoyageContent Content => _content ? _content.Content : null;
        VoyageLines Lines => _content ? _content.Lines : null;

        // ── Embark ───────────────────────────────────────────────────────────

        /// <summary>Deploy the squad and sail for <paramref name="job"/> (picked in the Voyage window from
        /// <see cref="VoyageHarbour.Board"/>).</summary>
        public void SetSail(IVesselStatus local, VoyageJob job)
        {
            if (job == null || _active && _following) return;
            if (!_active) Embark(local, following: false);
            if (_active) TakeJob(job);
        }

        void Embark(IVesselStatus local, bool following)
        {
            if (_active || local?.Transform == null) return;
            if (!_def || Content == null)
            {
                CSDebug.LogWarning("[Voyage] No voyage content (Resources/Voyages/VoyageContent) - the voyage cannot set out.");
                return;
            }

            var domain = LiveDomain(_context);
            _squad = ActiveSquad(_context);
            _state = LoadOrBegin(Content, domain);
            _following = following;
            _pendingLeg = null;
            SaveState();

            _active = true;
            _underway = _fallen = _fallTaken = _docked = false;
            _seeded.Clear();
            EnsureSea();
            _arkCfg = _def.BuildArkConfig(_content);
            _sea.Configure(_arkCfg, _context, _context?.Container, _corridor, OnSeaEnded);
            _worlds = CountWorlds(local.Transform.position);

            Deploy(local, domain);

            ArkFall.Settled -= OnFallSettled;
            ArkFall.Settled += OnFallSettled;

            if (_following)
            {
                Banner(Lines.Following);
                return;
            }
            VoyageCourseLink.Send(new VoyageCourse { Kind = VoyageCourseKind.Embark });
        }

        // ── Friends: following the host's course (SLICE.md §4 step 5) ────────

        /// <summary>
        /// The host's course reached this machine. A friend deploys their own seat and sails each leg the host takes,
        /// with their own Ark, fauna and finds; the board and the news are the host's. A friend already on a voyage of
        /// their own keeps it.
        /// </summary>
        public void Follow(VoyageCourse course)
        {
            if (course == null || _active && !_following) return;
            switch (course.Kind)
            {
                case VoyageCourseKind.Embark:
                    if (!_active) Embark(LocalVessel(), following: true);
                    break;
                case VoyageCourseKind.Leg:
                    if (!_active) Embark(LocalVessel(), following: true);
                    if (!_active) return;
                    // Still sailing the last leg: take this one on arriving, so the stop is seen first.
                    if (_sea.IsRunning && !_sea.IsHoldingAtRouteEnd) _pendingLeg = course;
                    else SailLeg(course);
                    break;
                case VoyageCourseKind.End:
                    // A friend already sailing home docks on their own; anywhere else the voyage ends with the host's.
                    if (_active && _state.Current is not { IsReturn: true }) GiveUp("the host's voyage ended");
                    break;
            }
        }

        void SailLeg(VoyageCourse course)
        {
            var local = LocalVessel();
            if (local == null || course.State == null || course.Job == null || course.Route is not { Count: > 0 }) return;
            if (_state.Current != null && !_state.AtSea)
                Resolve(VoyageRules.WorkDone(_state.Current, _stowedHere, _held) ? JobOutcome.Succeeded : JobOutcome.Failed);

            // The galaxy is the host's; what this machine found and is owed stays its own.
            var aboard = _state.Aboard;
            var owed = _state.Owed;
            _state = course.State;
            _state.Normalize();
            _state.Aboard = aboard ?? new List<string>();
            _state.Owed = owed ?? new List<int>();

            ClearStop();
            if (_sea.IsRunning) _sea.ContinueRoute(course.Route);
            else
            {
                _sea.SetRoute(course.Route);
                _sea.Begin(local);
            }
            Banner(Line(Lines.Depart, course.Job.To));
        }

        void SaveState()
        {
            // A friend's galaxy is their own: the host's, followed, is never written over it.
            if (!_following) VoyageStore.Save(_state);
        }

        /// <summary>The squad that sails: the one a party launch named, else the player's active squad, else the hull
        /// they are flying in every seat (a player who has never built a squad can still voyage).</summary>
        public static SavedSquad ActiveSquad(ToyContext context)
        {
            var squad = SquadLaunchIntent.Squad ?? UGSDataService.Instance?.SquadRepo?.Data?.Book?.GetActive();
            if (squad != null) return squad;
            var hull = Alive(context?.GameData?.LocalPlayer?.Vessel)?.VesselType ?? default;
            return new SavedSquad(string.Empty, new SquadSlot(hull, Element.None), new SquadSlot(hull, Element.None),
                new SquadSlot(hull, Element.None));
        }

        /// <summary>The pilots seated in <paramref name="squad"/>, in seat order: they decide which jobs are offered.</summary>
        public static List<string> SeatedPilots(SavedSquad squad)
        {
            var pilots = new List<string>();
            if (squad == null) return pilots;
            var catalog = PilotArtifactCatalogSO.Load();
            for (int seat = 0; seat < SavedSquad.SlotCount; seat++)
            {
                var pilot = catalog ? catalog.ResolvePilot(squad.GetSlot(seat), null) : null;
                if (pilot) pilots.Add(pilot.Id);
            }
            return pilots;
        }

        /// <summary>The saved galaxy, ready to set out from where the last voyage left the Ark; a new galaxy before the
        /// first voyage (not saved until one sets out).</summary>
        public static VoyageState LoadOrBegin(VoyageContent content, Domains home)
        {
            var state = VoyageStore.Load();
            if (state == null) return VoyageRules.NewGalaxy(content, home);
            VoyageRules.Resume(state, content, home);
            return state;
        }

        void Deploy(IVesselStatus local, Domains domain)
        {
            int mySeat = Mathf.Clamp(_squad.MySeat, 0, SavedSquad.SlotCount - 1);
            SquadDeployment.Deploy(_squad, mySeat);


            // The pilot flies their seat's vessel (the swap lands a moment later; the new hull reads its slot then).
            var mine = _squad.GetSlot(mySeat);
            var swapper = _context?.VesselInitializer;
            if (swapper && VesselRequirement.IsConcrete(mine.Vessel) && mine.Vessel != local.VesselType)
                swapper.RequestSwap(mine.Vessel);

            // Every other seat is an AI wingmate, released by the host (a party client's voyage waits for step 5).
            if (!swapper || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer) return;
            var t = local.Transform;
            var prefabs = swapper.VesselPrefabContainer;
            for (int seat = 0; seat < SavedSquad.SlotCount; seat++)
            {
                if (seat == mySeat) continue;
                var slot = _squad.GetSlot(seat);
                if (!VesselRequirement.IsConcrete(slot.Vessel)) continue;
                // A hull with no registered prefab (Falcon, Termite, Shrike) cannot be released: the seat sails empty.
                if (prefabs && !prefabs.TryGetShipPrefab(slot.Vessel, out _, reportMissing: false))
                {
                    CSDebug.LogWarning($"[Voyage] Seat {seat + 1} names {slot.Vessel}, which has no vessel prefab - it sails empty.");
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
                });
            }
        }

        // ── Taking a job: the leg ────────────────────────────────────────────

        void TakeJob(VoyageJob job)
        {
            if (!_active || job == null) return;
            var local = LocalVessel();
            if (local == null) return;

            // A job still open here ends now: done if its work is done, otherwise left undone.
            if (_state.Current != null && !_state.AtSea)
                Resolve(VoyageRules.WorkDone(_state.Current, _stowedHere, _held) ? JobOutcome.Succeeded : JobOutcome.Failed);

            if (!VoyageRules.Take(_state, Content, job)) return;
            var route = VoyageRules.Corridor(Content, job.From, job.To, _state.Day, _worlds);
            if (route.Count == 0)
            {
                CSDebug.LogWarning($"[Voyage] No course from {job.From} to {job.To} ({_worlds} worlds to sail) - the job stays on the board.");
                _state.Current = null;
                _state.AtSea = false;
                return;
            }

            ClearStop();
            SaveState();
            VoyageCourseLink.Send(new VoyageCourse { Kind = VoyageCourseKind.Leg, State = _state, Job = job, Route = route });

            if (_sea.IsRunning)
            {
                if (!_sea.ContinueRoute(route))
                    CSDebug.LogWarning("[Voyage] The Ark was not holding at a stop - the leg could not be set.");
            }
            else
            {
                _sea.SetRoute(route);
                _sea.Begin(local);
            }
            Banner(Line(Lines.Depart, job.To));
            VoyageHarbour.RaiseChanged();
        }

        // ── The sea's events ─────────────────────────────────────────────────

        void OnUnderway()
        {
            var ark = _sea.Ark;
            if (!ark) return;

            // Each seat's artifact rides in the Ark: it is what that vessel's payloads become, and it is at stake.
            var hold = ark.Hold;
            if (hold)
                for (int seat = 0; seat < SavedSquad.SlotCount; seat++)
                {
                    var slot = _squad.GetSlot(seat);
                    if (slot.CarriesArtifact()) hold.Stow(slot.ArtifactId);
                }
            _holdKnown = hold ? hold.Artifacts.Count : 0;
            _underway = true;
            BringWingmates(force: true);
        }

        void OnRouteEnded()
        {
            if (!_active) return;
            var job = _state.Current;
            _arrival = VoyageRules.Arrive(_state, Content);
            if (_arrival == null || job == null) return;
            var ark = _sea.Ark;
            Banner(Line(Lines.Arrived, _state.At));

            if (_arrival.Extracts)
            {
                if (!job.IsReturn) Resolve(VoyageRules.WorkDone(job, 0, false) ? JobOutcome.Succeeded : JobOutcome.Failed);
                else VoyageRules.Resolve(_state, Content, JobOutcome.Succeeded);
                Dock();
                return;
            }

            StandStop(ark ? ark.Position : transform.position);
            if (VoyageRules.DoneOnArrival(job)) Resolve(JobOutcome.Succeeded);
            SaveState();
            VoyageHarbour.RaiseChanged();
            if (!_following) return;
            Banner(Lines.Following);
            if (_pendingLeg == null) return;
            var leg = _pendingLeg;
            _pendingLeg = null;
            SailLeg(leg);
        }

        void OnArkFallen()
        {
            if (!_active) return;
            _fallen = true;
            string where = _state.AtSea && _state.Current != null ? _state.Current.To : _state.At;
            var r = VoyageRules.ArkLost(_state, Content, where, _fallTaken, _holdMirror);
            SaveState();
            Banner(string.IsNullOrEmpty(r.StoryLine) ? Lines.ArkLost : Lines.ArkLost + "\n" + r.StoryLine);
        }

        void OnFallSettled(ArkHold hold, ArkFall.Outcome outcome)
        {
            if (!_active || !_sea.Ark || hold != _sea.Ark.Hold) return;
            _fallTaken = outcome.Verdict == ArkFall.Verdict.Transfer;
        }

        /// <summary>The Arkway run ended: docked, fallen, or given up.</summary>
        void OnSeaEnded()
        {
            if (!_active) return;
            if (!_fallen && !_docked) EndAtStop();
            Finish();
        }

        // ── A stop ───────────────────────────────────────────────────────────

        void StandStop(Vector3 ark)
        {
            ClearStop();
            _stowedHere = 0;
            _heldFor = 0f;
            _held = false;
            _holdLineSecond = -1;
            var prefab = _arkCfg?.PrismPrefab;
            int n = _arrival.VaultFinds.Count;
            for (int i = 0; i < n; i++)
            {
                var at = ark + Around(i, n) * _def.FindDistance;
                _vaults.Add(VoyageVault.Create(_root, at, _arrival.VaultFinds[i], prefab, _def.VaultRadius,
                    _def.VaultPrisms, _def.VaultPrismScale, _def.VaultOpenShare));
            }
            if (n > 0) Banner(Lines.Vaults);

            int w = _arrival.WreckFinds.Count;
            if (w > 0)
            {
                var wreck = ark - Around(0, 1) * _def.FindDistance;
                var cfg = ArkHold.Config;
                for (int i = 0; i < w; i++)
                    ArtifactPickup.Spill(_arrival.WreckFinds[i], wreck + ArkFall.SpillOffset(i, w, cfg.SpillRadius));
                Banner(Lines.Wreck);
            }
            if (_arrival.HoldSeconds > 0f) Banner(Line(Lines.Hold, _state.At));
        }

        /// <summary>Spot <paramref name="i"/> of <paramref name="n"/> on a level ring round the Ark.</summary>
        static Vector3 Around(int i, int n) =>
            Quaternion.AngleAxis(360f * i / Mathf.Max(1, n) + 45f, Vector3.up) * Vector3.forward;

        /// <summary>The Ark sails on: the vaults go back to the pool, and what nobody picked up stays behind (a wreck's
        /// unclaimed artifacts stay in the wreck's record).</summary>
        void ClearStop()
        {
            foreach (var v in _vaults)
                if (v) v.Retire();
            _vaults.Clear();
            var loose = ArtifactPickup.Loose;
            for (int i = loose.Count - 1; i >= 0; i--)
                if (loose[i] && !loose[i].Holder) loose[i].Strike();
            _arrival = null;
        }

        void Update()
        {
            if (!_active) return;
            if (!_sea.IsRunning)
            {
                // Not sailing yet (the leg is being laid): leaving freestyle ends the voyage.
                if (_context?.IsFreestyleActive != null && !_context.IsFreestyleActive()) GiveUp("left freestyle");
                SeedSeats();
                return;
            }
            SeedSeats();
            WatchHold();
            TickCore();
            if (Time.unscaledTime >= _nextLeashCheck)
            {
                _nextLeashCheck = Time.unscaledTime + LeashCheckSeconds;
                BringWingmates(force: false);
            }
        }

        /// <summary>Every seat starts levelled in its slot's element, once per hull (a swapped hull is a new one).</summary>
        void SeedSeats()
        {
            var local = LocalVessel();
            if (local != null && _seeded.Add(local)) SquadDeployment.SeedElement(local, _def.SeedElementLevel);
            for (int i = _wingmates.Count - 1; i >= 0; i--)
            {
                var status = Alive(_wingmates[i]);
                if (status == null) { _wingmates.RemoveAt(i); continue; }
                if (_seeded.Add(status)) SquadDeployment.SeedElement(status, _def.SeedElementLevel);
            }
        }

        /// <summary>What came aboard: a find stowed here is the job's work (and the player's, once home).</summary>
        void WatchHold()
        {
            var hold = _sea.Ark ? _sea.Ark.Hold : null;
            if (!hold || _fallen || !_underway) return;
            var aboard = hold.Artifacts;
            if (aboard.Count != _holdMirror.Count)
            {
                _holdMirror.Clear();
                _holdMirror.AddRange(aboard);
            }
            if (aboard.Count <= _holdKnown)
            {
                _holdKnown = aboard.Count;
                return;
            }
            for (int i = _holdKnown; i < aboard.Count; i++)
            {
                VoyageRules.Stowed(_state, aboard[i]);
                if (!_state.AtSea) _stowedHere++;
            }
            _holdKnown = aboard.Count;
            Banner(Lines.Stowed);
            CheckWork();
            SaveState();
        }

        /// <summary>A core is held by staying with the Ark there for its hold time.</summary>
        void TickCore()
        {
            if (_arrival == null || _arrival.HoldSeconds <= 0f || _held || _state.Current == null) return;
            var ark = _sea.Ark;
            var t = LocalVessel()?.Transform;
            if (!ark || !t) return;
            if ((t.position - ark.Position).sqrMagnitude > _def.HoldRadius * _def.HoldRadius) return;
            _heldFor += Time.deltaTime;
            int left = Mathf.CeilToInt(Mathf.Max(0f, _arrival.HoldSeconds - _heldFor));
            if (left != _holdLineSecond)
            {
                _holdLineSecond = left;
                _sea.ShowBanner(TemplateText.Fill(Line(Lines.HoldProgress, _state.At), "seconds", left.ToString()), 1.5f);
            }
            if (_heldFor < _arrival.HoldSeconds) return;
            _held = true;
            CheckWork();
            SaveState();
        }

        void CheckWork()
        {
            var job = _state.Current;
            if (job == null || _state.AtSea) return;
            if (VoyageRules.WorkDone(job, _stowedHere, _held)) Resolve(JobOutcome.Succeeded);
        }

        void Resolve(JobOutcome outcome)
        {
            var r = VoyageRules.Resolve(_state, Content, outcome);
            string head = outcome == JobOutcome.Succeeded ? Lines.Done : Lines.Failed;
            string line = string.IsNullOrEmpty(r.StoryLine) ? head : head + "\n" + r.StoryLine;
            // The next job is chosen in the Voyage window, where the news this outcome wrote has made it.
            if (!_following && !string.IsNullOrEmpty(Lines.ChooseNext)) line += "\n" + Lines.ChooseNext;
            Banner(line);
            SaveState();
            VoyageHarbour.RaiseChanged();
        }

        /// <summary>
        /// The voyage ends short of home: at the stop the squad anchors there (a job still open is done if its work is,
        /// left undone if not), and mid-leg the voyage is given up and what was found is lost.
        /// </summary>
        void EndAtStop()
        {
            bool atSea = _state.AtSea;
            VoyageRules.Anchor(_state, Content, VoyageRules.WorkDone(_state.Current, _stowedHere, _held));
            SaveState();
            if (atSea) Banner(Lines.Failed);
        }

        /// <summary>Home: everything aboard and every crystal owed is the player's now, and the voyage ends at the dock.</summary>
        void Dock()
        {
            var x = VoyageRules.Extract(_state);
            if (x != null)
            {
                foreach (var id in x.Artifacts) PilotLoadoutStore.Grant(id);
                for (int e = 0; e < x.Crystals.Length; e++)
                    if (x.Crystals[e] > 0) PilotLoadoutStore.AddCrystals((Element)e, x.Crystals[e], "voyage");
                Banner(TemplateText.Fill(Line(Lines.Extracted, _state.At), "count", x.Artifacts.Count.ToString()));
            }
            SaveState();
            _docked = true;
            _sea.End(returnToCell: true, "the voyage docked at home");
        }

        /// <summary>End the voyage here (freestyle left, the host's voyage ended): anchored at a stop, given up at sea.</summary>
        public void GiveUp(string reason)
        {
            if (!_active) return;
            CSDebug.LogVerbose(CSLogChannel.ToyBox, $"[Voyage] Ended: {reason}.");
            if (_sea && _sea.IsRunning)
            {
                _sea.End(returnToCell: true, reason); // OnSeaEnded anchors or gives up, and finishes
                return;
            }
            EndAtStop();
            Finish();
        }

        void Finish()
        {
            if (!_following && !_docked) VoyageCourseLink.Send(new VoyageCourse { Kind = VoyageCourseKind.End });
            _active = false;
            _following = false;
            _pendingLeg = null;
            _underway = false;
            ArkFall.Settled -= OnFallSettled;
            ClearStop();
            SquadDeployment.Recall();
            var swapper = _context?.VesselInitializer;
            foreach (var w in _wingmates)
                if (swapper && Alive(w) != null) swapper.DespawnAiCompanion(w);
            _wingmates.Clear();
            _seeded.Clear();
            _holdMirror.Clear();
            _onEnded?.Invoke();
        }

        // ── Wingmates ────────────────────────────────────────────────────────

        /// <summary>AI wingmates escort the Ark: one that has ranged past the leash is brought back to its flank.</summary>
        void BringWingmates(bool force)
        {
            var ark = _sea ? _sea.Ark : null;
            if (!ark) return;
            float leashSqr = _def.WingmateLeash * _def.WingmateLeash;
            for (int i = 0; i < _wingmates.Count; i++)
            {
                var w = _wingmates[i];
                var status = Alive(w);
                if (status == null || !status.Transform) continue;
                if (!force && (status.Transform.position - ark.Position).sqrMagnitude <= leashSqr) continue;
                var side = i % 2 == 0 ? -1f : 1f;
                var at = ark.Position + ark.transform.right * (side * WingmateAbeam * 2f) - ark.Forward * (WingmateAbeam * (1 + i));
                w.SetPose(new Pose(at, Quaternion.LookRotation(ark.Forward, Vector3.up)));
            }
        }

        // ── Plumbing ─────────────────────────────────────────────────────────

        void EnsureSea()
        {
            if (_sea) return;
            var corridor = new GameObject("VoyageCellConveyor");
            corridor.transform.SetParent(_root, false);
            _corridor = corridor.AddComponent<CellConveyor>();
            var sea = new GameObject("VoyageArkwayRun");
            sea.transform.SetParent(_root, false);
            _sea = sea.AddComponent<ArkwayRun>();
            _sea.Underway += OnUnderway;
            _sea.RouteEnded += OnRouteEnded;
            _sea.ArkFallen += OnArkFallen;
        }

        /// <summary>How many worlds a route's indices count into: the corridor's own list, read from the cell it will
        /// clone (Garrett's Arkway decides which worlds those are).</summary>
        int CountWorlds(Vector3 at)
        {
            var cell = Cell.FindCellContaining(at, sceneCellsOnly: true);
            if (!cell) cell = Cell.FindNearestActiveCell(at, sceneCellsOnly: true);
            var worlds = new List<CellConfigDataSO>();
            CellConveyor.TraversalWorlds(_arkCfg?.Cells, cell, worlds);
            if (worlds.Count == 0)
                CSDebug.LogWarning("[Voyage] No worlds to sail through (no cell pool and no host cell configs) - no leg can be set.");
            return worlds.Count;
        }

        public static Domains LiveDomain(ToyContext context)
        {
            var domain = context?.GameData?.LocalPlayer?.Domain ?? Domains.Jade;
            return domain == Domains.Blue ? Domains.Jade : domain;
        }

        IVesselStatus LocalVessel() => Alive(_context?.GameData?.LocalPlayer?.Vessel);

        static IVesselStatus Alive(IVessel vessel)
        {
            if (vessel == null) return null;
            if (vessel is Object uo && !uo) return null;
            return vessel.VesselStatus;
        }

        string Line(string template, string locationId)
        {
            var at = Content.Location(locationId);
            return VoyageRules.Fill(template, at, at, at, null);
        }

        void Banner(string line) => _sea?.ShowBanner(line, BannerSeconds);

        void OnDestroy()
        {
            ArkFall.Settled -= OnFallSettled;
            if (_sea)
            {
                _sea.Underway -= OnUnderway;
                _sea.RouteEnded -= OnRouteEnded;
                _sea.ArkFallen -= OnArkFallen;
            }
            if (_active) SquadDeployment.Recall();
            _onEnded = null;
        }
    }
}
