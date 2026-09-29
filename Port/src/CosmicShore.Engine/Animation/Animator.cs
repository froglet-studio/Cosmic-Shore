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

    public class AnimationClip : Object
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

    /// <summary>
    /// Mecanim state-machine driver (UnityEngine.Animator). The port stores parameters,
    /// layer weights and the current state exactly as the game sets them; driving bone
    /// poses from an AnimatorController graph is the animation arc's job (the vessel
    /// puppetry the game does in code — VesselAnimation — already runs without it).
    /// </summary>
    public class Animator : Behaviour
    {
        readonly Dictionary<int, float> _floats = new();
        readonly Dictionary<int, int> _ints = new();
        readonly Dictionary<int, bool> _bools = new();
        readonly HashSet<int> _triggers = new();
        readonly Dictionary<int, float> _layerWeights = new();
        readonly Dictionary<int, AnimatorStateInfo> _states = new();

        public RuntimeAnimatorController runtimeAnimatorController { get; set; }
        public Avatar avatar { get; set; }
        public float speed { get; set; } = 1f;
        public bool applyRootMotion { get; set; }
        public AnimatorUpdateMode updateMode { get; set; }
        public AnimatorCullingMode cullingMode { get; set; }
        public bool keepAnimatorStateOnDisable { get; set; }
        public bool writeDefaultValuesOnDisable { get; set; }
        public bool isInitialized => true;
        public bool hasBoundPlayables => runtimeAnimatorController != null;
        public int layerCount => Math.Max(1, _layerWeights.Count);
        public AnimatorControllerParameter[] parameters { get; set; } = Array.Empty<AnimatorControllerParameter>();
        public int parameterCount => parameters.Length;

        /// <summary>Unity hashes names with CRC-32 (IEEE); the value is stable across runs.</summary>
        public static int StringToHash(string name)
        {
            if (string.IsNullOrEmpty(name)) return 0;
            uint crc = 0xFFFFFFFFu;
            foreach (char ch in name)
            {
                crc ^= (byte)ch;
                for (int k = 0; k < 8; k++) crc = (crc >> 1) ^ (0xEDB88320u & (uint)-(int)(crc & 1));
            }
            return (int)~crc;
        }

        public void SetFloat(string name, float value) => _floats[StringToHash(name)] = value;
        public void SetFloat(int id, float value) => _floats[id] = value;
        public void SetFloat(string name, float value, float dampTime, float deltaTime) => SetFloat(StringToHash(name), value, dampTime, deltaTime);
        public void SetFloat(int id, float value, float dampTime, float deltaTime)
        {
            float cur = GetFloat(id);
            _floats[id] = dampTime <= 0f ? value : cur + (value - cur) * Math.Clamp(deltaTime / dampTime, 0f, 1f);
        }
        public float GetFloat(string name) => GetFloat(StringToHash(name));
        public float GetFloat(int id) => _floats.TryGetValue(id, out var v) ? v : 0f;
        public void SetInteger(string name, int value) => _ints[StringToHash(name)] = value;
        public void SetInteger(int id, int value) => _ints[id] = value;
        public int GetInteger(string name) => GetInteger(StringToHash(name));
        public int GetInteger(int id) => _ints.TryGetValue(id, out var v) ? v : 0;
        public void SetBool(string name, bool value) => _bools[StringToHash(name)] = value;
        public void SetBool(int id, bool value) => _bools[id] = value;
        public bool GetBool(string name) => GetBool(StringToHash(name));
        public bool GetBool(int id) => _bools.TryGetValue(id, out var v) && v;
        public void SetTrigger(string name) => _triggers.Add(StringToHash(name));
        public void SetTrigger(int id) => _triggers.Add(id);
        public void ResetTrigger(string name) => _triggers.Remove(StringToHash(name));
        public void ResetTrigger(int id) => _triggers.Remove(id);
        public bool IsParameterControlledByCurve(string name) => false;

        public void SetLayerWeight(int layerIndex, float weight) => _layerWeights[layerIndex] = weight;
        public float GetLayerWeight(int layerIndex) => _layerWeights.TryGetValue(layerIndex, out var w) ? w : (layerIndex == 0 ? 1f : 0f);
        public int GetLayerIndex(string layerName) => -1;
        public string GetLayerName(int layerIndex) => layerIndex == 0 ? "Base Layer" : $"Layer {layerIndex}";

        public void Play(string stateName, int layer = -1, float normalizedTime = float.NegativeInfinity) => Play(StringToHash(stateName), layer, normalizedTime);
        public void Play(int stateNameHash, int layer = -1, float normalizedTime = float.NegativeInfinity)
            => _states[Math.Max(0, layer)] = new AnimatorStateInfo { shortNameHash = stateNameHash, fullPathHash = stateNameHash, normalizedTime = float.IsNegativeInfinity(normalizedTime) ? 0f : normalizedTime, speed = 1f, speedMultiplier = 1f };
        public void PlayInFixedTime(string stateName, int layer = -1, float fixedTime = float.NegativeInfinity) => Play(stateName, layer, 0f);
        public void CrossFade(string stateName, float normalizedTransitionDuration, int layer = -1, float normalizedTimeOffset = float.NegativeInfinity) => Play(stateName, layer, normalizedTimeOffset);
        public void CrossFade(int stateHashName, float normalizedTransitionDuration, int layer = -1, float normalizedTimeOffset = float.NegativeInfinity) => Play(stateHashName, layer, normalizedTimeOffset);
        public void CrossFadeInFixedTime(string stateName, float fixedTransitionDuration, int layer = -1, float fixedTimeOffset = 0f) => Play(stateName, layer, 0f);
        public AnimatorStateInfo GetCurrentAnimatorStateInfo(int layerIndex) => _states.TryGetValue(layerIndex, out var s) ? s : default;
        public AnimatorStateInfo GetNextAnimatorStateInfo(int layerIndex) => default;
        public bool IsInTransition(int layerIndex) => false;
        public bool HasState(int layerIndex, int stateID) => true;
        public void Rebind() { _floats.Clear(); _ints.Clear(); _bools.Clear(); _triggers.Clear(); _states.Clear(); }
        public void Update(float deltaTime) { }
        public void StartPlayback() { }
        public void StopPlayback() { }
        public void StartRecording(int frameCount) { }
        public void StopRecording() { }
        public void WriteDefaultValues() { }
        public void MarkMaterialsDirty() { }
        public Transform GetBoneTransform(HumanBodyBones humanBoneId) => null;
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
