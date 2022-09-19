using System.Collections;
using UnityEngine;

public class Impact : MonoBehaviour
{
    public float positionScale;
    public float maxDistance = 3f;
    public IEnumerator ImpactCoroutine(Vector3 velocity, Material material,string ID)
    {
        var velocityScale = .07f/positionScale;
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
        
        while (distance.magnitude <= maxDistance)
        {
            yield return null;
            //timeStamp += .001f;
            distance += velocityScale * Time.deltaTime * velocity;
            material.SetVector("_velocity", distance);
            var opac = 1 - (distance.magnitude / 3f);
            material.SetFloat("_opacity", Mathf.Clamp(1 - (distance.magnitude / maxDistance), 0, 1));
            transform.position += positionScale*distance;
        }
        Destroy(material);
        Destroy(transform.gameObject);
    }
}