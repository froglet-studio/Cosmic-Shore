using UnityEngine;

[CreateAssetMenu(fileName = "Trail", menuName = "Create SO/Trail")]
public class SO_Trail_Base : ScriptableObject
{
    [SerializeField]
    private string trailName;
    [SerializeField]
    private float maxHealth; 
    [SerializeField]
    private float fuel; 

    public string TrailName { get => trailName; set => trailName = value; }
    // public float MaxHealth { get => maxHealth; set => maxHealth = value; }
    public float Fuel { get => fuel; set => fuel = value; }
    public float MaxHealth { get => maxHealth; set => maxHealth = value; }
}
