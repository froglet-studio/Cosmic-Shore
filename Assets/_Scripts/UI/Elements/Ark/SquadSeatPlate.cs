using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// One seat on the Squads tab's holo table (Rev 110 B, Docs/MENU_REDESIGN_B.md section 203): a plate of light on the
    /// dome under the seat's pad, with the pilot's face, who flies it, the vessel and element, and whether they are here.
    /// The picked plate rises and glows; an open seat shows a plus instead of a face. <see cref="SquadsPage"/> writes it.
    /// </summary>
    public class SquadSeatPlate : MonoBehaviour
    {
        [SerializeField] private KitButton button;
        [Tooltip("Hides the plate while no squad is shown.")]
        [SerializeField] private CanvasGroup group;
        [Tooltip("The plate's light, in the squad's domain colour.")]
        [SerializeField] private Image fill;
        [Tooltip("The plate's edge, in the squad's domain colour.")]
        [SerializeField] private Image edge;
        [Tooltip("The pilot's face; hidden on an open seat.")]
        [SerializeField] private Image avatar;
        [Tooltip("The plus an open seat shows instead of a face.")]
        [SerializeField] private Graphic openMark;
        [SerializeField] private TMP_Text whoText;
        [SerializeField] private TMP_Text lineText;
        [Tooltip("The presence diamond: lit in the domain while the seat's pilot is here.")]
        [SerializeField] private Image dot;

        [Header("Look")]
        [Tooltip("How far the picked plate rises, reference pixels.")]
        [SerializeField] private float pickedRise = 26f;
        [SerializeField, Range(0f, 1f)] private float fillAlpha = 0.08f;
        [SerializeField, Range(0f, 1f)] private float pickedFillAlpha = 0.26f;
        [SerializeField, Range(0f, 1f)] private float edgeAlpha = 0.45f;
        [SerializeField, Range(0f, 1f)] private float offEdgeAlpha = 0.25f;
        [Tooltip("The plate's opacity while its seat is neither picked nor live.")]
        [SerializeField, Range(0f, 1f)] private float restAlpha = 0.72f;
        [Tooltip("An away pilot's face and presence diamond: this opaque (the diamond in the domain colour, unlit).")]
        [SerializeField, Range(0f, 1f)] private float awayAlpha = 0.45f;

        RectTransform _rect;
        Vector2 _rest;
        bool _hasRest;

        public KitButton Button => button;

        /// <summary>The plate's centre across the table (its anchored x), where the picked seat's projection stands.</summary>
        public float CentreX
        {
            get
            {
                Remember();
                return _rest.x;
            }
        }

        void Awake() => Remember();

        void Remember()
        {
            if (_hasRest) return;
            _rect = transform as RectTransform;
            if (!_rect) return;
            _rest = _rect.anchoredPosition;
            _hasRest = true;
        }

        public void Hide()
        {
            if (group) KitVisibility.SetShown(group, false);
            if (button) button.interactable = false;
        }

        public void Draw(string who, string line, Sprite face, bool open, bool live, bool picked, Color domain)
        {
            Remember();
            if (group)
            {
                KitVisibility.SetShown(group, true);
                group.alpha = picked || live ? 1f : restAlpha;
            }
            if (button) button.interactable = true;
            if (whoText) whoText.text = who;
            if (lineText) lineText.text = line;
            if (avatar)
            {
                avatar.enabled = !open && face;
                avatar.sprite = face;
                avatar.color = new Color(1f, 1f, 1f, live ? 1f : awayAlpha);
            }
            if (openMark) openMark.enabled = open;
            if (openMark) openMark.color = WithAlpha(domain, edgeAlpha);
            if (fill) fill.color = WithAlpha(domain, picked ? pickedFillAlpha : fillAlpha);
            if (edge) edge.color = picked ? domain : WithAlpha(domain, live ? edgeAlpha : offEdgeAlpha);
            if (dot) dot.color = live && !open ? domain : WithAlpha(domain, awayAlpha * offEdgeAlpha);
            if (_rect) _rect.anchoredPosition = _rest + (picked ? new Vector2(0f, pickedRise) : Vector2.zero);
        }

        static Color WithAlpha(Color c, float a) => new(c.r, c.g, c.b, a);
    }
}
