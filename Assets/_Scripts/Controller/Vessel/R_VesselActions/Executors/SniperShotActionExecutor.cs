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
        readonly List<Transform> _vesselScratch = new();

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
        /// How far the round reaches, in world units — the anchor the FLIGHT view's reticle is
        /// projected at.
        ///
        /// <para>The eyepiece does not need it: that camera sits ON the shot's own axis, so the
        /// cone projects to a circle about the centre of the picture whatever range you pick. The
        /// gameplay camera does not, so the axis projects to a POINT only where that camera is
        /// behind the hull and on its line — which is the Serpent's steady state (its authored
        /// follow offset is a pure <c>(0, 0, -250)</c>) and is NOT true while the camera's
        /// smoothing is catching up through a turn. Projecting the point the shot actually reaches
        /// is right in both cases; assuming screen centre is right in one of them.</para>
        /// </summary>
        public float RangeUnits => config != null ? config.RangeUnits : 3000f;

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

                // THE ROUND PIERCES BY DEFAULT. It used to stop at the first prism unless the
                // Charge-5 upgrade was up, which made an un-upgraded rifle on a twelve-second
                // cooldown worth exactly one prism - measured by a pilot as "the destruction was
                // small" in the same breath as the cone being too wide. A sniper round's whole
                // proposition is the hole it leaves, so the budget is the AUTHORED number and 0
                // is unlimited; what Charge 5 buys is stated one method down, in what counts as
                // a target at all.
                int budget = so.PierceCount <= 0 ? int.MaxValue : so.PierceCount;

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

            StripVessels(so, origin, direction, stop);

            DrawTracer(so, origin, direction, stop, hit);
            PlayReport(so);
        }

        /// <summary>
        /// THE SERPENT'S ANTI-VESSEL VERB. The round strips elements from every opposing pilot
        /// standing in the cone it just tested, and the petals are EJECTED - knocked out of the
        /// victim's hull as free-for-all crystals rather than handed to the sniper, because a
        /// ranged verb cannot take what it never touched (<see cref="ElementalTransfer"/>).
        ///
        /// <para><b>Why this exists at all.</b> The Serpent was the one hull in the fleet with no
        /// way to affect another pilot: no skimmer, no projectile container, no blast - its only
        /// weapon is this hitscan, and the hitscan queried PRISMS and nothing else. So it was
        /// excluded from Broadside outright, and under the elemental economy it would have been
        /// the only vessel that could be robbed and could never rob anyone.</para>
        ///
        /// <para><b>It reuses the round's OWN cone.</b> The test is
        /// <see cref="PrismSpatialIndex.ConeContains"/>, the same public predicate
        /// <c>QueryCone</c> applies to prisms, with the same apex, axis, range, half-angle and
        /// minimum path radius. One cone, one answer: a pilot the tracer visibly passes through
        /// cannot be missed by arithmetic that disagrees with the mass around them.</para>
        ///
        /// <para><b>It stops where the round stopped.</b> With the shipped unlimited pierce that
        /// is the full range, but a limited <c>PierceCount</c> parks the round at a prism, and a
        /// pilot standing behind that prism must not be stripped by a round that never got there.
        /// </para>
        ///
        /// <para>The roster is <see cref="VesselVisionShading.CollectStampedVessels"/> - the
        /// vision band's own live handle, which that platform law maintains because the law
        /// depends on it being right, and which excludes the toybox's mini hulls by construction.
        /// It is a handful of entries, so the sweep is O(pilots) and allocates nothing.</para>
        /// </summary>
        void StripVessels(SniperShotActionSO so, Vector3 origin, Vector3 direction, Vector3 stop)
        {
            if (so == null || so.VesselStripPerElement <= 0f || _status == null) return;

            float reach = Vector3.Dot(stop - origin, direction);
            if (reach <= 0f) return;

            float tanHalf = Mathf.Tan(Mathf.Deg2Rad * Mathf.Clamp(so.ConeHalfAngleDegrees, 0f, 89f));
            Vector3 launch = direction * so.VesselEjectSpeed;

            VesselVisionShading.CollectStampedVessels(_vesselScratch);
            for (int i = 0; i < _vesselScratch.Count; i++)
            {
                var candidate = _vesselScratch[i];
                if (candidate == null) continue;

                var component = candidate.GetComponentInChildren<VesselStatus>();
                if (component == null) continue;

                // Typed as the INTERFACE from here on, because Domain is a DEFAULT INTERFACE
                // MEMBER (IVesselStatus implements it over Player) and a default member is
                // reachable only through the interface - VesselStatus itself does not declare
                // one. The Unity null check above is deliberately done on the concrete
                // reference first: `== null` on an interface-typed variable is a plain
                // reference compare and misses a destroyed Object.
                IVesselStatus victim = component;

                // Never yourself, never a team-mate. The own-domain rule is the same one every
                // other anti-vessel effect in the fleet applies, and it is what stops a Serpent
                // paying for shooting its own wingman.
                if (ReferenceEquals(victim, _status)) continue;
                if (victim.Domain == _status.Domain) continue;

                if (!PrismSpatialIndex.ConeContains(component.transform.position, origin, direction,
                                                    reach, tanHalf, so.MinPathRadius))
                    continue;

                // Classed Other: this is a gun round rather than a blast or a contact, so neither
                // the Explosion nor the VesselContact ward should stop it, and only a pilot warded
                // against everything is spared.
                ElementalTransfer.ApplyAll(ElementalTransferForm.Eject, victim, attacker: null,
                                           so.VesselStripPerElement, launch,
                                           ElementalDebuffSources.Other);
            }
            _vesselScratch.Clear();
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
            if (prism.Domain == _status.Domain) return false;

            // ARMOUR IS A TARGET ONLY AT CHARGE 5, and this is what that upgrade now buys. Below
            // it a super-shielded prism is not a target at all, so the round passes THROUGH it and
            // carries on to whatever is behind - rather than stopping on mass it cannot break,
            // which is what an ordinary damage call would do to it silently (Prism.Damage
            // hard-ignores super-shielded mass, so the round would end its life there having
            // destroyed nothing). "Pierce" therefore names a CAPABILITY rather than a count: how
            // many prisms the round goes through is the weapon's own authored budget at every
            // tier, and what it can go through is the element's.
            if (!IsPierceUnlocked && prism.prismProperties is { IsSuperShielded: true }) return false;

            return true;
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

        /// <summary>
        /// The Charge-5 <b>Pierce</b> upgrade: whether this round may break SUPER-SHIELDED mass.
        /// Below it armour is not a target and the round flies past it (see
        /// <see cref="IsValidTarget"/>); at it, the sanctioned teardown in
        /// <see cref="DestroyPrism"/> runs and the Serpent is the fleet's second force that can
        /// take armour off, after the Rhino's energised blade.
        /// </summary>
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
