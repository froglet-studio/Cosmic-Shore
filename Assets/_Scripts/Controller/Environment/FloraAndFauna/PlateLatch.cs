using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A rider's grip on a MOVING plate (Scarab Reef's "piranha plate-eaters", Docs/Voyages/ARK_ESCORT.md).
    ///
    /// A forager <see cref="Boid"/> that reaches a prism it meant to eat and finds the prism has moved
    /// under it since the behaviour tick chose it (the Ark's hull, any towed mass - still mass is just
    /// eaten) hangs on instead of biting once: this component remembers the grip and where on it the
    /// creature holds, and every frame eases the creature's own transform to that seat, so it rides
    /// the plate. The Boid keeps taking mouthfuls of the plates AROUND its grip on its ordinary
    /// consume cadence (never the grip itself - the grip is what it holds), so a latched school
    /// strips a ring of plates. It lets go when the ride is over (<see cref="Configure"/> seconds),
    /// when the grip is eaten or retired by someone else, when there is nothing edible left around
    /// it, or when a vessel passes inside the scare radius; the release is a fling away from the grip
    /// (the substrate leech's numbers, GripLoss 1.6 / FlingSpeed 90), and a short cooldown stops it
    /// re-latching the plate it was just pulled off.
    ///
    /// Invariants it keeps: it writes ONLY the creature's own transform (position and rotation), never
    /// a HealthPrism's localPosition - the body prism follows its parent and the Boid's own
    /// NotifyBodyPrismsMoved keeps the index honest; the latch eases in and the release is a velocity,
    /// so nothing pops; no Ark seek - the plate is found by the ordinary densest-mass goal, and the
    /// test is "did it move", not "whose is it". The Boid ADDS this component at runtime when its
    /// config authors <see cref="Utility.FaunaConfigurationSO.PlateLatchSeconds"/>; no prefab carries it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlateLatch : MonoBehaviour
    {
        /// <summary>A plate that drifted at least this far (world units) between being chosen and being bitten is MOVING.</summary>
        public const float MovingPlateMinDrift = 0.35f;
        /// <summary>Seconds after a release before the rider may latch again (stops a flung rider re-gripping at once).</summary>
        const float RelatchCooldownSeconds = 3f;
        /// <summary>Per-second ease of the body onto its seat (one exponential lerp; ~95% seated in 0.4 s).</summary>
        const float SeatSharpness = 8f;
        /// <summary>The seat is clamped to this many units from the grip's centre, so the body hugs the plate.</summary>
        const float SeatReach = 6f;

        static readonly Collider[] ScareScratch = new Collider[16];

        float _rideSeconds;
        float _flingSpeed;
        float _scareRadius;

        Prism _grip;
        Vector3 _seatLocal;     // where on the grip the body holds (the grip's local frame)
        float _releaseAt;       // when the ride is over
        float _cooldownUntil;   // no latching before this

        /// <summary>The plate being ridden (null when free).</summary>
        public Prism Grip => _grip;
        public bool IsLatched => _grip;

        public void Configure(float rideSeconds, float flingSpeed, float scareRadius)
        {
            _rideSeconds = Mathf.Max(0f, rideSeconds);
            _flingSpeed = Mathf.Max(0f, flingSpeed);
            _scareRadius = Mathf.Max(0f, scareRadius);
        }

        /// <summary>
        /// True when <paramref name="plate"/> is something a rider may hold: live, and it MOVED since the
        /// tick that chose it (<paramref name="positionWhenChosen"/>), and the rider is not cooling down.
        /// </summary>
        public bool CanLatch(Prism plate, Vector3 positionWhenChosen)
        {
            if (_grip || _rideSeconds <= 0f || Time.time < _cooldownUntil) return false;
            if (!IsLiveGrip(plate)) return false;
            return (plate.transform.position - positionWhenChosen).sqrMagnitude >= MovingPlateMinDrift * MovingPlateMinDrift;
        }

        /// <summary>Take hold: remember the seat (where the body is now, in the grip's frame) and start the ride clock.</summary>
        public void Latch(Prism plate, Transform body)
        {
            _grip = plate;
            Vector3 seat = plate.transform.InverseTransformPoint(body.position);
            float reach = Mathf.Max(0.01f, SeatReach / Mathf.Max(1e-3f, plate.transform.lossyScale.magnitude / 1.7320508f));
            if (seat.sqrMagnitude > reach * reach) seat = seat.normalized * reach;
            _seatLocal = seat;
            _releaseAt = Time.time + _rideSeconds;
        }

        /// <summary>
        /// One frame of riding: ease <paramref name="body"/> onto its seat and face the grip. Returns false when
        /// the ride is over or the grip is gone (the caller then <see cref="Release"/>s and flings).
        /// </summary>
        public bool Ride(Transform body, float dt, out Quaternion facing)
        {
            facing = body.rotation;
            if (!IsLiveGrip(_grip) || Time.time >= _releaseAt) return false;

            Vector3 seat = _grip.transform.TransformPoint(_seatLocal);
            body.position = Vector3.Lerp(body.position, seat, 1f - Mathf.Exp(-SeatSharpness * Mathf.Max(0f, dt)));
            Vector3 toGrip = _grip.transform.position - body.position;
            if (toGrip.sqrMagnitude > 1e-6f) facing = Quaternion.LookRotation(toGrip, body.up);
            return true;
        }

        /// <summary>A vessel inside the scare radius (one physics overlap; call it on the behaviour tick, not per frame).</summary>
        public bool IsScared(Vector3 at)
        {
            if (_scareRadius <= 0f) return false;
            int hits = Physics.OverlapSphereNonAlloc(at, _scareRadius, ScareScratch, Fauna.VesselSenseMask);
            for (int i = 0; i < hits; i++)
            {
                var c = ScareScratch[i];
                if (c && c.TryGetComponent<IVesselStatus>(out _)) return true;
            }
            return false;
        }

        /// <summary>Let go. Returns the fling velocity (away from the grip; the body's up when the grip is gone).</summary>
        public Vector3 Release(Transform body)
        {
            Vector3 away = body.up;
            if (_grip)
            {
                Vector3 d = body.position - _grip.transform.position;
                if (d.sqrMagnitude > 1e-4f) away = d.normalized;
            }
            _grip = null;
            _cooldownUntil = Time.time + RelatchCooldownSeconds;
            return away * _flingSpeed;
        }

        /// <summary>Let go without a fling (the rider died on the plate).</summary>
        public void Drop()
        {
            _grip = null;
            _cooldownUntil = Time.time + RelatchCooldownSeconds;
        }

        static bool IsLiveGrip(Prism p) => p && !p.destroyed && p.gameObject.activeInHierarchy;
    }
}
