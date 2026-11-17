using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Crystal Run's opening (Shombith, 2026-11-16: "the in game still has no intro"): the escort's reel
    /// (<see cref="VoyageIntro"/>) with Crystal Run's words (<see cref="VoyageIntroSO.LoadCrystalRun"/>), played when the Ark
    /// first sets sail on a run. As in the escort (<c>ArkEscortRun.Intro.cs</c>), the pilot's own vessel flies on the escort
    /// AI with the stick held while the camera is away - or, when the reel launches the squad, waits in the Ark's hangar and
    /// flies out to its slot beside the Ark (<see cref="ArkSortie"/>) - and the hand-over returns it exactly once however
    /// the intro ends.
    /// The run's set-off toast waits for it (<see cref="ArkwayRun.HoldBanners"/>). A friend following the host's course
    /// sees it too, with the party's other pilots in the cast (Docs/Voyages/PARTY.md); a run that sails on from a stop has none.
    /// </summary>
    public sealed partial class VoyageRun
    {
        VoyageIntro _intro;
        ArkEscortPilot _introPilot;
        bool _introPending, _holdingForIntro;

        /// <summary>Called as a job is taken: the intro plays when this leg gets under way.</summary>
        void ArmIntro(VoyageJob job, bool firstLeg) => _introPending = firstLeg && job is { CountsUnderway: true };

        void BeginIntro()
        {
            if (!_introPending) return;
            _introPending = false;
            var job = _state?.Current;
            var script = VoyageIntroSO.LoadCrystalRun();
            var local = LocalVessel();
            if (!script)
            {
                CSDebug.LogWarning($"[Voyage] No intro at Resources/{VoyageIntroSO.CrystalRunResourcePath} - the run opens without one.");
                return;
            }
            if (local == null || !Ark || job == null)
            {
                CSDebug.LogWarning("[Voyage] The run's intro has no " + (local == null ? "local vessel" : !Ark ? "Ark" : "job") +
                                   " to show - the run opens without one.");
                return;
            }

            int mySeat = Mathf.Clamp(_squad.MySeat, 0, SavedSquad.SlotCount - 1);
            var cast = new VoyageIntro.Cast { Ark = Ark, ArkSize = HullHalfLength };
            cast.Squad.Add(new VoyageIntro.Member
            {
                Hull = local.Transform, Vessel = local.VesselType, Seat = mySeat, IsYou = true, Slot = _squad.GetSlot(mySeat),
            });
            var wingmates = new List<VoyageIntro.Member>();
            foreach (var w in _wingmates)
            {
                var status = Alive(w);
                if (status == null || !status.Transform) continue;
                int seat = status is Component c && c && c.TryGetComponent(out ArkEscortPilot p) ? p.Seat : -1;
                wingmates.Add(new VoyageIntro.Member
                {
                    Hull = status.Transform, Vessel = status.VesselType, Seat = seat,
                    Slot = seat >= 0 ? _squad.GetSlot(seat) : new SquadSlot(status.VesselType, default),
                });
            }
            // The party's other pilots, in the seats the host gave them.
            foreach (var (status, client) in OtherHumans())
            {
                int seat = SeatOfClient(client);
                wingmates.Add(new VoyageIntro.Member
                {
                    Hull = status.Transform, Vessel = status.VesselType, Seat = seat, Friend = status.PlayerName,
                    Slot = VoyagePartySeats.Of((ulong)Mathf.Max(0, client))?.Slot ?? new SquadSlot(status.VesselType, default),
                });
            }
            wingmates.Sort((a, b) => a.Seat.CompareTo(b.Seat));
            cast.Squad.AddRange(wingmates);

            cast.Tokens.AddRange(new[]
            {
                "goal", Mathf.CeilToInt(job.Goal).ToString(),
                "to", PlaceName(job.To) ?? string.Empty,
                "breach", Mathf.RoundToInt(job.BreachHull * 100f).ToString(),
            });

            HoldForIntro(local, mySeat, script);
            cast.Launch = LaunchSquad;
            _intro = VoyageIntro.Play(script, cast, _root, EndIntro);
        }

        /// <summary>The stick is held while the camera is away. The squad waits in the Ark's hangar for the reel's launch
        /// shot and forms up beside the Ark (<see cref="ArkSortie"/>); a reel with no launch keeps the pilot's vessel with
        /// the Ark on the escort AI instead.</summary>
        void HoldForIntro(IVesselStatus local, int seat, VoyageIntroSO script)
        {
            _holdingForIntro = true;
            var input = _context?.GameData?.LocalPlayer?.InputController;
            if (input) input.SetPause(true);
            if (StowForLaunch(script)) return;
            if (local.Vessel == null) return;
            local.Vessel.ToggleAIPilot(true);
            Escort(local.Vessel, seat);
            if (local is Component c && c) _introPilot = c.GetComponent<ArkEscortPilot>();
        }

        /// <summary>The intro is over (played, skipped or stopped): the pilot has the stick back.</summary>
        void EndIntro()
        {
            _intro = null;
            if (!_holdingForIntro) return;
            _holdingForIntro = false;

            // The squad flies on from its slots (a launch the intro cut short finishes there first).
            ReleaseSquad();
            if (_introPilot)
            {
                _introPilot.Disarm();
                _pilots.Remove(_introPilot);
                Destroy(_introPilot);
            }
            _introPilot = null;

            var local = LocalVessel();
            if (local?.Vessel != null && local.AutoPilotEnabled) local.Vessel.ToggleAIPilot(false);
            var input = _context?.GameData?.LocalPlayer?.InputController;
            if (input) input.SetPause(false);
        }

        void StopIntro()
        {
            _introPending = false;
            if (_intro) _intro.Stop();
            else EndIntro();
        }
    }
}
