using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.UI
{
    // Part of ArcadeGameConfigureModal: the pilots drawn as a lineup of pedestals (ConfigureSeatLineup,
    // Docs/MENU_REDESIGN_B.md section 74). The lineup is a view of the SAME roster the domain tiles' chips show;
    // what a pilot does on it comes back here and goes through the methods the tiles always used.
    public partial class ArcadeGameConfigureModal
    {
        // The seats the lineup was last handed, so its "seat N moved team" maps back to a pilot or a placement.
        readonly List<ConfigureSeatLineup.Seat> _lineupSeats = new();

        // The domain each AI seat was drawn under by RefreshAIPreviewChips, in ordinal order: a guest's AI seats
        // are not placed (the host holds the placements), so the chips' balanced draw is the only record of them.
        readonly List<Domains> _aiSeatDomains = new();

        void WireSeatLineup(ConfigureSeatLineup lineup)
        {
            if (!lineup) return;
            lineup.OnAddAiRequested += AddAiToLeastFilledTeam;
            lineup.OnTeamStepRequested += HandleSeatTeamStep;
            lineup.OnKickRequested += HandleKickAIRequested;
            lineup.OnSeatPicked += HandleSeatPicked;
        }

        void UnwireSeatLineup(ConfigureSeatLineup lineup)
        {
            if (!lineup) return;
            lineup.OnAddAiRequested -= AddAiToLeastFilledTeam;
            lineup.OnTeamStepRequested -= HandleSeatTeamStep;
            lineup.OnKickRequested -= HandleKickAIRequested;
            lineup.OnSeatPicked -= HandleSeatPicked;
        }

        /// <summary>Hand the active panel's lineup the roster as it stands. Cheap; a no-op without a lineup.</summary>
        void RefreshSeatLineup()
        {
            var lineup = _activePanel ? _activePanel.SeatLineup : null;
            if (!lineup || config == null || gameData == null) return;

            _lineupSeats.Clear();
            ulong localId = NetworkManager.Singleton ? NetworkManager.Singleton.LocalClientId : 0UL;
            var dataService = PlayerDataService.Instance;
            bool editable = !_weeklyChallengeLocked && !_localPlayerReady;
            // Rev 25 (section 79): every pilot stands on the hull they fly, and where the card's hulls are picked a
            // press on a seat points the shared vessel strip at it - the seat rows' hull taps, drawn on the lineup.
            bool picker = AllyHullPickerAvailable && _activePanel is ArenaLaunchPanel;
            // Wherever the AI row is offered, the host's press on an AI's pedestal also selects that AI for its own
            // difficulty - Arcade cards, whose hull is pinned, included. The host's own pedestal takes it back.
            bool levels = SeatDifficultyAvailable && !IsClientMode;
            var humanDomains = HumanDomains();

            foreach (var ip in gameData.Players)
            {
                if (ip is not Player p || !p || p.NetIsAI.Value) continue;
                bool isLocal = p.OwnerClientId == localId;
                _lineupSeats.Add(new ConfigureSeatLineup.Seat
                {
                    Kind = ConfigureSeatLineup.SeatKind.Human,
                    Name = p.Name,
                    Avatar = dataService != null ? dataService.GetAvatarSprite(p.NetAvatarId.Value) : null,
                    Domain = p.NetDomain.Value,
                    IsLocal = isLocal,
                    IsHost = p.OwnerClientId == NetworkManager.ServerClientId,
                    AiOrdinal = -1,
                    CanChangeTeam = isLocal && editable,
                    Hull = HullIcon(isLocal ? LocalSeatVessel() : CardVessel(PilotHull(p))),
                    CanPickVessel = isLocal && (picker || levels),
                    EditingVessel = isLocal && picker && _vesselSeat == OwnVesselSeat,
                });
            }

            for (int i = 0; i < _aiSeatDomains.Count; i++)
            {
                bool placed = config.AIDomains != null && i < config.AIDomains.Count && config.AIDomains[i] != Domains.Blue;
                bool host = !IsClientMode && placed;
                var pick = AllyHullPickerAvailable ? config.AIVesselAt(i) : VesselClassType.Random;
                bool pickable = (picker || levels) && MaySelectAiSeat(i);
                _lineupSeats.Add(new ConfigureSeatLineup.Seat
                {
                    Kind = ConfigureSeatLineup.SeatKind.Ai,
                    Domain = _aiSeatDomains[i],
                    AiOrdinal = i,
                    CanChangeTeam = host && editable,
                    CanKick = host && CanKickAi,
                    Hull = HullIcon(CardVessel(AIHullSeating.SeatHull(OpponentHull, _aiSeatDomains[i], humanDomains, pick))),
                    CanPickVessel = pickable,
                    EditingVessel = pickable && _vesselSeat == i,
                    Difficulty = SeatDifficultyAvailable && i < config.AIDifficulties.Count ? config.AIDifficulties[i] : 0,
                });
            }

            int ceiling = MatchSeatCeiling;
            int seats = _weeklyChallengeLocked ? _lineupSeats.Count : Mathf.Max(ceiling, _lineupSeats.Count);
            bool canAdd = !IsClientMode && editable && _lineupSeats.Count < ceiling;
            lineup.Show(_lineupSeats, seats, canAdd, showTeams: TeamsOnCard > 1);
        }

        /// <summary>A seat was pressed: the shared vessel strip picks that seat's hull (your own, or an AI's).</summary>
        void HandleSeatPicked(int seatIndex)
        {
            if (seatIndex < 0 || seatIndex >= _lineupSeats.Count) return;
            var seat = _lineupSeats[seatIndex];
            if (!seat.CanPickVessel) return;
            SelectVesselSeat(seat.Kind == ConfigureSeatLineup.SeatKind.Ai ? seat.AiOrdinal : OwnVesselSeat);
        }

        /// <summary>The hull the local pilot has on the strip now (picked, claimed or not).</summary>
        SO_Vessel LocalSeatVessel() =>
            _availableShipIndex >= 0 && _availableShipIndex < _availableShips.Count ? _availableShips[_availableShipIndex] : null;

        /// <summary>Another pilot's hull as this machine knows it: their arena claim, else the hull they last flew.</summary>
        static VesselClassType PilotHull(Player p) =>
            AIHullSeating.IsConcrete(p.ArenaHullClaim) ? p.ArenaHullClaim : p.NetDefaultVesselType.Value;

        /// <summary>A seat's hull icon; a card with one hull shows that hull on every seat whose own is not decided.</summary>
        Sprite HullIcon(SO_Vessel vessel)
        {
            if (!vessel && _availableShips.Count == 1) vessel = _availableShips[0];
            return vessel ? vessel.IconActive : null;
        }

        /// <summary>
        /// The teams a pilot may step through on this card: the Jade → Ruby → Gold prefix the card allows
        /// (<see cref="SO_ArcadeGame.MaxDomainsAllowed"/>). Not the player-count bound - a solo pilot may still
        /// pick their colour, as the domain tiles always let them.
        /// </summary>
        int TeamsOnCard => _selectedGame
            ? Mathf.Clamp(_selectedGame.MaxDomainsAllowed, MinDomainsForGame, MaxSupportedDomains)
            : MaxSupportedDomains;

        static readonly Domains[] TeamOrder = { Domains.Jade, Domains.Ruby, Domains.Gold };

        Domains StepTeam(Domains from, int direction)
        {
            int count = Mathf.Max(1, TeamsOnCard);
            int i = System.Array.IndexOf(TeamOrder, from);
            if (i < 0 || i >= count) i = 0;
            return TeamOrder[((i + direction) % count + count) % count];
        }

        /// <summary>A seat's team stepper moved: your own seat picks your domain; an AI's (host) moves the placement.</summary>
        void HandleSeatTeamStep(int seatIndex, int direction)
        {
            if (seatIndex < 0 || seatIndex >= _lineupSeats.Count) return;
            var seat = _lineupSeats[seatIndex];
            if (!seat.CanChangeTeam) return;

            var next = StepTeam(seat.Domain, direction);
            if (next == seat.Domain) return;
            if (seat.Kind == ConfigureSeatLineup.SeatKind.Human) HandleDomainSelected(next);
            else MoveAiToDomain(seat.AiOrdinal, next);
        }

        /// <summary>
        /// Host only. Move a placed AI to <paramref name="domain"/> - the placement list is the roster, so the
        /// seat simply changes team where it stands; nothing else re-balances.
        /// </summary>
        void MoveAiToDomain(int ordinal, Domains domain)
        {
            if (IsClientMode || config == null || _selectedGame == null || _weeklyChallengeLocked) return;
            if (ordinal < 0 || ordinal >= config.AIDomains.Count) return;

            config.AIDomains[ordinal] = domain;
            // As Add AI does: a placement declares the domain in the match.
            config.DomainCount = Mathf.Max(config.DomainCount, DomainPrefixCount(config.AIDomains));
            HandlePlayerCountSelected(BaseSeats + config.AIDomains.Count);
            BroadcastRosterToClients();
        }

        /// <summary>
        /// Add AI from the lineup: the AI joins the team with the fewest pilots (Shombith, 2026-10-26). A tie goes
        /// to a team other than your own, then to the first in Jade, Ruby, Gold order.
        /// </summary>
        void AddAiToLeastFilledTeam()
        {
            if (IsClientMode || config == null || _selectedGame == null) return;

            int count = Mathf.Max(1, TeamsOnCard);
            var tally = new int[count];
            foreach (var seat in _lineupSeats)
            {
                int i = System.Array.IndexOf(TeamOrder, seat.Domain);
                if (i >= 0 && i < count) tally[i]++;
            }

            var local = LocalOwnedPlayerQuiet();
            var mine = local ? local.NetDomain.Value : Domains.Blue;
            int best = 0;
            for (int i = 1; i < count; i++)
            {
                bool fewer = tally[i] < tally[best];
                bool tieAwayFromMine = tally[i] == tally[best] && TeamOrder[best] == mine && TeamOrder[i] != mine;
                if (fewer || tieAwayFromMine) best = i;
            }
            AddAiToDomain(TeamOrder[best]);
        }
    }
}
