using System.Collections.Generic;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Keeps the game's own loading screen (the splash <see cref="SceneLoader"/> lifts once the new scene's client is ready)
    /// up until the scene says its opening is ready to be seen. A scene that builds its world after the client is ready
    /// claims the hold before that moment (in <c>Awake</c>) and releases it when the world stands, so the player sees the
    /// one loading screen every game has and then the scene's first shot, never the build, the pilot flying blind, or a
    /// second loading screen raised over it (the voyage scene, Shombith 2026-11-17: "only the intro scene and the loading
    /// scene each game gets").
    /// <para>Every claim carries its own deadline: a claim that outlives it lets go with a warning, and a claim whose owner
    /// was destroyed lets go silently, so a scene that fails never leaves the player behind the splash.</para>
    /// </summary>
    public static class SceneRevealHold
    {
        static readonly Dictionary<object, (float until, string why)> s_claims = new();
        static readonly List<object> s_expired = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_claims.Clear();

        /// <summary>Hold the splash for <paramref name="owner"/> for at most <paramref name="maxSeconds"/> (unscaled).
        /// A second claim by the same owner moves its deadline.</summary>
        public static void Claim(object owner, float maxSeconds, string why)
        {
            if (owner == null) return;
            s_claims[owner] = (Time.unscaledTime + Mathf.Max(0f, maxSeconds), why ?? owner.ToString());
        }

        /// <summary>Let go of <paramref name="owner"/>'s claim (no-op when it holds none).</summary>
        public static void Release(object owner)
        {
            if (owner != null) s_claims.Remove(owner);
        }

        /// <summary>True while <paramref name="owner"/> holds a live claim.</summary>
        public static bool Holds(object owner) => owner != null && IsHeld && s_claims.ContainsKey(owner);

        /// <summary>True while any live claim holds the splash.</summary>
        public static bool IsHeld
        {
            get
            {
                if (s_claims.Count == 0) return false;
                float now = Time.unscaledTime;
                s_expired.Clear();
                foreach (var (owner, claim) in s_claims)
                {
                    if (owner is Object uo && !uo)
                    {
                        s_expired.Add(owner);
                    }
                    else if (now > claim.until)
                    {
                        CSDebug.LogWarning($"[SceneRevealHold] '{claim.why}' still held the loading screen at its deadline - " +
                                           "revealing the scene anyway.");
                        s_expired.Add(owner);
                    }
                }
                foreach (var owner in s_expired) s_claims.Remove(owner);
                return s_claims.Count > 0;
            }
        }
    }
}
