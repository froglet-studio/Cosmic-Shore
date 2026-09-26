using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Reflex.Attributes;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Butterfly's ONE skimmer: a capsule that hangs BELOW the hull and is live only in Dust
    /// mode. Design record: <c>R_VesselActions/BUTTERFLY.md</c> §3.1.
    ///
    /// <para><b>What it is.</b> A trigger <see cref="CapsuleCollider"/> on this GameObject, axis
    /// local Y, authored with height 1 / radius 0.5 and its centre at (0, -0.5, 0) — so the capsule
    /// runs from the hull DOWN. The sibling <see cref="Skimmer"/> authors <c>elongateYOnly</c>, so
    /// SPACE's <c>Scale</c> drives only this transform's local Y: <b>Space lengthens the dust
    /// column and nothing else</b>. Everything the dust DOES lives in the skimmer's effect
    /// container; this component only owns whether it is on, and what it looks like.</para>
    ///
    /// <para><b>Off is OFF.</b> In Mass mode the collider is disabled (PhysX sends no trigger
    /// callbacks — Unity would otherwise deliver them to a DISABLED MonoBehaviour) AND the
    /// <see cref="SkimmerImpactor"/> is disabled, whose own OnDisable clears its overlap set and
    /// unregisters its shell-tier probes. Disabling only the collider leaves a stale overlap set,
    /// because a disabled collider fires no OnTriggerExit.</para>
    ///
    /// <para><b>The dust is the ONLY thing drawn.</b> There is no mesh and no forcefield overlay —
    /// the two sphere skimmers this replaced each carried the crackle overlay and read as two
    /// visible bubbles. The visual is a particle column built at runtime (no authored particle
    /// asset to drift), sized to the live capsule, and it honours continuity of existence at both
    /// ends: switching on starts emission and the column fills in over a particle lifetime;
    /// switching off STOPS EMITTING and lets the motes already in the air finish and fade, rather
    /// than clearing them. The particle object lives beside the capsule rather than under it, so
    /// the capsule's non-uniform scale never stretches a mote.</para>
    /// </summary>
    [RequireComponent(typeof(CapsuleCollider))]
    public sealed class ButterflyDustField : MonoBehaviour
    {
        [Header("Look")]
        [Tooltip("Particle material for the dust motes. A URP Particles/Unlit material; a missing " +
                 "one falls back to that shader and is reported once.")]
        [SerializeField] Material particleMaterial;

        [Tooltip("FALLBACK mote colour, used only while the vessel has no playable domain or no " +
                 "palette is reachable. In play the motes wear the SHIELDED tier of the vessel's " +
                 "own domain (see ResolveMoteColour). Alpha is shaped over each mote's life (in, " +
                 "hold, out) and this alpha is the peak in both cases.")]
        [SerializeField] Color dustColor = new(1f, 0.86f, 0.52f, 0.9f);

        [Tooltip("Motes emitted per second per 100 cubic units of dust column, so a longer column " +
                 "(more Space) is equally dense rather than thinner.")]
        [SerializeField, Min(0f)] float motesPer100Volume = 3.5f;

        [Tooltip("Hard ceiling on motes per second, whatever the column's size.")]
        [SerializeField, Min(1f)] float maxMotesPerSecond = 260f;

        [SerializeField, Min(0.05f)] float moteLifetime = 1.1f;

        [Tooltip("Mote size range in WORLD units. Sized for the Butterfly's own camera, which sits " +
                 "~204 units back: at that range a world unit is ~2.6 px at 1080p, so the original " +
                 "0.5-1.6 motes were 1-4 px and Dust mode read as nothing happening at all.")]
        [SerializeField] Vector2 moteSize = new(2.5f, 6f);

        [Tooltip("Motes drift slowly downward, as dust falls off a wing.")]
        [SerializeField] float fallSpeed = 6f;

        // The palette. Injected: the dust skimmer is a child of the vessel prefab, and vessels are
        // GameObjectInjector.InjectRecursive'd on every spawn path.
        [Inject] GameDataSO _gameData;

        IVesselStatus _status;
        Domains _paintedDomain;
        bool _painted;

        CapsuleCollider _capsule;
        SkimmerImpactor _impactor;
        ParticleSystem _particles;
        bool _active;
        bool _built;
        static bool s_warnedNoMaterial;

        /// <summary>True while the dust is live.</summary>
        public bool IsActive => _active;

        void Awake()
        {
            _capsule = GetComponent<CapsuleCollider>();
            TryGetComponent(out _impactor);
            // Default to OFF: a Butterfly spawns in Mass mode, and a skimmer live for its first
            // frame would dust whatever it spawned inside.
            ApplyColliderState(false);
        }

        /// <summary>
        /// Hand the field the vessel it belongs to, so the motes can wear that vessel's domain.
        /// Called from <see cref="SpreadWingsActionExecutor"/>'s Initialize, which re-runs on a
        /// re-init, so a vessel handed to another pilot repaints on the next frame.
        /// </summary>
        public void Bind(IVesselStatus status)
        {
            _status = status;
            _painted = false;
        }

        /// <summary>Switch the dust on or off. Idempotent.</summary>
        public void SetActive(bool active)
        {
            if (_built && _active == active) return;
            _active = active;
            ApplyColliderState(active);

            EnsureParticles();
            if (!_particles) return;
            if (active)
            {
                if (!_particles.isPlaying) _particles.Play(true);
            }
            else
            {
                // StopEmitting, never StopEmittingAndClear: the motes in the air finish their
                // lives and fade out — continuity of existence.
                _particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        void ApplyColliderState(bool active)
        {
            if (_capsule) _capsule.enabled = active;
            if (_impactor) _impactor.enabled = active;
        }

        void OnDisable()
        {
            if (_particles) _particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        void OnDestroy()
        {
            if (_particles) Destroy(_particles.gameObject);
        }

        void LateUpdate()
        {
            if (!_particles) return;
            FitParticlesToCapsule();
            RefreshColour();
        }

        /// <summary>
        /// Repaint when the vessel's domain changes. Read LIVE rather than snapshotted, so the
        /// freestyle domain changer and every replicated NetDomain change reach the dust on the
        /// next frame. Only NEW motes take the colour - the ones already in the air finish in
        /// the colour they were born in, which is continuity rather than a snap.
        /// </summary>
        void RefreshColour()
        {
            var domain = _status != null && _status.Player != null ? _status.Domain : Domains.Blue;
            if (_painted && domain == _paintedDomain) return;
            _painted = true;
            _paintedDomain = domain;

            var main = _particles.main;
            main.startColor = ResolveMoteColour(domain);
        }

        /// <summary>
        /// The SHIELDED tier's rim colour for <paramref name="domain"/> - the frosty, bright colour
        /// that says "shielded" on a prism - through <c>SO_ColorSet.TryGetPrismKindColors</c>, the
        /// single source every prism tier is painted from, so the dust can never drift from the
        /// shielded mass it sits beside. The rim rather than the base face because a mote is a
        /// point of light: the base is the darker half of the pair and reads as dim smoke.
        /// Blue (no team) and a missing palette fall back to the authored dust colour.
        /// </summary>
        Color ResolveMoteColour(Domains domain)
        {
            var colorSet = _gameData?.ThemeManagerData?.ColorSet;
            if (domain == Domains.Blue || colorSet == null ||
                !colorSet.TryGetPrismKindColors(domain, PrismKind.Shielded, out var rim, out _))
                return dustColor;

            rim.a = dustColor.a;
            return rim;
        }

        /// <summary>The capsule's world length and radius, read the way PhysX sizes it.</summary>
        void MeasureCapsule(out float length, out float radius)
        {
            var lossy = transform.lossyScale;
            length = _capsule ? _capsule.height * Mathf.Abs(lossy.y) : Mathf.Abs(lossy.y);
            float r = Mathf.Max(Mathf.Abs(lossy.x), Mathf.Abs(lossy.z));
            radius = _capsule ? _capsule.radius * r : 0.5f * r;
        }

        void FitParticlesToCapsule()
        {
            MeasureCapsule(out float length, out float radius);
            var pt = _particles.transform;
            // Centre the emitter on the capsule's own centre, in world terms.
            pt.position = transform.TransformPoint(_capsule ? _capsule.center : new Vector3(0f, -0.5f, 0f));
            pt.rotation = transform.rotation;

            var shape = _particles.shape;
            shape.scale = new Vector3(radius * 2f, length, radius * 2f);

            var emission = _particles.emission;
            float volume = Mathf.PI * radius * radius * length;
            emission.rateOverTime = Mathf.Min(maxMotesPerSecond, volume / 100f * motesPer100Volume);
        }

        void EnsureParticles()
        {
            if (_built) return;
            _built = true;

            var go = new GameObject("DustMotes");
            // BESIDE the capsule, not under it: the capsule carries a non-uniform scale (Space
            // stretches its Y), and a child would inherit it.
            go.transform.SetParent(transform.parent ? transform.parent : transform, false);
            go.layer = gameObject.layer;

            _particles = go.AddComponent<ParticleSystem>();
            _particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = _particles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World; // dust trails behind the hull
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.startLifetime = moteLifetime;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(moteSize.x, moteSize.y);
            main.startColor = dustColor;
            main.gravityModifier = 0f;
            main.maxParticles = Mathf.CeilToInt(maxMotesPerSecond * moteLifetime * 1.25f);

            var shape = _particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.boxThickness = Vector3.zero;

            var velocity = _particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            velocity.y = new ParticleSystem.MinMaxCurve(-fallSpeed, -fallSpeed);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            // In, hold, out: a mote is never born or retired at full alpha.
            var colour = _particles.colorOverLifetime;
            colour.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f),
                    new GradientAlphaKey(1f, 0.65f), new GradientAlphaKey(0f, 1f),
                });
            colour.color = new ParticleSystem.MinMaxGradient(g);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = ResolveMaterial();

            FitParticlesToCapsule();
        }

        Material ResolveMaterial()
        {
            if (particleMaterial) return particleMaterial;
            if (!s_warnedNoMaterial)
            {
                s_warnedNoMaterial = true;
                CSDebug.LogWarning("[ButterflyDustField] no particle material wired — falling " +
                                   "back to URP Particles/Unlit. Wire one on the dust skimmer.", this);
            }
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            return shader ? new Material(shader) : null;
        }
    }
}
