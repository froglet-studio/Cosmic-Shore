using UnityEngine;
using System.Collections;
using CosmicShore.Game;
using CosmicShore.Soap;
using CosmicShore.Utility;
using CosmicShore.Models.Enums;

/// <summary>
/// Used for testing only (scene TestHarnessOctree).  Prints coordinates for three target sets (one for each color).
/// The scene consists only of one "pumpkin" of each color.  For example, the targets printed for Gold team should be
/// near the Ruby and Jade pumpkins.
/// </summary>
public class TestHarnessOctreeDensitySearch : MonoBehaviour
{
    [SerializeField] private CellRuntimeDataSO cellData;
    
    void Start()
    {
        if (!cellData)
        {
            CSDebug.LogError($"Cell data not found!");
            return;
        }
        
        StartCoroutine(WaitCoroutine());
    }

    IEnumerator WaitCoroutine()
    {
        yield return new WaitForSeconds(3);

        Cell targetNode = cellData.Cell;

        Domains[] teams = { Domains.Jade, Domains.Ruby, Domains.Gold };
        foreach (Domains t in teams)
        {
            Vector3 explosionTarget = targetNode.GetExplosionTarget(t);
            CSDebug.Log($"Found explosion target in node {targetNode.ID} for team {t}:");
            CSDebug.Log($"Target position: {explosionTarget}, Block count density: {targetNode.countGrids[t].GetDensityAtPosition(explosionTarget)}");
        }
    }
}