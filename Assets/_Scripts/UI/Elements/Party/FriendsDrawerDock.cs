using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Makes room for the friends drawer inside a hub window (Arcade, Arena, Mission, Toy Box; Docs/MENU_REDESIGN_B.md
    /// §68, Shombith 2026-10-25: "when it is enabled inside the arcade screen the cards move to the right when this panel
    /// opens up"). While <see cref="FriendsDrawer.DockWidth"/> is above zero - the drawer is open over a hub window - the
    /// window's header and body start right of it; at zero they return to their authored gutter. Home, the Hangar and
    /// every other screen carry no dock, so the drawer slides over them and moves nothing.
    ///
    /// <para>Only each rect's left edge moves, so the layout groups inside reflow the cards into the narrower body.
    /// Event-driven: it writes when the drawer moves, never per frame on its own.</para>
    /// </summary>
    [AddComponentMenu("Cosmic Shore/UI/Friends Drawer Dock")]
    public class FriendsDrawerDock : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("The rects whose left edge moves right of the open drawer: the window's header and body.")]
        [SerializeField] private RectTransform[] pushed = System.Array.Empty<RectTransform>();

        [Header("Layout")]
        [Tooltip("Air between the drawer's right edge and the window's content (canvas units).")]
        [SerializeField, Min(0f)] private float gap = 24f;

        private float[] _authoredLeft;

        private void Awake()
        {
            _authoredLeft = new float[pushed.Length];
            for (int i = 0; i < pushed.Length; i++)
                if (pushed[i]) _authoredLeft[i] = pushed[i].offsetMin.x;
        }

        private void OnEnable()
        {
            FriendsDrawer.DockWidthChanged += Apply;
            Apply(FriendsDrawer.DockWidth);
        }

        private void OnDisable()
        {
            FriendsDrawer.DockWidthChanged -= Apply;
            Apply(0f);
        }

        private void Apply(float width)
        {
            if (_authoredLeft == null) return;
            for (int i = 0; i < pushed.Length; i++)
            {
                var rect = pushed[i];
                if (!rect) continue;
                // The authored gutter already clears the closed drawer's orb; the open drawer pushes past it.
                float left = width > 0f ? Mathf.Max(_authoredLeft[i], width + gap) : _authoredLeft[i];
                if (!Mathf.Approximately(rect.offsetMin.x, left))
                    rect.offsetMin = new Vector2(left, rect.offsetMin.y);
            }
        }
    }
}
