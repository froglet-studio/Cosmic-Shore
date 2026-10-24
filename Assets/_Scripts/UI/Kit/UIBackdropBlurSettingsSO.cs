using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// The frosted glass's settings (<see cref="UIBackdropBlur"/>, Docs/MENU_REDESIGN_B.md §62). One asset, loaded from
    /// <c>Resources/UIBackdropBlurSettings</c> (written by <c>Tools/Build/author_ui_lattice.py</c>). With the blur off
    /// or below <see cref="MinQualityLevel"/>, every window falls back to its dark veil under the hexagons.
    /// </summary>
    [CreateAssetMenu(fileName = "UIBackdropBlurSettings", menuName = "ScriptableObjects/UI/Backdrop Blur Settings")]
    public class UIBackdropBlurSettingsSO : ScriptableObject
    {
        [Header("Frosted glass")]
        [Tooltip("Off: no blur pass at all; windows show their dark veil under the hexagons instead of frosted glass.")]
        [SerializeField] bool enabled = true;
        [Tooltip("The blurred copy is the screen divided by this (4 = a quarter of the width and height).")]
        [SerializeField, Range(1, 16)] int downsample = 4;
        [Tooltip("Blur passes at that size. More is softer and costs a little more.")]
        [SerializeField, Range(1, 6)] int passes = 2;
        [Tooltip("Quality levels below this (Project Settings > Quality, 0 = the first) skip the blur.")]
        [SerializeField, Min(0)] int minQualityLevel = 1;
        [Tooltip("BackdropBlur.shader. Referenced here so the build includes it.")]
        [SerializeField] Shader blurShader;

        public bool Enabled => enabled;
        public int Downsample => downsample;
        public int Passes => passes;
        public int MinQualityLevel => minQualityLevel;
        public Shader BlurShader => blurShader;
    }
}
