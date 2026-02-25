using System;
using UnityEngine;

namespace CosmicShore.Game.Arcade.AstroLeague
{
    /// <summary>
    /// Physics-driven ball for Astro League.
    /// Ships collide with it to push it toward goals.
    /// Self-illuminates with a point light, animated emission, and a speed trail.
    /// Designed as a "special payload" — visually distinctive and satisfying to hit.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(SphereCollider))]
    public class AstroLeagueBall : MonoBehaviour
    {
        [Header("Physics")]
        [SerializeField] float maxSpeed = 120f;
        [SerializeField] float hitForceMultiplier = 8f;
        [SerializeField] float drag = 0.01f;
        [SerializeField] float angularDrag = 0.02f;
        [SerializeField] float ballBounciness = 0.95f;
        [SerializeField] float mass = 2f;

        [Header("Reset")]
        [SerializeField] float resetDelay = 1.5f;

        [Header("Visuals")]
        [SerializeField] Color primaryColor = new(1f, 0.6f, 0.1f, 1f);
        [SerializeField] Color secondaryColor = new(0.2f, 0.5f, 1f, 1f);
        [SerializeField] Color tertiaryColor = new(1f, 0.15f, 0.6f, 1f);
        [SerializeField] float emissionIntensity = 4f;
        [SerializeField] float pulseSpeed = 1.2f;
        [SerializeField] float lightRange = 50f;
        [SerializeField] float lightIntensity = 3f;
        [SerializeField] float trailTime = 0.6f;
        [SerializeField] float trailWidth = 3f;

        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        Rigidbody rb;
        Vector3 spawnPosition;
        bool isResetting;
        Light ballLight;
        TrailRenderer trail;
        Material ballMat;
        Renderer ballRenderer;
        ParticleSystem auraParticles;

        public event Action<Domains> OnGoalScored;

        public Vector3 Velocity => rb.linearVelocity;

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.useGravity = false;
            rb.linearDamping = drag;
            rb.angularDamping = angularDrag;
            rb.mass = mass;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            // Zero-friction, high-bounce physics material so the ball glides and ricochets
            var sphereCol = GetComponent<SphereCollider>();
            sphereCol.material = new PhysicsMaterial("PayloadPhysics")
            {
                bounciness = ballBounciness,
                bounceCombine = PhysicsMaterialCombine.Maximum,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                dynamicFriction = 0f,
                staticFriction = 0f
            };

            spawnPosition = transform.position;

            SetupVisuals();
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
                ballMat.SetColor(EmissionColorId, primaryColor * emissionIntensity);
                ballRenderer.material = ballMat;
            }

            // Point light — color syncs with emission in Update
            ballLight = gameObject.AddComponent<Light>();
            ballLight.type = LightType.Point;
            ballLight.color = primaryColor;
            ballLight.range = lightRange;
            ballLight.intensity = lightIntensity;
            ballLight.shadows = LightShadows.None;

            // Speed trail — wider, more vivid
            trail = gameObject.AddComponent<TrailRenderer>();
            trail.time = trailTime;
            trail.startWidth = trailWidth;
            trail.endWidth = 0.2f;
            trail.numCapVertices = 4;
            trail.material = new Material(Shader.Find("Universal Render Pipeline/Unlit"))
            {
                color = primaryColor
            };
            SetTrailTransparent(trail.material);
            trail.startColor = primaryColor;
            trail.endColor = new Color(secondaryColor.r, secondaryColor.g, secondaryColor.b, 0f);
            trail.minVertexDistance = 0.5f;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.generateLightingData = false;

            SetupAuraParticles();
        }

        void SetTrailTransparent(Material mat)
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

            var velocityOverLifetime = auraParticles.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.orbitalX = 3f;
            velocityOverLifetime.orbitalY = 2f;
            velocityOverLifetime.orbitalZ = 1.5f;
            velocityOverLifetime.radial = -1f;

            var sizeOverLifetime = auraParticles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

            var colorOverLifetime = auraParticles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(primaryColor, 0f), new GradientColorKey(secondaryColor, 1f) },
                new[] { new GradientAlphaKey(0.8f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = gradient;

            // Additive particle material
            var particleRenderer = auraGO.GetComponent<ParticleSystemRenderer>();
            var particleMat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            particleMat.SetFloat("_Surface", 1);
            particleMat.SetInt("_Blend", 1); // Additive
            particleMat.SetColor(BaseColorId, Color.white);
            particleMat.renderQueue = 3100;
            particleRenderer.material = particleMat;
            particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void Update()
        {
            if (ballMat == null) return;

            // Three-way color cycle: primary → secondary → tertiary → primary
            float t = Time.time * pulseSpeed;
            float phase = t % 3f;
            Color emissionColor;

            if (phase < 1f)
                emissionColor = Color.Lerp(primaryColor, secondaryColor, phase);
            else if (phase < 2f)
                emissionColor = Color.Lerp(secondaryColor, tertiaryColor, phase - 1f);
            else
                emissionColor = Color.Lerp(tertiaryColor, primaryColor, phase - 2f);

            // Breath pulse on top of the color cycle
            float breath = 0.8f + 0.2f * Mathf.Sin(Time.time * 4f);
            Color finalEmission = emissionColor * emissionIntensity * breath;

            ballMat.SetColor(EmissionColorId, finalEmission);

            if (ballLight != null)
            {
                ballLight.color = emissionColor;
                ballLight.intensity = lightIntensity * breath;
            }
        }

        void FixedUpdate()
        {
            if (rb.linearVelocity.magnitude > maxSpeed)
                rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
        }

        void OnCollisionEnter(Collision collision)
        {
            if (isResetting) return;

            var otherRb = collision.rigidbody;
            if (otherRb == null) return;

            Vector3 hitDirection = (transform.position - collision.contacts[0].point).normalized;
            float impactSpeed = otherRb.linearVelocity.magnitude;
            Vector3 force = hitDirection * impactSpeed * hitForceMultiplier;

            rb.AddForce(force, ForceMode.Impulse);
        }

        /// <summary>
        /// Called by AstroLeagueGoal when the ball enters a goal zone.
        /// </summary>
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
    }
}
