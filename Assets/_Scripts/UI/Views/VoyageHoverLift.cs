using UnityEngine;
using UnityEngine.EventSystems;

namespace CosmicShore.UI
{
    /// <summary>
    /// A glass card's hover (Frogma Rev 108, Docs/MENU_REDESIGN_B.md section 202: "hover lift", the waves "faster on
    /// hover"): while the pointer is over the card its content rises <see cref="Rise"/> units and its sea lines drift
    /// faster; it settles back when the pointer leaves. No glow.
    /// </summary>
    public class VoyageHoverLift : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public RectTransform Target;
        public VoyageSeaLines Lines;
        public float Rise = 6f;
        public float Ease = 12f;

        bool _over;
        float _t, _baseSpeed = -1f;

        public void OnPointerEnter(PointerEventData eventData) => _over = true;
        public void OnPointerExit(PointerEventData eventData) => _over = false;

        void OnDisable()
        {
            _over = false;
            _t = 0f;
            Apply();
        }

        void Update()
        {
            float goal = _over ? 1f : 0f;
            if (Mathf.Approximately(_t, goal)) return;
            _t = Mathf.MoveTowards(_t, goal, Ease * Time.unscaledDeltaTime * Mathf.Max(0.05f, Mathf.Abs(goal - _t)) + 0.002f);
            Apply();
        }

        void Apply()
        {
            if (Target) Target.anchoredPosition = new Vector2(Target.anchoredPosition.x, Rise * _t);
            if (!Lines) return;
            if (_baseSpeed < 0f) _baseSpeed = Lines.Speed;
            Lines.Speed = _baseSpeed * (1f + 3f * _t);
        }
    }
}
