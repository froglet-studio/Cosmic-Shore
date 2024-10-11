using UnityEngine;

[CreateAssetMenu(fileName = "Color Set", menuName = "CosmicShore/ColorSet")]
[System.Serializable]
public class SO_ColorSet : ScriptableObject
{
    [SerializeField] public DomainColorSet JadeColors;
    [SerializeField] public DomainColorSet RubyColors;
    [SerializeField] public DomainColorSet GoldColors;
    [SerializeField] public DomainColorSet BlueColors;
    [SerializeField] public EnvironmentColorSet EnvironmentColors;
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