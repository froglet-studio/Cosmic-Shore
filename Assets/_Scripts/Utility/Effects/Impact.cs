using System.Collections;
using UnityEngine;

public class Impact : MonoBehaviour
{
    public float positionScale;
    public float maxDistance = 3f;

    public void HandleImpact(Vector3 velocity, Material material, string ID)
    {
        StartCoroutine(ImpactCoroutine(velocity, material, ID));
    }

    IEnumerator ImpactCoroutine(Vector3 velocity, Material material, string ID)
    {

        var velocityScale = .07f/positionScale;
        Vector3 distance = Vector3.zero;
        if (ID == "Player")
            material.SetFloat("_player", 1);
        else if (ID == "red")
        {
            material.SetFloat("_player", 0);
            material.SetFloat("_red", 1);
        }
        else
        {
            material.SetFloat("_player", 0);
            material.SetFloat("_red", 0);
        }
        
        velocity = velocity.sqrMagnitude < 2f ? Vector3.one * 2 : velocity;
        while (distance.magnitude <= maxDistance)
        {
            yield return null;
            distance += velocityScale * Time.deltaTime * velocity;
            material.SetVector("_velocity", distance);
            material.SetFloat("_opacity", Mathf.Clamp(1 - (distance.magnitude / maxDistance), 0, 1));
            transform.position += positionScale*distance;
        }

        Destroy(material);
        Destroy(transform.gameObject);
    }
}