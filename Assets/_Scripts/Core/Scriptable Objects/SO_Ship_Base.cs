using UnityEngine;

[CreateAssetMenu(fileName = "Ship", menuName ="SO/Ship")]
public class SO_Ship_Base : ScriptableObject
{
    [SerializeField]
    private string shipName;
    [SerializeField]
    private float maxHealth;
    [SerializeField]
    private float maxEnergy; //fuel used to move

    public string ShipName { get => shipName; set => shipName = value; }
    public float MaxHealth { get => maxHealth; set => maxHealth = value; }
    public float MaxEnergy { get => maxEnergy; set => maxEnergy = value; }
}
