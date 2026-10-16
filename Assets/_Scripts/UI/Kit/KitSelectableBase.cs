using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The interaction half of the style-B kit (Docs/MENU_REDESIGN_B.md): a <see cref="Button"/>
    /// whose every state - normal, hover, press, gamepad focus, disabled - is painted from
    /// <see cref="UIThemeSO"/> tokens instead of Selectable's colour-tint transition.
    ///
    /// <para><b>Why a Button subclass.</b> <see cref="Selectable.DoStateTransition"/> is the one
    /// hook that sees every state change (pointer, navigation, interactable, CanvasGroup) with no
    /// polling, and a subclass keeps <c>onClick</c>, navigation and everything wired to a Button
    /// working unchanged - an existing scene Button can become a kit button by swapping its script
    /// reference, with its serialized fields intact. The stock transition is forced to None: the kit
    /// owns the visuals.</para>
    ///
    /// <para><b>Focus ring.</b> Gamepad and keyboard focus draws a CTA ring (§3: focus is CTA green;
    /// the approved kit mockup: a 2px slivered ring 3px outside the control). A click also selects
    /// a Selectable, so the ring is shown only when the selection did NOT come from a pointer - the
    /// uGUI equivalent of CSS :focus-visible. Selection made by code (ScreenSwitcher.Refocus) shows
    /// it, which is what a pad user landing on a screen needs.</para>
    ///
    /// <para><b>Behind a modal.</b> A CanvasGroup that blocks interaction also reports Disabled. A
    /// control whose OWN interactable flag is still on is drawn Normal in that case, so a screen
    /// behind an open window keeps its look instead of greying out.</para>
    ///
    /// <para><b>Motion</b> is §7 <c>micro</c> (hover, focus) on UNSCALED time - every non-HOME menu
    /// screen runs at timeScale 0.</para>
    /// </summary>
    public abstract class KitSelectableBase : Button
    {
        protected enum VisualState
        {
            Normal = 0,
            Highlighted = 1,
            Pressed = 2,
            Selected = 3,
            Disabled = 4,
        }

        [Header("Kit")]
        [Tooltip("The chrome theme (Docs/STYLE_FOUNDATION.md §11). Unwired falls back to the authored §11 values.")]
        [SerializeField] protected UIThemeSO theme;
        [Tooltip("The focus ring drawn while this control has gamepad / keyboard focus. Optional.")]
        [SerializeField] protected Graphic focusRing;
        [Tooltip("The ring's colour. White by default: selection is never the CTA green (rev 3, 2026-10-16), " +
                 "which stays reserved for the one action a screen wants pressed.")]
        [SerializeField] protected UIColorToken focusRingToken = UIColorToken.TextLight;

        private VisualState _visual;
        private bool _selected;
        private bool _pointerSelected;

        private readonly List<Graphic> _targets = new();
        private readonly List<Color> _goal = new();
        private readonly List<Color> _start = new();
        private float _tweenTime;
        private float _tweenDuration;
        private bool _tweening;

        protected UIThemeSO Theme => theme;
        protected VisualState Visual => _visual;
        protected Color Token(UIColorToken token) => UITheme.Resolve(theme, token);
        protected Color Token(UIColorToken token, float alpha)
        {
            var c = UITheme.Resolve(theme, token);
            c.a *= alpha;
            return c;
        }

        /// <summary>Fill <paramref name="into"/> with the colour every owned graphic wears in
        /// <paramref name="state"/>. Also the place to swap sprites or toggle effects.</summary>
        protected abstract void CollectColors(VisualState state, List<(Graphic graphic, Color color)> into);

        // No OnValidate / Reset override: Selectable's own editor-only OnValidate already re-runs
        // DoStateTransition (so an inspector edit repaints the kit), and those two members exist only
        // in uGUI's EDITOR build, which the offline reference compile cannot see - an override of
        // them would ship unverified. Awake runs in edit mode too (Selectable is ExecuteAlways).
        protected override void Awake()
        {
            base.Awake();
            transition = Transition.None;
        }

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            base.DoStateTransition(state, instant);
            _visual = state switch
            {
                SelectionState.Highlighted => VisualState.Highlighted,
                SelectionState.Pressed => VisualState.Pressed,
                SelectionState.Selected => VisualState.Selected,
                // Interactable but blocked by a CanvasGroup (a modal is open): keep the look.
                SelectionState.Disabled => interactable ? VisualState.Normal : VisualState.Disabled,
                _ => VisualState.Normal,
            };
            Refresh(instant);
        }

        public override void OnSelect(BaseEventData eventData)
        {
            _selected = true;
            _pointerSelected = eventData is PointerEventData;
            base.OnSelect(eventData);
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            _selected = false;
            base.OnDeselect(eventData);
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            // A click on an already-focused control hides the ring, as :focus-visible does.
            _pointerSelected = true;
            base.OnPointerDown(eventData);
        }

        protected override void OnDisable()
        {
            _selected = false;
            _tweening = false;
            base.OnDisable();
        }

        /// <summary>Re-evaluate every owned graphic. Subclasses call this when their own state
        /// (variant, selected, locked) changes.</summary>
        protected void Refresh(bool instant)
        {
            var pairs = ListPool.Get();
            CollectColors(_visual, pairs);
            if (focusRing)
            {
                bool ring = _selected && !_pointerSelected && _visual != VisualState.Disabled;
                pairs.Add((focusRing, Token(focusRingToken, ring ? 1f : 0f)));
            }

            _targets.Clear();
            _goal.Clear();
            _start.Clear();
            foreach (var (g, c) in pairs)
            {
                if (!g) continue;
                _targets.Add(g);
                _goal.Add(c);
                _start.Add(g.color);
            }
            ListPool.Release(pairs);

            _tweenDuration = UITheme.Duration(theme, UIMotionToken.Micro);
            bool snap = instant || !Application.isPlaying || !isActiveAndEnabled || _tweenDuration <= 0f;
            if (snap)
            {
                _tweening = false;
                for (int i = 0; i < _targets.Count; i++) _targets[i].color = _goal[i];
                return;
            }
            _tweenTime = 0f;
            _tweening = true;
        }

        protected virtual void Update()
        {
            if (!_tweening) return;
            _tweenTime += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(_tweenTime / _tweenDuration);
            float eased = 1f - (1f - t) * (1f - t); // §7 micro: OutQuad
            for (int i = 0; i < _targets.Count; i++)
                if (_targets[i]) _targets[i].color = Color.LerpUnclamped(_start[i], _goal[i], eased);
            if (t >= 1f) _tweening = false;
        }

        /// <summary>One scratch list per Refresh, without allocating per state change.</summary>
        private static class ListPool
        {
            private static readonly Stack<List<(Graphic, Color)>> Pool = new();

            public static List<(Graphic, Color)> Get() => Pool.Count > 0 ? Pool.Pop() : new List<(Graphic, Color)>(8);

            public static void Release(List<(Graphic, Color)> list)
            {
                list.Clear();
                Pool.Push(list);
            }
        }
    }
}
