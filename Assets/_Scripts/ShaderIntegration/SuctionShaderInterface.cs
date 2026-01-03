using System.Collections;
using UnityEngine;

namespace CosmicShore
{
    public class SuctionShaderInterface : MonoBehaviour
    {
        public void ApplySuctionToTarget(GameObject target, Vector3 slocation, float duration = 5)
        {
            // Determine Necessary Information
            float inverse_partial = (float)24 / duration;
            int[] face_order = GenerateFaceOrder(target, slocation);
            Material suction_material = new Material(suction_material_base);

            // Encode Face Order Into Vertices
            Vector3 pull_directions1 = new Vector3(face_order[0], face_order[1], face_order[2]);
            Vector3 pull_directions2 = new Vector3(face_order[3], face_order[2], face_order[3]);

            // Pass In Values And Apply Material
            suction_material.SetFloat("InversePartialDuration", inverse_partial);
            suction_material.SetVector("SuctionLocation", slocation);
            suction_material.SetVector("PullDirections1", pull_directions1);
            suction_material.SetVector("PullDirections2", pull_directions2);
            suction_material.SetFloat("StartTime", Time.time);

            MeshRenderer renderer = target.GetComponent<MeshRenderer>();
            renderer.material = suction_material;

            // Set Callback to Remove Shader After Duration
            StartCoroutine(RemoveMaterialAfterDuration(target, suction_material, duration));
        }

        private int[] GenerateFaceOrder(GameObject target, Vector3 slocataion)
        {
            int[] face_order = new int[6];

            // Generate Face Order

            return face_order;
        }

        private IEnumerator RemoveMaterialAfterDuration(GameObject target, Material mat, float duration)
        {
            yield return new WaitForSeconds(duration);

            // Remove Material
        }

        [SerializeField]
        private Material suction_material_base;
    }
}
