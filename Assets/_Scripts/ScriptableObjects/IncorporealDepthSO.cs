using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The ONE depth effect shared by the HyperSea's incorporeal parts, at
    /// <c>Resources/IncorporealDepth</c> (<c>IncorporealDepth.hlsl</c>, Docs/SPINDLE_LOD.md §7).
    ///
    /// Mass has a depth language: a prism OPENS with distance on a squared-distance ramp. The
    /// incorporeal parts - spindles today; the snow shards, the toy switches, the membrane and the
    /// nucleus as each adopts the include - speak the same one, so the sea reads as one thing: they
    /// open on the prisms' own ramp, as hard as DANGER mass does, and a wave travels through what has
    /// opened so the part reads as alive. The ramp and the strength are READ off the theme's danger
    /// prism, never copied, so a retune of the prism materials moves every incorporeal part with it.
    /// What is authored here is only what the prisms do not have: how strongly to follow them, and
    /// the wave.
    /// </summary>
    [CreateAssetMenu(fileName = "IncorporealDepth", menuName = "ScriptableObjects/Environment/Incorporeal Depth")]
    public class IncorporealDepthSO : ScriptableObject
    {
        public const string ResourcePath = "IncorporealDepth";

        [Header("The prisms' depth effect, read")]
        [Tooltip("The material set whose DANGEROUS block material supplies the spread (_Spread) and the ramp (_SqrDistance). The theme's base set (OriginalMaterialSet), so the incorporeal parts open exactly as hard and on exactly the ramp danger mass does.")]
        public SO_MaterialSet tierMaterialSet;

        [Tooltip("Multiplier on the danger prisms' spread. 1 = on par with danger mass; above 1, stronger.")]
        [Range(0f, 3f)] public float strength = 1f;

        [Header("Alive: the wave through what has opened")]
        [Tooltip("How much the opening breathes with the wave, as a fraction of the opening (0 = still). Scales with how open the part is, so a closed part never moves.")]
        [Range(0f, 1f)] public float flowAmplitude = 0.35f;

        [Tooltip("Wave crests along a part's flow coordinate (a spindle's length).")]
        [Range(0.1f, 6f)] public float flowWaves = 1.5f;

        [Tooltip("Crests per second, travelling toward the end of the flow coordinate (a spindle's tip: away from its crystal).")]
        [Range(0f, 4f)] public float flowSpeed = 0.6f;

        [Tooltip("Brightness added on a crest of a fully open part (0 = no pulse of light).")]
        [Range(0f, 3f)] public float pulseGlow = 0.8f;

        static readonly int SpreadId = Shader.PropertyToID("_Spread");
        static readonly int SqrDistanceId = Shader.PropertyToID("_SqrDistance");

        /// <summary>The danger tier's spread (xyz, x <see cref="strength"/>) and squared-distance ramp
        /// (w) - BlockGraph's own two numbers. Zero spread when no tier is wired (the effect is off).</summary>
        public Vector4 TierSpread()
        {
            var m = tierMaterialSet ? tierMaterialSet.DangerousBlockMaterial : null;
            if (!m || !m.HasProperty(SpreadId)) return new Vector4(0f, 0f, 0f, 100000f);
            Vector4 s = m.GetVector(SpreadId) * strength;
            s.w = m.HasProperty(SqrDistanceId) ? m.GetFloat(SqrDistanceId) : 100000f;
            return s;
        }

        public Vector4 Flow => new(flowAmplitude, flowWaves, flowSpeed, pulseGlow);

        static IncorporealDepthSO _instance;
        static bool _resolved;

        /// <summary>The shipped asset, or null (the effect stays off: every adopter is a no-op).</summary>
        public static IncorporealDepthSO Instance
        {
            get
            {
                if (!_resolved)
                {
                    _instance = Resources.Load<IncorporealDepthSO>(ResourcePath);
                    _resolved = true;
                }
                return _instance;
            }
        }
    }
}
