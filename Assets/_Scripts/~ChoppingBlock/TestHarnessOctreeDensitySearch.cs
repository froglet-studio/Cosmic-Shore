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
        int targetCount = 5;

        Teams[] teams = { Teams.Green, Teams.Red, Teams.Gold };
        foreach (Teams t in teams)
        {
            List<Vector3> explosionTargets = targetNode.GetExplosionTargets(targetCount, t);
            Debug.Log($"Found {explosionTargets.Count} explosion targets in node {targetNode.ID} for team {t}:");
            foreach (Vector3 target in explosionTargets)
            {
                Debug.Log($"Target position: {target}, Block count density: {targetNode.blockOctrees[t].GetBlockDensityAtPosition(target)}");
            }
        }
    }
}