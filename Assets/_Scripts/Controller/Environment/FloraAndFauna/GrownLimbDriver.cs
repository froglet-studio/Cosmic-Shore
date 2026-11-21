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
    /// <para><b>The turn</b> (Garrett, 2026-11-18: "its rotations just happen ... i want the wings to take on chiral
    /// movements that match their rotations"). The body's own angular velocity is measured here every frame and turned
    /// into four answers, three of them handed to the shader per creature (a MaterialPropertyBlock on its skins) and
    /// applied to the riders with the same formula: the OUTSIDE of a turn beats harder than the inside
    /// (<see cref="strokeSeconds"/>); in a roll the hind edges deflect like ailerons, one side up and the other down
    /// (<see cref="twistSeconds"/>); everything far from the heart lags the rotation and springs back
    /// (<see cref="lagSeconds"/>); and the whole body banks into the turn (<see cref="bankSeconds"/>, a roll of the
    /// body under the root, so the heart and the creature's own heading are untouched). All four are zero for a body
    /// that is not turning, and the two sides of the body always do mirrored things.</para>
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

        [Tooltip("Where each rider sits in the BODY's own space at rest (the shader's position for it): the turn's terms are functions of it.")]
        [SerializeField] Vector3[] riderHome;

        [Header("The turn: how the body answers its own rotation (authored by unity_port.py from the spec's turn block)")]
        [Tooltip("The skins that draw the body. Each is handed the turn's numbers every frame.")]
        [SerializeField] Renderer[] skins;

        [Tooltip("The transform that banks: the body under the root, so the heart and the heading do not roll with it.")]
        [SerializeField] Transform bankRoot;

        [Tooltip("The body's reach: how far most of it lies from the heart (its wingtips), in its own units. The turn's angles are AT this distance; a tail longer than it lags by more.")]
        [Min(0.01f)] [SerializeField] float reach = 30f;

        [Tooltip("LAG: how far behind the rotation the wingtips trail, in seconds of turning. 0 = a rigid body.")]
        [Min(0f)] [SerializeField] float lagSeconds = 0.14f;

        [Tooltip("The most the tips ever trail, in radians.")]
        [Min(0f)] [SerializeField] float maxLag = 0.45f;

        [Tooltip("How fast the lag springs back, in cycles a second. Lower is heavier.")]
        [Min(0.05f)] [SerializeField] float springHz = 1.3f;

        [Tooltip("How quickly the spring settles: 1 never overshoots, 0.4 swings past once and settles.")]
        [Range(0.05f, 1.5f)] [SerializeField] float damping = 0.45f;

        [Tooltip("STROKE: how much harder the outside of a turn beats than the inside, per radian a second of turning.")]
        [Min(0f)] [SerializeField] float strokeSeconds = 0.3f;

        [Tooltip("The most one side's beat is ever raised (and the other's lowered): 0.75 = 1.75 x and 0.25 x.")]
        [Range(0f, 1f)] [SerializeField] float maxStroke = 0.75f;

        [Tooltip("TWIST: how far the hind edges deflect in a roll, in radians at the wingtip per radian a second of roll.")]
        [Min(0f)] [SerializeField] float twistSeconds = 0.12f;

        [Tooltip("The most the hind edges ever deflect, in radians at the wingtip.")]
        [Min(0f)] [SerializeField] float maxTwist = 0.35f;

        [Tooltip("BANK: how far the body rolls into a turn, in seconds of turning (0.45 = 26 degrees at one radian a second).")]
        [Min(0f)] [SerializeField] float bankSeconds = 0.45f;

        [Tooltip("The most it ever banks, in degrees.")]
        [Range(0f, 80f)] [SerializeField] float maxBankDegrees = 38f;

        static readonly int BendXId = Shader.PropertyToID("_GrownBendX");
        static readonly int BendYId = Shader.PropertyToID("_GrownBendY");
        static readonly int BendZId = Shader.PropertyToID("_GrownBendZ");
        static readonly int StrokeXId = Shader.PropertyToID("_GrownStrokeX");
        static readonly int StrokeYId = Shader.PropertyToID("_GrownStrokeY");
        static readonly int TwistId = Shader.PropertyToID("_GrownTwist");
        static MaterialPropertyBlock s_block;

        Quaternion[] _rest;
        Vector3[] _seat;
        Quaternion _bankRest = Quaternion.identity;
        Quaternion _lastRotation;
        Vector3 _omega, _lag, _lagVelocity;
        Vector2 _stroke;
        float _twist, _bank;
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
            if (bankRoot) _bankRest = bankRoot.localRotation;
        }

        void OnEnable()
        {
            _lastPosition = transform.position;
            _lastRotation = transform.rotation;
            _omega = _lag = _lagVelocity = Vector3.zero;
            _stroke = Vector2.zero;
            _twist = _bank = 0f;
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
            AnswerTheTurn(dt, effort);
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
            bool turning = riderHome != null && riderHome.Length >= count;
            float inv = 1f / reach;
            Vector3 lag = _lag * inv;
            for (int i = 0; i < count; i++)
            {
                var rider = riders[i];
                if (!rider) continue;
                Vector3 offset = riderSin[i] * sin + riderCos[i] * cos;
                if (turning)
                {
                    // SpindleGrownSway.hlsl, line for line, at the rider's place in the body
                    Vector3 p = riderHome[i];
                    offset *= Mathf.Max(0f, 1f + (_stroke.x * p.x + _stroke.y * p.y) * inv);
                    offset += Vector3.Cross(lag, p) * p.magnitude;
                    offset += Vector3.Cross(new Vector3(_twist * inv * p.x, 0f, 0f), p);
                }
                rider.localPosition = _seat[i] + offset;
            }
        }

        /// <summary>
        /// Measures how the body is rotating, in its own frame, and turns it into the four answers. Sign notes, in the
        /// body's frame (z ahead, x to one side): a turn toward +x is a positive y rate, so the INSIDE of that turn is
        /// +x and the stroke on +x drops; a roll that takes +x down is a negative z rate, and +x's hind edge lifts.
        /// </summary>
        void AnswerTheTurn(float dt, float effort)
        {
            var rotation = transform.rotation;
            var step = rotation * Quaternion.Inverse(_lastRotation);
            _lastRotation = rotation;
            step.ToAngleAxis(out float degrees, out Vector3 axis);
            if (degrees > 180f) degrees -= 360f;
            Vector3 omega = Vector3.zero;
            if (!float.IsNaN(axis.x) && !float.IsInfinity(axis.x) && Mathf.Abs(degrees) > 1e-4f)
                omega = transform.InverseTransformDirection(axis) * (degrees * Mathf.Deg2Rad / dt);
            // a teleport or a snap is not a turn
            if (omega.sqrMagnitude > 36f) omega = Vector3.zero;
            _omega = Vector3.Lerp(_omega, omega, 1f - Mathf.Exp(-dt * 10f));

            // the lag is a spring toward "this far behind the rotation": it trails, overshoots a little and settles
            float h = Mathf.Min(dt, 1f / 30f);
            float w = springHz * (Mathf.PI * 2f);
            Vector3 target = Vector3.ClampMagnitude(-lagSeconds * _omega, maxLag);
            _lagVelocity += (w * w * (target - _lag) - 2f * damping * w * _lagVelocity) * h;
            _lag = Vector3.ClampMagnitude(_lag + _lagVelocity * h, maxLag * 1.5f);

            _stroke = Vector2.ClampMagnitude(new Vector2(-_omega.y, _omega.x) * strokeSeconds, maxStroke);
            _twist = Mathf.Clamp(-twistSeconds * _omega.z, -maxTwist, maxTwist);

            float wantBank = Mathf.Clamp(-bankSeconds * _omega.y * Mathf.Rad2Deg * effort, -maxBankDegrees, maxBankDegrees);
            _bank = Mathf.Lerp(_bank, wantBank, 1f - Mathf.Exp(-dt * 4f));
            if (bankRoot) bankRoot.localRotation = _bankRest * Quaternion.AngleAxis(_bank, Vector3.forward);

            if (skins == null || skins.Length == 0) return;
            float inv = 1f / reach;
            s_block ??= new MaterialPropertyBlock();
            for (int i = 0; i < skins.Length; i++)
            {
                var skin = skins[i];
                if (!skin) continue;
                // keep whatever else is stamped on this renderer (the spindle's own fade)
                skin.GetPropertyBlock(s_block);
                s_block.SetFloat(BendXId, _lag.x * inv);
                s_block.SetFloat(BendYId, _lag.y * inv);
                s_block.SetFloat(BendZId, _lag.z * inv);
                s_block.SetFloat(StrokeXId, _stroke.x * inv);
                s_block.SetFloat(StrokeYId, _stroke.y * inv);
                s_block.SetFloat(TwistId, _twist * inv);
                skin.SetPropertyBlock(s_block);
            }
        }
    }
}
