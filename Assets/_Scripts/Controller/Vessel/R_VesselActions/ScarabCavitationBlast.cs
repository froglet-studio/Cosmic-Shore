using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Scarab's cavitation blast (design: R_VesselActions/SCARAB.md §3.4) — the mantis-shrimp
    /// punch that rides the dash. Fires along the JUKE direction whenever the pilot dashes and the
    /// blast is off cooldown: it shreds prisms, kills fauna (a creature dies when its body prisms
    /// are destroyed — platform-wide since Wildlife Liberation, so this needs no fauna-specific
    /// code), and debuffs opposing pilots caught in it through the explosion's vessel-effect
    /// container.
    ///
    /// A SPHERE, not a capsule. The Dolphin's blast used a spherical AOE before it was reworked
    /// into the parametric capsule cone, and the earlier feel — a compact round thump placed
    /// down-range — is what this wants; the "cone" is the OFFSET, the blast sitting ahead of you
    /// along the dash rather than centred on the hull.
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
        [Tooltip("The spherical AOE prefab to fire (AOEExplosion-family). Its own authored " +
                 "ExplosionImpactor settings decide what it destroys; its container decides what " +
                 "it does to a vessel it engulfs.")]
        [SerializeField] AOEExplosion blastPrefab;

        [Tooltip("Blast diameter in world units. 'Small' is the point — this is a punch at arm's " +
                 "length, not artillery.")]
        [SerializeField, Min(1f)] float blastScale = 90f;

        [Tooltip("How far along the dash direction the blast centre sits, so it lands AHEAD of " +
                 "the hull rather than on top of it.")]
        [SerializeField, Min(0f)] float forwardOffset = 45f;

        [Header("Cooldown (CHARGE)")]
        [Tooltip("Seconds between blasts at element level 0.")]
        [SerializeField, Min(0f)] float cooldownSeconds = 2.5f;

        [Tooltip("Cooldown multiplier at CHARGE 10 — 0.5 halves it. Read live at fire time.")]
        [SerializeField, Range(0.1f, 1f)] float cooldownMultiplierAtFullCharge = 0.5f;

        IVesselStatus _status;
        ScarabJukeController _juke;
        float _lastFireTime = float.NegativeInfinity;
        bool _wasReady = true;

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
            Vector3 at = ship.position + dir * forwardOffset;

            var blast = Instantiate(blastPrefab, at, Quaternion.LookRotation(dir, ship.up));
            blast.Initialize(new AOEExplosion.InitializeStruct
            {
                OwnDomain = _status.Domain,
                Vessel = _status.Vessel,
                MaxScale = blastScale,
                SpawnPosition = at,
                SpawnRotation = Quaternion.LookRotation(dir, ship.up),
                // Domain-tinted like every other blast in the fleet. Without it the explosion
                // renders with whatever the prefab shipped and reads as nobody's.
                OverrideMaterial = _status.AOEExplosionMaterial,
                // CHARGE 5 — "Cavitation Shear": devastate destroys SHIELDED prisms outright
                // instead of only shedding the shield. Per-use snapshot at fire time.
                DevastatingOverride = _status.ElementalAbilityHandler != null
                                      && _status.ElementalAbilityHandler.IsUpgradeActive(Element.Charge)
            });

            // INITIALIZE ONLY ARMS IT. `Initialize` sets the blast up and deliberately leaves it
            // at zero scale with its renderer OFF; `Detonate` is what starts ExplodeAsync. Missing
            // this call is why the blast never worked — every dash spawned a correctly-configured
            // explosion that then sat inert and invisible forever (and leaked a GameObject with
            // it). Every other AOE call site in the codebase pairs the two.
            blast.Detonate();

            CSDebug.Log($"[ScarabCavitation] Blast along {dir} (cooldown {CurrentCooldown():F2}s).");
        }
    }
}
