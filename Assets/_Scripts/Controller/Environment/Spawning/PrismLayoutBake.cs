using System;
using System.Collections.Generic;
using CosmicShore.Utility;
using CosmicShore.Utility.PerformanceBenchmark;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Serves a cell environment's layout from its BAKE (<see cref="PrismLayoutBakeFormat"/>)
    /// instead of running its generator while the game loads. Called by
    /// <see cref="CellEnvironmentSpawnableBase"/> before it would generate; a miss always falls
    /// back to live generation, so a bake is purely a load-time saving and never a correctness
    /// dependency.
    ///
    /// <para>The bake is loaded on demand from <c>Resources</c> and unloaded as soon as it is
    /// decoded, so a menu that references every world (the Cell Selector) keeps none of their
    /// bytes resident.</para>
    ///
    /// <para>A world with no bake for its current parameters is generated live and warns once
    /// (per type and fingerprint), naming the command that bakes it: in a player build that is a
    /// load-time regression nobody would otherwise see. In the Editor a bake made from an older
    /// version of the generator's own script is skipped the same way, so a code edit shows in Play
    /// mode before anyone re-bakes.</para>
    /// </summary>
    public static class PrismLayoutBake
    {
        static int _bypass;
        static readonly HashSet<string> _warned = new();

        /// <summary>True inside a <see cref="Bypass"/> scope: every world generates live.</summary>
        public static bool Bypassed => _bypass > 0;

        /// <summary>
        /// Generate live inside this scope, ignoring every bake:
        /// <c>using (PrismLayoutBake.Bypass()) { env.InvalidateCache(); env.GetTrailData(); }</c>.
        /// For the edit-mode test that proves a bake equals its generator, and for anyone tuning a
        /// generator's code in the editor.
        /// </summary>
        public static Scope Bypass() => new(1);

        public readonly struct Scope : IDisposable
        {
            readonly int _delta;
            public Scope(int delta) { _delta = delta; _bypass += delta; }
            public void Dispose() => _bypass -= _delta;
        }

        /// <summary>
        /// The baked lays and planting sites for <paramref name="env"/>'s current parameters, or
        /// false (generate live) when bypassed, missing, or not a bake of those parameters.
        /// </summary>
        public static bool TryLoad(CellEnvironmentSpawnableBase env, out List<PrismLay> lays,
            out List<FloraPlantingSite> sites)
        {
            lays = null;
            sites = null;
            if (_bypass > 0) return false;

            using var _ = LoadInsights.IsRecording
                ? LoadInsights.Measure(LoadInsightCategory.Environment, $"Prism layout bake: {env.GetType().Name}")
                : LoadSpanScope.None;

            ulong fingerprint = PrismLayoutBakeFormat.Fingerprint(env);
            var asset = Resources.Load<TextAsset>(PrismLayoutBakeFormat.ResourcePath(env.GetType(), fingerprint));
            if (asset == null)
            {
                if (_warned.Add($"missing {fingerprint:x16}"))
                    CSDebug.LogWarning($"[PrismLayoutBake] {env.GetType().Name} ({env.name}) has no baked layout for its " +
                                       $"current parameters (fingerprint {fingerprint:x16}), so it is generated live and " +
                                       "costs load time. Bake it: python3 Tools/Build/bake_prism_layouts.py", env);
                return false;
            }

            string file = asset.name;
            byte[] data = asset.bytes;   // a copy per access, so read it once
            Resources.UnloadAsset(asset);
#if UNITY_EDITOR
            if (!SourceMatches(env, data, out string script))
            {
                if (_warned.Add($"source {fingerprint:x16}"))
                    CSDebug.LogWarning($"[PrismLayoutBake] {script} changed since {file} was baked, so {env.GetType().Name} " +
                                       "is generated live in the Editor (a player build still reads the old bake). " +
                                       "Re-bake: python3 Tools/Build/bake_prism_layouts.py", env);
                return false;
            }
#endif
            bool ok = PrismLayoutBakeFormat.TryDecode(data, fingerprint, out lays, out sites);
            if (!ok && _warned.Add($"invalid {fingerprint:x16}"))
                CSDebug.LogWarning($"[PrismLayoutBake] {file} is not a valid bake of {env.GetType().Name} " +
                                   $"(fingerprint {fingerprint:x16}); generating live. Re-bake: " +
                                   "python3 Tools/Build/bake_prism_layouts.py", env);
            return ok;
        }

#if UNITY_EDITOR
        /// <summary>The bake was made from the generator script the Editor is running (or that
        /// cannot be told, in which case the bake is trusted as the player trusts it).</summary>
        static bool SourceMatches(CellEnvironmentSpawnableBase env, byte[] data, out string script)
        {
            var mono = MonoScript.FromMonoBehaviour(env);
            script = mono != null ? AssetDatabase.GetAssetPath(mono) : env.GetType().Name;
            return mono == null || !PrismLayoutBakeFormat.TryReadSourceHash(data, out ulong baked) ||
                   baked == PrismLayoutBakeFormat.SourceHash(mono.text);
        }
#endif
    }
}
