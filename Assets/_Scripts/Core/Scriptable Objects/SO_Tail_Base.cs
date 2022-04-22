using UnityEngine;

[CreateAssetMenu(fileName = "Tail", menuName = "SO/Tail")]
public class SO_Tail_Base : ScriptableObject
{
    [SerializeField]
    private string tailName;
    //[SerializeField]
    // private float maxHealth; 
    [SerializeField]
    private float energy; //energy supplied while ship is in range

    public string TailName { get => tailName; set => tailName = value; }
    // public float MaxHealth { get => maxHealth; set => maxHealth = value; }
    public float Energy { get => energy; set => energy = value; }
}
