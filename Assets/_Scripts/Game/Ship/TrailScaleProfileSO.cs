using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "TrailScaleProfile", menuName = "CosmicShore/Trail/Scale Profile")]
    public class TrailScaleProfileSO : ScriptableObject
    {
        // If true → multiply current scalers by vector
        // If false → set absolute scalers to vector
        public bool isChange = true;

        // XScaler, YScaler, ZScaler targets or multipliers (depending on isChange)
        public Vector3 scaleXYZ = new Vector3(0.7f, 1f, 0.7f);

        // Optional smooth apply/revert
        public float applyLerpSeconds  = 0.15f;
        public float revertLerpSeconds = 0.15f;
    }
}
