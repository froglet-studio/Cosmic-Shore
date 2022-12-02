using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewShip", menuName ="Create Ship")]
public class SO_Ship : ScriptableObject
{
    [SerializeField] string shipName;
    [SerializeField] float maxHealth;
    [SerializeField] float maxFuel;
    [SerializeField] List<CrystalImpactEffect> crystalImpactEffects;
    [SerializeField] List<TrailBlockImpactEffect> trailBlockImpactEffects;

    public string Name { get => shipName; }
    public float MaxHealth { get => maxHealth; }
    public float MaxFuel { get => maxFuel; }
    public List<CrystalImpactEffect> CrystalImpactEffects { get => crystalImpactEffects; }
    public List<TrailBlockImpactEffect> TrailBlockImpactEffects { get => trailBlockImpactEffects; }
}