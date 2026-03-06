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

        void Start()
        {
            LoadoutButton.Select();
        }

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