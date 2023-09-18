using UnityEngine;

[CreateAssetMenu(fileName = "Spike Spiegel", menuName = "CosmicShore/Pilot", order = 2)]
[System.Serializable]
public class SO_Pilot : ScriptableObject
{
    [SerializeField] public string Name;
    [SerializeField] public string Description;
    [SerializeField] public Sprite Image;
    [SerializeField] public Sprite Icon;
    [SerializeField] public Sprite SelectedIcon;
    [SerializeField] public SO_Ship Ship;
    [SerializeField] public Element PrimaryElement;
    [SerializeField] public float InitialMass;
    [SerializeField] public float InitialCharge;
    [SerializeField] public float InitialSpace;
    [SerializeField] public float InitialTime;
}