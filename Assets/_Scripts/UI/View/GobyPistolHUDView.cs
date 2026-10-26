using System;
using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Goby &amp; Pistol's HUD: one four-card row (charge, mass, space, time) that each seat draws its own way
    /// (Garrett, 2026-10-24: "give both of its seats separate UI ability icons, glyphs, etc."). Every card carries two
    /// icons, the Goby's and the Pistol's, and <see cref="SetSeat"/> shows the one for the seat the person flies:
    /// <list type="bullet">
    /// <item><b>Charge</b>: the Goby's Lock (four brackets round a target), or the Pistol's Snap (a vortex ring leaving
    /// the claw).</item>
    /// <item><b>Mass</b>: the Goby's perch (the pad and the den it starts), or the Pistol's den and its way back.</item>
    /// <item><b>Space</b>: the undertow seen from above (the Goby's view), or the Pistol's ghost (its outline,
    /// dashed).</item>
    /// <item><b>Time</b>: the Goby's dart, or the Pistol's dive down a hill.</item>
    /// </list>
    /// The controller (<see cref="GobyPistolHUDController"/>) also re-seeds each card's control chip, presses, veils and
    /// counts per seat. Two gauges are this view's: the Time card's (the dart's charge, or the Pistol's speed) and the
    /// Space card's (how far the undertow reaches, the same pull in both seats). The lockup adopts both through each
    /// binding's <c>gauge</c>; this view writes them through its own fields (both are needed, the Urchin's lesson).
    /// <para>No icon here is an upgrade sprite: the level-5 signal is the card's own rim and bloom, so swapping an
    /// icon's sprite never fights <see cref="VesselHUDView.SetAbilityUpgraded"/>, which writes a sprite only where one
    /// is authored.</para>
    /// </summary>
    public sealed class GobyPistolHUDView : VesselHUDView
    {
        [Serializable]
        public struct SeatIcon
        {
            [Tooltip("The card these two icons are drawn on.")]
            public Element element;
            [Tooltip("The card's icon while the person flies the Goby.")]
            public Sprite goby;
            [Tooltip("The card's icon while the person flies the Pistol.")]
            public Sprite pistol;
        }

        public const int GobySeat = 0, PistolSeat = 1;

        [Header("Each seat's icons")]
        [Tooltip("Per card, the Goby's icon and the Pistol's. An empty sprite keeps the card's authored icon.")]
        [SerializeField] List<SeatIcon> seatIcons = new();

        [Header("Gauges")]
        [Tooltip("The Time card's gauge: the Goby's dart charge while it holds Dart, or the Pistol's speed.")]
        [SerializeField] Image timeGauge;
        [Tooltip("The Space card's gauge: how far the undertow reaches (Space's level), in both seats.")]
        [SerializeField] Image spaceGauge;
        [Tooltip("A gauge's colour as it fills.")]
        [SerializeField] Color gaugeColor = new(0.22f, 0.51f, 1f, 0.55f);
        [Tooltip("A full gauge's colour.")]
        [SerializeField] Color gaugeFullColor = new(0.96f, 0.96f, 1f, 0.5f);

        /// <summary>The seat whose icons the cards show: <see cref="GobySeat"/> or <see cref="PistolSeat"/>; -1 before the first.</summary>
        public int Seat { get; private set; } = -1;

        public override void Initialize()
        {
            Seat = -1;
            Fill(timeGauge, 0f, force: true);
            Fill(spaceGauge, 0f, force: true);
            SetSeat(GobySeat);
        }

        /// <summary>Shows <paramref name="seat"/>'s icon on every card.</summary>
        public void SetSeat(int seat)
        {
            if (seat == Seat) return;
            Seat = seat;
            foreach (var pair in seatIcons)
            {
                var sprite = seat == PistolSeat ? pair.pistol : pair.goby;
                if (!sprite || !TryGetAbilityIcon(pair.element, out var icon) || !icon) continue;
                icon.sprite = sprite;
            }
        }

        /// <summary>True when every card has both seats' icons (the generator's and the tests' check).</summary>
        public bool HasBothSeatIcons(Element element)
        {
            foreach (var pair in seatIcons)
                if (pair.element == element) return pair.goby && pair.pistol && pair.goby != pair.pistol;
            return false;
        }

        public void SetTimeGauge(float value01) => Fill(timeGauge, value01, force: false);
        public void SetSpaceGauge(float value01) => Fill(spaceGauge, value01, force: false);

        void Fill(Image gauge, float value01, bool force)
        {
            if (!gauge) return;
            value01 = Mathf.Clamp01(value01);
            if (!force && Mathf.Abs(gauge.fillAmount - value01) < 0.002f) return;
            gauge.fillAmount = value01;
            gauge.color = Color.Lerp(gaugeColor, gaugeFullColor, value01);
        }
    }
}
