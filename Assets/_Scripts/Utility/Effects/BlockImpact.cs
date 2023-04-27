using System.Collections;
using UnityEngine;

public class BlockImpact : MonoBehaviour
{
    public void HandleImpact(Vector3 velocity, Teams team)
    {
        StartCoroutine(ImpactCoroutine(velocity, team));
    }

    private IEnumerator ImpactCoroutine(Vector3 velocity, Teams team)
    {
        
        Vector3 distance = Vector3.zero;

        Material material = GetComponent<MeshRenderer>().material;

        if (team == Teams.Green) { 
            material.SetFloat("_playerHit", 1); 
        }
        else
        {
            if (team == Teams.Red) 
            {   
                material.SetFloat("_playerHit", 0); material.SetFloat("_redHit", 1);
            }
            else {
                material.SetFloat("_playerHit", 0); material.SetFloat("_redHit", 0); 
            }
        }
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