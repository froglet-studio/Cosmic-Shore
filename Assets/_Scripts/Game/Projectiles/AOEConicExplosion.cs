using System;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace CosmicShore.Game.Projectiles
{
    public class AOEConicExplosion : AOEExplosion
    {
        [SerializeField] private float height = 800f;
        [SerializeField] protected GameObject coneContainer;

        public override void Initialize(InitializeStruct initStruct)
        {
            AnonymousExplosion = initStruct.AnnonymousExplosion;
            Vessel = initStruct.Vessel;

            if (Vessel == null)
            {
                Debug.LogError("Vessel is not initialized in AOEConicExplosion!");
                return;
            }

            Domain = initStruct.OwnDomain;
            if (Domain == Domains.Unassigned)
                Domain = Vessel.VesselStatus.Domain;

            MaxScale = initStruct.MaxScale;
            MaxScaleVector = new Vector3(MaxScale, MaxScale, height);

            speed = height / (ExplosionDuration * 4);

            // Clone material so opacity change doesn't affect shared asset
            Material = new Material(Vessel.VesselStatus.AOEConicExplosionMaterial);
            if (!Material)
                Material = new Material(Vessel.VesselStatus.AOEExplosionMaterial);

            if (!coneContainer)
                coneContainer = new GameObject("AOEContainer");

            coneContainer.transform.SetPositionAndRotation(initStruct.SpawnPosition, initStruct.SpawnRotation);

            // Parent our object to the container.
            // Euler(-90,0,0) rotates the cone mesh so its apex (mesh +Y)
            // points along -Z in container space. The Z+0.5 offset then
            // places the apex at the container origin (the spawn point),
            // with the cone opening forward along +Z.
            transform.SetParent(coneContainer.transform, false);
            transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            transform.localPosition = new Vector3(0f, 0f, 0.5f);

            // create CTS for explosion
            explosionCts = new CancellationTokenSource();
        }

        protected override async UniTaskVoid ExplodeAsync(CancellationToken ct)
        {
            try
            {
                await UniTask.Delay(
                    System.TimeSpan.FromSeconds(ExplosionDelay),
                    DelayType.DeltaTime,
                    PlayerLoopTiming.Update,
                    ct);

                if (TryGetComponent<MeshRenderer>(out var meshRenderer))
                    meshRenderer.material = Material;

                float elapsed = 0f;

                var sphereCol = GetComponent<SphereCollider>();

                while (elapsed < ExplosionDuration)
                {
                    ct.ThrowIfCancellationRequested();

                    elapsed += Time.deltaTime;
                    float t = elapsed / ExplosionDuration;
                    float lerp = Mathf.Sin(t * PI_OVER_TWO);

                    // Scale cone
                    coneContainer.transform.localScale =
                        Vector3.Lerp(Vector3.zero, MaxScaleVector, lerp);

                    // Dynamic collider radius update
                    float z = Mathf.Clamp(coneContainer.transform.localScale.z, 0.01f, Mathf.Infinity);
                    sphereCol.radius = coneContainer.transform.localScale.x / (z * 2f);

                    // Opacity fade
                    float opacity =
                        Mathf.Clamp(
                            (MaxScaleVector - coneContainer.transform.localScale).magnitude
                             / MaxScaleVector.magnitude,
                            0f,
                            1f);

                    Material.SetFloat("_Opacity", opacity);

                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }

                // Clean up when the animation finishes
                DestroyContainer();
                if (this) Destroy(gameObject);
            }
            catch (OperationCanceledException)
            {
            }
        }

        protected override void PerformResetCleanup()
        {
            DestroyContainer();
            base.PerformResetCleanup();
        }

        private void DestroyContainer()
        {
            if (coneContainer != null)
            {
                Destroy(coneContainer);
                coneContainer = null;
            }
        }
    }
}
