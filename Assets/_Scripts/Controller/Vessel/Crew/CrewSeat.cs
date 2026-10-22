using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A second seat on a hull: what makes a hull a CREW vessel (one hull class, one squad slot, two subvessels a human
    /// and an AI, or two humans, fly apart). The two shipped seats are <see cref="KiteNavigator"/> (the Wrasse on the
    /// Grouper, card "Wrasse and Grouper") and <c>GobyPistolDuo</c> (the Pistol on the Goby, card "Goby and Pistol").
    /// The contract every crew vessel keeps, and the shared pieces it is built from, are in
    /// <c>.claude/skills/duo-vessel/SKILL.md</c> ("One notion of a crew vessel"); <c>Tools/Build/check_crew_vessels.py</c>
    /// holds every hull that carries a seat to it (its class card names both subvessels, it is on the Vessel Changer's
    /// roster and in the prefab container).
    /// </summary>
    public interface ICrewSeat
    {
        /// <summary>The seat's subvessel, as its card names it (the second of <c>SO_Vessel.CrewNames</c>'s pair or the first:
        /// "Wrasse", "Pistol").</summary>
        string SeatName { get; }

        /// <summary>True while the local human flies this seat, and the hull's own AI flies the hull.</summary>
        bool HumanInSeat { get; }
    }

    /// <summary>
    /// A crew seat that can hand its human back to the hull seat when something else parks the hull on its AI (the app
    /// shell, the mode preview's exit and ghost demo, the end-of-match flourish). Once a seat has taken the hull, a menu's
    /// park looks exactly like the seat's own (input paused, autopilot on), so the seat cannot tell them apart and kept
    /// reading the touchscreen and pad under the menu (Garrett 2026-10-21: "the wrasse is controllable after going back to
    /// the app shell in freestyle"). The parker says so instead, through <see cref="CrewSeat.ReturnHumansToHull"/>.
    /// </summary>
    public interface IHullParkAware
    {
        /// <summary>The human leaves this seat for the hull's seat WITHOUT the seat stopping the autopilot it started (the
        /// caller keeps it on); the seat's wake rule then unbinds it. The remembered seat choice is left as it is, so the
        /// next freestyle entry seats the person where they were.</summary>
        void ReturnHumanToHull();
    }

    /// <summary>
    /// The seat rules every crew vessel shares, in one place so the seats cannot drift apart (they had: the Kite paused
    /// the player's input through its <c>InputController</c>, the Goby through its <c>InputStatus</c>, in the opposite
    /// order). Engine state only through the hull and the player: no seat-specific knowledge.
    /// </summary>
    public static class CrewSeat
    {
        /// <summary>
        /// A seat wakes only on the local pilot's own hull while a human flies it, or while this seat has handed the hull
        /// to its AI (<paramref name="weTookHull"/>). Anything else - an AI hull, a remote copy, a hull the Vessel Changer
        /// has just spawned on autopilot, a menu that parked the hull on its AI - leaves the seat idle.
        /// </summary>
        public static bool ShouldWake(Component seat, bool weTookHull, out IVessel vessel)
        {
            vessel = null;
            if (!seat || VesselVisionShading.LocalVessel != seat.transform || !seat.TryGetComponent(out vessel)) return false;
            return weTookHull || vessel.VesselStatus == null || !vessel.VesselStatus.AutoPilotEnabled;
        }

        /// <summary>
        /// The swap is the arena's gesture (<see cref="PilotSwapGesture"/>: D-pad left / right, keys 1 / 2) on the arena's
        /// cooldown (<see cref="PilotSwap.CooldownSeconds"/>, unscaled time). True on a swap this frame.
        /// </summary>
        /// <summary>
        /// Call FIRST wherever a hull is parked on its AI (before the caller's <c>SetPause(true)</c> and
        /// <c>ToggleAIPilot(true)</c>): every crew seat on it sends its human back to the hull's seat and forgets that it
        /// took the hull, so none keeps reading raw input under a menu. A seat's own hand-back may unpause the player's
        /// input for that moment; the caller's pause right after restores it. A null or destroyed hull is skipped.
        /// </summary>
        public static void ReturnHumansToHull(IVessel hull)
        {
            if (hull == null || !UnityLiveness.Alive(hull.Transform)) return;
            foreach (var seat in hull.Transform.GetComponentsInChildren<IHullParkAware>(true)) seat.ReturnHumanToHull();
        }

        public static bool PollSwap(ref float readyAt)
        {
            if (PilotSwapGesture.DirectionThisFrame() == 0 || Time.unscaledTime < readyAt) return false;
            readyAt = Time.unscaledTime + PilotSwap.CooldownSeconds;
            return true;
        }

        /// <summary>
        /// The human moves to the seat: the player's flight input is paused (the sticks and the pointer belong to the
        /// seat), then the hull's own AI flies it - the order the lava lamp and the mode preview hand a hull to its AI in
        /// (MenuCrystalClickHandler, ModePreviewSession). Returns true when THIS call started the autopilot; only that one
        /// is stopped again by <see cref="GiveHullBack"/> (an autopilot something else started is not the seat's to end).
        /// </summary>
        public static bool HandHullToAI(IVessel hull, IPlayer player)
        {
            SetFlightInputPaused(player, true);
            if (hull == null || hull.VesselStatus == null || hull.VesselStatus.AutoPilotEnabled) return false;
            hull.ToggleAIPilot(true);
            return true;
        }

        /// <summary>The human flies the hull again: its autopilot stopped (only when the seat started it), then the
        /// player's input restored - <see cref="HandHullToAI"/> in the opposite order. A destroyed hull is skipped.</summary>
        public static void GiveHullBack(IVessel hull, IPlayer player, bool weTookHull)
        {
            if (weTookHull && hull != null && UnityLiveness.Alive(hull.Transform)) hull.ToggleAIPilot(false);
            SetFlightInputPaused(player, false);
        }

        /// <summary>The input is the PLAYER's, which outlives the hull (a swap re-homes it): its controller when it still
        /// lives, else its input status; a destroyed one is skipped.</summary>
        public static void SetFlightInputPaused(IPlayer player, bool paused)
        {
            if (player == null) return;
            if (UnityLiveness.Alive(player.InputController)) player.InputController.SetPause(paused);
            else if (player.InputStatus != null) player.InputStatus.Paused = paused;
        }
    }
}
