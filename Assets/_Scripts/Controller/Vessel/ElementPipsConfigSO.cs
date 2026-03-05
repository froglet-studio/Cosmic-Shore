using System;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    [CreateAssetMenu(fileName = "ElementPipsConfig", menuName = "CosmicShore/UI/Element Pips Config")]
    public class ElementPipsConfigSO : ScriptableObject
    {
        /// <summary>Fires when any field changes (editor OnValidate or runtime setter).</summary>
        public event Action OnChanged;

        /// <summary>Call this at runtime after modifying fields to trigger a rebuild.</summary>
        public void NotifyChanged() => OnChanged?.Invoke();

#if UNITY_EDITOR
        void OnValidate() => OnChanged?.Invoke();
#endif

        [Header("Layout")]
        [Tooltip("Number of pips per element column (range covers -5 to +15 = 20 steps)")]
        public int pipsPerColumn = 20;

        [Tooltip("Vertical spacing between each pip in UI units")]
        public float pipSpacing = 8f;

        [Tooltip("Horizontal spacing between element columns")]
        public float columnSpacing = 24f;

        [Tooltip("Size of each pip tick mark")]
        public Vector2 pipSize = new(14f, 5f);

        [Tooltip("Size of the element label icon at the bottom of each column")]
        public Vector2 labelIconSize = new(20f, 20f);

        [Tooltip("Gap between the label icon and the first pip")]
        public float labelGap = 4f;

        [Header("Zero Line")]
        [Tooltip("Index of the zero-level pip from the bottom (0-based). With range -5 to +15 the zero sits at index 5.")]
        public int zeroLineIndex = 5;

        [Tooltip("Color of the zero-line marker")]
        public Color zeroLineColor = new(1f, 1f, 1f, 0.4f);

        [Tooltip("Height of the zero-line marker in UI units")]
        public float zeroLineHeight = 1f;

        [Header("Pip Appearance")]
        [Tooltip("Color for filled (active) pips")]
        public Color filledColor = Color.white;

        [Tooltip("Color for empty (inactive) pips")]
        public Color emptyColor = new(1f, 1f, 1f, 0.15f);

        [Tooltip("Color for negative-territory filled pips (below zero)")]
        public Color negativeFillColor = new(1f, 0.3f, 0.3f, 0.8f);

        [Header("Per-Element Shapes")]
        [Tooltip("Sprite used for Charge pips")]
        public Sprite chargeSprite;

        [Tooltip("Sprite used for Mass pips")]
        public Sprite massSprite;

        [Tooltip("Sprite used for Space pips")]
        public Sprite spaceSprite;

        [Tooltip("Sprite used for Time pips")]
        public Sprite timeSprite;

        [Tooltip("Sprite used for the Charge column label")]
        public Sprite chargeLabelSprite;

        [Tooltip("Sprite used for the Mass column label")]
        public Sprite massLabelSprite;

        [Tooltip("Sprite used for the Space column label")]
        public Sprite spaceLabelSprite;

        [Tooltip("Sprite used for the Time column label")]
        public Sprite timeLabelSprite;

        public Sprite GetPipSprite(Element element) => element switch
        {
            Element.Charge => chargeSprite,
            Element.Mass   => massSprite,
            Element.Space  => spaceSprite,
            Element.Time   => timeSprite,
            _              => null
        };

        public Sprite GetLabelSprite(Element element) => element switch
        {
            Element.Charge => chargeLabelSprite,
            Element.Mass   => massLabelSprite,
            Element.Space  => spaceLabelSprite,
            Element.Time   => timeLabelSprite,
            _              => null
        };
    }
}
