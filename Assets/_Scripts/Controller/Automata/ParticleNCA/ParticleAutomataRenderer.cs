using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Renders a ParticleAutomataSimulator's population as camera-facing
    /// billboards via Graphics.RenderPrimitives — zero per-particle
    /// GameObjects, zero CPU readback. Color blends from accent to base by
    /// the vitality channel (state0.x) so freshly damaged/regrowing regions
    /// read visually.
    /// </summary>
    [RequireComponent(typeof(ParticleAutomataSimulator))]
    public class ParticleAutomataRenderer : MonoBehaviour
    {
        [Header("Rendering")]
        [Tooltip("Material using the CosmicShore/Automata/ParticleAutomata shader.")]
        [SerializeField] Material material;

        [Tooltip("Billboard size in world units.")]
        [SerializeField] float particleSize = 0.6f;

        [Tooltip("Color at full vitality.")]
        [SerializeField] Color baseColor = new(0.23f, 0.84f, 0.75f, 0.9f);

        [Tooltip("Color at zero vitality (fresh / damaged particles).")]
        [SerializeField] Color accentColor = new(1f, 0.35f, 0.65f, 0.9f);

        [Tooltip("Extra padding on the culling bounds, in world units.")]
        [SerializeField] float boundsPadding = 10f;

        static readonly int ParticlesId = Shader.PropertyToID("_Particles");
        static readonly int LocalToWorldId = Shader.PropertyToID("_LocalToWorld");
        static readonly int ParticleSizeId = Shader.PropertyToID("_ParticleSize");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int AccentColorId = Shader.PropertyToID("_AccentColor");

        ParticleAutomataSimulator _simulator;
        MaterialPropertyBlock _propertyBlock;

        void Awake()
        {
            _simulator = GetComponent<ParticleAutomataSimulator>();
            _propertyBlock = new MaterialPropertyBlock();
        }

        void LateUpdate()
        {
            if (material == null || !_simulator.IsInitialized) return;

            _propertyBlock.SetBuffer(ParticlesId, _simulator.ParticleBuffer);
            _propertyBlock.SetMatrix(LocalToWorldId, _simulator.LocalToWorld);
            _propertyBlock.SetFloat(ParticleSizeId, particleSize);
            _propertyBlock.SetColor(BaseColorId, baseColor);
            _propertyBlock.SetColor(AccentColorId, accentColor);

            // Trained shapes live within ~1.5 units of the local origin.
            float extent = _simulator.LocalToWorld.lossyScale.x * 1.5f + boundsPadding;

            var renderParams = new RenderParams(material)
            {
                worldBounds = new Bounds(transform.position, Vector3.one * extent * 2f),
                matProps = _propertyBlock,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                layer = gameObject.layer,
            };

            Graphics.RenderPrimitives(renderParams, MeshTopology.Triangles,
                vertexCount: 6, instanceCount: _simulator.ParticleCount);
        }
    }
}
