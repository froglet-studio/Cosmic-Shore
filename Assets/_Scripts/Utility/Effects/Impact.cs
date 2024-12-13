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
        var startTime = Time.fixedTime;
        var elapsedTime = 0f;
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
        
        while (distance.magnitude <= maxDistance || elapsedTime > 5)
        {
            yield return null;
            distance += velocityScale * Time.deltaTime * velocity;
            elapsedTime = Time.fixedTime - startTime;;
            material.SetVector("_velocity", distance);
            material.SetFloat("_opacity", Mathf.Clamp(1 - (distance.magnitude / maxDistance), 0, 1));
            transform.position += positionScale*distance;
        }

        Destroy(material);
        Destroy(transform.gameObject);
    }
}