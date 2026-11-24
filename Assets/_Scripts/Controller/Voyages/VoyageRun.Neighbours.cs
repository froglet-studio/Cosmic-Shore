using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.UI;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Events next door and the detour vote (Frogma Rev 134 WorldEvents, Event, Vote B, Detour; Shombith 2026-11-24:
    /// "nothing happening in neighbouring cells", "the detour is a vote"). A named voyage's leg is planned with events beside
    /// the lane (<see cref="VoyageNeighbours.Plan"/>, seeded, so a party plans the same); the pause chart shows them all.
    /// When the Ark crosses into a cell with one, it lights up (a toast, its clock starts) and the squad votes, five
    /// seconds: AI seats vote as the vote opens by their pilot's mood, the one human press decides. A detour holds the Ark
    /// in the cell (<see cref="VoyageToyDefinitionSO.DetourSeconds"/> on its anchor) and brings what was next door to the
    /// lane's side: a wreck's spilled crystals, a bloom's. Pressing on leaves it to fade.
    /// <para>Not built yet: a party friend's press (the vote runs on the host, which counts only its own press and the AI
    /// seats), and the distress call and predator events.</para>
    /// </summary>
    public sealed partial class VoyageRun
    {
        VoyageJob _neighboursFor;
        readonly List<VoyageNeighbourFix> _legNeighbours = new();
        /// <summary>When each planned event fades (Time.time), or -1 while the Ark has not reached its cell.</summary>
        readonly List<float> _neighbourEnds = new();
        int _neighbourCell;
        int _voteAbout = -1;
        float _detourPending;
        readonly List<PilotMood> _seatMoods = new(SavedSquad.SlotCount);

        /// <summary>Every frame at sea: plan the leg once, light the event of the cell the Ark crossed into, run the vote,
        /// and keep the screens' list with each event's time left.</summary>
        void TickNeighbours()
        {
            var job = _state?.Current;
            if (!_def || job is not { IsNamed: true } || !_state.AtSea || _fallen)
            {
                if (_neighbours.Count > 0 && (job == null || !_state.AtSea)) ClearNeighbours();
                return;
            }
            int cell = LegCell(out int cells);
            if (cells <= 0) return;
            if (_neighboursFor != job) PlanNeighbours(job, cells);

            if (cell != _neighbourCell)
            {
                _neighbourCell = cell;
                for (int i = 0; i < _legNeighbours.Count; i++)
                    if (_legNeighbours[i].Cell == cell && _neighbourEnds[i] < 0f) LightNeighbour(i);
            }
            TickVote();
            RefreshNeighbours();
        }

        void PlanNeighbours(VoyageJob job, int cells)
        {
            ClearNeighbours();
            _neighboursFor = job;
            _legNeighbours.AddRange(VoyageNeighbours.Plan(VoyageRules.Hash(_state.Day, job.VoyageId ?? string.Empty, "next door"),
                cells, VoyageSea.DifficultyOf(job), _def.NeighbourChance, _def.NeighbourSeconds));
            foreach (var _ in _legNeighbours) _neighbourEnds.Add(-1f);
        }

        void ClearNeighbours()
        {
            _neighboursFor = null;
            _legNeighbours.Clear();
            _neighbourEnds.Clear();
            _neighbours.Clear();
            _neighbourCell = 0;
            _voteAbout = -1;
            _vote = null;
            _detourPending = 0f;
        }

        /// <summary>The Ark is in the event's cell: its clock starts, the feed says so, and the squad votes.</summary>
        void LightNeighbour(int i)
        {
            var n = _legNeighbours[i];
            _neighbourEnds[i] = Time.time + n.SecondsLeft;
            VoyageScreenToasts.Neighbour(_sea, Lines, n);
            if (_following || (_vote != null && !_vote.Closed)) return;   // the host asks; one vote at a time
            AskVote(i);
        }

        void AskVote(int about)
        {
            int mySeat = _squad != null ? Mathf.Clamp(_squad.MySeat, 0, SavedSquad.SlotCount - 1) : 0;
            var human = new bool[SavedSquad.SlotCount];
            human[mySeat] = true;
            var info = Content?.VoyageType(_state.Current.TemplateId);
            _seatMoods.Clear();
            for (int seat = 0; seat < SavedSquad.SlotCount; seat++)
            {
                var pilot = _squad != null ? Content?.Person(_squad.GetSlot(seat).PilotId) : null;
                _seatMoods.Add(VoyageMood.Of(info, pilot?.Faction ?? FactionType.None));
            }
            var n = _legNeighbours[about];
            _vote = new VoyageVote(SavedSquad.SlotCount, human)
            {
                Asker = VoyageNeighbours.Asker(_seatMoods, mySeat),
                Host = mySeat,
                DetourSeconds = _def.DetourSeconds,
                About = n.Kind,
                AboutTitle = n.Title ?? string.Empty,
            };
            for (int seat = 0; seat < SavedSquad.SlotCount; seat++)
                if (seat != mySeat) _vote.CastAi(seat, VoyageNeighbours.AiBallot(_seatMoods[seat]));
            _voteAbout = about;
        }

        /// <summary>The open vote's clock; when it closes, a detour is taken.</summary>
        void TickVote()
        {
            if (_vote == null || _voteAbout < 0) return;
            _vote.Tick(Time.deltaTime);
            if (!_vote.Closed) return;
            int about = _voteAbout;
            _voteAbout = -1;
            if (_vote.Outcome == VoyageVote.Detour && about < _legNeighbours.Count && Time.time < _neighbourEnds[about])
                TakeDetour(about);
            // The panel keeps the closed vote to show its result line, then lets it go (VoyageVotePanel).
        }

        /// <summary>The squad turns off the lane: the Ark holds in this cell, and what was next door lies at the lane's
        /// side on its bearing (a wreck's spilled crystals, a bloom's), seeded like the leg.</summary>
        void TakeDetour(int about)
        {
            var n = _legNeighbours[about];
            VoyageScreenToasts.Detour(_sea, Lines, n);
            if (_sea.IsAnchored) _sea.SetAnchorRemaining(_sea.AnchorRemaining + _def.DetourSeconds);
            else _detourPending += _def.DetourSeconds;
            var ark = Ark;
            var cell = CurrentCell;
            if (!ark || !cell) return;
            int count = n.Kind == VoyageNeighbourKind.Wreck ? _def.WreckCrystals : n.Kind == VoyageNeighbourKind.Bloom ? _def.BloomCrystals : 0;
            float radius = CellConveyor.MembraneRadiusFloor * Mathf.Max(1f, _arkCfg?.CellScale ?? 1f);
            // Bearing 90 is port (the Ark's left).
            var side = ark.transform.right * (n.Bearing >= 0f ? -1f : 1f);
            var at = ark.Position + side * radius * 0.6f;
            SpillLoose(cell.transform, at, count, radius * 0.15f,
                new System.Random(VoyageRules.Hash(_state.Day, _state.Current.VoyageId ?? string.Empty, "detour" + n.Cell)));
        }

        /// <summary>The Ark reached a cell: a detour voted before it got there holds it this much longer.</summary>
        float TakeDetourAnchor()
        {
            float s = _detourPending;
            _detourPending = 0f;
            return s;
        }

        /// <summary>The anchor the Ark drops at each cell: a Crystal Run's or a Salvage's window, plus a detour's hold.</summary>
        float AnchorAtCell() => RaidAnchorSeconds() + TakeDetourAnchor();

        /// <summary>The screens' list: every planned event, lit ones with their time left, faded ones gone.</summary>
        void RefreshNeighbours()
        {
            _neighbours.Clear();
            for (int i = 0; i < _legNeighbours.Count; i++)
            {
                var n = _legNeighbours[i];
                float end = _neighbourEnds[i];
                if (end >= 0f)
                {
                    float left = end - Time.time;
                    if (left <= 0f) continue;
                    n.SecondsLeft = left;
                }
                _neighbours.Add(n);
            }
        }
    }
}
