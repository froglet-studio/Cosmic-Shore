using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// A voyage on the Voyage window that is sailed in freestyle rather than launched as an arcade card: the Voyage toy
    /// (Docs/Voyages/SLICE.md, <see cref="VoyageToyDefinitionSO"/>). The twin of <see cref="VoyageView"/>: the art, name
    /// and description are the toy's, and Set sail opens the toy's own window (<see cref="ToyConfigureModal"/>), the same
    /// one the Toy Box opens, whose commit enters freestyle and sets out. Nothing here starts a voyage itself, so the
    /// window, the Toy Box and the toy in the world cannot disagree about what setting sail does.
    /// </summary>
    public class VoyageToyView : MonoBehaviour
    {
        [Header("Voyage")]
        [Tooltip("The Voyage toy this card opens.")]
        [SerializeField] VoyageToyDefinitionSO voyageToy;
        [Tooltip("The toy window that sets sail. Found in the scene when left empty (the window is a prefab and cannot " +
                 "name the scene's modal).")]
        [SerializeField] ToyConfigureModal configureModal;

        [Header("Drawn from the toy")]
        [SerializeField] Image art;
        [SerializeField] TMP_Text titleText;
        [SerializeField] TMP_Text descriptionText;

        [Header("Controls")]
        [Tooltip("Set sail: opens the voyage toy's window.")]
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

        /// <summary>Puts the toy's art, name and description on the card.</summary>
        public void Draw()
        {
            if (!voyageToy) return;
            if (art)
            {
                art.sprite = voyageToy.CardArt;
                art.enabled = voyageToy.CardArt;
            }
            if (titleText) titleText.text = voyageToy.DisplayName;
            if (descriptionText) descriptionText.text = voyageToy.Description;
        }

        /// <summary>Opens the voyage toy's window over this one; Back returns here.</summary>
        public void SetSail()
        {
            if (!voyageToy) return;

            var surface = FindSurface();
            if (surface == null)
            {
                CSDebug.LogWarning("[VoyageToyView] The Voyage toy is not standing (the toybox is not built yet, or a cell " +
                                   "swap tore it down), so Set sail opens nothing.");
                return;
            }

            if (!configureModal) configureModal = FindAnyObjectByType<ToyConfigureModal>(FindObjectsInactive.Include);
            if (!configureModal)
            {
                CSDebug.LogWarning("[VoyageToyView] Set sail found no ToyConfigureModal in the scene; the voyage cannot open.");
                return;
            }

            configureModal.Bind(surface);
            var switcher = FindAnyObjectByType<ScreenSwitcher>(FindObjectsInactive.Include);
            if (!switcher || !switcher.OpenModal(ScreenSwitcher.ModalWindows.TOYBOX_CONFIGURE))
                configureModal.ModalWindowIn();
        }

        IToyShellSurface FindSurface()
        {
            var surfaces = ToyShellRegistry.Surfaces;
            for (int i = 0; i < surfaces.Count; i++)
                if (surfaces[i] != null && surfaces[i].ShellDefinition == voyageToy)
                    return surfaces[i];
            return null;
        }
    }
}
