using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace CosmicShore.Engine
{
    /// <summary>
    /// Mecanim state-machine driver (UnityEngine.Animator). Plays an <see cref="AnimatorController"/>:
    /// per layer a current state (and, during a cross-fade, a next one), exit-time and parameter
    /// transitions including the Any State, triggers consumed when a transition takes them, and the
    /// clips' float curves written onto the objects they name every frame (write-defaults restores
    /// what the active state does not animate). Ticked by the game loop after Update and coroutines,
    /// before LateUpdate - where the original engine evaluates animation.
    ///
    /// All playback state lives in a side table keyed by the instance, never in fields: Instantiate
    /// copies an Animator's declared fields, and a clone must start fresh rather than drive the
    /// original's objects.
    /// </summary>
    public class Animator : Behaviour
    {
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
        public int layerCount => Controller?.Layers.Count is > 0 and var n ? n : 1;
        public AnimatorControllerParameter[] parameters
        {
            get => Controller?.Parameters ?? State.ExtraParameters;
            set => State.ExtraParameters = value ?? Array.Empty<AnimatorControllerParameter>();
        }
        public int parameterCount => parameters.Length;

        AnimatorController Controller => runtimeAnimatorController switch
        {
            AnimatorController c => c,
            AnimatorOverrideController o => o.runtimeAnimatorController as AnimatorController,
            _ => null,
        };

        // ── State ──────────────────────────────────────────────────────

        sealed class LayerRuntime
        {
            public int Current = -1, Next = -1;
            public float CurrentTime, NextTime, TransitionElapsed, TransitionDuration;
            public float Weight = 1f;
        }

        sealed class Slot
        {
            public int Index;
            public ClipBinding Binding;
            public Func<float> Get;
            public Action<float> Set;
            public float Default;
            public bool IsBool;
        }

        sealed class Runtime
        {
            public readonly Dictionary<int, float> Floats = new();
            public readonly Dictionary<int, int> Ints = new();
            public readonly Dictionary<int, bool> Bools = new();
            public readonly HashSet<int> Triggers = new();
            public readonly Dictionary<int, float> LayerWeights = new();
            public readonly Dictionary<int, AnimatorStateInfo> PlayedWithoutController = new();
            public AnimatorControllerParameter[] ExtraParameters = Array.Empty<AnimatorControllerParameter>();
            public AnimatorController BoundController;
            public LayerRuntime[] Layers = Array.Empty<LayerRuntime>();
            public readonly Dictionary<string, Slot> Slots = new(StringComparer.Ordinal);
            public readonly List<Slot> SlotList = new();
            /// <summary>Transforms a rotation slot writes: renormalized after every write pass.</summary>
            public readonly List<Transform> RotationTargets = new();
            /// <summary>Each clip's curves indexed by slot (null = the clip does not animate that slot).</summary>
            public readonly Dictionary<AnimationClip, AnimationCurve[]> ClipCurves = new();
            public float[] Values = Array.Empty<float>(), Acc = Array.Empty<float>(), Cover = Array.Empty<float>();
            public bool[] Has = Array.Empty<bool>();
            public readonly List<MotionSample> Samples = new();
            public bool PlayedSinceTick;
            public int LastTickFrame = int.MinValue;
        }

        struct MotionSample
        {
            public AnimationClip Clip;
            public float Weight;
            public float TimeScale;
            public float Time;
        }

        static readonly ConditionalWeakTable<Animator, Runtime> s_state = new();
        Runtime State => s_state.GetValue(this, static _ => new Runtime());

        /// <summary>Resolves a MonoBehaviour binding's script guid to its type (installed by the content layer).</summary>
        public static Func<string, Type> ScriptTypeResolver;

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

        // ── Parameters ─────────────────────────────────────────────────

        public void SetFloat(string name, float value) => State.Floats[StringToHash(name)] = value;
        public void SetFloat(int id, float value) => State.Floats[id] = value;
        public void SetFloat(string name, float value, float dampTime, float deltaTime) => SetFloat(StringToHash(name), value, dampTime, deltaTime);
        public void SetFloat(int id, float value, float dampTime, float deltaTime)
        {
            float cur = GetFloat(id);
            State.Floats[id] = dampTime <= 0f ? value : cur + (value - cur) * Math.Clamp(deltaTime / dampTime, 0f, 1f);
        }
        public float GetFloat(string name) => GetFloat(StringToHash(name));
        public float GetFloat(int id) => State.Floats.TryGetValue(id, out var v) ? v : DefaultOf(id, p => p.defaultFloat);
        public void SetInteger(string name, int value) => State.Ints[StringToHash(name)] = value;
        public void SetInteger(int id, int value) => State.Ints[id] = value;
        public int GetInteger(string name) => GetInteger(StringToHash(name));
        public int GetInteger(int id) => State.Ints.TryGetValue(id, out var v) ? v : (int)DefaultOf(id, p => p.defaultInt);
        public void SetBool(string name, bool value) => State.Bools[StringToHash(name)] = value;
        public void SetBool(int id, bool value) => State.Bools[id] = value;
        public bool GetBool(string name) => GetBool(StringToHash(name));
        public bool GetBool(int id) => State.Bools.TryGetValue(id, out var v) ? v : DefaultOf(id, p => p.defaultBool ? 1f : 0f) > 0.5f;
        public void SetTrigger(string name) => State.Triggers.Add(StringToHash(name));
        public void SetTrigger(int id) => State.Triggers.Add(id);
        public void ResetTrigger(string name) => State.Triggers.Remove(StringToHash(name));
        public void ResetTrigger(int id) => State.Triggers.Remove(id);
        public bool IsParameterControlledByCurve(string name) => false;

        float DefaultOf(int id, Func<AnimatorControllerParameter, float> pick)
        {
            foreach (var p in parameters)
                if (p.nameHash == id) return pick(p);
            return 0f;
        }

        // ── Layers ─────────────────────────────────────────────────────

        public void SetLayerWeight(int layerIndex, float weight)
        {
            State.LayerWeights[layerIndex] = weight;
            if (Bind() && layerIndex >= 0 && layerIndex < State.Layers.Length) State.Layers[layerIndex].Weight = weight;
        }
        public float GetLayerWeight(int layerIndex)
        {
            if (State.LayerWeights.TryGetValue(layerIndex, out var w)) return w;
            var c = Controller;
            if (c != null && layerIndex >= 0 && layerIndex < c.Layers.Count) return layerIndex == 0 ? 1f : c.Layers[layerIndex].DefaultWeight;
            return layerIndex == 0 ? 1f : 0f;
        }
        public int GetLayerIndex(string layerName)
        {
            var c = Controller;
            if (c == null) return -1;
            for (int i = 0; i < c.Layers.Count; i++) if (c.Layers[i].Name == layerName) return i;
            return -1;
        }
        public string GetLayerName(int layerIndex)
        {
            var c = Controller;
            if (c != null && layerIndex >= 0 && layerIndex < c.Layers.Count) return c.Layers[layerIndex].Name;
            return layerIndex == 0 ? "Base Layer" : $"Layer {layerIndex}";
        }

        // ── Playing states ─────────────────────────────────────────────

        public void Play(string stateName, int layer = -1, float normalizedTime = float.NegativeInfinity) => Play(StringToHash(stateName), layer, normalizedTime);
        public void Play(int stateNameHash, int layer = -1, float normalizedTime = float.NegativeInfinity)
            => GoTo(stateNameHash, layer, float.IsNegativeInfinity(normalizedTime) ? 0f : normalizedTime, 0f, false);
        public void PlayInFixedTime(string stateName, int layer = -1, float fixedTime = float.NegativeInfinity)
            => GoTo(StringToHash(stateName), layer, 0f, 0f, false);
        public void CrossFade(string stateName, float normalizedTransitionDuration, int layer = -1, float normalizedTimeOffset = float.NegativeInfinity)
            => CrossFade(StringToHash(stateName), normalizedTransitionDuration, layer, normalizedTimeOffset);
        public void CrossFade(int stateHashName, float normalizedTransitionDuration, int layer = -1, float normalizedTimeOffset = float.NegativeInfinity)
            => GoTo(stateHashName, layer, float.IsNegativeInfinity(normalizedTimeOffset) ? 0f : normalizedTimeOffset, normalizedTransitionDuration, false);
        public void CrossFadeInFixedTime(string stateName, float fixedTransitionDuration, int layer = -1, float fixedTimeOffset = 0f)
            => GoTo(StringToHash(stateName), layer, 0f, fixedTransitionDuration, true);

        void GoTo(int hash, int layer, float normalizedTime, float transition, bool fixedDuration)
        {
            var st = State;
            st.PlayedSinceTick = true;
            if (!Bind())
            {
                st.PlayedWithoutController[Math.Max(0, layer)] = new AnimatorStateInfo { shortNameHash = hash, fullPathHash = hash, normalizedTime = normalizedTime, speed = 1f, speedMultiplier = 1f };
                return;
            }
            var c = st.BoundController;
            for (int li = 0; li < c.Layers.Count; li++)
            {
                if (layer >= 0 && li != layer) continue;
                var states = c.Layers[li].States;
                int idx = states.FindIndex(s => s.NameHash == hash || s.FullPathHash == hash);
                if (idx < 0) continue;
                var lr = st.Layers[li];
                float len = StateLength(states[idx]);
                if (transition > 0f && lr.Current >= 0)
                {
                    lr.Next = idx;
                    lr.NextTime = normalizedTime * len;
                    lr.TransitionElapsed = 0f;
                    lr.TransitionDuration = fixedDuration ? transition : transition * StateLength(states[lr.Current]);
                }
                else
                {
                    lr.Current = idx;
                    lr.CurrentTime = normalizedTime * len;
                    lr.Next = -1;
                }
                return;
            }
            Debug.LogWarning($"Animator.GotoState: State could not be found (hash {hash}) on '{name}'");
        }

        public AnimatorStateInfo GetCurrentAnimatorStateInfo(int layerIndex)
        {
            var st = State;
            if (!Bind() || layerIndex < 0 || layerIndex >= st.Layers.Length)
                return st.PlayedWithoutController.TryGetValue(layerIndex, out var s) ? s : default;
            var lr = st.Layers[layerIndex];
            return Info(st.BoundController.Layers[layerIndex], lr.Current, lr.CurrentTime);
        }

        public AnimatorStateInfo GetNextAnimatorStateInfo(int layerIndex)
        {
            var st = State;
            if (!Bind() || layerIndex < 0 || layerIndex >= st.Layers.Length || st.Layers[layerIndex].Next < 0) return default;
            var lr = st.Layers[layerIndex];
            return Info(st.BoundController.Layers[layerIndex], lr.Next, lr.NextTime);
        }

        AnimatorStateInfo Info(AnimatorLayerData layer, int index, float time)
        {
            if (index < 0 || index >= layer.States.Count) return default;
            var s = layer.States[index];
            float len = StateLength(s);
            return new AnimatorStateInfo
            {
                shortNameHash = s.NameHash, fullPathHash = s.FullPathHash, tagHash = s.TagHash,
                normalizedTime = Normalized(s, time, len), length = len, speed = s.Speed, speedMultiplier = 1f,
                loop = IsLooping(s),
            };
        }

        public bool IsInTransition(int layerIndex)
            => Bind() && layerIndex >= 0 && layerIndex < State.Layers.Length && State.Layers[layerIndex].Next >= 0;

        public bool HasState(int layerIndex, int stateID)
        {
            var c = Controller;
            if (c == null) return false;
            if (layerIndex < 0 || layerIndex >= c.Layers.Count) return false;
            return c.Layers[layerIndex].States.Exists(s => s.NameHash == stateID || s.FullPathHash == stateID);
        }

        public void Rebind()
        {
            var st = State;
            st.Floats.Clear(); st.Ints.Clear(); st.Bools.Clear(); st.Triggers.Clear(); st.PlayedWithoutController.Clear();
            st.BoundController = null;
            Bind();
        }

        public void Update(float deltaTime) => Evaluate(deltaTime);
        public void StartPlayback() { }
        public void StopPlayback() { }
        public void StartRecording(int frameCount) { }
        public void StopRecording() { }
        public void WriteDefaultValues()
        {
            foreach (var slot in State.SlotList) slot.Set(slot.Default);
        }
        public void MarkMaterialsDirty() { }
        public Transform GetBoneTransform(HumanBodyBones humanBoneId) => null;

        /// <summary>Port diagnostics: controller, current state, float parameters and how far the pose is from rest.</summary>
        public string DebugSummary()
        {
            var st = State;
            if (!Bind()) return $"{name}: no controller";
            var c = st.BoundController;
            var sb = new System.Text.StringBuilder();
            sb.Append($"{name} [{c.name}]");
            for (int li = 0; li < c.Layers.Count; li++)
            {
                var lr = st.Layers[li];
                sb.Append($" L{li}={(lr.Current >= 0 ? c.Layers[li].States[lr.Current].Name : "-")}");
            }
            foreach (var p in c.Parameters)
                if (p.type == AnimatorControllerParameterType.Float) sb.Append($" {p.name}={GetFloat(p.name):0.###}");
            float maxDev = 0f; string maxKey = null;
            foreach (var slot in st.SlotList)
            {
                float v; try { v = slot.Get(); } catch (Exception) { continue; }
                float d = MathF.Abs(v - slot.Default);
                if (d > maxDev) { maxDev = d; maxKey = slot.Binding.Path + ":" + slot.Binding.Attribute; }
            }
            sb.Append($" slots={st.SlotList.Count} maxDeviation={maxDev:0.####} at {maxKey}");
            return sb.ToString();
        }

        // ── Frame ──────────────────────────────────────────────────────

        static readonly List<Animator> s_tick = new();

        /// <summary>Advances and applies every active Animator (called once per frame by the game loop).</summary>
        static readonly bool s_traceAnim = Environment.GetEnvironmentVariable("CS_PORT_TRACE_ANIM") != null;

        static void TraceCensus()
        {
            int live = 0; long slots = 0;
            var top = new List<(int n, string name)>();
            foreach (var a in s_tick)
            {
                if (!a.isActiveAndEnabled) continue;
                live++;
                int n = a.State.SlotList.Count; slots += n;
                top.Add((n, a.gameObject.name + ":" + (a.State.BoundController?.name ?? "-")));
            }
            top.Sort((x, y) => y.n.CompareTo(x.n));
            Console.WriteLine($"[anim] frame {Time.frameCount}: {live} live animators, {slots} slots; top: " +
                string.Join(", ", top.GetRange(0, Math.Min(4, top.Count)).ConvertAll(t => $"{t.name}={t.n}")));
        }

        public static void TickAll()
        {
            LiveComponents<Animator>.CollectActive(s_tick);
            if (s_traceAnim && Time.frameCount % 300 == 0) TraceCensus();
            foreach (var a in s_tick)
            {
                if (!a.isActiveAndEnabled) continue;
                float dt = a.updateMode == AnimatorUpdateMode.UnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                try { a.Evaluate(dt); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        void Evaluate(float deltaTime)
        {
            var st = State;
            if (!Bind()) return;
            // Re-enabled after being off: the original engine restarts the controller from its
            // default states (unless told to keep them) - but a Play issued in the same frame wins.
            // Not ticked last frame = it was off (inactive objects never reach TickAll).
            bool reenabled = st.LastTickFrame != Time.frameCount - 1 && st.LastTickFrame != Time.frameCount;
            if (reenabled && !st.PlayedSinceTick && !keepAnimatorStateOnDisable) ResetToDefaults();
            st.LastTickFrame = Time.frameCount;
            st.PlayedSinceTick = false;

            var c = st.BoundController;
            float dt = deltaTime * speed;
            for (int li = 0; li < c.Layers.Count; li++)
                StepLayer(c.Layers[li], st.Layers[li], dt);
            Apply(c);
        }

        void ResetToDefaults()
        {
            var st = State;
            var c = st.BoundController;
            for (int i = 0; i < c.Layers.Count; i++)
            {
                st.Layers[i].Current = c.Layers[i].States.Count > 0 ? Math.Clamp(c.Layers[i].DefaultState, 0, c.Layers[i].States.Count - 1) : -1;
                st.Layers[i].CurrentTime = 0f;
                st.Layers[i].Next = -1;
            }
        }

        /// <summary>A state's duration: its clip's length, or a blend tree's weight-averaged child length.</summary>
        float StateLength(AnimatorStateData s)
        {
            if (s.Tree != null)
            {
                var samples = new List<MotionSample>();
                GatherTree(s.Tree, 1f, 1f, samples);
                float sum = 0f, wsum = 0f;
                foreach (var m in samples)
                {
                    if (m.Clip == null || m.Clip.length <= 0f) continue;
                    sum += m.Weight * m.Clip.length / (m.TimeScale > 0f ? m.TimeScale : 1f);
                    wsum += m.Weight;
                }
                return wsum > 0f && sum > 0f ? sum / wsum : 1f;
            }
            return s.Clip != null && s.Clip.length > 0f ? s.Clip.length : 1f;
        }

        float Normalized(AnimatorStateData s, float time, float len)
            => !string.IsNullOrEmpty(s.TimeParameter) ? GetFloat(s.TimeParameter) : len > 0f ? time / len : 0f;

        static bool IsLooping(AnimatorStateData s)
        {
            if (s.Clip != null) return s.Clip.isLooping;
            return s.Tree != null && AnyLooping(s.Tree);
        }

        static bool AnyLooping(BlendTree t)
        {
            foreach (var c in t.Children)
                if (c.Clip != null ? c.Clip.isLooping : c.Tree != null && AnyLooping(c.Tree)) return true;
            return false;
        }

        float StateSpeed(AnimatorStateData s)
            => s.Speed * (string.IsNullOrEmpty(s.SpeedParameter) ? 1f : GetFloat(s.SpeedParameter));

        void StepLayer(AnimatorLayerData layer, LayerRuntime lr, float dt)
        {
            if (lr.Current < 0) return;
            var cur = layer.States[lr.Current];
            if (lr.Next >= 0)
            {
                var next = layer.States[lr.Next];
                lr.CurrentTime += dt * StateSpeed(cur);
                lr.NextTime += dt * StateSpeed(next);
                lr.TransitionElapsed += dt;
                if (lr.TransitionElapsed >= lr.TransitionDuration)
                {
                    lr.Current = lr.Next;
                    lr.CurrentTime = lr.NextTime;
                    lr.Next = -1;
                }
                return;
            }

            lr.CurrentTime += dt * StateSpeed(cur);
            float len = StateLength(cur);
            float norm = Normalized(cur, lr.CurrentTime, len);
            var taken = Pick(layer.AnyStateTransitions, norm, lr.Current, anyState: true) ?? Pick(cur.Transitions, norm, lr.Current, anyState: false);
            if (taken == null) return;
            int dst = taken.Destination >= 0 && taken.Destination < layer.States.Count
                ? taken.Destination
                : Math.Clamp(layer.DefaultState, 0, layer.States.Count - 1);
            float dstLen = StateLength(layer.States[dst]);
            float duration = taken.HasFixedDuration ? taken.Duration : taken.Duration * len;
            if (duration <= 0f)
            {
                lr.Current = dst;
                lr.CurrentTime = taken.Offset * dstLen;
                return;
            }
            lr.Next = dst;
            lr.NextTime = taken.Offset * dstLen;
            lr.TransitionElapsed = 0f;
            lr.TransitionDuration = duration;
        }

        AnimatorTransitionData Pick(List<AnimatorTransitionData> transitions, float norm, int current, bool anyState)
        {
            foreach (var t in transitions)
            {
                if (t.Mute) continue;
                if (anyState && t.Destination == current) continue; // "can transition to self" is off by default
                if (t.Conditions.Count == 0 && !t.HasExitTime) continue; // the editor rejects this; so does the engine
                if (t.HasExitTime && norm < t.ExitTime) continue;
                bool ok = true;
                foreach (var cnd in t.Conditions)
                    if (!Holds(cnd)) { ok = false; break; }
                if (!ok) continue;
                foreach (var cnd in t.Conditions)
                    State.Triggers.Remove(StringToHash(cnd.Parameter)); // a transition consumes the triggers it read
                return t;
            }
            return null;
        }

        bool Holds(AnimatorCondition c)
        {
            int id = StringToHash(c.Parameter);
            var st = State;
            AnimatorControllerParameterType? type = null;
            foreach (var p in parameters) if (p.nameHash == id) { type = p.type; break; }
            switch (c.Mode)
            {
                case AnimatorConditionMode.If:
                    return type == AnimatorControllerParameterType.Trigger ? st.Triggers.Contains(id) : GetBool(id);
                case AnimatorConditionMode.IfNot:
                    return !GetBool(id);
                case AnimatorConditionMode.Greater:
                    return type == AnimatorControllerParameterType.Int ? GetInteger(id) > c.Threshold : GetFloat(id) > c.Threshold;
                case AnimatorConditionMode.Less:
                    return type == AnimatorControllerParameterType.Int ? GetInteger(id) < c.Threshold : GetFloat(id) < c.Threshold;
                case AnimatorConditionMode.Equals:
                    return GetInteger(id) == (int)c.Threshold;
                case AnimatorConditionMode.NotEqual:
                    return GetInteger(id) != (int)c.Threshold;
            }
            return false;
        }

        // ── Writing the curves ─────────────────────────────────────────

        void Apply(AnimatorController c)
        {
            var st = State;
            int n = st.SlotList.Count;
            var values = st.Values; var has = st.Has; var acc = st.Acc; var cover = st.Cover;
            Array.Clear(has, 0, n);
            for (int li = 0; li < c.Layers.Count; li++)
            {
                var lr = st.Layers[li];
                if (lr.Current < 0) continue;
                float layerWeight = li == 0 ? 1f : lr.Weight;
                if (layerWeight <= 0f) continue;
                var cur = c.Layers[li].States[lr.Current];
                AnimatorStateData next = lr.Next >= 0 ? c.Layers[li].States[lr.Next] : null;
                float w = next == null ? 0f : Math.Clamp(lr.TransitionDuration > 0f ? lr.TransitionElapsed / lr.TransitionDuration : 1f, 0f, 1f);

                var samples = st.Samples;
                samples.Clear();
                GatherState(cur, lr.CurrentTime, 1f - w, samples);
                if (next != null) GatherState(next, lr.NextTime, w, samples);
                Array.Clear(acc, 0, n);
                Array.Clear(cover, 0, n);
                foreach (var m in samples)
                {
                    if (m.Weight <= 0f || m.Clip == null) continue;
                    var curves = CurvesOf(m.Clip);
                    float len = m.Clip.length;
                    float t = len <= 0f ? 0f : m.Clip.isLooping ? PositiveMod(m.Time, len) : Math.Clamp(m.Time, 0f, len);
                    for (int i = 0; i < curves.Length; i++)
                    {
                        var curve = curves[i];
                        if (curve == null) continue;
                        acc[i] += m.Weight * curve.Evaluate(t);
                        cover[i] += m.Weight;
                    }
                }

                // Everything the controller binds takes part: write-defaults fills what a state does not animate.
                bool writeDefaults = cur.WriteDefaults || (next != null && next.WriteDefaults);
                for (int i = 0; i < n; i++)
                {
                    var slot = st.SlotList[i];
                    bool animated = cover[i] > 0f;
                    if (li == 0)
                    {
                        if (!animated && !writeDefaults) continue;
                        values[i] = acc[i] + Math.Max(0f, 1f - cover[i]) * slot.Default;
                        has[i] = true;
                    }
                    else
                    {
                        if (!animated) continue; // a higher layer only overrides what it animates
                        float lower = has[i] ? values[i] : slot.Default;
                        float v = acc[i] + Math.Max(0f, 1f - cover[i]) * lower;
                        values[i] = lower + (v - lower) * layerWeight;
                        has[i] = true;
                    }
                }
            }
            for (int i = 0; i < n; i++)
            {
                if (!has[i]) continue;
                var slot = st.SlotList[i];
                try { slot.Set(slot.IsBool ? (values[i] > 0.5f ? 1f : 0f) : values[i]); }
                catch (Exception) { /* a destroyed target: the next Rebind drops it */ }
            }
            // Rotation curves are written a component at a time and blended as a weighted sum:
            // restore unit length once every component is in (the original engine does the same).
            foreach (var t in st.RotationTargets)
            {
                if (t == null) continue;
                var q = t.localRotation;
                float mag = MathF.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
                if (mag > 1e-6f && MathF.Abs(mag - 1f) > 1e-6f)
                    t.localRotation = new Quaternion(q.x / mag, q.y / mag, q.z / mag, q.w / mag);
            }
        }

        /// <summary>The clips a state plays this frame, with their blend weights and local times.</summary>
        void GatherState(AnimatorStateData s, float time, float weight, List<MotionSample> into)
        {
            if (weight <= 0f) return;
            if (s.Tree != null)
            {
                float len = StateLength(s);
                float norm = Normalized(s, time, len);
                int start = into.Count;
                GatherTree(s.Tree, weight, 1f, into);
                for (int i = start; i < into.Count; i++)
                {
                    var m = into[i];
                    m.Time = m.Clip != null ? norm * m.Clip.length : 0f;
                    into[i] = m;
                }
                return;
            }
            if (s.Clip == null) return;
            float t = !string.IsNullOrEmpty(s.TimeParameter) ? GetFloat(s.TimeParameter) * s.Clip.length : time;
            into.Add(new MotionSample { Clip = s.Clip, Weight = weight, TimeScale = 1f, Time = t });
        }

        void GatherTree(BlendTree tree, float weight, float timeScale, List<MotionSample> into)
        {
            var kids = tree.Children;
            if (kids.Count == 0 || weight <= 0f) return;
            Span<float> w = kids.Count <= 64 ? stackalloc float[kids.Count] : new float[kids.Count];
            BlendTreeWeights.Compute(tree, GetFloat(tree.ParameterX ?? ""), GetFloat(tree.ParameterY ?? ""),
                p => GetFloat(p ?? ""), w);
            for (int i = 0; i < kids.Count; i++)
            {
                if (w[i] <= 0f) continue;
                var k = kids[i];
                float ts = timeScale * (k.TimeScale == 0f ? 1f : k.TimeScale);
                if (k.Tree != null) GatherTree(k.Tree, weight * w[i], ts, into);
                else if (k.Clip != null) into.Add(new MotionSample { Clip = k.Clip, Weight = weight * w[i], TimeScale = ts });
            }
        }

        AnimationCurve[] CurvesOf(AnimationClip clip)
        {
            var st = State;
            if (st.ClipCurves.TryGetValue(clip, out var arr)) return arr;
            arr = new AnimationCurve[st.SlotList.Count];
            foreach (var slot in st.SlotList) arr[slot.Index] = clip.CurveFor(slot.Binding.Key);
            st.ClipCurves[clip] = arr;
            return arr;
        }

        static float PositiveMod(float x, float m) { float r = x % m; return r < 0f ? r + m : r; }

        // ── Binding ────────────────────────────────────────────────────

        bool Bind()
        {
            var st = State;
            var c = Controller;
            if (c == null) return false;
            if (ReferenceEquals(st.BoundController, c)) return true;
            st.BoundController = c;
            st.Layers = new LayerRuntime[c.Layers.Count];
            for (int i = 0; i < c.Layers.Count; i++)
            {
                st.Layers[i] = new LayerRuntime { Weight = st.LayerWeights.TryGetValue(i, out var w) ? w : (i == 0 ? 1f : c.Layers[i].DefaultWeight) };
            }
            st.Slots.Clear();
            st.SlotList.Clear();
            st.ClipCurves.Clear();
            st.RotationTargets.Clear();
            var clips = new List<AnimationClip>();
            foreach (var layer in c.Layers)
                foreach (var s in layer.States)
                {
                    if (s.Clip != null) clips.Add(s.Clip);
                    if (s.Tree != null) CollectClips(s.Tree, clips);
                }
            foreach (var clip in clips)
                foreach (var b in clip.Bindings)
                {
                    if (st.Slots.ContainsKey(b.Key)) continue;
                    var slot = AnimatorBindings.Resolve(transform, b);
                    if (slot.set == null) continue;
                    float def = 0f;
                    try { def = slot.get(); } catch (Exception) { }
                    var s = new Slot { Index = st.SlotList.Count, Binding = b, Get = slot.get, Set = slot.set, Default = def, IsBool = slot.isBool };
                    st.Slots[b.Key] = s;
                    st.SlotList.Add(s);
                    if ((b.ClassId == 4 || b.ClassId == 224) && b.Attribute.StartsWith("m_LocalRotation", StringComparison.Ordinal)
                        && (string.IsNullOrEmpty(b.Path) ? transform : transform.Find(b.Path)) is { } rt && !st.RotationTargets.Contains(rt))
                        st.RotationTargets.Add(rt);
                }
            int n = st.SlotList.Count;
            st.Values = new float[n]; st.Acc = new float[n]; st.Cover = new float[n]; st.Has = new bool[n];
            ResetToDefaults();
            return true;
        }

        static void CollectClips(BlendTree t, List<AnimationClip> into)
        {
            foreach (var k in t.Children)
            {
                if (k.Clip != null) into.Add(k.Clip);
                if (k.Tree != null) CollectClips(k.Tree, into);
            }
        }
    }

    /// <summary>Resolves a clip binding to a getter/setter on the live object it names.</summary>
    static class AnimatorBindings
    {
        static readonly Dictionary<int, string> s_classNames = new()
        {
            [23] = "MeshRenderer", [137] = "SkinnedMeshRenderer", [212] = "SpriteRenderer", [108] = "Light",
            [20] = "Camera", [82] = "AudioSource", [96] = "TrailRenderer", [120] = "LineRenderer",
        };

        public static (Func<float> get, Action<float> set, bool isBool) Resolve(Transform root, ClipBinding b)
        {
            var t = string.IsNullOrEmpty(b.Path) ? root : root.Find(b.Path);
            if (t == null) return default;
            var go = t.gameObject;
            string attr = b.Attribute ?? "";
            int dot = attr.IndexOf('.');
            string head = dot < 0 ? attr : attr[..dot];
            string part = dot < 0 ? null : attr[(dot + 1)..];

            switch (b.ClassId)
            {
                case 1:
                    if (attr == "m_IsActive") return (() => go.activeSelf ? 1f : 0f, v => go.SetActive(v > 0.5f), true);
                    return default;
                case 4:
                case 224:
                    return TransformSlot(t, head, part);
                case 225:
                {
                    var cg = go.GetComponent<CanvasGroup>();
                    if (cg == null) return default;
                    return attr switch
                    {
                        "m_Alpha" => (() => cg.alpha, v => cg.alpha = v, false),
                        "m_BlocksRaycasts" => (() => cg.blocksRaycasts ? 1f : 0f, v => cg.blocksRaycasts = v > 0.5f, true),
                        "m_Interactable" => (() => cg.interactable ? 1f : 0f, v => cg.interactable = v > 0.5f, true),
                        "m_IgnoreParentGroups" => (() => cg.ignoreParentGroups ? 1f : 0f, v => cg.ignoreParentGroups = v > 0.5f, true),
                        "m_Enabled" => (() => cg.enabled ? 1f : 0f, v => cg.enabled = v > 0.5f, true),
                        _ => default,
                    };
                }
            }

            if (b.ClassId == 137 && head == "blendShape" && part != null && go.GetComponent<SkinnedMeshRenderer>() is { } smr)
            {
                // "blendShape.<name>": resolved to the mesh's shape index once, at bind.
                int idx = smr.sharedMesh ? smr.sharedMesh.GetBlendShapeIndex(part) : -1;
                if (idx < 0) return default;
                return (() => smr.GetBlendShapeWeight(idx), v => smr.SetBlendShapeWeight(idx, v), false);
            }

            Component comp = null;
            if (b.ClassId == 114)
            {
                var type = b.ScriptGuid != null ? Animator.ScriptTypeResolver?.Invoke(b.ScriptGuid) : null;
                if (type != null) comp = go.GetComponent(type);
            }
            else if (s_classNames.TryGetValue(b.ClassId, out var cname))
            {
                foreach (var c in go.GetComponents<Component>())
                    if (c != null && c.GetType().Name == cname) { comp = c; break; }
            }
            if (comp == null) return default;
            if (attr == "m_Enabled" && comp is Behaviour beh)
                return (() => beh.enabled ? 1f : 0f, v => beh.enabled = v > 0.5f, true);
            if (attr == "m_Enabled" && comp is Renderer ren)
                return (() => ren.enabled ? 1f : 0f, v => ren.enabled = v > 0.5f, true);
            return MemberSlot(comp, head, part);
        }

        static (Func<float>, Action<float>, bool) TransformSlot(Transform t, string head, string part)
        {
            int k = Axis(part);
            if (k < 0) return default;
            var rt = t as RectTransform;
            switch (head)
            {
                case "m_LocalPosition":
                    return (() => Get(t.localPosition, k), v => t.localPosition = With(t.localPosition, k, v), false);
                case "m_LocalScale":
                    return (() => Get(t.localScale, k), v => t.localScale = With(t.localScale, k, v), false);
                case "m_LocalRotation":
                    return (() => GetQ(t.localRotation, k), v => t.localRotation = WithQ(t.localRotation, k, v), false);
                case "localEulerAnglesRaw":
                case "localEulerAngles":
                case "m_LocalEulerAnglesHint":
                {
                    // Euler curves write one axis at a time: keep the three together here rather than
                    // re-deriving them from the quaternion, which can flip representation mid-frame.
                    var euler = new float[] { t.localEulerAngles.x, t.localEulerAngles.y, t.localEulerAngles.z };
                    return (() => euler[k], v => { euler[k] = v; t.localRotation = Quaternion.Euler(euler[0], euler[1], euler[2]); }, false);
                }
            }
            if (rt == null || k > 1) return default;
            return head switch
            {
                "m_AnchoredPosition" => (() => Get2(rt.anchoredPosition, k), v => rt.anchoredPosition = With2(rt.anchoredPosition, k, v), false),
                "m_SizeDelta" => (() => Get2(rt.sizeDelta, k), v => rt.sizeDelta = With2(rt.sizeDelta, k, v), false),
                "m_AnchorMin" => (() => Get2(rt.anchorMin, k), v => rt.anchorMin = With2(rt.anchorMin, k, v), false),
                "m_AnchorMax" => (() => Get2(rt.anchorMax, k), v => rt.anchorMax = With2(rt.anchorMax, k, v), false),
                "m_Pivot" => (() => Get2(rt.pivot, k), v => rt.pivot = With2(rt.pivot, k, v), false),
                _ => default,
            };
        }

        /// <summary>A serialized member by its serialized name: prefer the public property (it marks things dirty), else the field.</summary>
        static (Func<float>, Action<float>, bool) MemberSlot(Component comp, string head, string part)
        {
            const BindingFlags f = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            string plain = head.StartsWith("m_", StringComparison.Ordinal) && head.Length > 2 ? char.ToLowerInvariant(head[2]) + head[3..] : head;
            Type type = comp.GetType();
            PropertyInfo prop = null;
            for (var tt = type; tt != null && prop == null; tt = tt.BaseType)
                prop = tt.GetProperty(plain, f | BindingFlags.DeclaredOnly) is { CanRead: true, CanWrite: true } p && p.GetIndexParameters().Length == 0 ? p : null;
            FieldInfo field = null;
            if (prop == null)
                for (var tt = type; tt != null && field == null; tt = tt.BaseType)
                    field = tt.GetField(head, f | BindingFlags.DeclaredOnly) ?? tt.GetField(plain, f | BindingFlags.DeclaredOnly);
            Type vt = prop?.PropertyType ?? field?.FieldType;
            if (vt == null) return default;
            Func<object> get = prop != null ? () => prop.GetValue(comp) : () => field.GetValue(comp);
            Action<object> set = prop != null ? v => prop.SetValue(comp, v) : v => field.SetValue(comp, v);

            if (part == null)
            {
                if (vt == typeof(float)) return (() => (float)get(), v => set(v), false);
                if (vt == typeof(int)) return (() => (int)get(), v => set((int)MathF.Round(v)), false);
                if (vt == typeof(bool)) return (() => (bool)get() ? 1f : 0f, v => set(v > 0.5f), true);
                return default;
            }
            int k = Axis(part);
            if (k < 0) return default;
            if (vt == typeof(Color))
                return (() => GetC((Color)get(), k), v => set(WithC((Color)get(), k, v)), false);
            if (vt == typeof(Vector3))
                return (() => Get((Vector3)get(), k), v => set(With((Vector3)get(), k, v)), false);
            if (vt == typeof(Vector2) && k < 2)
                return (() => Get2((Vector2)get(), k), v => set(With2((Vector2)get(), k, v)), false);
            if (vt == typeof(Vector4))
                return (() => Get4((Vector4)get(), k), v => set(With4((Vector4)get(), k, v)), false);
            return default;
        }

        static int Axis(string part) => part switch
        {
            "x" or "r" => 0, "y" or "g" => 1, "z" or "b" => 2, "w" or "a" => 3, _ => -1,
        };

        static float Get(Vector3 v, int k) => k switch { 0 => v.x, 1 => v.y, 2 => v.z, _ => 0f };
        static Vector3 With(Vector3 v, int k, float x) { if (k == 0) v.x = x; else if (k == 1) v.y = x; else if (k == 2) v.z = x; return v; }
        static float Get2(Vector2 v, int k) => k == 0 ? v.x : v.y;
        static Vector2 With2(Vector2 v, int k, float x) { if (k == 0) v.x = x; else v.y = x; return v; }
        static float Get4(Vector4 v, int k) => k switch { 0 => v.x, 1 => v.y, 2 => v.z, _ => v.w };
        static Vector4 With4(Vector4 v, int k, float x) { if (k == 0) v.x = x; else if (k == 1) v.y = x; else if (k == 2) v.z = x; else v.w = x; return v; }
        static float GetQ(Quaternion q, int k) => k switch { 0 => q.x, 1 => q.y, 2 => q.z, _ => q.w };
        static Quaternion WithQ(Quaternion q, int k, float x) { if (k == 0) q.x = x; else if (k == 1) q.y = x; else if (k == 2) q.z = x; else q.w = x; return q; }
        static float GetC(Color c, int k) => k switch { 0 => c.r, 1 => c.g, 2 => c.b, _ => c.a };
        static Color WithC(Color c, int k, float x) { if (k == 0) c.r = x; else if (k == 1) c.g = x; else if (k == 2) c.b = x; else c.a = x; return c; }
    }
}
