using UnityEngine;

namespace CosmicShore.Gameplay
{
    [CreateAssetMenu(fileName = "WarpFieldData", menuName = "ScriptableObjects/Warp/WarpfieldSO", order = 30)]
    [System.Serializable] 
    public class WarpFieldSO : ScriptableObject
    { 
        public int fieldThickness;
        public int fieldWidth;
        public int fieldHeight;
        public float fieldMax;

        [SerializeField, Min(0f), Tooltip("Seconds the field takes to come in when a cell switches it on, and to " +
                                          "go out when the cell retires — every scale it drives eases between 1 " +
                                          "and the field's value over this, so nothing pops.")]
        float easeSeconds = 1.5f;

        public float EaseSeconds => easeSeconds;

        public WarpFieldSO()
        {
            fieldThickness = 200;
            fieldWidth = 200;
            fieldHeight = 200;
            fieldMax = 300f;
        }

        virtual public Vector3 HybridVector(Transform node) // direction is aligned with the gradient, but magnitude is the value of the scalar field
        {
            return Vector3.zero;
        }

        /// <summary>
        /// The field's LOCAL LENGTH SCALE at <paramref name="offset"/> from its centre — the factor
        /// <see cref="WarpFieldRuntime"/> multiplies every length a player observes by
        /// (Docs/WARP_FIELD.md). 1 is no warp, which is what a field that does not override this
        /// (<see cref="ZeroWarp"/>, the 2022 vector-only fields) means.
        /// </summary>
        public virtual float ScaleAt(Vector3 offset) => 1f;
    }
}
