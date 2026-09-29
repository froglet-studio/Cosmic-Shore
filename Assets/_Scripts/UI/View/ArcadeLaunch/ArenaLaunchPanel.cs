using System;
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
    /// can be flown in several, so this panel carries a vessel carousel (one icon, prev / next)
    /// and a SELECT VESSEL button, and Start stays dead until that button has been pressed. The
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

        /// <summary>+1 / -1: the pilot asked for the next / previous vessel.</summary>
        public event Action<int> OnVesselCycleRequested;

        /// <summary>The pilot confirmed the vessel currently shown.</summary>
        public event Action OnVesselConfirmRequested;

        public override bool Handles(SO_ArcadeGame game)
            => game != null && roster != null && roster.Games != null && roster.Games.Contains(game);

        protected override void OnEnable()
        {
            base.OnEnable();
            if (prevButton)         prevButton.onClick.AddListener(RequestPrev);
            if (nextButton)         nextButton.onClick.AddListener(RequestNext);
            if (selectVesselButton) selectVesselButton.onClick.AddListener(RequestConfirm);
        }

        protected override void OnDisable()
        {
            if (prevButton)         prevButton.onClick.RemoveListener(RequestPrev);
            if (nextButton)         nextButton.onClick.RemoveListener(RequestNext);
            if (selectVesselButton) selectVesselButton.onClick.RemoveListener(RequestConfirm);
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
                selectVesselButton.gameObject.SetActive(vessel && !confirmed);
                selectVesselButton.interactable = vessel && !confirmed;
            }
        }

        void RequestPrev()    => OnVesselCycleRequested?.Invoke(-1);
        void RequestNext()    => OnVesselCycleRequested?.Invoke(+1);
        void RequestConfirm() => OnVesselConfirmRequested?.Invoke();
    }
}
