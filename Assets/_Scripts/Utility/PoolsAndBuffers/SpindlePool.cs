using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Reuses flora limbs (<see cref="Spindle"/>) the way <see cref="EnvironmentPrismPool"/> reuses their
    /// prisms. A growing garden made and destroyed a limb GameObject per growth step, and the clone
    /// (produce, copy, Awake, Start) was the largest in-game stall measured in Bloomrush and Sirocco
    /// (profiler capture of a built player, 2026-11-21).
    ///
    /// <para>A limb comes back only when it evaporated on its own with nothing left under it
    /// (<see cref="TryRelease"/>); a limb destroyed with its plant or its parent limb is simply gone,
    /// and the pool skips the dead entry. A reused limb is handed out in the state a fresh clone is
    /// in before its Start, and its Start work (<see cref="Spindle.Begin"/>) runs at the top of the
    /// next frame, which is when Unity would have run Start: the caller poses and scales it first,
    /// exactly as it does a clone.</para>
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class SpindlePool : MonoBehaviour
    {
        /// <summary>Idle limbs kept per prefab. Past this a returned limb is destroyed as before.</summary>
        const int MaxIdlePerPrefab = 4096;

        static SpindlePool s_host;
        static Dictionary<int, Stack<Spindle>> s_stacks = new(8);
        static List<Spindle> s_pendingBegin = new(256);

        /// <summary>Diagnostic counters since the last domain reload: limbs cloned, reused and taken back.</summary>
        public static int Minted { get; private set; }
        public static int Reused { get; private set; }
        public static int Released { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_host = null;
            s_stacks = new Dictionary<int, Stack<Spindle>>(8);
            s_pendingBegin = new List<Spindle>(256);
            Minted = Reused = Released = 0;
        }

        static bool EnsureHost()
        {
            if (s_host) return true;
            if (!Application.isPlaying) return false;
            var go = new GameObject(nameof(SpindlePool)) { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            s_host = go.AddComponent<SpindlePool>();
            return true;
        }

        /// <summary>
        /// A limb of <paramref name="prefab"/> at a world pose under <paramref name="parent"/> (null: the
        /// active scene's root), reused when one is idle and cloned otherwise. The result is in the
        /// state <c>Instantiate(prefab, position, rotation, parent)</c> returns.
        /// </summary>
        public static Spindle Get(Spindle prefab, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            int prefabId = prefab.GetInstanceID();
            if (EnsureHost() && s_stacks.TryGetValue(prefabId, out var stack))
            {
                while (stack.Count > 0)
                {
                    var reused = stack.Pop();
                    if (!reused) continue;   // destroyed while idle (the host scene never unloads, but be safe)

                    var t = reused.transform;
                    t.SetParent(parent, worldPositionStays: false);
                    if (!parent) SceneManager.MoveGameObjectToScene(reused.gameObject, SceneManager.GetActiveScene());
                    t.SetPositionAndRotation(position, rotation);
                    reused.gameObject.SetActive(true);
                    reused.BeginPending = true;
                    s_pendingBegin.Add(reused);
                    Reused++;
                    return reused;
                }
            }

            var minted = Instantiate(prefab, position, rotation, parent);
            minted.PoolPrefabId = prefabId;
            Minted++;
            return minted;
        }

        /// <summary>
        /// Takes back a limb that has finished evaporating. False when it was not issued by
        /// <see cref="Get"/>, still carries something a clone would not (a prism, a child limb), or
        /// the pool is full or gone: the caller then destroys it, as it always did.
        /// </summary>
        public static bool TryRelease(Spindle spindle)
        {
            if (!spindle || spindle.PoolPrefabId == 0) return false;
            if (!spindle.gameObject.scene.isLoaded || !spindle.IsBareForPool) return false;
            if (!EnsureHost()) return false;

            if (!s_stacks.TryGetValue(spindle.PoolPrefabId, out var stack))
            {
                stack = new Stack<Spindle>(64);
                s_stacks[spindle.PoolPrefabId] = stack;
            }
            if (stack.Count >= MaxIdlePerPrefab) return false;

            // Deactivate where it stands, so OnDisable deregisters against a loaded scene, then park it.
            spindle.gameObject.SetActive(false);
            spindle.transform.SetParent(s_host.transform, worldPositionStays: false);
            spindle.ResetForPool();
            stack.Push(spindle);
            Released++;
            return true;
        }

        /// <summary>Runs the Start work of every limb reused last frame, before anything else updates.
        /// A limb that is inactive waits, as an inactive clone's Start would.</summary>
        void Update()
        {
            int kept = 0;
            for (int i = 0; i < s_pendingBegin.Count; i++)
            {
                var spindle = s_pendingBegin[i];
                if (!spindle || !spindle.BeginPending) continue;   // destroyed, back in the pool, or listed twice
                if (!spindle.isActiveAndEnabled) { s_pendingBegin[kept++] = spindle; continue; }
                spindle.Begin();
            }
            s_pendingBegin.RemoveRange(kept, s_pendingBegin.Count - kept);
        }
    }
}
