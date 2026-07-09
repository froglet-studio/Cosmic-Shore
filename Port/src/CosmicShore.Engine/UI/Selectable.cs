namespace CosmicShore.Engine.UI
{
    /// <summary>Per-state tint set for ColorTint transitions (original contract: ColorBlock).</summary>
    public struct ColorBlock
    {
        public Color normalColor;
        public Color highlightedColor;
        public Color pressedColor;
        public Color selectedColor;
        public Color disabledColor;
        public float colorMultiplier;
        public float fadeDuration;

        /// <summary>The original's default block (white-ish ramp, 1× multiplier, 0.1s fade).</summary>
        public static ColorBlock defaultColorBlock => new()
        {
            normalColor = new Color(1f, 1f, 1f, 1f),
            highlightedColor = new Color(0.9607843f, 0.9607843f, 0.9607843f, 1f),
            pressedColor = new Color(0.78431374f, 0.78431374f, 0.78431374f, 1f),
            selectedColor = new Color(0.9607843f, 0.9607843f, 0.9607843f, 1f),
            disabledColor = new Color(0.78431374f, 0.78431374f, 0.78431374f, 0.5f),
            colorMultiplier = 1f,
            fadeDuration = 0.1f,
        };
    }

    /// <summary>
    /// Interactive UI element base (original contract): tracks pointer/selection state,
    /// gates on <see cref="interactable"/>, and drives the target graphic's tint per
    /// state. Headless deviation (documented): ColorTint applies INSTANTLY rather than
    /// cross-fading over fadeDuration — steady-state colors are identical; the fade
    /// arrives with the Arc-C render loop's tweening.
    /// </summary>
    public class Selectable : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
        ISelectHandler, IDeselectHandler, IMoveHandler
    {
        public enum Transition { None = 0, ColorTint = 1, SpriteSwap = 2, Animation = 3 }

        protected enum SelectionState { Normal = 0, Highlighted = 1, Pressed = 2, Selected = 3, Disabled = 4 }

        // Live registry (original contract: allSelectables) — navigation searches it.
        // GameLoop's fresh-world reset clears it alongside the raycaster registry.
        static readonly System.Collections.Generic.List<Selectable> s_Selectables = new();

        public static System.Collections.Generic.IReadOnlyList<Selectable> allSelectables => s_Selectables;

        internal static void ResetRegistry() => s_Selectables.Clear();

        [SerializeField] bool m_Interactable = true;
        [SerializeField] Transition m_Transition = Transition.ColorTint;
        [SerializeField] Graphic m_TargetGraphic;
        [SerializeField] ColorBlock m_Colors = ColorBlock.defaultColorBlock;
        [SerializeField] Sprite m_HighlightedSprite;
        [SerializeField] Sprite m_PressedSprite;
        [SerializeField] Sprite m_SelectedSprite;
        [SerializeField] Sprite m_DisabledSprite;
        [SerializeField] Navigation m_Navigation = Navigation.defaultNavigation;

        public Navigation navigation { get => m_Navigation; set => m_Navigation = value; }

        bool m_IsPointerInside;
        bool m_IsPointerDown;
        bool m_HasSelection;

        public bool interactable
        {
            get => m_Interactable;
            set
            {
                if (m_Interactable == value) return;
                m_Interactable = value;
                if (!value && EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject)
                    EventSystem.current.SetSelectedGameObject(null);
                DoStateTransition(currentSelectionState);
            }
        }

        public Transition transition { get => m_Transition; set { m_Transition = value; DoStateTransition(currentSelectionState); } }
        public Graphic targetGraphic { get => m_TargetGraphic; set { m_TargetGraphic = value; DoStateTransition(currentSelectionState); } }
        public ColorBlock colors { get => m_Colors; set { m_Colors = value; DoStateTransition(currentSelectionState); } }

        public Image image => m_TargetGraphic as Image;

        public virtual bool IsInteractable() => m_Interactable;

        protected bool IsPressed() => m_IsPointerInside && m_IsPointerDown;

        protected SelectionState currentSelectionState
        {
            get
            {
                if (!m_Interactable) return SelectionState.Disabled;
                if (IsPressed()) return SelectionState.Pressed;
                if (m_HasSelection) return SelectionState.Selected;
                if (m_IsPointerInside) return SelectionState.Highlighted;
                return SelectionState.Normal;
            }
        }

        protected virtual void OnEnable()
        {
            if (!s_Selectables.Contains(this)) s_Selectables.Add(this);
            // If nothing wired the target graphic, adopt one on this object (original
            // editor-time default, resolved lazily here).
            m_TargetGraphic ??= gameObject.GetComponent<Graphic>();
            DoStateTransition(currentSelectionState);
        }

        protected virtual void OnDisable()
        {
            s_Selectables.Remove(this);
            m_IsPointerInside = false;
            m_IsPointerDown = false;
            m_HasSelection = false;
        }

        protected virtual void DoStateTransition(SelectionState state)
        {
            if (!gameObject.activeInHierarchy) return;

            switch (m_Transition)
            {
                case Transition.ColorTint:
                    if (m_TargetGraphic == null) return;
                    var tint = state switch
                    {
                        SelectionState.Highlighted => m_Colors.highlightedColor,
                        SelectionState.Pressed => m_Colors.pressedColor,
                        SelectionState.Selected => m_Colors.selectedColor,
                        SelectionState.Disabled => m_Colors.disabledColor,
                        _ => m_Colors.normalColor,
                    };
                    // Instant apply (see class doc) — the original CrossFades over fadeDuration.
                    m_TargetGraphic.color = tint * m_Colors.colorMultiplier;
                    break;

                case Transition.SpriteSwap:
                    if (m_TargetGraphic is not Image img) return;
                    img.overrideSprite = state switch
                    {
                        SelectionState.Highlighted => m_HighlightedSprite,
                        SelectionState.Pressed => m_PressedSprite,
                        SelectionState.Selected => m_SelectedSprite,
                        SelectionState.Disabled => m_DisabledSprite,
                        _ => null,
                    };
                    break;
            }
        }

        /// <summary>Makes this the event system's current selection (original contract).</summary>
        public virtual void Select()
        {
            if (EventSystem.current == null) return;
            EventSystem.current.SetSelectedGameObject(gameObject);
        }

        // ── navigation (original contract) ───────────────────────────

        public Selectable FindSelectableOnLeft()
        {
            if (m_Navigation.mode == Navigation.Mode.Explicit) return m_Navigation.selectOnLeft;
            if (((int)m_Navigation.mode & (int)Navigation.Mode.Horizontal) != 0)
                return FindSelectable(transform.rotation * Vector3.left);
            return null;
        }

        public Selectable FindSelectableOnRight()
        {
            if (m_Navigation.mode == Navigation.Mode.Explicit) return m_Navigation.selectOnRight;
            if (((int)m_Navigation.mode & (int)Navigation.Mode.Horizontal) != 0)
                return FindSelectable(transform.rotation * Vector3.right);
            return null;
        }

        public Selectable FindSelectableOnUp()
        {
            if (m_Navigation.mode == Navigation.Mode.Explicit) return m_Navigation.selectOnUp;
            if (((int)m_Navigation.mode & (int)Navigation.Mode.Vertical) != 0)
                return FindSelectable(transform.rotation * Vector3.up);
            return null;
        }

        public Selectable FindSelectableOnDown()
        {
            if (m_Navigation.mode == Navigation.Mode.Explicit) return m_Navigation.selectOnDown;
            if (((int)m_Navigation.mode & (int)Navigation.Mode.Vertical) != 0)
                return FindSelectable(transform.rotation * Vector3.down);
            return null;
        }

        /// <summary>
        /// The best selectable in world direction <paramref name="dir"/> — original
        /// scoring: candidates ahead of this rect's edge, ranked by dot ÷ distance²
        /// (favors on-axis over merely-near).
        /// </summary>
        public Selectable FindSelectable(Vector3 dir)
        {
            dir = dir.normalized;
            Vector3 localDir = Quaternion.Inverse(transform.rotation) * dir;
            Vector3 searchOrigin = transform.TransformPoint(GetPointOnRectEdge(transform as RectTransform, localDir));

            float maxScore = float.NegativeInfinity;
            Selectable bestPick = null;

            foreach (var candidate in s_Selectables)
            {
                if (candidate == this) continue;
                if (!candidate.IsInteractable() || candidate.m_Navigation.mode == Navigation.Mode.None) continue;
                if (!candidate.gameObject.activeInHierarchy || !candidate.enabled) continue;

                var candidateRect = candidate.transform as RectTransform;
                Vector3 candidateCenter = candidateRect != null ? (Vector3)candidateRect.rect.center : Vector3.zero;
                Vector3 toCandidate = candidate.transform.TransformPoint(candidateCenter) - searchOrigin;

                float dot = Vector3.Dot(dir, toCandidate);
                if (dot <= 0f) continue;

                float score = dot / toCandidate.sqrMagnitude;
                if (score > maxScore)
                {
                    maxScore = score;
                    bestPick = candidate;
                }
            }
            return bestPick;
        }

        static Vector3 GetPointOnRectEdge(RectTransform rect, Vector3 dir)
        {
            if (rect == null) return Vector3.zero;
            var r = rect.rect;
            var dir2 = new Vector2(dir.x, dir.y);
            if (dir2 != Vector2.zero) dir2 /= Mathf.Max(Mathf.Abs(dir2.x), Mathf.Abs(dir2.y));
            return r.center + new Vector2(r.width * 0.5f * dir2.x, r.height * 0.5f * dir2.y);
        }

        void Navigate(AxisEventData eventData, Selectable target)
        {
            if (target != null && target.gameObject.activeInHierarchy)
                eventData.selectedObject = target.gameObject;
        }

        public virtual void OnMove(AxisEventData eventData)
        {
            switch (eventData.moveDir)
            {
                case MoveDirection.Left: Navigate(eventData, FindSelectableOnLeft()); break;
                case MoveDirection.Right: Navigate(eventData, FindSelectableOnRight()); break;
                case MoveDirection.Up: Navigate(eventData, FindSelectableOnUp()); break;
                case MoveDirection.Down: Navigate(eventData, FindSelectableOnDown()); break;
            }
        }

        // ── event-system entry points ────────────────────────────────

        public virtual void OnPointerEnter(PointerEventData eventData)
        {
            m_IsPointerInside = true;
            DoStateTransition(currentSelectionState);
        }

        public virtual void OnPointerExit(PointerEventData eventData)
        {
            m_IsPointerInside = false;
            DoStateTransition(currentSelectionState);
        }

        public virtual void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (IsInteractable()) Select(); // original: pressing selects
            m_IsPointerDown = true;
            DoStateTransition(currentSelectionState);
        }

        public virtual void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            m_IsPointerDown = false;
            DoStateTransition(currentSelectionState);
        }

        public virtual void OnSelect(BaseEventData eventData)
        {
            m_HasSelection = true;
            DoStateTransition(currentSelectionState);
        }

        public virtual void OnDeselect(BaseEventData eventData)
        {
            m_HasSelection = false;
            DoStateTransition(currentSelectionState);
        }
    }
}
