using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// What the run tells the in-game screens (Frogma Rev 134 / Rev 137; <see cref="IVoyageScreenSource"/>): the leg and its
    /// difficulty for the pause chart, the seats for the vote's ballots, and - once the Milestone 2 gameplay builds them - the
    /// events next door, the rival crews and the detour vote. Until then those three report none and the screens that show
    /// them stay hidden. The gameplay fills <see cref="_neighbours"/>, <see cref="_rivals"/> and <see cref="_vote"/>.
    /// </summary>
    public sealed partial class VoyageRun : IVoyageScreenSource
    {
        /// <summary>Events in the cells beside the lane (the gameplay's to fill; Rev 134 WorldEvents).</summary>
        readonly List<VoyageNeighbourFix> _neighbours = new();

        /// <summary>Rival crews in sight (the gameplay's to fill; Rev 137).</summary>
        readonly List<VoyageRivalFix> _rivals = new();

        /// <summary>The detour vote open now (the gameplay's to open, tick and close; Rev 134 Vote B).</summary>
        VoyageVote _vote;

        PilotArtifactCatalogSO _screenPilots;

        public int SeaDifficulty => _state?.Current is { IsNamed: true } job ? VoyageSea.DifficultyOf(job) : 0;

        public float CellSeconds => _state?.Current is { IsNamed: true } && _def ? _def.CellSeconds : 0f;

        public void GetNeighbours(List<VoyageNeighbourFix> into)
        {
            into.Clear();
            into.AddRange(_neighbours);
        }

        public void GetRivals(List<VoyageRivalFix> into)
        {
            into.Clear();
            into.AddRange(_rivals);
        }

        public VoyageVote OpenVote => _vote is { Closed: false } ? _vote : null;

        public void CastVote(int option)
        {
            if (_vote == null || _squad == null) return;
            _vote.Cast(Mathf.Clamp(_squad.MySeat, 0, SavedSquad.SlotCount - 1), option);
        }

        public void GetSeats(List<VoyageScreenSeat> into)
        {
            into.Clear();
            if (_squad == null) return;
            if (!_screenPilots) _screenPilots = PilotArtifactCatalogSO.Load();
            int mySeat = Mathf.Clamp(_squad.MySeat, 0, SavedSquad.SlotCount - 1);
            for (int seat = 0; seat < SavedSquad.SlotCount; seat++)
            {
                var slot = _squad.GetSlot(seat);
                var pilot = _screenPilots ? _screenPilots.ResolvePilot(slot, null) : null;
                bool you = seat == mySeat;
                bool friend = !you && seat < _seatClients.Count && _seatClients[seat] >= 0;
                into.Add(new VoyageScreenSeat
                {
                    Pilot = CosmicShore.UI.VoyageSeats.PilotName(slot, pilot, you),
                    Player = friend ? slot.MemberName ?? string.Empty : string.Empty,
                    Vessel = slot.Vessel,
                    You = you,
                    Human = you || friend,
                    Portrait = pilot ? pilot.Portrait : null,
                });
            }
        }

        /// <summary>What the end board's Awards A and Lost layouts need beyond the numbers (Frogma Rev 134): a named voyage's
        /// name, its places in order for the run line, and where the Ark fell.</summary>
        void TellScreens(VoyageScoreboard board, VoyageJob job)
        {
            if (board == null || job is not { IsNamed: true }) return;
            if (string.IsNullOrEmpty(board.Name)) board.Name = job.Title ?? string.Empty;
            board.Stops.Add(PlaceName(job.From));
            if (TryGetObjective(out var o) && o.Cells > 0)
            {
                for (int i = 0; i < o.Cells; i++) board.Stops.Add(VoyageObjectiveText.StopName(Lines, o, i));
                if (board.Board == VoyageScoreboard.Kind.Lost)
                {
                    board.LostCell = Mathf.Max(1, o.Cell);
                    board.LostAt = VoyageObjectiveText.StopName(Lines, o, board.LostCell - 1);
                }
            }
            else board.Stops.Add(PlaceName(job.To));
        }

        /// <summary>The screens read this run while it is under way (<see cref="VoyageScreenFeed"/>).</summary>
        void HookScreens(bool on)
        {
            if (on) VoyageScreenFeed.Source = this;
            else VoyageScreenFeed.Release(this);
            if (on) return;
            _neighbours.Clear();
            _rivals.Clear();
            _vote = null;
        }
    }
}
