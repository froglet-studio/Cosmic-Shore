// Ported from Assets/_Scripts/UI/Elements/Hangar/HangarVesselGridCard.cs (Hangar unit
// 2026-07-10) — verbatim except the DOTween hover tween (deviation-marked inline);
// UnityEngine → CosmicShore.Engine, UnityEngine.UI / TMPro / UnityEngine.EventSystems →
// CosmicShore.Engine.UI (duplicate usings dropped).
using CosmicShore.UI;
// PORT Deviation (Hangar unit, restore with the DOTween arc): using DG.Tweening;
using CosmicShore.Engine.UI;
using CosmicShore.Engine;

namespace CosmicShore.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public class HangarVesselGridCard : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("UI Components")]
        [SerializeField] private Image vesselIcon;
        [SerializeField] private TMP_Text vesselName;
        [SerializeField] private GameObject lockOverlay;
        [SerializeField] private Button cardButton;

        [Header("Hover Animation")]
        [SerializeField] private float hoverScale = 1.15f;
        [SerializeField] private float hoverDuration = 0.2f;

        SO_Vessel _ship;
        HangarScreen _hangarScreen;
        CanvasGroup _canvasGroup;
        // PORT Deviation (Hangar unit, restore with the DOTween arc): Tween _hoverTween;

        public SO_Vessel Ship => _ship;

        void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
        }

        void OnDisable()
        {
            // PORT Deviation (Hangar unit, restore with the DOTween arc): _hoverTween?.Kill();
            transform.localScale = Vector3.one;
        }

        public void Configure(SO_Vessel ship, HangarScreen hangarScreen)
        {
            _ship = ship;
            _hangarScreen = hangarScreen;

            if (vesselIcon)
                vesselIcon.sprite = ship.IconActive;

            if (vesselName)
                vesselName.text = ship.Name.ToUpperInvariant();

            UpdateLockState();

            if (cardButton)
            {
                cardButton.onClick.RemoveAllListeners();
                cardButton.onClick.AddListener(OnCardClicked);
            }
        }

        public void UpdateLockState()
        {
            if (lockOverlay)
                lockOverlay.SetActive(_ship != null && _ship.IsLocked);
        }

        public void SetNameVisible(bool visible)
        {
            if (vesselName)
                vesselName.gameObject.SetActive(visible);
        }

        public void SetAlpha(float alpha)
        {
            if (!_canvasGroup)
                _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup)
                _canvasGroup.alpha = alpha;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            // PORT Deviation (Hangar unit, restore with the DOTween arc):
            // DOScale(hoverScale, hoverDuration).SetEase(Ease.OutBack).SetUpdate(true)
            // eased hover — instant set
            transform.localScale = Vector3.one * hoverScale;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // PORT Deviation (Hangar unit, restore with the DOTween arc):
            // DOScale(1f, hoverDuration).SetEase(Ease.InOutQuad).SetUpdate(true)
            // eased hover — instant set
            transform.localScale = Vector3.one;
        }

        void OnCardClicked()
        {
            if (_hangarScreen && _ship)
                _hangarScreen.SelectVesselForDetail(_ship);
        }
    }
}
