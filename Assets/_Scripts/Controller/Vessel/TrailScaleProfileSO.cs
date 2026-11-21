using UnityEngine;

namespace CosmicShore.Gameplay
{
    [CreateAssetMenu(fileName = "TrailScaleProfile", menuName = "ScriptableObjects/Trail/Scale Profile")]
    public class TrailScaleProfileSO : ScriptableObject
    {
        // If true → multiply current scalers by vector
        // If false → set absolute scalers to vector
        [SerializeField] bool isChange = true;
        public bool IsChange => isChange;

        // XScaler, YScaler, ZScaler targets or multipliers (depending on isChange)
        [SerializeField] Vector3 scaleXYZ = new Vector3(0.7f, 1f, 0.7f);
        public Vector3 ScaleXYZ => scaleXYZ;

        // Optional smooth apply/revert
        [SerializeField] float applyLerpSeconds  = 0.15f;
        public float ApplyLerpSeconds => applyLerpSeconds;
        [SerializeField] float revertLerpSeconds = 0.15f;
        public float RevertLerpSeconds => revertLerpSeconds;
    }
}
