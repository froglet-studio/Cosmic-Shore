using UnityEngine;

[CreateAssetMenu(fileName = "Spike Spiegel", menuName = "TailGlider/Pilot", order = 1)]
[System.Serializable]
public class SO_Pilot : ScriptableObject
{
    [SerializeField] public string Name;
    [SerializeField] public string CallSign;
    [SerializeField] public string Description;
    [SerializeField] public Sprite Image;
    [SerializeField] public Sprite Icon;
    [SerializeField] public Sprite SelectedIcon;
    [SerializeField] public SO_Ship Ship;
    [SerializeField] public Element PrimaryElement;
    [SerializeField] public int InitialMass;
    [SerializeField] public int InitialCharge;
    [SerializeField] public int InitialSpace;
    [SerializeField] public int InitialTime;
}