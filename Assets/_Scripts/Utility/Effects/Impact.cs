using System.Collections;
using UnityEngine;

public class Impact : MonoBehaviour
{
    public float positionScale;
    public float maxDistance = 3f;

    private static readonly int PlayerID = Shader.PropertyToID("_player");
    private static readonly int RedID = Shader.PropertyToID("_red");
    private static readonly int VelocityID = Shader.PropertyToID("_velocity");
    private static readonly int OpacityID = Shader.PropertyToID("_Opacity");

    public void HandleImpact(Vector3 velocity, Material material, string ID)
    {
        StartCoroutine(ImpactCoroutine(velocity, material, ID));
    }

    IEnumerator ImpactCoroutine(Vector3 velocity, Material material, string ID)
    {
        var velocityScale = .07f / positionScale;
        Vector3 distance = Vector3.zero;
        if (ID == "Player")
            material.SetFloat(PlayerID, 1);
        else if (ID == "red")
        {
            material.SetFloat(PlayerID, 0);
            material.SetFloat(RedID, 1);
        }
        else
        {
            material.SetFloat(PlayerID, 0);
            material.SetFloat(RedID, 0);
        }

        velocity = velocity.sqrMagnitude < 2f ? Vector3.one * 2 : velocity;
        while (distance.magnitude <= maxDistance)
        {
            yield return null;
            distance += velocityScale * Time.deltaTime * velocity;
            material.SetVector(VelocityID, distance);
            material.SetFloat(OpacityID, Mathf.Clamp(1 - (distance.magnitude / maxDistance), 0, 1));
            transform.position += positionScale * distance;
        }

        Destroy(material);
        Destroy(transform.gameObject);
    }
}
