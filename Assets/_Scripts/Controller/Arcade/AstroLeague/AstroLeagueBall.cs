using System;
using Cysharp.Threading.Tasks;
using CosmicShore.Game.CameraSystem;
using CosmicShore.Game.IO;
using UnityEngine;

namespace CosmicShore.Game.Arcade.AstroLeague
{
    /// <summary>
    /// Billiard-physics ball for Astro League.
    /// Ships transfer momentum on contact using VesselStatus velocity (not rigidbody velocity,
    /// because ships move via transform.position). Wall bounces are handled by Unity physics
    /// with a high-restitution material. Impact juice (hitstop, camera shake, emission flash,
    /// burst particles) scales with hit intensity for Rocket-League-grade game feel.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(SphereCollider))]
    public class AstroLeagueBall : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] AstroLeagueBallSettingsSO settings;

        [Header("Visuals")]
        [SerializeField] Color primaryColor = new(1f, 0.6f, 0.1f, 1f);
        [SerializeField] Color secondaryColor = new(0.2f, 0.5f, 1f, 1f);
        [SerializeField] Color tertiaryColor = new(1f, 0.15f, 0.6f, 1f);
        [SerializeField] float pulseSpeed = 1.2f;
        [SerializeField] float baseLightIntensity = 3f;

        [Header("Reset")]
        [SerializeField] float resetDelay = 1.5f;

        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        Rigidbody rb;
        Vector3 spawnPosition;
        bool isResetting;
        bool hitstopActive;

        // Visuals
        Light ballLight;
        TrailRenderer trail;
        Material ballMat;
        Renderer ballRenderer;
        ParticleSystem auraParticles;
        ParticleSystem impactParticles;

        // Impact flash
        float flashTimer;
        float currentEmissionBoost = 1f;

        // Camera
        CustomCameraController cameraController;

        public event Action<Domains> OnGoalScored;
        public Vector3 Velocity => rb.linearVelocity;

        #region Setup

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.useGravity = false;
            rb.mass = settings != null ? settings.Mass : 3f;
            rb.linearDamping = 0f; // Custom drag in FixedUpdate
            rb.angularDamping = 0.05f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var sphereCol = GetComponent<SphereCollider>();
            float bounciness = settings != null ? settings.BallBounciness : 0.98f;
            sphereCol.material = new PhysicsMaterial("BilliardBall")
            {
                bounciness = bounciness,
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
            var mainCam = Camera.main;
            if (mainCam != null)
                mainCam.TryGetComponent(out cameraController);
        }

        void SetupVisuals()
        {
            ballRenderer = GetComponent<Renderer>();
            if (ballRenderer != null)
            {
                ballMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                ballMat.SetFloat("_Metallic", 0.9f);
                ballMat.SetFloat("_Smoothness", 0.95f);
                ballMat.SetColor(BaseColorId, primaryColor);
                ballMat.EnableKeyword("_EMISSION");
                ballMat.SetColor(EmissionColorId, primaryColor * (settings != null ? settings.MinEmissionIntensity : 4f));
                ballRenderer.material = ballMat;
            }

            ballLight = gameObject.AddComponent<Light>();
            ballLight.type = LightType.Point;
            ballLight.color = primaryColor;
            ballLight.range = settings != null ? settings.MinLightRange : 50f;
            ballLight.intensity = baseLightIntensity;
            ballLight.shadows = LightShadows.None;

            trail = gameObject.AddComponent<TrailRenderer>();
            trail.time = 0.3f;
            trail.startWidth = settings != null ? settings.MinTrailWidth : 0.5f;
            trail.endWidth = 0.1f;
            trail.numCapVertices = 4;
            var trailMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            trailMat.color = primaryColor;
            SetMaterialTransparent(trailMat);
            trail.material = trailMat;
            trail.startColor = primaryColor;
            trail.endColor = new Color(secondaryColor.r, secondaryColor.g, secondaryColor.b, 0f);
            trail.minVertexDistance = 0.5f;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.generateLightingData = false;

            SetupAuraParticles();
            SetupImpactParticles();
        }

        static void SetMaterialTransparent(Material mat)
        {
            mat.SetFloat("_Surface", 1);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.renderQueue = 3000;
        }

        void SetupAuraParticles()
        {
            var auraGO = new GameObject("PayloadAura");
            auraGO.transform.SetParent(transform, false);

            auraParticles = auraGO.AddComponent<ParticleSystem>();
            var main = auraParticles.main;
            main.startLifetime = 0.8f;
            main.startSpeed = 2f;
            main.startSize = 0.6f;
            main.maxParticles = 30;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new ParticleSystem.MinMaxGradient(primaryColor, secondaryColor);
            main.gravityModifier = 0f;

            var emission = auraParticles.emission;
            emission.rateOverTime = 20f;

            var shape = auraParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 1.5f;

            var vel = auraParticles.velocityOverLifetime;
            vel.enabled = true;
            vel.orbitalX = 3f;
            vel.orbitalY = 2f;
            vel.orbitalZ = 1.5f;
            vel.radial = -1f;

            var size = auraParticles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

            var col = auraParticles.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(primaryColor, 0f), new GradientColorKey(secondaryColor, 1f) },
                new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            col.color = gradient;

            var particleRenderer = auraGO.GetComponent<ParticleSystemRenderer>();
            var particleMat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            particleMat.SetFloat("_Surface", 1);
            particleMat.SetInt("_Blend", 1); // Additive
            particleMat.SetColor(BaseColorId, Color.white);
            particleMat.renderQueue = 3100;
            particleRenderer.material = particleMat;
            particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void SetupImpactParticles()
        {
            var impactGO = new GameObject("ImpactBurst");
            impactGO.transform.SetParent(transform, false);

            impactParticles = impactGO.AddComponent<ParticleSystem>();

            // Disable auto-play — we burst on demand
            var main = impactParticles.main;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(15f, 40f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
            main.maxParticles = 60;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new ParticleSystem.MinMaxGradient(Color.white, primaryColor);
            main.gravityModifier = 0f;

            var emission = impactParticles.emission;
            emission.rateOverTime = 0f; // Only burst

            var shape = impactParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.5f;

            var size = impactParticles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            var col = impactParticles.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(primaryColor, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            col.color = gradient;

            var particleRenderer = impactGO.GetComponent<ParticleSystemRenderer>();
            var particleMat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            particleMat.SetFloat("_Surface", 1);
            particleMat.SetInt("_Blend", 1); // Additive
            particleMat.SetColor(BaseColorId, Color.white);
            particleMat.renderQueue = 3100;
            particleRenderer.material = particleMat;
            particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        #endregion

        #region Physics

        void FixedUpdate()
        {
            if (settings == null) return;

            float speed = rb.linearVelocity.magnitude;

            // Speed-dependent drag: low drag at high speed (coasting), high drag at low speed (settle)
            float speedRatio = Mathf.Clamp01(speed / settings.MaxSpeed);
            float drag = Mathf.Lerp(settings.LowSpeedDrag, settings.HighSpeedDrag, speedRatio);
            rb.linearVelocity *= Mathf.Max(0f, 1f - drag * Time.fixedDeltaTime);

            // Snap to zero below threshold — no creeping ball
            if (rb.linearVelocity.sqrMagnitude < settings.StopThreshold * settings.StopThreshold)
                rb.linearVelocity = Vector3.zero;

            // Hard cap
            if (rb.linearVelocity.sqrMagnitude > settings.MaxSpeed * settings.MaxSpeed)
                rb.linearVelocity = rb.linearVelocity.normalized * settings.MaxSpeed;
        }

        void OnCollisionEnter(Collision collision)
        {
            if (isResetting || settings == null) return;
            if (collision.contactCount == 0) return;

            Vector3 contactPoint = collision.contacts[0].point;
            Vector3 contactNormal = collision.contacts[0].normal;

            // --- Wall bounce: Unity physics handles the bounce, we add juice ---
            if (collision.rigidbody == null)
            {
                HandleWallBounceJuice(contactPoint, contactNormal);
                return;
            }

            // --- Ship / rigidbody hit: custom billiard momentum transfer ---
            HandleShipHit(collision, contactPoint);
        }

        void HandleWallBounceJuice(Vector3 contactPoint, Vector3 contactNormal)
        {
            float bounceSpeed = rb.linearVelocity.magnitude;
            float bounceIntensity = Mathf.Clamp01(bounceSpeed / settings.MaxSpeed);

            if (bounceSpeed > settings.HitstopSpeedThreshold * 0.3f)
            {
                int burstCount = (int)(settings.ImpactParticleBurstCount * bounceIntensity * 0.4f);
                SpawnImpactParticles(contactPoint, contactNormal, burstCount);
                TriggerFlash(bounceIntensity * 0.3f);
            }

            if (bounceSpeed > settings.HitstopSpeedThreshold * 0.7f)
                ShakeCamera(bounceIntensity * 0.3f);
        }

        void HandleShipHit(Collision collision, Vector3 contactPoint)
        {
            Vector3 impactorVelocity = ResolveImpactorVelocity(collision);
            float impactorSpeed = impactorVelocity.magnitude;

            if (impactorSpeed < settings.MinimumHitSpeed) return;

            // --- Billiard deflection ---
            // Direction from contact point toward ball center = natural billiard deflection
            Vector3 deflectionDir = (transform.position - contactPoint).normalized;
            // Ship's forward push direction
            Vector3 pushDir = impactorVelocity.normalized;

            // Blend: 0 = pure billiard, 1 = pure push
            Vector3 resultDir = Vector3.Slerp(deflectionDir, pushDir, settings.DirectionalBias).normalized;
            float resultSpeed = impactorSpeed * settings.HitBoostMultiplier;

            // New velocity with partial retention of existing momentum
            Vector3 newVelocity = resultDir * resultSpeed;
            Vector3 retainedVelocity = rb.linearVelocity * settings.VelocityRetention;
            rb.linearVelocity = retainedVelocity + newVelocity;

            // Clamp
            float finalSpeed = rb.linearVelocity.magnitude;
            if (finalSpeed > settings.MaxSpeed)
            {
                rb.linearVelocity = rb.linearVelocity.normalized * settings.MaxSpeed;
                finalSpeed = settings.MaxSpeed;
            }

            // --- Impact juice scaled to hit intensity ---
            float impactIntensity = Mathf.Clamp01(finalSpeed / settings.MaxSpeed);

            SpawnImpactParticles(contactPoint, deflectionDir,
                (int)(settings.ImpactParticleBurstCount * Mathf.Max(0.3f, impactIntensity)));
            TriggerFlash(impactIntensity);
            ShakeCamera(impactIntensity);

            if (finalSpeed > settings.HitstopSpeedThreshold)
                ApplyHitstopAsync().Forget();

            HapticController.PlayHaptic(HapticType.ShipCollision);
        }

        /// <summary>
        /// Resolves the true velocity of whatever hit the ball.
        /// Ships move via transform.position (not physics), so rigidbody velocity is ~0.
        /// We read VesselStatus.Speed * Course instead.
        /// </summary>
        Vector3 ResolveImpactorVelocity(Collision collision)
        {
            // VesselStatus — the authoritative source for ship velocity
            var vesselStatus = collision.gameObject.GetComponentInParent<VesselStatus>();
            if (vesselStatus != null && vesselStatus.Speed > 0.1f)
                return vesselStatus.Course * vesselStatus.Speed;

            // Physics rigidbody velocity (projectiles, other physics objects)
            if (collision.rigidbody != null && collision.rigidbody.linearVelocity.sqrMagnitude > 1f)
                return collision.rigidbody.linearVelocity;

            // Last resort
            return collision.relativeVelocity;
        }

        #endregion

        #region Visual Feedback

        void Update()
        {
            if (settings == null || ballMat == null) return;

            float speed = rb.linearVelocity.magnitude;
            float speedRatio = Mathf.Clamp01(speed / settings.SpeedForMaxVisuals);

            UpdateFlashDecay();
            UpdateEmission(speedRatio);
            UpdateLight(speedRatio);
            UpdateTrail(speedRatio);
            UpdateAuraParticles(speedRatio);
        }

        void UpdateFlashDecay()
        {
            if (flashTimer > 0f)
            {
                flashTimer -= Time.deltaTime;
                float flashRatio = Mathf.Clamp01(flashTimer / settings.ImpactFlashDuration);
                currentEmissionBoost = Mathf.Lerp(1f, settings.ImpactFlashIntensity, flashRatio);
            }
            else
            {
                currentEmissionBoost = 1f;
            }
        }

        void UpdateEmission(float speedRatio)
        {
            // Three-way color cycle
            float t = Time.time * pulseSpeed;
            float phase = t % 3f;
            Color emissionColor;

            if (phase < 1f)
                emissionColor = Color.Lerp(primaryColor, secondaryColor, phase);
            else if (phase < 2f)
                emissionColor = Color.Lerp(secondaryColor, tertiaryColor, phase - 1f);
            else
                emissionColor = Color.Lerp(tertiaryColor, primaryColor, phase - 2f);

            float breath = 0.8f + 0.2f * Mathf.Sin(Time.time * 4f);
            float emissionIntensity = Mathf.Lerp(settings.MinEmissionIntensity, settings.MaxEmissionIntensity, speedRatio);

            Color finalEmission = emissionColor * emissionIntensity * breath * currentEmissionBoost;
            ballMat.SetColor(EmissionColorId, finalEmission);

            if (ballLight != null)
                ballLight.color = emissionColor;
        }

        void UpdateLight(float speedRatio)
        {
            if (ballLight == null) return;

            float breath = 0.8f + 0.2f * Mathf.Sin(Time.time * 4f);
            float intensity = baseLightIntensity * (1f + speedRatio * 2f) * breath;
            if (currentEmissionBoost > 1f)
                intensity *= Mathf.Sqrt(currentEmissionBoost);

            ballLight.intensity = intensity;
            ballLight.range = Mathf.Lerp(settings.MinLightRange, settings.MaxLightRange, speedRatio);
        }

        void UpdateTrail(float speedRatio)
        {
            if (trail == null) return;
            trail.startWidth = Mathf.Lerp(settings.MinTrailWidth, settings.MaxTrailWidth, speedRatio);
            trail.time = Mathf.Lerp(0.15f, 0.8f, speedRatio);
        }

        void UpdateAuraParticles(float speedRatio)
        {
            if (auraParticles == null) return;
            var emission = auraParticles.emission;
            emission.rateOverTime = Mathf.Lerp(5f, 40f, speedRatio);
        }

        #endregion

        #region Impact Juice

        void TriggerFlash(float intensity)
        {
            flashTimer = settings.ImpactFlashDuration * Mathf.Max(0.3f, intensity);
        }

        void SpawnImpactParticles(Vector3 position, Vector3 normal, int count)
        {
            if (impactParticles == null || count <= 0) return;

            impactParticles.transform.position = position;
            impactParticles.transform.forward = normal;
            impactParticles.Emit(count);
        }

        void ShakeCamera(float intensity)
        {
            if (cameraController == null) return;
            cameraController.Shake(
                settings.CameraShakeIntensity * intensity,
                settings.CameraShakeDuration);
        }

        async UniTaskVoid ApplyHitstopAsync()
        {
            if (hitstopActive) return;
            hitstopActive = true;

            float originalTimeScale = Time.timeScale;
            float originalFixedDelta = Time.fixedDeltaTime;

            Time.timeScale = settings.HitstopTimeScale;
            Time.fixedDeltaTime = originalFixedDelta * settings.HitstopTimeScale;

            await UniTask.Delay(
                TimeSpan.FromSeconds(settings.HitstopDuration),
                ignoreTimeScale: true,
                cancellationToken: this.GetCancellationTokenOnDestroy());

            Time.timeScale = originalTimeScale;
            Time.fixedDeltaTime = originalFixedDelta;
            hitstopActive = false;
        }

        #endregion

        #region Goal Scoring & Reset

        public void NotifyGoalScored(Domains scoringTeam)
        {
            if (isResetting) return;
            isResetting = true;

            OnGoalScored?.Invoke(scoringTeam);

            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            Invoke(nameof(ResetToCenter), resetDelay);
        }

        void ResetToCenter()
        {
            transform.position = spawnPosition;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            isResetting = false;

            if (trail != null) trail.Clear();
        }

        public void SetSpawnPosition(Vector3 position)
        {
            spawnPosition = position;
        }

        public void FullReset()
        {
            CancelInvoke();
            isResetting = false;
            ResetToCenter();
        }

        #endregion
    }
}
