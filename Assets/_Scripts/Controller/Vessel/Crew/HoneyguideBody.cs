using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Honeyguide hull's own body (model <c>Honeyguide.fbx</c>, built by <c>Tools/Build/vessel_model/specs/honeyguide.py</c>;
    /// Garrett, 2026-10-21: "get everything including models"). Lives on the Honeyguide root.
    ///
    /// <para>The model is the vessel's drawn geometry (it is in <see cref="VesselCustomization"/>'s ship geometries, so the
    /// domain paint and every other reader treat it as the hull), and the Sparrow's mesh it replaced is switched off. It is
    /// puppeted from the hull's MEASURED motion, so it reads the same on every peer: the wings bank into a turn, beat
    /// slowly below cruise and rise to brake when the hull slows hard, the tail fan steers out of a turn and lifts with the
    /// pitch, and the Ratel's hex turret turns a sixth of a turn each time the Dynamo spins (on the machine that seats the
    /// crew; elsewhere it holds still). Its element morphs need nothing here: the hull's <see cref="VesselAnimation"/>
    /// drives every element-labelled shape key on every skinned mesh under the root, this model's included.</para>
    ///
    /// <para>Bones are found BY NAME (vessel-model contract: FBX sub-object ids are not derivable offline); a missing model
    /// leaves this component idle. It writes bone <c>localRotation</c> only, over the rest pose it captured; the morphs
    /// write vertices only.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HoneyguideBody : MonoBehaviour
    {
        [Header("Model (found by name under this hull)")]
        [Tooltip("Name of the Honeyguide's model root (Honeyguide.fbx nested under the hull).")]
        [SerializeField] string modelName = "Honeyguide";

        [Header("Flight")]
        [Tooltip("The hull's cruise speed (u/s): the wing beat fades out and the tail settles as the hull reaches it.")]
        [SerializeField, Min(1f)] float cruiseSpeed = 60f;
        [Tooltip("Yaw rate (degrees per second) that banks the wings and steers the tail all the way.")]
        [SerializeField, Min(1f)] float fullYawRate = 110f;
        [Tooltip("Pitch rate (degrees per second) that lifts or drops the tail fan all the way.")]
        [SerializeField, Min(1f)] float fullPitchRate = 90f;
        [Tooltip("Slowing (u/s per second) that raises the wings into a full flare.")]
        [SerializeField, Min(1f)] float fullFlareDeceleration = 120f;

        [Header("Wings")]
        [Tooltip("Degrees both wings roll into a turn at the full yaw rate.")]
        [SerializeField] float wingBankDegrees = 22f;
        [Tooltip("Degrees the wing tips rise in a full flare.")]
        [SerializeField] float wingFlareDegrees = 30f;
        [Tooltip("Half-amplitude (degrees) of the wing beat at a standstill (none at cruise).")]
        [SerializeField] float wingBeatDegrees = 10f;
        [Tooltip("Wing beats per second.")]
        [SerializeField, Min(0f)] float wingBeatHz = 1.6f;

        [Header("Tail and turret")]
        [Tooltip("Degrees the tail fan steers out of a turn at the full yaw rate.")]
        [SerializeField] float tailSteerDegrees = 14f;
        [Tooltip("Degrees the tail fan lifts at the full pitch rate.")]
        [SerializeField] float tailLiftDegrees = 16f;
        [Tooltip("Degrees the Ratel's turret turns for each Dynamo spin (a hex: 60).")]
        [SerializeField] float turretStepDegrees = 60f;
        [Tooltip("How fast the turret and every other pose close on their targets (per second).")]
        [SerializeField, Min(0.1f)] float smoothing = 8f;

        Transform _model;
        Bone _wingL, _wingR, _tail, _ratel;
        HoneyguideCrew _crew;
        Vector3 _prevPosition, _prevForward;
        bool _prevValid;
        float _speed, _decel, _yawRate, _pitchRate, _beatPhase, _turret, _turretTarget, _lastSpinAt = -99f;
        float _bank, _flare, _steer, _lift;

        struct Bone
        {
            public Transform T;
            public Quaternion Rest;
            public Vector3 Up, Right, Forward;    // the hull's axes, in the bone's local frame at rest

            public static Bone Find(Transform root, string name, Quaternion bodyFrame)
            {
                var t = FindDeep(root, name);
                if (!t) return default;
                var toLocal = Quaternion.Inverse(t.rotation);
                return new Bone
                {
                    T = t, Rest = t.localRotation,
                    Up = toLocal * (bodyFrame * Vector3.up),
                    Right = toLocal * (bodyFrame * Vector3.right),
                    Forward = toLocal * (bodyFrame * Vector3.forward),
                };
            }

            /// <summary>Rest, then yaw (about the hull's up), pitch (about its right) and roll (about its nose), degrees.</summary>
            public void Pose(float yaw, float pitch, float roll)
            {
                if (!T) return;
                T.localRotation = Rest * Quaternion.AngleAxis(yaw, Up) * Quaternion.AngleAxis(pitch, Right)
                                       * Quaternion.AngleAxis(roll, Forward);
            }
        }

        void Awake()
        {
            _model = FindDeep(transform, modelName);
            if (!_model) return;
            TryGetComponent(out _crew);
            // The model flies with the hull: its body frame IS the hull's (nose +Z, back +Y).
            var bodyFrame = transform.rotation;
            _wingL = Bone.Find(_model, "wing.L", bodyFrame);
            _wingR = Bone.Find(_model, "wing.R", bodyFrame);
            _tail = Bone.Find(_model, "tail", bodyFrame);
            _ratel = Bone.Find(_model, "ratel", bodyFrame);
            // The boost and body flares light VesselAnimation's renderer: hand it the drawn mesh, not the hidden Sparrow's
            var anim = GetComponentInChildren<VesselAnimation>(true);
            var drawn = _model.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (anim && drawn) anim.SkinnedMeshRenderer = drawn;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (!_model || dt <= 0f) return;

            var pos = transform.position;
            var fwd = transform.forward;
            if (_prevValid)
            {
                float a = 1f - Mathf.Exp(-6f * dt);
                float speed = (pos - _prevPosition).magnitude / dt;
                _decel = Mathf.Lerp(_decel, Mathf.Max(0f, (_speed - speed) / dt), a);
                _speed = Mathf.Lerp(_speed, speed, a);
                _yawRate = Mathf.Lerp(_yawRate, Vector3.SignedAngle(Vector3.ProjectOnPlane(_prevForward, transform.up), fwd, transform.up) / dt, a);
                _pitchRate = Mathf.Lerp(_pitchRate, Vector3.SignedAngle(Vector3.ProjectOnPlane(_prevForward, transform.right), fwd, transform.right) / dt, a);
            }
            _prevPosition = pos;
            _prevForward = fwd;
            _prevValid = true;

            float cruise01 = Mathf.Clamp01(_speed / cruiseSpeed);
            float yaw01 = Mathf.Clamp(_yawRate / fullYawRate, -1f, 1f);
            float pitch01 = Mathf.Clamp(_pitchRate / fullPitchRate, -1f, 1f);
            float k = 1f - Mathf.Exp(-smoothing * dt);
            _bank = Mathf.Lerp(_bank, yaw01 * wingBankDegrees, k);
            _flare = Mathf.Lerp(_flare, Mathf.Clamp01(_decel / fullFlareDeceleration) * wingFlareDegrees, k);
            _steer = Mathf.Lerp(_steer, -yaw01 * tailSteerDegrees, k);
            _lift = Mathf.Lerp(_lift, pitch01 * tailLiftDegrees, k);

            _beatPhase = Mathf.Repeat(_beatPhase + wingBeatHz * dt, 1f);
            float beat = Mathf.Sin(_beatPhase * 2f * Mathf.PI) * wingBeatDegrees * (1f - cruise01);

            // a turn rolls both wings the same way; a flare and the beat raise both tips (opposite rolls: +roll lifts +X)
            _wingL.Pose(0f, 0f, -_bank - _flare - beat);
            _wingR.Pose(0f, 0f, -_bank + _flare + beat);
            _tail.Pose(_steer, -_lift, 0f);

            if (_crew && _crew.SpinAt > _lastSpinAt)
            {
                _turretTarget += turretStepDegrees;
                _lastSpinAt = _crew.SpinAt;
            }
            if (_turretTarget >= 360f) { _turretTarget -= 360f; _turret -= 360f; }
            _turret = Mathf.Lerp(_turret, _turretTarget, k);
            _ratel.Pose(_turret, 0f, 0f);
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var hit = FindDeep(root.GetChild(i), name);
                if (hit) return hit;
            }
            return null;
        }
    }
}
