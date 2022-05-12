using UnityEngine;

[CreateAssetMenu(fileName = "Trail", menuName = "Create SO/Trail")]
public class SO_Trail_Base : ScriptableObject
{
    [SerializeField]
    private string tailName;
    [SerializeField]
    private float maxHealth; 
    [SerializeField]
    private float intensity; 

    public string TailName { get => tailName; set => tailName = value; }
    // public float MaxHealth { get => maxHealth; set => maxHealth = value; }
    public float Intensity { get => intensity; set => intensity = value; }
    public float MaxHealth { get => maxHealth; set => maxHealth = value; }
}
