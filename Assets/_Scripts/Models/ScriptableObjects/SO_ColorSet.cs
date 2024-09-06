using UnityEditor;
using UnityEngine;

[CreateAssetMenu(fileName = "Color Set", menuName = "CosmicShore/ColorSet")]
[System.Serializable]
public class SO_ColorSet : ScriptableObject
{
    [SerializeField] public DomainColorSet JadeColors;
    [SerializeField] public DomainColorSet RubyColors;
    [SerializeField] public DomainColorSet GoldColors;
    [SerializeField] public DomainColorSet BlueColors;
    [ColorUsage(true, true)] [SerializeField] public Color LightColor;
    [ColorUsage(true, true)] [SerializeField] public Color DarkColor;
    [ColorUsage(true, true)] [SerializeField] public Color BrightCTA;
    [ColorUsage(true, true)] [SerializeField] public Color DarkCTA;
    [ColorUsage(true, true)] [SerializeField] public Color Danger;
}

[System.Serializable]
public class DomainColorSet
{
    [ColorUsage(true, true)] [SerializeField] public Color OutsideBlockColor;
    [ColorUsage(true, true)] [SerializeField] public Color ShieldedOutsideBlockColor;
    [ColorUsage(true, true)] [SerializeField] public Color SuperShieldedOutsideBlockColor;
    [ColorUsage(true, true)] [SerializeField] public Color InsideBlockColor;
    [ColorUsage(true, true)] [SerializeField] public Color ShieldedInsideBlockColor;
    [ColorUsage(true, true)] [SerializeField] public Color SuperShieldedInsideBlockColor;
}