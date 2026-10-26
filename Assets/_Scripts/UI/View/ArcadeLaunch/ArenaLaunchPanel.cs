using System;
using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The launch surface for an <b>Arena</b> card — a <see cref="MinigameLaunchPanel"/> whose one
    /// difference is that the pilot PICKS A HULL before they can start.
    ///
    /// <para>An arcade card locks to one vessel, so its panel has nothing to ask. An arena card
    /// can be flown in several, so this panel carries a vessel picker (the style-B hull strip, or
    /// the older carousel: one icon, prev / next) and a claim button, and Start stays dead until that button has been pressed. The
    /// choice is per session: it is re-armed every time the card opens, on the host and on every
    /// client alike, because each pilot picks their own hull.</para>
    ///
    /// <para>Same contract as every panel (<c>Docs/ArcadeLaunch/ARCHITECTURE.md</c> §2): the panel
    /// owns the widgets and RAISES; <see cref="ArcadeGameConfigureModal"/> owns the decision —
    /// which vessel is current, whether it is confirmed, and whether Start may fire. Nothing
    /// here writes <c>ArcadeGameConfigSO</c> or the player's vessel type.</para>
    ///
    /// <para>It lives in its OWN window (<see cref="ArcadeLaunchPanel.HostModal"/>) the way the
    /// Maelstrom's does, and it answers <see cref="Handles"/> for the cards in
    /// <see cref="roster"/> — the Arena's <c>SO_GameList</c>. It must sit BEFORE the arcade's
    /// <see cref="MinigameLaunchPanel"/> in the modal's list, because that panel accepts every
    /// non-Maelstrom card and first match wins.</para>
    /// </summary>
    public class ArenaLaunchPanel : MinigameLaunchPanel
    {
        [Header("Arena")]
        [SerializeField, Tooltip("The Arena roster. This panel draws exactly the cards in this list.")]
        SO_GameList roster;

        [Header("Vessel picker")]
        [SerializeField, Tooltip("Shows the current vessel's IconActive.")]
        Image vesselIcon;

        [SerializeField, Tooltip("Steps the carousel backwards.")]
        Button prevButton;

        [SerializeField, Tooltip("Steps the carousel forwards.")]
        Button nextButton;

        [SerializeField, Tooltip("Confirms the shown vessel. Hidden once pressed; Start is dead " +
                                 "until it has been.")]
        Button selectVesselButton;

        [Header("Hull strip (optional)")]
        [SerializeField, Tooltip("The tiles of the hull strip, left to right (the style-B configure tree). " +
                                 "They show a window onto the card's hulls in roster order: a card with more " +
                                 "hulls than tiles slides the window to keep the current pick in view, so the " +
                                 "prev / next arrows reach every hull. A tap shows that hull; a hull another " +
                                 "pilot holds is drawn locked and refuses. Surplus tiles are hidden. Optional: " +
                                 "the carousel arrows work without it.")]
        List<KitHullTile> hullTiles = new();

        [Header("Shared vessel panel")]
        [SerializeField, Tooltip("The vessel section's caption. It names whose hull the panel is picking: the " +
                                 "pilot's own, or the AI seat they tapped. Optional.")]
        TMPro.TMP_Text seatCaptionText;

        [SerializeField, Tooltip("The caption while the panel picks the pilot's own hull. Empty: no caption.")]
        string ownSeatCaption = "Your vessel";

        [SerializeField, Tooltip("The caption while the panel picks an AI seat's hull. {domain} is the AI's team. " +
                                 "Empty: no caption.")]
        string aiSeatCaption = "{domain} AI's vessel";

        [SerializeField, Tooltip("What the vessel section names when the selected AI has no hull yet (dealt at " +
                                 "launch from the hulls left). Empty: nothing.")]
        string autoVesselLine = "Auto";

        [SerializeField, Tooltip("The vessel section's own hull labels (the section value, the class name). While " +
                                 "an AI seat is selected only these follow its hull; the pilot's summary chips, " +
                                 "FLY row and controls keep naming the pilot's own.")]
        List<TMPro.TMP_Text> seatVesselNameTexts = new();

        // The card hull the leftmost tile shows (StripOffset).
        int _stripOffset;

        /// <summary>+1 / -1: the pilot asked for the next / previous vessel.</summary>
        public event Action<int> OnVesselCycleRequested;

        /// <summary>The pilot tapped hull tile <c>index</c> (an index into the card's vessel list).</summary>
        public event Action<int> OnVesselPickRequested;

        /// <summary>The pilot confirmed the vessel currently shown.</summary>
        public event Action OnVesselConfirmRequested;

        public override bool Handles(SO_ArcadeGame game)
            => game != null && roster != null && roster.Games != null && roster.Games.Contains(game);

        public override void Bind(SO_ArcadeGame game, int intensity)
        {
            _controlsVessel = null;
            _stripOffset = 0;
            base.Bind(game, intensity);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (prevButton)         prevButton.onClick.AddListener(RequestPrev);
            if (nextButton)         nextButton.onClick.AddListener(RequestNext);
            if (selectVesselButton) selectVesselButton.onClick.AddListener(RequestConfirm);
            for (int i = 0; i < hullTiles.Count; i++)
                if (hullTiles[i]) hullTiles[i].Bind(i, RequestTile);
        }

        protected override void OnDisable()
        {
            if (prevButton)         prevButton.onClick.RemoveListener(RequestPrev);
            if (nextButton)         nextButton.onClick.RemoveListener(RequestNext);
            if (selectVesselButton) selectVesselButton.onClick.RemoveListener(RequestConfirm);
            foreach (var tile in hullTiles)
                if (tile) tile.Bind(-1, null);
            base.OnDisable();
        }

        /// <summary>
        /// Draw the picker. <paramref name="canCycle"/> is false when there is nothing to step
        /// through (one hull, or none); <paramref name="confirmed"/> hides the Select button and
        /// freezes the arrows for the rest of the session.
        /// </summary>
        public void ShowVessel(SO_Vessel vessel, bool canCycle, bool confirmed)
        {
            if (vesselIcon)
            {
                vesselIcon.sprite  = vessel ? vessel.IconActive : null;
                vesselIcon.enabled = vessel;
            }

            bool arrows = vessel && canCycle && !confirmed;
            if (prevButton) prevButton.interactable = arrows;
            if (nextButton) nextButton.interactable = arrows;

            if (selectVesselButton)
            {
                SetPartShown(selectVesselButton.gameObject, vessel && !confirmed);
                selectVesselButton.interactable = vessel && !confirmed;
            }

            DrawVessel(vessel);
            if (seatCaptionText) seatCaptionText.text = TemplateText.Fill(ownSeatCaption);

            // The controls block describes the hull this pilot will fly, so it follows the strip.
            if (vessel && vessel != _controlsVessel)
            {
                _controlsVessel = vessel;
                ShowControlsFor(vessel);
            }
        }

        SO_Vessel _controlsVessel;

        /// <summary>
        /// Draw an AI seat's hull in the shared vessel panel (the pilot tapped that seat): the caption names
        /// the seat, the section's own labels and description show the hull (<see cref="autoVesselLine"/> when
        /// none is picked yet), the arrows step it when <paramref name="canCycle"/>, and the claim button stands
        /// aside - an AI's pick lands as it is made. The pilot's own summary, FLY row and controls are untouched.
        /// </summary>
        public void ShowSeatVessel(SO_Vessel vessel, string seatDomain, bool canCycle)
        {
            if (seatCaptionText) seatCaptionText.text = TemplateText.Fill(aiSeatCaption, "domain", seatDomain);

            if (vesselIcon)
            {
                vesselIcon.sprite  = vessel ? vessel.IconActive : null;
                vesselIcon.enabled = vessel;
            }

            if (prevButton) prevButton.interactable = canCycle;
            if (nextButton) nextButton.interactable = canCycle;

            if (selectVesselButton)
            {
                SetPartShown(selectVesselButton.gameObject, false);
                selectVesselButton.interactable = false;
            }

            string name = vessel ? vessel.Name : TemplateText.Fill(autoVesselLine);
            foreach (var label in seatVesselNameTexts)
                if (label) label.text = name;
            if (vesselDescriptionText) vesselDescriptionText.text = vessel ? vessel.Description : string.Empty;
        }

        /// <summary>
        /// Draw the hull strip: the tiles show a window onto <paramref name="ships"/> that keeps
        /// <paramref name="shown"/> in view (selected), a hull <paramref name="heldByOthers"/> names drawn
        /// locked. Once the pick is <paramref name="confirmed"/> the strip stops taking taps. A panel
        /// without tiles ignores it.
        /// </summary>
        public void ShowHullStrip(IReadOnlyList<SO_Vessel> ships, int shown, Func<SO_Vessel, bool> heldByOthers,
                                  bool confirmed)
        {
            int count = ships?.Count ?? 0;
            _stripOffset = StripOffset(_stripOffset, shown, count, hullTiles.Count);
            for (int i = 0; i < hullTiles.Count; i++)
            {
                var tile = hullTiles[i];
                if (!tile) continue;
                int index = _stripOffset + i;
                var ship = index < count ? ships[index] : null;
                tile.Show(ship, index == shown, ship && heldByOthers != null && heldByOthers(ship), !confirmed);
            }
        }

        /// <summary>
        /// The card hull the leftmost of <paramref name="tiles"/> tiles shows: unchanged while
        /// <paramref name="shown"/> is in view, slid just far enough to bring it in otherwise, and never past
        /// either end. Zero when every hull fits. The Regatta card lists nine hulls on an eight-tile strip,
        /// and with a fixed strip the ninth (the Stoat) had no tile and could never be picked (2026-10-25).
        /// </summary>
        public static int StripOffset(int offset, int shown, int count, int tiles)
        {
            if (tiles <= 0 || count <= tiles) return 0;
            if (shown >= 0 && shown < count)
            {
                if (shown < offset) offset = shown;
                else if (shown >= offset + tiles) offset = shown - tiles + 1;
            }
            return Mathf.Clamp(offset, 0, count - tiles);
        }

        void RequestPrev()    => OnVesselCycleRequested?.Invoke(-1);
        void RequestNext()    => OnVesselCycleRequested?.Invoke(+1);
        void RequestConfirm() => OnVesselConfirmRequested?.Invoke();
        void RequestTile(int tile) => OnVesselPickRequested?.Invoke(_stripOffset + tile);
    }
}
