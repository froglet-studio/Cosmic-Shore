using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// What the CPU owes a GROWN creature's motion (the Ray and its kin, <c>Tools/Build/fauna_kit</c>).
    ///
    /// <para><b>Riders.</b> A grown body's wave runs in the spindle shader (<c>SpindleGrownSway.hlsl</c>): every mesh
    /// vertex carries the sine and cosine parts of its own wave, and the vertex stage adds them from the platform
    /// clock. A prism is a collider and must be where it is drawn, so each body prism (a rider) is carried along the
    /// same wave here, from the same clock and the same per-spindle phase: its local position is its seat plus
    /// <c>riderSin * sin(t) + riderCos * cos(t)</c>, the wave of the limb where it sits.</para>
    ///
    /// <para><b>Joints</b> (the fallback, a spec that says <c>"sway": "joints"</c>). Each joint turns about its own
    /// axis, back and forth, a little behind the joint before it, so a wave runs out along the limb; the beat quickens
    /// and deepens with the creature's own speed. With the wave in the shader the joint lists are empty and the
    /// joints are left to the attack (<see cref="FaunaAttackDriver"/>).</para>
    ///
    /// <para>Everything is authored on the prefab by <c>Tools/Build/fauna_kit/unity_port.py</c> from the creature's
    /// spec, never typed here. Nothing is authoritative and nothing replicates: every peer moves its own copy.</para>
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

        [Header("Riders on the shader's wave (parallel lists, authored by unity_port.py)")]
        [Tooltip("The body prisms that ride the wave of the limb they sit on.")]
        [SerializeField] Transform[] riders;

        [Tooltip("The sine part of each rider's wave, in its parent's space and units.")]
        [SerializeField] Vector3[] riderSin;

        [Tooltip("The cosine part of each rider's wave.")]
        [SerializeField] Vector3[] riderCos;

        [Tooltip("Waves per second: the material's _GrownFrequency over 2 pi. 0 leaves the riders at their seats.")]
        [Min(0f)] [SerializeField] float waveHz;

        Quaternion[] _rest;
        Vector3[] _seat;
        Spindle _spindle;
        float _wavePhase;
        bool _wavePhaseKnown;
        Vector3 _lastPosition;
        float _clock;

        void Awake()
        {
            int n = joints != null ? joints.Length : 0;
            _rest = new Quaternion[n];
            for (int i = 0; i < n; i++)
                _rest[i] = joints[i] ? joints[i].localRotation : Quaternion.identity;

            int r = riders != null ? riders.Length : 0;
            _seat = new Vector3[r];
            for (int i = 0; i < r; i++)
                _seat[i] = riders[i] ? riders[i].localPosition : Vector3.zero;
            _spindle = GetComponentInChildren<Spindle>(true);
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

            CarryRiders();
        }

        /// <summary>Each rider to its seat plus the wave there: the shader's own formula, clock and phase.</summary>
        void CarryRiders()
        {
            if (waveHz <= 0f || _seat == null || _seat.Length == 0) return;
            // the phase is the bucket the spindle's material variant was minted from; it is known once the spindle starts
            if (!_wavePhaseKnown && _spindle) _wavePhaseKnown = _spindle.TryGetSwayPhase(out _wavePhase);
            if (!_wavePhaseKnown) return;

            float t = PrismClock.Now * waveHz * (Mathf.PI * 2f) + _wavePhase;
            float sin = Mathf.Sin(t), cos = Mathf.Cos(t);
            int count = Mathf.Min(_seat.Length, Mathf.Min(riderSin.Length, riderCos.Length));
            for (int i = 0; i < count; i++)
            {
                var rider = riders[i];
                if (rider) rider.localPosition = _seat[i] + riderSin[i] * sin + riderCos[i] * cos;
            }
        }
    }
}
