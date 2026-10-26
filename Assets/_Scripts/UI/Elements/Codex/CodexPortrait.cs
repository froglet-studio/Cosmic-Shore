using CosmicShore.Data;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// An entry's picture in the Codex - on a tile and on the open page - subtly alive (Docs/MENU_REDESIGN_B.md section
    /// 89; Shombith 2026-10-26: "Make everything subtly animated"): it floats a few units on a slow sine; a lifeform's
    /// picture drifts through its palette (the domains it is seen in); an element draws the HUD's own flower and shape
    /// instead of a picture, the petals lighting one by one, holding, and starting again while the shape breathes.
    ///
    /// <para>Moves only while the Codex is the shown screen (<see cref="Animating"/>, set by <see cref="CodexScreen"/>), so
    /// a screen slid off the strip costs nothing. Each copy starts from its own phase, so a grid does not bob in step.</para>
    /// </summary>
    [AddComponentMenu("Cosmic Shore/UI/Codex Portrait")]
    public class CodexPortrait : MonoBehaviour
    {
        [Tooltip("What moves: the picture and the element flower ride on it.")]
        [SerializeField] private RectTransform motion;
        [Tooltip("The picture (a portrait, a hull, a crystal).")]
        [SerializeField] private Image art;
        [Tooltip("The element flower and shape: shown instead of the picture on an element.")]
        [SerializeField] private CanvasGroup elementGroup;
        [SerializeField] private KitElementFlower flower;
        [SerializeField] private Image shape;
        [Tooltip("The element shapes, Charge to Time (Assets/_Graphics/ElementShapes).")]
        [SerializeField] private Sprite[] shapes = new Sprite[4];

        [Header("Motion")]
        [Tooltip("How far the picture floats, in canvas units.")]
        [SerializeField, Min(0f)] private float floatAmplitude = 4f;
        [SerializeField, Min(0.5f)] private float floatPeriod = 4.5f;
        [Tooltip("Seconds a palette colour takes to blend into the next.")]
        [SerializeField, Min(0.5f)] private float driftSeconds = 5f;
        [Tooltip("How far a palette colour reaches into the picture (0 keeps it white, 1 the full colour).")]
        [SerializeField, Range(0f, 1f)] private float driftStrength = 0.8f;
        [Tooltip("Seconds between petals lighting.")]
        [SerializeField, Min(0.05f)] private float petalStep = 0.32f;
        [Tooltip("Seconds the full flower holds before it starts again.")]
        [SerializeField, Min(0f)] private float petalHold = 1.4f;
        [Tooltip("How much the element shape grows at the top of a breath (0.04 = 4 %).")]
        [SerializeField, Range(0f, 0.2f)] private float shapeBreath = 0.05f;

        /// <summary>Whether any portrait moves: true only while the Codex is the shown screen.</summary>
        public static bool Animating { get; set; }

        Color[] _palette;
        Element _element = Element.None;
        float _phase;
        int _level = -1;

        public void Show(Sprite picture, Element element, Color[] palette, float phase)
        {
            _phase = phase;
            _palette = palette is { Length: > 0 } ? palette : null;
            _element = element;
            bool isElement = element is Element.Charge or Element.Mass or Element.Space or Element.Time;
            if (art)
            {
                art.sprite = isElement ? null : picture;
                art.enabled = !isElement && picture;
                art.color = _palette != null ? Tint(0f) : Color.white;
            }
            KitVisibility.SetShown(elementGroup, isElement);
            if (isElement)
            {
                int i = (int)element - 1;
                if (shape && i >= 0 && i < shapes.Length) shape.sprite = shapes[i];
                _level = -1;
                if (flower) flower.Set(element, 5);
            }
        }

        void Update()
        {
            if (!Animating) return;
            float t = Time.unscaledTime + _phase;
            if (motion) motion.anchoredPosition = new Vector2(0f, Mathf.Sin(t * 2f * Mathf.PI / floatPeriod) * floatAmplitude);
            if (_palette != null && art) art.color = Tint(t);
            if (_element is Element.Charge or Element.Mass or Element.Space or Element.Time)
            {
                // 1..5 petals, one per step, then the full flower holds.
                float cycle = 5f * petalStep + petalHold;
                int level = Mathf.Min(5, 1 + Mathf.FloorToInt(Mathf.Repeat(t, cycle) / petalStep));
                if (level != _level && flower)
                {
                    _level = level;
                    flower.SetLevel(level);
                }
                if (shape) shape.rectTransform.localScale = Vector3.one * (1f + shapeBreath * (0.5f + 0.5f * Mathf.Sin(t * 2f * Mathf.PI / floatPeriod)));
            }
        }

        Color Tint(float t)
        {
            int n = _palette.Length;
            float k = Mathf.Repeat(t / driftSeconds, n);
            int a = Mathf.FloorToInt(k) % n, b = (a + 1) % n;
            float f = Mathf.SmoothStep(0f, 1f, k - Mathf.Floor(k));
            var c = Color.Lerp(_palette[a], _palette[b], f);
            return Color.Lerp(Color.white, new Color(c.r, c.g, c.b, 1f), driftStrength);
        }
    }
}
