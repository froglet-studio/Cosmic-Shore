using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The squad beside the Ark (<see cref="ArkFormation"/>, Shombith 2026-11-17), as in a voyage (VoyageRun.Formation.cs): as the voyage opens each seat this
    /// machine flies is set down in its slot (seat 1 to port, seat 2 to starboard, seat 3 over the back; the escort sim's own AI seat too); when the intro
    /// launches the squad they wait in the hangar instead and fly out to those slots (<see cref="ArkSortie"/>). A
    /// wingmate that strays past its leash comes back to its own slot.
    /// </summary>
    public sealed partial class ArkEscortRun
    {
        ArkSortie _sortie;
        Bounds? _arkBounds;
        readonly List<(IVesselStatus status, int seat)> _formation = new();

        ArkFormationSettings FormationSettings => _def ? _def.ArkFormation : null;

        /// <summary>The Ark's own extent, read once per voyage (the hull is laid by then).</summary>
        Bounds ArkBounds()
        {
            if (_arkBounds == null && Ark) _arkBounds = ArkFormation.LocalBounds(Ark, HullHalfLength);
            return _arkBounds ?? new Bounds(Vector3.zero, Vector3.one * HullHalfLength);
        }

        int WingmateSeat(IVesselStatus status) =>
            status != null && _seatOf.TryGetValue(status, out int seat) ? seat : -1;

        /// <summary>Each AI wingmate in seat order, then the pilot's own vessel, with their seats (a party friend's is
        /// their machine's). The order is the launch order: the wingmates leave the hangar first and the pilot last, so the
        /// intro's camera follows the pilot out into play (Shombith 2026-11-17). A wingmate whose seat is not known yet
        /// takes the first seat nobody holds, so it still has a slot.</summary>
        List<(IVesselStatus status, int seat)> FormationSquad()
        {
            _formation.Clear();
            var local = LocalVessel();
            int mine = Mathf.Clamp(_squad.MySeat, 0, SavedSquad.SlotCount - 1);
            int taken = 1 << mine;
            foreach (var w in _wingmates)
            {
                var status = Alive(w);
                if (status == null || !status.Transform || status == local) continue;
                int seat = WingmateSeat(status);
                if (seat >= 0 && (taken & (1 << seat)) == 0) { taken |= 1 << seat; _formation.Add((status, seat)); }
            }
            foreach (var w in _wingmates)
            {
                var status = Alive(w);
                if (status == null || !status.Transform || status == local || WingmateSeat(status) >= 0) continue;
                int free = 0;
                while (free < SavedSquad.SlotCount && (taken & (1 << free)) != 0) free++;
                if (free >= SavedSquad.SlotCount) break;
                taken |= 1 << free;
                _formation.Add((status, free));
            }
            _formation.Sort((a, b) => a.seat.CompareTo(b.seat));
            if (local != null) _formation.Add((local, mine));
            return _formation;
        }

        /// <summary>A wingmate released after the voyage opened: into the hangar with the rest while the intro holds them,
        /// otherwise straight into its slot.</summary>
        void FormUpLate(IVesselStatus status, int seat)
        {
            if (status == null || !Ark || !(_sea && _sea.IsRunning)) return;
            if (_sortie && _sortie.IsHolding && _sortie.Add(status, seat, HullHalfLength, ArkBounds())) return;
            ArkSortie.SetDown(Ark, new[] { (status, seat) }, ArkBounds(), FormationSettings);
        }

        /// <summary>The voyage opens: every seat in its slot.</summary>
        void FormUp()
        {
            _arkBounds = null;
            if (Ark) ArkSortie.SetDown(Ark, FormationSquad(), ArkBounds(), FormationSettings);
        }

        /// <summary>The intro launches the squad: stow it in the hangar. False (the squad stays in its slots) when the
        /// reel has no launch shot or there is nothing to stow.</summary>
        bool StowForLaunch(VoyageIntroSO script)
        {
            if (!script || !script.LaunchesSquad || !Ark) return false;
            _sortie = ArkSortie.Stow(_root, Ark, FormationSquad(), HullHalfLength, ArkBounds(), FormationSettings);
            return _sortie;
        }

        void LaunchSquad(Transform hull)
        {
            if (_sortie) _sortie.Launch(hull);
        }

        void ReleaseSquad()
        {
            if (_sortie) _sortie.Release();
            _sortie = null;
        }

        /// <summary>Where a straying wingmate is brought back to: its seat's slot.</summary>
        bool TryWingmateSlot(IVesselStatus status, out Pose pose)
        {
            int seat = WingmateSeat(status);
            if (seat < 0 || !Ark)
            {
                pose = default;
                return false;
            }
            pose = ArkFormation.SlotPose(Ark, seat, ArkBounds(), FormationSettings);
            return true;
        }
    }
}
