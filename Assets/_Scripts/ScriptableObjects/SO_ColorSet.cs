using UnityEngine;
using CosmicShore.Data;

namespace CosmicShore.ScriptableObjects
{
    [CreateAssetMenu(fileName = "Color Set", menuName = "ScriptableObjects/ColorSet")]
    [System.Serializable]
    public class SO_ColorSet : ScriptableObject
    {
        [SerializeField] public DomainColorSet JadeColors;
        [SerializeField] public DomainColorSet RubyColors;
        [SerializeField] public DomainColorSet GoldColors;
        [SerializeField] public DomainColorSet BlueColors;
        [SerializeField] public EnvironmentColorSet EnvironmentColors;

        public bool TryGetColorSetByDomain(Domains domain,  out DomainColorSet colorSet)
        {
            colorSet = domain switch
            {
                Domains.Jade => JadeColors,
                Domains.Ruby => RubyColors,
                Domains.Gold => GoldColors,
                Domains.Blue => BlueColors,
                _ => null
            };

            if (colorSet != null)
                return true;

            return false;
        }

        /// <summary>
        /// The single representative domain color for flat UI surfaces (scoreboard banner,
        /// player score cards, in-game HUD entries). Returns the domain's
        /// <see cref="DomainColorSet.TrailHighlightColor"/> - the same vivid color players
        /// see on that domain's vessel trails - so UI matches what's on the field. Neutral
        /// gray for domains with no color set.
        /// </summary>
        public Color GetDomainUIColor(Domains domain) =>
            TryGetColorSetByDomain(domain, out var colorSet) ? colorSet.TrailHighlightColor : Color.gray;

        /// <summary>
        /// The per-domain accent for translucent flat-UI card tints (Maelstrom round/player/summary
        /// cards, Connecting-panel domain rank) - deliberately brighter than
        /// <see cref="DomainColorSet.TrailHighlightColor"/> and alpha-tinted so card backgrounds stay
        /// translucent over the scene. Falls back to <see cref="GetDomainUIColor"/> when the accent is
        /// unauthored (alpha 0), so color sets without accents keep the unified domain UI color.
        /// </summary>
        public Color GetDomainUIAccentColor(Domains domain) =>
            TryGetColorSetByDomain(domain, out var colorSet) && colorSet.UIAccentColor.a > 0f
                ? colorSet.UIAccentColor
                : GetDomainUIColor(domain);
    }

    [System.Serializable]
    public class DomainColorSet
    {
        [ColorUsage(true, true)] [SerializeField] public Color ShipColor1;
        [ColorUsage(true, true)] [SerializeField] public Color ShipColor2;
        [ColorUsage(true, true)] [SerializeField] public Color OutsideBlockColor;
        [ColorUsage(true, true)] [SerializeField] public Color ShieldedOutsideBlockColor;
        [ColorUsage(true, true)] [SerializeField] public Color SuperShieldedOutsideBlockColor;
        [ColorUsage(true, true)] [SerializeField] public Color InsideBlockColor;
        [ColorUsage(true, true)] [SerializeField] public Color ShieldedInsideBlockColor;
        [ColorUsage(true, true)] [SerializeField] public Color SuperShieldedInsideBlockColor;
        [ColorUsage(true, true)] [SerializeField] public Color AOETextureColor;
        [ColorUsage(true, true)] [SerializeField] public Color AOEFresnelColor;
        [ColorUsage(true, true)] [SerializeField] public Color AOEConicColor;
        [ColorUsage(true, true)] [SerializeField] public Color AOEConicEdgeColor;
        [ColorUsage(true, true)] [SerializeField] public Color SpikeLightColor;
        [ColorUsage(true, true)] [SerializeField] public Color SpikeDarkColor;
        [ColorUsage(true, true)] [SerializeField] public Color SkimmerColor;
        [ColorUsage(true, true)] [SerializeField] public Color DullCrystalColor;
        [ColorUsage(true, true)] [SerializeField] public Color BrightCrystalColor;
        [ColorUsage(true, true)] [SerializeField] public Color TrailHighlightColor;
        [ColorUsage(true, true)] [SerializeField] public Color TrailCoreColor;
        [Tooltip("Translucent flat-UI accent (Maelstrom cards, Connecting-panel rank). Brighter than " +
                 "TrailHighlightColor, alpha-tinted for card backgrounds. Alpha 0 = unauthored, falls " +
                 "back to the unified domain UI color (TrailHighlightColor).")]
        [SerializeField] public Color UIAccentColor;
    }

    [System.Serializable]
    public class EnvironmentColorSet
    {
        [ColorUsage(true, true)] [SerializeField] public Color SkyColor;
        [ColorUsage(true, true)] [SerializeField] public Color LightColor;
        [ColorUsage(true, true)] [SerializeField] public Color DarkColor;
        [ColorUsage(true, true)] [SerializeField] public Color BrightCTA;
        [ColorUsage(true, true)] [SerializeField] public Color DarkCTA;
        [ColorUsage(true, true)] [SerializeField] public Color Danger;
    }
}
