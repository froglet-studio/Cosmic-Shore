using CosmicShore.Data;
using CosmicShore.Utility;
using Reflex.Injectors;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Scarab's cavitation blast (design: R_VesselActions/SCARAB.md §3.4) — the mantis-shrimp
    /// punch that rides the dash. Fires along the JUKE direction whenever the pilot dashes and the
    /// blast is off cooldown: it shreds prisms, kills fauna (a creature dies when its body prisms
    /// are destroyed — platform-wide since Wildlife Liberation, so this needs no fauna-specific
    /// code), debuffs opposing pilots caught in it through the explosion's vessel-effect container,
    /// and launches an Astro League ball it reaches.
    ///
    /// A SWEPT PLATE, not a sphere. The blast is a circular disc whose face normal is the dash
    /// direction — so it lies flat ACROSS the vessel's course — starting centred on the hull and
    /// sweeping along its own normal (<see cref="AOECylindricalExplosion"/>). Everything it claims
    /// is thrown the way the plate travelled rather than away from a point, which is what makes it
    /// read as a slap that carries mass down-range instead of a bomb that blooms.
    ///
    /// ITS SIZE IS THE HULL'S SIZE, MEASURED, NOT AUTHORED. The plate's radius is
    /// <see cref="radiusPerVesselRadius"/> × the vessel's own collider radius and its length a
    /// further multiple of that radius (<c>AOECylindricalExplosion.lengthPerRadius</c>), read live off
    /// <see cref="VesselImpactor.HullColliders"/> at fire time. So the punch is stated as a
    /// RELATIONSHIP to the ship rather than as a second number beside it: reshape the Scarab's hull
    /// collider and the blast follows, with nothing left behind to drift.
    ///
    /// THE DASH IS FREE; ONLY THE BLAST IS PACED. <see cref="ScarabJukeController"/> has no
    /// cooldown of its own, so a pilot can juke as often as they like — this component simply
    /// declines to fire while it is recharging. That split is deliberate: dodging is mobility and
    /// should never be rationed, while the destructive punch is a resource.
    ///
    /// CHARGE scales the cooldown (×<see cref="cooldownMultiplierAtFullCharge"/> at level 10, the
    /// fleet's authored-cooldown idiom — the Squirrel's boost ring and the Dolphin's crystal seed
    /// both work this way, with the map's generic multiplier pinned to 1 so nothing double-dips).
    /// CHARGE 5 unlocks "Cavitation Shear": the blast destroys SHIELDED prisms outright instead of
    /// merely shedding their shields.
    /// </summary>
    [RequireComponent(typeof(ScarabJukeController))]
    public class ScarabCavitationBlast : MonoBehaviour
    {
        [Header("Blast")]
        [Tooltip("The swept-plate AOE prefab to fire (AOECylindricalExplosion). Its own authored " +
                 "ExplosionImpactor settings decide what it destroys; its container decides what " +
                 "it does to a vessel it engulfs.")]
        [SerializeField] AOEExplosion blastPrefab;

        [Tooltip("The plate's radius as a multiple of the VESSEL COLLIDER's radius. Shipped at " +
                 "10, so the punch is ten times as wide as the ship that threw it - a broad wall " +
                 "of destruction sweeping off the hull, not a jab. The plate's LENGTH is a " +
                 "multiple of THAT radius, authored on the blast prefab (1.2).")]
        [SerializeField, Min(0.1f)] float radiusPerVesselRadius = 10f;

        [Tooltip("Fallback VESSEL radius in world units, used only if the hull collider cannot " +
                 "be measured. Matches the shipped Scarab hull, a single 4.5-unit sphere.")]
        [SerializeField, Min(0.1f)] float fallbackVesselRadius = 4.5f;

        [Header("Cooldown (CHARGE)")]
        [Tooltip("Seconds between blasts at element level 0.")]
        [SerializeField, Min(0f)] float cooldownSeconds = 2.5f;

        [Tooltip("Cooldown multiplier at CHARGE 10 — 0.5 halves it. Read live at fire time.")]
        [SerializeField, Range(0.1f, 1f)] float cooldownMultiplierAtFullCharge = 0.5f;

        IVesselStatus _status;
        ScarabJukeController _juke;
        VesselImpactor _vesselImpactor;
        float _lastFireTime = float.NegativeInfinity;
        bool _wasReady = true;
        bool _warnedUnmeasurableHull;

        /// <summary>True while the blast is ready — the HUD's Charge-row readout.</summary>
        public bool IsBlastReady => Time.time - _lastFireTime >= CurrentCooldown();

        /// <summary>
        /// Raised on every ready↔recharging edge, carrying the cooldown length that edge was
        /// measured against so a HUD can drive a radial sweep with no polling of its own. Fired
        /// IMMEDIATELY on use (not on the next frame's poll) so the readout is zero-latency.
        /// </summary>
        public event System.Action<bool, float> OnBlastReadyChanged;

        void Awake()
        {
            _status = GetComponent<VesselStatus>();
            _juke = GetComponent<ScarabJukeController>();
            _vesselImpactor = GetComponent<VesselImpactor>();
        }

        void OnEnable()
        {
            if (_juke != null) _juke.OnJukeFired += HandleJukeFired;
        }

        void OnDisable()
        {
            if (_juke != null) _juke.OnJukeFired -= HandleJukeFired;
        }

        // One bool compare per frame. The recharge edge has no other trigger to hang off — the
        // fire edge is raised inline below — and a scheduler entry for a sub-second readout would
        // cost more than it saves.
        void Update()
        {
            bool ready = IsBlastReady;
            if (ready == _wasReady) return;
            _wasReady = ready;
            OnBlastReadyChanged?.Invoke(ready, CurrentCooldown());
        }

        /// <summary>
        /// CHARGE shortens the wait. The authored multiplier is what the cooldown reaches at
        /// level 10, so this is the fleet's authored-cooldown idiom rather than the map's generic
        /// scaler (which stays pinned to 1 for Charge — no double-dip). Clamped to [0, 1] because
        /// the normalized band runs to 1.5 in overcharge and a maintained mechanism must not keep
        /// paying past level 10.
        /// </summary>
        float CurrentCooldown()
        {
            float t = Mathf.Clamp01(ElementalScaling.Level01(_status, Element.Charge));
            return cooldownSeconds * Mathf.Lerp(1f, cooldownMultiplierAtFullCharge, t);
        }

        /// <summary>
        /// The vessel's own collider radius in WORLD units, measured off
        /// <see cref="VesselImpactor.HullColliders"/> — the hull set the shell tier already probes
        /// with, which excludes the skimmer (it owns its own Rigidbody) and so cannot mistake the
        /// skim sphere for the ship. The Scarab authors a single sphere; the box/capsule branches
        /// exist so this survives a vessel that has not been converted, and every branch returns a
        /// CIRCUMSCRIBING radius so the blast can only ever be sized generously, never clipped.
        /// Measured per fire rather than cached: a vessel can be rescaled at runtime, and this runs
        /// at most a few times a second.
        /// </summary>
        float ResolveVesselColliderRadius()
        {
            var colliders = _vesselImpactor != null ? _vesselImpactor.HullColliders : null;
            float best = 0f;

            if (colliders != null)
            {
                for (int i = 0; i < colliders.Length; i++)
                {
                    var col = colliders[i];
                    if (col == null) continue;

                    Vector3 lossy = col.transform.lossyScale;
                    float maxAxis = Mathf.Max(Mathf.Abs(lossy.x),
                                    Mathf.Max(Mathf.Abs(lossy.y), Mathf.Abs(lossy.z)));

                    switch (col)
                    {
                        // Unity scales a sphere collider by the LARGEST axis - measure it the same way.
                        case SphereCollider sphere:
                            best = Mathf.Max(best, sphere.radius * maxAxis);
                            break;
                        // Half-diagonal: the smallest sphere that contains the box.
                        case BoxCollider box:
                            best = Mathf.Max(best, Vector3.Scale(box.size * 0.5f, lossy).magnitude);
                            break;
                        // Half-height already includes the caps, so it circumscribes the capsule.
                        case CapsuleCollider capsule:
                            best = Mathf.Max(best,
                                Mathf.Max(capsule.radius, capsule.height * 0.5f) * maxAxis);
                            break;
                        default:
                            best = Mathf.Max(best, col.bounds.extents.magnitude);
                            break;
                    }
                }
            }

            if (best > 0f) return best;

            if (!_warnedUnmeasurableHull)
            {
                _warnedUnmeasurableHull = true;
                CSDebug.LogError(
                    $"[ScarabCavitation] Could not measure a hull collider on {name} - the blast " +
                    $"is falling back to a fixed {fallbackVesselRadius} vessel radius. The hull " +
                    "collider is the blast's only size input; check the prefab.");
            }
            return fallbackVesselRadius;
        }

        void HandleJukeFired(Vector3 direction)
        {
            if (_status == null || blastPrefab == null) return;
            if (!IsBlastReady) return;                       // the DASH already happened — only the punch waits
            if (direction.sqrMagnitude < 1e-4f) return;

            _lastFireTime = Time.time;
            _wasReady = false;
            OnBlastReadyChanged?.Invoke(false, CurrentCooldown());

            var ship = _status.ShipTransform ? _status.ShipTransform : transform;
            Vector3 dir = direction.normalized;

            // The plate STARTS CENTRED ON THE VESSEL and sweeps forward from there, so there is no
            // forward offset to author: the punch leaves the hull rather than appearing ahead of it.
            Vector3 at = ship.position;

            // THE UP-HINT MUST NOT BE PARALLEL TO THE DASH. A straight up/down juke produces a
            // shove of exactly ship.up (the keyboard's right stick is DIGITAL, so it hands over
            // exactly (0,1), and ProjectOnPlane leaves it untouched while flying straight), and
            // LookRotation(up, up) cannot resolve a basis — it returns identity. That was harmless
            // while the blast was a SPHERE, which is what it was when this line was written; the
            // plate reads its sweep axis straight off this rotation (`_axis = transform.forward`),
            // so a degenerate one silently aims the whole punch along world +Z while the player
            // watches their ship dash upward. The existing `sqrMagnitude < 1e-8` guard cannot catch
            // it either: identity yields a perfectly unit forward. The plate is rotationally
            // symmetric about its axis, so the hint carries no meaning here beyond being usable —
            // unlike the cone's gape axis, which IS gameplay.
            Vector3 upHint = Mathf.Abs(Vector3.Dot(dir, ship.up)) > 0.999f ? ship.forward : ship.up;
            var rotation = Quaternion.LookRotation(dir, upHint);

            float radius = ResolveVesselColliderRadius() * radiusPerVesselRadius;

            var blast = Instantiate(blastPrefab, at, rotation);

            // INJECT BEFORE Initialize, or the blast has no turn-end kill switch. AOEExplosion's
            // gameData is [Inject], and it is the ONLY thing that wires OnMiniGameTurnEnd ->
            // CancelExplosion and OnResetForReplay -> PerformResetCleanup. Nothing in
            // Controller/Environment or a bare Instantiate injects, so this blast was the one AOE
            // in the game that kept sweeping and destroying mass after a turn ended and survived a
            // replay reset — silently, because the null-guard that swallows it is written exactly
            // as a good null-guard is (CLAUDE.md's anti-pattern on relying on [Inject] at a
            // non-injecting spawn site). The vessel's own impactor carries the container, so there
            // is one to hand over; ExplosionHelper does the same thing off impactor.DIContainer.
            // Ordering matters: Initialize's subscription retry runs after injection by design.
            var container = _vesselImpactor != null ? _vesselImpactor.DIContainer : null;
            if (container != null)
                GameObjectInjector.InjectRecursive(blast.gameObject, container);

            blast.Initialize(new AOEExplosion.InitializeStruct
            {
                OwnDomain = _status.Domain,
                Vessel = _status.Vessel,
                // MaxScale is the plate's DIAMETER, matching the rest of the AOE family. Its LENGTH
                // is derived on the blast (lengthPerRadius = 2), so the shape rule lives in one place.
                MaxScale = radius * 2f,
                SpawnPosition = at,
                SpawnRotation = rotation,
                // Domain-tinted like every other blast in the fleet. Without it the explosion
                // renders with whatever the prefab shipped and reads as nobody's.
                OverrideMaterial = _status.AOEExplosionMaterial,
                // CHARGE 5 — "Cavitation Shear": devastate destroys SHIELDED prisms outright
                // instead of only shedding the shield. Per-use snapshot at fire time.
                DevastatingOverride = _status.ElementalAbilityHandler != null
                                      && _status.ElementalAbilityHandler.IsUpgradeActive(Element.Charge)
            });

            // INITIALIZE ONLY ARMS IT. `Initialize` sets the blast up and deliberately leaves it
            // at zero extent with its renderer OFF; `Detonate` is what starts ExplodeAsync. Missing
            // this call is why the blast never worked — every dash spawned a correctly-configured
            // explosion that then sat inert and invisible forever (and leaked a GameObject with
            // it). Every other AOE call site in the codebase pairs the two.
            blast.Detonate();

            if (CSDebug.IsVerbose(CSLogChannel.ScarabDash))
                CSDebug.LogVerbose(CSLogChannel.ScarabDash,
                    $"[ScarabCavitation] Plate r={radius:F1} along {dir} " +
                    $"(cooldown {CurrentCooldown():F2}s).");
        }
    }
}
