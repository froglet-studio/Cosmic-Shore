using System;
using UnityEngine;
using CosmicShore.Data;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Shared visual + behaviour spec for the elemental "flower" bars used by every vessel HUD
    /// (<see cref="CosmicShore.UI.ElementalBarsView"/>). One asset is the single source of truth for
    /// the five per-tick colours, the per-element petal sprite, and the juice timings - so the look
    /// stays consistent across all 11 vessels instead of drifting between per-prefab copies.
    ///
    /// Element level is an integer in [-5, 15] distributed round-robin across five petals; each petal
    /// holds a per-tick value in {-1,0,1,2,3} → {fire, grey, white, blue, lime}. Petals are pure-white
    /// silhouettes tinted by <see cref="ColorForTick"/>, so a single multiply-tint reproduces every
    /// spec colour (no hue-shifting).
    /// </summary>
    [CreateAssetMenu(
        fileName = "ElementalBarsConfig",
        menuName = "ScriptableObjects/UI/Elemental Bars Config")]
    public class ElementalBarsConfigSO : ScriptableObject
    {
        [Serializable]
        public struct ElementPetal
        {
            [Tooltip("Element this petal sprite belongs to.")]
            public Element element;

            [Tooltip("Crisp white petal silhouette, pivot-centred on the flower; rotated 72°·n at runtime.")]
            public Sprite petalSprite;
        }

        [Header("Standard placement (fleet-wide)")]
        [Tooltip("Enforce one screen placement for the element flowers on EVERY vessel: " +
                 "ElementalBarsView.Build() stamps these values onto its own RectTransform, so " +
                 "the display is identical across the fleet regardless of how a vessel's HUD " +
                 "authored the container. Turn off only for a deliberate per-vessel layout.")]
        public bool enforceStandardPlacement = true;
        [Tooltip("Anchors on the HUD root (default: screen centre).")]
        public Vector2 standardAnchorMin = new(0.5f, 0.5f);
        public Vector2 standardAnchorMax = new(0.5f, 0.5f);
        public Vector2 standardPivot = new(0.5f, 0f);
        [Tooltip("Offset from the anchor — the Squirrel's reference placement (bottom-right cluster).")]
        public Vector2 standardAnchoredPosition = new(727.2f, -332f);
        public Vector2 standardSize = new(464.5f, 100f);

        [Header("Per-element petal sprites")]
        [Tooltip("One white petal silhouette per element. Looked up by element; order does not matter.")]
        [SerializeField] private ElementPetal[] petals;

        [Header("Element tick colours (per-petal value)")]
        [Tooltip("-1 : fire")]  public Color fireColor  = new(1f,    0.33f, 0.10f, 1f);
        [Tooltip(" 0 : grey")]  public Color greyColor  = new(0.51f, 0.51f, 0.54f, 1f);
        [Tooltip(" 1 : white")] public Color whiteColor = new(0.96f, 0.96f, 1f,    1f);
        [Tooltip(" 2 : blue")]  public Color blueColor  = new(0.22f, 0.51f, 1f,    1f);
        [Tooltip(" 3 : lime")]  public Color limeColor  = new(0.59f, 0.92f, 0.16f, 1f);
        [Tooltip("Flash colour on a petal that downgrades, before it settles to its tick colour.")]
        public Color debuffFlashColor = new(1f, 0.2f, 0.2f, 1f);

        [Header("Juice - petal transitions")]
        public float buffPopScale        = 1.3f;
        public float buffPopDuration     = 0.22f;
        public float debuffShakeDuration = 0.20f;
        public float debuffShakeStrength = 8f;

        [Header("Juice - haptics")]
        public bool  hapticOnDebuff        = true;
        public float debuffHapticAmplitude = 0.6f;
        public float debuffHapticFrequency = 0.5f;
        public float debuffHapticDuration  = 0.15f;

        [Header("Juice - label icon punch")]
        public float iconPunchDuration  = 0.25f;
        public float iconPunchScale     = 1.4f;
        public float colorTweenDuration = 0.35f;

        [Header("Juice - joust")]
        public Color joustFlashColor = Color.red;

        [Header("Juice - drift")]
        public float  driftRotationAngle    = 15f;
        public float  driftRotationDuration = 0.2f;
        public Color  driftTintColor        = new(0.7f, 0.9f, 1f, 1f);
        public Sprite doubleDriftSprite;

        /// <summary>Number of petals per flower (5-fold symmetry).</summary>
        public const int PetalCount = 5;
        /// <summary>Z-rotation between adjacent petals.</summary>
        public const float PetalSpacing = 360f / PetalCount;
        /// <summary>Lowest element level - all petals fire.</summary>
        public const int MinLevel = -PetalCount;
        /// <summary>Highest element level - all petals lime.</summary>
        public const int MaxLevel = PetalCount * 3;

        /// <summary>Maps a per-tick value {-1..3} to its spec colour.</summary>
        public Color ColorForTick(int v) => v switch
        {
            <= -1 => fireColor,
            0     => greyColor,
            1     => whiteColor,
            2     => blueColor,
            _     => limeColor, // >= 3
        };

        /// <summary>Returns the petal sprite for an element, or null if none is configured.</summary>
        public Sprite GetPetalSprite(Element element)
        {
            if (petals == null) return null;
            for (int i = 0; i < petals.Length; i++)
                if (petals[i].element == element)
                    return petals[i].petalSprite;
            return null;
        }

        /// <summary>
        /// Distributes a total level in [MinLevel, MaxLevel] round-robin across the five petals. Each
        /// petal value lands in {-1,0,1,2,3}; the first <c>extra</c> petals take the higher of the two
        /// adjacent colours, the rest the lower - exactly the spec fill order. Writes into <paramref name="dst"/>.
        /// </summary>
        public static void DistributePetalValues(int level, int[] dst)
        {
            int inc    = Mathf.Clamp(level, MinLevel, MaxLevel) - MinLevel; // 0..20
            int rounds = inc / PetalCount;   // 0..4
            int extra  = inc % PetalCount;   // 0..4
            for (int i = 0; i < PetalCount; i++)
                dst[i] = -1 + rounds + (i < extra ? 1 : 0);
        }
    }
}
