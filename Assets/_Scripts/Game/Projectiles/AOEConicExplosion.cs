using System.Collections;
using UnityEngine;

namespace CosmicShore.Game.Projectiles
{
    public class AOEConicExplosion : AOEExplosion
    {
        [SerializeField] float height = 800; // TODO: maybe pull from node diameter
        GameObject coneContainer;

        public override void Initialize(InitializeStruct initStruct)
        {
            SpawnPosition = initStruct.SpawnPosition;
            SpawnRotation = initStruct.SpawnRotation;
            
            AnonymousExplosion = initStruct.AnnonymousExplosion;
            Vessel = initStruct.Vessel;
            if (Vessel == null)
            {
                Debug.LogError("Vessel is not initialized in AOEExplosion!");
                return;
            }

            Team = initStruct.OwnTeam;
            if (Team == Teams.Unassigned)
                Team = Vessel.VesselStatus.Team;

            MaxScale = initStruct.MaxScale;
            MaxScaleVector = new Vector3(MaxScale, MaxScale, height);
            speed = height / (ExplosionDuration * 4);


            Material = new Material(Vessel.VesselStatus.AOEConicExplosionMaterial);
            if (!Material)
                Material = new Material(Vessel.VesselStatus.AOEExplosionMaterial);
            
            if (!coneContainer)
                coneContainer = new GameObject("AOEContainer");
            coneContainer.transform.SetPositionAndRotation(SpawnPosition, SpawnRotation);
            transform.SetParent(coneContainer.transform, false);
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
                GetComponent<SphereCollider>().radius = coneContainer.transform.localScale.x / (Mathf.Clamp(coneContainer.transform.localScale.z, .01f, Mathf.Infinity) * 2);
                Material.SetFloat("_Opacity", Mathf.Clamp((MaxScaleVector - coneContainer.transform.localScale).magnitude / MaxScaleVector.magnitude, 0, 1));
                yield return null;
            }

            Destroy(gameObject);
        }

        /*public override void SetPositionAndRotation(Vector3 position, Quaternion rotation)
        {
            containerPosition = position;
            containerRotation = rotation;
        }*/
    }
}