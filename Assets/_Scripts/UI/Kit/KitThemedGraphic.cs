using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Paints one <see cref="Graphic"/> (Image, TMP text, RawImage) from a <see cref="UIThemeSO"/>
    /// colour token - the style-B kit's answer to "colours come from UIThemeSO, never literals"
    /// (Docs/MENU_REDESIGN_B.md). Use it for STATIC chrome: a panel fill, a hairline divider, a
    /// section label. Anything with interaction states belongs to <see cref="KitButton"/> or
    /// <see cref="KitTile"/>, which own every graphic they tint.
    ///
    /// Runs in edit mode so a prefab shows its real colours in the editor, and re-applies on
    /// enable so a theme edit reaches every instance the next time it is shown.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Graphic))]
    [AddComponentMenu("Cosmic Shore/UI Kit/Kit Themed Graphic")]
    public class KitThemedGraphic : MonoBehaviour
    {
        [Header("Theme")]
        [Tooltip("The chrome theme (Docs/STYLE_FOUNDATION.md §11). Unwired falls back to the authored §11 values.")]
        [SerializeField] private UIThemeSO theme;
        [Tooltip("Which §11 colour role this graphic wears.")]
        [SerializeField] private UIColorToken token = UIColorToken.TextLight;
        [Tooltip("Opacity on top of the token, 0..1. A transparent button's fill is its role at 0.12.")]
        [Range(0f, 1f)]
        [SerializeField] private float alpha = 1f;

        private Graphic _graphic;

        public UIColorToken Token => token;

        /// <summary>Switch the role (and optionally the opacity) at runtime.</summary>
        public void SetToken(UIColorToken newToken, float newAlpha = 1f)
        {
            token = newToken;
            alpha = Mathf.Clamp01(newAlpha);
            Apply();
        }

        private void OnEnable() => Apply();

        private void OnValidate() => KitEditorDefer.Run(this, Apply);

        public void Apply()
        {
            if (!_graphic && !TryGetComponent(out _graphic)) return;
            var c = UITheme.Resolve(theme, token);
            c.a *= alpha;
            _graphic.color = c;
        }
    }
}
