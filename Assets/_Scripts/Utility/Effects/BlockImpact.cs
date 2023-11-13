using System.Collections;
using UnityEngine;
using CosmicShore.Core.HangerBuilder;

public class BlockImpact : MonoBehaviour
{
    public void HandleImpact(Vector3 velocity)
    {
        StartCoroutine(ImpactCoroutine(velocity));
    }
    Material material;

    private IEnumerator ImpactCoroutine(Vector3 velocity)
    {
        
        Vector3 distance = Vector3.zero;

        material = gameObject.GetComponent<MeshRenderer>().material;

        var initialPosition = transform.position;
        var maxDuration = 7;
        var duration = 0f;
        while (duration <= maxDuration)
        {
            yield return null;
            duration += Time.deltaTime;
            distance += Time.deltaTime * velocity;
            material.SetVector("_velocity", distance);
            material.SetFloat("_opacity", 1 - (duration / maxDuration));
          
            transform.position = initialPosition + distance;
        }

        Destroy(transform.gameObject);
    }
}