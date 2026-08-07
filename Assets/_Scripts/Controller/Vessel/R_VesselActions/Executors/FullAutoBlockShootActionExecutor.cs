using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Obvious.Soap;
using CosmicShore.Core;
using CosmicShore.Gameplay;
using Reflex.Attributes;
using UnityEngine;
using CosmicShore.Utility;
using CosmicShore.Data;
using System.Linq;
namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Turret Stance fire loop: while the Sparrow is stopped its guns launch PRISMS on the
    /// bullets' terms — same cadence, same muzzle speed, same eased flight path, same impact
    /// effects — differing only in that a turret prism always pierces (it keeps destroying
    /// for the whole flight instead of dying on first contact) and, at the end of that
    /// flight, anchors as permanent world mass.
    ///
    /// Every one of those numbers is read off <see cref="FullAutoBlockShootActionSO"/>'s
    /// bullet action, so there is nothing here to keep in step by hand.
    /// </summary>
    public class FullAutoBlockShootActionExecutor : ShipActionExecutorBase
    {
        [Inject] AudioSystem audioSystem;

        /// <summary>Static event: each time a block prism is shot. Param = player name.</summary>
        public static event Action<string> OnBlockShot;

        [Header("Scene Refs")]
        [SerializeField] private Transform[] muzzles;
        [SerializeField] private BlockProjectileFactory blockFactory;

        [Header("Visual")]
        [Tooltip("Seconds the prism's renderer stays hidden after spawn while its domain " +
                 "material settles. Keep at 0 (a single frame): turret prisms now leave the " +
                 "muzzle at BULLET speed, so every 0.1s here is ~150 units of invisible flight.")]
        [SerializeField] private float spawnVisibilityDelay;

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
            if (_cts != null) return;

            _cts = CancellationTokenSource.CreateLinkedTokenSource(
                this.GetCancellationTokenOnDestroy());

            FireLoopAsync(so, _cts.Token).Forget();
        }

        public void End()
        {
            if (_cts == null) return;

            try
            {
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
                return;
            }

            if (!so.HasBulletAction)
            {
                CSDebug.LogError(
                    "[FullAutoBlockShootActionExecutor] The action's bulletAction is unwired — " +
                    "turret cadence and speed are adopted from it, so there is nothing to fire with.");
                return;
            }

            // Cadence and flight are the BULLETS' numbers, read off the shared action asset.
            var interval  = 1f / Mathf.Max(0.1f, so.FireRate);
            var flightTime = Mathf.Max(0.01f, so.FlightTime);
            var rotOffset = Quaternion.Euler(so.RotationOffsetEuler);

            try
            {
                while (!token.IsCancellationRequested)
                {
                    // Muzzle speed is resolved per volley, not per hold: the SPACE level that
                    // scales the guns can move mid-press (crystals, comeback buffs).
                    var shotSpeed = so.ResolveSpeed(_status);

                    foreach (var m in muzzles)
                    {
                        if (!m) continue;

                        var domainAtShot = _status.Domain;
                        var prism = blockFactory.GetBlock(
                            so.PrismType,
                            m.position,
                            m.rotation * rotOffset,
                            null);

                        if (!prism) continue;

                        // Stationary blocks bypass Projectile.LaunchProjectile (movement is
                        // driven by MoveAndAnchorAsync), so play the launch SFX here — the
                        // flying-mode guns get theirs from LaunchProjectile.
                        audioSystem.PlayGameplaySFX(GameplaySFXCategory.ProjectileLaunch, m.position);

                        prism.transform.SetParent(null, true);

                        // MASS → turret prism stretch: the long z-axis scales with the vessel's
                        // live Mass level (ElementalAbilityMapSO). Volume = x·y·z of lossyScale,
                        // so the stretch feeds Cell.LiveVolume — "volume is the spine".
                        var blockScale = so.BlockScale;
                        blockScale.z *= _status?.ElementalAbilityHandler.Multiplier(Element.Mass) ?? 1f;

                        // Route sizing through the scale animator instead of a raw
                        // localScale write: TargetScale stays truthful (a later
                        // Grow/ChangeSize no longer snaps the prism back to its authored
                        // size), live volume tracks, and the block blooms in from zero
                        // during flight instead of popping in (continuity law).
                        var scaleAnimator = prism.GetComponent<PrismScaleAnimator>();
                        if (scaleAnimator)
                        {
                            prism.transform.localScale = Vector3.zero;
                            scaleAnimator.SetTargetScale(blockScale);
                            scaleAnimator.BeginGrowthAnimation();
                        }
                        else
                        {
                            prism.transform.localScale = blockScale;
                        }

                        //prism.ownerID = _status.PlayerName;
                        prism.ChangeTeam(domainAtShot);
                        prism.RegisterProjectileCreated(_status.PlayerName);

                        SetupPrismVisualAsync(prism, domainAtShot, spawnVisibilityDelay,
                            this.GetCancellationTokenOnDestroy()).Forget();

                        if (so.DisableCollidersOnLaunch)
                        {
                            var rootColliders = prism.GetComponents<Collider>();
                            foreach (var col in rootColliders)
                                col.enabled = false;
                        }
                        var childProjectile = prism.GetComponentInChildren<Projectile>();
                        if (childProjectile)
                        {
                            // Give the carried projectile the same identity a bullet gets at
                            // fire time. Without it the impact chain has no VesselStatus (the
                            // damage helper dereferences status.Player) and no OwnDomain, so
                            // the friendly-fire check in ProjectileImpactor could never match
                            // and hits were unattributable.
                            //
                            // stopOnFirstPrismImpact: FALSE, unconditionally — piercing is what
                            // the Turret Stance IS. A bullet only earns pierce at SPACE 5; a
                            // turret prism keeps destroying for the whole length of its path.
                            //
                            // No ProjectileFactory: this projectile is a part of a pooled PRISM,
                            // not a pooled projectile. Nothing on this flight can call
                            // ReturnToFactory — the pierce flag is off, the prism's impact
                            // container authors no end effects, and movement is driven here
                            // rather than through Projectile.LaunchProjectile.
                            childProjectile.Initialize(
                                null,
                                domainAtShot,
                                _status,
                                charge: 0f,
                                detachOnLaunch: false,
                                stopOnFirstPrismImpact: false,
                                spareOwnDomain: false);

                            childProjectile.Velocity = m.forward * shotSpeed; // m.forward is already unit

                            if (childProjectile.TryGetComponent<Collider>(out var projCol))
                                projCol.enabled = true;

                            if (childProjectile.TryGetComponent<Rigidbody>(out var rb))
                            {
                                rb.isKinematic = false;
                            }
                        }

                        OnBlockShot?.Invoke(_status?.PlayerName);

                        // MASS level-5 'Shielded Prisms': snapshot at fire time, applied at
                        // anchor. Regular shield only — one-hit ablative armor that fauna can
                        // still eat via devastate, preserving the food-web sink (SuperShield
                        // would create mass with no active sink — an ecosystem freeze vector).
                        bool shieldOnAnchor =
                            _status?.ElementalAbilityHandler.IsUpgradeActive(Element.Mass) == true;

                        var movementToken = this.GetCancellationTokenOnDestroy();
                        MoveAndAnchorAsync(
                            prism.transform,
                            m.forward * shotSpeed,
                            flightTime,
                            so.DisableCollidersOnLaunch,
                            prism,
                            childProjectile,
                            shieldOnAnchor,
                            movementToken
                        ).Forget();
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
        }
        #endregion

        #region Visual Setup
        private async UniTaskVoid SetupPrismVisualAsync(
            Prism prism,
            Domains domain,
            float delaySeconds,
            CancellationToken token)
        {
            try
            {
                if (!prism) return;

                var matAnim = prism.GetComponent<MaterialPropertyAnimator>();
                if (!matAnim || !matAnim.MeshRenderer)
                {
                    prism.Domain = domain;
                    return;
                }

                var mr = matAnim.MeshRenderer;
                mr.enabled = false;
                prism.Domain = domain;
                matAnim.MarkMaterialsDirty();

                matAnim.SetTransparency(false);

                if (delaySeconds > 0f)
                {
                    await UniTask.Delay(
                        TimeSpan.FromSeconds(delaySeconds),
                        DelayType.DeltaTime,
                        PlayerLoopTiming.Update,
                        token);
                }
                else
                {
    
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }

                if (!prism || !prism.gameObject.activeInHierarchy)
                    return;

                mr.enabled = true;
            }
            catch (OperationCanceledException)
            {

            }
            catch (Exception e)
            {
                CSDebug.LogError($"[FullAutoBlockShoot] SetupPrismVisual error: {e}");
            }
        }
        #endregion

        #region Movement / Anchor
        /// <summary>
        /// Fly the prism along a BULLET's path, then anchor it where that bullet's flight
        /// would have ended.
        ///
        /// The step is eased by <c>cos(t·π/2T)</c> and yields at <c>PreLateUpdate</c> — both
        /// copied deliberately from <c>Projectile.MoveProjectileAsync</c>, so a turret
        /// prism and a bullet released at the same instant stay abreast for the whole flight
        /// and stop at the same range. There is no separate travel distance to author: the
        /// end of the path IS the end of the bullet's life.
        /// </summary>
        private async UniTaskVoid MoveAndAnchorAsync(Transform block, Vector3 velocity, float flightTime, bool reactivateCollidersAtEnd, Prism prism, Projectile childProjectile, bool shieldOnAnchor, CancellationToken token)
        {
            float elapsedTime = 0f;

            try
            {
                while (elapsedTime < flightTime)
                {
                    token.ThrowIfCancellationRequested();

                    if (!block || !block.gameObject.activeInHierarchy)
                        return;

                    float deltaTime = Time.deltaTime;
                    float factor = Mathf.Cos(elapsedTime * Mathf.PI / (2f * flightTime));
                    block.position += velocity * (deltaTime * factor);

                    elapsedTime += deltaTime;
                    await UniTask.Yield(PlayerLoopTiming.PreLateUpdate, token);
                }

                if (reactivateCollidersAtEnd && prism && prism.gameObject.activeInHierarchy)
                {
                    var rootColliders = prism.GetComponents<Collider>();
                    foreach (var col in rootColliders)
                        col.enabled = true;
                }

                if (childProjectile && childProjectile.gameObject.activeInHierarchy)
                {
                    if (childProjectile.TryGetComponent<Collider>(out var projCol))
                        projCol.gameObject.SetActive(false);

                    if (childProjectile.TryGetComponent<Rigidbody>(out var rb))
                    {
                        rb.isKinematic     = true;
                    }
                }

                // Anchored: the block is now permanent world mass. Register it with the
                // spatial index (the one registration lifecycle) so it participates in
                // Burst AOE damage, growth occupancy, fauna density queries, and the
                // containing cell's LiveVolume — unregistered turret prisms were
                // invisible to the entire ecosystem. Registered at rest, not at the
                // muzzle, so the bucket grid files it at its true position.
                if (prism && prism.gameObject.activeInHierarchy && prism.SpatialIndexId < 0)
                {
                    prism.prismProperties.position = prism.transform.position;
                    var spatialIndex = PrismSpatialIndex.EnsureInstance();
                    if (spatialIndex != null && spatialIndex.IsAvailable)
                        prism.SpatialIndexId = spatialIndex.Register(prism);
                }

                // MASS level-5: engage the shield AFTER the collider re-enable and the index
                // registration — the shield's Box→Mesh collider swap must run last so the
                // reactivation loop can't re-enable the disabled BoxCollider, and the state
                // manager's index flag sync needs SpatialIndexId ≥ 0. Collider budget: the
                // swap is 1:1 (count-neutral); note shield MeshColliders are LOD-exempt today.
                if (shieldOnAnchor && prism && prism.gameObject.activeInHierarchy)
                    prism.ActivateShield();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                CSDebug.LogError($"[FullAutoBlockShoot] MoveAndAnchor error: {e}");
            }
        }
        #endregion
    }
}