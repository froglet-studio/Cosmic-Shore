using CosmicShore.App.Systems.Audio;
using System.Collections;
using UnityEngine;

public class BlockImpact : MonoBehaviour
{
    [SerializeField] private float minSpeed = 30f;
    [SerializeField] private float maxSpeed = 250f;
    [SerializeField] AudioClip ImpactSound;
    private Material material;

    public void HandleImpact(Vector3 velocity)
    {
        // Validate velocity before starting coroutine
        if (float.IsNaN(velocity.x) || float.IsNaN(velocity.y) || float.IsNaN(velocity.z))
        {
            velocity = Vector3.up * minSpeed; // Fallback velocity
        }
        StartCoroutine(ImpactCoroutine(velocity));
    }

    IEnumerator ImpactCoroutine(Vector3 velocity)
    {
        float speed;
        velocity = GeometryUtils.ClampMagnitude(velocity, minSpeed, maxSpeed, out speed);

        material = GetComponent<MeshRenderer>()?.material;
        if (material != null)
        {
            material.SetVector("_velocity", velocity);
        }

        if (ImpactSound != null)
            AudioSystem.Instance.PlaySFXClip(ImpactSound);

        var initialPosition = transform.position;
        var maxDuration = 7f;
        var duration = 0f;

        while (duration <= maxDuration && this != null && material != null)
        {
            duration += Time.deltaTime;
            
            // Calculate new position
            Vector3 newPosition = initialPosition + duration * velocity;
            
            // Validate position before applying
            if (!float.IsNaN(newPosition.x) && !float.IsNaN(newPosition.y) && !float.IsNaN(newPosition.z))
            {
                transform.position = newPosition;
            }

            // Update material properties
            material.SetFloat("_ExplosionAmount", speed * duration);
            material.SetFloat("_opacity", 1 - (duration / maxDuration));
            
            yield return null;
        }

        if (this != null && gameObject != null)
        {
            // Get the tag from the object itself since it might be from any of the team pools
            transform.parent.GetComponent<TeamColorPersistentPool>()?.ReturnToPool(gameObject, gameObject.tag);
        }
    }
}
