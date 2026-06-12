using System;

namespace CosmicShore.Engine
{
    /// <summary>
    /// Root of the engine object model. Preserves the destroyed-object equality contract
    /// the ported code relies on everywhere: after destruction completes, an instance
    /// compares equal to null and converts to <c>false</c>, even though the managed
    /// reference still exists.
    /// </summary>
    public abstract class Object
    {
        public virtual string name { get; set; }

        internal bool destroyedFlag;

        /// <summary>True once destruction has completed (end of the Destroy frame).</summary>
        public bool IsDestroyed => destroyedFlag;

        public static implicit operator bool(Object o) => o is not null && !o.destroyedFlag;

        public static bool operator ==(Object a, Object b)
        {
            bool aNull = a is null || a.destroyedFlag;
            bool bNull = b is null || b.destroyedFlag;
            if (aNull && bNull) return true;
            if (aNull || bNull) return false;
            return ReferenceEquals(a, b);
        }

        public static bool operator !=(Object a, Object b) => !(a == b);

        public override bool Equals(object other)
        {
            if (other is null) return destroyedFlag;
            return other is Object o && this == o;
        }

        public override int GetHashCode() => base.GetHashCode();

        public override string ToString() => destroyedFlag ? "null" : $"{name} ({GetType().Name})";

        /// <summary>
        /// Schedule destruction at the end of the current frame (GameObjects/Components),
        /// matching the original engine's deferred-destroy contract. ScriptableObjects are
        /// destroyed immediately.
        /// </summary>
        public static void Destroy(Object obj)
        {
            switch (obj)
            {
                case null:
                    return;
                case ScriptableObject so:
                    so.destroyedFlag = true;
                    return;
                default:
                    if (GameLoop.Current == null)
                        throw new InvalidOperationException(
                            $"Object.Destroy({obj.GetType().Name}) requires an active GameLoop.");
                    GameLoop.Current.QueueDestroy(obj);
                    return;
            }
        }

        /// <summary>Single-scene engine for now: objects survive (nonexistent) scene loads by default.</summary>
        public static void DontDestroyOnLoad(Object target) { }

        public static T FindFirstObjectByType<T>() where T : class
            => GameLoop.Current?.Scene.FindObjectOfType<T>(includeInactive: true);

        /// <summary>Clone an asset or object graph (see ObjectUtilities for semantics).</summary>
        public static T Instantiate<T>(T original) where T : Object
            => ObjectUtilities.InstantiateObject(original);

        /// <summary>Clone and place at a world pose in one step (cell visuals and spawners use this shape).</summary>
        public static T Instantiate<T>(T original, Vector3 position, Quaternion rotation) where T : Object
        {
            var clone = ObjectUtilities.InstantiateObject(original);
            var transform = clone switch
            {
                GameObject go => go.transform,
                Component component => component.transform,
                _ => null,
            };
            if (transform is not null)
            {
                transform.position = position;
                transform.rotation = rotation;
            }
            return clone;
        }

        /// <summary>Clone, place at a world pose, and parent in one step (original engine's 4-arg shape).</summary>
        public static T Instantiate<T>(T original, Vector3 position, Quaternion rotation, Transform parent) where T : Object
        {
            var clone = Instantiate(original, position, rotation);
            var transform = clone switch
            {
                GameObject go => go.transform,
                Component component => component.transform,
                _ => null,
            };
            transform?.SetParent(parent, worldPositionStays: true);
            return clone;
        }

        /// <summary>Clone and parent in one step (pool managers and spawners use this shape).</summary>
        public static T Instantiate<T>(T original, Transform parent, bool instantiateInWorldSpace = false) where T : Object
        {
            var clone = ObjectUtilities.InstantiateObject(original);
            var transform = clone switch
            {
                GameObject go => go.transform,
                Component component => component.transform,
                _ => null,
            };
            transform?.SetParent(parent, instantiateInWorldSpace);
            return clone;
        }

        /// <summary>Destroy synchronously, right now. Prefer <see cref="Destroy"/> in gameplay code.</summary>
        public static void DestroyImmediate(Object obj)
        {
            switch (obj)
            {
                case null:
                    return;
                case ScriptableObject so:
                    so.destroyedFlag = true;
                    return;
                case GameObject go:
                    go.DestroyNow();
                    return;
                case Component c:
                    c.DestroyComponentNow();
                    return;
                default:
                    obj.destroyedFlag = true;
                    return;
            }
        }
    }
}
