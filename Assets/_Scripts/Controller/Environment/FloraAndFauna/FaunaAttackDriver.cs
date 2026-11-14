using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A creature's ATTACK animation, for every species that has none of its own (Garrett, 2026-11-14: "right trigger should
    /// animate the fauna with an attack animation against vessels (all the brittlestar's legs whip out). Fauna missing this
    /// should have this and use it against vessels when attacking an ark"). Added on demand by
    /// <see cref="Fauna.PlayPossessionAttack"/>, so no prefab carries it and no species is left out.
    ///
    /// <para><b>Limbs</b>: a body with a rig - a hub joint whose children are chains of joints (the Brittlestar's five arms
    /// A-E, joints 1-5) - whips every chain: its root joint swings toward the target and a wave runs down the chain and
    /// out. <b>No rig</b>: the body lunges - its model punches forward along the attack and swells, then settles.</para>
    ///
    /// <para><b>Presentation only.</b> It decides nothing: whoever plays it decides what the attack hits (the Jewel Wasp's
    /// scored hit, <c>JewelWaspExecutor</c>; the Ark escort's wild strikes, <c>ArkEscortRun.Chaos</c>). It writes joint
    /// rotations in LateUpdate, after any Animator, from the pose captured when the attack began, and puts that pose back
    /// when it ends, so a rig nothing animates is left exactly as it was found.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FaunaAttackDriver : MonoBehaviour
    {
        [Tooltip("How far a limb's root joint swings toward the target at the height of the whip (degrees).")]
        [SerializeField, Range(0f, 120f)] float limbSwingDegrees = 65f;
        [Tooltip("The lash: how far each joint down a limb bends as the wave passes it (degrees).")]
        [SerializeField, Range(0f, 90f)] float lashDegrees = 28f;
        [Tooltip("Waves that run down a limb in one attack.")]
        [SerializeField, Range(0.5f, 4f)] float lashWaves = 1.5f;
        [Tooltip("No rig: how far the body's model punches toward the target, as a share of its size.")]
        [SerializeField, Range(0f, 2f)] float lungeShare = 0.6f;
        [Tooltip("No rig: how much the model swells at the height of the lunge (1 = not at all).")]
        [SerializeField, Range(1f, 2f)] float lungeSwell = 1.25f;

        sealed class Limb
        {
            public Transform[] Joints;
            public Quaternion[] Rest;
        }

        readonly List<Limb> _limbs = new();
        Transform _model;
        Vector3 _modelRestPos, _modelRestScale;
        bool _scanned, _playing;
        float _start, _seconds;
        Vector3 _target;

        /// <summary>True while an attack plays.</summary>
        public bool Playing => _playing;

        /// <summary>How far through the attack it is (0..1), or -1 when none plays.</summary>
        public float Progress => _playing ? Mathf.Clamp01((Time.time - _start) / _seconds) : -1f;

        /// <summary>Strike toward <paramref name="target"/> (world) over <paramref name="seconds"/>. A new attack while one
        /// plays starts over from the pose the first one captured.</summary>
        public void Play(Vector3 target, float seconds)
        {
            if (!_scanned) Scan();
            if (!_playing) CaptureRest();
            _target = target;
            _seconds = Mathf.Max(0.1f, seconds);
            _start = Time.time;
            _playing = true;
        }

        /// <summary>The height of the strike: the attack reaches furthest at this share of its length.</summary>
        public const float StrikeAt = 0.35f;

        void OnDisable() => Stop();

        void Stop()
        {
            if (!_playing) return;
            _playing = false;
            RestorePose();
        }

        void LateUpdate()
        {
            if (!_playing) return;
            float t = (Time.time - _start) / _seconds;
            if (t >= 1f)
            {
                Stop();
                return;
            }
            // out fast, back slow: the whip peaks at StrikeAt and eases home
            float reach = t < StrikeAt ? Mathf.SmoothStep(0f, 1f, t / StrikeAt)
                                       : Mathf.SmoothStep(1f, 0f, (t - StrikeAt) / (1f - StrikeAt));
            if (_limbs.Count > 0) WhipLimbs(t, reach);
            else Lunge(reach);
        }

        void WhipLimbs(float t, float reach)
        {
            Vector3 centre = transform.position;
            for (int l = 0; l < _limbs.Count; l++)
            {
                var limb = _limbs[l];
                var joints = limb.Joints;
                if (joints[0] == null) continue;
                for (int j = 0; j < joints.Length; j++)
                    if (joints[j]) joints[j].localRotation = limb.Rest[j];

                // the root joint swings its limb toward the target
                Transform root = joints[0];
                Transform tip = joints[joints.Length - 1];
                Vector3 along = tip.position - root.position;
                if (along.sqrMagnitude < 1e-6f) along = root.position - centre;
                Vector3 toTarget = _target - root.position;
                if (along.sqrMagnitude > 1e-6f && toTarget.sqrMagnitude > 1e-6f)
                {
                    var full = Quaternion.FromToRotation(along, toTarget);
                    full.ToAngleAxis(out float angle, out Vector3 axis);
                    if (angle > 180f) angle -= 360f;
                    float swing = Mathf.Clamp(angle, -limbSwingDegrees, limbSwingDegrees) * reach;
                    if (axis.sqrMagnitude > 1e-6f) root.rotation = Quaternion.AngleAxis(swing, axis) * root.rotation;
                }

                // the lash: a wave runs from the root to the tip
                Vector3 bendAxis = Vector3.Cross(along, toTarget);
                if (bendAxis.sqrMagnitude < 1e-6f) bendAxis = transform.right;
                bendAxis.Normalize();
                for (int j = 1; j < joints.Length; j++)
                {
                    if (!joints[j]) continue;
                    float s = (float)j / joints.Length;
                    float wave = Mathf.Sin((t * lashWaves - s * 0.5f) * Mathf.PI * 2f);
                    float bend = lashDegrees * wave * reach * (0.4f + 0.6f * s);
                    joints[j].rotation = Quaternion.AngleAxis(bend, bendAxis) * joints[j].rotation;
                }
            }
        }

        void Lunge(float reach)
        {
            if (!_model) return;
            Vector3 dir = _target - transform.position;
            float size = Mathf.Max(1f, transform.lossyScale.magnitude);
            Vector3 localDir = dir.sqrMagnitude > 1e-6f ? _model.parent.InverseTransformDirection(dir.normalized) : Vector3.forward;
            _model.localPosition = _modelRestPos + localDir * (lungeShare * reach * size / Mathf.Max(0.01f, _model.parent.lossyScale.magnitude));
            _model.localScale = _modelRestScale * Mathf.Lerp(1f, lungeSwell, reach);
        }

        void CaptureRest()
        {
            for (int l = 0; l < _limbs.Count; l++)
            {
                var limb = _limbs[l];
                for (int j = 0; j < limb.Joints.Length; j++)
                    limb.Rest[j] = limb.Joints[j] ? limb.Joints[j].localRotation : Quaternion.identity;
            }
            if (_model)
            {
                _modelRestPos = _model.localPosition;
                _modelRestScale = _model.localScale;
            }
        }

        void RestorePose()
        {
            for (int l = 0; l < _limbs.Count; l++)
            {
                var limb = _limbs[l];
                for (int j = 0; j < limb.Joints.Length; j++)
                    if (limb.Joints[j]) limb.Joints[j].localRotation = limb.Rest[j];
            }
            if (_model)
            {
                _model.localPosition = _modelRestPos;
                _model.localScale = _modelRestScale;
            }
        }

        static readonly List<Transform> s_chain = new();

        /// <summary>
        /// Find the limbs: the HUB is the joint with the most children that are chains (a child with a child), and each such
        /// child, followed down its first child, is one limb. A body with fewer than three chains has no limbs to whip: it
        /// lunges with its first renderer's transform as the model.
        /// </summary>
        void Scan()
        {
            _scanned = true;
            _limbs.Clear();
            Transform hub = null;
            int bestChains = 0;
            var all = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                var t = all[i];
                if (t == transform) continue;
                int chains = 0;
                for (int c = 0; c < t.childCount; c++)
                    if (t.GetChild(c).childCount > 0 && !t.GetChild(c).GetComponent<Renderer>()) chains++;
                if (chains > bestChains) { bestChains = chains; hub = t; }
            }
            if (hub && bestChains >= 3)
            {
                for (int c = 0; c < hub.childCount; c++)
                {
                    var start = hub.GetChild(c);
                    if (start.childCount == 0 || start.GetComponent<Renderer>()) continue;
                    s_chain.Clear();
                    for (var j = start; j; j = j.childCount > 0 ? j.GetChild(0) : null)
                    {
                        s_chain.Add(j);
                        if (s_chain.Count >= 12) break;
                    }
                    if (s_chain.Count < 2) continue;
                    _limbs.Add(new Limb { Joints = s_chain.ToArray(), Rest = new Quaternion[s_chain.Count] });
                }
                s_chain.Clear();
                if (_limbs.Count > 0) return;
            }
            var r = GetComponentInChildren<Renderer>();
            _model = r && r.transform != transform ? r.transform : (transform.childCount > 0 ? transform.GetChild(0) : null);
            if (_model && !_model.parent) _model = null;
        }
    }
}
