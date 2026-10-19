using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// One intensity node of the style-B configure tree (Docs/MENU_REDESIGN_B.md section 13.1): a
    /// 150-unit tile with the level's numeral, a state caption, a rising four-bar meter and - while
    /// the level is still to be earned - the lock glyph and the goal that earns it.
    ///
    /// <para>The state is the legacy <see cref="IntensitySelectButton"/>'s, driven by the modal
    /// exactly as before; this class only draws it. The frame, the focus ring and the lock glyph are
    /// the node's <see cref="KitTile"/> (which is also the Button the base class looks up), so the
    /// node takes the kit's selected / locked look and its gamepad ring for free.</para>
    ///
    /// <para>Motion: the meter grows on the std token when the node becomes the selected one (state
    /// motion), and the route stub under it - the line down to the pilots row - is shown only on the
    /// selected node, so the live route always runs from the level that is actually picked.</para>
    /// </summary>
    [AddComponentMenu("Cosmic Shore/UI Kit/Kit Intensity Node")]
    public class KitIntensityNode : IntensitySelectButton
    {
        private const float OutOfRangeAlpha = 0.4f;

        [Header("Style-B Node")]
        [Tooltip("The chrome theme. Unwired falls back to the authored section 11 values.")]
        [SerializeField] private UIThemeSO theme;
        [Tooltip("The node's tile: frame, focus ring, lock glyph. It is also the Button the base class finds.")]
        [SerializeField] private KitTile tile;
        [Tooltip("This node's own group: dims the whole node while the card does not offer the level.")]
        [SerializeField] private CanvasGroup nodeGroup;
        [Tooltip("The level's numeral (Display role).")]
        [SerializeField] private KitText numeralText;
        [Tooltip("\"Intensity\", \"Selected\" or \"Locked\" (Label role).")]
        [SerializeField] private KitText captionText;

        [Header("Meter")]
        [Tooltip("The four bars, shortest first. The first Intensity bars are lit.")]
        [SerializeField] private Image[] bars = { };
        [Tooltip("The meter's group: hidden while locked (the goal takes its place).")]
        [SerializeField] private CanvasGroup meterGroup;
        [Tooltip("Grows the meter when this node becomes the selected one. Optional.")]
        [SerializeField] private KitEntrance meterGrow;

        [Header("Locked")]
        [Tooltip("What earns the level (BodySmall), shown only while locked.")]
        [SerializeField] private KitText goalText;
        [Tooltip("The goal's group.")]
        [SerializeField] private CanvasGroup goalGroup;

        [Header("Route")]
        [Tooltip("The line from this node down to the pilots bus, shown only while this node is selected.")]
        [SerializeField] private CanvasGroup routeStub;

        private bool _wasSelected;

        // The tile's click is the node's Select - wired here rather than as a persistent call, so
        // the prefab carries no inspector wiring to drift.
        private void Awake()
        {
            if (tile) tile.onClick.AddListener(Select);
        }

        protected override void DrawLevel(int intensity)
        {
            if (numeralText) numeralText.SetText(intensity.ToString());
            Redraw();
        }

        protected override void DrawSelected(bool selected) => Redraw();
        protected override void DrawActive(bool active) => Redraw();
        protected override void DrawLocked(bool locked) => Redraw();

        private void Redraw()
        {
            bool locked = IsLocked && IsActiveLevel;
            bool selected = IsSelected && IsActiveLevel && !locked;

            if (tile)
            {
                tile.IsSelected = selected;
                tile.IsLocked = locked;
            }
            if (nodeGroup) nodeGroup.alpha = IsActiveLevel ? 1f : OutOfRangeAlpha;

            if (captionText)
                captionText.SetText(locked ? "Locked" : selected ? "Selected" : "Intensity");

            KitVisibility.SetShown(meterGroup, !locked);
            for (int i = 0; i < bars.Length; i++)
            {
                if (!bars[i]) continue;
                bool lit = i < Intensity;
                var token = !lit ? UIColorToken.SurfaceDark
                    : selected ? UIColorToken.TextLight
                    : UIColorToken.NeutralLightest;
                bars[i].color = UITheme.Resolve(theme, token);
            }

            KitVisibility.SetShown(goalGroup, locked);
            if (goalText && locked)
                goalText.SetText(string.IsNullOrEmpty(LockedGoal) ? $"Play Intensity {Intensity - 1} to unlock" : LockedGoal);

            KitVisibility.SetShown(routeStub, selected);

            if (selected && !_wasSelected && meterGrow && Application.isPlaying && isActiveAndEnabled)
                meterGrow.Play();
            _wasSelected = selected;
        }
    }
}
