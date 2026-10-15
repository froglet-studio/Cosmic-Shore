using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// One element of the Ark converter's recipe panel (Docs/MENU_REDESIGN_B.md section 16): its numeral,
    /// its crystal, its name, a Ready / Merging / Merged word and a bar that fills over the crystal's own
    /// flight into the omni. The merging row wears the CTA green and bobs; the others rest.
    ///
    /// <para><see cref="ElementalConverterView"/> drives it every frame from
    /// <c>OmniCombinePlayer.ElementProgress</c>; colours are written only when the state changes.</para>
    /// </summary>
    [AddComponentMenu("Cosmic Shore/UI/Converter Recipe Row")]
    public class ConverterRecipeRow : MonoBehaviour
    {
        private const float ReadyArtAlpha = 0.45f;

        private enum State
        {
            None = -1,
            Ready = 0,
            Merging = 1,
            Merged = 2,
        }

        [Tooltip("The chrome theme. Unwired falls back to the authored section 11 values.")]
        [SerializeField] private UIThemeSO theme;
        [Tooltip("\"Ready\", \"Merging\" or \"Merged\" (Label role).")]
        [SerializeField] private KitText stateText;
        [Tooltip("The flight bar (Image, Filled Horizontal).")]
        [SerializeField] private Image fill;
        [Tooltip("The row's border: CTA while merging, SurfaceDark otherwise.")]
        [SerializeField] private Image border;
        [Tooltip("The crystal render: dimmed while it waits.")]
        [SerializeField] private Image art;
        [Tooltip("Bobs the crystal while it merges. Optional.")]
        [SerializeField] private KitAmbient bob;

        private State _state = State.None;

        /// <summary>Draw the row at <paramref name="progress"/> (0 waiting, 1 landed).</summary>
        public void Draw(float progress)
        {
            if (fill) fill.fillAmount = Mathf.Clamp01(progress);
            var state = progress <= 0f ? State.Ready : progress < 1f ? State.Merging : State.Merged;
            if (state == _state) return;
            _state = state;

            if (stateText)
            {
                stateText.SetText(state switch { State.Merging => "Merging", State.Merged => "Merged", _ => "Ready" });
                stateText.SetColorToken(state switch
                {
                    State.Merging => UIColorToken.Cta,
                    State.Merged => UIColorToken.TextLight,
                    _ => UIColorToken.NeutralLightest,
                });
            }
            if (fill) fill.color = UITheme.Resolve(theme, state == State.Merging ? UIColorToken.Cta : UIColorToken.ControlActive);
            if (border) border.color = UITheme.Resolve(theme, state == State.Merging ? UIColorToken.Cta : UIColorToken.SurfaceDark);
            if (art) art.color = new Color(1f, 1f, 1f, state == State.Ready ? ReadyArtAlpha : 1f);
            if (bob) bob.enabled = state == State.Merging;
        }
    }
}
