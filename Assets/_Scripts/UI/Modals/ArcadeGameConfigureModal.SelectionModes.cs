using System.Collections.Generic;
using System.Linq;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.UI
{
    // Part of ArcadeGameConfigureModal: the Vessels row of an ARENA card (SO_ArcadeGame.ArenaRules), which replaced its
    // intensity row (Garrett, 2026-11-06). Four ways the seats get their hulls - Pick, Random, Draft, Squad
    // (VesselSelectionMode) - all built on the arena seating that was already here: the server-arbitrated human claim
    // (Player.NetArenaHullClaim), the host's AI picks (config.AIVessels) and the one-pilot-per-hull rule.
    //
    // The HOST decides everything: which mode, whose draft turn it is, which AI gets which hull. It gates every human
    // claim through Player.ServerArenaClaimGate and every AI pick through MaySetAiHullUnderSelection, and publishes the
    // mode and the draft's turn in the lobby snapshot. Each pilot's own machine asks for its own deal (Random, Squad)
    // and adopts whatever claim the server grants it, so a guest's panel always shows the server's truth.
    public partial class ArcadeGameConfigureModal
    {
        #region Vessel selection modes (arena cards)

        [Header("Vessels row (arena cards)")]
        [SerializeField, Tooltip("Seconds a draft seat has to pick before it is given a random free hull " +
                                 "(Garrett's 20 s, 2026-11-06).")]
        [Min(3f)] float draftTurnSeconds = 20f;

        [SerializeField, Tooltip("Seconds an all-AI team's draft turn waits before it picks at random, so the players " +
                                 "can follow the draft.")]
        [Min(0f)] float draftAiPickDelaySeconds = 1f;

        [SerializeField, Tooltip("Start's reason while the draft is still running. Empty: no reason shown.")]
        string draftRunningStartLine = "The draft is still picking.";

        [SerializeField, Tooltip("The Vessels line on your own draft turn. {seconds} is the time left.")]
        string draftYourTurnLine = "Your pick ({seconds})";

        [SerializeField, Tooltip("The Vessels line when you pick for an AI teammate. {domain} is its team, {seconds} the time left.")]
        string draftAllyTurnLine = "Pick for {domain} AI ({seconds})";

        [SerializeField, Tooltip("The Vessels line while someone else picks. {seat} names the seat, {seconds} the time left.")]
        string draftWaitingLine = "{seat} is picking ({seconds})";

        [SerializeField, Tooltip("How an AI seat is named in the draft line. {domain} is its team.")]
        string draftAiSeatName = "{domain} AI";

        [SerializeField, Tooltip("The Vessels line once every seat has a hull.")]
        string draftDoneLine = "Draft complete";

        [SerializeField, Tooltip("The vessel section's caption in Squad mode before the deal. {squad} is the squad's name, " +
                                 "{vessels} its three vessels. The arrows step through your saved squads.")]
        string squadChoiceCaption = "Squad: {squad} ({vessels})";

        [SerializeField, Tooltip("The Vessels line in Squad mode for a pilot with no saved squad: they are dealt at random.")]
        string noSquadLine = "No saved squad: dealt at random";

        const float DealRetrySeconds = 1f;
        const float HostDealIntervalSeconds = 0.25f;

        // Host: the mode the host picked. A guest reads the lobby's.
        VesselSelectionMode _selectionMode = VesselSelectionRules.Default;
        float _nextDealRequestAt;
        float _nextHostDealAt;

        // The mode this pilot's panel last drew, so a change of mode (not a draft turn) drops a confirmation.
        VesselSelectionMode _drawnSelectionMode = VesselSelectionRules.Default;

        // Squad mode, this pilot's own choice.
        int _squadChoice = -1;
        bool _squadDealRequested;

        /// <summary>One drafting seat: a human (by client id) or a placed AI (by ordinal).</summary>
        struct DraftSeat
        {
            public bool IsAi;
            public ulong Client;
            public int AiOrdinal;
            public Domains Domain;
        }

        // Host: the draft.
        readonly List<DraftSeat> _draftOrder = new();
        int _draftTurn;                 // LobbySnapshot.DraftTurn: 0 none, 1..N the turn, -1 done
        double _draftTurnEndsAt;
        float _draftAiPickAt;
        string _draftSignature;

        /// <summary>Whether this card has the Vessels row: an arena card with more than one hull, outside the weekly challenge.</summary>
        bool SelectionRowAvailable => AllyHullPickerAvailable && _activePanel is ArenaLaunchPanel;

        /// <summary>The selection mode in force: the host's own, a guest's lobby's, Pick on any other card.</summary>
        VesselSelectionMode SelectionMode =>
            !SelectionRowAvailable ? VesselSelectionMode.Pick
            : IsClientMode ? VesselSelectionRules.Resolve(arcadeConfigSyncManager ? arcadeConfigSyncManager.CurrentLobby.VesselSelection : 0)
            : _selectionMode;

        int DraftTurnNow => IsClientMode ? (arcadeConfigSyncManager ? arcadeConfigSyncManager.CurrentLobby.DraftTurn : 0) : _draftTurn;

        bool DraftRunning => SelectionMode == VesselSelectionMode.Draft && DraftTurnNow > 0;

        bool DraftIncomplete => SelectionMode == VesselSelectionMode.Draft && DraftTurnNow != -1;

        double NetTime =>
            NetworkManager.Singleton && NetworkManager.Singleton.IsListening
                ? NetworkManager.Singleton.ServerTime.Time
                : Time.unscaledTimeAsDouble;

        // ── Card open / close ─────────────────────────────────────────────────────────────

        /// <summary>Card open, host and guest: Pick, no draft, no squad chosen; the host gates claims for this card.</summary>
        void ResetVesselSelection()
        {
            _selectionMode = VesselSelectionRules.Default;
            _draftOrder.Clear();
            _draftTurn = 0;
            _draftSignature = null;
            _squadChoice = -1;
            _squadDealRequested = false;
            _nextDealRequestAt = 0f;
            _drawnSelectionMode = VesselSelectionRules.Default;

            Player.ServerArenaClaimGate = !IsClientMode && ArenaHullsExclusive ? GateArenaClaim : null;
            PublishVesselSelection();
            DrawVesselSelection();
        }

        /// <summary>Card closed: no gate outlives the lobby that set it.</summary>
        void ClearVesselSelectionGate()
        {
            if (!IsClientMode) Player.ServerArenaClaimGate = null;
        }

        // ── The host's choice ─────────────────────────────────────────────────────────────

        /// <summary>LB / RB or the row's arrows: the host steps the mode.</summary>
        void HandleVesselSelectionStepRequested(int direction)
        {
            if (IsClientMode || !SelectionRowAvailable || _localPlayerReady) return;
            int next = ((int)_selectionMode - 1 + direction + 4) % 4 + 1;
            if (audioSystem) audioSystem.PlayMenuAudio(MenuAudioCategory.OptionClick);
            SetVesselSelectionMode(VesselSelectionRules.Resolve(next));
        }

        /// <summary>
        /// HOST: change the mode. Every seat's hull is given back (a hull picked under one mode is not a hull dealt or
        /// drafted under another), and a draft starts from its first turn.
        /// </summary>
        void SetVesselSelectionMode(VesselSelectionMode mode)
        {
            if (IsClientMode || _selectionMode == mode) return;
            _selectionMode = mode;
            CSDebug.LogVerbose(CSLogChannel.ArcadeLaunch, $"[ArcadeLaunch] Vessel selection: {mode}.");

            ReleaseEverySeat();
            _draftOrder.Clear();
            _draftTurn = 0;
            _draftSignature = null;
            if (mode == VesselSelectionMode.Draft) RestartDraft();

            PublishVesselSelection();
            HandleVesselSelectionChanged();
        }

        /// <summary>HOST: every human claim and every AI pick back to none.</summary>
        void ReleaseEverySeat()
        {
            if (gameData != null)
                foreach (var ip in gameData.Players)
                    if (ip is Player p && p && p.IsSpawned && !p.NetIsAI.Value)
                        p.TryGrantArenaHullClaim(VesselClassType.Random, Player.ArenaClaimKind.Chosen);

            if (config == null) return;
            config.SyncAIVesselSlots();
            bool changed = false;
            for (int i = 0; i < config.AIVessels.Count; i++)
            {
                if (config.AIVessels[i] == VesselClassType.Random) continue;
                config.AIVessels[i] = VesselClassType.Random;
                changed = true;
            }
            if (!changed) return;
            _aiChipSignature = null;
            RefreshRoster();
            BroadcastRosterToClients();
        }

        void PublishVesselSelection()
        {
            if (IsClientMode || !arcadeConfigSyncManager || !arcadeConfigSyncManager.IsSpawned) return;
            var seat = _draftTurn > 0 && _draftTurn <= _draftOrder.Count ? _draftOrder[_draftTurn - 1] : default;
            arcadeConfigSyncManager.NotifyVesselSelectionChanged((int)_selectionMode, _draftTurn, seat.IsAi, seat.Client,
                                                                 seat.AiOrdinal, _draftTurnEndsAt);
        }

        // ── The host's gates ──────────────────────────────────────────────────────────────

        /// <summary>HOST (<see cref="Player.ServerArenaClaimGate"/>): whether a human's claim may land under the mode.</summary>
        bool GateArenaClaim(Player pilot, Player.ArenaClaimKind kind, VesselClassType hull)
        {
            if (!ArenaHullsExclusive || !SelectionRowAvailable) return true;
            return SelectionMode switch
            {
                VesselSelectionMode.Pick => kind == Player.ArenaClaimKind.Chosen,
                // A drafted AI's hull is final: the pilot after it cannot take it (in Random and Squad the human wins and
                // the host re-deals the AI instead).
                VesselSelectionMode.Draft => IsDraftTurnOf(pilot) && !AiHolds(hull),
                _ => kind == Player.ArenaClaimKind.Dealt,
            };
        }

        bool AiHolds(VesselClassType hull) => config != null && config.AIVessels.Contains(hull);

        bool IsDraftTurnOf(Player pilot)
        {
            if (!pilot || _draftTurn <= 0 || _draftTurn > _draftOrder.Count) return false;
            var seat = _draftOrder[_draftTurn - 1];
            return !seat.IsAi && seat.Client == pilot.OwnerClientId;
        }

        /// <summary>
        /// HOST: whether <paramref name="requester"/> may set AI <paramref name="ordinal"/>'s hull by hand under the mode
        /// (on top of <see cref="MayRequesterPickAiSeat"/>): any time in Pick; in Draft only on that seat's own turn and
        /// only from its own team; never in Random or Squad, where the host deals every AI.
        /// </summary>
        bool MaySetAiHullUnderSelection(Player requester, int ordinal)
        {
            switch (SelectionMode)
            {
                case VesselSelectionMode.Pick:
                    return true;
                case VesselSelectionMode.Draft:
                    if (_draftTurn <= 0 || _draftTurn > _draftOrder.Count) return false;
                    var seat = _draftOrder[_draftTurn - 1];
                    return seat.IsAi && seat.AiOrdinal == ordinal && requester && requester.NetDomain.Value == seat.Domain;
                default:
                    return false;
            }
        }

        // ── Per frame ─────────────────────────────────────────────────────────────────────

        void TickVesselSelection()
        {
            if (!SelectionRowAvailable || _availableShips.Count == 0) return;
            if (!IsClientMode) TickVesselSelectionHost();
            TickOwnDeal();
            if (DraftRunning) DrawVesselSelection();   // the seconds count down
        }

        /// <summary>This pilot's own machine: ask for a deal when the mode deals one, and adopt whatever the server grants.</summary>
        void TickOwnDeal()
        {
            var mode = SelectionMode;
            if (mode == VesselSelectionMode.Pick) return;

            var self = LocalOwnedPlayerQuiet();
            if (!self || !self.IsSpawned) return;
            var claim = self.ArenaHullClaim;

            if (AIHullSeating.IsConcrete(claim))
            {
                int index = _availableShips.FindIndex(v => v && v.Class == claim);
                if (index >= 0 && (!_vesselConfirmed || index != _availableShipIndex))
                {
                    _pendingHullClaim = VesselClassType.Random;
                    _availableShipIndex = index;
                    ConfirmVesselNow();
                }
                return;
            }

            // No hull: whatever was confirmed under an earlier mode is gone.
            if (_vesselConfirmed)
            {
                _vesselConfirmed = false;
                RefreshVesselPicker();
                RefreshStartAvailability();
            }

            if (!VesselSelectionRules.HumanIsDealt(mode) || Time.unscaledTime < _nextDealRequestAt) return;
            // Squad waits for the pilot to choose a squad and press SELECT, unless they have none to choose.
            if (mode == VesselSelectionMode.Squad && SavedSquads().Count > 0 && !_squadDealRequested) return;

            _nextDealRequestAt = Time.unscaledTime + DealRetrySeconds;
            self.RequestArenaDealtClaim(mode == VesselSelectionMode.Squad ? ChosenSquadHulls() : null, CardHulls());
        }

        void TickVesselSelectionHost()
        {
            if (Time.unscaledTime < _nextHostDealAt) return;
            _nextHostDealAt = Time.unscaledTime + HostDealIntervalSeconds;

            switch (_selectionMode)
            {
                case VesselSelectionMode.Random:
                case VesselSelectionMode.Squad:
                    DealEveryAiSeat();
                    break;
                case VesselSelectionMode.Draft:
                    TickDraft();
                    break;
            }
        }

        /// <summary>HOST: every AI seat without a hull, or on a hull a human now holds, is dealt a random free one.</summary>
        void DealEveryAiSeat()
        {
            if (config == null) return;
            config.SyncAIVesselSlots();
            var humanClaims = HumanClaims();
            bool changed = false;
            for (int i = 0; i < config.AIVessels.Count; i++)
            {
                if (IsPinnedOpponentSeat(i)) continue;
                var current = config.AIVessels[i];
                if (AIHullSeating.IsConcrete(current) && !humanClaims.Contains(current)) continue;
                changed |= DealAiSeat(i);
            }
            if (!changed) return;
            _aiChipSignature = null;
            RefreshRoster();
            BroadcastRosterToClients();
        }

        /// <summary>HOST: put AI <paramref name="ordinal"/> in a random free hull. True when its hull changed.</summary>
        bool DealAiSeat(int ordinal)
        {
            var taken = TakenHullsForAiSeat(ordinal);
            var rng = new System.Random(unchecked(System.Environment.TickCount * 31 + ordinal));
            if (!VesselSelectionRules.TryDealRandom(CardHulls(), taken, rng, out var hull)) return false;
            if (config.AIVessels[ordinal] == hull) return false;
            config.AIVessels[ordinal] = hull;
            CSDebug.LogVerbose(CSLogChannel.ArcadeLaunch, $"[ArcadeLaunch] AI {ordinal} ({config.AIDomains[ordinal]}) dealt {hull}.");
            return true;
        }

        /// <summary>
        /// Regatta keeps its pinned opponent grid (every AI on a team no human flies races the Squirrel) until the
        /// racing AIs are confirmed well trained (Garrett's plan, 2026-11-06): those seats are neither dealt nor drafted.
        /// </summary>
        bool IsPinnedOpponentSeat(int ordinal) =>
            ordinal >= 0 && ordinal < config.AIDomains.Count &&
            AIHullSeating.IsOpponentSeat(OpponentHull, config.AIDomains[ordinal], HumanDomains());

        HashSet<VesselClassType> HumanClaims()
        {
            var claims = new HashSet<VesselClassType>();
            if (gameData == null) return claims;
            foreach (var ip in gameData.Players)
                if (ip is Player p && p && p.IsSpawned && !p.NetIsAI.Value && AIHullSeating.IsConcrete(p.ArenaHullClaim))
                    claims.Add(p.ArenaHullClaim);
            return claims;
        }

        VesselClassType[] CardHulls() =>
            _availableShips.Where(v => v).Select(v => v.Class).Distinct().ToArray();

        // ── Draft (host) ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// HOST: build the snake order from the seats in the lobby and start at turn 1. Teams pick in domain order; within a
        /// team its humans go first, then its AI, in lobby order. A pinned opponent seat does not draft.
        /// </summary>
        void RestartDraft()
        {
            _draftOrder.Clear();
            var teams = new SortedDictionary<int, List<DraftSeat>>();

            void Add(DraftSeat seat)
            {
                if (!teams.TryGetValue((int)seat.Domain, out var list)) teams[(int)seat.Domain] = list = new List<DraftSeat>();
                list.Add(seat);
            }

            if (gameData != null)
                foreach (var p in gameData.Players.OfType<Player>()
                             .Where(p => p && p.IsSpawned && !p.NetIsAI.Value && !p.IsInitializedAsAI)
                             .OrderBy(p => p.OwnerClientId))
                    Add(new DraftSeat { Client = p.OwnerClientId, Domain = p.NetDomain.Value, AiOrdinal = -1 });

            if (config != null)
                for (int i = 0; i < config.AIDomains.Count; i++)
                    if (!IsPinnedOpponentSeat(i))
                        Add(new DraftSeat { IsAi = true, AiOrdinal = i, Domain = config.AIDomains[i] });

            var seats = teams.Values.ToList();
            foreach (var turn in VesselSelectionRules.SnakeOrder(seats.Select(t => t.Count).ToList()))
                _draftOrder.Add(seats[turn.Team][turn.Seat]);

            _draftSignature = DraftSignature();
            _draftTurn = 0;
            AdvanceDraft();
        }

        /// <summary>Who sits where: a change (a pilot joined, an AI was added, someone moved team) restarts the draft.</summary>
        string DraftSignature()
        {
            var parts = new List<string>();
            if (gameData != null)
                foreach (var p in gameData.Players.OfType<Player>().Where(p => p && p.IsSpawned && !p.NetIsAI.Value))
                    parts.Add($"h{p.OwnerClientId}:{(int)p.NetDomain.Value}");
            if (config != null)
                for (int i = 0; i < config.AIDomains.Count; i++) parts.Add($"a{i}:{(int)config.AIDomains[i]}");
            parts.Sort(System.StringComparer.Ordinal);
            return string.Join(",", parts);
        }

        void TickDraft()
        {
            if (_draftSignature != DraftSignature())
            {
                CSDebug.LogVerbose(CSLogChannel.ArcadeLaunch, "[ArcadeLaunch] The seats changed: the draft starts again.");
                ReleaseEverySeat();
                RestartDraft();
                PublishVesselSelection();
                HandleVesselSelectionChanged();
                return;
            }

            if (_draftTurn <= 0 || _draftTurn > _draftOrder.Count) return;
            var seat = _draftOrder[_draftTurn - 1];

            if (SeatHasHull(seat))
            {
                AdvanceDraft();
                return;
            }

            bool allAiTeam = seat.IsAi && !HumanDomains().Contains(seat.Domain);
            bool aiTurnDue = allAiTeam && Time.unscaledTime >= _draftAiPickAt;
            if (aiTurnDue || NetTime >= _draftTurnEndsAt)
            {
                PickRandomForDraftSeat(seat);
                // With no free hull left the seat is passed over and flies the spawner's draw.
                AdvanceDraft();
            }
        }

        bool SeatHasHull(DraftSeat seat)
        {
            if (seat.IsAi)
                return config != null && seat.AiOrdinal < config.AIVessels.Count &&
                       AIHullSeating.IsConcrete(config.AIVessels[seat.AiOrdinal]);
            var pilot = FindHumanPlayer(seat.Client);
            return !pilot || AIHullSeating.IsConcrete(pilot.ArenaHullClaim);   // a pilot who left has nothing to wait for
        }

        void PickRandomForDraftSeat(DraftSeat seat)
        {
            if (seat.IsAi)
            {
                config.SyncAIVesselSlots();
                if (seat.AiOrdinal >= config.AIVessels.Count || !DealAiSeat(seat.AiOrdinal)) return;
                _aiChipSignature = null;
                RefreshRoster();
                BroadcastRosterToClients();
                return;
            }

            var pilot = FindHumanPlayer(seat.Client);
            if (pilot) pilot.ServerDealArenaHull(null, CardHulls().Where(h => !AiHolds(h)).ToArray(), Player.ArenaClaimKind.Dealt);
        }

        /// <summary>HOST: move to the next seat that still needs a hull, or finish the draft.</summary>
        void AdvanceDraft()
        {
            do { _draftTurn++; }
            while (_draftTurn <= _draftOrder.Count && SeatHasHull(_draftOrder[_draftTurn - 1]));

            if (_draftTurn > _draftOrder.Count)
            {
                _draftTurn = -1;
                CSDebug.LogVerbose(CSLogChannel.ArcadeLaunch, "[ArcadeLaunch] Draft complete.");
            }
            else
            {
                _draftTurnEndsAt = NetTime + draftTurnSeconds;
                _draftAiPickAt = Time.unscaledTime + draftAiPickDelaySeconds;
            }

            PublishVesselSelection();
            HandleVesselSelectionChanged();
        }

        // ── What the pilot may do ─────────────────────────────────────────────────────────

        /// <summary>Whether this pilot may pick their own hull on the panel right now (Pick, or their own draft turn).</summary>
        bool MayChooseOwnHullNow()
        {
            switch (SelectionMode)
            {
                case VesselSelectionMode.Pick:
                    return true;
                case VesselSelectionMode.Draft:
                    var lobbyTurn = CurrentDraftSeat();
                    var self = LocalOwnedPlayerQuiet();
                    return lobbyTurn.HasValue && !lobbyTurn.Value.IsAi && self && lobbyTurn.Value.Client == self.OwnerClientId;
                default:
                    return false;
            }
        }

        /// <summary>The seat whose draft turn it is, read the same way on the host and on a guest.</summary>
        DraftSeat? CurrentDraftSeat()
        {
            if (!DraftRunning) return null;
            if (!IsClientMode)
                return _draftTurn > 0 && _draftTurn <= _draftOrder.Count ? _draftOrder[_draftTurn - 1] : null;
            var lobby = arcadeConfigSyncManager.CurrentLobby;
            int ordinal = lobby.DraftAiOrdinal;
            return new DraftSeat
            {
                IsAi = lobby.DraftSeatIsAi,
                Client = lobby.DraftClient,
                AiOrdinal = ordinal,
                Domain = lobby.DraftSeatIsAi && ordinal >= 0 && ordinal < config.AIDomains.Count ? config.AIDomains[ordinal] : default,
            };
        }

        double DraftTurnEndsAt => IsClientMode ? arcadeConfigSyncManager.CurrentLobby.DraftTurnEndsAt : _draftTurnEndsAt;

        /// <summary>Start's extra gate: no launch while a draft is still picking.</summary>
        bool SelectionHoldsStart(out string reason)
        {
            reason = draftRunningStartLine;
            return DraftIncomplete;
        }

        // ── Squad (this pilot) ────────────────────────────────────────────────────────────

        /// <summary>This pilot's saved squads, made and joined.</summary>
        static List<SavedSquad> SavedSquads()
        {
            var book = UGSDataService.Instance?.SquadRepo?.Data?.Book;
            return book?.Squads?.Where(s => s != null).ToList() ?? new List<SavedSquad>();
        }

        SavedSquad ChosenSquad()
        {
            var squads = SavedSquads();
            if (squads.Count == 0) return null;
            if (_squadChoice < 0 || _squadChoice >= squads.Count)
            {
                // Open on the squad the player took into the Arena from the Ark, else their active one.
                var intent = SquadLaunchIntent.Squad;
                int index = intent != null ? squads.FindIndex(s => s.Id == intent.Id) : -1;
                var book = UGSDataService.Instance?.SquadRepo?.Data?.Book;
                _squadChoice = index >= 0 ? index : Mathf.Clamp(book?.ActiveIndex ?? 0, 0, squads.Count - 1);
            }
            return squads[_squadChoice];
        }

        /// <summary>The chosen squad's vessels. Only the vessel comes: never the slot's element (Garrett, 2026-11-06).</summary>
        VesselClassType[] ChosenSquadHulls()
        {
            var squad = ChosenSquad();
            if (squad == null) return System.Array.Empty<VesselClassType>();
            var hulls = new List<VesselClassType>();
            for (int i = 0; i < SavedSquad.SlotCount; i++) hulls.Add(squad.GetSlot(i).Vessel);
            return hulls.ToArray();
        }

        /// <summary>Squad mode, before the deal: the vessel arrows step through the saved squads.</summary>
        bool TryStepSquad(int direction)
        {
            if (SelectionMode != VesselSelectionMode.Squad || _squadDealRequested || _vesselConfirmed) return false;
            var squads = SavedSquads();
            if (squads.Count == 0) return true;
            ChosenSquad();
            _squadChoice = ((_squadChoice + direction) % squads.Count + squads.Count) % squads.Count;
            if (audioSystem) audioSystem.PlayMenuAudio(MenuAudioCategory.OptionClick);
            DrawVesselSelection();
            return true;
        }

        /// <summary>Squad mode: SELECT deals this pilot one of the chosen squad's vessels.</summary>
        bool TryDealFromChosenSquad()
        {
            if (SelectionMode != VesselSelectionMode.Squad || _vesselConfirmed) return false;
            _squadDealRequested = true;
            _nextDealRequestAt = 0f;
            TickOwnDeal();
            return true;
        }

        // ── Drawing ───────────────────────────────────────────────────────────────────────

        /// <summary>The mode or the draft's turn moved (host or guest): every pilot redraws, and the squad choice re-arms.</summary>
        void HandleVesselSelectionChanged()
        {
            var mode = SelectionMode;
            if (mode != _drawnSelectionMode)
            {
                // A hull confirmed under the last mode was given back by the host: pick, deal or draft again.
                _drawnSelectionMode = mode;
                _vesselConfirmed = false;
            }
            if (mode != VesselSelectionMode.Squad) _squadDealRequested = false;
            if (!VesselSelectionRules.HumanPicksFreely(mode) && !MayChooseOwnHullNow())
                _pendingHullClaim = VesselClassType.Random;
            _nextDealRequestAt = 0f;
            RefreshVesselPicker();   // draws the Vessels row too
            DrawVesselSelection();   // ...also when an AI seat holds the panel
            RefreshStartAvailability();
        }

        /// <summary>A guest's lobby moved the mode or the draft's turn.</summary>
        void HandleVesselSelectionChangedOnClient()
        {
            if (!IsClientMode) return;
            HandleVesselSelectionChanged();
        }

        void DrawVesselSelection()
        {
            if (_activePanel is not ArenaLaunchPanel arena) return;
            bool shown = SelectionRowAvailable;
            var mode = SelectionMode;
            arena.ShowVesselSelection(shown, mode, !IsClientMode && !_localPlayerReady, shown ? SelectionStatus(mode) : null);

            if (shown && mode == VesselSelectionMode.Squad && !_vesselConfirmed && _vesselSeat == OwnVesselSeat)
            {
                var squad = ChosenSquad();
                if (squad != null)
                    arena.ShowVesselCaption(TemplateText.Fill(squadChoiceCaption, "squad", squad.Name,
                        "vessels", string.Join(", ", ChosenSquadHulls().Select(h => h.ToString()))));
            }
        }

        /// <summary>The line beside the Vessels caption: the draft's turn, or null for the mode's own hint.</summary>
        string SelectionStatus(VesselSelectionMode mode)
        {
            if (mode == VesselSelectionMode.Squad && SavedSquads().Count == 0)
                return TemplateText.Fill(noSquadLine);

            if (mode != VesselSelectionMode.Draft) return null;
            if (DraftTurnNow == -1) return TemplateText.Fill(draftDoneLine);

            var seat = CurrentDraftSeat();
            if (!seat.HasValue) return null;
            string seconds = Mathf.Max(0, Mathf.CeilToInt((float)(DraftTurnEndsAt - NetTime))).ToString();
            var self = LocalOwnedPlayerQuiet();

            if (!seat.Value.IsAi && self && seat.Value.Client == self.OwnerClientId)
                return TemplateText.Fill(draftYourTurnLine, "seconds", seconds);
            if (seat.Value.IsAi && self && self.NetDomain.Value == seat.Value.Domain)
                return TemplateText.Fill(draftAllyTurnLine, "domain", seat.Value.Domain.ToString(), "seconds", seconds);

            string name = seat.Value.IsAi
                ? TemplateText.Fill(draftAiSeatName, "domain", seat.Value.Domain.ToString())
                : FindPilotName(seat.Value.Client);
            return TemplateText.Fill(draftWaitingLine, "seat", name, "seconds", seconds);
        }

        string FindPilotName(ulong client)
        {
            if (gameData != null)
                foreach (var ip in gameData.Players)
                    if (ip is Player p && p && !p.NetIsAI.Value && p.OwnerClientId == client)
                        return p.NetName.Value.ToString();
            return string.Empty;
        }

        #endregion
    }
}
