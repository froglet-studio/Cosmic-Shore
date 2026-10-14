using System;
using System.Collections.Generic;

namespace CosmicShore.Engine
{
    /// <summary>
    /// One animated property of a clip: the object at <see cref="Path"/> (relative to the
    /// Animator, "" = its own GameObject), the component of serialized class
    /// <see cref="ClassId"/> (114 = a MonoBehaviour, identified by <see cref="ScriptGuid"/>),
    /// and the serialized <see cref="Attribute"/> ("m_Alpha", "m_LocalScale.x", "m_Color.a").
    /// </summary>
    public sealed class ClipBinding
    {
        public string Path = "";
        public int ClassId;
        public string Attribute = "";
        public string ScriptGuid;
        public AnimationCurve Curve;

        /// <summary>Identity of the animated property (the key write-defaults and blending share).</summary>
        public string Key => _key ??= $"{Path}|{ClassId}|{ScriptGuid}|{Attribute}";
        string _key;
    }

    public enum AnimatorConditionMode { If = 1, IfNot = 2, Greater = 3, Less = 4, Equals = 6, NotEqual = 7 }

    public sealed class AnimatorCondition
    {
        public AnimatorConditionMode Mode;
        public string Parameter;
        public float Threshold;
    }

    public sealed class AnimatorTransitionData
    {
        /// <summary>Index of the destination state in its layer (-1 = exit / unsupported).</summary>
        public int Destination = -1;
        public bool HasExitTime;
        public float ExitTime;
        public float Duration;
        public bool HasFixedDuration;
        public float Offset;
        public bool Mute;
        public readonly List<AnimatorCondition> Conditions = new();
    }

    public sealed class AnimatorStateData
    {
        public string Name = "";
        public int NameHash;
        public int FullPathHash;
        public int TagHash;
        public AnimationClip Clip;
        /// <summary>A blend-tree motion (used instead of <see cref="Clip"/> when set).</summary>
        public BlendTree Tree;
        /// <summary>"Motion Time" parameter: when set, the state's normalized time IS this float parameter.</summary>
        public string TimeParameter;
        public float Speed = 1f;
        public string SpeedParameter;
        public bool WriteDefaults = true;
        public readonly List<AnimatorTransitionData> Transitions = new();
    }

    /// <summary>UnityEditor.Animations.BlendTreeType, by serialized value.</summary>
    public enum BlendTreeType { Simple1D = 0, SimpleDirectional2D = 1, FreeformDirectional2D = 2, FreeformCartesian2D = 3, Direct = 4 }

    public sealed class BlendTreeChild
    {
        public AnimationClip Clip;
        public BlendTree Tree;
        public float Threshold;
        public Vector2 Position;
        public float TimeScale = 1f;
        public string DirectParameter;
    }

    /// <summary>A blend-tree motion: children (clips or nested trees) weighted by one or two float parameters.</summary>
    public sealed class BlendTree
    {
        public string Name = "BlendTree";
        public BlendTreeType Type;
        public string ParameterX = "", ParameterY = "";
        public bool NormalizedBlendValues;
        public readonly List<BlendTreeChild> Children = new();
    }

    public sealed class AnimatorLayerData
    {
        public string Name = "Base Layer";
        public float DefaultWeight = 1f;
        public int DefaultState;
        public readonly List<AnimatorStateData> States = new();
        public readonly List<AnimatorTransitionData> AnyStateTransitions = new();
    }

    /// <summary>
    /// An AnimatorController asset: its layers, states, transitions and parameters. Built by the
    /// content importer from the .controller YAML; played by <see cref="Animator"/>.
    /// </summary>
    public class AnimatorController : RuntimeAnimatorController
    {
        public readonly List<AnimatorLayerData> Layers = new();
        public AnimatorControllerParameter[] Parameters = Array.Empty<AnimatorControllerParameter>();
    }

    public partial class AnimationClip
    {
        /// <summary>Every float curve the clip drives (vector curves are split into .x/.y/.z/.w).</summary>
        public readonly List<ClipBinding> Bindings = new();

        Dictionary<string, AnimationCurve> _byKey;
        int _byKeyCount = -1;

        /// <summary>The curve bound to <paramref name="key"/> (a <see cref="ClipBinding.Key"/>), or null.</summary>
        public AnimationCurve CurveFor(string key)
        {
            if (_byKey == null || _byKeyCount != Bindings.Count)
            {
                _byKey = new Dictionary<string, AnimationCurve>(StringComparer.Ordinal);
                foreach (var b in Bindings) _byKey.TryAdd(b.Key, b.Curve);
                _byKeyCount = Bindings.Count;
            }
            return _byKey.TryGetValue(key, out var c) ? c : null;
        }

        /// <summary>
        /// Poses <paramref name="go"/> as the clip has it at <paramref name="time"/> seconds
        /// (UnityEngine.AnimationClip.SampleAnimation): every binding the hierarchy has is written,
        /// with no Animator and no blending. Bindings whose target is missing are skipped.
        /// </summary>
        public void SampleAnimation(GameObject go, float time)
        {
            if (go == null) return;
            var root = go.transform;
            HashSet<Transform> rotated = null;
            foreach (var b in Bindings)
            {
                if (b.Curve == null) continue;
                var slot = AnimatorBindings.Resolve(root, b);
                if (slot.set == null) continue;
                float v = b.Curve.Evaluate(time);
                slot.set(slot.isBool ? (v > 0.5f ? 1f : 0f) : v);
                if ((b.ClassId == 4 || b.ClassId == 224) && b.Attribute.StartsWith("m_LocalRotation", StringComparison.Ordinal)
                    && (string.IsNullOrEmpty(b.Path) ? root : root.Find(b.Path)) is { } t)
                    (rotated ??= new HashSet<Transform>()).Add(t);
            }
            // Rotation is written a component at a time: restore unit length once all four are in.
            if (rotated == null) return;
            foreach (var t in rotated)
            {
                var q = t.localRotation;
                float mag = MathF.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
                if (mag > 1e-6f && MathF.Abs(mag - 1f) > 1e-6f) t.localRotation = new Quaternion(q.x / mag, q.y / mag, q.z / mag, q.w / mag);
            }
        }
    }
}
