using System.Collections.Generic;
using System.Linq;
using CosmicShore.Gameplay;
using CosmicShore.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Editor.Froglet
{
    /// <summary>
    /// Wire Menu_Main's home hub — four buttons, four modals, one registry — in one press.
    ///
    /// <para><b>Why this is a tool and not a checklist.</b> The hub screens were authored by
    /// duplicating the Arcade's, which is the right way to get the layout and the wrong way to get
    /// the wiring: a duplicate carries the original's components and serialized values, and every
    /// one of those is a claim about what the object IS. Measured on the authored scene, all four
    /// hub buttons called <c>ScreenSwitcher.OnClickArcadeNav</c>, all four screen modals declared
    /// <c>ModalType = ARCADE</c>, and none of the three new windows was in the switcher's
    /// <c>Modals</c> list — so every button opened the Arcade and <c>OpenModal(TOYBOX)</c> had
    /// nothing to find. Nineteen edits, all mechanical, all easy to get subtly wrong by hand.</para>
    ///
    /// <para><b>Every write goes through <c>SerializedObject</c> and <c>AddComponent</c></b>, never
    /// hand-edited YAML — the standing rule in CLAUDE.md's tooling section. The read-only twin,
    /// <c>Tools/Build/wire_home_hub_scene.py --check</c>, states the same contract from outside the
    /// editor and is what proves the work landed.</para>
    ///
    /// <para><b>Availability is authored here, not discovered.</b> Arena is <c>Locked</c> ("this
    /// exists and you cannot open it yet" — its modal behind the lock is real) and Mission is
    /// <c>Unavailable</c> ("this is not built"). Both stay DRAWN, because an entry that is simply
    /// absent tells the player the game has two things in it, and the day it ships they have to
    /// re-learn the screen (<c>Docs/HomeHub/ARCHITECTURE.md</c> §2).</para>
    /// </summary>
    public class HomeHubWiringWindow : EditorWindow
    {
        const string ToolName = "Home Hub Wiring";
        const string ScenePath = "Assets/_Scenes/Menu_Main.unity";

        // Permanent, not a one-off: the audit half is the guard that keeps the hub wired as the
        // three unbuilt screens come online, so there is nothing to retire.
        static readonly FrogletToolShipContext Ship = new(ToolName)
        {
            CommitType = "feat",
            CommitScope = "menu",
            CommitSubject = n => $"feat(menu): wire the home hub - {n} file(s)",
        };

        readonly List<string> _log = new();
        Vector2 _scroll;

        struct HubEntry
        {
            public string ButtonName;
            public ScreenSwitcher.ModalWindows Target;
            public MenuAvailability Availability;
        }

        static readonly HubEntry[] Hub =
        {
            new() { ButtonName = "ArcadeButton",  Target = ScreenSwitcher.ModalWindows.ARCADE,  Availability = MenuAvailability.Available },
            new() { ButtonName = "ToyboxButton",  Target = ScreenSwitcher.ModalWindows.TOYBOX,  Availability = MenuAvailability.Available },
            new() { ButtonName = "ArenaButton",   Target = ScreenSwitcher.ModalWindows.ARENA,   Availability = MenuAvailability.Locked },
            new() { ButtonName = "MissionButton", Target = ScreenSwitcher.ModalWindows.MISSION, Availability = MenuAvailability.Unavailable },
        };

        // Screen modal GameObject name -> the type it must declare. The trailing space the
        // authored scene carries on 'MissionScreenModal ' is trimmed on lookup AND fixed.
        static readonly (string Name, ScreenSwitcher.ModalWindows Type)[] Modals =
        {
            ("ArcadeScreenModal",        ScreenSwitcher.ModalWindows.ARCADE),
            ("ToyboxScreenModal",        ScreenSwitcher.ModalWindows.TOYBOX),
            ("ArenaScreenModal",         ScreenSwitcher.ModalWindows.ARENA),
            ("MissionScreenModal",       ScreenSwitcher.ModalWindows.MISSION),
            ("ToyboxGameConfigureModal", ScreenSwitcher.ModalWindows.TOYBOX_CONFIGURE),
        };

        [MenuItem("FrogletTools/Interface/Home Hub Wiring", false, 20)]
        [FrogletTool(FrogletToolCategory.Interface, Importance = 4,
            Description = "Wire Menu_Main's Mission / Toy Box / Arena / Arcade buttons, their " +
                          "modal types, and the ScreenSwitcher registry.")]
        public static void Open() =>
            GetWindow<HomeHubWiringWindow>("Home Hub Wiring").minSize = new Vector2(520, 380);

        void OnGUI()
        {
            FrogletEditorPalette.Banner(
                "Home Hub Wiring",
                "Four buttons, four modals, one registry. Open Menu_Main first.",
                FrogletEditorPalette.Jade);

            EditorGUILayout.HelpBox(
                "Duplicating the Arcade's screens copied its WIRING too: every hub button called " +
                "OnClickArcadeNav and every screen modal declared ModalType ARCADE. This repoints " +
                "them, registers the new windows with the ScreenSwitcher, and sets Arena to Locked " +
                "and Mission to Unavailable.", MessageType.Info);

            EditorGUILayout.Space();

            bool sceneOpen = EditorSceneManager.GetActiveScene().path == ScenePath;
            if (!sceneOpen)
            {
                EditorGUILayout.HelpBox($"Open {ScenePath} to run this.", MessageType.Warning);
                if (GUILayout.Button("Open Menu_Main"))
                {
                    if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                        EditorSceneManager.OpenScene(ScenePath);
                }
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (FrogletEditorPalette.ColorButton("✓  AUDIT", FrogletEditorPalette.Azure, 140f, 30f,
                        "Report what is still unwired. Writes nothing."))
                    Run(dryRun: true);

                if (FrogletEditorPalette.ColorButton("⚡  WIRE IT", FrogletEditorPalette.Jade, 160f, 30f,
                        "Apply every fix, then save the scene."))
                    Run(dryRun: false);
            }

            EditorGUILayout.Space();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var line in _log) EditorGUILayout.LabelField(line, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndScrollView();

            FrogletToolShipPanel.Draw(Ship, this);
        }

        void Run(bool dryRun)
        {
            _log.Clear();
            int changed = 0;

            var switcher = FindAnyObjectByType<ScreenSwitcher>(FindObjectsInactive.Include);
            if (!switcher)
            {
                _log.Add("ERROR: no ScreenSwitcher in the scene — nothing can be registered.");
                return;
            }

            changed += WireButtons(dryRun);
            changed += WireModals(dryRun, out var managers);
            changed += RegisterModals(switcher, managers, dryRun);

            if (changed == 0)
            {
                _log.Add("Nothing to do — the home hub is already wired.");
                return;
            }

            if (dryRun)
            {
                _log.Add($"— {changed} change(s) pending. Press WIRE IT to apply.");
                return;
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            FrogletToolChangeLedger.Record(ToolName, ScenePath);
            _log.Add($"✔ {changed} change(s) applied and {ScenePath} saved.");
            _log.Add("Verify with: python3 Tools/Build/wire_home_hub_scene.py --check");
        }

        // ── buttons ──────────────────────────────────────────────────────────

        int WireButtons(bool dryRun)
        {
            int changed = 0;
            foreach (var entry in Hub)
            {
                var go = FindByName(entry.ButtonName);
                if (!go) { _log.Add($"MISSING: no GameObject named '{entry.ButtonName}'."); continue; }

                if (!go.TryGetComponent(out MenuHubButton hub))
                {
                    _log.Add($"{entry.ButtonName}: add MenuHubButton (target {entry.Target}).");
                    changed++;
                    if (!dryRun) hub = Undo.AddComponent<MenuHubButton>(go);
                }

                if (hub && !dryRun)
                {
                    var so = new SerializedObject(hub);
                    so.FindProperty("target").enumValueIndex =
                        EnumIndexOf<ScreenSwitcher.ModalWindows>(entry.Target);
                    so.ApplyModifiedProperties();
                }

                // Availability lives on the shared view, which MenuHubButton ensures at Awake —
                // ensured HERE too so the state is authored data rather than something that only
                // appears at runtime.
                var view = go.GetComponent<MenuAvailabilityView>();
                if (!view)
                {
                    _log.Add($"{entry.ButtonName}: add MenuAvailabilityView ({entry.Availability}).");
                    changed++;
                    if (!dryRun) view = Undo.AddComponent<MenuAvailabilityView>(go);
                }
                if (view && !dryRun)
                {
                    var so = new SerializedObject(view);
                    so.FindProperty("availability").enumValueIndex =
                        EnumIndexOf<MenuAvailability>(entry.Availability);
                    so.ApplyModifiedProperties();
                }

                // Drop the inherited nav call. MenuAudio.PlayAudio is left alone: it is the press
                // sound, and it is on the reviewed persistent-listener allow-list.
                if (go.TryGetComponent(out Button button))
                    changed += StripNavCalls(button, entry.ButtonName, dryRun);
            }
            return changed;
        }

        int StripNavCalls(Button button, string label, bool dryRun)
        {
            int removed = 0;
            for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            {
                var target = button.onClick.GetPersistentTarget(i);
                string method = button.onClick.GetPersistentMethodName(i);
                if (target is not ScreenSwitcher) continue;
                if (!method.StartsWith("OnClick")) continue;

                _log.Add($"{label}: remove persistent call ScreenSwitcher.{method}().");
                removed++;
                if (!dryRun)
                    UnityEditor.Events.UnityEventTools.RemovePersistentListener(button.onClick, i);
            }
            return removed;
        }

        // ── modals ───────────────────────────────────────────────────────────

        int WireModals(bool dryRun, out List<ModalWindowManager> managers)
        {
            managers = new List<ModalWindowManager>();
            int changed = 0;

            foreach (var (rawName, type) in Modals)
            {
                var go = FindByName(rawName);
                if (!go) { _log.Add($"MISSING: no GameObject named '{rawName}'."); continue; }

                // A trailing space in a name is invisible in the hierarchy and fatal to any
                // find-by-name. Fix it while we are here.
                if (go.name != go.name.Trim())
                {
                    _log.Add($"'{go.name}': trim the trailing whitespace in the name.");
                    changed++;
                    if (!dryRun) go.name = go.name.Trim();
                }

                changed += SwapScreenScript(go, type, dryRun);

                var mgr = go.GetComponent<ModalWindowManager>();
                if (!mgr) { _log.Add($"{rawName}: no ModalWindowManager-derived component."); continue; }
                managers.Add(mgr);

                var so = new SerializedObject(mgr);
                var prop = so.FindProperty("ModalType");
                if (prop == null) { _log.Add($"{rawName}: no ModalType property."); continue; }

                int want = EnumIndexOf<ScreenSwitcher.ModalWindows>(type);
                if (prop.enumValueIndex != want)
                {
                    _log.Add($"{rawName}: ModalType -> {type}.");
                    changed++;
                    if (!dryRun) { prop.enumValueIndex = want; so.ApplyModifiedProperties(); }
                }
            }
            return changed;
        }

        /// <summary>
        /// The Toy Box's two windows carry the Arcade's controllers because they were duplicated
        /// from it. Neither fits: <c>ArcadeScreen</c> drives a game-card grid off an SO_GameList,
        /// and <c>ArcadeGameConfigureModal</c> is ~2,250 lines of Netcode, player-count and domain
        /// policy for launching a match. A toy has none of those.
        /// </summary>
        int SwapScreenScript(GameObject go, ScreenSwitcher.ModalWindows type, bool dryRun)
        {
            int changed = 0;

            if (type == ScreenSwitcher.ModalWindows.TOYBOX)
            {
                if (go.TryGetComponent(out ArcadeScreen stale))
                {
                    _log.Add($"{go.name}: remove ArcadeScreen (it drives the game-card grid).");
                    changed++;
                    if (!dryRun) Undo.DestroyObjectImmediate(stale);
                }
                if (!go.GetComponent<ToyboxModal>())
                {
                    _log.Add($"{go.name}: add ToyboxModal.");
                    changed++;
                    if (!dryRun) Undo.AddComponent<ToyboxModal>(go);
                }
            }

            if (type == ScreenSwitcher.ModalWindows.TOYBOX_CONFIGURE)
            {
                if (go.TryGetComponent(out ArcadeGameConfigureModal stale))
                {
                    _log.Add($"{go.name}: remove ArcadeGameConfigureModal (a toy configures nothing).");
                    changed++;
                    if (!dryRun) Undo.DestroyObjectImmediate(stale);
                }
                if (!go.GetComponent<ToyConfigureModal>())
                {
                    _log.Add($"{go.name}: add ToyConfigureModal.");
                    changed++;
                    if (!dryRun) Undo.AddComponent<ToyConfigureModal>(go);
                }
            }

            return changed;
        }

        // ── the registry ─────────────────────────────────────────────────────

        /// <summary>
        /// The switcher finds a modal BY TYPE by walking this list, so a window that is not in it
        /// cannot be opened at all — and a NULL in it is not inert, because both
        /// <c>OpenModal</c> and <c>CloseAllModals</c> walk the same list.
        /// </summary>
        int RegisterModals(ScreenSwitcher switcher, List<ModalWindowManager> managers, bool dryRun)
        {
            var so = new SerializedObject(switcher);
            var list = so.FindProperty("Modals");
            if (list == null) { _log.Add("ScreenSwitcher: no Modals property."); return 0; }

            var present = new List<ModalWindowManager>();
            int nulls = 0;
            for (int i = 0; i < list.arraySize; i++)
            {
                var v = list.GetArrayElementAtIndex(i).objectReferenceValue as ModalWindowManager;
                if (v) present.Add(v); else nulls++;
            }

            var missing = managers.Where(m => m && !present.Contains(m)).ToList();
            if (nulls == 0 && missing.Count == 0) return 0;

            if (nulls > 0) _log.Add($"ScreenSwitcher.Modals: drop {nulls} null entr(y/ies).");
            foreach (var m in missing) _log.Add($"ScreenSwitcher.Modals: register {m.name}.");

            int changed = nulls + missing.Count;
            if (dryRun) return changed;

            var final = present.Concat(missing).ToList();
            list.arraySize = final.Count;
            for (int i = 0; i < final.Count; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = final[i];
            so.ApplyModifiedProperties();
            return changed;
        }

        // ── helpers ──────────────────────────────────────────────────────────

        /// <summary>
        /// Find by name across the loaded scene INCLUDING inactive objects — every modal in this
        /// scene is inactive or alpha-0 most of the time — and tolerate the authored trailing
        /// space this tool is also here to remove.
        /// </summary>
        static GameObject FindByName(string wanted)
        {
            foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (!t || t.gameObject.scene != EditorSceneManager.GetActiveScene()) continue;
                if (t.name.Trim() == wanted) return t.gameObject;
            }
            return null;
        }

        static int EnumIndexOf<T>(T value) where T : System.Enum =>
            System.Array.IndexOf(System.Enum.GetValues(typeof(T)).Cast<T>().ToArray(), value);
    }
}
