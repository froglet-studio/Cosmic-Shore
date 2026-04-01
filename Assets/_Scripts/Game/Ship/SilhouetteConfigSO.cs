using UnityEngine;
using CosmicShore.Core;
using CosmicShore.Game;

namespace CosmicShore
{
    [CreateAssetMenu(fileName = "SilhouetteConfig", menuName = "CosmicShore/UI/Silhouette Config")]
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
        public float gapMultiplier       = 1f; // row gap (±xShift) — already used
        public float columnGapMultiplier = 1f; // NEW: spacing between columns (stride)

        // Color source
        public bool useDomainPaletteColors = true;
        public DomainColorPaletteSO domainPalette; // your existing palette

        // Danger (overheat) visual swap (UI-only)
        public bool       enableDangerVisual = true;
        public GameObject dangerBlockPrefab;       // optional: UI prefab to swap sprite from
        public Color      dangerColor = new Color(1f, 0.25f, 0.2f); // UI tint during danger (alpha is respected)
    }
}
