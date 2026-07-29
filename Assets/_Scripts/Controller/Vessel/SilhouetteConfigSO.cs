using UnityEngine;
using CosmicShore.Gameplay;


namespace CosmicShore.Gameplay
{
    [CreateAssetMenu(fileName = "SilhouetteConfig", menuName = "ScriptableObjects/UI/Silhouette Config")]
    public class SilhouetteConfigSO : ScriptableObject
    {
        // Smoothing (jaw travel + silhouette rotation)
        public bool  smooth = true;
        public float smoothingSeconds = 0.08f;

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
