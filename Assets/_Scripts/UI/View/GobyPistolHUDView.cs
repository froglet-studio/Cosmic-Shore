using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
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
    /// counts per seat. Three gauges are this view's: the Time card's (the dart's charge, or the Pistol's speed), the
    /// Space card's (how far the undertow reaches, the same pull in both seats) and the Mass card's (the Pistol's energy,
    /// Garrett 2026-10-29: "the energy should be shown on its mass icon"). The lockup adopts each through its binding's
    /// <c>gauge</c>; this view writes them through its own fields (both are needed, the Urchin's lesson).
    /// <para>Level 5: the card's rim and bloom mark every upgrade, and a seat whose act the upgrade changes also shows
    /// its level-5 picture (<see cref="SeatIcon.gobyUpgraded"/> / <see cref="SeatIcon.pistolUpgraded"/>; the fleet pass,
    /// 2026-11-13): Vortex Steal and Fortress on the Pistol's Charge and Mass cards, Deep Undertow and Phase Dart on the
    /// Goby's Space and Time cards. The bindings carry no <c>upgradedSprite</c>, so the base view never writes a sprite;
    /// this view picks the seat's picture for the card's state in one place (<see cref="Repaint"/>).</para>
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
            [Tooltip("The Goby's icon once this card's level-5 upgrade is active. Empty keeps the Goby's card.")]
            public Sprite gobyUpgraded;
            [Tooltip("The Pistol's icon once this card's level-5 upgrade is active. Empty keeps the Pistol's card.")]
            public Sprite pistolUpgraded;
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
        [Tooltip("The Mass card's gauge: the Pistol's energy while the person flies it (the blocks it flew over gave it; its " +
                 "next den is built from it). Empty while they fly the Goby.")]
        [SerializeField] Image massGauge;
        [Tooltip("A gauge's colour as it fills.")]
        [SerializeField] Color gaugeColor = new(0.22f, 0.51f, 1f, 0.55f);
        [Tooltip("A full gauge's colour.")]
        [SerializeField] Color gaugeFullColor = new(0.96f, 0.96f, 1f, 0.5f);

        /// <summary>The seat whose icons the cards show: <see cref="GobySeat"/> or <see cref="PistolSeat"/>; -1 before the first.</summary>
        public int Seat { get; private set; } = -1;

        public override void Initialize()
        {
            Seat = -1;
            _upgradedCards.Clear();
            Fill(timeGauge, 0f, force: true);
            Fill(spaceGauge, 0f, force: true);
            Fill(massGauge, 0f, force: true);
            SetSeat(GobySeat);
        }

        /// <summary>Shows <paramref name="seat"/>'s icon on every card.</summary>
        public void SetSeat(int seat)
        {
            if (seat == Seat) return;
            Seat = seat;
            foreach (var pair in seatIcons) Repaint(pair);
        }

        readonly HashSet<Element> _upgradedCards = new();

        /// <summary>The base marks the card (rim, bloom, punch); this view then shows the seat's level-5 picture, if it
        /// has one, or its card again on a re-lock.</summary>
        public override void SetAbilityUpgraded(Element element, bool upgraded)
        {
            base.SetAbilityUpgraded(element, upgraded);
            if (upgraded) _upgradedCards.Add(element);
            else _upgradedCards.Remove(element);
            foreach (var pair in seatIcons)
                if (pair.element == element) Repaint(pair);
        }

        /// <summary>The card's sprite for the seat flown now and the card's level-5 state.</summary>
        void Repaint(SeatIcon pair)
        {
            bool pistol = Seat == PistolSeat, upgraded = _upgradedCards.Contains(pair.element);
            var sprite = pistol ? pair.pistol : pair.goby;
            var up = pistol ? pair.pistolUpgraded : pair.gobyUpgraded;
            if (upgraded && up) sprite = up;
            if (!sprite || !TryGetAbilityIcon(pair.element, out var icon) || !icon) return;
            icon.sprite = sprite;
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
        public void SetMassGauge(float value01) => Fill(massGauge, value01, force: false);

        void OnEnable()
        {
            ColorTrace.Changed += RepaintGauges;
            RepaintGauges();
        }

        void OnDisable() => ColorTrace.Changed -= RepaintGauges;

        // Every gauge at its fill in the colour set worn now (another was worn, or one was worn while this was hidden): a
        // gauge whose fill does not move is not written again by the controller's pushes.
        void RepaintGauges()
        {
            if (timeGauge) Fill(timeGauge, timeGauge.fillAmount, force: true);
            if (spaceGauge) Fill(spaceGauge, spaceGauge.fillAmount, force: true);
            if (massGauge) Fill(massGauge, massGauge.fillAmount, force: true);
        }

        void Fill(Image gauge, float value01, bool force)
        {
            if (!gauge) return;
            value01 = Mathf.Clamp01(value01);
            if (!force && Mathf.Abs(gauge.fillAmount - value01) < 0.002f) return;
            gauge.fillAmount = value01;
            // Both ends as the worn colour set paints them (ColorTrace).
            gauge.color = Color.Lerp(ColorTrace.Retrace(ColorTrace.Ui, gaugeColor),
                                     ColorTrace.Retrace(ColorTrace.Ui, gaugeFullColor), value01);
        }
    }
}
