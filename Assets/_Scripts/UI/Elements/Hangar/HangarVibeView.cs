using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The old Hangar's three "how it plays" scales under the 3D vessel (rev 3: "the old icons and sliders that were
    /// there I want them back"): Casual - Challenging, Relaxing - Thrilling, Solo - Social.
    ///
    /// <para>Rev 24 (Shombith, 2026-10-26: "for the vibe they look like sliders we can change they can be like bars rather
    /// than that"): each scale is a row of segments, lit from the left up to the value - read-only, so it no longer looks
    /// like a control. Section 90: one tick lights, at the value, on an odd row of ticks (a true middle). A row authored
    /// with the rev-3 rail and thumb still works (the thumb is placed by ANCHOR along its
    /// rail, so it lands right at any width and before the first layout pass).</para>
    ///
    /// <para>The words and values are the vessel's own (<c>SO_Vessel.gameplayParameter1..3</c>, authored per class since
    /// 2024).</para>
    /// </summary>
    public class HangarVibeView : MonoBehaviour
    {
        [System.Serializable]
        public class Slider
        {
            public TMP_Text leftText;
            public TMP_Text rightText;
            [Tooltip("Rev 24: the bar's segments, left to right; the first value x count of them are lit.")]
            public Image[] segments = System.Array.Empty<Image>();
            [Tooltip("Rev 3: the thumb, a child of the rail, anchored along it (optional).")]
            public RectTransform thumb;
            [Tooltip("Rev 3: the lit part of the rail, from the left edge to the thumb (optional).")]
            public RectTransform fill;
        }

        [SerializeField] private Slider[] sliders = new Slider[3];

        [Header("Segments")]
        [Tooltip("Section 90 (Shombith 2026-10-27: \"Vibe: single active tick, odd tick count, no extra colour\"): light only " +
                 "the one tick at the value, so an odd count has a true middle. Off: light every tick up to the value.")]
        [SerializeField] private bool singleTick = true;
        [Tooltip("A lit segment.")]
        [SerializeField] private UIColorToken litToken = UIColorToken.TextLight;
        [Tooltip("A segment past the value.")]
        [SerializeField] private UIColorToken unlitToken = UIColorToken.SurfaceDark;

        public void SetVessel(SO_Vessel vessel)
        {
            if (!vessel) return;
            Apply(0, vessel.gameplayParameter1);
            Apply(1, vessel.gameplayParameter2);
            Apply(2, vessel.gameplayParameter3);
        }

        void Apply(int i, GameplayParameter p)
        {
            if (i >= sliders.Length || sliders[i] == null) return;
            var s = sliders[i];
            if (s.leftText) s.leftText.text = p.LeftHandLabel;
            if (s.rightText) s.rightText.text = p.RightHandLabel;
            float v = Mathf.Clamp01(p.Value);
            if (s.segments is { Length: > 0 })
            {
                int n = s.segments.Length;
                int lit = Mathf.RoundToInt(v * n), at = Mathf.RoundToInt(v * (n - 1));
                Color on = UITheme.Resolve(null, litToken), off = UITheme.Resolve(null, unlitToken);
                for (int k = 0; k < n; k++)
                    if (s.segments[k]) s.segments[k].color = (singleTick ? k == at : k < lit) ? on : off;
            }
            if (s.thumb)
            {
                s.thumb.anchorMin = new Vector2(v, s.thumb.anchorMin.y);
                s.thumb.anchorMax = new Vector2(v, s.thumb.anchorMax.y);
                s.thumb.anchoredPosition = new Vector2(0f, s.thumb.anchoredPosition.y);
            }
            if (s.fill) s.fill.anchorMax = new Vector2(v, s.fill.anchorMax.y);
        }
    }
}
