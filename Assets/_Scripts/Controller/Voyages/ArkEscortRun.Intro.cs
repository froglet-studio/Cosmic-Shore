using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The escort's opening (<see cref="VoyageIntro"/>, Shombith 2026-11-16, Milestone 1): the moment the Ark sets sail the
    /// intro shows the Ark, the squad and the course, and the departure banner follows it. Meanwhile the pilot's own
    /// vessel flies on the escort AI with the stick held, so it keeps with the Ark and is beside it when the stick comes
    /// back; the hand-over returns it to the pilot exactly once, however the intro ends. The escort sim (every seat AI)
    /// has no intro.
    /// </summary>
    public sealed partial class ArkEscortRun
    {
        VoyageIntro _intro;
        ArkEscortPilot _introPilot;
        bool _holdingForIntro;

        void BeginIntro()
        {
            var script = _aiLocal ? null : VoyageIntroSO.LoadEscort();
            var local = LocalVessel();
            if (!script || local == null || !Ark)
            {
                // The sim (every seat AI) has no intro by design; anything else is a fault, said once with its reason.
                if (!_aiLocal)
                    CSDebug.LogWarning("[ArkEscort] The escort opens without its intro: " +
                                       (!script ? $"nothing at Resources/{VoyageIntroSO.EscortResourcePath}"
                                           : local == null ? "no live local vessel" : "no Ark") + ".");
                Depart();
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
                int seat = _seatOf.TryGetValue(status, out int seated) ? seated : -1;
                wingmates.Add(new VoyageIntro.Member
                {
                    Hull = status.Transform, Vessel = status.VesselType, Seat = seat,
                    Slot = seat >= 0 ? _squad.GetSlot(seat) : new SquadSlot(status.VesselType, default),
                });
            }
            // The party's other pilots, in the seats the host gave them (Docs/Voyages/PARTY.md).
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

            var names = new List<string>();
            foreach (var leg in _escort.Legs)
                if (leg != null && !string.IsNullOrEmpty(leg.Name)) names.Add(leg.Name);
            cast.Tokens.AddRange(new[]
            {
                "voyage", _escort.VoyageName ?? string.Empty,
                "difficulty", _difficulty.ToString(),
                "legs", _escort.Legs.Count.ToString(),
                "worlds", string.Join(script.WorldSeparator, names),
                "first", names.Count > 0 ? names[0] : string.Empty,
            });

            HoldForIntro(local, mySeat);
            _intro = VoyageIntro.Play(script, cast, _root, EndIntro);
        }

        /// <summary>The pilot's vessel keeps with the Ark on the escort AI while the camera is away; the stick is held.</summary>
        void HoldForIntro(IVesselStatus local, int seat)
        {
            _holdingForIntro = true;
            var input = _context?.GameData?.LocalPlayer?.InputController;
            if (input) input.SetPause(true);
            if (local.Vessel == null) return;
            local.Vessel.ToggleAIPilot(true);
            Escort(local.Vessel, seat);
            if (local is Component c && c) _introPilot = c.GetComponent<ArkEscortPilot>();
        }

        /// <summary>The intro is over (played, skipped or stopped): the pilot has the stick, then the voyage is named.</summary>
        void EndIntro()
        {
            _intro = null;
            if (!_holdingForIntro) return;
            _holdingForIntro = false;

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
            Depart();
        }

        void StopIntro()
        {
            if (_intro) _intro.Stop();
            else EndIntro();
        }
    }
}
