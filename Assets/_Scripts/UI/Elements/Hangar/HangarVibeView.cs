using TMPro;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// The old Hangar's three "how it plays" sliders, back under the 3D vessel (rev 3: "the old icons and
    /// sliders that were there I want them back"): Casual - Challenging, Relaxing - Thrilling, Solo -
    /// Social, each a read-only rail with a thumb, drawn with the original rail and thumb art.
    ///
    /// <para>The words and values are the vessel's own (<c>SO_Vessel.gameplayParameter1..3</c>, authored
    /// per class since 2024). The thumb is placed by ANCHOR along its rail rather than by a pixel offset,
    /// so it lands right at any width and before the first layout pass - the legacy display measured the
    /// rail's rect, which reads 0 until the layout has run.</para>
    /// </summary>
    public class HangarVibeView : MonoBehaviour
    {
        [System.Serializable]
        public class Slider
        {
            public TMP_Text leftText;
            public TMP_Text rightText;
            [Tooltip("The thumb; a child of the rail, anchored along it.")]
            public RectTransform thumb;
            [Tooltip("The lit part of the rail, from the left edge to the thumb (optional).")]
            public RectTransform fill;
        }

        [SerializeField] private Slider[] sliders = new Slider[3];

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
