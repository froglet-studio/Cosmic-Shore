using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CosmicShore.App.UI.FX
{
    /// <summary>
    /// Visual highlight when a Selectable receives focus via gamepad/keyboard navigation.
    /// Scale pulse + optional color tint to clearly show which element is selected.
    /// Attach alongside any Button, Toggle, or Selectable.
    /// </summary>
    [RequireComponent(typeof(Selectable))]
    public class SelectionHighlight : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        [Header("Scale")]
        [SerializeField] private float selectedScale = 1.08f;
        [SerializeField] private float scaleDuration = 0.2f;

        [Header("Color Tint")]
        [Tooltip("If assigned, tints this Graphic when selected.")]
        [SerializeField] private Graphic targetGraphic;
        [SerializeField] private Color selectedTint = new Color(1f, 1f, 1f, 1f);
        [SerializeField] private float colorDuration = 0.15f;

        [Header("Outline")]
        [Tooltip("If true, adds/enables an Outline component when selected.")]
        [SerializeField] private bool useOutline;
        [SerializeField] private Color outlineColor = new Color(0.4f, 0.8f, 1f, 0.8f);
        [SerializeField] private Vector2 outlineDistance = new Vector2(2f, 2f);

        private Vector3 _originalScale;
        private Color _originalColor;
        private Tween _scaleTween;
        private Tween _colorTween;
        private Outline _outline;

        void Awake()
        {
            _originalScale = transform.localScale;

            if (targetGraphic != null)
                _originalColor = targetGraphic.color;

            if (useOutline)
            {
                _outline = GetComponent<Outline>();
                if (_outline == null)
                    _outline = gameObject.AddComponent<Outline>();

                _outline.effectColor = outlineColor;
                _outline.effectDistance = outlineDistance;
                _outline.enabled = false;
            }
        }

        public void OnSelect(BaseEventData eventData)
        {
            _scaleTween?.Kill();
            _scaleTween = transform.DOScale(_originalScale * selectedScale, scaleDuration)
                .SetEase(Ease.OutBack)
                .SetUpdate(true);

            if (targetGraphic != null)
            {
                _colorTween?.Kill();
                _colorTween = targetGraphic.DOColor(selectedTint, colorDuration)
                    .SetUpdate(true);
            }

            if (_outline != null)
                _outline.enabled = true;
        }

        public void OnDeselect(BaseEventData eventData)
        {
            _scaleTween?.Kill();
            _scaleTween = transform.DOScale(_originalScale, scaleDuration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true);

            if (targetGraphic != null)
            {
                _colorTween?.Kill();
                _colorTween = targetGraphic.DOColor(_originalColor, colorDuration)
                    .SetUpdate(true);
            }

            if (_outline != null)
                _outline.enabled = false;
        }

        void OnDisable()
        {
            _scaleTween?.Kill();
            _colorTween?.Kill();
            transform.localScale = _originalScale;

            if (targetGraphic != null)
                targetGraphic.color = _originalColor;

            if (_outline != null)
                _outline.enabled = false;
        }

        void OnDestroy()
        {
            _scaleTween?.Kill();
            _colorTween?.Kill();
        }
    }
}
