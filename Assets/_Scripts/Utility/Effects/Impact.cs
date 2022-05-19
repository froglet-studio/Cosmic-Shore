using System.Collections;
using UnityEngine;

public class Impact : MonoBehaviour
{
    public float positionScale;
    public IEnumerator ImpactCoroutine(Vector3 velocity, Material material,string ID)
    {
        var velocityScale = .05f;
        Vector3 distance = Vector3.zero;

        if (ID == "Player") { 
            material.SetFloat("_player", 1);
        }
        else
        {
            if (ID == "red") {
                material.SetFloat("_player", 0); material.SetFloat("_red", 1);
            }
            else {
                material.SetFloat("_player", 0); material.SetFloat("_red", 0);
            }
        }
        
        while (distance.magnitude <= 1000)
        {
            yield return new WaitForSeconds(.01f);
            //timeStamp += .001f;
            distance += velocityScale * Time.deltaTime * velocity;
            material.SetVector("_velocity", distance);
            material.SetFloat("_opacity", (1000- distance.magnitude)/1000);
            transform.position += positionScale*distance;
        }
        Destroy(material);
        Destroy(transform.gameObject);
    }
}