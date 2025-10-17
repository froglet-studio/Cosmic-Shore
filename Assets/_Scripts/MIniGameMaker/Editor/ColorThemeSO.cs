using UnityEngine;

namespace CosmicShore.Tools.MiniGameMaker
{
    [CreateAssetMenu(fileName = "ColorTheme", menuName = "CosmicShore/Editor/Color Theme")]
    public sealed class ColorThemeSO : ScriptableObject
    {
        [Header("Text")]
        [SerializeField] private Color titleText     = new(0.85f, 0.90f, 1f, 1f);
        [SerializeField] private Color subtitleText  = new(0.70f, 0.78f, 0.95f, 1f);
        [SerializeField] private Color bodyText      = Color.white;

        [Header("Accents")]
        [SerializeField] private Color accent        = new(0.25f, 0.65f, 1f, 1f);
        [SerializeField] private Color warning       = new(1f, 0.6f, 0.2f, 1f);
        [SerializeField] private Color success       = new(0.3f, 0.85f, 0.4f, 1f);

        public Color TitleText    => titleText;
        public Color SubtitleText => subtitleText;
        public Color BodyText     => bodyText;

        public Color Accent       => accent;
        public Color Warning      => warning;
        public Color Success      => success;
    }
}