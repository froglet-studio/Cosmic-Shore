using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using Reflex.Attributes;
using Reflex.Core;
using Reflex.Injectors;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Menu-specific MiniGameHUD for freestyle mode in Menu_Main.
    /// Provides the Volume/Pause button that opens the vessel selection panel
    /// (matching the MinigameFreestyle scene pattern), vessel HUD reparenting
    /// via the onShipHUDInitialized SOAP event, and PauseMenu instantiation.
    ///
    /// This is a slim alternative to the full <see cref="MiniGameHUD"/> which
    /// has gameplay behavior (connecting panel, cinematic, score tracking)
    /// unsuitable for the menu context. The full MiniGameHUD can replace this
    /// when Phase 2/3 lava-lamp features are needed.
    /// </summary>
    public class MenuMiniGameHUD : MonoBehaviour
    {
        [Header("HUD Visibility")]
        [SerializeField] CanvasGroup canvasGroup;

        [Header("Volume / Pause Button")]
        [SerializeField] Button volumePauseButton;

        [Header("Vessel Selection")]
        [SerializeField] MenuVesselSelectionPanelController vesselSelectionPanel;

        [Header("SOAP Events")]
        [SerializeField] ScriptableEventShipHUDData onShipHUDInitialized;

        [Header("Pause Menu")]
        [SerializeField] GameObject pauseMenuPrefab;

        [Inject] Container _container;

        void Awake()
        {
            volumePauseButton.onClick.AddListener(OnVolumePauseClicked);
        }

        void OnEnable()
        {
            if (onShipHUDInitialized)
                onShipHUDInitialized.OnRaised += OnShipHUDInitialized;
        }

        void Start()
        {
            InstantiatePauseMenu();
        }

        void OnDisable()
        {
            if (onShipHUDInitialized)
                onShipHUDInitialized.OnRaised -= OnShipHUDInitialized;
        }

        void OnDestroy()
        {
            volumePauseButton?.onClick.RemoveListener(OnVolumePauseClicked);
        }

        void OnVolumePauseClicked()
        {
            vesselSelectionPanel.Open();
            Hide();
        }

        public void Show()
        {
            canvasGroup.alpha = 1;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        public void Hide()
        {
            canvasGroup.alpha = 0;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        /// <summary>
        /// Vessel HUD reparenting — identical to MiniGameHUD.OnShipHUDInitialized().
        /// When a vessel spawns, ShipHUD.Start() raises this SOAP event with the
        /// vessel's MiniGameHUD children. We reparent them under our parent
        /// (Game UI canvas) so they render as siblings.
        /// </summary>
        void OnShipHUDInitialized(ShipHUDData data)
        {
            if (!data.ShipHUD) return;

            Hide();

            foreach (Transform child in data.ShipHUD.GetComponentsInChildren<Transform>(false))
            {
                if (child == data.ShipHUD.transform) continue;
                child.SetParent(transform.parent, false);
                child.SetSiblingIndex(0);
            }

            data.ShipHUD.gameObject.SetActive(true);
        }

        void InstantiatePauseMenu()
        {
            if (!pauseMenuPrefab) return;
            var go = Instantiate(pauseMenuPrefab, transform.parent);
            GameObjectInjector.InjectRecursive(go, _container);
            go.SetActive(false);
        }
    }
}
