using UnityEngine;
using CosmicShore.Gameplay;


namespace CosmicShore.Gameplay
{
    [CreateAssetMenu(fileName = "SilhouetteConfig", menuName = "ScriptableObjects/UI/Silhouette Config")]
    public class SilhouetteConfigSO : ScriptableObject
    {
        public enum FlowDirection { HorizontalRTL, VerticalTopDown }

        // Mapping (legacy)
        public float worldToUIScale = 2f;
        public float imageScale     = 0.02f;

        // Flow & Layout
        public FlowDirection flow = FlowDirection.VerticalTopDown;
        public float columnRotationOffsetDeg = 0f;
        public int  minColumns = 10;
        public int maxColumns;
        public bool applyMaxColumnValues;

        // Smoothing
        public bool  smooth = true;
        public float smoothingSeconds = 0.08f;

        // Multipliers
        public float thicknessMultiplier = 1f; // block thickness
        public float lengthMultiplier    = 1f; // block length
        public float gapMultiplier       = 1f; // row gap (±xShift) - already used
        public float columnGapMultiplier = 1f; // NEW: spacing between columns (stride)

        // Color source - domain (and danger) colors now come from the theme ColorSet (R5),
        // resolved by SilhouetteController via GameDataSO.ThemeManagerData. This flag just
        // gates whether the silhouette trail is tinted with the domain color at all.
        public bool useDomainPaletteColors = true;

        // Danger (overheat) visual swap (UI-only)
        public bool       enableDangerVisual = true;
        public GameObject dangerBlockPrefab;       // optional: UI prefab to swap sprite from
        public Color      dangerColor = new Color(1f, 0.25f, 0.2f); // UI tint during danger (alpha is respected)

        // Holographic silhouette-icon treatment (CosmicShore/UI/SilhouetteHolo) - domain-tinted
        // body, pulsing edge rim, scanline shimmer. Look parameters live ON THE MATERIAL (one
        // asset for every vessel, per Config Separation); only the domain accent is per-instance.
        [Header("Holo icon treatment")]
        [Tooltip("Apply the holographic material to the silhouette icon images.")]
        public bool enableHoloStyle = true;
        [Tooltip("The shared SilhouetteHolo material. Unassigned = plain sprite look.")]
        public Material holoMaterial;
    }
}
