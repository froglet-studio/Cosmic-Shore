using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The camera feel's state and math (<c>Docs/CAMERA_FEEL.md</c>): G-force (the lag of acceleration and the slide
    /// of a drift, and the neck tilt they give), kicks, shake, rumble and breathing, folded into one camera-local
    /// offset (<see cref="LocalOffset"/>, a fraction of the follow distance) and one camera-local tilt
    /// (<see cref="LocalAngles"/>, degrees) that <see cref="CustomCameraController"/> adds on top of its settled pose.
    /// Plain C# with no scene dependency, so every behaviour is tested directly.
    ///
    /// <para><b>G lags ACCELERATION and SLIDE, never speed.</b> The framed point's velocity is estimated from its
    /// motion, and its change drives a damped spring as a pseudo-force (the camera is a mass on a spring riding the
    /// hull): a constant acceleration settles at a constant lag, a constant velocity at none. A drift's slide is a
    /// second rest point on the same spring. So a cruise frames exactly as before, and only boosts, stops, turns and
    /// drifts move the hull on screen, each with the same accelerating start and settle.</para>
    ///
    /// <para><b>Kick units are true peaks.</b> An author writes "this shoves the camera 3.5% of the follow distance";
    /// the impulse that produces that peak through the kick spring's damping is worked out here
    /// (<see cref="PeakFactor"/>), so retuning the spring does not silently retune every event.</para>
    /// </summary>
    public sealed class CameraFeel
    {
        // Longest frame the feel advances in one go: a hitch is not a reason to lurch.
        const float MaxStep = 0.05f;
        // How fast the velocity estimate follows the framed point, per second (~25 ms).
        const float VelocitySmoothing = 40f;
        // The furthest the inertia spring is driven, in "full lags" (a dash or a launch saturates here).
        const float MaxDrive = 3f;
        // How fast the drift rumble follows the drift, per second.
        const float DriftRumbleEase = 5f;

        Vector3 _lag, _lagVelocity;                       // G: world axes, fraction of the follow distance
        Vector3 _kick, _kickVelocity;                     // kicks: camera-local, fraction of the follow distance
        Vector3 _kickAngles, _kickAngularVelocity;        // kicks: camera-local Euler degrees
        float _trauma, _skimRumble, _drift, _slide;
        Vector3 _lastPoint, _velocity;
        int _primed;                                      // 0 = no point yet, 1 = a point, 2 = a velocity
        float _clock, _noiseTime;
        float _lastSkim = float.NegativeInfinity, _lastImpact = float.NegativeInfinity;
        float _lastStruck = float.NegativeInfinity, _lastShot = float.NegativeInfinity;
        readonly float _seed;

        /// <summary>The offset to add this frame, camera-local, as a fraction of the follow distance.</summary>
        public Vector3 LocalOffset { get; private set; }

        /// <summary>The tilt to add this frame, camera-local Euler degrees (pitch, yaw, roll).</summary>
        public Vector3 LocalAngles { get; private set; }

        /// <summary>Shake trauma, 0..1 (felt squared).</summary>
        public float Trauma => _trauma;

        /// <summary>The rumble level from skimming, 0..1.</summary>
        public float SkimRumble => _skimRumble;

        /// <param name="seed">Offsets the noise, so two cameras never shake in lockstep.</param>
        public CameraFeel(float seed = 0f) => _seed = seed;

        /// <summary>Forget everything: a cut, a teleport, a new vessel. The next frame starts from rest.</summary>
        public void Reset()
        {
            _lag = _lagVelocity = Vector3.zero;
            _kick = _kickVelocity = Vector3.zero;
            _kickAngles = _kickAngularVelocity = Vector3.zero;
            _trauma = _skimRumble = _drift = _slide = 0f;
            _velocity = Vector3.zero;
            _primed = 0;
            LocalOffset = Vector3.zero;
            LocalAngles = Vector3.zero;
        }

        /// <summary>The framed point was carried by <paramref name="shift"/> without moving (a portal), so the
        /// velocity estimate must not read the jump as motion.</summary>
        public void Shift(Vector3 shift) => _lastPoint += shift;

        // ---------------------------------------------------------------- events (camera-local directions)

        /// <summary>A prism entered the skimmer. <paramref name="awayLocal"/> points from the prism to the skimmer's
        /// centre; <paramref name="closeness01"/> is 1 at the centre, 0 at the surface.</summary>
        public void Skim(CameraFeelConfigSO c, Vector3 awayLocal, float closeness01, float weight)
        {
            float share = Mathf.Lerp(c.SkimEdgeFloor, 1f, Mathf.Clamp01(closeness01)) * weight;
            _skimRumble = Mathf.Min(1f, _skimRumble + c.SkimRumblePerSkim * share);
            if (_clock - _lastSkim < c.SkimMinInterval) return;
            _lastSkim = _clock;
            // Mostly in the screen plane: a skim brushes past, it does not push the camera at the hull.
            Vector3 dir = Flatten(awayLocal, 0.3f);
            Kick(c, dir, c.SkimNudge * share, TiltFrom(dir, c.SkimTilt * share));
        }

        /// <summary>The hull hit something; <paramref name="awayLocal"/> points from it to the hull.</summary>
        public void Impact(CameraFeelConfigSO c, Vector3 awayLocal, float weight)
        {
            if (_clock - _lastImpact < c.ImpactMinInterval) return;
            _lastImpact = _clock;
            // The hull stops; the camera's momentum carries it on toward the hull (local +z), and the rest of
            // the shove is away from what was hit.
            Vector3 away = Flatten(awayLocal, 0.5f);
            Vector3 dir = (away * (1f - c.ImpactLurch) + Vector3.forward * c.ImpactLurch).normalized;
            Kick(c, dir, c.ImpactNudge * weight, TiltFrom(dir, c.ImpactTilt * weight));
            AddTrauma(c.ImpactTrauma * weight);
        }

        /// <summary>A blast caught the vessel; <paramref name="awayLocal"/> points from its centre to the hull.</summary>
        public void Blast(CameraFeelConfigSO c, Vector3 awayLocal, float weight)
        {
            Vector3 dir = Flatten(awayLocal, 0.5f);
            Kick(c, dir, c.BlastNudge * weight, TiltFrom(dir, c.BlastTilt * weight));
            AddTrauma(c.BlastTrauma * weight);
        }

        /// <summary>A projectile struck the vessel; <paramref name="awayLocal"/> points from the round to the hull.</summary>
        public void Struck(CameraFeelConfigSO c, Vector3 awayLocal, float weight)
        {
            if (_clock - _lastStruck < c.StruckMinInterval) return;
            _lastStruck = _clock;
            Vector3 dir = Flatten(awayLocal, 0.5f);
            Kick(c, dir, c.StruckNudge * weight, TiltFrom(dir, c.StruckTilt * weight));
            AddTrauma(c.StruckTrauma * weight);
        }

        /// <summary>The vessel fired: the camera recoils back and noses up.</summary>
        public void Shot(CameraFeelConfigSO c, float weight)
        {
            if (_clock - _lastShot < c.ShotMinInterval) return;
            _lastShot = _clock;
            Kick(c, new Vector3(0f, 0.25f, -1f).normalized, c.ShotKick * weight, new Vector3(-c.ShotPitch * weight, 0f, 0f));
            AddTrauma(c.ShotTrauma * weight);
        }

        /// <summary>The vessel collected a crystal: a small lift.</summary>
        public void Pickup(CameraFeelConfigSO c, float weight)
        {
            Vector3 dir = new Vector3(0f, 1f, 0.4f).normalized;
            Kick(c, dir, c.PickupLift * weight, new Vector3(-c.PickupTilt * weight, 0f, 0f));
        }

        /// <summary>Add shake trauma (0..1), held at 1.</summary>
        public void AddTrauma(float amount) => _trauma = Mathf.Clamp01(_trauma + Mathf.Max(0f, amount));

        /// <summary>
        /// A kick of TRUE peak <paramref name="peakFraction"/> (of the follow distance) along
        /// <paramref name="localDirection"/>, and a tilt of true peak <paramref name="tiltDegrees"/>.
        /// </summary>
        public void Kick(CameraFeelConfigSO c, Vector3 localDirection, float peakFraction, Vector3 tiltDegrees)
        {
            float omega = 2f * Mathf.PI * c.KickFrequency;
            float gain = omega / PeakFactor(c.KickDamping);
            if (localDirection.sqrMagnitude > 1e-12f && peakFraction > 0f)
                _kickVelocity += localDirection.normalized * (peakFraction * gain);
            _kickAngularVelocity += tiltDegrees * gain;
        }

        // ---------------------------------------------------------------- the frame

        /// <summary>
        /// Advance by <paramref name="dt"/> and write <see cref="LocalOffset"/> / <see cref="LocalAngles"/>.
        /// </summary>
        /// <param name="point">The framed point this frame (world).</param>
        /// <param name="view">The camera's settled rotation this frame, before any feel.</param>
        /// <param name="distance">The follow distance, world units (offsets are fractions of it).</param>
        /// <param name="gForce">How much G this hull's pilot feels: 1 = the reference hull, below for a bigger hull,
        /// above for a smaller one (<see cref="CameraFeelConfigSO.SizeFeel"/> times the hull's table row); 0 = none.</param>
        /// <param name="response">How quickly the G builds and settles: 1 = the reference hull, below for a bigger,
        /// slower-responding hull.</param>
        /// <param name="drift">The signed drift, -1..1: how far, and to which side of the view, the hull's travel
        /// slips from where it points (0 when not drifting).</param>
        public void Step(CameraFeelConfigSO c, float dt, Vector3 point, Quaternion view, float distance,
            float gForce, float response, float drift)
        {
            if (!(dt > 0f)) return;   // paused: hold the pose as it is
            dt = Mathf.Min(dt, MaxStep);
            _clock += dt;
            _noiseTime += dt;
            distance = Mathf.Max(1f, distance);
            gForce = Mathf.Max(0f, gForce);
            response = Mathf.Max(0.05f, response);
            drift = Mathf.Clamp(drift, -1f, 1f);

            // --- G: the acceleration of the framed point, and the slide of a drift, drive one spring.
            Vector3 accel = Vector3.zero;
            if (_primed == 0) { _lastPoint = point; _primed = 1; }
            else
            {
                Vector3 v = (point - _lastPoint) / dt;
                _lastPoint = point;
                if (_primed == 1) { _velocity = v; _primed = 2; }
                else
                {
                    Vector3 previous = _velocity;
                    _velocity = Vector3.Lerp(_velocity, v, 1f - Mathf.Exp(-VelocitySmoothing * dt));
                    accel = (_velocity - previous) / dt;
                }
            }
            float lagMax = c.MaxLag * gForce;
            float gMax = c.MaxGOffset * gForce;
            float lagOmega = 2f * Mathf.PI * c.InertiaFrequency * response;
            // In "full lags": 1 settles the spring at lagMax behind the acceleration.
            Vector3 drive = Vector3.ClampMagnitude(accel / (distance * c.FullLagAcceleration), MaxDrive);

            // A drift locks the hull's course while its nose swings, so its real acceleration is near zero and the
            // lag alone never saw one. The slide is fed to the same spring as a rest point of its own: the camera
            // is left behind toward where the hull is sliding FROM, so the hull slides out across the frame. At
            // drift speed only (a slow slide is a gentle one), and through the spring, so it builds with an
            // accelerating start, swings a little past and settles, instead of easing in along a straight line.
            float speedShare = Mathf.SmoothStep(0f, 1f, _velocity.magnitude / (distance * c.DriftFullSpeed));
            // The slide's rest point is eased before it reaches the spring: a drift ending (or the nose coming
            // back) would otherwise snap the rest to zero and the spring would whip the hull back with an
            // overshoot - the jerk that broke the Squirrel's flow. Out is slower than in, so the hull glides home.
            float slideTarget = drift * speedShare;
            bool growing = Mathf.Abs(slideTarget) > Mathf.Abs(_slide) && slideTarget * _slide >= 0f;
            float slideRate = growing ? c.DriftSlideIn : c.DriftSlideOut;
            _slide = Mathf.Lerp(_slide, slideTarget, 1f - Mathf.Exp(-slideRate * dt));
            Vector3 slideRest = view * new Vector3(-_slide * c.DriftSlide * gForce, 0f, 0f);

            // e'' = -w^2 (e - rest) - 2 z w e': settles at rest = behind the push, plus the slide. The rest is
            // held for the frame, so the exact step is the spring's step around that rest point.
            if (gMax > 0f)
            {
                Vector3 rest = -drive * lagMax + slideRest;
                Vector3 offset = _lag - rest;
                Spring.Step(lagOmega, c.InertiaDamping, dt).Apply(ref offset, ref _lagVelocity);
                _lag = rest + offset;
            }
            else
            {
                _lag = _lagVelocity = Vector3.zero;
            }

            var kickStep = Spring.Step(2f * Mathf.PI * c.KickFrequency, c.KickDamping, dt);
            kickStep.Apply(ref _kick, ref _kickVelocity);
            kickStep.Apply(ref _kickAngles, ref _kickAngularVelocity);
            ClampState(ref _kick, ref _kickVelocity, c.MaxKick);
            ClampState(ref _kickAngles, ref _kickAngularVelocity, c.MaxKickAngle);

            Vector3 gLocal = Quaternion.Inverse(view) * _lag;
            gLocal.z *= c.ForwardLagShare;
            gLocal = SoftClamp(gLocal, gMax);

            // The pilot's neck: the same G that moves the camera tilts it. Thrown back under a boost (nose up),
            // forward under a stop (nose down), and leaned outward through a turn or a slide. Read against the
            // fleet's full G, so a big hull that is pushed less also leans less.
            float gFull = Mathf.Max(1e-4f, c.MaxGOffset);
            Vector3 neck = new(c.GPitch * gLocal.z / gFull, 0f, -c.GRoll * gLocal.x / gFull);

            // --- Shake and rumble.
            _trauma = Mathf.Max(0f, _trauma - c.TraumaDecay * dt);
            float shake = _trauma * _trauma;
            _skimRumble *= Mathf.Exp(-c.SkimRumbleDecay * dt);
            _drift = Mathf.Lerp(_drift, Mathf.Abs(drift) * speedShare, 1f - Mathf.Exp(-DriftRumbleEase * dt));
            float rumble = Mathf.Clamp01(Mathf.Max(_skimRumble, _drift * c.DriftRumble));

            Vector3 shakeNoise = shake > 0f ? Noise(c.TraumaFrequency, 0f) : Vector3.zero;
            Vector3 rumbleNoise = rumble > 0f ? Noise(c.RumbleFrequency, 17f) : Vector3.zero;
            Vector3 breathNoise = c.BreathAngle > 0f ? Noise(c.BreathFrequency, 41f) : Vector3.zero;

            LocalAngles = _kickAngles
                          + Vector3.Scale(shakeNoise, new Vector3(c.TraumaAngle, c.TraumaAngle, c.TraumaRoll)) * shake
                          + Vector3.Scale(rumbleNoise, new Vector3(1f, 1f, 0.6f)) * (c.RumbleAngle * rumble)
                          + Vector3.Scale(breathNoise, new Vector3(1f, 1f, 0.5f)) * c.BreathAngle
                          + neck;

            LocalOffset = gLocal
                          + _kick
                          + new Vector3(shakeNoise.y, shakeNoise.z, 0f) * (c.TraumaOffset * shake);
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>
        /// One EXACT step of a damped spring, x'' = -w^2 x - 2 z w x', over <c>t</c> seconds: the closed-form
        /// solution written as the linear map (x, v) -> (a11 x + a12 v, a21 x + a22 v). Exact at any frame rate,
        /// so a kick peaks at the same height at 30 fps and 144 fps (a substepped Euler step fell ~8% short at
        /// 4.5 Hz), and it costs four multiplies per axis instead of a substep loop.
        /// </summary>
        public readonly struct Spring
        {
            readonly float _a11, _a12, _a21, _a22;

            Spring(float a11, float a12, float a21, float a22)
            {
                _a11 = a11; _a12 = a12; _a21 = a21; _a22 = a22;
            }

            public static Spring Step(float omega, float zeta, float t)
            {
                if (!(omega > 0f) || !(t > 0f)) return new Spring(1f, 0f, 0f, 1f);
                zeta = Mathf.Max(0f, zeta);
                if (zeta < 0.999f)
                {
                    float wd = omega * Mathf.Sqrt(1f - zeta * zeta);
                    float e = Mathf.Exp(-zeta * omega * t);
                    float cos = Mathf.Cos(wd * t), sin = Mathf.Sin(wd * t);
                    return new Spring(
                        e * (cos + zeta * omega * sin / wd), e * sin / wd,
                        -e * omega * omega * sin / wd, e * (cos - zeta * omega * sin / wd));
                }
                if (zeta <= 1.001f)
                {
                    float e = Mathf.Exp(-omega * t);
                    return new Spring(e * (1f + omega * t), e * t, -e * omega * omega * t, e * (1f - omega * t));
                }
                float root = Mathf.Sqrt(zeta * zeta - 1f);
                float r1 = -omega * (zeta - root), r2 = -omega * (zeta + root);
                float e1 = Mathf.Exp(r1 * t), e2 = Mathf.Exp(r2 * t), d = r1 - r2;
                return new Spring(
                    (r1 * e2 - r2 * e1) / d, (e1 - e2) / d,
                    r1 * r2 * (e2 - e1) / d, (r1 * e1 - r2 * e2) / d);
            }

            public void Apply(ref Vector3 x, ref Vector3 v)
            {
                Vector3 x0 = x;
                x = _a11 * x0 + _a12 * v;
                v = _a21 * x0 + _a22 * v;
            }
        }

        /// <summary>
        /// The share of its initial-velocity ratio v0/w a damped spring actually reaches at its first peak:
        /// exp(-z/sqrt(1-z^2) * atan(sqrt(1-z^2)/z)). Dividing an impulse by it makes the authored number the peak.
        /// At and above critical damping the first-peak formula has no oscillation; it is held just below 1.
        /// </summary>
        public static float PeakFactor(float zeta)
        {
            zeta = Mathf.Clamp(zeta, 0.01f, 0.999f);
            float root = Mathf.Sqrt(1f - zeta * zeta);
            return Mathf.Exp(-zeta / root * Mathf.Atan(root / zeta));
        }

        /// <summary><paramref name="v"/> eased toward a magnitude of <paramref name="max"/> (tanh), so a saturated
        /// lag rounds off instead of hitting a wall.</summary>
        public static Vector3 SoftClamp(Vector3 v, float max)
        {
            if (!(max > 0f)) return Vector3.zero;
            float m = v.magnitude;
            if (m < 1e-9f) return v;
            return v * (max * Tanh(m / max) / m);
        }

        /// <summary>A unit direction with its along-view (z) part scaled by <paramref name="zShare"/>.</summary>
        static Vector3 Flatten(Vector3 v, float zShare)
        {
            v.z *= zShare;
            return v.sqrMagnitude > 1e-12f ? v.normalized : Vector3.zero;
        }

        /// <summary>The tilt a shove along <paramref name="dir"/> gives: up noses down, sideways rolls and turns
        /// a little, toward the hull noses down.</summary>
        static Vector3 TiltFrom(Vector3 dir, float degrees) =>
            new Vector3(dir.y * 0.6f + dir.z * 0.4f, dir.x * 0.5f, -dir.x) * degrees;

        static void ClampState(ref Vector3 x, ref Vector3 v, float max)
        {
            float m = x.magnitude;
            if (m <= max) return;
            x = max > 0f ? x * (max / m) : Vector3.zero;
            // Shed the velocity carrying it further out, so a clamped kick settles instead of grinding the wall.
            if (max > 0f && Vector3.Dot(v, x) > 0f) v -= Vector3.Project(v, x);
            else if (!(max > 0f)) v = Vector3.zero;
        }

        Vector3 Noise(float frequency, float row)
        {
            float t = _noiseTime * frequency;
            return new Vector3(
                (Mathf.PerlinNoise(_seed + row + 0.37f, t) - 0.5f) * 2f,
                (Mathf.PerlinNoise(_seed + row + 5.71f, t) - 0.5f) * 2f,
                (Mathf.PerlinNoise(_seed + row + 11.13f, t) - 0.5f) * 2f);
        }

        static float Tanh(float x)
        {
            float e = Mathf.Exp(-2f * Mathf.Abs(x));
            return Mathf.Sign(x) * (1f - e) / (1f + e);
        }
    }
}
