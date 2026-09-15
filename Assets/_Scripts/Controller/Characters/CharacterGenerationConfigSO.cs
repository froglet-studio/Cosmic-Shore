using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Every global tunable of the generator, in one asset (<c>Resources/Characters/CharacterGenerationConfig</c>).
    /// Per-clade values live on the clade; per-individual values in the genome; everything else is here.
    /// <see cref="CreateBaseHead"/> is the ONE line that decides procedural vs authored head.
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterGenerationConfig", menuName = "ScriptableObjects/Characters/Generation Config")]
    public class CharacterGenerationConfigSO : ScriptableObject
    {
        public const string ResourcesPath = "Characters/CharacterGenerationConfig";

        public enum BaseHeadKind { Procedural = 0, Authored = 1 }

        [Header("Sources")]
        [Tooltip("The human source every chimera blends from. Falls back to the catalog's IsHuman asset.")]
        public CladeSO HumanSource;

        [Header("Base head")]
        public BaseHeadKind Head = BaseHeadKind.Procedural;
        [Tooltip("Authored sculpt with blend shapes named after HeadAxis members. Only read when Head = Authored.")]
        public Mesh AuthoredHeadMesh;
        [Tooltip("Sites for the authored head (the procedural head declares its own).")]
        public HeadSiteSpec[] AuthoredSites;
        public ProceduralBaseHead.Form ProceduralForm = ProceduralBaseHead.Form.Default;
        public HeadDetail PortraitDetail = HeadDetail.Portrait;
        public HeadDetail RuntimeDetail = HeadDetail.Runtime;

        [Header("Blending")]
        [Tooltip("Effective weight = w^gamma (renormalised). Below 1 boosts the minority clade's continuous travel; 1 is linear.")]
        [Range(0.3f, 1.5f)] public float TravelGamma = 0.72f;
        [Tooltip("Multiplier on every clade's authored axis targets. 1 = as authored.")]
        [Range(0.5f, 1.5f)] public float CladeAxisTravel = 1f;

        [Header("Human variation (the control)")]
        [Tooltip("Sigma of the per-axis gaussian roll for the human base proportions.")]
        [Range(0.05f, 0.8f)] public float HumanVariationSigma = 0.34f;
        [Range(0.1f, 1f)] public float HumanVariationClamp = 0.85f;

        [Header("Human palette")]
        public Color SkinPale = new Color(0.82f, 0.64f, 0.52f);
        public Color SkinDeep = new Color(0.26f, 0.15f, 0.10f);
        [Tooltip("Blended in by SkinWarmth: 0 = cool/olive, 1 = warm/ruddy.")]
        public Color SkinCoolTint = new Color(0.92f, 0.96f, 0.88f);
        public Color SkinWarmTint = new Color(1.03f, 0.94f, 0.92f);
        public Color HairDark = new Color(0.06f, 0.04f, 0.03f);
        public Color HairPale = new Color(0.82f, 0.68f, 0.45f);
        public Color HairRedTint = new Color(1.0f, 0.62f, 0.38f);
        public Color LipTint = new Color(0.78f, 0.38f, 0.38f);

        [Header("Textures")]
        public int SkinTextureSize = 1024;
        public int EyeTextureSize = 256;
        public int SmallTextureSize = 256;
        [Tooltip("Strength of the pore/detail normal map. 0 disables it.")]
        [Range(0f, 4f)] public float DetailNormalStrength = 1.8f;

        [Header("Materials (optional templates; URP Lit is found when empty)")]
        public Material SkinMaterialTemplate;
        public Material EyeMaterialTemplate;
        public Material KeratinMaterialTemplate;
        public Material HairMaterialTemplate;

        /// <summary>
        /// THE swap point. Returning an <see cref="AuthoredBaseHead"/> here is the whole of the
        /// "the procedural human failed, use a sculpt" change — nothing downstream knows.
        /// </summary>
        public IBaseHead CreateBaseHead(HeadDetail detail)
        {
            if (Head == BaseHeadKind.Authored && AuthoredHeadMesh != null)
                return new AuthoredBaseHead(AuthoredHeadMesh, AuthoredSites);
            return new ProceduralBaseHead(detail, ProceduralForm);
        }

        public static CharacterGenerationConfigSO LoadDefault() => Resources.Load<CharacterGenerationConfigSO>(ResourcesPath);
    }
}
