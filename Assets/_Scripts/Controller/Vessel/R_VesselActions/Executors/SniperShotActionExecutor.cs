using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using Reflex.Attributes;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Serpent's <b>sniper shot</b>: one hitscan round down the scope's own line, destroying
    /// what it hits — <b>including SUPER-SHIELDED mass</b> — on a long cooldown.
    ///
    /// <para><b>It only fires while the scope is up.</b> The right trigger carries both this and
    /// the Serpent's cloak; <see cref="SniperScopeActionExecutor.IsScoped"/> tells them apart, so
    /// scoped RT is the rifle and unscoped RT is still the cloak. The two abilities never learn
    /// about each other's wiring — each asks the scope. That flag is maintained on EVERY peer
    /// precisely so this gate resolves the same way everywhere.</para>
    ///
    /// <para><b>The line is the SCOPE's line.</b> The round leaves along the vessel's forward
    /// axis, which is exactly the axis the scope window's camera is posed along
    /// (<c>ScopePipView.PoseCamera</c> writes <c>vessel.rotation</c>), so the shot lands where the
    /// eyepiece is pointing and the reticle is a promise rather than a decoration. Deriving the
    /// direction from <c>Course</c> instead would put the round somewhere the pilot is not looking
    /// the moment the vessel is sliding.</para>
    ///
    /// <para><b>Why the hitscan is a CONE and not a ray — or a tube.</b>
    /// <see cref="PrismSpatialIndex.QueryCone"/> tests a prism's CENTRE, and a prism is several
    /// units across, so a mathematical line threaded through a lattice of centres misses almost
    /// everything it visually passes through. A fixed-radius capsule fixes that and introduces a
    /// worse problem: it is measured in world units while the pilot aims in ANGLE, so the first
    /// cut's 4 u path was a blunderbuss at the muzzle and 0.076° — about 7 px inside the scope —
    /// at its 3,000 u reach. The cone covers the same on-screen area at every range, which is what
    /// makes "put the reticle on it" mean one thing, and it is what lets the scope HUD draw a
    /// reticle at the beam's TRUE size rather than at a guess.</para>
    ///
    /// <para><b>The cone is deliberately NOT a function of zoom.</b> Widening it as the pilot zooms
    /// out would be the obvious next step and would desync the prismscape: the zoom is a LOCALLY
    /// smoothed value (<c>SniperScopeActionExecutor.Zoom01</c> eases toward the trigger every
    /// frame on the owner's machine only), and this shot resolves on EVERY peer. Making destruction
    /// depend on it would have each machine destroying a different set of conserved mass. The
    /// angular size is authored, so every peer's cone is the same cone.</para>
    ///
    /// <para><b>Breaking a super-shield uses the ONE sanctioned sequence.</b> Super-shielded mass
    /// is invulnerable to <c>Prism.Damage</c> outright — it early-returns through
    /// <c>AbsorbSuperShieldHit</c> — so the shields are dropped FIRST and the prism is then
    /// devastated, which is precisely what the Rhino's energised blade does
    /// (<c>RhinoSkimmerDamagePrismEffectSO.PopSuperShield</c>). Both calls are handed the same
    /// impact vector and the same true-velocity ceiling, so the stellation sheds as ordinary
    /// explosion debris on the same terms as the prism's own pieces — one effect, two meshes
    /// (Docs/PRISM_ANIMATION.md §4.8.1). Nothing pops out of existence and mass is conserved.</para>
    ///
    /// <para><b>Element → parameter: CHARGE sets the RECOVERY</b> (how long between shots), read at
    /// use time. Its level-5 upgrade is <b>Pierce</b> — the round no longer stops at the first
    /// prism — and that one gates on the REPLICATED <c>IsUpgradeActive(Element.Charge)</c> rather
    /// than a local level read, because it decides how much mass dies and every peer must agree
    /// (CLAUDE.md, the outcome-affecting-upgrade rule).</para>
    ///
    /// <para><b>Known limitation — the cooldown's element scaling is owner-local.</b> Element
    /// levels never replicate, so a remote replica cannot derive the owner's Charge-scaled
    /// cooldown. The press itself DOES replicate, and the owner is the only machine that can
    /// produce one, so the owner's cooldown is the only one that can be authoritative: a
    /// non-owner therefore enforces only a FLOOR (the fastest this ability can ever recover),
    /// which can never refuse a shot the owner admitted while still bounding a duplicated or
    /// replayed press. The alternative — replicating the resolved cooldown the way
    /// <c>R_VesselActionHandler.NetEchoSightShape</c> replicates the Dolphin's cone — is the fix
    /// if this ever needs to be exact, and is deliberately not paid for here.</para>
    /// </summary>
    public sealed class SniperShotActionExecutor : ShipActionExecutorBase
    {
        [Header("Config")]
        [Tooltip("Wired directly so a missing wire is visible in the inspector rather than " +
                 "silently falling back to field initializers.")]
        [SerializeField] private SniperShotActionSO config;

        [Header("Scope")]
        [Tooltip("The scope this shot is gated on. Resolved from the registry when left empty.")]
        [SerializeField] private SniperScopeActionExecutor scope;

        [Header("Audio")]
        [Tooltip("FMOD event for the shot. Leave EMPTY for silence - never point it at a " +
                 "borrowed event (CLAUDE.md, the audio convention).")]
        [SerializeField] private FMODUnity.EventReference fireEvent;

        // The player roster carries the shared ColorSet, which is where the tracer's domain colour
        // comes from. Injected rather than searched: vessels DO get
        // GameObjectInjector.InjectRecursive at spawn, so this resolves on every spawn path.
        [Inject] GameDataSO _gameData;

        IVesselStatus _status;
        ActionExecutorRegistry _registry;

        float _cooldownEndTime;

        // Reused so a shot allocates nothing on the hot path.
        readonly List<Prism> _hits = new();
        readonly List<(Prism prism, float distance)> _ordered = new();

        /// <summary>Seconds until the next shot is available, 0 when ready. Read by the HUD.</summary>
        public float CooldownRemaining => Mathf.Max(0f, _cooldownEndTime - Time.time);

        /// <summary>
        /// Fraction of the cooldown still to run: 1 the instant the shot fires, 0 when the next
        /// one is available. This is the sense <c>VesselHUDView.SetAbilityCooldown</c> wants —
        /// the fleet's clockwise veil DEPLETES, so the number it takes is what is LEFT.
        /// </summary>
        public float CooldownRemaining01
        {
            get
            {
                float total = ResolveCooldownSeconds();
                if (total <= 0f) return 0f;
                return Mathf.Clamp01(CooldownRemaining / total);
            }
        }

        /// <summary>
        /// The authored half-angle of the round's path, in degrees — what the scope's reticle is
        /// drawn at. Exposed so the readout is a measurement of THIS weapon rather than a second
        /// number that can drift from it.
        /// </summary>
        public float ConeHalfAngleDegrees => config != null ? config.ConeHalfAngleDegrees : 0.5f;

        /// <summary>
        /// The firing pilot's domain colour. One resolver for the tracer and the reticle, so the
        /// mark the pilot aims with and the mark the shot leaves can never disagree about whose
        /// shot it was.
        /// </summary>
        public Color TracerColour => ResolveTracerColour();

        public override void Initialize(IVesselStatus shipStatus)
        {
            _status = shipStatus;
            // GetComponentInParent, NOT GetComponent: on every shipped vessel the registry lives
            // on the "ShipActions" container and each executor sits on a CHILD of it, so a
            // same-object lookup returns null and every fallback below it is silently dead.
            // (ToggleTranslationModeActionExecutor carries that bug today.) includeInactive so a
            // vessel initialized while deactivated still resolves.
            _registry = GetComponentInParent<ActionExecutorRegistry>(true);

            if (scope == null && _registry != null)
                scope = _registry.Get<SniperScopeActionExecutor>();

            // A re-init hands this component to a different pilot. The previous pilot's cooldown
            // is not this one's.
            _cooldownEndTime = 0f;
        }

        public void Fire(SniperShotActionSO so, IVesselStatus status)
        {
            if (so == null) return;
            if (_status == null) _status = status;
            if (_status == null) return;

            // The whole reason this ability and the cloak can share a trigger.
            if (scope == null || !scope.IsScoped) return;

            if (Time.time < _cooldownEndTime) return;

            _cooldownEndTime = Time.time + ResolveAppliedCooldown(so);

            ResolveShot(so);
        }

        /// <summary>
        /// The cooldown THIS machine enforces. The owner (and the non-networked single-player
        /// path, which is the only machine there is) uses the real Charge-scaled value; every
        /// other peer uses the floor. See the class summary.
        /// </summary>
        float ResolveAppliedCooldown(SniperShotActionSO so)
        {
            if (IsAuthoritativeMachine) return ResolveCooldownSeconds(so);
            return so.CooldownSeconds * so.CooldownMultiplierAtFullCharge;
        }

        bool IsAuthoritativeMachine
        {
            get
            {
                var handler = _status?.ActionHandler;
                // Not spawned == the non-networked single-player path: this is the only machine.
                if (handler == null || !handler.IsSpawned) return true;
                return _status.IsNetworkOwner;
            }
        }

        float ResolveCooldownSeconds() => ResolveCooldownSeconds(config);

        float ResolveCooldownSeconds(SniperShotActionSO so)
        {
            if (so == null) return 0f;
            // Charge SHORTENS the wait, so its multiplier is below 1 at full. minMul is the
            // authored multiplier itself: the element may reach its designed floor and no
            // further, so an overcharged Serpent cannot drive the cooldown toward zero.
            float mul = ElementalScaling.Multiplier(
                _status, Element.Charge,
                atFull: so.CooldownMultiplierAtFullCharge,
                minMul: so.CooldownMultiplierAtFullCharge);
            return so.CooldownSeconds * mul;
        }

        void ResolveShot(SniperShotActionSO so)
        {
            var ship = _status.ShipTransform;
            if (ship == null) return;

            Vector3 origin = ship.position;
            Vector3 direction = ship.forward;
            Vector3 stop = origin + direction * so.RangeUnits;
            bool hit = false;

            var index = PrismSpatialIndex.EnsureInstance();
            if (index != null && index.IsAvailable)
            {
                index.QueryCone(origin, direction, so.RangeUnits, so.ConeHalfAngleDegrees,
                                so.MinPathRadius, _hits);

                // QueryCone's snapshot is UNORDERED (it walks buckets), and a sniper round has to
                // stop at the FIRST thing it reaches, so the hits are ordered along the axis here.
                _ordered.Clear();
                for (int i = 0; i < _hits.Count; i++)
                {
                    var prism = _hits[i];
                    if (!IsValidTarget(prism)) continue;
                    _ordered.Add((prism, Vector3.Dot(prism.transform.position - origin, direction)));
                }
                _ordered.Sort((a, b) => a.distance.CompareTo(b.distance));

                bool pierces = IsPierceUnlocked;
                int budget = pierces
                    ? (so.PierceCount <= 0 ? int.MaxValue : so.PierceCount)
                    : 1;

                int killed = 0;
                for (int i = 0; i < _ordered.Count && killed < budget; i++)
                {
                    var prism = _ordered[i].prism;
                    // Re-tested: the list is a snapshot, and destroying a prism can destroy others
                    // through its own side effects (QueryCone's own documented contract). The stop
                    // point is read BEFORE the kill, because a destroyed prism's transform is on
                    // its way back to the pool.
                    if (!IsValidTarget(prism)) continue;

                    stop = prism.transform.position;
                    hit = true;
                    DestroyPrism(prism, so, direction);
                    killed++;
                }
            }

            DrawTracer(so, origin, direction, stop, hit);
            PlayReport(so);
        }

        /// <summary>
        /// The tracer, drawn at the CONE's own radius at each end, so what the pilot sees is the
        /// volume the shot tested rather than a decorative line through the middle of it.
        ///
        /// It runs on every peer for free, because this whole method is reached from a replicated
        /// press — see the class summary.
        /// </summary>
        void DrawTracer(SniperShotActionSO so, Vector3 origin, Vector3 direction, Vector3 stop,
            bool hit)
        {
            if (so.BeamSeconds <= 0f) return;

            float distance = Vector3.Distance(origin, stop);
            float endRadius = Mathf.Max(so.MinPathRadius,
                distance * Mathf.Tan(so.ConeHalfAngleDegrees * Mathf.Deg2Rad));

            SniperBeam.Fire(origin, stop,
                startWidth: so.BeamStartWidth,
                endWidth: endRadius * 2f,
                colour: ResolveTracerColour(),
                beamSeconds: so.BeamSeconds,
                flareRadius: so.ImpactFlareRadius,
                flareSeconds: so.ImpactFlareSeconds,
                hit: hit);
        }

        /// <summary>
        /// The firing vessel's own domain colour at full strength, read LIVE off the shared
        /// ColorSet — the path every other domain-tinted surface reads, so the freestyle
        /// domain-changer toy re-colours the next shot and nothing is snapshotted at
        /// component-creation time. White is the honest fallback: <c>Domains.Blue</c> is the
        /// platform's "no team" sentinel and <c>GetDomainSignalColor</c> already answers white
        /// for it.
        /// </summary>
        Color ResolveTracerColour()
        {
            var colorSet = _gameData?.ThemeManagerData?.ColorSet;
            var domain = _status?.Player != null ? _status.Domain : Domains.Blue;
            return colorSet != null ? colorSet.GetDomainSignalColor(domain) : Color.white;
        }

        bool IsValidTarget(Prism prism)
        {
            if (prism == null || prism.destroyed) return false;
            // Never eat your own wall. The Serpent's whole identity is the mass it weaves, and a
            // rifle that cuts through it would make the two abilities fight each other.
            // Domains.Blue is the neutral sentinel and stays hostile to everyone.
            return prism.Domain != _status.Domain;
        }

        /// <summary>
        /// Destroy one prism, dropping a super-shield first when it has one. Both branches carry
        /// a TRUE-velocity impact vector and the matching ceiling, so the debris flies at the
        /// authored speed rather than saturating the explosion prefab's legacy 33.33 u/s clamp.
        /// </summary>
        void DestroyPrism(Prism prism, SniperShotActionSO so, Vector3 direction)
        {
            Vector3 impact = direction * so.DebrisSpeed;

            if (prism.prismProperties is { IsSuperShielded: true })
            {
                // THE SANCTIONED SEQUENCE, IN THIS ORDER. Damage() hard-ignores super-shielded
                // mass, so the shields come off first; devastate then stops the prism restoring
                // its armour instead of dying.
                prism.DeactivateShields(impact, so.DebrisSpeedLimit);
                prism.Damage(impact, _status.Domain, _status.PlayerName,
                             devastate: true, debrisSpeedLimit: so.DebrisSpeedLimit);
                return;
            }

            // devastate on the ordinary path too: a rifle round that merely sheds a plain
            // shield would make an armoured prism take two shots on a twelve-second cooldown.
            prism.Damage(impact, _status.Domain, _status.PlayerName,
                         devastate: true, debrisSpeedLimit: so.DebrisSpeedLimit);
        }

        bool IsPierceUnlocked
        {
            get
            {
                var elemental = _status?.ElementalAbilityHandler;
                return elemental != null && elemental.IsUpgradeActive(Element.Charge);
            }
        }

        /// <summary>
        /// The shot's own feedback: a camera kick for the pilot who fired and the report for
        /// everyone. Local-pilot-only for the shake, because a camera is a thing one machine has.
        /// </summary>
        void PlayReport(SniperShotActionSO so)
        {
            if (!fireEvent.IsNull)
                _registry?.AudioSystem?.PlaySFXEvent(fireEvent, _status.ShipTransform.position);

            if (so.ShakeIntensity <= 0f) return;
            if (_status.Player == null || !_status.Player.IsLocalPilot) return;

            var controller = CameraManager.Instance != null
                ? CameraManager.Instance.GetActiveController() as CustomCameraController
                : null;
            controller?.Shake(so.ShakeIntensity, so.ShakeDuration);
        }
    }
}
