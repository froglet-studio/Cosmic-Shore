using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A bee's wingbeat: the CPU-driver motion tier (the /fauna skill §3), because a wing is a <see cref="HealthPrism"/>
    /// (conserved mass with its own collider) that no vertex shader can move. It writes only <c>localRotation</c> on the
    /// two wing pivots (never a position), so the wings flap about their roots with zero collider or index work of their
    /// own; the body's per-frame index sync (<see cref="Fauna"/>'s movers contract) carries them.
    ///
    /// <para>The pivots are found by name under the bee (<see cref="leftWingName"/>, <see cref="rightWingName"/>), so
    /// the prefab needs no fileID references into its nested wing instances. A per-bee phase keeps a swarm from beating
    /// in lockstep.</para>
    /// </summary>
    [DefaultExecutionOrder(-1)]
    public class BeeWingDriver : MonoBehaviour
    {
        [Tooltip("Name of the left wing's pivot (a child of the bee).")]
        [SerializeField] string leftWingName = "BeeWingL";
        [Tooltip("Name of the right wing's pivot.")]
        [SerializeField] string rightWingName = "BeeWingR";
        [Tooltip("Wingbeats per second. Real bees beat at ~200 Hz; this is the read of a blur, not the real rate.")]
        [SerializeField, Min(0f)] float beatsPerSecond = 9f;
        [Tooltip("Half the stroke, in degrees, about each wing's root.")]
        [SerializeField, Range(0f, 80f)] float strokeDegrees = 38f;

        Transform _left, _right;
        Quaternion _leftRest, _rightRest;
        float _phase;

        void Awake()
        {
            _left = Find(transform, leftWingName);
            _right = Find(transform, rightWingName);
            if (_left) _leftRest = _left.localRotation;
            if (_right) _rightRest = _right.localRotation;
            _phase = (GetInstanceID() & 1023) * 0.0061f;
        }

        void Update()
        {
            if (!_left && !_right) return;
            float a = strokeDegrees * Mathf.Sin((Time.time * beatsPerSecond + _phase) * 6.2831853f);
            if (_left) _left.localRotation = _leftRest * Quaternion.Euler(0f, 0f, a);
            if (_right) _right.localRotation = _rightRest * Quaternion.Euler(0f, 0f, -a);
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
