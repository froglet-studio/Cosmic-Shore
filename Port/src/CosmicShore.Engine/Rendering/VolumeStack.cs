using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CosmicShore.Engine.Rendering
{
    /// <summary>
    /// The blended post-process state one camera sees (the SRP volume framework's documented
    /// model): every component starts at its defaults, then each enabled volume on a layer
    /// in the camera's volume mask is applied in ascending priority — each OVERRIDDEN
    /// parameter interpolated toward the volume's value by the volume's weight (numbers and
    /// colours blend; other values switch once the weight is non-zero). A global volume
    /// applies everywhere; a local one only while the camera is inside its colliders' bounds,
    /// fading over its blend distance outside them.
    /// </summary>
    public sealed class VolumeStack
    {
        static Type[] s_componentTypes;
        readonly Dictionary<Type, VolumeComponent> _defaults = new();
        readonly Dictionary<Type, VolumeComponent> _stack = new();
        readonly List<Volume> _sorted = new();

        static IEnumerable<Type> ComponentTypes => s_componentTypes ??= typeof(VolumeComponent).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(VolumeComponent).IsAssignableFrom(t) && t.GetConstructor(Type.EmptyTypes) != null)
            .ToArray();

        public VolumeStack()
        {
            foreach (var t in ComponentTypes)
            {
                _defaults[t] = (VolumeComponent)ScriptableObject.CreateInstance(t);
                _stack[t] = (VolumeComponent)ScriptableObject.CreateInstance(t);
            }
        }

        public T GetComponent<T>() where T : VolumeComponent
            => _stack.TryGetValue(typeof(T), out var c) ? (T)c : null;

        public void Update(Vector3 position, int layerMask)
        {
            foreach (var (type, comp) in _stack)
            {
                var defaults = _defaults[type];
                var dst = Params(comp);
                var src = Params(defaults);
                for (int i = 0; i < dst.Length; i++)
                {
                    dst[i].SetBoxed(src[i].GetBoxed());
                    dst[i].overrideState = false;
                }
                comp.active = true;
            }

            _sorted.Clear();
            var world = GameLoop.Current?.Scene;
            foreach (var v in Volume.Active)
                if (v != null && v.isActiveAndEnabled && ReferenceEquals(v.gameObject.scene, world)
                    && ((1 << v.gameObject.layer) & layerMask) != 0 && v.weight > 0f)
                    _sorted.Add(v);
            _sorted.Sort((a, b) => a.priority.CompareTo(b.priority));

            foreach (var volume in _sorted)
            {
                var profile = volume.profileRef;
                if (profile == null) continue;
                float weight = Math.Clamp(volume.weight, 0f, 1f);
                if (!volume.isGlobal) weight *= LocalInfluence(volume, position);
                if (weight <= 0f) continue;

                foreach (var component in profile.components)
                {
                    if (component == null || !component.active) continue;
                    if (!_stack.TryGetValue(component.GetType(), out var target)) continue;
                    var from = Params(component);
                    var to = Params(target);
                    for (int i = 0; i < from.Length && i < to.Length; i++)
                    {
                        if (!from[i].overrideState) continue;
                        to[i].SetBoxed(Blend(to[i].GetBoxed(), from[i].GetBoxed(), weight));
                        to[i].overrideState = true;
                    }
                }
            }
        }

        static float LocalInfluence(Volume volume, Vector3 position)
        {
            float closest = float.PositiveInfinity;
            foreach (var col in volume.GetComponents<Collider>())
            {
                if (col == null || !col.enabled) continue;
                var p = col.ClosestPoint(position);
                closest = Math.Min(closest, (p - position).sqrMagnitude);
            }
            if (float.IsPositiveInfinity(closest)) return 0f;
            if (closest <= 0f) return 1f;
            float blend = volume.blendDistance;
            if (blend <= 0f) return 0f;
            return Math.Clamp(1f - MathF.Sqrt(closest) / blend, 0f, 1f);
        }

        static object Blend(object from, object to, float t) => (from, to) switch
        {
            (float a, float b) => a + (b - a) * t,
            (int a, int b) => (int)MathF.Round(a + (b - a) * t),
            (Color a, Color b) => Color.Lerp(a, b, t),
            (Vector2 a, Vector2 b) => Vector2.Lerp(a, b, t),
            (Vector3 a, Vector3 b) => Vector3.Lerp(a, b, t),
            (Vector4 a, Vector4 b) => Vector4.Lerp(a, b, t),
            _ => t > 0f ? to : from,
        };

        static readonly Dictionary<Type, FieldInfo[]> s_paramFields = new();

        static VolumeParameter[] Params(VolumeComponent c)
        {
            var type = c.GetType();
            if (!s_paramFields.TryGetValue(type, out var fields))
                s_paramFields[type] = fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public)
                    .Where(f => typeof(VolumeParameter).IsAssignableFrom(f.FieldType))
                    .OrderBy(f => f.MetadataToken)
                    .ToArray();
            var result = new VolumeParameter[fields.Length];
            for (int i = 0; i < fields.Length; i++) result[i] = (VolumeParameter)fields[i].GetValue(c);
            return result;
        }
    }
}
