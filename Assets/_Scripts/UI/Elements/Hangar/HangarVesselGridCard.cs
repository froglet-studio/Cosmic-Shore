using CosmicShore.UI;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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
        Tween _hoverTween;

        public SO_Vessel Ship => _ship;

        void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
        }

        void OnDisable()
        {
            _hoverTween?.Kill();
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
            _hoverTween?.Kill();
            _hoverTween = transform.DOScale(hoverScale, hoverDuration)
                .SetEase(Ease.OutBack)
                .SetUpdate(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hoverTween?.Kill();
            _hoverTween = transform.DOScale(1f, hoverDuration)
                .SetEase(Ease.InOutQuad)
                .SetUpdate(true);
        }

        void OnCardClicked()
        {
            if (_hangarScreen && _ship)
                _hangarScreen.SelectVesselForDetail(_ship);
        }
    }
}
