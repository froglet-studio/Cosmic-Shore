using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Obvious.Soap;
using CosmicShore.Core;
using CosmicShore.ECS;
using CosmicShore.Gameplay;
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

            _cts = CancellationTokenSource.CreateLinkedTokenSource(
                this.GetCancellationTokenOnDestroy());

            FireLoopAsync(so, _cts.Token).Forget();
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
        private async UniTaskVoid FireLoopAsync(FullAutoBlockShootActionSO so, CancellationToken token)
        {
            if (!blockFactory)
            {
                CSDebug.LogError("[FullAutoBlockShootActionExecutor] BlockFactory not assigned.");
                ClearLoopHandle();
                return;
            }

            if (!so.HasBulletAction)
            {
                CSDebug.LogError(
                    "[FullAutoBlockShootActionExecutor] The action's bulletAction is unwired — " +
                    "turret cadence, speed and flight time are adopted from it, so there is " +
                    "nothing to fire with.");
                ClearLoopHandle();
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
                ClearLoopHandle();
            }
        }

        /// <summary>Drop the loop handle without cancelling — the loop is already over.
        /// Leaving a live _cts behind is what would latch the next Begin() into a no-op.</summary>
        private void ClearLoopHandle()
        {
            if (_cts == null) return;
            _cts.Dispose();
            _cts = null;
        }

        private void FireOne(FullAutoBlockShootActionSO so, Transform muzzle, Quaternion rotOffset,
            Vector3 blockScale, float shotSpeed, float flightTime, float range,
            bool piercing, bool shielded)
        {
            var domainAtShot = _status.Domain;
            var velocity = muzzle.forward * shotSpeed;   // muzzle.forward is already unit

            // The prism is born at the flight's END POINT: gameplay state belongs where
            // the mass will rest, and the vertex stage draws it walking in from the barrel.
            var anchorPoint = muzzle.position + muzzle.forward * range;
            var prism = blockFactory.GetBlock(so.PrismType, anchorPoint, muzzle.rotation * rotOffset, null);
            if (!prism) return;

            // Stationary shots bypass Projectile.LaunchProjectile's own SFX for the prism
            // itself, so play the launch sound here — the flying-mode guns get theirs from
            // LaunchProjectile.
            if (audioSystem)
                audioSystem.PlayGameplaySFX(GameplaySFXCategory.ProjectileLaunch, muzzle.position);

            prism.transform.SetParent(null, true);

            // Target scale BEFORE Initialize: Prism.Initialize reads the authored target
            // off the scale animator and hands it to the creation coroutine.
            var scaleAnimator = prism.GetComponent<PrismScaleAnimator>();
            if (scaleAnimator) scaleAnimator.SetTargetScale(blockScale);
            else prism.transform.localScale = blockScale;

            prism.ChangeTeam(domainAtShot);
            prism.prismProperties.IsShielded = shielded;

            // THE line this whole path was missing. Without it IsCreationComplete stays
            // false, so BeginGrowthAnimation early-returns, the prism never leaves
            // localScale zero, its collider has no volume, and SetRenderVisible(true) is
            // never reached — every turret shot was an invisible, intangible nothing.
            // Initialize is the documented pool-spawn entry point every other pooled-prism
            // spawner in the project uses.
            prism.Initialize(_status.PlayerName);
            prism.RegisterProjectileCreated(_status.PlayerName);

            StampFlight(prism, velocity, flightTime, range, blockScale);

            LaunchCarriedProjectile(prism, muzzle, velocity, flightTime, domainAtShot,
                piercing, anchorPoint);

            OnBlockShot?.Invoke(_status?.PlayerName);
        }
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

            // Culling envelope: the entity matrix never moves, so without this the prism
            // frustum-culls against its ANCHOR box while the visual is still out at the
            // barrel — it would pop in halfway down the shot. The sweep vector is the
            // muzzle offset (minus the whole flight vector) in object space. Reset first:
            // pooled reuse must not compound envelopes run over run.
            var meshFilter = prism.GetComponent<MeshFilter>();
            if (meshFilter) PrismRenderService.ResetBoundsToMesh(in prism.RenderHandle, meshFilter.sharedMesh);

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
            float flightTime, Domains domainAtShot, bool piercing, Vector3 anchorPoint)
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
            var homeParent = carriedTransform.parent;

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
            carriedTransform.SetPositionAndRotation(muzzle.position, muzzle.rotation);
            carried.Velocity = velocity;

            if (carried.TryGetComponent<Collider>(out var col)) col.enabled = true;
            if (carried.TryGetComponent<Rigidbody>(out var rb)) rb.isKinematic = false;

            carried.FlightEnded += (p, stoppedByImpact) =>
                AnchorPrism(prism, p, homeParent, stoppedByImpact, anchorPoint);

            carried.LaunchProjectile(flightTime);
        }

        /// <summary>
        /// Touchpoint 3: the flight is over, so the prism BE its end state. One transform
        /// write at most (only when an impact cut the flight short — a timeout lands
        /// exactly on the anchor point the prism was already stamped at), then the flight
        /// stamp is settled and the carried projectile goes home.
        /// </summary>
        private void AnchorPrism(Prism prism, Projectile carried, Transform homeParent,
            bool stoppedByImpact, Vector3 anchorPoint)
        {
            if (prism)
            {
                if (stoppedByImpact)
                {
                    // Interruption = re-stamp: the shot died early, so the mass belongs at
                    // the impact point, not at maximum range. NotifyPositionChanged is the
                    // sanctioned mover contract — spatial index, shell, and the render
                    // entity matrix in one call.
                    prism.transform.position = carried ? carried.transform.position : anchorPoint;
                    prism.NotifyPositionChanged();
                }

                // The visual snaps to the transform, which is exactly where the shader had
                // already drawn it — the stamp reaches zero offset at t = flightTime.
                PrismRenderService.ClearFlightStamp(in prism.RenderHandle);
            }

            if (!carried) return;

            // Home the carried projectile so a pooled reuse of this prism finds it intact.
            // Note: the GameObject stays ACTIVE — deactivating it (as this path used to)
            // is never undone, and the next prism drawn from the pool would fire as a dud.
            if (carried.TryGetComponent<Collider>(out var col)) col.enabled = false;
            if (carried.TryGetComponent<Rigidbody>(out var rb)) rb.isKinematic = true;

            if (homeParent && carried.transform.parent != homeParent)
            {
                carried.transform.SetParent(homeParent, false);
                carried.transform.localPosition = Vector3.zero;
                carried.transform.localRotation = Quaternion.identity;
            }
        }
        #endregion
    }
}
