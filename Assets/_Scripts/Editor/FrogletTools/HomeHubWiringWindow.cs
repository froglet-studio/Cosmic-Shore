using System.Collections.Generic;
using System.Linq;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using TMPro;
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

        // The run is DEFERRED to the next Layout event rather than executed inside the button's
        // own draw. Running it inline appends to _log during a Repaint/MouseDown, so the number of
        // LabelFields inside the scroll view changes between the Layout pass and the Repaint pass -
        // which is exactly the "EndLayoutGroup: BeginLayoutGroup must be called first" IMGUI reports.
        // Saving the scene mid-OnGUI compounds it. One flag, consumed on Layout, removes both.
        bool? _pendingRun;

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
            if (Event.current.type == EventType.Layout && _pendingRun.HasValue)
            {
                bool dry = _pendingRun.Value;
                _pendingRun = null;
                Run(dry);
            }

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
                    _pendingRun = true;

                if (FrogletEditorPalette.ColorButton("⚡  WIRE IT", FrogletEditorPalette.Jade, 160f, 30f,
                        "Apply every fix, then save the scene."))
                    _pendingRun = false;
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
            changed += FillSlots(switcher, dryRun);

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
                    so.FindProperty("target").intValue = (int)entry.Target;
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
                    so.FindProperty("availability").intValue = (int)entry.Availability;
                    so.ApplyModifiedProperties();
                }

                // Drop the inherited nav call. MenuAudio.PlayAudio is left alone: it is the press
                // sound, and it is on the reviewed persistent-listener allow-list.
                if (go.TryGetComponent(out Button button))
                    changed += StripNavCalls(button, entry.ButtonName, dryRun);
            }
            return changed;
        }

        /// <summary>
        /// Remove every persistent listener on a repurposed button except the press sound. See the
        /// call site for why an inherited persistent call is a functional defect and not clutter.
        /// </summary>
        int StripForeignCalls(Button button, string label, bool dryRun)
        {
            int removed = 0;
            for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            {
                var target = button.onClick.GetPersistentTarget(i);
                if (target is MenuAudio) continue;

                string method = button.onClick.GetPersistentMethodName(i);
                _log.Add($"{label}: remove inherited persistent call {target?.GetType().Name ?? "<missing>"}.{method}().");
                removed++;
                if (!dryRun)
                    UnityEditor.Events.UnityEventTools.RemovePersistentListener(button.onClick, i);
            }
            return removed;
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

                int want = (int)type;
                if (prop.intValue != want)
                {
                    _log.Add($"{rawName}: ModalType {prop.intValue} -> {want} ({type}).");
                    changed++;
                    if (!dryRun) { prop.intValue = want; so.ApplyModifiedProperties(); }
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

        // ── the serialized slots ─────────────────────────────────────────────

        const string CardTemplateName = "ToyCardTemplate";
        const string EmptyStateName = "ToyboxEmptyState";
        const string FreestyleEventsAsset =
            "Assets/_SO_Assets/MenuFreestyle/MenuFreestyleEvents.asset";

        // Arcade content the Toy Box's detail window inherited and a toy has no use for. Switched
        // OFF rather than deleted: the authoring is somebody's work, and re-activating a GameObject
        // is a cheaper mistake to undo than re-authoring one.
        static readonly string[] ConfigureBranchesToRetire =
        {
            "ControlsDescription", "Ship Select", "Intensity", "Player Count", "Domain Count",
            "TeamHolder", "Toggle", "ObjectiveBox", "FavoriteIcon",
            "ModePreviewStatus", "ModePreviewFocus", "FocusHint", "InGameHUDToast",
        };

        /// <summary>
        /// Both Toy Box windows are duplicates of the Arcade's, so they arrive carrying the
        /// Arcade's CONTENT as well as its wiring: a game-card grid, a party list, a friends
        /// column, a vessel picker, three steppers and a launch button. A toy configures none of
        /// that — it has one verb — so this pass turns that inheritance into the Toy Box's own two
        /// windows and then binds every serialized reference the two new components need.
        ///
        /// <para>Destruction is deliberately narrow. A whole arcade BRANCH is switched off; only a
        /// component that would actively fight for an object the Toy Box KEEPS is removed —
        /// <c>ArcadeExploreView</c> would drive the very same grid off an SO_GameList, and
        /// <c>ModePreviewWindow</c> would stand a satellite arena up behind a toy.</para>
        ///
        /// <para>Those components are matched by TYPE NAME rather than by a compile-time reference,
        /// so this tool does not take a hard dependency on a dozen arcade classes it only wants to
        /// stand down. The cost is stated rather than hidden: if one of them is renamed, the removal
        /// silently becomes a no-op — which the audit then reports as still-unwired rather than
        /// passing quietly.</para>
        /// </summary>
        int FillSlots(ScreenSwitcher switcher, bool dryRun)
        {
            var toybox = FindByName("ToyboxScreenModal");
            var configure = FindByName("ToyboxGameConfigureModal");

            int changed = 0;
            changed += FillToyGrid(toybox, configure, switcher, dryRun);
            changed += FillConfigure(configure, switcher, dryRun);
            changed += EnsureSocialColumns(dryRun);
            return changed;
        }

        /// <summary>
        /// The party roster and the friends column are the SAME surface on every hub window, and
        /// they are the one part of the duplicated Arcade screen that a Toy Box, an Arena and a
        /// Mission all genuinely want: who is with you does not change with which thing you are
        /// about to play. They stay ON in all four, and they need no syncing of their own - both
        /// read <c>HostConnectionDataSO</c> / <c>FriendsDataSO</c> through SOAP, so four copies of
        /// the view show one state by construction.
        /// </summary>
        int EnsureSocialColumns(bool dryRun)
        {
            int changed = 0;
            foreach (var (screen, _) in Modals)
            {
                var go = FindByName(screen);
                if (!go) continue;
                foreach (var column in new[] { "ArcadeLobbyList", "FriendListPanel" })
                    changed += Activate(FindIn(go, column), dryRun);
            }
            return changed;
        }

        int FillToyGrid(GameObject toybox, GameObject configure, ScreenSwitcher switcher, bool dryRun)
        {
            if (!toybox) return 0;

            var modal = toybox.GetComponent<ToyboxModal>();
            if (!modal)
            {
                // WIRE IT adds the component in this same pass, so a dry run legitimately sees none.
                if (!dryRun) _log.Add("ToyboxScreenModal: no ToyboxModal - press WIRE IT again.");
                return 0;
            }

            int changed = 0;
            changed += RemoveComponent(FindIn(toybox, "Explore"), "ArcadeExploreView", dryRun);

            // The cards are laid out by the GRID ITSELF, in one GridLayoutGroup, rather than by the
            // arcade's row-of-four nesting: the arcade fills fixed rows from a roster it knows the
            // length of, and the Toy Box's list is however many toys are standing. One wrapping
            // grid needs no row bookkeeping and no empty trailing row.
            var grid = FindIn(toybox, "GameGrid");
            var template = EnsureCardTemplate(toybox, FindIn(grid, "GameListRow"), dryRun, ref changed);
            changed += EnsureCardLayout(grid, dryRun);
            if (grid)
                for (int i = 0; i < grid.transform.childCount; i++)
                    changed += Deactivate(grid.transform.GetChild(i).gameObject, dryRun);

            var empty = EnsureEmptyState(toybox, dryRun, ref changed);

            var so = new SerializedObject(modal);
            changed += SetRef(so, "cardGrid", grid ? grid.transform : null, "the toy grid", dryRun);
            changed += SetRef(so, "cardPrefab", template ? template.GetComponent<ToyboxCard>() : null,
                              "the card template", dryRun);
            changed += SetRef(so, "emptyState", empty, "the empty state", dryRun);
            changed += SetRef(so, "configureModal",
                              configure ? configure.GetComponent<ToyConfigureModal>() : null,
                              "the detail window", dryRun);
            changed += SetRef(so, "screenSwitcher", switcher, "the screen switcher", dryRun);
            if (!dryRun) so.ApplyModifiedProperties();
            return changed;
        }

        /// <summary>
        /// Put a wrapping <see cref="GridLayoutGroup"/> on the card parent, in place of whichever
        /// linear layout it inherited. A Horizontal group squeezes N cards into one row and a
        /// Vertical group stacks them into a strip; both read as a list of one column, which is
        /// what the duplicated arcade grid produced. The cell size is a starting point, not a
        /// ruling - it is authored data and the designer owns it from here.
        /// </summary>
        int EnsureCardLayout(GameObject grid, bool dryRun)
        {
            if (!grid || grid.GetComponent<GridLayoutGroup>()) return 0;

            _log.Add("GameGrid: swap the linear layout for a GridLayoutGroup.");
            if (dryRun) return 1;

            foreach (var stale in grid.GetComponents<HorizontalOrVerticalLayoutGroup>())
                Undo.DestroyObjectImmediate(stale);

            var layout = Undo.AddComponent<GridLayoutGroup>(grid);
            layout.cellSize = new Vector2(260f, 96f);
            layout.spacing = new Vector2(12f, 12f);
            layout.padding = new RectOffset(12, 12, 12, 12);
            layout.startCorner = GridLayoutGroup.Corner.UpperLeft;
            layout.startAxis = GridLayoutGroup.Axis.Horizontal;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.constraint = GridLayoutGroup.Constraint.Flexible;
            return 1;
        }

        /// <summary>
        /// Turn one inherited arcade GameCard into the Toy Box's card template and empty the row.
        /// Re-using the authored card rather than building one from nothing is what keeps the Toy
        /// Box looking like the rest of the menu without anybody re-authoring a card.
        /// </summary>
        GameObject EnsureCardTemplate(GameObject toybox, GameObject row, bool dryRun, ref int changed)
        {
            var existing = FindIn(toybox, CardTemplateName);
            if (existing) return existing;

            if (!row || row.transform.childCount == 0)
            {
                _log.Add("ToyboxScreenModal: no GameCard to convert into a toy card template.");
                return null;
            }

            _log.Add($"ToyboxScreenModal: convert a GameCard into '{CardTemplateName}' and clear the row.");
            changed++;
            if (dryRun) return null;

            var card = row.transform.GetChild(0).gameObject;
            for (int i = row.transform.childCount - 1; i >= 1; i--)
                Undo.DestroyObjectImmediate(row.transform.GetChild(i).gameObject);

            Undo.RecordObject(card, "toy card template");
            card.name = CardTemplateName;
            // Out of the grid: the template is instantiated per toy, never drawn itself.
            card.transform.SetParent(toybox.transform, false);

            int ignored = 0;
            ignored += RemoveComponent(card, "GameCard", false);
            ignored += RemoveComponent(card, "CallToActionTarget", false);

            foreach (var dead in new[] { "FavoriteIcon", "CallToActionIndicator", "AvatarSpace" })
            {
                var go = FindIn(card, dead);
                if (go) go.SetActive(false);
            }

            var toyCard = card.GetComponent<ToyboxCard>();
            if (!toyCard) toyCard = Undo.AddComponent<ToyboxCard>(card);

            var title = FindComponentIn<TMP_Text>(card, "GameTitle");
            if (title)
            {
                // A game's name is one short word; a toy's is "Connect the Dots" or "Lifeform
                // Matrix". Left at the arcade's fixed single-line size the label CLIPS, which reads
                // as a broken card rather than as a long name.
                Undo.RecordObject(title, "toy card title");
                title.textWrappingMode = TextWrappingModes.Normal;
                title.enableAutoSizing = true;
                title.fontSizeMin = 12f;
                title.fontSizeMax = Mathf.Max(18f, title.fontSize);
                title.overflowMode = TextOverflowModes.Ellipsis;
            }

            var cardSo = new SerializedObject(toyCard);
            SetRef(cardSo, "portrait", FindComponentIn<Image>(card, "VesselIcon"), "the toy portrait", false);
            SetRef(cardSo, "accentFill", FindComponentIn<Image>(card, "Background"), "the accent fill", false);
            SetRef(cardSo, "nameText", title, "the toy name", false);
            cardSo.ApplyModifiedProperties();

            card.SetActive(false);
            return card;
        }

        GameObject EnsureEmptyState(GameObject toybox, bool dryRun, ref int changed)
        {
            var existing = FindIn(toybox, EmptyStateName);
            if (existing) return existing;

            _log.Add($"ToyboxScreenModal: create '{EmptyStateName}' (shown when no toy is standing).");
            changed++;
            if (dryRun) return null;

            var host = FindIn(toybox, "Toybox_Panel");
            if (!host) host = toybox;

            var go = new GameObject(EmptyStateName, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "toybox empty state");
            go.transform.SetParent(host.transform, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.1f, 0.4f);
            rect.anchorMax = new Vector2(0.9f, 0.6f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var label = go.AddComponent<TextMeshProUGUI>();
            label.text = "No toys are standing right now.";
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 28f;
            label.raycastTarget = false;

            go.SetActive(false);
            return go;
        }

        int FillConfigure(GameObject configure, ScreenSwitcher switcher, bool dryRun)
        {
            if (!configure) return 0;

            var modal = configure.GetComponent<ToyConfigureModal>();
            if (!modal)
            {
                if (!dryRun) _log.Add("ToyboxGameConfigureModal: no ToyConfigureModal - press WIRE IT again.");
                return 0;
            }

            int changed = 0;
            changed += RemoveComponent(FindIn(configure, "ConfigurationContent"), "MinigameLaunchPanel", dryRun);
            changed += RemoveComponent(FindIn(configure, "GameView"), "GameBriefingView", dryRun);
            changed += RemoveComponent(FindIn(configure, "Preview"), "ModePreviewWindow", dryRun);

            foreach (var branch in ConfigureBranchesToRetire)
                changed += Deactivate(FindIn(configure, branch), dryRun);

            // The picture is the LIVE toy, so the arcade's preview surface keeps its RawImage and
            // changes only which camera writes into it.
            var surface = FindIn(configure, "ModePreviewSurface");
            var preview = surface ? surface.GetComponent<ToyPreviewCamera>() : null;
            if (surface && !preview)
            {
                _log.Add("ModePreviewSurface: add ToyPreviewCamera (a live window onto the toy).");
                changed++;
                if (!dryRun) preview = Undo.AddComponent<ToyPreviewCamera>(surface);
            }

            // One verb. A hand-authored "Navigate Button" wins outright; otherwise the first
            // arcade launch button that is actually LIVE in the hierarchy, and only then the first
            // one at all. Binding by name-then-position alone bound an inactive Play Button inside
            // the retired detail column while the designer's own button sat unwired on screen -
            // lit, raycasting, doing nothing. A control nobody can see is never the right target.
            var plays = AllIn(configure, "Play Button");
            var navigate = FindIn(configure, "Navigate Button")
                           ?? plays.FirstOrDefault(p => p.activeInHierarchy)
                           ?? (plays.Count > 0 ? plays[0] : null);
            foreach (var play in plays)
                if (play != navigate) changed += Deactivate(play, dryRun);

            if (navigate)
            {
                // The inherited launch button still carries the Arcade's persistent onClick calls.
                // They must go, and not for tidiness: UnityEvent.Invoke runs the persistent list
                // BEFORE the runtime one and guards neither, so one throwing entry eats every
                // AddListener handler behind it - which is exactly "the button is lit, it raycasts,
                // and it does nothing". MenuAudio.PlayAudio is kept: it is the press sound, and it
                // is the one reviewed entry on the persistent-listener allow-list.
                if (navigate.TryGetComponent(out Button navButton))
                    changed += StripForeignCalls(navButton, "Navigate", dryRun);

                if (!dryRun)
                {
                    var caption = navigate.GetComponentInChildren<TMP_Text>(true);
                    if (caption && caption.text != "NAVIGATE")
                    {
                        Undo.RecordObject(caption, "navigate caption");
                        caption.text = "NAVIGATE";
                    }
                }
            }

            // Same rule for Back: the live CloseButton, never one inside a retired branch.
            var closes = AllIn(configure, "CloseButton");
            var back = closes.FirstOrDefault(c => c.activeInHierarchy)
                       ?? (closes.Count > 0 ? closes[0] : null);

            // The window shows a PARAGRAPH where the arcade showed a caption, so the label has to
            // be sized for one and allowed to wrap. Left at the arcade's size it reads as a
            // footnote beside a picture - which is what the first pass shipped.
            var body = FindComponentIn<TMP_Text>(configure, "Game Description");
            if (body && !dryRun && body.fontSize < 26f)
            {
                _log.Add("Game Description: size the label for body copy.");
                changed++;
                Undo.RecordObject(body, "toy description size");
                body.fontSize = 28f;
                body.textWrappingMode = TextWrappingModes.Normal;
                body.alignment = TextAlignmentOptions.TopLeft;
            }

            var so = new SerializedObject(modal);
            changed += SetRef(so, "titleText", FindComponentIn<TMP_Text>(configure, "Game Name"),
                              "the toy's name", dryRun);
            changed += SetRef(so, "descriptionText", FindComponentIn<TMP_Text>(configure, "Game Description"),
                              "the toy's description", dryRun);
            changed += SetRef(so, "categoryText", FindComponentIn<TMP_Text>(configure, "Header"),
                              "the fundamental it changes", dryRun);
            changed += SetRef(so, "preview", preview, "the live toy window", dryRun);
            changed += SetRef(so, "navigateButton", navigate ? navigate.GetComponent<Button>() : null,
                              "Navigate", dryRun);
            changed += SetRef(so, "backButton", back ? back.GetComponent<Button>() : null,
                              "Back", dryRun);
            changed += SetRef(so, "crystalClickHandler",
                              FindAnyObjectByType<MenuCrystalClickHandler>(FindObjectsInactive.Include),
                              "the freestyle toggle", dryRun);
            changed += SetRef(so, "freestyleEvents",
                              AssetDatabase.LoadAssetAtPath<MenuFreestyleEventsContainerSO>(FreestyleEventsAsset),
                              "the freestyle event channel", dryRun);
            changed += SetRef(so, "screenSwitcher", switcher, "the screen switcher", dryRun);
            if (!dryRun) so.ApplyModifiedProperties();
            return changed;
        }

        // ── slot helpers ─────────────────────────────────────────────────────

        int SetRef(SerializedObject so, string property, UnityEngine.Object value, string label, bool dryRun)
        {
            var prop = so.FindProperty(property);
            if (prop == null)
            {
                _log.Add($"{so.targetObject.name}: no serialized field '{property}'.");
                return 0;
            }

            if (!value)
            {
                // In a dry run the objects this pass would CREATE do not exist yet, so silence is
                // the honest report there; on a real run it is a genuine miss and must be said.
                if (!dryRun) _log.Add($"{so.targetObject.name}.{property}: nothing found for {label}.");
                return 0;
            }

            if (prop.objectReferenceValue == value) return 0;

            _log.Add($"{so.targetObject.name}.{property} -> {value.name} ({label}).");
            if (!dryRun) prop.objectReferenceValue = value;
            return 1;
        }

        int RemoveComponent(GameObject go, string typeName, bool dryRun)
        {
            if (!go) return 0;

            int removed = 0;
            foreach (var mb in go.GetComponents<MonoBehaviour>())
            {
                if (!mb || mb.GetType().Name != typeName) continue;
                _log.Add($"{go.name}: remove {typeName}.");
                removed++;
                if (!dryRun) Undo.DestroyObjectImmediate(mb);
            }
            return removed;
        }

        int Activate(GameObject go, bool dryRun)
        {
            if (!go || go.activeSelf) return 0;
            _log.Add($"{go.name}: switch ON.");
            if (!dryRun)
            {
                Undo.RecordObject(go, "restore social column");
                go.SetActive(true);
            }
            return 1;
        }

        int Deactivate(GameObject go, bool dryRun)
        {
            if (!go || !go.activeSelf) return 0;
            _log.Add($"{go.name}: switch off (arcade content).");
            if (!dryRun)
            {
                Undo.RecordObject(go, "retire arcade branch");
                go.SetActive(false);
            }
            return 1;
        }

        static List<GameObject> AllIn(GameObject root, string wanted)
        {
            var found = new List<GameObject>();
            if (!root) return found;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name.Trim() == wanted) found.Add(t.gameObject);
            return found;
        }

        static GameObject FindIn(GameObject root, string wanted)
        {
            if (!root) return null;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name.Trim() == wanted) return t.gameObject;
            return null;
        }

        static T FindComponentIn<T>(GameObject root, string wanted) where T : Component
        {
            var go = FindIn(root, wanted);
            return go ? go.GetComponent<T>() : null;
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

        // Deliberately NOT SerializedProperty.enumValueIndex.
        //
        // `enumValueIndex` is the position in the enum's name list, not the member's value, and
        // `ModalWindows` is SPARSE - it starts at NONE = -1 and skips 2 and 6, so ARCADE = 10 sits
        // at index 9. Writing the index stored 9, which reads back as HANGAR_TRAINING, and every
        // ModalType and every hub button's target landed one member short. The failure is invisible
        // from inside the editor because the same wrong mapping is used to READ it back, so the
        // tool's own audit reported the scene clean - it was `wire_home_hub_scene.py`, reading the
        // raw serialized int from outside, that caught it. General rule: write an enum through
        // `intValue`, and keep a checker that does not share the writer's arithmetic.
    }
}
