using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using CosmicShore.Gameplay;
using CosmicShore.Data;
using CosmicShore.Utility;
using Reflex.Core;
using Reflex.Injectors;

namespace CosmicShore.Gameplay
{
    [CreateAssetMenu(fileName = "ProjectileDetonator",
        menuName = "ScriptableObjects/Services/Projectile Detonator")]
    public sealed class ProjectileDetonatorSO : ScriptableObject
    {
        [Serializable]
        public struct Request
        {
            public Projectile Projectile;        // required
            public Vector3 Position;             // world position to detonate
            public Quaternion Rotation;          // base rotation
            public bool FaceExitVelocity;        // align to projectile velocity?

            public float MinScale;               // charge=0
            public float MaxScale;               // charge=1

            public float ExplodeDelaySeconds;    // << NEW: wait before spawning AOE
            public float ReturnDelay;            // return to pool after explosion

            public bool StopAtImpact;    
            public bool DisableColliderNow;      // default true for safety

            public AOEExplosion[] Prefabs;
            public bool Anonymous;
            public Material OverrideMaterial;
            public Container DIContainer;

            // null = default behavior (!Projectile.SpareOwnDomain). Set explicitly when the
            // shooter must still be hit by its own blast while sparing its domain's prisms
            // (e.g. Grizzly explosion self-propulsion with Space-5 friendly fire disabled).
            public bool? AffectSelfOverride;
        }

        public void Detonate(in Request req)
        {
            if (!req.Projectile) return;
            _ = DetonateAsync(req);
        }

        private async UniTaskVoid DetonateAsync(Request req)
        {
            var proj   = req.Projectile;
            if (!proj) return;

            // Guard against acting on a pooled-and-reissued instance after our delays:
            // if the projectile launches a new flight while we are waiting, every
            // continuation below must bail instead of exploding/returning someone
            // else's live projectile.
            var generation = proj.FlightGeneration;

            var status = proj.VesselStatus;
            var pos    = req.Position;
            var rot    = req.Rotation;

            if (req.StopAtImpact)
            {
                // Stop motion as best-effort (no dependency on specific projectile impl)
                proj.Velocity = Vector3.zero;
                if (proj.TryGetComponent<Rigidbody>(out var rb))
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
            }

            if (req.DisableColliderNow)
            {
                var col = proj.GetComponent<Collider>();
                if (col) col.enabled = false;
            }

            if (req.ExplodeDelaySeconds > 0f)
                await UniTask.Delay(TimeSpan.FromSeconds(req.ExplodeDelaySeconds));

            if (!proj || proj.FlightGeneration != generation) return; // pooled/reissued meanwhile

            if (req.FaceExitVelocity && proj.Velocity.sqrMagnitude > 1e-6f &&
                SafeLookRotation.TryGet(proj.Velocity, Vector3.up, out var velocityRotation, proj))
            {
                rot = velocityRotation;
            }

            float charge01    = Mathf.Clamp01(proj.Charge);
            float targetScale = Mathf.Lerp(req.MinScale, req.MaxScale, charge01);

            if (req.Prefabs != null)
            {
                foreach (var prefab in req.Prefabs)
                {
                    if (!prefab) continue;
                    var spawned = Instantiate(prefab, pos, rot);
                    if (req.DIContainer != null)
                        GameObjectInjector.InjectRecursive(spawned.gameObject, req.DIContainer);
                    spawned.Initialize(new AOEExplosion.InitializeStruct
                    {
                        OwnDomain           = status.Domain,
                        Vessel              = status.Vessel,
                        MaxScale            = targetScale,
                        OverrideMaterial    = req.OverrideMaterial,
                        AnnonymousExplosion = req.Anonymous,
                        SpawnPosition       = pos,
                        SpawnRotation       = rot,
                        // Friendly fire is ON by default: the CHARGE level-5 'Domain-Safe
                        // Skybursts' snapshot on the projectile (set at fire time) is the ONLY
                        // thing that makes a detonation spare the shooter's own domain — so a
                        // hit, timeout, mine, or vessel-strike detonation all honor one gate,
                        // and the AOE prefabs' authored affectSelf never decides this path.
                        AffectSelfOverride  = req.AffectSelfOverride ?? !proj.SpareOwnDomain
                    });
                    spawned.Detonate();
                }
            }

            // Return after (post-explosion) delay
            if (req.ReturnDelay > 0f)
                await UniTask.Delay(TimeSpan.FromSeconds(req.ReturnDelay));

            if (proj && proj.FlightGeneration == generation) proj.ReturnToFactory();
        }
    }
}
