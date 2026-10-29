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
