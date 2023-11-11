using System.Collections;
using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game.Projectiles
{
    public class AOEConicExplosion : AOEExplosion
    {
        [SerializeField] float height = 800; // TODO: maybe pull from node diameter
        GameObject coneContainer;
        Vector3 containerPosition;
        Quaternion containerRotation;

        protected override void Start()
        {
            base.Start();
            Material = new Material(Ship.AOEConicExplosionMaterial);
            coneContainer = new GameObject("ExplosionCone");
            coneContainer.transform.SetParent(container.transform, false);
            coneContainer.transform.SetPositionAndRotation(containerPosition, containerRotation);
            transform.SetParent(coneContainer.transform, false);
            MaxScaleVector = new Vector3(MaxScale, MaxScale, height);
            speed = height / (ExplosionDuration * 4);
        }

        protected override IEnumerator ExplodeCoroutine()
        {
            yield return new WaitForSeconds(ExplosionDelay);

            if (TryGetComponent<MeshRenderer>(out var meshRenderer))
                meshRenderer.material = Material;

            var elapsedTime = 0f;
            while (elapsedTime < ExplosionDuration)
            {
                elapsedTime += Time.deltaTime;
                var lerpAmount = Mathf.Sin((elapsedTime / ExplosionDuration) * PI_OVER_TWO);
                coneContainer.transform.localScale = Vector3.Lerp(Vector3.zero, MaxScaleVector, lerpAmount);
                GetComponent<SphereCollider>().radius = coneContainer.transform.localScale.x / (coneContainer.transform.localScale.z * 2);
                Material.SetFloat("_Opacity", Mathf.Clamp((MaxScaleVector - coneContainer.transform.localScale).magnitude / MaxScaleVector.magnitude, 0, 1));
                yield return null;
            }

            Destroy(gameObject);
        }

        public override void SetPositionAndRotation(Vector3 position, Quaternion rotation)
        {
            containerPosition = position;
            containerRotation = rotation;
        }
    }
}