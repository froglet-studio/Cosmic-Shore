using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor.Froglet
{
    /// <summary>One discovered tool: where it lives in the menu and how to launch it.</summary>
    public sealed class FrogletToolEntry
    {
        public string MenuPath;          // full MenuItem path, e.g. "FrogletTools/Game Modes/Prefab Kit"
        public string DisplayName;       // last path segment, or the attribute override
        public string Group;             // middle path segment, e.g. "Game Modes"
        public string Description;
        public FrogletToolCategory Category;
        public int Importance;           // 1..5
        public MethodInfo Method;
        public string DeclaringType;
        public string SourceFile;        // best-effort script asset path, for "Ping script"
        public bool HasAttribute;

        public void Invoke()
        {
            // Route through the menu system so Unity's own validate functions and
            // context (selection, etc.) behave exactly as a manual click would.
            if (!EditorApplication.ExecuteMenuItem(MenuPath))
            {
                // ExecuteMenuItem fails for items that were never registered (e.g. the
                // domain reloaded mid-frame). Fall back to a direct call.
                try { Method?.Invoke(null, null); }
                catch (Exception e)
                {
                    Debug.LogError($"[FrogletTools] Failed to launch '{MenuPath}': {e.InnerException?.Message ?? e.Message}");
                }
            }
        }
    }

    /// <summary>
    /// Discovers every Froglet editor tool by reflecting over <see cref="MenuItem"/> attributes.
    ///
    /// THE CONVENTION: a tool appears in the Froglet Master Tool automatically as soon as its
    /// <c>[MenuItem]</c> path starts with <see cref="Root"/> ("FrogletTools/"). Nothing else is
    /// required - no registration list to maintain, no asset to update. Add
    /// <see cref="FrogletToolAttribute"/> when you want a specific section, importance or
    /// description.
    ///
    /// That prefix is also the only filter: menus belonging to third-party packages (PlayFab,
    /// FMOD, Soap, Quick Scene Pro, ...) live under their own roots and are simply never picked
    /// up, so the board only ever shows tools we own.
    /// </summary>
    public static class FrogletToolRegistry
    {
        public const string Root = "FrogletTools/";

        static List<FrogletToolEntry> _cache;

        public static IReadOnlyList<FrogletToolEntry> All => _cache ??= Discover();

        public static void Refresh() => _cache = null;

        static List<FrogletToolEntry> Discover()
        {
            var found = new List<FrogletToolEntry>();
            var seen = new HashSet<string>();

            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!IsProjectAssembly(asm)) continue;

                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }

                foreach (var type in types)
                {
                    MethodInfo[] methods;
                    try
                    {
                        methods = type.GetMethods(BindingFlags.Static | BindingFlags.Public |
                                                  BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                    }
                    catch { continue; }

                    foreach (var m in methods)
                    {
                        foreach (var mi in m.GetCustomAttributes<MenuItem>(false))
                        {
                            if (mi.validate) continue;                       // validate stubs aren't tools
                            var path = mi.menuItem;
                            if (string.IsNullOrEmpty(path)) continue;
                            path = StripHotkey(path);
                            if (!path.StartsWith(Root, StringComparison.Ordinal)) continue;
                            if (!seen.Add(path)) continue;

                            found.Add(Build(path, m, mi));
                        }
                    }
                }
            }

            return found
                .OrderBy(e => e.Category)
                .ThenByDescending(e => e.Importance)
                .ThenBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        static FrogletToolEntry Build(string path, MethodInfo m, MenuItem mi)
        {
            var attr = m.GetCustomAttribute<FrogletToolAttribute>(false);
            var segments = path.Split('/');
            var leaf = segments[^1];

            // "FrogletTools/Game Modes/Prefab Kit" -> group "Game Modes".
            // "FrogletTools/Prefab Kit"            -> no group.
            string group = null;
            if (segments.Length >= 3)
                group = string.Join("/", segments.Skip(1).Take(segments.Length - 2));

            return new FrogletToolEntry
            {
                MenuPath = path,
                DisplayName = attr?.DisplayName ?? leaf,
                Group = group,
                Description = attr?.Description,
                Category = attr?.Category ?? InferCategory(path, m.DeclaringType),
                Importance = Mathf.Clamp(attr?.Importance ?? InferImportance(mi), 1, 5),
                Method = m,
                DeclaringType = m.DeclaringType?.Name,
                SourceFile = FindScriptPath(m.DeclaringType),
                HasAttribute = attr != null,
            };
        }

        // ── Heuristics for un-annotated tools ────────────────────────────────────

        static FrogletToolCategory InferCategory(string path, Type declaring)
        {
            var hay = (path + " " + (declaring?.Name ?? "")).ToLowerInvariant();

            if (Has(hay, "build", "release", "player")) return FrogletToolCategory.Build;
            if (Has(hay, "benchmark", "performance", "memory", "profil", "texture")) return FrogletToolCategory.Performance;
            // Deliberately not bare "bug" (matches "debug") or "log" (the Logging toolbox is Misc).
            if (Has(hay, "crash", "diagnos", "watchdog", "bug ledger", "bugledger")) return FrogletToolCategory.Diagnostics;
            if (Has(hay, "audit", "validate", "validator", "verify", "integrity")) return FrogletToolCategory.Validation;
            if (Has(hay, "prism", "cell", "lifeform", "crystal", "ecolog", "flora", "fauna", "toybox")) return FrogletToolCategory.Ecology;
            if (Has(hay, "vessel", "ship", "hud", "elemental", "petal", "rig", "ability")) return FrogletToolCategory.Vessels;
            if (Has(hay, "game mode", "gamemode", "minigame", "end game", "endcondition", "end condition", "arcade")) return FrogletToolCategory.GameModes;
            if (Has(hay, "canvas", "toast", "dialogue", "ui", "raycast", "notification")) return FrogletToolCategory.Interface;
            if (Has(hay, "playfab", "ugs", "product", "leaderboard", "cloud")) return FrogletToolCategory.Services;
            if (Has(hay, "scene", "setup", "spawn", "photobooth", "recording")) return FrogletToolCategory.SceneSetup;
            return FrogletToolCategory.Misc;
        }

        static bool Has(string haystack, params string[] needles)
            => needles.Any(n => haystack.Contains(n, StringComparison.Ordinal));

        /// <summary>
        /// Unity's MenuItem priority is a menu-ordering hint, not an importance score, and most
        /// tools leave it at the 1000 default. Map the few that set one onto 1..5 and give
        /// everything else the neutral 3.
        /// </summary>
        static int InferImportance(MenuItem mi)
        {
            var p = mi.priority;
            if (p is <= 0 or >= 1000) return 3;
            if (p < 50) return 5;
            if (p < 150) return 4;
            if (p < 400) return 3;
            return 2;
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        static bool IsProjectAssembly(Assembly asm)
        {
            var n = asm.GetName().Name;
            return n is "Assembly-CSharp-Editor" or "Assembly-CSharp"
                   || n.StartsWith("CosmicShore.", StringComparison.Ordinal);
        }

        /// <summary>MenuItem paths may carry a hotkey suffix (" %&amp;m"); strip it for display.</summary>
        public static string StripHotkey(string path)
        {
            int i = path.IndexOf(" %", StringComparison.Ordinal);
            if (i < 0) i = path.IndexOf(" #", StringComparison.Ordinal);
            if (i < 0) i = path.IndexOf(" &", StringComparison.Ordinal);
            if (i < 0) i = path.IndexOf(" _", StringComparison.Ordinal);
            return i > 0 ? path[..i] : path;
        }

        static readonly Dictionary<Type, string> ScriptPathCache = new();

        static string FindScriptPath(Type t)
        {
            if (t == null) return null;
            if (ScriptPathCache.TryGetValue(t, out var cached)) return cached;

            string result = null;
            foreach (var guid in AssetDatabase.FindAssets($"{t.Name} t:MonoScript"))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                var ms = AssetDatabase.LoadAssetAtPath<MonoScript>(p);
                if (ms != null && ms.GetClass() == t) { result = p; break; }
            }
            ScriptPathCache[t] = result;
            return result;
        }
    }
}
