using System.Collections;
using UnityEngine;

public class BlockImpact : MonoBehaviour
{
    public IEnumerator ImpactCoroutine(Vector3 velocity, Material material,string ID)
    {
        var velocityScale = 5f;
        float timeStamp = 0;

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
        
        while (timeStamp <= 100)
        {
            yield return null;
            timeStamp += .001f;
            material.SetVector("_velocity", velocityScale*timeStamp*velocity);
            material.SetFloat("_opacity", (1-timeStamp));
            transform.position += (velocityScale * timeStamp * velocity);
        }
        Destroy(material);
        Destroy(transform.gameObject);
    }
}