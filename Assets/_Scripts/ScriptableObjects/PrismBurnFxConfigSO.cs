using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// How burning prisms LOOK (<c>PrismBurnView</c>, drawn for every <c>PrismBurnManager</c> burn - Fulmen's bolts,
    /// the Grizzly's claw). Three distance tiers, crossfaded by dithered bands so no prism ever pops:
    /// <list type="bullet">
    /// <item><b>NEAR</b> (closer than <see cref="NearDistance"/>): <see cref="NearMaterial"/> on a subdivided cube - the
    /// full paper-curl burn.</item>
    /// <item><b>MID</b> (to <see cref="MidDistance"/>): <see cref="MidMaterial"/> on a plain cube - the same dissolve and
    /// ember rim, no curl, cheaper noise.</item>
    /// <item><b>FAR</b>: nothing is drawn; the prism keeps its ordinary look and, when it burns out, simply goes back to
    /// the pool (no debris).</item>
    /// </list>
    /// The LASER style (Fulmen's flameless burn, <c>PrismLaserBurn.shader</c>) is a fourth, separate list: one
    /// <see cref="LaserMaterial"/> on a plain cube, drawn out to <see cref="LaserDistance"/> (bolts land far away),
    /// tinted per burn with the domain colour (<see cref="AshColor"/>). Docs/Shaders/PRISM_EFFECTS.md.
    /// Place the asset at <c>Resources/PrismBurnFxConfig</c>. With no asset (or no materials) burns keep the old look:
    /// the danger state and an ordinary explosion at burnout.
    /// </summary>
    public enum AshColorSource : byte { Prism = 0, Igniter = 1 }

    [CreateAssetMenu(fileName = "PrismBurnFxConfig", menuName = "ScriptableObjects/Rendering/Prism Burn FX Config")]
    public class PrismBurnFxConfigSO : ScriptableObject
    {
        public const string ResourcePath = "PrismBurnFxConfig";

        [Header("Materials (PrismPaperBurn.shader, GPU instancing ON)")]
        [SerializeField, Tooltip("PrismPaperBurnNear.mat - _BURN_CURL on.")] Material nearMaterial;
        [SerializeField, Tooltip("PrismPaperBurnMid.mat - _BURN_CURL off.")] Material midMaterial;

        [Header("Laser burn (Fulmen) - PrismLaserBurn.shader, GPU instancing ON")]
        [SerializeField, Tooltip("PrismLaserBurn.mat. Empty: laser burns fall back to the paper look.")] Material laserMaterial;
        [SerializeField, Min(0f), Tooltip("Laser burns are drawn out to this distance from the camera (Fulmen strikes far away).")]
        float laserDistance = 600f;
        [SerializeField, Min(0.01f), Tooltip("Width of the laser burn's dithered far fade.")] float laserFarBand = 120f;
        [SerializeField, Tooltip("Whose domain colour the skin and ash take: the burning PRISM's (it ashes in its own colour) or the IGNITER's.")]
        AshColorSource ashColorSource = AshColorSource.Prism;
        [SerializeField, Tooltip("Ash colour when the domain has no colour set entry (or none).")]
        Color fallbackAshColor = new(0.2f, 0.45f, 1f, 1f);

        [Header("Distance tiers (world units from the main camera)")]
        [SerializeField, Min(0f), Tooltip("NEAR -> MID crossfade centre.")] float nearDistance = 70f;
        [SerializeField, Min(0.01f), Tooltip("Width of the NEAR -> MID dithered crossfade.")] float nearBand = 30f;
        [SerializeField, Min(0f), Tooltip("MID -> FAR fade-out centre. Beyond it nothing is drawn.")] float midDistance = 260f;
        [SerializeField, Min(0.01f), Tooltip("Width of the MID -> FAR dithered fade.")] float farBand = 80f;

        [Header("Look")]
        [SerializeField, Range(0f, 0.5f), Tooltip("Seconds the burn proxy dithers in over the prism when it is lit.")]
        float appearSeconds = 0.1f;
        [SerializeField, Range(1f, 1.1f), Tooltip("Proxy scale over the prism's bounds, so it covers it during the crossfade.")]
        float inflate = 1.015f;
        [SerializeField, Range(2, 16), Tooltip("NEAR proxy subdivisions per cube face edge - more = smoother curl, more vertices.")]
        int nearSubdivisions = 8;

        [Header("Burnout")]
        [SerializeField, Tooltip("A burned-out prism dies with no explosion/SFX (it already burned away on screen).")]
        bool silentBurnout = true;

        [Header("Budget")]
        [SerializeField, Range(16, 1023), Tooltip("Most burn proxies drawn at once (per tier). Extra burns keep the ordinary look.")]
        int maxDrawn = 256;

        public Material NearMaterial => nearMaterial;
        public Material MidMaterial => midMaterial;
        public float NearDistance => nearDistance;
        public float NearBand => nearBand;
        public float MidDistance => Mathf.Max(midDistance, nearDistance);
        public float FarBand => farBand;
        public float AppearSeconds => appearSeconds;
        public float Inflate => inflate;
        public int NearSubdivisions => nearSubdivisions;
        public bool SilentBurnout => silentBurnout;
        public int MaxDrawn => maxDrawn;
        public Material LaserMaterial => laserMaterial;
        public float LaserDistance => laserDistance;
        public float LaserFarBand => laserFarBand;
        public AshColorSource AshSource => ashColorSource;
        public Color FallbackAshColor => fallbackAshColor;
        public bool IsUsable => nearMaterial != null && midMaterial != null;

        static PrismBurnFxConfigSO s_instance;
        static bool s_resolved;

        /// <summary><c>Resources/PrismBurnFxConfig</c>, resolved once and cached; null when absent.</summary>
        public static PrismBurnFxConfigSO Instance
        {
            get
            {
                if (!s_resolved)
                {
                    s_instance = Resources.Load<PrismBurnFxConfigSO>(ResourcePath);
                    s_resolved = true;
                }
                return s_instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_instance = null;
            s_resolved = false;
        }
    }
}
