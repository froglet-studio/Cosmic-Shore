using System.Collections;
using UnityEngine;

public class BlockImpact : MonoBehaviour
{
    public void HandleImpact(Vector3 velocity, string ID)
    {
        StartCoroutine(ImpactCoroutine(velocity, ID));
    }

    private IEnumerator ImpactCoroutine(Vector3 velocity,string ID)
    {
        var velocityScale = .1f;
        //var positionScale = 1;
        
        Vector3 distance = Vector3.zero;

        Material material = GetComponent<MeshRenderer>().material;

        if (ID == "Player") { 
            material.SetFloat("_playerHit", 1); 
        }
        else
        {
            if (ID == "red") 
            {   
                material.SetFloat("_playerHit", 0); material.SetFloat("_redHit", 1);
            }
            else { 
                material.SetFloat("_playerHit", 0); material.SetFloat("_redHit", 0); 
            }
        }
        
        while (distance.magnitude <= 1000)
        {
            yield return null;
            distance += velocityScale * Time.deltaTime * velocity;
            material.SetVector("_velocity", distance);
            material.SetFloat("_opacity", (1000 - distance.magnitude) / 1000);
            //transform.position += positionScale * distance;
        }

        Destroy(transform.gameObject);
    }
}