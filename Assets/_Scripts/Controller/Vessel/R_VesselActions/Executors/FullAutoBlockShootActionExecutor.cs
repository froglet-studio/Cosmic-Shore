using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Obvious.Soap;
using CosmicShore.Core;
using CosmicShore.ECS;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using Reflex.Attributes;
using UnityEngine;
using CosmicShore.Utility;
using CosmicShore.Data;
using Unity.Mathematics;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Turret Stance fire loop: while the Sparrow is stopped its guns fire PRISMS.
    ///
    /// A turret shot IS a bullet. Same cadence, same muzzle speed, same eased flight,
    /// same impact effects, and the same SPACE-5 gating on whether it pierces — all of
    /// it adopted from the vessel's own Full Auto action rather than authored twice.
    /// Exactly two things differ, and they are the two the design asks for:
    ///
    ///   1. what you SEE flying is the prism, not a tracer; and
    ///   2. where the bullet would be DESTROYED — a stopping prism impact, or its
    ///      lifetime expiring — the prism stays there as permanent world mass.
    ///
    /// The flight is GPU-side (Docs/PRISM_ANIMATION.md §5 C5). The prism is spawned at
    /// the flight's END POINT with everything final — collider, volume, spatial
    /// registration, MASS-5 shield — and <c>PrismFlightClock</c> walks the visual in
    /// from the muzzle off one stamp. The CPU writes nothing to the prism between the
    /// stamp and the anchor.
    ///
    /// The thing that actually collides along the path is the prism's carried
    /// <see cref="Projectile"/>, detached at the muzzle and flown by the bullets' own
    /// mover. That is a projectile, not prism animation, so it keeps the ordinary
    /// gameplay-transform contract — and it is what answers C5's open question: gameplay
    /// DOES collide mid-flight, which is why the prism's transform is final at the
    /// destination while a separate collider travels.
    /// </summary>
    public class FullAutoBlockShootActionExecutor : ShipActionExecutorBase
    {
        [Inject] AudioSystem audioSystem;

        /// <summary>Static event: each time a block prism is shot. Param = player name.</summary>
        public static event Action<string> OnBlockShot;

        [Header("Scene Refs")]
        [SerializeField] private Transform[] muzzles;
        [SerializeField] private BlockProjectileFactory blockFactory;

        [Tooltip("The PrismFactory spawn channel (EventOnSpawnPrismAndReturn). The " +
                 "ReverseSuction visualization rides it to spawn its grow effect - " +
                 "PrismType.Grow's first producer. Fail-loud: no null guard.")]
        [SerializeField] private PrismEventChannelWithReturnSO spawnPrismChannel;

        [Header("Events")]
        [SerializeField] private ScriptableEventNoParam OnMiniGameTurnEnd;

        private IVesselStatus _status;
        private CancellationTokenSource _cts;

        #region Unity Lifecycle
        private void OnEnable()
        {
            if (OnMiniGameTurnEnd)
                OnMiniGameTurnEnd.OnRaised += OnTurnEndOfMiniGame;
        }

        private void OnDisable()
        {
            End();
            if (OnMiniGameTurnEnd)
                OnMiniGameTurnEnd.OnRaised -= OnTurnEndOfMiniGame;
        }
        #endregion

        #region ShipActionExecutorBase
        public override void Initialize(IVesselStatus vesselStatus)
        {
            _status = vesselStatus;
            if (muzzles == null || muzzles.Length == 0)
                muzzles = new[] { _status.ShipTransform };
        }
        #endregion

        #region Public API
        public void Begin(FullAutoBlockShootActionSO so)
        {
            // Always stop any previous loop first, exactly like the flying-mode guns
            // (FullAutoActionExecutor.Begin). A bare `if (_cts != null) return;` is a
            // sticky gate: any path that ends the loop without clearing _cts latches
            // the turret off for the rest of the session, silently.
            End();

            if (!isActiveAndEnabled) return;

            var cts = CancellationTokenSource.CreateLinkedTokenSource(
                this.GetCancellationTokenOnDestroy());
            _cts = cts;

            FireLoopAsync(so, cts).Forget();
        }

        public void End()
        {
            if (_cts == null) return;

            try
            {
                if (!_cts.IsCancellationRequested)
                    _cts.Cancel();
            }
            catch
            {
                // no-op
            }

            _cts.Dispose();
            _cts = null;
        }

        private void OnTurnEndOfMiniGame() => End();
        #endregion

        #region Core Loop
        private async UniTaskVoid FireLoopAsync(FullAutoBlockShootActionSO so, CancellationTokenSource cts)
        {
            var token = cts.Token;

            if (!blockFactory)
            {
                CSDebug.LogError("[FullAutoBlockShootActionExecutor] BlockFactory not assigned.");
                ClearLoopHandle(cts);
                return;
            }

            if (!so.HasBulletAction)
            {
                CSDebug.LogError(
                    "[FullAutoBlockShootActionExecutor] The action's bulletAction is unwired — " +
                    "turret cadence, speed and flight time are adopted from it, so there is " +
                    "nothing to fire with.");
                ClearLoopHandle(cts);
                return;
            }

            // Cadence and flight are the BULLETS' numbers, read off the shared action asset.
            var interval = 1f / Mathf.Max(0.1f, so.FireRate);
            var flightTime = Mathf.Max(0.01f, so.FlightTime);
            var rotOffset = Quaternion.Euler(so.RotationOffsetEuler);

            try
            {
                while (!token.IsCancellationRequested)
                {
                    var abilities = _status?.ElementalAbilityHandler;

                    // Muzzle speed is resolved per volley, not per hold: the SPACE level
                    // that scales the guns can move mid-press (crystals, comeback buffs).
                    var shotSpeed = so.ResolveSpeed(_status);

                    // SPACE level-5 'Piercing Bullets' — the SAME gate the cannons use.
                    // Below it the shot is stopped by the first prism it hits and leaves
                    // its prism there; at 5+ it pierces on to the end of its path.
                    var piercing = abilities && abilities.IsUpgradeActive(Element.Space);

                    // MASS level-5 'Shielded Prisms': snapshot at fire time and applied as
                    // a pre-Initialize flag, so the shield is part of the prism's BIRTH and
                    // snaps (Docs/PRISM_ANIMATION.md §4.5) instead of morphing on arrival.
                    // Regular shield only — one-hit ablative armor fauna can still eat via
                    // devastate, which is what keeps the food-web sink intact.
                    var shielded = abilities && abilities.IsUpgradeActive(Element.Mass);

                    // MASS → turret prism stretch: the long z-axis scales with the vessel's
                    // live Mass level. Volume = x·y·z of lossyScale, so the stretch feeds
                    // Cell.LiveVolume — "volume is the spine".
                    var blockScale = so.BlockScale;
                    blockScale.z *= abilities ? abilities.Multiplier(Element.Mass) : 1f;

                    // Distance the shot covers before its lifetime ends. The bullets' mover
                    // eases each step by cos(t·π/2T), so the range is that integral —
                    // speed · 2T/π, not speed · T.
                    var range = shotSpeed * flightTime * (2f / Mathf.PI);

                    foreach (var m in muzzles)
                    {
                        if (!m) continue;
                        FireOne(so, m, rotOffset, blockScale, shotSpeed, flightTime, range,
                            piercing, shielded);
                    }

                    await UniTask.Delay(
                        TimeSpan.FromSeconds(interval),
                        DelayType.DeltaTime,
                        PlayerLoopTiming.PreLateUpdate,
                        token);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                CSDebug.LogError($"[FullAutoBlockShoot] loop error: {e}");
            }
            finally
            {
                ClearLoopHandle(cts);
            }
        }

        /// <summary>
        /// Drop the loop handle without cancelling — the loop is already over. Leaving a
        /// live _cts behind is what would latch the next Begin() into a no-op.
        ///
        /// The identity check is load-bearing: UniTask.Delay observes cancellation on the
        /// NEXT PlayerLoop tick, so an outgoing loop's finally can run AFTER a replacement
        /// loop has already installed its own source. Without it, the dying loop would
        /// dispose the live one and End() would never be able to stop the turret again.
        /// </summary>
        private void ClearLoopHandle(CancellationTokenSource cts)
        {
            if (cts == null) return;
            if (ReferenceEquals(_cts, cts)) _cts = null;
            cts.Dispose();
        }

        private void FireOne(FullAutoBlockShootActionSO so, Transform muzzle, Quaternion rotOffset,
            Vector3 blockScale, float shotSpeed, float flightTime, float range,
            bool piercing, bool shielded)
        {
            var domainAtShot = _status.Domain;
            var velocity = muzzle.forward * shotSpeed;   // muzzle.forward is already unit

            // The prism is pulled at the flight's END POINT — where the mass will rest.
            var anchorPoint = muzzle.position + muzzle.forward * range;
            var prism = blockFactory.GetBlock(so.PrismType, anchorPoint, muzzle.rotation * rotOffset, null);
            if (!prism) return;

            // No launch SFX here: the carried projectile's LaunchProjectile plays it, exactly
            // as the flying-mode guns do. Playing one here too made every turret shot fire
            // two launch sounds.

            prism.transform.SetParent(null, true);

            // Read per shot so the inspector enum can be flipped LIVE in play mode —
            // the whole point of shipping both visualizations is comparing them.
            var viz = so.Visualization;
            PrismImplosion suctionEffect = null;

            if (viz == FullAutoBlockShootActionSO.FlightVisualization.TranslateAndGrow)
            {
                MakePrismLive(prism, blockScale, domainAtShot, shielded, anchorPoint);
                StampFlight(prism, velocity, flightTime, range, blockScale);
            }
            else
            {
                // ReverseSuction: the prism is NOT initialized yet — it flies as a BLANK
                // and the effect does the drawing; the real prism is created the moment
                // the shot lands. A fresh instance is already a blank (Awake zeroes the
                // scale), but a pool-recycled one may arrive at full scale with a live
                // collider from its previous life — disarm it explicitly.
                prism.transform.localScale = Vector3.zero;
                foreach (var col in prism.GetComponents<Collider>())
                    col.enabled = false;

                // The grow effect at the anchor streams the prism's faces out of the
                // MOVING shot point (the carried projectile).
                suctionEffect = SpawnReverseSuctionEffect(prism, blockScale, domainAtShot,
                    flightTime, anchorPoint, muzzle.rotation * rotOffset);
            }

            LaunchCarriedProjectile(prism, muzzle, velocity, flightTime, domainAtShot,
                piercing, anchorPoint, blockScale, viz, shielded, suctionEffect);

            OnBlockShot?.Invoke(_status?.PlayerName);
        }

        /// <summary>
        /// The pooled prism becomes real, live mass: team, shield flag, and the one call
        /// this path historically missed — <c>Prism.Initialize</c>, the documented
        /// pool-spawn entry point. Without it IsCreationComplete stays false,
        /// BeginGrowthAnimation early-returns on its pre-creation guard, and the prism
        /// lives at localScale zero: invisible, with a zero-volume collider. Silently.
        /// </summary>
        private void MakePrismLive(Prism prism, Vector3 blockScale, Domains domain,
            bool shielded, Vector3 restPoint)
        {
            // Target scale BEFORE Initialize: Initialize reads the authored target off
            // the scale animator and hands it to the creation coroutine.
            var scaleAnimator = prism.GetComponent<PrismScaleAnimator>();
            if (scaleAnimator)
            {
                // Snappy bloom: the prefab's authored GrowthRate is tuned for trail
                // lay; at bullet speed the shot is over in 0.3s, so take the fastest
                // clock rate (8 pins ClockRateK at its ceiling) or the prism arrives
                // still near-invisible.
                scaleAnimator.GrowthRate = 8f;
                scaleAnimator.SetTargetScale(blockScale);
            }
            else
            {
                prism.transform.localScale = blockScale;
            }

            prism.ChangeTeam(domain);
            prism.prismProperties.IsShielded = shielded;
            prism.Initialize(_status.PlayerName);

            // NOT RegisterProjectileCreated: it raises the same prism-created SOAP channel
            // that Initialize's creation coroutine already raises, so every turret shot
            // counted as two prisms created (and double-credited its volume) in
            // StatsManager. Its other job — owner attribution — is one field.
            prism.ownerID = _status.PlayerName;
            prism.prismProperties.position = restPoint;
        }

        /// <summary>
        /// ReverseSuction's flight visual: the fauna consumption shader run backwards.
        /// A pooled grow effect posed at the anchor streams the prism's faces out of the
        /// moving shot point (<c>PrismImplosion.StartGrow</c> tracks the carried
        /// projectile under the documented moving-target exception) over exactly one
        /// bullet lifetime. PrismType.Grow's first producer.
        /// </summary>
        private PrismImplosion SpawnReverseSuctionEffect(Prism prism, Vector3 blockScale,
            Domains domain, float flightTime, Vector3 anchorPoint, Quaternion rotation)
        {
            var carried = prism.GetComponentInChildren<Projectile>(true);
            if (!carried) return null; // LaunchCarriedProjectile warns about this case

            var ret = spawnPrismChannel.RaiseEvent(new PrismEventData
            {
                PrismType = PrismType.Grow,
                ownDomain = domain,
                SpawnPosition = anchorPoint,
                Rotation = rotation,
                Scale = blockScale,
                TargetTransform = carried.transform,
                // A hair longer than the flight: the real prism is created at landing
                // and takes a frame or two to reveal — the overlap keeps the completed
                // effect on screen across that seam instead of leaving a hole.
                GrowDuration = flightTime + RevealOverlapSeconds,
            });

            return ret.SpawnedObject ? ret.SpawnedObject.GetComponent<PrismImplosion>() : null;
        }

        /// <summary>Seconds the reverse-suction effect outlives the flight, covering the
        /// real prism's 1–2 frame creation window at the anchor.</summary>
        private const float RevealOverlapSeconds = 0.2f;

        private static readonly int FlightStartTimeId = Shader.PropertyToID("_FlightStartTime");
        #endregion

        #region Flight (GPU clock)
        /// <summary>
        /// Touchpoint 1: one stamp of the flight's initial conditions. The GPU evaluates
        /// the position for the whole flight; the CPU writes nothing until the anchor.
        /// </summary>
        private void StampFlight(Prism prism, Vector3 velocity, float flightTime, float range,
            Vector3 finalScale)
        {
            float now = PrismClock.Now;
            var v = new float3(velocity.x, velocity.y, velocity.z);

            bool stamped = PrismRenderService.StampFlight(in prism.RenderHandle, now, flightTime, in v);

            // One self-heal before screaming: the stamp is a ONE-SHOT write, so a prism
            // that reaches this instant without a companion entity loses its flight for
            // good. Creation is idempotent and no-ops on the happy path.
            if (!stamped && prism.TryEnsureRenderEntityForStamp())
                stamped = PrismRenderService.StampFlight(in prism.RenderHandle, now, flightTime, in v);

            if (!stamped)
            {
                // STRICT MODE: no CPU fallback. The prism simply appears at its anchor
                // point with no flight — loudly, naming the broken gate.
                PrismClockDiagnostics.WarnNoRenderEntity($"flight:{prism.name}", this,
                    prism.DescribeRenderEntityState());
                return;
            }

            // The stamp landing on the ENTITY proves nothing about the SHADER: if the
            // graph wiring regressed (or was never imported), the per-instance values
            // upload into a property no shader reads and the prism silently teleports
            // to maximum range with no flight — exactly a "the turret fires nothing"
            // report from anyone watching the muzzle. Scream, once per material.
            var sharedMat = prism.TryGetComponent<MeshRenderer>(out var mr) ? mr.sharedMaterial : null;
            if (sharedMat && !sharedMat.HasProperty(FlightStartTimeId))
                PrismClockDiagnostics.WarnUnwiredMaterial(sharedMat, "_FlightStartTime", this);

            // Culling envelope: the entity matrix never moves, so without this the prism
            // frustum-culls against its ANCHOR box while the visual is still out at the
            // barrel — it would pop in halfway down the shot. The sweep vector is the
            // muzzle offset (minus the whole flight vector) in object space. Reset first:
            // pooled reuse must not compound envelopes run over run.
            var mesh = prism.EffectiveRenderMesh();
            if (mesh) PrismRenderService.ResetBoundsToMesh(in prism.RenderHandle, mesh);

            // Object-space muzzle offset. NOT Transform.InverseTransformVector: a
            // just-pulled prism sits at localScale ZERO until its creation coroutine
            // completes (PrismScaleAnimator derives the bloom's start fraction from
            // that zero, so it must not be pre-written), and inverting a degenerate
            // matrix yields NaN bounds. Rotate-then-divide by the FINAL per-axis scale
            // — which is what the entity's LocalToWorld will hold once it is visible —
            // is the same math SpawnExplosionDebrisBatch uses for the same reason.
            Vector3 muzzleOffsetWS = -velocity.normalized * range;
            Vector3 local = Quaternion.Inverse(prism.transform.rotation) * muzzleOffsetWS;
            var objDisp = new float3(
                local.x / Mathf.Max(1e-4f, Mathf.Abs(finalScale.x)),
                local.y / Mathf.Max(1e-4f, Mathf.Abs(finalScale.y)),
                local.z / Mathf.Max(1e-4f, Mathf.Abs(finalScale.z)));
            PrismRenderService.ExpandBoundsForClockAnimation(in prism.RenderHandle, in objDisp,
                4f + 0.25f * math.length(objDisp));
        }

        /// <summary>
        /// The travelling half: the prism's carried projectile, detached at the muzzle and
        /// flown by <c>Projectile.LaunchProjectile</c> — literally the bullets' mover, so
        /// the two modes cannot drift. It is what pierces, what destroys prisms along the
        /// path, and what decides where the flight ends.
        /// </summary>
        private void LaunchCarriedProjectile(Prism prism, Transform muzzle, Vector3 velocity,
            float flightTime, Domains domainAtShot, bool piercing, Vector3 anchorPoint,
            Vector3 blockScale, FullAutoBlockShootActionSO.FlightVisualization viz,
            bool shielded, PrismImplosion suctionEffect)
        {
            var carried = prism.GetComponentInChildren<Projectile>(true);
            if (!carried)
            {
                CSDebug.LogWarning(
                    $"[FullAutoBlockShoot] {prism.name} carries no Projectile — the shot will " +
                    "fly and anchor but hit nothing.");
                return;
            }

            var carriedTransform = carried.transform;

            // Wake it for the flight. The anchor puts it back to sleep, and that
            // deactivate/activate cycle is what keeps Projectile's OnEnable/OnDisable
            // bookkeeping honest — most importantly PrismColliderLodManager's focus
            // registration, which is how the prisms along the shot's path have their
            // colliders awake by the time it arrives. Leaving it permanently active
            // leaked one focus entry per anchored prism, forever.
            if (!carried.gameObject.activeSelf) carried.gameObject.SetActive(true);

            // Give it the same identity a bullet gets at fire time. Without a VesselStatus
            // the impact chain has no player (the damage helper dereferences status.Player)
            // and OwnDomain stays default, so the friendly-fire check can never match.
            carried.Initialize(
                null,
                domainAtShot,
                _status,
                charge: 0f,
                detachOnLaunch: false,
                stopOnFirstPrismImpact: !piercing,
                spareOwnDomain: false,
                carriedByHost: true);

            carriedTransform.SetParent(null, true);

            // Size it EXPLICITLY. The prism it just detached from is still at localScale
            // ZERO — its creation coroutine has not run yet — and SetParent(worldPositionStays)
            // preserves world scale, so the projectile would inherit a zero-volume trigger and
            // the shot would collide with nothing. Parented under a grown prism its world
            // scale is the prism's, so that is what it takes here. (This is the same class of
            // failure as the zero-scale prism itself: a degenerate collider that fails
            // silently.)
            carriedTransform.localScale = blockScale;
            carriedTransform.SetPositionAndRotation(muzzle.position, muzzle.rotation);
            carried.Velocity = velocity;

            if (carried.TryGetComponent<Collider>(out var col)) col.enabled = true;
            if (carried.TryGetComponent<Rigidbody>(out var rb)) rb.isKinematic = false;

            carried.FlightEnded += (p, stoppedByImpact) =>
                AnchorPrism(prism, p, stoppedByImpact, anchorPoint, blockScale, domainAtShot,
                    viz, shielded, suctionEffect);

            carried.LaunchProjectile(flightTime);
        }

        /// <summary>
        /// Touchpoint 3: the flight is over, so the prism BE its end state.
        ///
        /// TranslateAndGrow: the prism was live the whole flight — settle the flight
        /// stamp (one transform write only when an impact cut it short) and restore the
        /// culling envelope.
        ///
        /// ReverseSuction: the prism was a scale-zero blank the whole flight and the
        /// effect did the drawing — this is where the real prism is CREATED, exactly
        /// where the shot died. Pre-scaling the transform to the final size before
        /// Initialize makes the creation bloom settle instantly, so the reveal is a
        /// straight hand-off from the effect's completed shape (which outlives the
        /// flight by RevealOverlapSeconds to cover the 1–2 frame creation window).
        /// </summary>
        private void AnchorPrism(Prism prism, Projectile carried, bool stoppedByImpact,
            Vector3 anchorPoint, Vector3 blockScale, Domains domain,
            FullAutoBlockShootActionSO.FlightVisualization viz, bool shielded,
            PrismImplosion suctionEffect)
        {
            bool translateViz = viz == FullAutoBlockShootActionSO.FlightVisualization.TranslateAndGrow;

            if (prism)
            {
                if (translateViz)
                {
                    if (stoppedByImpact)
                    {
                        // Interruption = re-stamp: the shot died early, so the mass belongs
                        // at the impact point, not at maximum range. NotifyPositionChanged
                        // is the sanctioned mover contract — spatial index, shell, and the
                        // render entity matrix in one call.
                        prism.transform.position = carried ? carried.transform.position : anchorPoint;
                        prism.prismProperties.position = prism.transform.position;
                        prism.NotifyPositionChanged();
                    }

                    // The visual snaps to the transform, which is exactly where the shader
                    // had already drawn it — the stamp reaches zero offset at t = flightTime.
                    PrismRenderService.ClearFlightStamp(in prism.RenderHandle);

                    // Give the culling envelope back. StampFlight inflated RenderBounds to
                    // cover the whole muzzle-to-anchor sweep; anchored turret prisms are
                    // permanent mass that is never released, so without this every one of
                    // them would be un-cullable for the rest of the session.
                    var mesh = prism.EffectiveRenderMesh();
                    if (mesh) PrismRenderService.ResetBoundsToMesh(in prism.RenderHandle, mesh);
                }
                else
                {
                    // ReverseSuction: the shot landed — create the mass where it died.
                    if (stoppedByImpact && carried)
                        prism.transform.position = carried.transform.position;

                    // Pre-scale so the creation bloom's start fraction is ~1: the bloom
                    // settles immediately and the reveal IS the effect's end state, not a
                    // second from-zero grow on top of it.
                    prism.transform.localScale = blockScale;

                    MakePrismLive(prism, blockScale, domain, shielded, prism.transform.position);

                    // An early impact leaves the effect mid-stream at the wrong place
                    // (its rest shape sits at maximum range) — cut it; the prism's own
                    // creation carries the reveal. On a timeout the effect is already
                    // drawing the completed shape at the anchor and retires itself
                    // RevealOverlapSeconds later, bridging the creation window.
                    if (stoppedByImpact && suctionEffect && suctionEffect.gameObject.activeSelf)
                        suctionEffect.ReturnToPool();
                }
            }
            else if (suctionEffect && suctionEffect.gameObject.activeSelf)
            {
                // Host prism destroyed mid-flight (viz 2's blank is intangible, but the
                // pool can still sweep it on teardown) — nothing will land; cut the effect.
                suctionEffect.ReturnToPool();
            }

            if (!carried) return;

            // The prism died mid-flight (its collider is live at the anchor point from the
            // stamp, so anyone can shoot it). The carried projectile is DETACHED at that
            // moment, so it does not go down with its host — it would sit in the world
            // forever. It is a part of a prism that no longer exists: destroy it.
            if (!prism)
            {
                Destroy(carried.gameObject);
                return;
            }

            // Home it under the prism it belongs to. The prefab parents ProjectileCollider
            // directly to the prism root, so this is the authored home by construction —
            // reading transform.parent at fire time would capture null on a prism whose
            // previous flight had not landed.
            var carriedTransform = carried.transform;
            carriedTransform.SetParent(prism.transform, false);
            carriedTransform.localPosition = Vector3.zero;
            carriedTransform.localRotation = Quaternion.identity;

            if (carried.TryGetComponent<Collider>(out var col)) col.enabled = false;
            if (carried.TryGetComponent<Rigidbody>(out var rb)) rb.isKinematic = true;

            // Sleep it: OnDisable releases the collider-LOD focus this flight registered,
            // and the next shot from this pooled prism wakes it again in
            // LaunchCarriedProjectile. (The old code deactivated it here and never
            // re-activated, so a recycled prism fired as a dud.)
            carried.gameObject.SetActive(false);
        }
        #endregion
    }
}
