using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewShip", menuName ="Create Ship")]
public class SO_Ship : ScriptableObject
{
    [SerializeField] string shipName;
    [SerializeField] float maxHealth;
    [SerializeField] float maxFuel;
    [SerializeField] List<Ship.CrystalImpactEffect> crystalImpactEffects;
    [SerializeField] List<Ship.TrailBlockImpactEffect> trailBlockImpactEffects;

    public string Name { get => shipName; }
    public float MaxHealth { get => maxHealth; }
    public float MaxFuel { get => maxFuel; }
    public List<Ship.CrystalImpactEffect> CrystalImpactEffects { get => crystalImpactEffects; }
    public List<Ship.TrailBlockImpactEffect> TrailBlockImpactEffects { get => trailBlockImpactEffects; }
}