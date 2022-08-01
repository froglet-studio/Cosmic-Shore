using System.Collections;
using UnityEngine;

public class BlockImpact : MonoBehaviour
{
    public IEnumerator ImpactCoroutine(Vector3 velocity, Material material,string ID)
    {
        var velocityScale = 100f;
        var positionScale = 1;
        
        Vector3 distance = Vector3.zero;

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
        
        //while (timeStamp <= 100)
        //{
        //    yield return null;
        //    timeStamp += .001f;
        //    material.SetVector("_velocity", velocityScale*timeStamp*velocity);
        //    material.SetFloat("_opacity", (1-timeStamp));
        //    transform.position += (velocityScale * timeStamp * velocity);
        //}
        while (distance.magnitude <= 1000)
        {
            yield return null;
            //timeStamp += .001f;
            distance += velocityScale * Time.deltaTime * velocity;
            material.SetVector("_velocity", distance);
            material.SetFloat("_opacity", (1000 - distance.magnitude) / 1000);
            transform.position += positionScale * distance;
        }
        Destroy(material);
        Destroy(transform.gameObject);
    }
}