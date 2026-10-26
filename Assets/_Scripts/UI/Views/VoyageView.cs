using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Voyage window's featured voyage (Rev 13, Docs/MENU_REDESIGN_B.md §69; Shombith 2026-10-25: "the episodes and quest
    /// tracker moves into the voyages ... and we can lead onto voyages (specific missions like the tandava)"). The window
    /// replaced the Mission window: the episode card and the quest track on the left, the voyages on the right.
    ///
    /// <para>A voyage is an ordinary arcade card (<see cref="SO_ArcadeGame"/>) that no grid lists. This draws its art, name and
    /// description from the card and Set sail opens its launch window through the ONE configure modal
    /// (<see cref="ArcadeGameConfigureModal.OpenFor"/>), which picks the panel the card needs (Tandava seats several hulls,
    /// so it is the arena launch panel with its vessel carousel). Everything the player reads is the card's data or text
    /// authored on the prefab.</para>
    /// </summary>
    public class VoyageView : MonoBehaviour
    {
        [Header("Voyage")]
        [Tooltip("The featured voyage's card (Tandava). It is on the master roster but on no grid.")]
        [SerializeField] SO_ArcadeGame voyage;
        [Tooltip("The configure modal that launches it. Found in the scene when left empty (the window is a prefab and " +
                 "cannot name the scene's modal).")]
        [SerializeField] ArcadeGameConfigureModal configureModal;

        [Header("Drawn from the card")]
        [SerializeField] Image art;
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text descriptionText;

        [Header("Controls")]
        [Tooltip("Set sail: opens the voyage's launch window.")]
        [SerializeField] Button setSailButton;

        void Awake()
        {
            if (setSailButton) setSailButton.onClick.AddListener(SetSail);
        }

        void OnDestroy()
        {
            if (setSailButton) setSailButton.onClick.RemoveListener(SetSail);
        }

        void OnEnable() => Draw();

        /// <summary>Puts the card's art, name and description on the featured voyage.</summary>
        public void Draw()
        {
            if (!voyage) return;
            if (art && voyage.CardBackground) art.sprite = voyage.CardBackground;
            if (titleText) titleText.text = voyage.DisplayName;
            if (descriptionText) descriptionText.text = voyage.Description;
        }

        /// <summary>Opens the voyage's launch window over this one; Back returns here.</summary>
        public void SetSail()
        {
            if (!voyage) return;
            if (!configureModal) configureModal = FindAnyObjectByType<ArcadeGameConfigureModal>(FindObjectsInactive.Include);
            if (configureModal) configureModal.OpenFor(voyage);
            else CSDebug.LogWarning("[VoyageView] Set sail found no ArcadeGameConfigureModal in the scene; the voyage cannot launch.");
        }
    }
}
