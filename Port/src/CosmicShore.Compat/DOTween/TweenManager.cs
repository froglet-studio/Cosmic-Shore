using System;
using System.Collections.Generic;
using CosmicShore.Engine;

namespace DG.Tweening
{
    /// <summary>
    /// Owns every top-level (non-nested) live tween and advances them from the engine frame
    /// loop. The loop is driven by a hidden <see cref="DOTweenRunner"/> MonoBehaviour on a
    /// DontDestroyOnLoad "[DOTween]" GameObject — the same shape as the original package — so
    /// tweens tick in the ordinary Update / LateUpdate / FixedUpdate phases with no engine hook.
    /// The runner is (re)created lazily per <see cref="GameLoop"/>; a new world drops the old
    /// world's tweens (their targets died with it).
    /// </summary>
    internal static class TweenManager
    {
        static readonly List<Tween> _active = new();
        static Tween[] _scratch = new Tween[64];
        static GameLoop _loop;
        static DOTweenRunner _runner;

        internal static int ActiveCount { get { SyncWorld(); return _active.Count; } }

        internal static IReadOnlyList<Tween> Active { get { SyncWorld(); return _active; } }

        static void SyncWorld()
        {
            var current = GameLoop.Current;
            if (ReferenceEquals(current, _loop)) return;
            foreach (var t in _active) { t.active = false; t.isPlaying = false; }
            _active.Clear();
            _loop = current;
            _runner = null;
        }

        internal static void Add(Tween t)
        {
            SyncWorld();
            _active.Add(t);
            EnsureRunner();
        }

        internal static void Remove(Tween t) => _active.Remove(t);

        internal static void EnsureRunning(Tween t)
        {
            SyncWorld();
            EnsureRunner();
        }

        static void EnsureRunner()
        {
            if (_loop is null) return; // no world yet: tweens advance only via DOTween.ManualUpdate
            if (_runner is not null && !_runner.IsDestroyed && !_runner.gameObject.IsDestroyed) return;
            var go = new GameObject("[DOTween]");
            _runner = go.AddComponent<DOTweenRunner>();
            CosmicShore.Engine.Object.DontDestroyOnLoad(go);
        }

        /// <summary>Advance every live tween registered for <paramref name="type"/>.</summary>
        internal static void Update(UpdateType type, float deltaTime, float unscaledDeltaTime)
        {
            SyncWorld();
            int count = _active.Count;
            if (count == 0) return;
            if (_scratch.Length < count) _scratch = new Tween[Math.Max(count, _scratch.Length * 2)];
            _active.CopyTo(_scratch, 0); // snapshot: tweens created during this pass wait for the next

            for (int i = 0; i < count; i++)
            {
                var t = _scratch[i];
                _scratch[i] = null;
                if (!t.active || t.updateType != type) continue;
                if (!t.CheckLink() || !t.isPlaying) continue;
                float dt = t.isIndependentUpdate
                    ? unscaledDeltaTime * DOTween.unscaledTimeScale
                    : deltaTime;
                dt *= DOTween.timeScale * t.timeScale;
                t.Advance(dt);
            }
        }

        internal static void Clear()
        {
            foreach (var t in _active) { t.active = false; t.isPlaying = false; }
            _active.Clear();
        }

        // ── Filtered operations (DOTween's static API) ──

        internal static bool Matches(Tween t, object targetOrId)
        {
            if (targetOrId == null) return false;
            if (ReferenceEquals(t.target, targetOrId)) return true;
            if (t.id != null && (ReferenceEquals(t.id, targetOrId) || t.id.Equals(targetOrId))) return true;
            if (targetOrId is string s && t.stringId != null && t.stringId == s) return true;
            if (targetOrId is int i && t.intId == i && i != -999) return true;
            return false;
        }

        /// <summary>Apply <paramref name="op"/> to every top-level tween matching the filter; returns the count.</summary>
        internal static int Filtered(object targetOrId, Func<Tween, bool> op, bool matchAll = false)
        {
            SyncWorld();
            if (_active.Count == 0) return 0;
            var snapshot = _active.ToArray();
            int n = 0;
            foreach (var t in snapshot)
            {
                if (!t.active) continue;
                if (!matchAll && !Matches(t, targetOrId)) continue;
                if (op(t)) n++;
            }
            return n;
        }
    }

    /// <summary>Hidden per-world driver that forwards the engine's update phases to the tween manager.</summary>
    [DefaultExecutionOrder(-10000)]
    internal sealed class DOTweenRunner : MonoBehaviour
    {
        void Update() => TweenManager.Update(UpdateType.Normal, Time.deltaTime, Time.unscaledDeltaTime);

        void LateUpdate() => TweenManager.Update(UpdateType.Late, Time.deltaTime, Time.unscaledDeltaTime);

        // Time.deltaTime reports fixedDeltaTime inside the fixed phase (engine contract).
        void FixedUpdate() => TweenManager.Update(UpdateType.Fixed, Time.deltaTime, Time.fixedDeltaTime);
    }
}
