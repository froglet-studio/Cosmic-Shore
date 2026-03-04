using UnityEngine;

namespace CosmicShore
{
    [CreateAssetMenu(fileName = "ElementalBarsConfig", menuName = "CosmicShore/UI/Elemental Bars Config")]
    public class ElementalBarsConfigSO : ScriptableObject
    {
        [Header("Layout")]
        [Tooltip("Horizontal spacing between element columns")]
        public float columnSpacing = 32f;

        [Tooltip("Size of the element label icon at the bottom of each column")]
        public Vector2 labelIconSize = new(28f, 28f);

        [Tooltip("Size of each fill bar")]
        public Vector2 barSize = new(18f, 120f);

        [Tooltip("Gap between the label icon and the fill bar")]
        public float labelGap = 6f;

        [Header("Range")]
        [Tooltip("Minimum element level")]
        public int minLevel = -5;

        [Tooltip("Maximum element level")]
        public int maxLevel = 15;

        [Header("Colors")]
        [Tooltip("Color for the fill bar background")]
        public Color barBackgroundColor = new(1f, 1f, 1f, 0.08f);

        [Tooltip("Color for positive fill region")]
        public Color positiveFillColor = Color.white;

        [Tooltip("Color for negative fill region (below zero)")]
        public Color negativeFillColor = new(1f, 0.3f, 0.3f, 0.8f);

        [Tooltip("Color of the zero-line marker")]
        public Color zeroLineColor = new(1f, 1f, 1f, 0.5f);

        [Tooltip("Height of the zero-line marker")]
        public float zeroLineHeight = 2f;

        [Header("Per-Element Fill Sprites")]
        [Tooltip("Fill sprite for Charge bar")]
        public Sprite chargeFillSprite;

        [Tooltip("Fill sprite for Mass bar")]
        public Sprite massFillSprite;

        [Tooltip("Fill sprite for Space bar")]
        public Sprite spaceFillSprite;

        [Tooltip("Fill sprite for Time bar")]
        public Sprite timeFillSprite;

        [Header("Per-Element Label Sprites")]
        [Tooltip("Label icon for Charge")]
        public Sprite chargeLabelSprite;

        [Tooltip("Label icon for Mass")]
        public Sprite massLabelSprite;

        [Tooltip("Label icon for Space")]
        public Sprite spaceLabelSprite;

        [Tooltip("Label icon for Time")]
        public Sprite timeLabelSprite;

        [Header("Juice Settings")]
        [Tooltip("Duration for icon scale punch on events")]
        public float iconPunchDuration = 0.25f;

        [Tooltip("Scale multiplier for icon punch")]
        public float iconPunchScale = 1.4f;

        [Tooltip("Duration for color tween back to original")]
        public float colorTweenDuration = 0.35f;

        [Tooltip("Color flash on joust")]
        public Color joustFlashColor = Color.red;

        [Tooltip("Rotation angle for drift icon (degrees)")]
        public float driftRotationAngle = 15f;

        [Tooltip("Duration of drift rotation tween")]
        public float driftRotationDuration = 0.2f;

        public Sprite GetFillSprite(Element element) => element switch
        {
            Element.Charge => chargeFillSprite,
            Element.Mass   => massFillSprite,
            Element.Space  => spaceFillSprite,
            Element.Time   => timeFillSprite,
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
