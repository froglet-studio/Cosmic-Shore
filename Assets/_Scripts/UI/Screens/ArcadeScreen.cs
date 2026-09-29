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
            // Both views are OPTIONAL. Arena and Mission are this same screen pointed at a
            // different roster and author no Loadout tab, so the unguarded dereference threw in
            // Awake on every one of them - and an Awake that throws leaves the fields BELOW it
            // unassigned, so ToggleView then threw again on a null explore group. Three red
            // lines per session, from a tab that is simply not part of those screens.
            _loadoutCanvasGroup = LoadoutView ? EnsureCanvasGroup(LoadoutView.gameObject) : null;
            _exploreCanvasGroup = ExploreView ? EnsureCanvasGroup(ExploreView.gameObject) : null;
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
            if (!cg) return;
            cg.alpha = visible ? 1f : 0f;
            cg.blocksRaycasts = visible;
            cg.interactable = visible;
        }
    }
}