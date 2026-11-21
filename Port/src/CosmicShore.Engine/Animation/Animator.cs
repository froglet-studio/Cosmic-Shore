using System;
using System.Collections.Generic;

namespace CosmicShore.Engine
{
    public enum AnimatorUpdateMode { Normal = 0, AnimatePhysics = 1, UnscaledTime = 2, Fixed = 3 }
    public enum AnimatorCullingMode { AlwaysAnimate = 0, CullUpdateTransforms = 1, CullCompletely = 2 }
    public enum AnimatorControllerParameterType { Float = 1, Int = 3, Bool = 4, Trigger = 9 }

    /// <summary>An animation controller asset (state graph); the port keeps its identity + parameter list.</summary>
    public class RuntimeAnimatorController : Object
    {
        public AnimationClip[] animationClips { get; set; } = Array.Empty<AnimationClip>();
    }

    public class AnimatorOverrideController : RuntimeAnimatorController
    {
        public RuntimeAnimatorController runtimeAnimatorController { get; set; }
    }

    public partial class AnimationClip : Object
    {
        public float length { get; set; }
        public float frameRate { get; set; } = 60f;
        public bool isLooping { get; set; }
        public WrapMode wrapMode { get; set; }
    }

    public enum WrapMode { Once = 1, Loop = 2, PingPong = 4, Default = 0, ClampForever = 8, Clamp = 1 }

    public class Avatar : Object
    {
        public bool isValid => true;
        public bool isHuman => false;
    }

    public class AnimatorControllerParameter
    {
        public string name { get; set; }
        public AnimatorControllerParameterType type { get; set; }
        public int nameHash => Animator.StringToHash(name);
        public float defaultFloat { get; set; }
        public int defaultInt { get; set; }
        public bool defaultBool { get; set; }
    }

    /// <summary>The playing state of one Animator layer (UnityEngine.AnimatorStateInfo).</summary>
    public struct AnimatorStateInfo
    {
        public int fullPathHash { get; set; }
        public int shortNameHash { get; set; }
        public float normalizedTime { get; set; }
        public float length { get; set; }
        public float speed { get; set; }
        public float speedMultiplier { get; set; }
        public int tagHash { get; set; }
        public bool loop { get; set; }
        public bool IsName(string name) => Animator.StringToHash(name) == shortNameHash || Animator.StringToHash(name) == fullPathHash;
        public bool IsTag(string tag) => Animator.StringToHash(tag) == tagHash;
    }

    public enum HumanBodyBones { Hips = 0, Head = 10, LastBone = 55 }

    /// <summary>Legacy Animation component (the game keeps a few; clips are data-only here).</summary>
    public class Animation : Behaviour
    {
        public AnimationClip clip { get; set; }
        public bool playAutomatically { get; set; } = true;
        public bool isPlaying { get; private set; }
        public bool Play() { isPlaying = clip != null; return isPlaying; }
        public bool Play(string animation) { isPlaying = true; return true; }
        public void Stop() => isPlaying = false;
    }
}
