using UnityEngine;
using System;

[Serializable]
public class ExtraOmniCrystals : CellModifier
{
    [SerializeField] private int additionalCrystals = 1;
    [SerializeField] private float spawnRadius = 50f;

    public override void Apply(Node cell)
    {
        for (int i = 0; i < additionalCrystals; i++)
        {
            SpawnExtraCrystal(cell);
        }
    }

    private void SpawnExtraCrystal(Node cell)
    {
        //Vector3 randomPosition = cell.transform.position + System.Random.insideUnitSphere * spawnRadius;
        //Crystal newCrystal = cell.SpawnCrystal(randomPosition);

        //if (newCrystal != null)
        //{
        //    newCrystal.SetOrigin(cell.transform.position);
        //    cell.AddItem(newCrystal);
        //}
        //else
        //{
        //    Debug.LogWarning("Failed to spawn extra OmniCrystal in cell: " + cell.name);
        //}
    }
}
