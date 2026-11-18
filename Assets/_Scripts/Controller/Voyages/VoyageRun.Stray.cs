using CosmicShore.Data;
using CosmicShore.UI;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Straying from the Ark (Shombith, 2026-11-17: "when the player goes far beyond the ark there should be the teleporting
    /// feature but only with a warning 5 4 3 2 1"). The rule is the Arkway's leash (<see cref="ArkwayRun"/>), its radius and
    /// countdown set from Voyage.asset (<c>strayRadiusCells</c>, <c>strayCountdownSeconds</c>); the voyage gives it three
    /// things: the countdown's words (a threat toast that counts down, naming the crystals at stake), the place a recalled
    /// pilot lands (their own formation seat beside the Ark, <see cref="ArkFormation"/>), and the cost: every crystal they
    /// carry unbanked is lost, gone rather than spilled into the sea.
    /// </summary>
    public sealed partial class VoyageRun
    {
        void HookStray(bool on)
        {
            if (!_sea) return;
            _sea.Recalled -= OnStrayRecalled;
            _sea.LeashCountdownText = null;
            _sea.RecallPose = null;
            if (!on) return;
            _sea.Recalled += OnStrayRecalled;
            _sea.LeashCountdownText = StrayCountdownText;
            _sea.RecallPose = StrayRecallPose;
        }

        /// <summary>The countdown toast for <paramref name="seconds"/> left: the line under it names the crystals at stake
        /// when there are any.</summary>
        (string line, string under) StrayCountdownText(int seconds)
        {
            var lines = Lines;
            if (lines == null) return (string.Empty, string.Empty);
            string s = seconds.ToString();
            int carried = StrayCarried();
            string under = carried > 0
                ? TemplateText.Fill(lines.ToastStrayCarrySub, "carried", carried.ToString(), "seconds", s)
                : TemplateText.Fill(lines.ToastStraySub, "seconds", s);
            return (TemplateText.Fill(lines.ToastStray, "seconds", s), under);
        }

        /// <summary>The pilot's own formation seat beside the Ark (seat 1 to port, 2 to starboard, 3 over its back).</summary>
        Pose? StrayRecallPose(IVesselStatus status)
        {
            var ark = Ark;
            if (!ark || _squad == null) return null;
            int seat = Mathf.Clamp(_squad.MySeat, 0, SavedSquad.SlotCount - 1);
            return ArkFormation.SlotPose(ark, seat, ArkBounds(), FormationSettings);
        }

        int StrayCarried()
        {
            var local = LocalVessel();
            return local != null && Raiding && CrystalsAtRisk ? _raid.CarriedBy(local.PlayerName) : 0;
        }

        /// <summary>The countdown ran out: what the pilot carried is lost, and a toast says so.</summary>
        void OnStrayRecalled(IVesselStatus status)
        {
            if (!_active || status == null) return;
            int lost = 0;
            if (Raiding && CrystalsAtRisk && _raid.CarriedBy(status.PlayerName) > 0)
            {
                var spilled = _raid.Spill(status.PlayerName);
                foreach (var e in VoyageRaid.Elements) lost += Mathf.Max(0, spilled[(int)e]);
            }
            var lines = Lines;
            if (lines == null) return;
            VoyageToasts.Show(_sea, ArkwayVoyageHud.Mood.Threat, lines.ToastRecalled,
                lost > 0 ? TemplateText.Fill(lines.ToastRecalledLostSub, "count", lost.ToString()) : lines.ToastRecalledSub);
        }
    }
}
