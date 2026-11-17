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

        /// <summary>The pilot's own vessel and each AI wingmate, with their seats (a party friend's is their machine's).</summary>
        List<(IVesselStatus status, int seat)> FormationSquad()
        {
            _formation.Clear();
            var local = LocalVessel();
            if (local != null) _formation.Add((local, Mathf.Clamp(_squad.MySeat, 0, SavedSquad.SlotCount - 1)));
            foreach (var w in _wingmates)
            {
                var status = Alive(w);
                if (status == null || !status.Transform) continue;
                int seat = WingmateSeat(status);
                if (seat >= 0) _formation.Add((status, seat));
            }
            return _formation;
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

        void LaunchSquad()
        {
            if (_sortie) _sortie.Launch();
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
