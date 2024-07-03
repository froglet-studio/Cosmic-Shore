using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class RKDebug : MonoBehaviour
{
    void Start()
    {
        StartCoroutine(WaitCoroutine());
    }

    IEnumerator WaitCoroutine()
    {
        yield return new WaitForSeconds(3);
        // Start octree search
        Node targetNode = NodeControlManager.Instance.GetNearestNode(transform.position);
        int targetCount = 3;
        List<Vector3> explosionTargets = targetNode.GetExplosionTargets(targetCount);
        Debug.Log($"Found {explosionTargets.Count} explosion targets in node {targetNode.ID}:");
        foreach (Vector3 target in explosionTargets)
        {
            Debug.Log($"Target position: {target}, Block count density: {targetNode.blockOctree.GetBlockDensityAtPosition(target)}");
        }
    }
}