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
        public string Key => $"{Path}|{ClassId}|{ScriptGuid}|{Attribute}";
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
        public float Speed = 1f;
        public string SpeedParameter;
        public bool WriteDefaults = true;
        public readonly List<AnimatorTransitionData> Transitions = new();
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
    }
}
