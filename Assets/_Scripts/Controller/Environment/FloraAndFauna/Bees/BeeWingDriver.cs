using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A bee's wingbeat: the CPU-driver motion tier (the /fauna skill §3), because a wing is a <see cref="HealthPrism"/>
    /// (conserved mass with its own collider) that no vertex shader can move. It writes only <c>localRotation</c> on the
    /// two wing pivots (never a position), so the wings flap about their roots with zero collider or index work of their
    /// own; the body's per-frame index sync (<see cref="Fauna"/>'s movers contract) carries them.
    ///
    /// <para>The pivots are found by name under the bee (<see cref="leftWings"/>, <see cref="rightWings"/>), so the prefab
    /// needs no fileID references into its nested instances - the bee model's four wings (Bee.fbx, round 2) and the two
    /// leading-edge vein prisms beat as one. A per-bee phase keeps a swarm from beating in lockstep.</para>
    /// </summary>
    [DefaultExecutionOrder(-1)]
    public class BeeWingDriver : MonoBehaviour
    {
        [Tooltip("Names of the left wings' pivots (children of the bee, at any depth): they beat together.")]
        [SerializeField] string[] leftWings = { "BeeWingL", "Bee_WingFL", "Bee_WingHL" };
        [Tooltip("Names of the right wings' pivots.")]
        [SerializeField] string[] rightWings = { "BeeWingR", "Bee_WingFR", "Bee_WingHR" };
        [Tooltip("Wingbeats per second. Real bees beat at ~200 Hz; this is the read of a blur, not the real rate.")]
        [SerializeField, Min(0f)] float beatsPerSecond = 9f;
        [Tooltip("Half the stroke, in degrees, about each wing's root.")]
        [SerializeField, Range(0f, 80f)] float strokeDegrees = 38f;

        Transform[] _left, _right;
        Quaternion[] _leftRest, _rightRest;
        float _phase;

        void Awake()
        {
            Resolve(leftWings, out _left, out _leftRest);
            Resolve(rightWings, out _right, out _rightRest);
            _phase = (GetInstanceID() & 1023) * 0.0061f;
        }

        void Resolve(string[] names, out Transform[] wings, out Quaternion[] rest)
        {
            int n = names?.Length ?? 0;
            wings = new Transform[n];
            rest = new Quaternion[n];
            for (int i = 0; i < n; i++)
            {
                wings[i] = Find(transform, names[i]);
                if (wings[i]) rest[i] = wings[i].localRotation;
            }
        }

        void Update()
        {
            float a = strokeDegrees * Mathf.Sin((Time.time * beatsPerSecond + _phase) * 6.2831853f);
            Beat(_left, _leftRest, a);
            Beat(_right, _rightRest, -a);
        }

        static void Beat(Transform[] wings, Quaternion[] rest, float degrees)
        {
            if (wings == null) return;
            var turn = Quaternion.Euler(0f, 0f, degrees);
            for (int i = 0; i < wings.Length; i++)
                if (wings[i]) wings[i].localRotation = rest[i] * turn;
        }

        static Transform Find(Transform root, string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            for (int i = 0; i < root.childCount; i++)
            {
                var c = root.GetChild(i);
                if (c.name == name) return c;
                var deeper = Find(c, name);
                if (deeper) return deeper;
            }
            return null;
        }
    }
}
