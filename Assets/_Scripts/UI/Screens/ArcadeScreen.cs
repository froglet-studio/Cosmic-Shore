using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.UI;
using Reflex.Attributes;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using System.Linq;

namespace CosmicShore.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public class ArcadeScreen : MonoBehaviour
    {
        [Inject] AudioSystem audioSystem;

        [FormerlySerializedAs("exploreMenu")]
        [SerializeField] ArcadeExploreView ExploreView;
        [FormerlySerializedAs("loadoutMenu")]
        [SerializeField] ArcadeLoadoutView LoadoutView;
        [SerializeField] Toggle LoadoutButton;
        [SerializeField] Toggle ExploreButton;

        CanvasGroup _canvasGroup;
        CanvasGroup _loadoutCanvasGroup;
        CanvasGroup _exploreCanvasGroup;

        void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            _loadoutCanvasGroup = EnsureCanvasGroup(LoadoutView.gameObject);
            _exploreCanvasGroup = EnsureCanvasGroup(ExploreView.gameObject);
        }

        // No Select() at Start. This component lives on the Arcade MODAL, which is active at
        // alpha 0 from scene load, so selecting its toggle here handed the EventSystem's
        // selection to a control inside an invisible window - and pressing A on the HOME screen
        // opened the first game card. Focus now follows the modal stack (ScreenSwitcher.Refocus)
        // and lands on this window only when it is actually open.

        public void Show()
        {
            SetCanvasGroupVisible(_canvasGroup, true);
        }

        public void Hide()
        {
            SetCanvasGroupVisible(_canvasGroup, false);
        }

        public void ToggleView(bool loadout)
        {
            if (loadout)
                UserActionSystem.Instance.CompleteAction(UserActionType.ViewArcadeLoadoutMenu);
            else
                UserActionSystem.Instance.CompleteAction(UserActionType.ViewArcadeExploreMenu);

            audioSystem.PlayMenuAudio(MenuAudioCategory.SwitchView);
            SetCanvasGroupVisible(_loadoutCanvasGroup, loadout);
            SetCanvasGroupVisible(_exploreCanvasGroup, !loadout);
        }

        static CanvasGroup EnsureCanvasGroup(GameObject go)
        {
            if (!go.TryGetComponent<CanvasGroup>(out var cg))
                cg = go.AddComponent<CanvasGroup>();
            return cg;
        }

        static void SetCanvasGroupVisible(CanvasGroup cg, bool visible)
        {
            cg.alpha = visible ? 1f : 0f;
            cg.blocksRaycasts = visible;
            cg.interactable = visible;
        }
    }
}