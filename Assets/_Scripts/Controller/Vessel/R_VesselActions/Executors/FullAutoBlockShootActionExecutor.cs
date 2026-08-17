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

        /// <summary>Per-frame catch-up cap — see <see cref="FullAutoActionExecutor"/>. Same
        /// number on purpose: the two modes must not drift in cadence under load either.</summary>
        private const int MaxVolleysPerTick = 4;

        private IVesselStatus _status;
        private GunSprayAccuracy _spray;
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
        public void Begin(FullAutoBlockShootActionSO so, GunSprayAccuracy spray = null)
        {
            // Always stop any previous loop first, exactly like the flying-mode guns
            // (FullAutoActionExecutor.Begin). A bare `if (_cts != null) return;` is a
            // sticky gate: any path that ends the loop without clearing _cts latches
            // the turret off for the rest of the session, silently.
            End();

            if (spray) _spray = spray;

            if (!isActiveAndEnabled) return;

            // Idempotent — taking the hold over from the flying-mode guns mid-press keeps the
            // cone that was already open, so toggling the stance is not a free accuracy reset.
            if (_spray) _spray.BeginHold(so.Spread);

            var cts = CancellationTokenSource.CreateLinkedTokenSource(
                this.GetCancellationTokenOnDestroy());
            _cts = cts;

            FireLoopAsync(so, cts).Forget();
        }

        public void End()
        {
            if (_spray) _spray.ReleaseHold();

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

            // Time-accumulator cadence, identical to the bullets' (see FullAutoActionExecutor):
            // a whole-frame Delay caps the rate at the frame rate, which at these cadences means
            // the turret would lay mass at a rate that depended on the device.
            float owed = interval;

            try
            {
                while (!token.IsCancellationRequested)
                {
                    float maxOwed = interval * MaxVolleysPerTick;
                    if (owed > maxOwed) owed = maxOwed;

                    int volleys = (int)(owed / interval);
                    owed -= volleys * interval;

                    if (volleys > 0)
                    {
                        var abilities = _status?.ElementalAbilityHandler;

                        // Muzzle speed is resolved per volley, not per hold: the SPACE level
                        // that scales the guns can move mid-press (crystals, comeback buffs).
                        var shotSpeed = so.ResolveSpeed(_status);

                        // SPACE level-5 'Piercing Bullets' — the SAME gate the cannons use.
                        // Below it the shot is stopped by the first prism it hits and leaves
                        // its prism there; at 5+ it pierces on to the end of its path.
                        var piercing = abilities && abilities.IsUpgradeActive(Element.Space);

                        // The born-in state. THE DESIGN is ShieldedAtMass5: regular prisms
                        // below MASS 5, shielded at 5+. MASS owns the SUBSTANCE of what you
                        // fire — prism length, round growth, and armour — while SPACE 5 owns
                        // REACH (pierce, on both fire modes). Plain/Shielded/Danger are
                        // unconditional playtest overrides. The state lands as a pre-Initialize flag, so it is part
                        // of the prism's BIRTH and snaps (Docs/PRISM_ANIMATION.md §4.5)
                        // instead of morphing on arrival. Regular shield only — one-hit
                        // ablative armor fauna can still eat via devastate, keeping the
                        // food-web sink. Danger suppresses shields: mutually exclusive by
                        // locked law.
                        var state = so.FiredState;
                        var dangerous = state == FullAutoBlockShootActionSO.FiredPrismState.Danger;
                        var shielded = state == FullAutoBlockShootActionSO.FiredPrismState.Shielded ||
                                       (state == FullAutoBlockShootActionSO.FiredPrismState.ShieldedAtMass5 &&
                                        abilities && abilities.IsUpgradeActive(Element.Mass));

                        // MASS → turret prism stretch: the long z-axis scales with the vessel's
                        // live Mass level. Volume = x·y·z of lossyScale, so the stretch feeds
                        // Cell.LiveVolume — "volume is the spine".
                        var blockScale = so.BlockScale;
                        blockScale.z *= abilities ? abilities.Multiplier(Element.Mass) : 1f;

                        // Distance the shot covers before its lifetime ends. The bullets' mover
                        // eases each step by cos(t·π/2T), so the range is that integral —
                        // speed · 2T/π, not speed · T.
                        var range = shotSpeed * flightTime * (2f / Mathf.PI);

                        // MASS → in-flight growth, the bullets' factor verbatim. The carried
                        // hit sphere swells as the shot travels, which also brings it much
                        // closer to the size of the prism the player watches flying.
                        var growth = so.ResolveGrowthFactor(_status);

                        for (int v = 0; v < volleys && !token.IsCancellationRequested; v++)
                        {
                            foreach (var m in muzzles)
                            {
                                if (!m) continue;
                                FireOne(so, m, rotOffset, blockScale, shotSpeed, flightTime, range,
                                    piercing, shielded, dangerous, growth);
                            }
                        }
                    }

                    await UniTask.Yield(PlayerLoopTiming.PreLateUpdate, token);
                    owed += Time.deltaTime;
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
            bool piercing, bool shielded, bool dangerous, float growthFactor)
        {
            var domainAtShot = _status.Domain;

            // Accuracy decay applies to a turret shot exactly as it does to a bullet: the round
            // walks off aim on a held trigger and the prism stays wherever that deflected round
            // would have died. The deflection is composed onto the muzzle's POSE rather than
            // rebuilt with LookRotation, which preserves the muzzle's roll — and the prism's long
            // axis IS the shot, so re-referencing roll to world up would visibly twist it.
            var aim = _spray ? _spray.PerturbAim(muzzle.forward) : muzzle.forward;
            var shotRotation = GunSpreadMath.DeflectionOf(muzzle.forward, aim) * muzzle.rotation;

            var velocity = aim * shotSpeed;              // aim is already unit

            // The prism is pulled at the flight's END POINT — where the mass will rest.
            var anchorPoint = muzzle.position + aim * range;
            var prism = blockFactory.GetBlock(so.PrismType, anchorPoint, shotRotation * rotOffset, null);
            if (!prism) return;

            // No launch SFX here: the carried projectile's LaunchProjectile plays it, exactly
            // as the flying-mode guns do. Playing one here too made every turret shot fire
            // two launch sounds.

            prism.transform.SetParent(null, true);

            // Read per shot so the inspector enum can be flipped LIVE in play mode —
            // the whole point of shipping both visualizations is comparing them.
            var viz = so.Visualization;
            ReverseSuctionShot suctionShot = null;

            if (viz == FullAutoBlockShootActionSO.FlightVisualization.TranslateAndGrow)
            {
                MakePrismLive(prism, blockScale, domainAtShot, shielded, dangerous,
                    so.SpawnFullSize, anchorPoint);
                StampFlight(prism, velocity, flightTime, range, blockScale);
                // Placement immunity: the prism is parked live at the anchor for the whole
                // flight, so the window must span flight + settle — from-fire-time only
                // would lapse mid-flight and earlier shots' projectiles (arriving at their
                // own anchors nearby) would erase it before it ever "placed".
                prism.ProjectileImmuneUntil = Time.time + flightTime + so.PlacementImmunitySeconds;
            }
            else
            {
                // ReverseSuction: the prism is NOT initialized yet — it flies as a BLANK
                // and the effect does the drawing; the real prism is created only when
                // the assembly stream finishes. A fresh instance is already a blank
                // (Awake zeroes the scale), but a pool-recycled one may arrive at full
                // scale with a live collider from its previous life — disarm it.
                prism.transform.localScale = Vector3.zero;
                foreach (var col in prism.GetComponents<Collider>())
                    col.enabled = false;

                // The assembly runs LONGER than the flight (the whole point of the
                // multiplier): the shot lands on the bullet clock, the faces keep
                // streaming into place after it, and the prism reveals at the end.
                float effectDuration = flightTime * so.SuctionDurationMultiplier;

                suctionShot = new ReverseSuctionShot
                {
                    Prism = prism,
                    BlockScale = blockScale,
                    Domain = domainAtShot,
                    Shielded = shielded,
                    Dangerous = dangerous,
                    LandingPoint = anchorPoint,
                    EffectDuration = effectDuration,
                    FlightTime = flightTime,
                    PlacementImmunitySeconds = so.PlacementImmunitySeconds,
                };

                suctionShot.Effect = SpawnReverseSuctionEffect(prism, blockScale, domainAtShot,
                    effectDuration, anchorPoint, shotRotation * rotOffset, suctionShot);
            }

            // The hit sphere launches at its AUTHORED diameter and is grown by MASS over the
            // flight (growthFactor, below) rather than being pre-multiplied here.
            //
            // bleeding-edge reached the same conclusion independently and landed a static
            // `diameter × sqrt(Multiplier(Mass))` bump in this exact spot (merged 2026-08-13).
            // It is deliberately NOT kept: the two are the same idea at different magnitudes,
            // and stacking them would apply MASS to one quantity twice — at Mass 10 that is
            // 1.58 × 6 = 9.5× on a 2.475 base, ~2× the visible prism's own bounding size,
            // which breaks the honesty rule the growth exists to satisfy. Its intent survives
            // in full: growth covers BOTH fire modes (that bump was turret-only) and reaches
            // 6× at Mass 10 instead of 1.58×.
            LaunchCarriedProjectile(prism, muzzle, shotRotation, velocity, flightTime, domainAtShot,
                piercing, anchorPoint,
                shielded ? so.ShieldedCollisionDiameter : so.CollisionDiameter,
                viz, shielded, dangerous, suctionShot, growthFactor);

            OnBlockShot?.Invoke(_status?.PlayerName);
        }

        /// <summary>
        /// One ReverseSuction shot's lifecycle state, shared by the flight-end handler,
        /// the scheduled reveal, and the effect-completion backstop. The latch makes
        /// prism creation exactly-once no matter which of the three arrives first.
        /// </summary>
        private sealed class ReverseSuctionShot
        {
            public Prism Prism;
            public PrismImplosion Effect;
            public Vector3 BlockScale;
            public Domains Domain;
            public bool Shielded;
            public bool Dangerous;
            public Vector3 LandingPoint;
            public float EffectDuration;
            public float FlightTime;
            public float PlacementImmunitySeconds;
            public bool Created;
        }

        /// <summary>
        /// Exactly-once creation of the real prism for a ReverseSuction shot — where the
        /// assembly stream finished. Pre-scaling makes the creation bloom settle
        /// instantly, so the reveal IS the effect's completed shape.
        /// </summary>
        private void CreateSuctionPrism(ReverseSuctionShot shot)
        {
            if (shot.Created) return;
            shot.Created = true;

            var prism = shot.Prism;
            if (!prism) return;

            prism.transform.position = shot.LandingPoint;
            prism.transform.localScale = shot.BlockScale;
            MakePrismLive(prism, shot.BlockScale, shot.Domain, shot.Shielded, shot.Dangerous,
                true, shot.LandingPoint);
            // Placement immunity from the moment this prism becomes live mass (the suction
            // path creates it AT landing, so no flight span is needed here).
            prism.ProjectileImmuneUntil = Time.time + shot.PlacementImmunitySeconds;
        }

        /// <summary>
        /// The pooled prism becomes real, live mass: team, shield flag, and the one call
        /// this path historically missed — <c>Prism.Initialize</c>, the documented
        /// pool-spawn entry point. Without it IsCreationComplete stays false,
        /// BeginGrowthAnimation early-returns on its pre-creation guard, and the prism
        /// lives at localScale zero: invisible, with a zero-volume collider. Silently.
        /// </summary>
        private void MakePrismLive(Prism prism, Vector3 blockScale, Domains domain,
            bool shielded, bool dangerous, bool fullSize, Vector3 restPoint)
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

            // Full size from the first visible frame: pre-scaling the transform makes the
            // creation stamp's start fraction ~1, so the bloom settles instantly instead
            // of growing in from zero. The flight out of the gun is itself the continuity
            // transition — the shot needs no second one on top.
            if (fullSize)
                prism.transform.localScale = blockScale;

            prism.ChangeTeam(domain);
            // Both flags assigned every shot — ResetState deliberately does not clear
            // them (spawners own them pre-Initialize), so a pool-recycled prism would
            // otherwise inherit its previous life's state.
            prism.prismProperties.IsShielded = shielded;
            prism.prismProperties.IsDangerous = dangerous;
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
        /// projectile under the documented moving-target exception). PrismType.Grow's
        /// first producer. The effect's completion is also the creation BACKSTOP: if the
        /// flight was cancelled (scene teardown mid-shot) and the scheduled reveal never
        /// armed, the mass still gets created when the stream ends.
        /// </summary>
        private PrismImplosion SpawnReverseSuctionEffect(Prism prism, Vector3 blockScale,
            Domains domain, float effectDuration, Vector3 anchorPoint, Quaternion rotation,
            ReverseSuctionShot shot)
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
                GrowDuration = effectDuration,
                OnGrowCompleted = () => CreateSuctionPrism(shot),
            });

            return ret.SpawnedObject ? ret.SpawnedObject.GetComponent<PrismImplosion>() : null;
        }

        /// <summary>Seconds before the effect's completion that the real prism is created,
        /// so the completed stream overlaps the prism's 1–2 frame creation window instead
        /// of leaving a hole at the hand-off.</summary>
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
        private void LaunchCarriedProjectile(Prism prism, Transform muzzle, Quaternion shotRotation,
            Vector3 velocity,
            float flightTime, Domains domainAtShot, bool piercing, Vector3 anchorPoint,
            float hitDiameter, FullAutoBlockShootActionSO.FlightVisualization viz,
            bool shielded, bool dangerous, ReverseSuctionShot suctionShot, float growthFactor)
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

            // Size it EXPLICITLY, and UNIFORMLY. Two reasons. (1) The prism it detached
            // from may still be at localScale ZERO (its creation coroutine has not run),
            // and SetParent(worldPositionStays) preserves world scale — an unsized
            // collider is degenerate and the shot silently hits nothing. (2) The hit
            // volume is the BULLETS' collision approach, verbatim: a sphere trigger whose
            // world diameter is authored to match the bullet's own (1.65 = the tracer's
            // visible 0.75 cross-section radius +10%, doubled; the prism's carried
            // collider is a unit sphere, radius 0.5, so world diameter == this scale).
            // Shielded shots take the larger authored diameter — the armored octahedron
            // reads bigger, so it hits bigger.
            carriedTransform.localScale = Vector3.one * hitDiameter;
            // The SHOT's rotation, not the muzzle's — the round travels down the spread-deflected
            // aim, so its collider must be posed on that line too.
            carriedTransform.SetPositionAndRotation(muzzle.position, shotRotation);
            carried.Velocity = velocity;

            if (carried.TryGetComponent<Collider>(out var col)) col.enabled = true;
            if (carried.TryGetComponent<Rigidbody>(out var rb)) rb.isKinematic = false;

            // MASS growth — set BEFORE launch, which is where the round captures the scale it
            // grows from (so the explicit per-shot sizing above must come first).
            carried.SetFlightGrowth(growthFactor);

            carried.FlightEnded += (p, stoppedByImpact) =>
                AnchorPrism(prism, p, stoppedByImpact, anchorPoint, viz, suctionShot);

            carried.LaunchProjectile(flightTime);
        }

        /// <summary>
        /// Touchpoint 3: the flight is over, so the prism BE its end state.
        ///
        /// TranslateAndGrow: the prism was live the whole flight — settle the flight
        /// stamp (one transform write only when an impact cut it short) and restore the
        /// culling envelope.
        ///
        /// ReverseSuction: the prism is a scale-zero blank and the effect does the
        /// drawing — the assembly stream runs SuctionDurationMultiplier× longer than the
        /// flight, so this only records where the shot died and sequences the reveal:
        /// on a timeout the real prism is created RevealOverlapSeconds before the stream
        /// completes (scheduled, one callback); on an early impact the stream is cut and
        /// the prism is created right there, its own creation bloom carrying the reveal.
        /// The effect's completion is the exactly-once backstop for both.
        /// </summary>
        private void AnchorPrism(Prism prism, Projectile carried, bool stoppedByImpact,
            Vector3 anchorPoint, FullAutoBlockShootActionSO.FlightVisualization viz,
            ReverseSuctionShot suctionShot)
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
                else if (suctionShot != null)
                {
                    if (stoppedByImpact)
                    {
                        // The shot died early. The stream's rest shape sits at maximum
                        // range — the wrong place now — so cut it and let the prism's own
                        // creation bloom at the impact point carry the reveal.
                        suctionShot.LandingPoint = carried ? carried.transform.position : anchorPoint;
                        CreateSuctionPrism(suctionShot);
                        if (suctionShot.Effect && suctionShot.Effect.gameObject.activeSelf)
                            suctionShot.Effect.ReturnToPool();
                    }
                    else
                    {
                        // Full flight: the shot rests at the anchor while the faces keep
                        // streaming in. Reveal the real prism just before the stream
                        // completes so the hand-off has no hole. One scheduled callback —
                        // the law's touchpoint-3 shape.
                        float delay = Mathf.Max(0f,
                            suctionShot.EffectDuration - suctionShot.FlightTime - RevealOverlapSeconds);
                        var shot = suctionShot;
                        PrismTimerManager.EnsureInstance()
                            .ScheduleAction(this, delay, () => CreateSuctionPrism(shot));
                    }
                }
            }
            else if (suctionShot?.Effect && suctionShot.Effect.gameObject.activeSelf)
            {
                // Host prism swept mid-flight (scene teardown) — nothing will land.
                suctionShot.Created = true; // the backstop must not resurrect it
                suctionShot.Effect.ReturnToPool();
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
