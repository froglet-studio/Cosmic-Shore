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
