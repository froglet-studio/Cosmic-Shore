using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The one friends / party panel, out of every window and into a drawer on the left edge
    /// (rev 3: "make it independent like a popup from the left side we can open, minimise, maximise
    /// whenever we want"). Three states:
    /// <list type="bullet">
    /// <item><b>Closed</b> - only the handle on the left edge shows; pressing it opens the drawer.</item>
    /// <item><b>Open</b> - the window slides in from the left at its normal size.</item>
    /// <item><b>Maximised</b> - the same window, grown to its large size; the button restores it.</item>
    /// </list>
    /// The – button minimises back to the handle. The panel inside (PartyPanel) keeps all its own
    /// behaviour; this only moves and sizes its window.
    /// </summary>
    [AddComponentMenu("Cosmic Shore/UI/Friends Drawer")]
    public class FriendsDrawer : MonoBehaviour
    {
        public enum DrawerState { Closed = 0, Open = 1, Maximised = 2 }

        [Header("Wiring")]
        [Tooltip("The handle on the left edge (shown while closed).")]
        [SerializeField] private CanvasGroup handleGroup;
        [SerializeField] private Button handleButton;
        [Tooltip("The drawer window (holds the party panel).")]
        [SerializeField] private CanvasGroup windowGroup;
        [SerializeField] private RectTransform window;
        [SerializeField] private Button minimiseButton;
        [SerializeField] private Button maximiseButton;
        [Tooltip("The maximise button's glyph while the window is maximised (shown) or normal (hidden).")]
        [SerializeField] private CanvasGroup restoreGlyph;
        [Tooltip("The maximise button's glyph while the window is normal.")]
        [SerializeField] private CanvasGroup maximiseGlyph;

        [Header("Layout")]
        [SerializeField] private Vector2 normalSize = new(460f, 860f);
        [SerializeField] private Vector2 maximisedSize = new(720f, 900f);
        [Tooltip("Gap between the screen's left edge and the open window.")]
        [SerializeField] private float openInset = 16f;
        [Tooltip("Seconds a slide or resize takes.")]
        [SerializeField, Min(0f)] private float moveSeconds = 0.22f;

        private DrawerState _state = DrawerState.Closed;
        private Vector2 _fromPos, _toPos, _fromSize, _toSize;
        private float _t = 1f;

        public DrawerState State => _state;

        private void Awake()
        {
            if (handleButton) handleButton.onClick.AddListener(Open);
            if (minimiseButton) minimiseButton.onClick.AddListener(Close);
            if (maximiseButton) maximiseButton.onClick.AddListener(ToggleMaximise);
            Apply(DrawerState.Closed, instant: true);
        }

        private void OnDestroy()
        {
            if (handleButton) handleButton.onClick.RemoveListener(Open);
            if (minimiseButton) minimiseButton.onClick.RemoveListener(Close);
            if (maximiseButton) maximiseButton.onClick.RemoveListener(ToggleMaximise);
        }

        public void Open() => Apply(DrawerState.Open, instant: false);
        public void Close() => Apply(DrawerState.Closed, instant: false);

        public void ToggleMaximise() =>
            Apply(_state == DrawerState.Maximised ? DrawerState.Open : DrawerState.Maximised, instant: false);

        private void Apply(DrawerState state, bool instant)
        {
            _state = state;
            bool open = state != DrawerState.Closed;
            KitVisibility.SetShown(handleGroup, !open);
            KitVisibility.SetShown(maximiseGlyph, state != DrawerState.Maximised);
            KitVisibility.SetShown(restoreGlyph, state == DrawerState.Maximised);
            // The window stays drawn while it slides out; it stops taking input at once.
            if (open) KitVisibility.SetShown(windowGroup, true);
            else if (windowGroup) { windowGroup.interactable = false; windowGroup.blocksRaycasts = false; }
            if (!window) return;

            Vector2 size = state == DrawerState.Maximised ? maximisedSize : normalSize;
            _fromPos = window.anchoredPosition;
            _fromSize = window.sizeDelta;
            _toSize = size;
            _toPos = new Vector2(open ? openInset : -size.x - 8f, _fromPos.y);
            _t = instant || moveSeconds <= 0f ? 1f : 0f;
            if (_t >= 1f) Place(1f);
        }

        private void Update()
        {
            if (_t >= 1f) return;
            _t = Mathf.Min(1f, _t + Time.unscaledDeltaTime / moveSeconds);
            Place(1f - (1f - _t) * (1f - _t) * (1f - _t));   // OutCubic
        }

        private void Place(float k)
        {
            window.anchoredPosition = Vector2.LerpUnclamped(_fromPos, _toPos, k);
            window.sizeDelta = Vector2.LerpUnclamped(_fromSize, _toSize, k);
            if (k >= 1f && _state == DrawerState.Closed) KitVisibility.SetShown(windowGroup, false);
        }
    }
}
