using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "Spike Spiegel", menuName = "TailGlider/Pilot", order = 1)]
[System.Serializable]
public class SO_Pilot : ScriptableObject
{
    [SerializeField] public string Name;
    [SerializeField] public string CallSign;
    [SerializeField] public string Description;
    [SerializeField] public Sprite Image;
    [SerializeField] public int InitialMass;
    [SerializeField] public int InitialCharge;
    [FormerlySerializedAs("InitialSpaceTime")]
    [SerializeField] public int InitialSpace;
    [SerializeField] public int InitialTime;
}