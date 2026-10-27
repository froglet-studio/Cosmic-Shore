using CosmicShore.Data;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// An entry's picture in the Codex - on a tile and on the open page (Docs/MENU_REDESIGN_B.md sections 89 and 90).
    ///
    /// <para>It holds STILL until it is picked (Shombith 2026-10-27: "Remove the bobbing animation and use some cool
    /// selection animations"). Picking it plays an entrance in its element's own motion, then its life while it stays
    /// picked: a crystal's flipbook runs (its real element effect, rendered from the crystal labs) and the element's
    /// petals light one by one. The motions say what the element is: Charge jolts in on a crackle, Mass drops and lands
    /// heavy, Space warps in on a turn, Time ticks into place. An entry with no element punches in.</para>
    ///
    /// <para>A lifeform's picture drifts through its palette (the domains it is seen in) whether picked or not. Nothing
    /// moves while the Codex is off screen (<see cref="Animating"/>, set by <see cref="CodexScreen"/>).</para>
    /// </summary>
    [AddComponentMenu("Cosmic Shore/UI/Codex Portrait")]
    public class CodexPortrait : MonoBehaviour
    {
        const int FlipColumns = 4, FlipFrames = 16;

        [Tooltip("What the entrance moves: the picture and the element badge ride on it.")]
        [SerializeField] private RectTransform motion;
        [Tooltip("The picture (a crystal, a lifeform, a card).")]
        [SerializeField] private Image art;
        [Tooltip("The picture in motion: a 4x4 sheet of frames, shown instead of the still while there is one.")]
        [SerializeField] private RawImage flip;
        [Tooltip("The element badge (the HUD's flower over the element's shape): shown on an element's entry.")]
        [SerializeField] private CanvasGroup elementGroup;
        [SerializeField] private KitElementFlower flower;
        [SerializeField] private Image shape;
        [Tooltip("The element shapes, Charge to Time (Assets/_Graphics/ElementShapes).")]
        [SerializeField] private Sprite[] shapes = new Sprite[4];

        [Header("Selection")]
        [Tooltip("Seconds the entrance takes when the entry is picked.")]
        [SerializeField, Min(0.1f)] private float introSeconds = 0.55f;
        [Tooltip("How much a picture punches up at the top of its entrance (0.12 = 12 %).")]
        [SerializeField, Range(0f, 0.4f)] private float punch = 0.12f;
        [Tooltip("Charge: how far the picture jolts sideways on its crackle, in canvas units.")]
        [SerializeField, Min(0f)] private float chargeJolt = 6f;
        [Tooltip("Mass: how far above the picture drops from, in canvas units.")]
        [SerializeField, Min(0f)] private float massDrop = 46f;
        [Tooltip("Space: the turn the picture warps in on, in degrees.")]
        [SerializeField] private float spaceTurn = 120f;
        [Tooltip("Time: the angle the picture ticks back from, in degrees, over four ticks.")]
        [SerializeField] private float timeTick = 40f;

        [Header("Life while picked")]
        [Tooltip("Frames a second of a flipbook (16 frames make one loop), per element: Omni (no element), then Charge " +
                 "to Time. Each matches its sheet's loop (Omni 8.3 s, Charge 3.2 s, Mass 8 s, Space 2.2 s, Time 2 s).")]
        [SerializeField] private float[] flipbookFps = { 1.92f, 5f, 2f, 7.27f, 8f };
        [Tooltip("Seconds between petals lighting.")]
        [SerializeField, Min(0.05f)] private float petalStep = 0.32f;
        [Tooltip("Seconds the full flower holds before it starts again.")]
        [SerializeField, Min(0f)] private float petalHold = 1.4f;

        [Header("Colour")]
        [Tooltip("Seconds a palette colour takes to blend into the next.")]
        [SerializeField, Min(0.5f)] private float driftSeconds = 5f;
        [Tooltip("How far a palette colour reaches into the picture (0 keeps it white, 1 the full colour).")]
        [SerializeField, Range(0f, 1f)] private float driftStrength = 0.8f;

        /// <summary>Whether any portrait moves: true only while the Codex is the shown screen.</summary>
        public static bool Animating { get; set; }

        Color[] _palette;
        Element _element = Element.None;
        bool _hasFlip, _selected;
        float _phase, _since = -1f;
        int _level = -1, _frame = -1;

        public void Show(Sprite picture, Texture2D flipbook, Element element, Color[] palette, float phase)
        {
            _phase = phase;
            _palette = palette is { Length: > 0 } ? palette : null;
            _element = element;
            _hasFlip = flipbook && flip;
            if (flip)
            {
                flip.texture = flipbook;
                flip.enabled = _hasFlip;
                SetFrame(0);
            }
            if (art)
            {
                art.sprite = picture;
                art.enabled = picture && !_hasFlip;
                art.color = _palette != null ? Tint(_phase) : Color.white;
            }
            bool isElement = IsElement(element);
            KitVisibility.SetShown(elementGroup, isElement);
            if (isElement)
            {
                int i = (int)element - 1;
                if (shape && i >= 0 && i < shapes.Length) shape.sprite = shapes[i];
                _level = -1;
                if (flower) flower.Set(element, 5);
            }
            _selected = false;
            _since = -1f;
            Pose(1f);
        }

        /// <summary>Picked: play the element's entrance, then its life. Unpicked: settle still on the first frame.</summary>
        public void Select(bool selected)
        {
            if (selected == _selected) return;
            _selected = selected;
            _since = selected ? 0f : -1f;
            if (selected) { _level = 0; Pose(0f); return; }
            Pose(1f);
            SetFrame(0);
            if (flower && IsElement(_element)) { _level = 5; flower.SetLevel(5); }
        }

        void Update()
        {
            if (!Animating) return;
            float dt = Time.unscaledDeltaTime, t = Time.unscaledTime + _phase;
            if (_palette != null && art) art.color = Tint(t);
            if (!_selected) return;

            if (_since < introSeconds)
            {
                _since += dt;
                Pose(Mathf.Clamp01(_since / introSeconds));
            }
            if (_hasFlip) SetFrame(Mathf.FloorToInt(_since * Fps()) % FlipFrames);
            if (IsElement(_element) && flower)
            {
                // 1..5 petals, one per step, then the full flower holds.
                float cycle = 5f * petalStep + petalHold;
                int level = Mathf.Min(5, 1 + Mathf.FloorToInt(Mathf.Repeat(_since, cycle) / petalStep));
                if (level != _level)
                {
                    _level = level;
                    flower.SetLevel(level);
                }
            }
        }

        /// <summary>The entrance at progress <paramref name="k"/> (0..1); 1 is at rest.</summary>
        void Pose(float k)
        {
            if (!motion) return;
            var pos = Vector2.zero;
            float angle = 0f, sx = 1f, sy = 1f;
            float swell = 1f + punch * Mathf.Sin(Mathf.PI * k);
            switch (_element)
            {
                case Element.Charge:
                    // A crackle: a hard sideways jolt that dies away, a quick swell.
                    pos.x = chargeJolt * (1f - k) * Mathf.Sign(Mathf.Sin(k * 46f)) * (k < 1f ? 1f : 0f);
                    sx = sy = swell;
                    break;
                case Element.Mass:
                    // Drops in from above and lands heavy: a bounce, and a squash where it hits.
                    pos.y = massDrop * (1f - OutBounce(k));
                    float impact = Mathf.Clamp01(1f - Mathf.Abs(k - 0.36f) / 0.14f);
                    sx = 1f + 0.12f * impact;
                    sy = 1f - 0.12f * impact;
                    break;
                case Element.Space:
                    // Warps in: turns out of a far angle and grows from small, overshooting a touch.
                    float b = OutBack(k);
                    angle = spaceTurn * (1f - b);
                    sx = sy = Mathf.LerpUnclamped(0.55f, 1f, b);
                    break;
                case Element.Time:
                    // Ticks into place: four snapped steps, like a clock hand, each with a small kick.
                    float steps = Mathf.Floor(k * 4f) / 4f;
                    angle = -timeTick * (1f - steps);
                    sx = sy = 1f + 0.04f * (1f - Mathf.Repeat(k * 4f, 1f)) * (k < 1f ? 1f : 0f);
                    break;
                default:
                    sx = sy = swell;
                    break;
            }
            motion.anchoredPosition = pos;
            motion.localEulerAngles = new Vector3(0f, 0f, angle);
            motion.localScale = new Vector3(sx, sy, 1f);
        }

        void SetFrame(int frame)
        {
            if (!flip || frame == _frame) return;
            _frame = frame;
            int col = frame % FlipColumns, row = frame / FlipColumns;
            float size = 1f / FlipColumns;
            flip.uvRect = new Rect(col * size, 1f - (row + 1) * size, size, size);
        }

        float Fps()
        {
            int i = IsElement(_element) ? (int)_element : 0;
            return flipbookFps != null && i < flipbookFps.Length && flipbookFps[i] > 0f ? flipbookFps[i] : 8f;
        }

        static bool IsElement(Element e) => e is Element.Charge or Element.Mass or Element.Space or Element.Time;

        static float OutBack(float k)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float x = k - 1f;
            return 1f + c3 * x * x * x + c1 * x * x;
        }

        static float OutBounce(float k)
        {
            const float n = 7.5625f, d = 2.75f;
            if (k < 1f / d) return n * k * k;
            if (k < 2f / d) { k -= 1.5f / d; return n * k * k + 0.75f; }
            if (k < 2.5f / d) { k -= 2.25f / d; return n * k * k + 0.9375f; }
            k -= 2.625f / d;
            return n * k * k + 0.984375f;
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
