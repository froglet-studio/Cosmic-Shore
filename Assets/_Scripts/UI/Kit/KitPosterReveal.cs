using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Poster game card's reveal (Frogma Rev 128 E, picked by Shombith 2026-11-23, Docs/MENU_REDESIGN_B.md section
    /// 222): at rest the card is all picture with the mode's name across its foot; when the pointer is on it, or the pad's
    /// focus, a panel slides up from the foot with the mode's line and its pilots, the name rides up on top of it and the
    /// hull fades out.
    ///
    /// <para>Pointer and pad only, never touch (a touch has no hover: a tap opens the mode). Runs a short coroutine on
    /// unscaled time (menu screens other than HOME run at timeScale 0), so a resting card costs nothing per frame.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class KitPosterReveal : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        [Header("Parts")]
        [Tooltip("The panel that slides up from the card's foot. Anchored to the bottom edge; its height is driven.")]
        [SerializeField] private RectTransform panel;
        [Tooltip("The mode's name. It rises with the panel's top.")]
        [SerializeField] private RectTransform title;
        [Tooltip("The hull. Faded out while the panel is open.")]
        [SerializeField] private CanvasGroup hull;

        [Header("Motion")]
        [Tooltip("The open panel's height, in canvas units (the board's 112).")]
        [SerializeField] private float openHeight = 112f;
        [Tooltip("Seconds to open or close (the board's .2 s).")]
        [SerializeField, Min(0.01f)] private float duration = 0.2f;

        private Vector2 _titleRest;
        private bool _hover, _focus, _restCaptured;
        private float _t;
        private Coroutine _run;

        public bool IsOpen => _hover || _focus;

        private void Awake() => CaptureRest();

        private void OnDisable()
        {
            _hover = _focus = false;
            _run = null;
            Apply(0f);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (eventData is ExtendedPointerEventData { pointerType: UIPointerType.Touch }) return;
            _hover = true;
            Retarget();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hover = false;
            Retarget();
        }

        public void OnSelect(BaseEventData eventData)
        {
            _focus = true;
            Retarget();
        }

        public void OnDeselect(BaseEventData eventData)
        {
            _focus = false;
            Retarget();
        }

        private void CaptureRest()
        {
            if (_restCaptured) return;
            if (title) _titleRest = title.anchoredPosition;
            _restCaptured = true;
        }

        private void Retarget()
        {
            if (!isActiveAndEnabled) return;
            CaptureRest();
            _run ??= StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            while (true)
            {
                float target = IsOpen ? 1f : 0f;
                _t = Mathf.MoveTowards(_t, target, Time.unscaledDeltaTime / duration);
                Apply(_t);
                if (Mathf.Approximately(_t, target)) break;
                yield return null;
            }
            _run = null;
        }

        private void Apply(float t)
        {
            _t = t;
            float eased = t * t * (3f - 2f * t);
            float h = openHeight * eased;
            if (panel) panel.sizeDelta = new Vector2(panel.sizeDelta.x, h);
            if (title && _restCaptured) title.anchoredPosition = _titleRest + new Vector2(0f, h);
            if (hull) hull.alpha = 1f - eased;
        }
    }
}
