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

            // The FIRST detonation of this flight owns the warhead. Two impact paths can commit
            // one in the same frame (a prism trigger and a vessel trigger inside one physics
            // step) and each still spawns its own authored explosion, as it always has - but the
            // round has exactly one warhead and must not spawn two.
            bool firstDetonation = proj.TryBeginDetonation();

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
                        AffectSelfOverride  = !proj.SpareOwnDomain
                    });
                    spawned.Detonate();
                }
            }

            // THE WARHEAD - the round's own blast, sized off its own body rather than off this
            // request's authored MinScale..MaxScale, and spawned here because this is the ONE
            // place every detonation path funnels through (timeout, proximity fuze, prism hit,
            // vessel hit, mine). Authoring it on the projectile rather than in each of the four
            // effect assets is what makes "the missile always does this when it goes off" true
            // by construction instead of by four assets agreeing.
            //
            // MaxScale is a DIAMETER for the spherical blast (its own trigger is authored at
            // radius 0.5, so world radius = MaxScale/2 at full expansion), hence the doubling.
            if (firstDetonation && proj.WarheadBlast && proj.WarheadBlastRadiusMultiplier > 0f)
            {
                float warheadRadius = proj.HitRadiusWorld * proj.WarheadBlastRadiusMultiplier;
                if (warheadRadius > 0f)
                {
                    var warhead = Instantiate(proj.WarheadBlast, pos, rot);
                    if (req.DIContainer != null)
                        GameObjectInjector.InjectRecursive(warhead.gameObject, req.DIContainer);
                    warhead.Initialize(new AOEExplosion.InitializeStruct
                    {
                        OwnDomain           = status.Domain,
                        Vessel              = status.Vessel,
                        MaxScale            = warheadRadius * 2f,
                        OverrideMaterial    = req.OverrideMaterial,
                        AnnonymousExplosion = req.Anonymous,
                        SpawnPosition       = pos,
                        SpawnRotation       = rot,
                        // NEVER self-affecting, and NOT the CHARGE-5 snapshot the prism blasts
                        // above take. Those two differ because the flag answers different
                        // questions for the two blasts. For a blast that destroys MASS,
                        // 'Domain-Safe Skybursts' is a real choice: below it you also blow up
                        // your own trail. This one destroys no mass at all - its whole payload
                        // is an elemental debuff on VESSELS - and there is no level at which a
                        // pilot should debuff themselves or a wingman. Taking the snapshot here
                        // did exactly that: it is TRUE below Charge 5, AcceptImpactee then
                        // accepts own-domain vessels, and the warhead is a 95-unit sphere
                        // centred at most ~76 units away (the fuze radius), so a Sparrow firing
                        // at the close range the fuze exists to encourage was reliably inside
                        // its own blast. Domains ARE the sides in every mode this weapon flies
                        // in (Dog Fight, Salvo, Wildlife Liberation), the same rule
                        // Projectile.DisallowImpactOnVessel enforces on the direct hit.
                        AffectSelfOverride  = false
                    });
                    warhead.Detonate();
                }
            }

            // Return after (post-explosion) delay
            if (req.ReturnDelay > 0f)
                await UniTask.Delay(TimeSpan.FromSeconds(req.ReturnDelay));

            if (proj && proj.FlightGeneration == generation) proj.ReturnToFactory();
        }
    }
}
