using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// One card on the Voyage window's coverflow (Rev 71 C with the Rev 74 C3 face, Docs/MENU_REDESIGN_B.md sections
    /// 148-151): the place's muted close-up and its name. Pressing it, or the D-pad's selection landing on it, picks it;
    /// it eases to the pose <see cref="VoyageCardRules.FlowPose"/> gives its offset from the picked card (the one motion,
    /// on a pick). Built by <see cref="VoyageOpenCard"/>.
    /// </summary>
    public class VoyageFlowCard : MonoBehaviour, ISelectHandler
    {
        const float Ease = 12f;

        public Button Button { get; private set; }
        public RawImage Art { get; private set; }
        public TMP_Text Name { get; private set; }
        public Image Edge { get; private set; }
        public int Index { get; set; }
        public Action<int> Picked;

        CanvasGroup _group;
        RectTransform _rt;
        float _x, _turn, _scale = 1f, _alpha = 1f;
        float _toX, _toTurn, _toScale = 1f, _toAlpha = 1f;
        bool _placed;

        /// <summary>Wire the card's parts (made by the column).</summary>
        public void Init(Button button, RawImage art, TMP_Text label, Image edge)
        {
            Button = button;
            Art = art;
            Name = label;
            Edge = edge;
            _rt = (RectTransform)transform;
            _group = GetComponent<CanvasGroup>();
            if (!_group) _group = gameObject.AddComponent<CanvasGroup>();
            button.onClick.AddListener(() => Picked?.Invoke(Index));
        }

        /// <summary>Stand <paramref name="offset"/> places from the picked card, <paramref name="step"/> pixels a place
        /// apart; snap there on the first call, ease there after.</summary>
        public void Pose(int offset, float step)
        {
            var p = VoyageCardRules.FlowPose(offset);
            _toX = p.Step * step;
            _toTurn = p.Turn;
            _toScale = p.Scale;
            _toAlpha = p.Alpha;
            if (!_placed)
            {
                _placed = true;
                _x = _toX;
                _turn = _toTurn;
                _scale = _toScale;
                _alpha = _toAlpha;
                Apply();
            }
        }

        void Update()
        {
            if (!_placed) return;
            float k = 1f - Mathf.Exp(-Ease * Time.unscaledDeltaTime);
            if (Mathf.Abs(_x - _toX) < 0.1f && Mathf.Abs(_scale - _toScale) < 0.001f && Mathf.Abs(_turn - _toTurn) < 0.05f
                && Mathf.Abs(_alpha - _toAlpha) < 0.005f)
                return;
            _x = Mathf.Lerp(_x, _toX, k);
            _turn = Mathf.Lerp(_turn, _toTurn, k);
            _scale = Mathf.Lerp(_scale, _toScale, k);
            _alpha = Mathf.Lerp(_alpha, _toAlpha, k);
            Apply();
        }

        void Apply()
        {
            if (!_rt) return;
            _rt.anchoredPosition = new Vector2(_x, 0f);
            _rt.localRotation = Quaternion.Euler(0f, _turn, 0f);
            _rt.localScale = new Vector3(_scale, _scale, 1f);
            if (_group) _group.alpha = _alpha;
        }

        public void OnSelect(BaseEventData eventData) => Picked?.Invoke(Index);
    }
}
