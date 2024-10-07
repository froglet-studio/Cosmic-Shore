using UnityEngine;
using System;
using CosmicShore.Environment.FlowField;

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
        //Vector3 randomposition = cell.transform.position + UnityEngine.Random.insideUnitSphere * spawnRadius;
        //Crystal newcrystal = cell.spawncrystal(randomposition);

        //if (newcrystal != null)
        //{
        //    newcrystal.setorigin(cell.transform.position);
        //    cell.additem(newcrystal);
        //}
        //else
        //{
        //    debug.logwarning("failed to spawn extra omnicrystal in cell: " + cell.name);
        //}
    }
}
