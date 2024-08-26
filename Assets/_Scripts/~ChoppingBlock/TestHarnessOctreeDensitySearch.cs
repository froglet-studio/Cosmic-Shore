using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Used for testing only (scene TestHarnessOctree).  Prints coordinates for three target sets (one for each color).
/// The scene consists only of one "pumpkin" of each color.  For example, the targets printed for Gold team should be
/// near the Ruby and Jade pumpkins.
/// </summary>
public class TestHarnessOctreeDensitySearch : MonoBehaviour
{
    void Start()
    {
        StartCoroutine(WaitCoroutine());
    }

    IEnumerator WaitCoroutine()
    {
        yield return new WaitForSeconds(3);
        
        Node targetNode = NodeControlManager.Instance.GetNearestNode(transform.position);

        Teams[] teams = { Teams.Jade, Teams.Ruby, Teams.Gold };
        foreach (Teams t in teams)
        {
            Vector3 explosionTarget = targetNode.GetExplosionTarget(t);
            Debug.Log($"Found explosion target in node {targetNode.ID} for team {t}:");
            Debug.Log($"Target position: {explosionTarget}, Block count density: {targetNode.countGrids[t].GetDensityAtPosition(explosionTarget)}");
        }
    }
}