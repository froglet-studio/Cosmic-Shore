using System;
using CosmicShore.Data;
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
        [SerializeField] int pipsPerColumn = 20;
        public int PipsPerColumn => pipsPerColumn;

        [Tooltip("Vertical spacing between each pip in UI units")]
        [SerializeField] float pipSpacing = 8f;
        public float PipSpacing => pipSpacing;

        [Tooltip("Horizontal spacing between element columns")]
        [SerializeField] float columnSpacing = 24f;
        public float ColumnSpacing => columnSpacing;

        [Tooltip("Size of each pip tick mark")]
        [SerializeField] Vector2 pipSize = new(14f, 5f);
        public Vector2 PipSize => pipSize;

        [Tooltip("Size of the element label icon at the bottom of each column")]
        [SerializeField] Vector2 labelIconSize = new(20f, 20f);
        public Vector2 LabelIconSize => labelIconSize;

        [Tooltip("Gap between the label icon and the first pip")]
        [SerializeField] float labelGap = 4f;
        public float LabelGap => labelGap;

        [Header("Zero Line")]
        [Tooltip("Index of the zero-level pip from the bottom (0-based). With range -5 to +15 the zero sits at index 5.")]
        [SerializeField] int zeroLineIndex = 5;
        public int ZeroLineIndex => zeroLineIndex;

        [Tooltip("Color of the zero-line marker")]
        [SerializeField] Color zeroLineColor = new(1f, 1f, 1f, 0.4f);
        public Color ZeroLineColor => zeroLineColor;

        [Tooltip("Height of the zero-line marker in UI units")]
        [SerializeField] float zeroLineHeight = 1f;
        public float ZeroLineHeight => zeroLineHeight;

        [Header("Pip Appearance")]
        [Tooltip("Color for filled (active) pips")]
        [SerializeField] Color filledColor = Color.white;
        public Color FilledColor => filledColor;

        [Tooltip("Color for empty (inactive) pips")]
        [SerializeField] Color emptyColor = new(1f, 1f, 1f, 0.15f);
        public Color EmptyColor => emptyColor;

        [Tooltip("Color for negative-territory filled pips (below zero)")]
        [SerializeField] Color negativeFillColor = new(1f, 0.3f, 0.3f, 0.8f);
        public Color NegativeFillColor => negativeFillColor;

        [Header("Per-Element Shapes")]
        [Tooltip("Sprite used for Charge pips")]
        [SerializeField] Sprite chargeSprite;
        public Sprite ChargeSprite => chargeSprite;

        [Tooltip("Sprite used for Mass pips")]
        [SerializeField] Sprite massSprite;
        public Sprite MassSprite => massSprite;

        [Tooltip("Sprite used for Space pips")]
        [SerializeField] Sprite spaceSprite;
        public Sprite SpaceSprite => spaceSprite;

        [Tooltip("Sprite used for Time pips")]
        [SerializeField] Sprite timeSprite;
        public Sprite TimeSprite => timeSprite;

        [Tooltip("Sprite used for the Charge column label")]
        [SerializeField] Sprite chargeLabelSprite;
        public Sprite ChargeLabelSprite => chargeLabelSprite;

        [Tooltip("Sprite used for the Mass column label")]
        [SerializeField] Sprite massLabelSprite;
        public Sprite MassLabelSprite => massLabelSprite;

        [Tooltip("Sprite used for the Space column label")]
        [SerializeField] Sprite spaceLabelSprite;
        public Sprite SpaceLabelSprite => spaceLabelSprite;

        [Tooltip("Sprite used for the Time column label")]
        [SerializeField] Sprite timeLabelSprite;
        public Sprite TimeLabelSprite => timeLabelSprite;

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
