using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The omni-crystal SURGE's sword ring (Garrett, 2026-10-26: "zoom out, re-center the camera on
    /// the rhino and surround it with swords. they should still be controlled by the triggers. this
    /// can be an 8 second state"). Owned by <see cref="ShieldSkimmerScaleDriver"/>, ticked from its
    /// LateUpdate, after the swipe executor has posed the blade for the frame.
    ///
    /// <para><b>The ring.</b> <see cref="ShieldSkimmerScaleConfigSO.SurgeRingSwordCount"/> − 1 copies of the
    /// blade, each one the blade's LIVE pose turned about the nose axis of the blade's parent (the frame
    /// the swipe pivots in), k × 360 / N degrees round. So the triggers swing every sword at once: the
    /// swipe spins the ring, the chop folds every sword in toward the nose like a closing flower, and a
    /// combo flourish plays on all of them. The ring fans out of the blade when the surge starts and
    /// folds back into it when the surge ends (<see cref="ShieldSkimmerScaleConfigSO.SurgeRingUnfurlSeconds"/>):
    /// a turn, never a size change - the blade does not resize.</para>
    ///
    /// <para><b>Each copy is a real blade.</b> It is a clone of the blade root (mesh, both trigger
    /// colliders, kinematic body, <see cref="SkimmerImpactor"/>, <see cref="ImpactCollider"/>, its own
    /// <see cref="SkimmerSwingKinematics"/>) with the parts that are the ONE sword's brain or look removed
    /// before it ever wakes (it is cloned under an inactive holder): <see cref="RhinoSwordFXController"/>
    /// would re-seat and tint the pilot's own tracers. Its <see cref="Skimmer"/> is initialised with the
    /// vessel, stands its own scale write down, and points <see cref="Skimmer.SwordState"/> at the real
    /// sword, so every copy cuts through the same effect chain, banks energy into the same meter, and
    /// pops super-shields while the blade is energized - which a surge holds it for. Each copy's own
    /// swing model gives its cuts the velocity of the part of THAT sword that struck.</para>
    ///
    /// <para><b>Every peer.</b> The surge bit replicates (<see cref="RhinoSwordNetworkRelay"/>) and the
    /// blade is posed from the replicated trigger mirrors, so every peer raises the same ring in the same
    /// pose and cuts against its own copy, exactly as it already does for the blade.</para>
    ///
    /// <para><b>The camera</b> is <see cref="RhinoSwordCamera"/>'s (the driver ticks it beside this).</para>
    /// </summary>
    public sealed class RhinoSurgeSwordRing
    {
        readonly Transform _blade;
        readonly IRhinoSwordState _sword;
        readonly List<Copy> _copies = new(15);
        readonly MaterialPropertyBlock _mpb = new();

        GameObject _holder;
        MeshRenderer _bladeBody;
        string _bodyPath;      // the blade body renderer's path under the blade root ("" = the root)
        float _open01;         // 0 = folded into the blade (hidden), 1 = fully fanned out
        bool _warned;

        struct Copy
        {
            public Transform Transform;
            public Skimmer Skimmer;
            public MeshRenderer Body;
        }

        public RhinoSurgeSwordRing(Transform blade, IRhinoSwordState sword)
        {
            _blade = blade;
            _sword = sword;
        }

        /// <summary>True while any copy is out (the surge, plus its fold-away).</summary>
        public bool IsOut => _open01 > 0f;

        /// <summary>
        /// The angle (degrees, about the nose axis) of copy <paramref name="index"/> (1..count-1) in a ring of
        /// <paramref name="count"/> swords fanned out <paramref name="open01"/> of the way. Eased, so the fan
        /// starts and stops softly; 0 for every copy while folded, so they all sit inside the blade. Pure.
        /// </summary>
        public static float CopyAngleDegrees(int index, int count, float open01)
        {
            if (count < 2) return 0f;
            float t = Mathf.Clamp01(open01);
            return index * (360f / count) * (t * t * (3f - 2f * t));
        }

        /// <summary>One frame. <paramref name="surging"/> is the replicated surge state.</summary>
        public void Tick(bool surging, ShieldSkimmerScaleConfigSO config, IVesselStatus status, MeshRenderer bladeBody, float dt)
        {
            if (!_blade || config == null) return;

            _open01 = Mathf.MoveTowards(_open01, surging ? 1f : 0f, dt / config.SurgeRingUnfurlSeconds);

            if (_open01 <= 0f)
            {
                if (_holder && _holder.activeSelf) _holder.SetActive(false);
                return;
            }

            int wanted = config.SurgeRingSwordCount - 1;
            if (!_holder || _copies.Count != wanted || bladeBody != _bladeBody)
            {
                if (!Build(wanted, status, bladeBody)) return;
            }

            if (!_holder.activeSelf)
            {
                // A vessel can change hands between surges (a swap, an AI taking over): hand each
                // copy the status the blade has NOW before it wakes.
                for (int i = 0; i < _copies.Count; i++) Arm(_copies[i].Skimmer, status);
                _holder.SetActive(true);
            }

            Pose(config.SurgeRingSwordCount);
        }

        /// <summary>Fold the ring away now (the driver's disable / pool path).</summary>
        public void Collapse()
        {
            _open01 = 0f;
            if (_holder) _holder.SetActive(false);
        }

        /// <summary>Destroy the copies (the driver's destroy path).</summary>
        public void Dispose()
        {
            if (_holder) Object.Destroy(_holder);
            _holder = null;
            _copies.Clear();
        }

        void Pose(int count)
        {
            var rot = _blade.localRotation;
            var pos = _blade.localPosition;
            var scale = _blade.localScale;

            if (_bladeBody) _bladeBody.GetPropertyBlock(_mpb);

            for (int i = 0; i < _copies.Count; i++)
            {
                var c = _copies[i];
                if (!c.Transform) continue;
                var turn = Quaternion.AngleAxis(CopyAngleDegrees(i + 1, count, _open01), Vector3.forward);
                c.Transform.localRotation = turn * rot;
                c.Transform.localPosition = turn * pos;
                c.Transform.localScale = scale;

                // The blade's live colour (heat, energized red, flashes), drawn on every copy.
                if (_bladeBody && c.Body) c.Body.SetPropertyBlock(_mpb);
            }
        }

        bool Build(int copies, IVesselStatus status, MeshRenderer bladeBody)
        {
            if (_holder) Object.Destroy(_holder);
            _copies.Clear();

            _bladeBody = bladeBody;
            _bodyPath = bladeBody ? PathUnder(_blade, bladeBody.transform) : null;

            // Cloned under an INACTIVE holder so nothing on a copy wakes before it is stripped: the FX
            // controller's OnEnable would otherwise re-seat the pilot's own blade tracers.
            _holder = new GameObject($"{_blade.name}::SurgeRing");
            _holder.SetActive(false);
            var holderTf = _holder.transform;
            holderTf.SetParent(_blade.parent, false);
            holderTf.localPosition = Vector3.zero;
            holderTf.localRotation = Quaternion.identity;
            holderTf.localScale = Vector3.one;
            _holder.layer = _blade.gameObject.layer;

            for (int i = 0; i < copies; i++)
            {
                var go = Object.Instantiate(_blade.gameObject, holderTf, false);
                go.name = $"{_blade.name}::Surge{i + 1}";
                Strip(go);

                if (!go.TryGetComponent(out Skimmer skimmer))
                {
                    if (!_warned)
                    {
                        _warned = true;
                        CSDebug.LogError($"[{nameof(RhinoSurgeSwordRing)}] The blade '{_blade.name}' carries no " +
                                         $"{nameof(Skimmer)}, so the surge's sword copies cannot cut. The ring is off.");
                    }
                    Object.Destroy(_holder);
                    _holder = null;
                    _copies.Clear();
                    return false;
                }

                Arm(skimmer, status);
                MeshRenderer body = null;
                if (_bodyPath != null)
                {
                    var bodyTf = _bodyPath.Length == 0 ? go.transform : go.transform.Find(_bodyPath);
                    if (bodyTf) bodyTf.TryGetComponent(out body);
                }
                _copies.Add(new Copy { Transform = go.transform, Skimmer = skimmer, Body = body });
            }
            return true;
        }

        void Arm(Skimmer skimmer, IVesselStatus status)
        {
            if (!skimmer) return;
            skimmer.HasExternalScaleDriver = true;   // no copy ever resizes itself (Space)
            skimmer.SwordState = _sword;             // cuts bank into, and read, the ONE sword
            if (status != null) skimmer.Initialize(status);
        }

        /// <summary>Remove what belongs to the one sword, not to a blade. Immediate, because the copy has
        /// not woken yet and must never wake with these on it.</summary>
        static void Strip(GameObject copy)
        {
            DestroyAll<RhinoSwordFXController>(copy);
            DestroyAll<ShieldSkimmerScaleDriver>(copy);
            DestroyAll<RhinoSwordAfterimage>(copy);
            DestroyAll<ShieldSwipeActionExecutor>(copy);
        }

        // Build time only (once per ring), so the array is fine.
        static void DestroyAll<T>(GameObject root) where T : Component
        {
            foreach (var c in root.GetComponentsInChildren<T>(true)) Object.DestroyImmediate(c);
        }

        static string PathUnder(Transform root, Transform t)
        {
            if (t == root) return string.Empty;
            string path = t.name;
            for (var p = t.parent; p && p != root; p = p.parent) path = p.name + "/" + path;
            return t.IsChildOf(root) ? path : null;
        }
    }
}
