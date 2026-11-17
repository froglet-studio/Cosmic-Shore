using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The slow beat of a GROWN creature's limbs (the Ray's wings and whip): each joint turns about its own axis, back
    /// and forth, a little behind the joint before it, so a wave runs out along the limb. It is the CPU tier of
    /// creature motion (the fauna skill, section 3): it writes <c>localRotation</c> on plain joint transforms and
    /// never a position, so the prisms and skins parented under a joint ride it at no collider or index cost.
    ///
    /// <para>The joints, axes, sweeps and phases are authored on the prefab by <c>Tools/Build/fauna_kit/unity_port.py</c>
    /// from the creature's spec (its <c>unity</c> block), never typed here. The beat quickens and deepens with the
    /// creature's own speed, measured from its transform, so a drifting creature idles and a fleeing one works.</para>
    ///
    /// <para>Nothing here is authoritative and nothing replicates: every peer beats its own copy. An attack
    /// (<see cref="FaunaAttackDriver"/>) writes the same joints in LateUpdate and wins while it plays.</para>
    /// </summary>
    [DefaultExecutionOrder(-1)]
    public sealed class GrownLimbDriver : MonoBehaviour
    {
        [Header("Joints (parallel lists, authored by unity_port.py)")]
        [Tooltip("The joint transforms, root to tip along each limb.")]
        [SerializeField] Transform[] joints;

        [Tooltip("Each joint's turning axis, in its own local space.")]
        [SerializeField] Vector3[] axes;

        [Tooltip("Each joint's sweep either side of its rest pose at full beat, in degrees. Negative mirrors it.")]
        [SerializeField] float[] degrees;

        [Tooltip("How far behind the beat each joint runs, in radians. A joint further out lags more: that is the wave.")]
        [SerializeField] float[] phases;

        [Header("Cadence")]
        [Tooltip("Beats per second while drifting.")]
        [Min(0f)] [SerializeField] float idleHz = 0.38f;

        [Tooltip("Beats per second at and above 'Speed For Full Beat'.")]
        [Min(0f)] [SerializeField] float fastHz = 0.9f;

        [Tooltip("The share of the full sweep used while drifting.")]
        [Range(0f, 1f)] [SerializeField] float idleSweepShare = 0.6f;

        [Tooltip("The creature's speed (u/s) at which the beat reaches its fast cadence and full sweep.")]
        [Min(0.01f)] [SerializeField] float speedForFullBeat = 40f;

        Quaternion[] _rest;
        Vector3 _lastPosition;
        float _clock;

        void Awake()
        {
            int n = joints != null ? joints.Length : 0;
            _rest = new Quaternion[n];
            for (int i = 0; i < n; i++)
                _rest[i] = joints[i] ? joints[i].localRotation : Quaternion.identity;
        }

        void OnEnable()
        {
            _lastPosition = transform.position;
            // a pooled body starts its next life from rest, and each individual on its own beat
            _clock = (transform.position.x + transform.position.z) * 0.37f;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || _rest == null) return;

            Vector3 position = transform.position;
            float speed = (position - _lastPosition).magnitude / dt;
            _lastPosition = position;

            float effort = Mathf.Clamp01(speed / speedForFullBeat);
            _clock += dt * Mathf.Lerp(idleHz, fastHz, effort) * (Mathf.PI * 2f);
            float sweep = Mathf.Lerp(idleSweepShare, 1f, effort);

            int n = Mathf.Min(_rest.Length, Mathf.Min(axes.Length, Mathf.Min(degrees.Length, phases.Length)));
            for (int i = 0; i < n; i++)
            {
                var joint = joints[i];
                if (!joint) continue;
                float angle = degrees[i] * sweep * Mathf.Sin(_clock - phases[i]);
                joint.localRotation = _rest[i] * Quaternion.AngleAxis(angle, axes[i]);
            }
        }
    }
}
