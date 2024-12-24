using System.Collections;
using UnityEngine;

public class BlockImpact : MonoBehaviour
{
    Material material;
    float minSpeed = 30f;
    float maxSpeed = 250f;

    public void HandleImpact(Vector3 velocity)
    {
        StartCoroutine(ImpactCoroutine(velocity));
    }

    IEnumerator ImpactCoroutine(Vector3 velocity)
    {
        
        Vector3 distance = Vector3.zero;
        float speed;
        velocity = GeometryUtils.ClampMagnitude(velocity, minSpeed, maxSpeed, out speed);

        material = gameObject.GetComponent<MeshRenderer>().material;
        material.SetVector("_velocity", velocity);

        var initialPosition = transform.position;
        var maxDuration = 7;
        var duration = 0f;

        while (duration <= maxDuration)
        {
            yield return null;
            duration += Time.deltaTime;
            distance = duration * velocity;
            var explosionAmount = speed * duration;
            material.SetFloat("_ExplosionAmount", explosionAmount);
            material.SetFloat("_opacity", 1 - (duration / maxDuration));
            transform.position = initialPosition + distance;
        }

        Destroy(material);
        Destroy(transform.gameObject);
    }
}