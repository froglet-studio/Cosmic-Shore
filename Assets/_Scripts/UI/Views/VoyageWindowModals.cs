using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Voyage window's top row (Garrett, 2026-11-01: "the episodes and saga are top level above the fold modals that
    /// contain their own content"): SAGA opens the saga's voyages (Tandava and its Severing) and EPISODES opens the
    /// episodes with their quests, each in a modal over the map and the voyage cards. One is open at a time; pressing its
    /// button again, or anywhere outside its panel, closes it. Laid out by Tools/Build/author_voyage_window.py.
    /// </summary>
    public class VoyageWindowModals : MonoBehaviour
    {
        [Header("Top row")]
        [Tooltip("Opens the saga's modal.")]
        [SerializeField] Button sagaButton;
        [Tooltip("Opens the episodes' modal.")]
        [SerializeField] Button episodesButton;

        [Header("Modals")]
        [Tooltip("The saga's voyages, closed until SAGA is pressed.")]
        [SerializeField] GameObject sagaModal;
        [Tooltip("The episodes and their quests, closed until EPISODES is pressed.")]
        [SerializeField] GameObject episodesModal;
        [Tooltip("The dim around the saga's panel: a press there closes it.")]
        [SerializeField] Button sagaClose;
        [Tooltip("The dim around the episodes' panel: a press there closes it.")]
        [SerializeField] Button episodesClose;

        void Awake()
        {
            sagaButton.onClick.AddListener(ToggleSaga);
            episodesButton.onClick.AddListener(ToggleEpisodes);
            sagaClose.onClick.AddListener(Close);
            episodesClose.onClick.AddListener(Close);
        }

        void OnDestroy()
        {
            if (sagaButton) sagaButton.onClick.RemoveListener(ToggleSaga);
            if (episodesButton) episodesButton.onClick.RemoveListener(ToggleEpisodes);
            if (sagaClose) sagaClose.onClick.RemoveListener(Close);
            if (episodesClose) episodesClose.onClick.RemoveListener(Close);
        }

        void ToggleSaga() => Open(sagaModal.activeSelf ? null : sagaModal);
        void ToggleEpisodes() => Open(episodesModal.activeSelf ? null : episodesModal);

        /// <summary>Close whichever modal is open.</summary>
        public void Close() => Open(null);

        void Open(GameObject modal)
        {
            sagaModal.SetActive(modal == sagaModal);
            episodesModal.SetActive(modal == episodesModal);
        }
    }
}
