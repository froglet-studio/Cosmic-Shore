using CosmicShore.UI;
using TMPro;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The words, look and placement of the player-facing FPS counter (<see cref="CosmicShore.UI.FpsCounter"/>),
    /// which Settings > Display > Show FPS turns on in every build, release included. The counter is built at
    /// runtime because it must sit over every scene, so its text and look live here, in
    /// <c>Resources/FpsCounterConfig.asset</c>, the same way the disconnect notice keeps its copy in
    /// <see cref="DisconnectNoticeConfigSO"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "FpsCounterConfig",
                     menuName = "ScriptableObjects/System/FPS Counter Config", order = 41)]
    public class FpsCounterConfigSO : ScriptableObject
    {
        [Header("Words (empty shows nothing)")]
        [Tooltip("The readout. {fps} is replaced by the frames drawn per second, as a whole number.")]
        public string Template = "{fps} FPS";

        [Header("Measurement")]
        [Tooltip("Seconds of frames averaged into each reading. The text changes once per window, never per frame.")]
        [Min(0.1f)] public float SampleSeconds = 0.5f;

        [Header("Look")]
        [Tooltip("The font. Empty uses TextMesh Pro's default font.")]
        public TMP_FontAsset Font;
        [Tooltip("Font size in canvas units (the canvas is scaled to a 1920 x 1080 reference).")]
        [Min(1f)] public float FontSize = 28f;
        [Tooltip("The chrome theme the text colour comes from. Unwired falls back to the theme's authored values.")]
        public UIThemeSO Theme;
        [Tooltip("Which theme colour the text wears.")]
        public UIColorToken Token = UIColorToken.TextLight;
        [Tooltip("Opacity on top of the token, 0..1.")]
        [Range(0f, 1f)] public float Alpha = 0.85f;

        [Header("Placement")]
        [Tooltip("The screen corner the counter is pinned to: (0, 1) top left, (1, 1) top right, (0, 0) bottom left.")]
        public Vector2 Corner = new(0f, 1f);
        [Tooltip("Distance from that corner, inside the device's safe area, in canvas units.")]
        public Vector2 Margin = new(16f, 12f);
        [Tooltip("Canvas sorting order. High so the counter draws over every menu and HUD.")]
        public int SortingOrder = 32000;
    }
}
