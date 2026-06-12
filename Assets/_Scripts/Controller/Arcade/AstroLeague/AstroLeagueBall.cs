using System;
using System.Threading;
using CosmicShore.Data;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Billiard-physics match ball for Astro League.
    /// Vessels move via transform (not rigidbody), so momentum transfer reads
    /// VesselStatus.Speed/Course from the striking vessel instead of physics velocity.
    /// Wall bounces are handled by Unity physics with a high-restitution material.
    /// Impact juice (hitstop, camera shake, emission flash, burst particles, haptics)
    /// scales with hit intensity. Emission animates via MaterialPropertyBlock.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(SphereCollider))]
    public class AstroLeagueBall : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] AstroLeagueSettingsSO settings;

        [Header("Payload Colors")]
        [SerializeField] Color primaryColor = new(1f, 0.6f, 0.1f, 1f);
        [SerializeField] Color secondaryColor = new(0.2f, 0.5f, 1f, 1f);
        [SerializeField] Color tertiaryColor = new(1f, 0.15f, 0.6f, 1f);
        [SerializeField] float colorCycleSpeed = 1.2f;
        [SerializeField] float baseLightIntensity = 3f;

        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        Rigidbody rb;
        Vector3 spawnPosition;
        bool isFrozen;
        bool isHidden;
        bool hitstopActive;
        CancellationToken destroyToken;

        // Visuals
        Light ballLight;
        TrailRenderer trail;
        Renderer ballRenderer;
        MaterialPropertyBlock mpb;
        ParticleSystem auraParticles;
        ParticleSystem impactParticles;
        float flashTimer;
        float currentEmissionBoost = 1f;

        CustomCameraController cameraController;

        /// <summary>Raised when the ball crosses a goal line. Payload: the scoring domain.</summary>
        public event Action<Domains> OnGoalScored;

        /// <summary>Raised when a vessel strikes the ball. Payload: resulting speed 0..1 of max.</summary>
        public event Action<float> OnBallStruck;

        public Vector3 Velocity => rb.linearVelocity;
        public Vector3 SpawnPosition => spawnPosition;
        public bool IsFrozen => isFrozen;

        void Awake()
        {
            destroyToken = this.GetCancellationTokenOnDestroy();

            rb = GetComponent<Rigidbody>();
            rb.useGravity = false;
            rb.mass = settings != null ? settings.ballMass : 3f;
            rb.linearDamping = 0f; // Speed-dependent drag applied in FixedUpdate
            rb.angularDamping = 0.05f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var sphereCol = GetComponent<SphereCollider>();
            sphereCol.material = new PhysicsMaterial("AstroLeagueBall")
            {
                bounciness = settings != null ? settings.ballBounciness : 0.98f,
                bounceCombine = PhysicsMaterialCombine.Maximum,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                dynamicFriction = 0f,
                staticFriction = 0f
            };

            spawnPosition = transform.position;
            SetupVisuals();
        }

        void Start()
        {
            if (CameraManager.Instance != null)
                cameraController = CameraManager.Instance.GetActiveController() as CustomCameraController;
        }

        #region Visual construction

        void SetupVisuals()
        {
            mpb = new MaterialPropertyBlock();

            ballRenderer = GetComponent<Renderer>();
            if (ballRenderer != null)
            {
                // One material instance created for the ball at startup; per-frame
                // animation goes through the MaterialPropertyBlock, never .material.
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.SetFloat("_Metallic", 0.9f);
                mat.SetFloat("_Smoothness", 0.95f);
                mat.SetColor(BaseColorId, primaryColor);
                mat.EnableKeyword("_EMISSION");
                ballRenderer.sharedMaterial = mat;
            }

            ballLight = gameObject.AddComponent<Light>();
            ballLight.type = LightType.Point;
            ballLight.color = primaryColor;
            ballLight.range = settings != null ? settings.minLightRange : 25f;
            ballLight.intensity = baseLightIntensity;
            ballLight.shadows = LightShadows.None;

            trail = gameObject.AddComponent<TrailRenderer>();
            trail.time = 0.3f;
            trail.startWidth = settings != null ? settings.minTrailWidth : 0.6f;
            trail.endWidth = 0.1f;
            trail.numCapVertices = 4;
            var trailMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            trailMat.color = primaryColor;
            MakeTransparent(trailMat);
            trail.sharedMaterial = trailMat;
            trail.startColor = primaryColor;
            trail.endColor = new Color(secondaryColor.r, secondaryColor.g, secondaryColor.b, 0f);
            trail.minVertexDistance = 0.5f;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.generateLightingData = false;

            auraParticles = CreateParticles("PayloadAura", burstOnly: false);
            impactParticles = CreateParticles("ImpactBurst", burstOnly: true);
        }

        static void MakeTransparent(Material mat)
        {
            mat.SetFloat("_Surface", 1);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.renderQueue = 3000;
        }

        ParticleSystem CreateParticles(string childName, bool burstOnly)
        {
            var go = new GameObject(childName);
            go.transform.SetParent(transform, false);

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = !burstOnly;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0f;
            main.startColor = new ParticleSystem.MinMaxGradient(primaryColor, secondaryColor);

            var emission = ps.emission;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;

            if (burstOnly)
            {
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(15f, 45f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.9f);
                main.maxParticles = 200;
                main.startColor = new ParticleSystem.MinMaxGradient(Color.white, primaryColor);
                emission.rateOverTime = 0f;
                shape.radius = 0.5f;
            }
            else
            {
                main.startLifetime = 0.8f;
                main.startSpeed = 2f;
                main.startSize = 0.6f;
                main.maxParticles = 60;
                emission.rateOverTime = 20f;
                shape.radius = 1.5f;

                var vel = ps.velocityOverLifetime;
                vel.enabled = true;
                vel.orbitalX = 3f;
                vel.orbitalY = 2f;
                vel.orbitalZ = 1.5f;
                vel.radial = -1f;
            }

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(secondaryColor, 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = gradient;

            var psRenderer = go.GetComponent<ParticleSystemRenderer>();
            var psMat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            psMat.SetFloat("_Surface", 1);
            psMat.SetInt("_Blend", 1); // Additive
            psMat.SetColor(BaseColorId, Color.white);
            psMat.renderQueue = 3100;
            psRenderer.sharedMaterial = psMat;
            psRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            return ps;
        }

        #endregion

        #region Physics

        void FixedUpdate()
        {
            if (settings == null || isFrozen) return;

            float speed = rb.linearVelocity.magnitude;

            // Speed-dependent drag: coast at speed, settle near rest.
            float speedRatio = Mathf.Clamp01(speed / settings.maxSpeed);
            float drag = Mathf.Lerp(settings.lowSpeedDrag, settings.highSpeedDrag, speedRatio);
            rb.linearVelocity *= Mathf.Max(0f, 1f - drag * Time.fixedDeltaTime);

            if (rb.linearVelocity.sqrMagnitude < settings.stopThreshold * settings.stopThreshold)
                rb.linearVelocity = Vector3.zero;

            if (rb.linearVelocity.sqrMagnitude > settings.maxSpeed * settings.maxSpeed)
                rb.linearVelocity = rb.linearVelocity.normalized * settings.maxSpeed;
        }

        void OnCollisionEnter(Collision collision)
        {
            if (settings == null || isFrozen || isHidden) return;
            if (collision.contactCount == 0) return;

            Vector3 contactPoint = collision.contacts[0].point;
            Vector3 contactNormal = collision.contacts[0].normal;

            // Static geometry (arena walls): Unity resolved the bounce, we add feedback.
            if (collision.rigidbody == null && collision.gameObject.GetComponentInParent<IVesselStatus>() == null)
            {
                WallBounceJuice(contactPoint, contactNormal);
                return;
            }

            VesselStrike(collision, contactPoint);
        }

        void WallBounceJuice(Vector3 contactPoint, Vector3 contactNormal)
        {
            float bounceSpeed = rb.linearVelocity.magnitude;
            float intensity = Mathf.Clamp01(bounceSpeed / settings.maxSpeed);

            if (bounceSpeed > settings.hitstopSpeedThreshold * 0.3f)
            {
                EmitBurst(contactPoint, contactNormal, (int)(settings.impactParticleBurst * intensity * 0.4f));
                TriggerFlash(intensity * 0.3f);
            }

            if (bounceSpeed > settings.hitstopSpeedThreshold * 0.7f)
                ShakeCamera(settings.strikeShakeIntensity * intensity * 0.35f, settings.strikeShakeDuration);
        }

        void VesselStrike(Collision collision, Vector3 contactPoint)
        {
            Vector3 strikerVelocity = ResolveStrikerVelocity(collision);
            float strikerSpeed = strikerVelocity.magnitude;
            if (strikerSpeed < settings.minimumHitSpeed) return;

            // Billiard deflection blended toward the striker's heading.
            Vector3 deflectionDir = (transform.position - contactPoint).normalized;
            Vector3 pushDir = strikerVelocity.normalized;
            Vector3 resultDir = Vector3.Slerp(deflectionDir, pushDir, settings.directionalBias).normalized;

            Vector3 retained = rb.linearVelocity * settings.velocityRetention;
            rb.linearVelocity = retained + resultDir * (strikerSpeed * settings.hitBoostMultiplier);

            float finalSpeed = rb.linearVelocity.magnitude;
            if (finalSpeed > settings.maxSpeed)
            {
                rb.linearVelocity = rb.linearVelocity.normalized * settings.maxSpeed;
                finalSpeed = settings.maxSpeed;
            }

            float intensity = Mathf.Clamp01(finalSpeed / settings.maxSpeed);
            EmitBurst(contactPoint, deflectionDir, (int)(settings.impactParticleBurst * Mathf.Max(0.3f, intensity)));
            TriggerFlash(intensity);
            ShakeCamera(settings.strikeShakeIntensity * intensity, settings.strikeShakeDuration);
            HapticController.PlayHaptic(HapticType.ShipCollision);

            if (finalSpeed > settings.hitstopSpeedThreshold)
                RunHitstopAsync().Forget();

            OnBallStruck?.Invoke(intensity);
        }

        /// <summary>
        /// Vessels move via transform.position, so rigidbody velocity is ~0 on them.
        /// Read VesselStatus.Speed * Course for the true strike velocity.
        /// </summary>
        Vector3 ResolveStrikerVelocity(Collision collision)
        {
            var vesselStatus = collision.gameObject.GetComponentInParent<IVesselStatus>();
            if (vesselStatus != null && vesselStatus.Speed > 0.1f)
                return vesselStatus.Course * vesselStatus.Speed;

            if (collision.rigidbody != null && collision.rigidbody.linearVelocity.sqrMagnitude > 1f)
                return collision.rigidbody.linearVelocity;

            return collision.relativeVelocity;
        }

        #endregion

        #region Per-frame visuals

        void Update()
        {
            if (settings == null || ballRenderer == null) return;

            float speedRatio = Mathf.Clamp01(rb.linearVelocity.magnitude / settings.speedForMaxVisuals);

            // Impact flash decay
            if (flashTimer > 0f)
            {
                flashTimer -= Time.deltaTime;
                float flashRatio = Mathf.Clamp01(flashTimer / settings.impactFlashDuration);
                currentEmissionBoost = Mathf.Lerp(1f, settings.impactFlashIntensity, flashRatio);
            }
            else
            {
                currentEmissionBoost = 1f;
            }

            // Three-way color cycle with breathing pulse
            float phase = (Time.time * colorCycleSpeed) % 3f;
            Color emissionColor = phase < 1f
                ? Color.Lerp(primaryColor, secondaryColor, phase)
                : phase < 2f
                    ? Color.Lerp(secondaryColor, tertiaryColor, phase - 1f)
                    : Color.Lerp(tertiaryColor, primaryColor, phase - 2f);

            float breath = 0.8f + 0.2f * Mathf.Sin(Time.time * 4f);
            float emissionIntensity = Mathf.Lerp(settings.minEmissionIntensity, settings.maxEmissionIntensity, speedRatio);

            mpb.SetColor(EmissionColorId, emissionColor * (emissionIntensity * breath * currentEmissionBoost));
            ballRenderer.SetPropertyBlock(mpb);

            if (ballLight != null)
            {
                ballLight.color = emissionColor;
                float boost = currentEmissionBoost > 1f ? Mathf.Sqrt(currentEmissionBoost) : 1f;
                ballLight.intensity = baseLightIntensity * (1f + speedRatio * 2f) * breath * boost;
                ballLight.range = Mathf.Lerp(settings.minLightRange, settings.maxLightRange, speedRatio);
            }

            if (trail != null)
            {
                trail.startWidth = Mathf.Lerp(settings.minTrailWidth, settings.maxTrailWidth, speedRatio);
                trail.time = Mathf.Lerp(0.15f, 0.8f, speedRatio);
            }

            if (auraParticles != null)
            {
                var emission = auraParticles.emission;
                emission.rateOverTime = isHidden ? 0f : Mathf.Lerp(8f, 45f, speedRatio);
            }
        }

        #endregion

        #region Juice helpers

        void TriggerFlash(float intensity) =>
            flashTimer = settings.impactFlashDuration * Mathf.Max(0.3f, intensity);

        void EmitBurst(Vector3 position, Vector3 normal, int count)
        {
            if (impactParticles == null || count <= 0) return;
            impactParticles.transform.position = position;
            impactParticles.transform.forward = normal;
            impactParticles.Emit(count);
        }

        void ShakeCamera(float intensity, float duration)
        {
            if (cameraController == null && CameraManager.Instance != null)
                cameraController = CameraManager.Instance.GetActiveController() as CustomCameraController;
            cameraController?.Shake(intensity, duration);
        }

        async UniTaskVoid RunHitstopAsync()
        {
            if (hitstopActive) return;
            hitstopActive = true;

            float originalTimeScale = Time.timeScale;
            float originalFixedDelta = Time.fixedDeltaTime;
            Time.timeScale = settings.hitstopTimeScale;
            Time.fixedDeltaTime = originalFixedDelta * settings.hitstopTimeScale;

            try
            {
                await UniTask.Delay(
                    TimeSpan.FromSeconds(settings.hitstopDuration),
                    ignoreTimeScale: true,
                    cancellationToken: destroyToken);
            }
            finally
            {
                Time.timeScale = originalTimeScale;
                Time.fixedDeltaTime = originalFixedDelta;
                hitstopActive = false;
            }
        }

        #endregion

        #region Match control API (driven by AstroLeagueMatchController)

        /// <summary>Called by the goal trigger when the ball crosses a goal line.</summary>
        public void NotifyGoalScored(Domains scoringDomain)
        {
            if (isHidden) return;

            // Detonate: big burst + hide until kickoff respawns the ball.
            EmitBurst(transform.position, Vector3.up, settings != null ? settings.goalParticleBurst : 120);
            ShakeCamera(settings != null ? settings.goalShakeIntensity : 2.5f,
                        settings != null ? settings.goalShakeDuration : 0.5f);
            HapticController.PlayHaptic(HapticType.MineCollision);
            SetHidden(true);

            OnGoalScored?.Invoke(scoringDomain);
        }

        /// <summary>Freeze the ball in place (kickoff count-in). Velocity is cleared.</summary>
        public void SetFrozen(bool frozen)
        {
            isFrozen = frozen;
            if (frozen)
            {
                // Kinematic bodies only support speculative continuous detection.
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                rb.isKinematic = true;
                transform.position = spawnPosition;
            }
            else
            {
                rb.isKinematic = false;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }
        }

        /// <summary>Hide/show the ball (between goal detonation and kickoff respawn).</summary>
        public void SetHidden(bool hidden)
        {
            isHidden = hidden;
            if (ballRenderer != null) ballRenderer.enabled = !hidden;
            if (ballLight != null) ballLight.enabled = !hidden;
            if (trail != null)
            {
                trail.emitting = !hidden;
                if (hidden) trail.Clear();
            }
            if (hidden)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        /// <summary>Respawn at center: visible, frozen, zero velocity.</summary>
        public void ResetToCenter()
        {
            SetFrozen(true);
            SetHidden(false);
            transform.position = spawnPosition;
            if (trail != null) trail.Clear();
        }

        public void SetSpawnPosition(Vector3 position) => spawnPosition = position;

        /// <summary>Full reset: visible, unfrozen, parked at center.</summary>
        public void FullReset()
        {
            SetHidden(false);
            SetFrozen(false);
            transform.position = spawnPosition;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            if (trail != null) trail.Clear();
        }

        #endregion
    }
}
