using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CosmicShore.Gameplay;
using CosmicShore.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CosmicShore.Editor.Froglet
{
    /// <summary>
    /// The fork-retirement flow behind <see cref="GameCanvasUnifierWindow"/>: the operations that
    /// take the project from two GameCanvas prefabs (and fifteen scenes each carrying ~1,770
    /// overrides on the fork) to ONE prefab and scenes that carry none.
    ///
    /// Three operations, each with a dry run that reports exactly what the real run would do:
    ///
    /// <list type="number">
    /// <item><see cref="Absorb"/> — make <c>CORE/GameCanvas.prefab</c> equal to the canvas the
    /// fork scenes actually SHIP, taken from a donor scene: its instance is unpacked in memory
    /// (outermost root only, so nested prefabs stay nested) and merged into the CORE prefab's
    /// contents by hierarchy path — objects the shipped canvas lacks are deleted, objects it adds
    /// are moved in, components are paired / script-swapped / added, values are copied, and every
    /// reference is remapped. CORE keeps every fileID it already has, so the ten scenes already on
    /// it stay valid without being touched.</item>
    /// <item><see cref="Repoint"/> — replace a scene's fork instance with a CORE instance at the
    /// same place, preserving only what the caller allow-listed (survivor overrides), any
    /// scene-added objects the prefab does not now carry, and every scene-side reference INTO the
    /// canvas (controllers' <c>countdownTimer</c>, volume UI, added-panel parents…), resolved by
    /// hierarchy path.</item>
    /// <item><see cref="DeleteFork"/> — once nothing references the fork guid.</item>
    /// </list>
    ///
    /// Above all three sits the <b>canvas contract</b>: the one in-game canvas is a
    /// Scale-With-Screen-Size canvas at <b>1920x1080</b> with an <see cref="AdaptiveCanvasScaler"/>
    /// driving the width/height match from the live aspect ratio. <see cref="FixPrefab"/> absorbs
    /// (when there is still a fork to absorb) and then ENFORCES that contract on CORE — through the
    /// Canvas Upgrader (<see cref="CanvasUpgradeProcessor"/>) when the canvas is still authored at
    /// 800x450, so every rect is rescaled with it rather than left tiny — and <see cref="Repoint"/>
    /// REFUSES to put a scene on a CORE that is not at the contract. That guard exists because the
    /// first re-point was run before the absorb and silently handed Skim Race an 800x450 canvas:
    /// both prefab assets are authored at 800x450 and only the scene overrides said 1920x1080.
    /// <see cref="FixScene"/> is the per-scene entry point: a fork scene is re-pointed, a CORE scene
    /// has every override that now merely repeats the prefab's value dropped (same settings, no
    /// override wall).
    ///
    /// Why a donor SCENE rather than the fork prefab asset: the shipped canvas is the fork MINUS
    /// nine objects and three components PLUS three scene-added components and two added
    /// objects, identical across twelve scenes. The prefab asset was never what ran.
    /// Measured by <c>Tools/Build/gamecanvas_unification_report.py</c>; record in
    /// <c>Docs/GAMECANVAS.md</c>.
    ///
    /// Every read of scene YAML goes through <see cref="PrefabInstanceSceneScanner"/>; every write
    /// here goes through <c>PrefabUtility</c> / <c>SerializedObject</c> on loaded content.
    /// </summary>
    public static class GameCanvasUnifier
    {
        public const string ToolName = "GameCanvas Unifier";
        public const string CorePrefabPath = "Assets/_Prefabs/CORE/GameCanvas.prefab";
        public const string ForkPrefabPath = "Assets/_Prefabs/GameCanvas-SkimRace.prefab";
        public const string CoreGuid = "65bf1ed35b752374ca46ae214710e41c";
        public const string ForkGuid = "abd30ad4cfca9ae4a8aecfde9f650cf3";

        /// <summary>
        /// The 12-scene family whose canvas edits are byte-identical; any of them is a valid donor.
        /// Rampage is the default because it carries the majority value on every divergent key.
        /// </summary>
        public const string DefaultDonorScene = "Assets/_Scenes/Multiplayer Scenes/MinigameRampage.unity";

        /// <summary>The one reference resolution the in-game canvas is allowed to have.</summary>
        public static readonly Vector2 ReferenceResolution = CanvasUpgradeProcessor.NewResolution;   // 1920x1080

        // ── Log ──────────────────────────────────────────────────────────────────

        public sealed class Log
        {
            public readonly List<string> Lines = new();
            public readonly List<string> Warnings = new();
            public bool Ok => Warnings.Count == 0;

            public void Info(string s) => Lines.Add(s);
            public void Warn(string s) { Warnings.Add(s); Lines.Add("⚠ " + s); }

            public override string ToString()
            {
                var sb = new StringBuilder();
                foreach (var l in Lines) sb.AppendLine(l);
                return sb.ToString();
            }
        }

        // ── Options ──────────────────────────────────────────────────────────────

        public sealed class AbsorbOptions
        {
            public string DonorScenePath = DefaultDonorScene;

            /// <summary>
            /// The donor's <c>EventDrivenStatsProvider.statsToTrack</c> is that mode's list; the
            /// shared prefab must carry NONE so <c>Resources/GameModeStatsProfile</c> decides.
            /// </summary>
            public bool ClearStatsToTrack = true;
        }

        public sealed class RepointOptions
        {
            /// <summary>
            /// propertyPath prefixes whose overrides survive the re-point (re-applied by hierarchy
            /// path). Empty by default: the one real per-mode value moved into
            /// <c>GameModeStatsProfile</c>, so nothing needs to live on the scene.
            /// </summary>
            public List<string> SurvivorPropertyPrefixes = new();

            /// <summary>Also keep overrides that reference scene-local objects (default: drop; the canvas resolves those itself).</summary>
            public bool KeepSceneReferenceOverrides;

            /// <summary>
            /// A scene-added object whose NAME already exists under the same parent in the prefab
            /// (Joust's second NotificationUI beside the prefab's nested one) is a leftover of a
            /// removed-then-re-added object, not content; it is dropped unless this is set.
            /// </summary>
            public bool CarrySameNamedAdditions;
        }

        // ── 0. The canvas contract ───────────────────────────────────────────────

        public sealed class ContractStatus
        {
            public bool Loads;
            public Vector2 Resolution;
            public bool ScaleWithScreenSize;
            public bool HasAdaptiveScaler;
            /// <summary>True once the shipped canvas has been absorbed (CORE's HUD is the MultiplayerHUD the fork scenes run).</summary>
            public bool Absorbed;
            public bool AtContract => Loads && ScaleWithScreenSize && HasAdaptiveScaler && Near(Resolution, ReferenceResolution);
            public string Summary => !Loads ? "CORE/GameCanvas.prefab does not load"
                : $"{Resolution.x:0}x{Resolution.y:0}" + (ScaleWithScreenSize ? "" : ", not Scale-With-Screen-Size")
                  + (HasAdaptiveScaler ? "" : ", no AdaptiveCanvasScaler") + (Absorbed ? "" : ", shipped canvas not absorbed yet");
        }

        /// <summary>Read-only: where <c>CORE/GameCanvas.prefab</c> stands against the contract.</summary>
        public static ContractStatus CoreStatus()
        {
            var st = new ContractStatus();
            var core = AssetDatabase.LoadAssetAtPath<GameObject>(CorePrefabPath);
            if (core == null) return st;
            st.Loads = true;
            var scaler = core.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                st.Resolution = scaler.referenceResolution;
                st.ScaleWithScreenSize = scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize;
            }
            st.HasAdaptiveScaler = core.GetComponent<AdaptiveCanvasScaler>() != null;
            st.Absorbed = core.GetComponentsInChildren<Component>(true).Any(c => c != null && c.GetType().Name == "MultiplayerHUD");
            return st;
        }

        static bool Near(Vector2 a, Vector2 b) => Mathf.Abs(a.x - b.x) < 0.5f && Mathf.Abs(a.y - b.y) < 0.5f;

        /// <summary>
        /// The ONE prefab-side entry point. While the fork still exists and CORE has not absorbed
        /// the shipped canvas, this is <see cref="Absorb"/> (which ends by applying the contract);
        /// afterwards it re-applies the contract alone, so it is safe to run again at any time.
        /// </summary>
        public static Log FixPrefab(bool dryRun)
        {
            var status = CoreStatus();
            if (System.IO.File.Exists(ForkPrefabPath) && !status.Absorbed)
                return Absorb(new AbsorbOptions(), dryRun);

            var log = new Log();
            if (!status.Loads) { log.Warn($"{CorePrefabPath} not found."); return log; }
            GameObject root = null;
            try
            {
                root = PrefabUtility.LoadPrefabContents(CorePrefabPath);
                log.Info($"{CorePrefabPath}: {status.Summary}");
                if (dryRun) { DescribeMissingScripts(root, log); RevertNulledNestedReferences(root, log, dryRun: true); DescribeContract(root, log); log.Info("DRY RUN — nothing written."); return log; }
                StripMissingScripts(root, log);
                RevertNulledNestedReferences(root, log, dryRun: false);
                ApplyCanvasContract(root, log);
                PrefabUtility.SaveAsPrefabAsset(root, CorePrefabPath, out var saved);
                if (!saved) log.Warn($"SaveAsPrefabAsset reported failure for {CorePrefabPath}.");
                else { log.Info($"Saved {CorePrefabPath}."); FrogletToolChangeLedger.Record(ToolName, CorePrefabPath); }
            }
            catch (Exception e) { log.Warn($"Fix prefab aborted: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
            finally
            {
                if (root != null) PrefabUtility.UnloadPrefabContents(root);
                AssetDatabase.SaveAssets();
            }
            return log;
        }

        /// <summary>
        /// Unity refuses to save a prefab that carries a component whose script no longer exists
        /// ("You are trying to save a Prefab with a missing script"), and CORE shipped with one:
        /// an old end-game view (script guid 1b511b9b…, no .cs in the project) added onto the
        /// nested EndGameStatsPanel. Component pairing cannot see it (a missing script has no
        /// type, and GetComponents hands back null), so it is swept explicitly before any save.
        /// The shipped canvas carries none — a missing script never runs — so anything missing in
        /// CORE is dead by definition and removing it changes nothing at runtime.
        /// </summary>
        static void StripMissingScripts(GameObject root, Log log)
        {
            int total = 0;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                int n = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                if (n == 0) continue;
                int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
                total += removed;
                log.Info($"   removed {removed} missing-script component(s) from '{RelPath(t, root.transform)}' (a prefab cannot be saved with one)");
                if (removed < n) log.Warn($"'{RelPath(t, root.transform)}' still carries {n - removed} missing-script component(s); the save will fail — remove them in the nested prefab asset.");
            }
            if (total == 0) log.Info("   no missing scripts");
        }

        /// <summary>
        /// Serialized fields a nested prefab may legitimately leave empty because the component
        /// resolves them itself at runtime (the canvas finds its MiniGameControllerBase).
        /// </summary>
        static readonly HashSet<string> RuntimeResolvedFields = new() { "gameController" };

        /// <summary>
        /// CORE nests other prefabs (NotificationUI, the pause menu, ...), and an override on one
        /// of those instances that NULLS a script-declared reference is the dangerous shape: the
        /// nested asset looks correctly wired and the feature quietly does nothing.
        /// <c>GameToastView.itemPrefab</c> shipped exactly that way, so every toast in every mode
        /// logged "Missing references" and drew nothing. Reverting the override lets the nested
        /// prefab's own wiring apply; Unity built-in properties (<c>m_*</c>) are left alone.
        /// </summary>
        static int RevertNulledNestedReferences(GameObject root, Log log, bool dryRun)
        {
            int total = 0;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var go = t.gameObject;
                if (!PrefabUtility.IsAnyPrefabInstanceRoot(go)) continue;
                var mods = PrefabUtility.GetPropertyModifications(go);
                if (mods == null) continue;
                foreach (var m in mods)
                {
                    if (m.target == null || m.objectReference != null || m.propertyPath.StartsWith("m_")) continue;
                    if (!string.IsNullOrEmpty(m.value) || RuntimeResolvedFields.Contains(m.propertyPath)) continue;
                    // A nulled objectReference with an empty value: the override sets a reference field to nothing.
                    var comp = m.target as Component;
                    if (comp == null) continue;
                    var so = new SerializedObject(comp);
                    var sp = so.FindProperty(m.propertyPath);
                    if (sp == null || sp.propertyType != SerializedPropertyType.ObjectReference) continue;
                    total++;
                    string where = $"'{RelPath(t, root.transform)}' {comp.GetType().Name}.{m.propertyPath}";
                    if (dryRun) { log.Info($"   WOULD revert override that nulls {where} (the nested prefab's own wiring applies instead)"); continue; }
                    var instComp = FindInstanceComponent(go, comp);
                    if (instComp == null) { log.Warn($"   could not locate the instance component for {where}; revert it by hand"); continue; }
                    var isp = new SerializedObject(instComp).FindProperty(m.propertyPath);
                    if (isp == null) { log.Warn($"   could not locate property {m.propertyPath} on the instance for {where}; revert it by hand"); continue; }
                    PrefabUtility.RevertPropertyOverride(isp, InteractionMode.AutomatedAction);
                    log.Info($"   reverted override that nulled {where}");
                }
            }
            if (total == 0) log.Info("   no nested-instance overrides null a reference");
            return total;
        }

        /// <summary>The component on the nested instance whose corresponding-source object is <paramref name="asset"/>.</summary>
        static Component FindInstanceComponent(GameObject instanceRoot, Component asset)
        {
            foreach (var c in instanceRoot.GetComponentsInChildren<Component>(true))
            {
                if (c == null) continue;
                if (PrefabUtility.GetCorrespondingObjectFromSource(c) == asset) return c;
            }
            return null;
        }

        static void DescribeMissingScripts(GameObject root, Log log)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                int n = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                if (n > 0) log.Info($"   WOULD remove {n} missing-script component(s) from '{RelPath(t, root.transform)}' (Unity refuses to save a prefab with one)");
            }
        }

        static void DescribeContract(GameObject root, Log log)
        {
            var scaler = root.GetComponent<CanvasScaler>();
            if (scaler == null) { log.Warn("canvas root has no CanvasScaler — the contract cannot be applied"); return; }
            var res = scaler.referenceResolution;
            log.Info("— contract (1920x1080, Scale-With-Screen-Size, AdaptiveCanvasScaler, smart re-anchor):");
            if (Near(res, CanvasUpgradeProcessor.OldResolution))
                log.Info("   canvas is authored at 800x450: WOULD run the Canvas Upgrader (every rect x2.4, reference 1920x1080, referencePixelsPerUnit x2.4)");
            else if (!Near(res, ReferenceResolution))
                log.Info($"   canvas is at {res.x:0}x{res.y:0} (neither 800x450 nor 1920x1080): WOULD set 1920x1080 WITHOUT rescaling children");
            else log.Info("   canvas already 1920x1080");
            if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize) log.Info($"   WOULD set uiScaleMode {scaler.uiScaleMode} -> ScaleWithScreenSize");
            log.Info(root.GetComponent<AdaptiveCanvasScaler>() != null ? "   AdaptiveCanvasScaler present" : "   WOULD add AdaptiveCanvasScaler");
            log.Info("   WOULD smart re-anchor the canvas's direct children (nearest corner/edge, visual position preserved) so the layout holds on other aspects");
        }

        /// <summary>
        /// Applies the contract to loaded prefab contents. The upgrade and the re-anchor are the
        /// Canvas Upgrader's own passes, so the prefab is fixed exactly the way a scene would be.
        /// </summary>
        static void ApplyCanvasContract(GameObject root, Log log)
        {
            var canvas = root.GetComponent<Canvas>();
            var scaler = root.GetComponent<CanvasScaler>();
            if (canvas == null || scaler == null) { log.Warn("canvas root has no Canvas/CanvasScaler — the contract cannot be applied"); return; }

            if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize)
            {
                log.Info($"   uiScaleMode {scaler.uiScaleMode} -> ScaleWithScreenSize");
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            }

            var entry = new CanvasUpgradeProcessor.CanvasEntry
            {
                Canvas = canvas,
                Scaler = scaler,
                AlreadyUpgraded = Near(scaler.referenceResolution, ReferenceResolution),
            };
            var entries = new List<CanvasUpgradeProcessor.CanvasEntry> { entry };

            if (Near(scaler.referenceResolution, CanvasUpgradeProcessor.OldResolution))
            {
                var counters = new CanvasUpgradeProcessor.UpgradeCounters();
                var report = CanvasUpgradeProcessor.Upgrade(entries, apply: true, addAdaptiveScaler: false, counters);
                log.Info($"   canvas was authored at 800x450: upgraded to 1920x1080 by the Canvas Upgrader ({counters.RectTransforms} rect(s) x2.4; full report in the console)");
                Debug.Log("[GameCanvasUnifier] " + report);
            }
            else if (!entry.AlreadyUpgraded)
            {
                log.Warn($"canvas was at {scaler.referenceResolution.x:0}x{scaler.referenceResolution.y:0} (neither 800x450 nor 1920x1080): set to 1920x1080 WITHOUT rescaling children — check the layout");
                scaler.referenceResolution = ReferenceResolution;
            }
            else log.Info("   canvas already 1920x1080");
            entry.AlreadyUpgraded = true;

            // AdaptiveCanvasScaler drives this from the live aspect; 1 is its value at 16:9.
            scaler.matchWidthOrHeight = 1f;

            if (root.GetComponent<AdaptiveCanvasScaler>() == null)
            {
                root.AddComponent<AdaptiveCanvasScaler>();
                log.Info("   added AdaptiveCanvasScaler (drives matchWidthOrHeight from the live aspect ratio)");
            }

            var reanchor = CanvasUpgradeProcessor.Reanchor(entries, recursive: false, out int changed, out int skipped);
            log.Info($"   smart re-anchor of the canvas's direct children: {changed} re-anchored, {skipped} left as authored (stretched / edge-anchored / layout-driven)");
            if (changed > 0) Debug.Log("[GameCanvasUnifier] " + reanchor);

            EditorUtility.SetDirty(root);
        }

        // ── 1. Report ────────────────────────────────────────────────────────────

        public sealed class SceneRow
        {
            public string ScenePath;
            public string Family;   // CORE / FORK
            public int Overrides;
            public int RemovedGameObjects, RemovedComponents, AddedGameObjects, AddedComponents;
            public string SceneName => System.IO.Path.GetFileNameWithoutExtension(ScenePath);
        }

        /// <summary>Read-only. Every canvas-bearing scene, from YAML, no scene opened.</summary>
        public static List<SceneRow> Report(out int forkReferencingFiles)
        {
            var scenes = PrefabInstanceSceneScanner.FindScenes(new[] { "Assets/_Scenes" });
            var instances = PrefabInstanceSceneScanner.ScanScenes(scenes, new HashSet<string> { CoreGuid, ForkGuid });
            var rows = instances.Select(i => new SceneRow
            {
                ScenePath = i.ScenePath,
                Family = i.SourcePrefabGuid == ForkGuid ? "FORK" : "CORE",
                Overrides = i.Overrides.Count,
                RemovedGameObjects = i.RemovedGameObjects,
                RemovedComponents = i.RemovedComponents,
                AddedGameObjects = i.AddedGameObjects,
                AddedComponents = i.AddedComponents,
            }).OrderBy(r => r.Family).ThenBy(r => r.SceneName).ToList();

            forkReferencingFiles = FilesReferencingGuid(ForkGuid).Count;
            return rows;
        }

        public static List<string> FilesReferencingGuid(string guid)
        {
            var hits = new List<string>();
            var guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" })
                .Concat(AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }));
            foreach (var g in guids.Distinct())
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                if (!path.EndsWith(".unity") && !path.EndsWith(".prefab")) continue;
                try
                {
                    if (System.IO.File.ReadAllText(path).Contains("guid: " + guid, StringComparison.Ordinal))
                        hits.Add(path);
                }
                catch { /* unreadable asset - not ours to report */ }
            }
            return hits;
        }

        public static List<string> ForkScenes()
            => FilesReferencingGuid(ForkGuid).Where(p => p.EndsWith(".unity")).OrderBy(p => p).ToList();

        // ── 2. Absorb ────────────────────────────────────────────────────────────

        /// <summary>
        /// Merge the shipped canvas (the donor scene's fork instance, unpacked one level) into
        /// <c>CORE/GameCanvas.prefab</c>. With <paramref name="dryRun"/> nothing is written and
        /// the log lists every deletion, addition, swap and dropped reference the run would make.
        /// </summary>
        public static Log Absorb(AbsorbOptions opt, bool dryRun)
        {
            var log = new Log();
            opt ??= new AbsorbOptions();

            var corePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CorePrefabPath);
            if (corePrefab == null) { log.Warn($"{CorePrefabPath} not found."); return log; }
            if (string.IsNullOrEmpty(opt.DonorScenePath) || !System.IO.File.Exists(opt.DonorScenePath))
            { log.Warn($"Donor scene '{opt.DonorScenePath}' not found."); return log; }

            if (!PrefabDriftFixer.PrepareForSceneWork()) { log.Warn("Cancelled: unsaved scene changes."); return log; }

            // A clean slate, then the donor ADDITIVELY so it can be closed without saving.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Scene donor;
            try { donor = EditorSceneManager.OpenScene(opt.DonorScenePath, OpenSceneMode.Additive); }
            catch (Exception e) { log.Warn($"Could not open donor: {e.Message}"); return log; }

            GameObject coreRoot = null;
            try
            {
                var oldRoots = PrefabDriftFixer.FindInstanceRoots(donor, ForkGuid);
                if (oldRoots.Count != 1)
                {
                    log.Warn($"Donor '{opt.DonorScenePath}' has {oldRoots.Count} fork instance(s); expected exactly one.");
                    return log;
                }

                var shipped = oldRoots[0];
                // Outermost root only: nested prefabs (CountdownTimer, Pip, NotificationUI,
                // GameOverPanel, the scene-added ConnectingPanel…) stay prefab instances and are
                // carried into CORE as nested instances, never as flattened copies.
                PrefabUtility.UnpackPrefabInstance(shipped, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
                log.Info($"Donor: {opt.DonorScenePath} — shipped canvas unpacked in memory (not saved).");

                coreRoot = PrefabUtility.LoadPrefabContents(CorePrefabPath);
                var plan = BuildAbsorbPlan(shipped, coreRoot, log);
                DescribePlan(plan, log);

                if (dryRun)
                {
                    DescribeMissingScripts(coreRoot, log);
                    DescribeContract(shipped, log);   // CORE's scaler takes the shipped values, then the contract
                    log.Info("DRY RUN — nothing written.");
                    return log;
                }

                ExecuteAbsorbPlan(plan, shipped, coreRoot, corePrefab, opt, log);

                PrefabUtility.SaveAsPrefabAsset(coreRoot, CorePrefabPath, out var saved);
                if (!saved) log.Warn($"SaveAsPrefabAsset reported failure for {CorePrefabPath}.");
                else
                {
                    log.Info($"Saved {CorePrefabPath}.");
                    FrogletToolChangeLedger.Record(ToolName, CorePrefabPath);
                }
            }
            catch (Exception e)
            {
                log.Warn($"Absorb aborted: {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
            }
            finally
            {
                if (coreRoot != null) PrefabUtility.UnloadPrefabContents(coreRoot);
                // Discard the in-memory unpack. If Unity asks about unsaved changes here, answer
                // "Don't Save" — the donor is never meant to be written by this operation.
                EditorSceneManager.CloseScene(donor, true);
                AssetDatabase.SaveAssets();
            }
            return log;
        }

        sealed class AbsorbPlan
        {
            public readonly List<GameObject> Delete = new();                                    // CORE objects to remove (top-most only)
            public readonly List<(GameObject shipped, GameObject coreParent, int sibling)> Add = new();
            public readonly List<(Component shipped, Component core)> Pairs = new();
            public readonly List<(Component shipped, Component core)> Swaps = new();          // core's base type -> shipped's derived type
            public readonly List<(Component shipped, GameObject coreGo)> AddComponents = new();
            public readonly List<Component> RemoveComponents = new();
            public readonly List<(GameObject shipped, GameObject core)> GoPairs = new();
        }

        static AbsorbPlan BuildAbsorbPlan(GameObject shipped, GameObject core, Log log)
        {
            var plan = new AbsorbPlan();
            var sIdx = IndexByPath(shipped.transform);
            var cIdx = IndexByPath(core.transform);

            // Deletions: CORE paths the shipped canvas lacks, top-most only.
            var deleted = new HashSet<string>();
            foreach (var (path, cGo) in cIdx.OrderBy(kv => kv.Key.Length))
            {
                if (path.Length == 0) continue;
                if (sIdx.ContainsKey(path))
                {
                    // Same path but the shipped one is a nested prefab root and CORE's is not the
                    // same asset (the plain NotificationUI vs the nested NotificationUI.prefab):
                    // replace, because a plain object cannot receive a nested instance's overrides.
                    var sGo = sIdx[path];
                    if (IsDifferentNestedRoot(sGo, cGo) && !AnyAncestorIn(path, deleted))
                    {
                        plan.Delete.Add(cGo); deleted.Add(path);
                    }
                    continue;
                }
                if (AnyAncestorIn(path, deleted)) continue;
                plan.Delete.Add(cGo); deleted.Add(path);
            }

            // Additions: shipped paths CORE lacks (or that were just scheduled for replacement), top-most only.
            var added = new HashSet<string>();
            foreach (var (path, sGo) in sIdx.OrderBy(kv => kv.Key.Length))
            {
                if (path.Length == 0) continue;
                bool present = cIdx.ContainsKey(path) && !deleted.Contains(path);
                if (present) continue;
                if (AnyAncestorIn(path, added)) continue;
                var parentPath = ParentPath(path);
                if (!cIdx.TryGetValue(parentPath, out var cParent) || deleted.Contains(parentPath))
                {
                    log.Warn($"Cannot add '{path}': its parent '{parentPath}' is not in CORE either.");
                    continue;
                }
                plan.Add.Add((sGo, cParent, sGo.transform.GetSiblingIndex()));
                added.Add(path);
            }

            // Component pairing on every path present on both sides (and not replaced).
            foreach (var (path, sGo) in sIdx)
            {
                if (!cIdx.TryGetValue(path, out var cGo) || deleted.Contains(path) || AnyAncestorIn(path, deleted)) continue;
                plan.GoPairs.Add((sGo, cGo));
                PairComponents(sGo, cGo, plan);
            }
            return plan;
        }

        static void PairComponents(GameObject sGo, GameObject cGo, AbsorbPlan plan)
        {
            var sComps = sGo.GetComponents<Component>().Where(c => c != null).ToList();
            var cComps = cGo.GetComponents<Component>().Where(c => c != null).ToList();
            var used = new HashSet<Component>();
            var unpaired = new List<Component>();

            // Pass 1: exact type, in order.
            foreach (var sc in sComps)
            {
                if (sc is Transform)
                {
                    plan.Pairs.Add((sc, cGo.transform)); used.Add(cGo.transform);
                    continue;
                }
                var match = cComps.FirstOrDefault(cc => !used.Contains(cc) && cc.GetType() == sc.GetType());
                if (match != null) { plan.Pairs.Add((sc, match)); used.Add(match); }
                else unpaired.Add(sc);
            }
            // Pass 2: CORE holds the BASE type of what the shipped canvas holds (MiniGameHUD →
            // MultiplayerHUD, MinigameHUDView → MultiplayerHUDView): swap the script in place so
            // the fileID - and every scene reference to it - survives.
            var stillUnpaired = new List<Component>();
            foreach (var sc in unpaired)
            {
                var baseMatch = cComps.FirstOrDefault(cc => !used.Contains(cc) && cc is MonoBehaviour && sc is MonoBehaviour
                                                            && cc.GetType() != sc.GetType() && cc.GetType().IsAssignableFrom(sc.GetType()));
                if (baseMatch != null) { plan.Swaps.Add((sc, baseMatch)); used.Add(baseMatch); }
                else stillUnpaired.Add(sc);
            }
            // Pass 3: add what CORE lacks; remove what the shipped canvas lacks.
            foreach (var sc in stillUnpaired) plan.AddComponents.Add((sc, cGo));
            foreach (var cc in cComps)
                if (!used.Contains(cc) && !(cc is Transform)) plan.RemoveComponents.Add(cc);
        }

        static void DescribePlan(AbsorbPlan plan, Log log)
        {
            log.Info($"— plan: delete {plan.Delete.Count} object(s), add {plan.Add.Count} subtree(s), " +
                     $"{plan.Pairs.Count} component pair(s), {plan.Swaps.Count} script swap(s), " +
                     $"{plan.AddComponents.Count} component add(s), {plan.RemoveComponents.Count} component removal(s)");
            foreach (var go in plan.Delete) log.Info($"   − delete   {PathOf(go)}   [{DescribeComponents(go)}]");
            foreach (var (s, p, i) in plan.Add) log.Info($"   + add      {PathOf(p)}/{s.name}  (sibling {i}, {s.GetComponentsInChildren<Transform>(true).Length} object(s), {(PrefabUtility.IsAnyPrefabInstanceRoot(s) ? "nested prefab " + System.IO.Path.GetFileName(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(s)) : "plain")})");
            foreach (var (s, c) in plan.Swaps) log.Info($"   ↻ swap     {PathOf(c.gameObject)} :: {c.GetType().Name} → {s.GetType().Name}");
            foreach (var (s, g) in plan.AddComponents) log.Info($"   + comp     {PathOf(g)} :: {s.GetType().Name}");
            foreach (var c in plan.RemoveComponents) log.Info($"   − comp     {PathOf(c.gameObject)} :: {c.GetType().Name}");
        }

        static void ExecuteAbsorbPlan(AbsorbPlan plan, GameObject shipped, GameObject core, GameObject coreAsset,
                                      AbsorbOptions opt, Log log)
        {
            var map = new Dictionary<UnityEngine.Object, UnityEngine.Object>();   // shipped object -> core object

            StripMissingScripts(core, log);

            foreach (var go in plan.Delete)
            {
                try { UnityEngine.Object.DestroyImmediate(go); }
                catch (Exception e) { log.Warn($"Could not delete '{PathOf(go)}': {e.Message}"); }
            }

            foreach (var (sGo, cParent, sibling) in plan.Add)
            {
                sGo.transform.SetParent(cParent.transform, false);
                sGo.transform.SetSiblingIndex(Mathf.Clamp(sibling, 0, cParent.transform.childCount - 1));
            }

            foreach (var (sGo, cGo) in plan.GoPairs)
            {
                map[sGo] = cGo;
                CopyGameObjectFields(sGo, cGo, isRoot: sGo == shipped);
            }

            foreach (var (sc, cc) in plan.Swaps)
            {
                var so = new SerializedObject(cc);
                var script = MonoScript.FromMonoBehaviour((MonoBehaviour)sc);
                so.FindProperty("m_Script").objectReferenceValue = script;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            // After a swap the C# wrapper for the same fileID reports the new type; re-fetch by index.
            var swappedPairs = plan.Swaps.Select(p => (p.shipped, RefetchComponent(p.core))).ToList();

            var allPairs = plan.Pairs.Concat(swappedPairs).ToList();
            foreach (var (sc, cc) in allPairs)
            {
                if (cc == null) { log.Warn($"Lost a component during swap on {PathOf(sc.gameObject)} :: {sc.GetType().Name}"); continue; }
                map[sc] = cc;
                CopyComponentFields(sc, cc);
            }

            foreach (var (sc, cGo) in plan.AddComponents)
            {
                Component added;
                try { added = cGo.AddComponent(sc.GetType()); }
                catch (Exception e) { log.Warn($"Could not add {sc.GetType().Name} to {PathOf(cGo)}: {e.Message}"); continue; }
                if (added == null) { log.Warn($"AddComponent returned null for {sc.GetType().Name} on {PathOf(cGo)}"); continue; }
                map[sc] = added;
                CopyComponentFields(sc, added);
            }

            foreach (var cc in plan.RemoveComponents)
            {
                try { UnityEngine.Object.DestroyImmediate(cc); }
                catch (Exception e) { log.Warn($"Could not remove {cc.GetType().Name} from {PathOf(cc.gameObject)}: {e.Message}"); }
            }

            RemapReferences(core, coreAsset, map, log);

            if (opt.ClearStatsToTrack)
            {
                foreach (var provider in core.GetComponentsInChildren<EventDrivenStatsProvider>(true))
                {
                    var so = new SerializedObject(provider);
                    var list = so.FindProperty("statsToTrack");
                    if (list != null && list.isArray && list.arraySize > 0)
                    {
                        list.ClearArray();
                        so.ApplyModifiedPropertiesWithoutUndo();
                        log.Info($"   cleared EventDrivenStatsProvider.statsToTrack on {PathOf(provider.gameObject)} (Resources/GameModeStatsProfile decides per mode)");
                    }
                }
            }

            // The contract last: the shipped canvas may arrive already upgraded (the fork scenes
            // were upgraded in-scene, so the unpacked donor carries 1920x1080 and x2.4 rects) or
            // still at the asset's authored 800x450 — either way CORE leaves here at 1920x1080.
            ApplyCanvasContract(core, log);
        }

        static Component RefetchComponent(Component before)
        {
            // Same instanceID, possibly a new managed wrapper type after the m_Script swap.
            var id = before.GetInstanceID();
            return EditorUtility.InstanceIDToObject(id) as Component;
        }

        // ── Field copying ────────────────────────────────────────────────────────

        static readonly HashSet<string> NeverCopy = new()
        {
            "m_ObjectHideFlags", "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset",
            "m_GameObject", "m_Script", "m_EditorClassIdentifier", "m_EditorHideFlags",
            "m_Father", "m_Children", "m_RootOrder", "m_Component",
        };

        static void CopyComponentFields(Component from, Component to)
        {
            var src = new SerializedObject(from);
            var dst = new SerializedObject(to);
            var it = src.GetIterator();
            if (!it.Next(true)) return;
            do
            {
                if (NeverCopy.Contains(it.name)) continue;
                if (dst.FindProperty(it.propertyPath) == null) continue;
                dst.CopyFromSerializedProperty(it);
            } while (it.Next(false));
            dst.ApplyModifiedPropertiesWithoutUndo();
        }

        static void CopyGameObjectFields(GameObject from, GameObject to, bool isRoot)
        {
            var src = new SerializedObject(from);
            var dst = new SerializedObject(to);
            var it = src.GetIterator();
            if (!it.Next(true)) return;
            do
            {
                if (NeverCopy.Contains(it.name)) continue;
                if (isRoot && it.name == "m_Name") continue;   // "GameCanvas" stays "GameCanvas"
                if (dst.FindProperty(it.propertyPath) == null) continue;
                dst.CopyFromSerializedProperty(it);
            } while (it.Next(false));
            dst.ApplyModifiedPropertiesWithoutUndo();
        }

        // ── Reference remap ──────────────────────────────────────────────────────

        static readonly HashSet<string> NeverRemap = new()
        {
            "m_Script", "m_GameObject", "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset",
            "m_Father", "m_Children",
        };

        /// <summary>
        /// Every object reference inside <paramref name="core"/> is made to point INSIDE core:
        /// shipped→core pairs are remapped; a reference into the CORE *asset* (the fork's eight
        /// dangling cross-prefab overrides) resolves to the same path in the loaded contents; a
        /// reference to a scene-local object (the donor's controller) or into some other prefab
        /// asset is dropped and reported. A UnityEvent persistent call whose target was dropped
        /// is deleted outright, so the HUD's self-wiring sees an empty list rather than a dead one.
        /// </summary>
        static void RemapReferences(GameObject core, GameObject coreAsset,
                                    Dictionary<UnityEngine.Object, UnityEngine.Object> map, Log log)
        {
            var coreIdx = IndexByPath(core.transform);
            var assetIdx = coreAsset != null ? IndexByPath(coreAsset.transform) : new Dictionary<string, GameObject>();
            var assetRootT = coreAsset != null ? coreAsset.transform : null;
            int remapped = 0, selfAsset = 0, dropped = 0, templates = 0;

            foreach (var comp in core.GetComponentsInChildren<Component>(true))
            {
                if (comp == null) continue;
                var so = new SerializedObject(comp);
                var it = so.GetIterator();
                var deadCalls = new List<(string arrayPath, int index)>();
                bool changed = false;

                while (it.Next(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                    if (NeverRemap.Contains(it.name)) continue;
                    var v = it.objectReferenceValue;
                    if (v == null) continue;

                    if (map.TryGetValue(v, out var replacement))
                    {
                        it.objectReferenceValue = replacement; changed = true; remapped++;
                        continue;
                    }
                    if (IsUnder(v, core.transform)) continue;

                    if (EditorUtility.IsPersistent(v))
                    {
                        var assetPath = AssetDatabase.GetAssetPath(v);
                        var t = TransformOf(v);
                        if (t == null) continue;   // SO / sprite / material / font - a real asset reference, keep
                        // Only a reference INTO one of the two canvas assets is a cross-prefab
                        // reference to resolve or drop. A reference to any OTHER prefab asset is a
                        // TEMPLATE the canvas instantiates at runtime (DomainScorePanel — the
                        // top-bar column; PlayerScoreEntry; the Goodies stat row) and must be kept
                        // exactly as authored: the first absorb nulled all three, and the top bar
                        // and the scoreboard rows silently stopped drawing.
                        if (assetPath != CorePrefabPath && assetPath != ForkPrefabPath)
                        {
                            templates++;
                            continue;
                        }
                        UnityEngine.Object resolved = null;
                        if (assetPath == CorePrefabPath && assetRootT != null && t.root == assetRootT)
                            resolved = ResolveLike(v, RelPath(t, assetRootT), coreIdx);
                        else
                            resolved = ResolveLike(v, RelPath(t, t.root), coreIdx);   // the fork asset: same path inside core?

                        if (resolved != null)
                        {
                            it.objectReferenceValue = resolved; changed = true; selfAsset++;
                            continue;
                        }
                        log.Info($"   ✕ dropped cross-prefab reference {PathOf(comp.gameObject)} :: {comp.GetType().Name}.{it.propertyPath} → {assetPath}/{RelPath(t, t.root)}");
                    }
                    else
                    {
                        log.Info($"   ✕ dropped scene-local reference {PathOf(comp.gameObject)} :: {comp.GetType().Name}.{it.propertyPath} → {v.name} ({v.GetType().Name})");
                    }

                    it.objectReferenceValue = null; changed = true; dropped++;
                    if (it.name == "m_Target" && TryPersistentCallIndex(it.propertyPath, out var arrayPath, out var index))
                        deadCalls.Add((arrayPath, index));
                }

                foreach (var (arrayPath, index) in deadCalls.OrderByDescending(d => d.index))
                {
                    var arr = so.FindProperty(arrayPath);
                    if (arr != null && arr.isArray && index < arr.arraySize)
                    {
                        arr.DeleteArrayElementAtIndex(index);
                        log.Info($"   − removed dead persistent call #{index} on {PathOf(comp.gameObject)} :: {comp.GetType().Name} ({arrayPath})");
                    }
                }
                if (changed) so.ApplyModifiedPropertiesWithoutUndo();
            }
            log.Info($"— references: {remapped} remapped, {selfAsset} resolved from the CORE asset into contents, {templates} template-prefab reference(s) kept, {dropped} dropped");
        }

        static bool TryPersistentCallIndex(string propertyPath, out string arrayPath, out int index)
        {
            // ...m_PersistentCalls.m_Calls.Array.data[N].m_Target
            arrayPath = null; index = -1;
            const string marker = ".m_Calls.Array.data[";
            var i = propertyPath.LastIndexOf(marker, StringComparison.Ordinal);
            if (i < 0) return false;
            var close = propertyPath.IndexOf(']', i);
            if (close < 0 || !int.TryParse(propertyPath.Substring(i + marker.Length, close - i - marker.Length), out index)) return false;
            arrayPath = propertyPath.Substring(0, i + ".m_Calls".Length);
            return true;
        }

        /// <summary>The object of the same kind (GameObject, or component type + ordinal) at <paramref name="relPath"/> inside <paramref name="idx"/>.</summary>
        static UnityEngine.Object ResolveLike(UnityEngine.Object like, string relPath, Dictionary<string, GameObject> idx)
        {
            if (!idx.TryGetValue(relPath, out var go)) return null;
            if (like is GameObject) return go;
            if (like is Component c)
            {
                var ordinal = OrdinalOf(c);
                var same = go.GetComponents(c.GetType()).Where(x => x != null && x.GetType() == c.GetType()).ToList();
                return ordinal < same.Count ? same[ordinal] : (same.Count > 0 ? same[0] : null);
            }
            return null;
        }

        // ── 3. Re-point ──────────────────────────────────────────────────────────

        /// <summary>
        /// Replace the scene's fork instance with a <c>CORE/GameCanvas</c> instance. See the class
        /// summary for what is preserved. With <paramref name="dryRun"/> the scene is opened and
        /// analysed but never modified or saved.
        /// </summary>
        public static Log Repoint(string scenePath, RepointOptions opt, bool dryRun)
        {
            var log = new Log();
            opt ??= new RepointOptions();

            var corePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CorePrefabPath);
            if (corePrefab == null) { log.Warn($"{CorePrefabPath} not found."); return log; }
            var status = CoreStatus();
            if (!status.AtContract)
            {
                // The guard the first re-point lacked: with CORE still at its authored 800x450, a
                // re-pointed scene silently lost the 1920x1080 it had been carrying as an override.
                log.Warn($"{CorePrefabPath} is not at the canvas contract ({status.Summary}). Run 'Fix prefab' first — " +
                         $"re-pointing now would put {System.IO.Path.GetFileNameWithoutExtension(scenePath)} on a {status.Resolution.x:0}x{status.Resolution.y:0} canvas.");
                return log;
            }
            if (!PrefabDriftFixer.PrepareForSceneWork()) { log.Warn("Cancelled: unsaved scene changes."); return log; }

            Scene scene;
            try { scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single); }
            catch (Exception e) { log.Warn($"Could not open '{scenePath}': {e.Message}"); return log; }

            var olds = PrefabDriftFixer.FindInstanceRoots(scene, ForkGuid);
            if (olds.Count == 0)
            {
                var cores = PrefabDriftFixer.FindInstanceRoots(scene, CoreGuid);
                log.Info(cores.Count > 0
                    ? $"{scenePath}: already on CORE/GameCanvas ({cores.Count} instance(s)). Nothing to do."
                    : $"{scenePath}: no GameCanvas instance found.");
                return log;
            }
            if (olds.Count > 1) { log.Warn($"{scenePath}: {olds.Count} fork instances; re-point one at a time by hand."); return log; }

            var old = olds[0];
            try
            {
                // ── snapshot ─────────────────────────────────────────────────────
                var oldIdx = IndexByPath(old.transform);
                var oldRev = oldIdx.ToDictionary(kv => kv.Value, kv => kv.Key);
                var placement = Placement.Capture(old);

                var survivors = CollectSurvivors(old, oldRev, opt, log, out var droppedOverrides);
                var addedGos = PrefabUtility.GetAddedGameObjects(old)
                    .Select(a => (go: a.instanceGameObject, parentPath: oldRev[a.instanceGameObject.transform.parent.gameObject], sibling: a.siblingIndex))
                    .ToList();
                var addedComps = PrefabUtility.GetAddedComponents(old)
                    .Select(a => (comp: a.instanceComponent, path: oldRev[a.instanceComponent.gameObject]))
                    .ToList();
                var removedComps = PrefabUtility.GetRemovedComponents(old).Count;
                var removedGos = CountRemovedGameObjects(old);
                var refs = CollectSceneReferencesInto(scene, old, oldRev, addedComps.Select(a => a.comp).ToHashSet());

                log.Info($"{scenePath}");
                log.Info($"— instance '{old.name}' at sibling {placement.SiblingIndex}: {survivors.Count} survivor override(s), {droppedOverrides} override(s) dropped, " +
                         $"{addedGos.Count} added object(s), {addedComps.Count} added component(s), {removedGos} removed object(s), {removedComps} removed component(s), " +
                         $"{refs.Count} scene reference(s) into the canvas");
                foreach (var s in survivors) log.Info($"   keep     {s.RelPath} :: {s.TypeName}#{s.Ordinal} . {s.PropertyPath}");

                // Decide the fate of scene additions against the NEW prefab (by path / type).
                var newIdxProbe = IndexByPath(corePrefab.transform);
                foreach (var (go, parentPath, sibling) in addedGos)
                {
                    var fullPath = JoinPath(parentPath, SegmentName(go.transform));
                    var fate = AddedObjectFate(newIdxProbe, fullPath, parentPath, go.name, opt);
                    log.Info(fate == AddedFate.Carry ? $"   carry    added object '{fullPath}' (sibling {sibling})"
                           : fate == AddedFate.DropSamePath ? $"   drop     added object '{fullPath}' — the prefab now carries it"
                           : $"   drop     added object '{fullPath}' — the prefab already has a '{go.name}' under '{parentPath}' (set Carry same-named additions to keep it)");
                }
                foreach (var (comp, path) in addedComps)
                {
                    bool prefabHas = newIdxProbe.TryGetValue(path, out var pg) && pg.GetComponent(comp.GetType()) != null;
                    log.Info(prefabHas
                        ? $"   drop     added component {path} :: {comp.GetType().Name} — the prefab now carries one"
                        : $"   carry    added component {path} :: {comp.GetType().Name}");
                }
                foreach (var r in refs) log.Info($"   rewire   {r.OwnerDescription}.{r.PropertyPath} → {r.TargetRelPath} :: {r.TargetTypeName}#{r.TargetOrdinal}");

                if (dryRun) { log.Info("DRY RUN — scene not modified."); return log; }

                // ── execute ──────────────────────────────────────────────────────
                foreach (var (go, _, _) in addedGos) go.transform.SetParent(null, true);   // park at scene root
                var carriedComps = new List<(Component copy, string path, Component original)>();
                foreach (var (comp, path) in addedComps)
                {
                    bool prefabHas = newIdxProbe.TryGetValue(path, out var pg) && pg.GetComponent(comp.GetType()) != null;
                    if (prefabHas) continue;
                    var holder = new GameObject("~GameCanvasUnifier carry " + comp.GetType().Name);
                    SceneManager.MoveGameObjectToScene(holder, scene);
                    if (ComponentUtility.CopyComponent(comp) && ComponentUtility.PasteComponentAsNew(holder))
                        carriedComps.Add((holder.GetComponent(comp.GetType()), path, comp));
                    else log.Warn($"Could not carry added component {path} :: {comp.GetType().Name}");
                }

                UnityEngine.Object.DestroyImmediate(old);

                var fresh = (GameObject)PrefabUtility.InstantiatePrefab(corePrefab, scene);
                placement.Apply(fresh, corePrefab.name);
                var newIdx = IndexByPath(fresh.transform);

                foreach (var (go, parentPath, sibling) in addedGos)
                {
                    var fullPath = JoinPath(parentPath, SegmentName(go.transform));
                    if (AddedObjectFate(newIdx, fullPath, parentPath, go.name, opt) != AddedFate.Carry) { UnityEngine.Object.DestroyImmediate(go); continue; }
                    if (!newIdx.TryGetValue(parentPath, out var parent))
                    {
                        log.Warn($"Added object '{fullPath}': parent '{parentPath}' is not in the new canvas; left at scene root.");
                        continue;
                    }
                    go.transform.SetParent(parent.transform, false);
                    go.transform.SetSiblingIndex(Mathf.Clamp(sibling, 0, parent.transform.childCount - 1));
                }
                newIdx = IndexByPath(fresh.transform);   // added objects are addressable now

                foreach (var (copy, path, original) in carriedComps)
                {
                    if (newIdx.TryGetValue(path, out var host) && ComponentUtility.CopyComponent(copy) && ComponentUtility.PasteComponentAsNew(host))
                    {
                        var pasted = host.GetComponents(copy.GetType()).Last();
                        refs.AddRange(RetargetOwner(refs, original, pasted));
                    }
                    else log.Warn($"Could not re-attach carried component {path} :: {copy.GetType().Name}");
                    UnityEngine.Object.DestroyImmediate(copy.gameObject);
                }

                int applied = 0;
                foreach (var s in survivors)
                {
                    var target = ResolveInIndex(newIdx, s.RelPath, s.TypeName, s.Ordinal);
                    if (target == null) { log.Warn($"Survivor '{s.RelPath} :: {s.TypeName}#{s.Ordinal}' has no counterpart in the new canvas."); continue; }
                    var so = new SerializedObject(target);
                    var p = so.FindProperty(s.PropertyPath);
                    if (p == null) { log.Warn($"Survivor property '{s.PropertyPath}' not found on {s.TypeName}."); continue; }
                    if (s.Value.Restore(p)) { so.ApplyModifiedPropertiesWithoutUndo(); applied++; }
                    else log.Warn($"Survivor '{s.PropertyPath}' has an unsupported type ({p.propertyType}).");
                }

                int rewired = 0, unresolved = 0;
                foreach (var r in refs)
                {
                    if (r.Owner == null) continue;
                    var target = ResolveInIndex(newIdx, r.TargetRelPath, r.TargetTypeName, r.TargetOrdinal);
                    if (target == null) { unresolved++; log.Warn($"Reference {r.OwnerDescription}.{r.PropertyPath} → '{r.TargetRelPath} :: {r.TargetTypeName}' has no counterpart in the new canvas."); continue; }
                    var so = new SerializedObject(r.Owner);
                    var p = so.FindProperty(r.PropertyPath);
                    if (p == null) { unresolved++; continue; }
                    p.objectReferenceValue = target;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    rewired++;
                }

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                FrogletToolChangeLedger.Record(ToolName, scenePath);

                var remaining = CountNonDefaultOverrides(fresh);
                log.Info($"— done: {applied} survivor(s) applied, {rewired} reference(s) rewired, {unresolved} unresolved; " +
                         $"the new instance carries {remaining} non-default override(s). Scene saved.");
            }
            catch (Exception e)
            {
                log.Warn($"Re-point aborted: {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
            }
            return log;
        }

        /// <summary>
        /// The ONE scene-side entry point. A scene on the fork is <see cref="Repoint"/>ed; a scene
        /// already on CORE has every override that merely repeats the prefab's current value
        /// dropped (Unity never prunes those, so an absorbed prefab leaves the scene's old
        /// 1920x1080 / x2.4 overrides standing as a wall that says nothing). Settings are
        /// identical before and after; only the override count changes.
        /// </summary>
        public static Log FixScene(string scenePath, bool dryRun)
        {
            var log = new Log();
            if (!System.IO.File.Exists(scenePath)) { log.Warn($"'{scenePath}' not found."); return log; }
            if (System.IO.File.ReadAllText(scenePath).Contains(ForkGuid))
                return Repoint(scenePath, new RepointOptions(), dryRun);

            var status = CoreStatus();
            if (!status.AtContract)
            {
                log.Warn($"{CorePrefabPath} is not at the canvas contract ({status.Summary}). Run 'Fix prefab' first.");
                return log;
            }
            if (!PrefabDriftFixer.PrepareForSceneWork()) { log.Warn("Cancelled: unsaved scene changes."); return log; }

            Scene scene;
            try { scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single); }
            catch (Exception e) { log.Warn($"Could not open '{scenePath}': {e.Message}"); return log; }

            var cores = PrefabDriftFixer.FindInstanceRoots(scene, CoreGuid);
            if (cores.Count == 0) { log.Info($"{scenePath}: no GameCanvas instance. Nothing to do."); return log; }

            try
            {
                int total = 0;
                foreach (var root in cores)
                {
                    log.Info($"{scenePath} — instance '{root.name}' on CORE/GameCanvas");
                    total += RevertRedundantOverrides(root, log, dryRun);
                    log.Info($"   {CountNonDefaultOverrides(root)} non-default override(s) {(dryRun ? "would remain" : "remain")} (values that genuinely differ from the prefab)");
                }
                if (dryRun) { log.Info($"DRY RUN — {total} redundant override(s) would be dropped; scene not modified."); return log; }
                if (total == 0) { log.Info("Nothing redundant. Scene untouched."); return log; }
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                FrogletToolChangeLedger.Record(ToolName, scenePath);
                log.Info($"— done: {total} redundant override(s) dropped. Scene saved.");
            }
            catch (Exception e) { log.Warn($"Fix scene aborted: {e.GetType().Name}: {e.Message}\n{e.StackTrace}"); }
            return log;
        }

        /// <summary>
        /// Reverts every property override on the instance whose value already equals the
        /// corresponding prefab value. Compares against <c>GetCorrespondingObjectFromSource</c>
        /// (the object in CORE, nested-instance overrides included), so a nested prefab's
        /// property is judged against what CORE actually shows, not the nested asset's default.
        /// </summary>
        static int RevertRedundantOverrides(GameObject instanceRoot, Log log, bool dryRun)
        {
            int reverted = 0;
            var rev = IndexByPath(instanceRoot.transform).ToDictionary(kv => kv.Value, kv => kv.Key);
            foreach (var oo in PrefabUtility.GetObjectOverrides(instanceRoot, false))
            {
                var inst = oo.instanceObject;
                if (inst == null) continue;
                var src = PrefabUtility.GetCorrespondingObjectFromSource(inst);
                if (src == null) continue;
                var go = inst as GameObject ?? (inst as Component)?.gameObject;
                var relPath = go != null && rev.TryGetValue(go, out var rp) ? rp : PathOf(go);

                var so = new SerializedObject(inst);
                var sso = new SerializedObject(src);
                var redundant = new List<string>();
                var it = so.GetIterator();
                while (it.Next(true))
                {
                    if (!it.prefabOverride) continue;
                    var sp = sso.FindProperty(it.propertyPath);
                    if (sp == null) continue;
                    if (SerializedProperty.DataEquals(it, sp)) redundant.Add(it.propertyPath);
                }
                foreach (var path in redundant)
                {
                    var p = so.FindProperty(path);
                    if (p == null || !p.prefabOverride) continue;   // a parent revert may already have covered it
                    log.Info($"   {(dryRun ? "would drop" : "drop")}     {relPath} :: {inst.GetType().Name} . {path}  (same value as the prefab)");
                    if (!dryRun) PrefabUtility.RevertPropertyOverride(p, InteractionMode.AutomatedAction);
                    reverted++;
                }
            }
            return reverted;
        }

        enum AddedFate { Carry, DropSamePath, DropSameName }

        static AddedFate AddedObjectFate(Dictionary<string, GameObject> idx, string fullPath, string parentPath, string name, RepointOptions opt)
        {
            if (idx.ContainsKey(fullPath)) return AddedFate.DropSamePath;
            if (opt.CarrySameNamedAdditions) return AddedFate.Carry;
            if (idx.TryGetValue(parentPath, out var parent))
                for (int i = 0; i < parent.transform.childCount; i++)
                    if (parent.transform.GetChild(i).name == name) return AddedFate.DropSameName;
            return AddedFate.Carry;
        }

        sealed class Placement
        {
            public Transform Parent; public int SiblingIndex; public Vector3 Pos; public Quaternion Rot; public Vector3 Scale;
            public bool Active; public int Layer;
            public static Placement Capture(GameObject go) => new()
            {
                Parent = go.transform.parent, SiblingIndex = go.transform.GetSiblingIndex(),
                Pos = go.transform.localPosition, Rot = go.transform.localRotation, Scale = go.transform.localScale,
                Active = go.activeSelf, Layer = go.layer,
            };
            public void Apply(GameObject go, string name)
            {
                if (Parent != null) go.transform.SetParent(Parent, false);
                go.transform.localPosition = Pos; go.transform.localRotation = Rot; go.transform.localScale = Scale;
                go.transform.SetSiblingIndex(SiblingIndex);
                go.SetActive(Active); go.layer = Layer; go.name = name;
            }
        }

        sealed class Survivor
        {
            public string RelPath, TypeName, PropertyPath; public int Ordinal; public PropertySnapshot Value;
        }

        sealed class SceneRef
        {
            public Component Owner; public string OwnerDescription, PropertyPath, TargetRelPath, TargetTypeName; public int TargetOrdinal;
        }

        static List<Survivor> CollectSurvivors(GameObject old, Dictionary<GameObject, string> rev, RepointOptions opt, Log log, out int dropped)
        {
            var list = new List<Survivor>();
            dropped = 0;
            foreach (var oo in PrefabUtility.GetObjectOverrides(old, false))
            {
                var inst = oo.instanceObject;
                if (inst == null) continue;
                var go = inst as GameObject ?? (inst as Component)?.gameObject;
                if (go == null || !rev.TryGetValue(go, out var relPath)) continue;
                var typeName = inst is GameObject ? "GameObject" : inst.GetType().FullName;
                var ordinal = inst is Component c ? OrdinalOf(c) : 0;

                var so = new SerializedObject(inst);
                var it = so.GetIterator();
                while (it.Next(true))
                {
                    if (!it.prefabOverride) continue;
                    if (IsDefaultOverride(relPath, it.propertyPath)) continue;
                    bool keep = opt.SurvivorPropertyPrefixes.Any(pfx => !string.IsNullOrWhiteSpace(pfx) && it.propertyPath.StartsWith(pfx.Trim(), StringComparison.Ordinal));
                    if (!keep && opt.KeepSceneReferenceOverrides && it.propertyType == SerializedPropertyType.ObjectReference
                        && it.objectReferenceValue != null && !EditorUtility.IsPersistent(it.objectReferenceValue))
                        keep = true;
                    if (!keep) { dropped++; continue; }
                    var snap = PropertySnapshot.Capture(it);
                    if (snap == null) { log.Warn($"Survivor '{it.propertyPath}' on {relPath} :: {typeName} has an unsupported type ({it.propertyType}); dropped."); dropped++; continue; }
                    list.Add(new Survivor { RelPath = relPath, TypeName = typeName, Ordinal = ordinal, PropertyPath = it.propertyPath, Value = snap });
                }
            }
            return list;
        }

        static bool IsDefaultOverride(string relPath, string propertyPath)
            => relPath.Length == 0 && (propertyPath == "m_Name" || propertyPath.StartsWith("m_LocalPosition") ||
                                       propertyPath.StartsWith("m_LocalRotation") || propertyPath.StartsWith("m_LocalScale") ||
                                       propertyPath.StartsWith("m_LocalEulerAnglesHint") || propertyPath == "m_RootOrder");

        static List<SceneRef> CollectSceneReferencesInto(Scene scene, GameObject old, Dictionary<GameObject, string> rev, HashSet<Component> addedComps)
        {
            var refs = new List<SceneRef>();
            foreach (var root in scene.GetRootGameObjects())
            foreach (var comp in root.GetComponentsInChildren<Component>(true))
            {
                if (comp == null) continue;
                if (comp.transform.IsChildOf(old.transform) && !addedComps.Contains(comp)) continue;   // the instance's own objects go with it
                if (comp is Transform) continue;                                                       // parenting is handled as added objects
                var so = new SerializedObject(comp);
                var it = so.GetIterator();
                while (it.Next(true))
                {
                    if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                    if (NeverRemap.Contains(it.name)) continue;
                    var v = it.objectReferenceValue;
                    if (v == null) continue;
                    var go = v as GameObject ?? (v as Component)?.gameObject;
                    if (go == null || !rev.TryGetValue(go, out var relPath)) continue;
                    if (addedComps.Contains(v as Component)) continue;   // a scene-added component is not a canvas object
                    refs.Add(new SceneRef
                    {
                        Owner = comp,
                        OwnerDescription = $"{PathOf(comp.gameObject)} :: {comp.GetType().Name}",
                        PropertyPath = it.propertyPath,
                        TargetRelPath = relPath,
                        TargetTypeName = v is GameObject ? "GameObject" : v.GetType().FullName,
                        TargetOrdinal = v is Component c ? OrdinalOf(c) : 0,
                    });
                }
            }
            return refs;
        }

        static IEnumerable<SceneRef> RetargetOwner(List<SceneRef> refs, Component original, Component pasted)
        {
            var moved = refs.Where(r => r.Owner == original).ToList();
            foreach (var r in moved) r.Owner = null;   // the original is destroyed with the old instance
            return moved.Select(r => new SceneRef
            {
                Owner = pasted, OwnerDescription = r.OwnerDescription, PropertyPath = r.PropertyPath,
                TargetRelPath = r.TargetRelPath, TargetTypeName = r.TargetTypeName, TargetOrdinal = r.TargetOrdinal,
            });
        }

        static int CountRemovedGameObjects(GameObject instanceRoot)
        {
            // GetRemovedGameObjects arrived in 2022.2; resolved by reflection so the tool compiles either way.
            var m = typeof(PrefabUtility).GetMethod("GetRemovedGameObjects", new[] { typeof(GameObject) });
            if (m == null) return -1;
            return m.Invoke(null, new object[] { instanceRoot }) is System.Collections.ICollection col ? col.Count : -1;
        }

        static int CountNonDefaultOverrides(GameObject instanceRoot)
        {
            int n = 0;
            var rev = IndexByPath(instanceRoot.transform).ToDictionary(kv => kv.Value, kv => kv.Key);
            foreach (var oo in PrefabUtility.GetObjectOverrides(instanceRoot, false))
            {
                var inst = oo.instanceObject;
                if (inst == null) continue;
                var go = inst as GameObject ?? (inst as Component)?.gameObject;
                if (go == null || !rev.TryGetValue(go, out var relPath)) continue;
                var it = new SerializedObject(inst).GetIterator();
                while (it.Next(true))
                    if (it.prefabOverride && !IsDefaultOverride(relPath, it.propertyPath)) n++;
            }
            n += PrefabUtility.GetAddedComponents(instanceRoot).Count + PrefabUtility.GetRemovedComponents(instanceRoot).Count
                 + PrefabUtility.GetAddedGameObjects(instanceRoot).Count;
            return n;
        }

        // ── 4. Delete the fork ───────────────────────────────────────────────────

        public static Log DeleteFork()
        {
            var log = new Log();
            var refs = FilesReferencingGuid(ForkGuid);
            if (refs.Count > 0)
            {
                log.Warn($"{refs.Count} file(s) still reference the fork; re-point them first:");
                foreach (var r in refs) log.Info("   " + r);
                return log;
            }
            if (!System.IO.File.Exists(ForkPrefabPath)) { log.Info("Fork already gone."); return log; }
            if (AssetDatabase.DeleteAsset(ForkPrefabPath))
            {
                log.Info($"Deleted {ForkPrefabPath}.");
                FrogletToolChangeLedger.Record(ToolName, ForkPrefabPath);
                FrogletToolChangeLedger.Record(ToolName, ForkPrefabPath + ".meta");
            }
            else log.Warn($"AssetDatabase.DeleteAsset refused {ForkPrefabPath}.");
            return log;
        }

        // ── Hierarchy addressing ─────────────────────────────────────────────────

        /// <summary>
        /// Name of <paramref name="t"/> as a path segment: its name, plus <c>~k</c> when k earlier
        /// siblings share that name, so duplicate sibling names (the TeamScorecard rows) still
        /// address one object each on both sides.
        /// </summary>
        public static string SegmentName(Transform t)
        {
            var parent = t.parent;
            if (parent == null) return t.name;
            int k = 0;
            for (int i = 0; i < parent.childCount; i++)
            {
                var s = parent.GetChild(i);
                if (s == t) break;
                if (s.name == t.name) k++;
            }
            return k == 0 ? t.name : $"{t.name}~{k}";
        }

        public static string RelPath(Transform t, Transform root)
        {
            if (t == root) return "";
            var parts = new List<string>();
            var cur = t;
            while (cur != null && cur != root) { parts.Add(SegmentName(cur)); cur = cur.parent; }
            parts.Reverse();
            return string.Join("/", parts);
        }

        public static Dictionary<string, GameObject> IndexByPath(Transform root)
        {
            var idx = new Dictionary<string, GameObject>();
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                idx[RelPath(t, root)] = t.gameObject;
            return idx;
        }

        static string ParentPath(string path)
        {
            var i = path.LastIndexOf('/');
            return i < 0 ? "" : path.Substring(0, i);
        }

        static string JoinPath(string parent, string segment) => parent.Length == 0 ? segment : parent + "/" + segment;

        static bool AnyAncestorIn(string path, HashSet<string> set)
        {
            var p = ParentPath(path);
            while (p.Length > 0) { if (set.Contains(p)) return true; p = ParentPath(p); }
            return false;
        }

        static bool IsDifferentNestedRoot(GameObject shipped, GameObject core)
        {
            bool sRoot = PrefabUtility.IsAnyPrefabInstanceRoot(shipped);
            bool cRoot = PrefabUtility.IsAnyPrefabInstanceRoot(core);
            if (!sRoot) return false;
            if (!cRoot) return true;
            return PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(shipped) != PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(core);
        }

        public static int OrdinalOf(Component c)
        {
            var same = c.gameObject.GetComponents<Component>().Where(x => x != null && x.GetType() == c.GetType()).ToList();
            var i = same.IndexOf(c);
            return i < 0 ? 0 : i;
        }

        static UnityEngine.Object ResolveInIndex(Dictionary<string, GameObject> idx, string relPath, string typeName, int ordinal)
        {
            if (!idx.TryGetValue(relPath, out var go)) return null;
            if (typeName == "GameObject") return go;
            var same = go.GetComponents<Component>().Where(x => x != null && x.GetType().FullName == typeName).ToList();
            if (same.Count == 0) return null;
            return ordinal < same.Count ? same[ordinal] : same[0];
        }

        static Transform TransformOf(UnityEngine.Object o) => o is GameObject g ? g.transform : (o as Component)?.transform;

        static bool IsUnder(UnityEngine.Object o, Transform root)
        {
            var t = TransformOf(o);
            return t != null && t.IsChildOf(root);
        }

        static string PathOf(GameObject go)
        {
            if (go == null) return "(null)";
            var parts = new List<string>();
            var cur = go.transform;
            while (cur != null) { parts.Add(cur.name); cur = cur.parent; }
            parts.Reverse();
            return string.Join("/", parts);
        }

        static string DescribeComponents(GameObject go)
            => string.Join(", ", go.GetComponents<Component>().Select(c => c == null ? "<missing>" : c.GetType().Name));

        // ── Property snapshot (survivor values outlive the object they were read from) ──

        sealed class PropertySnapshot
        {
            SerializedPropertyType _type; object _value;

            public static PropertySnapshot Capture(SerializedProperty p)
            {
                var s = new PropertySnapshot { _type = p.propertyType };
                switch (p.propertyType)
                {
                    case SerializedPropertyType.Integer:
                    case SerializedPropertyType.LayerMask:
                    case SerializedPropertyType.ArraySize:
                    case SerializedPropertyType.Character:
                    case SerializedPropertyType.Enum: s._value = p.propertyType == SerializedPropertyType.Enum ? p.enumValueIndex : p.longValue; break;
                    case SerializedPropertyType.Boolean: s._value = p.boolValue; break;
                    case SerializedPropertyType.Float: s._value = p.doubleValue; break;
                    case SerializedPropertyType.String: s._value = p.stringValue; break;
                    case SerializedPropertyType.ObjectReference: s._value = p.objectReferenceValue; break;
                    case SerializedPropertyType.Color: s._value = p.colorValue; break;
                    case SerializedPropertyType.Vector2: s._value = p.vector2Value; break;
                    case SerializedPropertyType.Vector3: s._value = p.vector3Value; break;
                    case SerializedPropertyType.Vector4: s._value = p.vector4Value; break;
                    case SerializedPropertyType.Rect: s._value = p.rectValue; break;
                    case SerializedPropertyType.Bounds: s._value = p.boundsValue; break;
                    case SerializedPropertyType.Quaternion: s._value = p.quaternionValue; break;
                    case SerializedPropertyType.AnimationCurve: s._value = p.animationCurveValue; break;
                    default: return null;
                }
                return s;
            }

            public bool Restore(SerializedProperty p)
            {
                if (p.propertyType != _type) return false;
                switch (_type)
                {
                    case SerializedPropertyType.Integer:
                    case SerializedPropertyType.LayerMask:
                    case SerializedPropertyType.ArraySize:
                    case SerializedPropertyType.Character: p.longValue = (long)_value; return true;
                    case SerializedPropertyType.Enum: p.enumValueIndex = (int)_value; return true;
                    case SerializedPropertyType.Boolean: p.boolValue = (bool)_value; return true;
                    case SerializedPropertyType.Float: p.doubleValue = (double)_value; return true;
                    case SerializedPropertyType.String: p.stringValue = (string)_value; return true;
                    case SerializedPropertyType.ObjectReference: p.objectReferenceValue = (UnityEngine.Object)_value; return true;
                    case SerializedPropertyType.Color: p.colorValue = (Color)_value; return true;
                    case SerializedPropertyType.Vector2: p.vector2Value = (Vector2)_value; return true;
                    case SerializedPropertyType.Vector3: p.vector3Value = (Vector3)_value; return true;
                    case SerializedPropertyType.Vector4: p.vector4Value = (Vector4)_value; return true;
                    case SerializedPropertyType.Rect: p.rectValue = (Rect)_value; return true;
                    case SerializedPropertyType.Bounds: p.boundsValue = (Bounds)_value; return true;
                    case SerializedPropertyType.Quaternion: p.quaternionValue = (Quaternion)_value; return true;
                    case SerializedPropertyType.AnimationCurve: p.animationCurveValue = (AnimationCurve)_value; return true;
                    default: return false;
                }
            }
        }
    }
}
