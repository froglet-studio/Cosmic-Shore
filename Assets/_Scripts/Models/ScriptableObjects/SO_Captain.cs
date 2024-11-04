using CosmicShore;
using CosmicShore.Models.Enums;
using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "Spike Spiegel", menuName = "CosmicShore/Captain/Captain", order = 3)]
[System.Serializable]
public class SO_Captain : ScriptableObject
{
    [SerializeField] public string Name;
    [SerializeField] public string Description;
    [SerializeField] public string AIBehaviorDescription;
    [SerializeField] public string Flavor;
    [SerializeField] public Sprite Image;
    [SerializeField] public Sprite HeadshotImage;
    [FormerlySerializedAs("SelectedIcon")]
    [SerializeField] public Sprite IconActive;
    [FormerlySerializedAs("Icon")]
    [SerializeField] public Sprite IconInactive;
    [SerializeField] public SO_Ship Ship;
    [SerializeField] public Element PrimaryElement;
    [SerializeField] public SO_Element Element;
    [SerializeField] public ResourceCollection InitialResourceLevels;
    [SerializeField] public int BasePrice;
}