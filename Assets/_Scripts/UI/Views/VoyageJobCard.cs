using System;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// One job on the Voyage window, drawn as a voyage card (<see cref="VoyageJobsView"/> stands one per job on offer):
    /// the galaxy map in the art's place with the job's course on it, the course and the pilot who brought it, the job's
    /// title, and Set sail. Every word is the voyage content's (<see cref="VoyageContentSO"/>), never this class's.
    /// </summary>
    public class VoyageJobCard : MonoBehaviour
    {
        [Header("The card")]
        [Tooltip("The card's art. The galaxy map is drawn over its area, and the art itself is hidden.")]
        [SerializeField] Image art;
        [SerializeField] TMP_Text eyebrowText;
        [SerializeField] TMP_Text titleText;
        [Tooltip("Set sail on this card's job.")]
        [SerializeField] Button setSailButton;

        VoyageMapGraphic _map;
        Action _onSetSail;

        void Awake()
        {
            if (setSailButton) setSailButton.onClick.AddListener(OnSetSail);
        }

        void OnDestroy()
        {
            if (setSailButton) setSailButton.onClick.RemoveListener(OnSetSail);
        }

        void OnSetSail() => _onSetSail?.Invoke();

        /// <summary>Draw the card. A null <paramref name="onSetSail"/> leaves Set sail dark (nothing to set out on).</summary>
        public void Show(VoyageContent content, VoyageToyDefinitionSO style, string here,
            System.Collections.Generic.IReadOnlyList<string> route, string eyebrow, string title, Action onSetSail)
        {
            if (eyebrowText) eyebrowText.text = eyebrow ?? string.Empty;
            if (titleText) titleText.text = title ?? string.Empty;
            _onSetSail = onSetSail;
            if (setSailButton) setSailButton.interactable = onSetSail != null;
            var map = Map();
            if (map) map.Show(content, style, here, route);
        }

        Button _lower, _raise;
        Vector2 _sailMin, _sailMax;
        bool _sailRead;

        const float StepWidth = 64f, StepGap = 8f;

        /// <summary>
        /// Two small buttons either side of Set sail that step a number down and up (the Ark escort's difficulty). They
        /// are copies of Set sail, so they look like the card; their labels are the caller's authored words. Set sail
        /// narrows between them while they show. Null actions hide them and give Set sail back its width.
        /// </summary>
        public void ShowStepper(string lowerLabel, string raiseLabel, Action lower, Action raise)
        {
            bool show = lower != null && raise != null && setSailButton;
            if (setSailButton && !_sailRead)
            {
                var sail = (RectTransform)setSailButton.transform;
                _sailMin = sail.offsetMin;
                _sailMax = sail.offsetMax;
                _sailRead = true;
            }
            if (show)
            {
                _lower = _lower ? _lower : Step(-1);
                _raise = _raise ? _raise : Step(1);
                Arm(_lower, lowerLabel, lower);
                Arm(_raise, raiseLabel, raise);
            }
            if (_lower) _lower.gameObject.SetActive(show);
            if (_raise) _raise.gameObject.SetActive(show);
            if (setSailButton)
            {
                // Set sail stretches across the card's foot; with the stepper up it keeps the middle and the steps
                // stand at its old ends, so none of the three covers another.
                float inset = show ? StepWidth + StepGap : 0f;
                var sail = (RectTransform)setSailButton.transform;
                sail.offsetMin = new Vector2(_sailMin.x + inset, _sailMin.y);
                sail.offsetMax = new Vector2(_sailMax.x - inset, _sailMax.y);
            }
        }

        /// <summary>A copy of Set sail, <see cref="StepWidth"/> wide, pinned to the left (down) or right (up) end of
        /// where Set sail stands at full width. Sized in units, not from Set sail's rect, which is not laid out yet when
        /// the window first draws.</summary>
        Button Step(int side)
        {
            var copy = Instantiate(setSailButton, setSailButton.transform.parent);
            copy.name = side < 0 ? "StepDown" : "StepUp";
            copy.onClick.RemoveAllListeners();
            var rt = (RectTransform)copy.transform;
            float x = side < 0 ? 0f : 1f;
            rt.anchorMin = new Vector2(x, rt.anchorMin.y);
            rt.anchorMax = new Vector2(x, rt.anchorMax.y);
            rt.pivot = new Vector2(x, rt.pivot.y);
            rt.sizeDelta = new Vector2(StepWidth, rt.sizeDelta.y);
            rt.anchoredPosition = new Vector2(side < 0 ? _sailMin.x : _sailMax.x, rt.anchoredPosition.y);
            return copy;
        }

        static void Arm(Button button, string label, Action onClick)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick());
            button.interactable = true;
            var text = button.GetComponentInChildren<TMP_Text>(true);
            if (text) text.text = label ?? string.Empty;
        }

        VoyageMapGraphic Map()
        {
            if (_map) return _map;
            _map = GetComponentInChildren<VoyageMapGraphic>(true);
            if (_map || !art) return _map;

            art.enabled = false;
            var go = new GameObject("GalaxyMap", typeof(RectTransform));
            go.transform.SetParent(art.transform.parent, false);
            go.transform.SetSiblingIndex(art.transform.GetSiblingIndex() + 1);
            var rt = (RectTransform)go.transform;
            var from = art.rectTransform;
            rt.anchorMin = from.anchorMin;
            rt.anchorMax = from.anchorMax;
            rt.pivot = from.pivot;
            rt.anchoredPosition = from.anchoredPosition;
            rt.sizeDelta = from.sizeDelta;
            _map = go.AddComponent<VoyageMapGraphic>();
            _map.SetLabelTemplate(eyebrowText ? eyebrowText : titleText);
            return _map;
        }
    }
}
