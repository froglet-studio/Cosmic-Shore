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
    /// <para><b>Availability is authored here, not discovered.</b> Arena is <c>Available</c>
    /// since its launch window shipped (it was <c>Locked</c> — "this exists and you cannot open it
    /// yet" — while the modal behind it was only a duplicate of the arcade's); Mission is
    /// <c>Unavailable</c> ("this is not built") and stays DRAWN, because an entry that is simply
    /// absent tells the player the game has three things in it, and the day it ships they have to
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
            new() { ButtonName = "ArenaButton",   Target = ScreenSwitcher.ModalWindows.ARENA,   Availability = MenuAvailability.Available },
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
            ("ArenaGameConfigureModal",  ScreenSwitcher.ModalWindows.ARENA_GAME_CONFIGURE),
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
                "them, registers the new windows with the ScreenSwitcher, and sets Mission to " +
                "Unavailable (Arena is open: its launch window is real).", MessageType.Info);

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

        // The detail window's variant card, once converted. Renamed off the authored
        // "ToyCardTemplate" so the scene does not carry two objects under that name doing two
        // different jobs - and both names are accepted on lookup so a re-run is idempotent.
        const string VariantTemplateName = "ToyVariantTemplate";

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
            // Mirrors the ARCADE's authored state rather than switching both on: the party roster
            // is always shown, while the friends column starts HIDDEN and is opened on demand by
            // the roster's add buttons (FriendsListPanel.Show). Forcing it on made every hub window
            // open with the friends panel already up - the "double click" look.
            int changed = 0;
            foreach (var (screen, _) in Modals)
            {
                var go = FindByName(screen);
                if (!go) continue;
                changed += Activate(FindIn(go, "ArcadeLobbyList"), dryRun);
                changed += Deactivate(FindIn(go, "FriendListPanel"), dryRun);
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
            changed += ShapeCardGrid(grid, dryRun);
            changed += ShapeToyCard(template, FindIn(configure, VariantTemplateName), dryRun);
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
            layout.cellSize = ToyLayout.ToyCell;
            layout.spacing = ToyLayout.ToySpacing;
            layout.padding = ToyLayout.Padding(ToyLayout.ToyPadding);
            layout.startCorner = GridLayoutGroup.Corner.UpperLeft;
            layout.startAxis = GridLayoutGroup.Axis.Horizontal;
            layout.childAlignment = ToyLayout.GridAlignment;
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

            foreach (var dead in new[] { "FavoriteIcon", "AvatarSpace" })
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

            // Two components came across with the duplicated launch button and neither belongs on
            // a toy. They are handled BEFORE Switch is cloned from it, so the clone comes out
            // clean rather than inheriting the same two problems.
            //
            // WeeklyChallengePlayButton is deleted outright: it writes `_button.interactable`
            // from the weekly-challenge service on enable and on every challenge change, so it
            // FIGHTS ToyConfigureModal for the same property - and when there is no valid
            // challenge it simply switches Navigate off, with nothing on screen to say why. That
            // is the exact criterion this pass already uses for ArcadeExploreView.
            //
            // ControllerButtonPress is RETARGETED rather than deleted, because the pad shortcut
            // it provides is wanted - it is just aimed at the wrong window. Measured on the
            // authored scene it declared ARCADE_GAME_CONFIGURE, so pressing that pad button
            // inside the ARCADE's configure modal invoked THIS window's Navigate: a teleport and
            // a freestyle entry from a modal the player is not even looking at. Its CanvasGroup
            // guard, which would have caught it, is left unwired, so it is wired here too.
            changed += RemoveComponent(navigate, "WeeklyChallengePlayButton", dryRun);
            changed += RetargetControllerHints(configure, dryRun);

            // Resolved BEFORE the sweep below and spared from it. Switch is made by duplicating
            // the button next to it, so it can easily still be carrying the arcade's own name -
            // and a sweep that retires "every launch button that is not Navigate" would switch
            // off the designer's second button the first time this tool ran after they added it.
            var switchGo = ResolveSwitchButton(configure, navigate, dryRun, ref changed);

            foreach (var play in plays)
                if (play != navigate && play != switchGo) changed += Deactivate(play, dryRun);

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

            // ── type ─────────────────────────────────────────────────────────
            // The window inherited the ARCADE's type scale, where the title labels a card grid and
            // the description is a caption under a picture. Here those three labels ARE the left
            // column - there is nothing else in it - so each is re-sized for the job it actually
            // has rather than the one it was duplicated from.
            //
            // Every one is set as a BAND rather than a fixed size. That is not a hedge: a toy's
            // name runs from "Wanderway" to "Connect the Dots", its description is authored prose
            // of no fixed length, and a fixed size is a promise the content cannot keep - the long
            // ones clip, and a clipped label reads as broken rather than as long. A band takes the
            // ceiling when it fits and steps down when it does not, which is the same answer the
            // grid card already needed.
            //
            // The title is resolved inside GameView specifically. The variants header beside it is
            // ALSO called "Game Name" - the designer built the column by duplicating the one next
            // to it - so a search by name alone answers with whichever is earlier in the hierarchy,
            // and which one that is depends on sibling order rather than on anything meaningful.
            var title = FindComponentIn<TMP_Text>(FindIn(configure, "GameView"), "Game Name")
                        ?? FindComponentIn<TMP_Text>(configure, "Game Name");
            changed += SizeType(title, ToyLayout.TitleMin, ToyLayout.TitleMax, "the toy's name", dryRun);

            var body = FindComponentIn<TMP_Text>(configure, "Game Description");
            changed += SizeType(body, ToyLayout.BodyMin, ToyLayout.BodyMax, "the toy's description", dryRun);
            if (body && !dryRun && body.alignment != TextAlignmentOptions.TopLeft)
            {
                // A paragraph starts at the top of its box. The arcade's caption was centred in
                // one, which on a four-line description leaves it floating in the column.
                _log.Add("Game Description: start the paragraph at the top of its box.");
                changed++;
                Undo.RecordObject(body, "toy description flow");
                body.alignment = TextAlignmentOptions.TopLeft;
            }

            // ...and the header over the variants list, taken as the "Game Name" that is not the
            // title. Sized under it, because it labels a section and the toy's name names the page.
            var variantsHeader = AllIn(configure, "Game Name")
                .Select(go => go.GetComponent<TMP_Text>())
                .FirstOrDefault(t => t && t != title);
            changed += SizeType(variantsHeader, ToyLayout.HeaderMin, ToyLayout.HeaderMax, "the variants header", dryRun);

            // The variants list: the scroll view the designer added inside ConfigurationDetailView,
            // its Content, and the card it holds. Resolved through the ScrollRect's own `content`
            // rather than by looking for a child called "Content" - the modal's own root is ALSO
            // called Content and is found first, which would have bound the whole window as the
            // card parent.
            var detail = FindIn(configure, "ConfigurationDetailView");
            var scroll = detail ? detail.GetComponentInChildren<ScrollRect>(true) : null;
            var variantContent = scroll && scroll.content ? scroll.content : null;
            var variantTemplate = EnsureVariantTemplate(configure, detail, variantContent, dryRun, ref changed);
            changed += ShapeVariantsList(scroll, dryRun);

            if (detail && !scroll)
                _log.Add("ConfigurationDetailView: no ScrollRect - the variants list has nowhere " +
                         "to draw. Add the scroll view, then run this again.");

            var so = new SerializedObject(modal);
            // The same two labels the type pass sized, by the same reference - so what the tool
            // binds and what it sizes can never be two different objects.
            changed += SetRef(so, "titleText", title, "the toy's name", dryRun);
            changed += SetRef(so, "descriptionText", body, "the toy's description", dryRun);
            // The arcade's "Header" was the only candidate, and this window's authoring deleted
            // it - the category is a nice-to-have (the GRID card already shows it) and the label
            // is optional at runtime, so the tool offers several names rather than demanding one
            // back. Add a label under any of them and it binds itself.
            changed += SetRef(so, "categoryText",
                              FindComponentIn<TMP_Text>(configure, "Header")
                              ?? FindComponentIn<TMP_Text>(configure, "Category")
                              ?? FindComponentIn<TMP_Text>(configure, "Toy Category"),
                              "the fundamental it changes", dryRun);
            changed += SetRef(so, "preview", preview, "the live toy window", dryRun);
            changed += SetRef(so, "navigateButton", navigate ? navigate.GetComponent<Button>() : null,
                              "Navigate", dryRun);
            changed += SetRef(so, "switchButton", switchGo ? switchGo.GetComponent<Button>() : null,
                              "Switch", dryRun);
            changed += SetRef(so, "variantsRoot", scroll ? scroll.gameObject : null,
                              "the variants scroll view", dryRun);
            changed += SetRef(so, "variantContent", variantContent, "the variants content", dryRun);
            changed += SetRef(so, "variantCardPrefab",
                              variantTemplate ? variantTemplate.GetComponent<ToyVariantCard>() : null,
                              "the variant card template", dryRun);
            changed += SetRef(so, "backButton", back ? back.GetComponent<Button>() : null,
                              "Back", dryRun);
            changed += SetRef(so, "crystalClickHandler",
                              FindAnyObjectByType<MenuCrystalClickHandler>(FindObjectsInactive.Include),
                              "the freestyle toggle", dryRun);
            changed += SetRef(so, "screenSwitcher", switcher, "the screen switcher", dryRun);
            if (!dryRun) so.ApplyModifiedProperties();
            return changed;
        }


        /// <summary>
        /// The <b>Switch</b> button — the second verb, which commits the selected variant without
        /// flying anywhere.
        ///
        /// <para>It is looked for by NAME first, then by CAPTION, and only then as a second copy of
        /// the launch button. That order is the point: the designer makes this control by
        /// duplicating the one beside it, so its name is whatever the duplicate inherited and the
        /// only thing that reliably says which button is which is the word on it. Guessing at a
        /// second launch button is the last resort and says so in the log, because the two are
        /// interchangeable from the outside and picking the wrong one puts Navigate's job on the
        /// button reading SWITCH.</para>
        /// </summary>
        GameObject ResolveSwitchButton(GameObject configure, GameObject navigate, bool dryRun, ref int changed)
        {
            GameObject found = null;

            foreach (var name in new[] { "Switch Button", "SwitchButton", "Switch" })
            {
                var go = FindIn(configure, name);
                if (go && go != navigate) { found = go; break; }
            }

            if (!found)
                found = AllButtonsIn(configure)
                    .FirstOrDefault(b => b.gameObject != navigate && CaptionOf(b) == "SWITCH")
                    ?.gameObject;

            if (!found)
            {
                var spares = AllIn(configure, "Navigate Button").Concat(AllIn(configure, "Play Button"))
                    .Where(go => go != navigate && go.activeInHierarchy).ToList();
                if (spares.Count == 1)
                {
                    found = spares[0];
                    _log.Add($"Switch: no button named or captioned SWITCH - taking the one spare " +
                             $"launch button '{found.name}'. Rename it or set its caption if that is wrong.");
                }
            }

            // Nothing to find: make one. A window with a variants list and no Switch can select
            // and never commit, which is the worst of the three states - so rather than reporting
            // it and stopping, the tool duplicates the button beside it. That is a mechanical
            // edit with an obvious right answer (same art, same size, same band), and duplicating
            // the authored control is what keeps the pair looking like one pair. Where it SITS is
            // a look decision and stays the designer's; the default just has to not overlap.
            found ??= CreateSwitchButton(navigate, dryRun, ref changed);
            if (!found) return null;

            if (found.TryGetComponent(out Button button))
                changed += StripForeignCalls(button, "Switch", dryRun);

            if (!dryRun)
            {
                var caption = found.GetComponentInChildren<TMP_Text>(true);
                if (caption && caption.text != "SWITCH")
                {
                    Undo.RecordObject(caption, "switch caption");
                    caption.text = "SWITCH";
                }
            }

            return found;
        }


        /// <summary>
        /// Duplicate the Navigate button into a Switch button, one width to its LEFT.
        ///
        /// <para>Cloned rather than built from nothing for the reason <see cref="EnsureCardTemplate"/>
        /// is: the authored control already carries this menu's art, size and press behaviour, and
        /// two buttons that came from one object read as a pair. The clone is taken AFTER the
        /// inherited arcade components have been dealt with on the original, so it never inherits
        /// them - and its own <c>ControllerButtonPress</c> is removed even so, because a pad
        /// binding names one button and two buttons answering to it would fire both.</para>
        ///
        /// <para>Placed by ANCHOR rather than by position: the button is anchored to a fraction of
        /// its parent (0.690..0.998 on the authored scene), so a pixel offset would drift with the
        /// window's size while shifting the anchors one width left keeps the pair together at every
        /// resolution.</para>
        /// </summary>
        GameObject CreateSwitchButton(GameObject navigate, bool dryRun, ref int changed)
        {
            if (!navigate || !navigate.transform.parent)
            {
                if (!dryRun)
                    _log.Add("Switch: no Navigate button to duplicate, so none was created - the " +
                             "variants list will select but not commit.");
                return null;
            }

            _log.Add("Switch: none found - duplicating 'Navigate Button' to make one.");
            changed++;
            if (dryRun) return null;

            var clone = Instantiate(navigate, navigate.transform.parent);
            Undo.RegisterCreatedObjectUndo(clone, "create switch button");
            clone.name = "Switch Button";
            clone.transform.SetSiblingIndex(navigate.transform.GetSiblingIndex());

            if (navigate.transform is RectTransform from && clone.transform is RectTransform to)
            {
                float width = from.anchorMax.x - from.anchorMin.x;
                float shift = width + width * 0.08f;
                to.anchorMin = new Vector2(from.anchorMin.x - shift, from.anchorMin.y);
                to.anchorMax = new Vector2(from.anchorMax.x - shift, from.anchorMax.y);
                to.anchoredPosition = from.anchoredPosition;
                to.sizeDelta = from.sizeDelta;
                to.pivot = from.pivot;
            }

            // The pad binding names ONE button. Left on the clone, one press would fire Navigate
            // and Switch together - a teleport and a world swap from a single button.
            RemoveComponent(clone, "ControllerButtonPress", false);
            RemoveComponent(clone, "WeeklyChallengePlayButton", false);

            return clone;
        }

        /// <summary>
        /// Point every <c>ControllerButtonPress</c> in this window at THIS window, and give it the
        /// CanvasGroup guard it needs to stay quiet while the window is closed.
        ///
        /// <para>The list is written through <c>intValue</c>, never <c>enumValueIndex</c> — the
        /// same rule the note at the bottom of this file records, and for the same reason:
        /// <c>ModalWindows</c> is sparse, so the index and the value are different numbers and the
        /// tool's own audit would read back the wrong one.</para>
        /// </summary>
        int RetargetControllerHints(GameObject configure, bool dryRun)
        {
            if (!configure) return 0;

            var group = configure.GetComponent<CanvasGroup>();
            int changed = 0;

            foreach (var mb in configure.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (!mb || mb.GetType().Name != "ControllerButtonPress") continue;

                var so = new SerializedObject(mb);
                var list = so.FindProperty("ActiveModalWindows");
                var canvas = so.FindProperty("canvasGroup");
                bool dirty = false;

                if (list is { isArray: true } &&
                    (list.arraySize != 1 ||
                     list.GetArrayElementAtIndex(0).intValue != (int)ScreenSwitcher.ModalWindows.TOYBOX_CONFIGURE))
                {
                    _log.Add($"{mb.name}: point ControllerButtonPress at TOYBOX_CONFIGURE " +
                             $"(it answers to another window's modal today).");
                    changed++;
                    dirty = true;
                    if (!dryRun)
                    {
                        list.arraySize = 1;
                        list.GetArrayElementAtIndex(0).intValue =
                            (int)ScreenSwitcher.ModalWindows.TOYBOX_CONFIGURE;
                    }
                }

                if (group && canvas != null && !canvas.objectReferenceValue)
                {
                    _log.Add($"{mb.name}: give ControllerButtonPress the window's CanvasGroup, so " +
                             "it stays quiet while the window is closed.");
                    changed++;
                    dirty = true;
                    if (!dryRun) canvas.objectReferenceValue = group;
                }

                if (dirty && !dryRun) so.ApplyModifiedProperties();
            }

            return changed;
        }

        static List<Button> AllButtonsIn(GameObject root) =>
            root ? root.GetComponentsInChildren<Button>(true).ToList() : new List<Button>();

        static string CaptionOf(Button button)
        {
            var text = button ? button.GetComponentInChildren<TMP_Text>(true) : null;
            return text ? text.text.Trim().ToUpperInvariant() : "";
        }

        /// <summary>
        /// Turn the card the designer put inside the variants scroll view into the window's
        /// <see cref="ToyVariantCard"/> template, and take it out of the Content so it is never
        /// drawn as a row itself.
        ///
        /// <para>The same move <see cref="EnsureCardTemplate"/> makes for the toy grid, and for the
        /// same reason: reusing the authored card is what keeps this list looking like the rest of
        /// the menu without anybody authoring a second one. It is renamed off "ToyCardTemplate"
        /// because the grid's template already carries that name, and two objects with one name
        /// doing two jobs is a scene nobody can read.</para>
        /// </summary>
        GameObject EnsureVariantTemplate(GameObject configure, GameObject detail, Transform content,
            bool dryRun, ref int changed)
        {
            var existing = FindIn(configure, VariantTemplateName);
            if (existing)
            {
                // Shaped on EVERY pass, not only the conversion. The template survives a re-run,
                // so a layout fix that only ran at conversion time would never reach a scene the
                // tool had already touched - which is every scene that matters.
                changed += ShapeVariantCard(existing, dryRun);
                return existing;
            }

            // Whatever is sitting in the scroll's Content, else the authored name anywhere in the
            // detail column - the designer may have parked it outside the Content already.
            GameObject source = content && content.childCount > 0 ? content.GetChild(0).gameObject : null;
            source ??= FindIn(detail, CardTemplateName);

            if (!source)
            {
                if (!dryRun)
                    _log.Add("ConfigurationDetailView: no card to convert into a variant template. " +
                             "Put one card inside the scroll view's Content and run this again.");
                return null;
            }

            _log.Add($"ConfigurationDetailView: convert '{source.name}' into '{VariantTemplateName}'.");
            changed++;
            if (dryRun) return null;

            // Anything else parked in the Content is authoring scratch, not a row: the list is
            // drawn from the pool and a leftover child would sit in it unbound and unpressable.
            if (content)
                for (int i = content.childCount - 1; i >= 0; i--)
                {
                    var child = content.GetChild(i).gameObject;
                    if (child != source) Undo.DestroyObjectImmediate(child);
                }

            Undo.RecordObject(source, "toy variant template");
            source.name = VariantTemplateName;
            // Out of the Content: the template is instantiated per variant, never drawn itself.
            source.transform.SetParent(configure.transform, false);

            int ignored = 0;
            ignored += RemoveComponent(source, "GameCard", false);
            ignored += RemoveComponent(source, "ToyboxCard", false);

            if (!source.GetComponent<Button>()) Undo.AddComponent<Button>(source);

            // One method adds the card component, lays it out, gives it its second line and binds
            // every slot - so the conversion and a later re-run cannot produce two different cards.
            changed += ShapeVariantCard(source, false);

            source.SetActive(false);
            return source;
        }


        /// <summary>
        /// Make the variants list actually SCROLL.
        ///
        /// <para>The authored Content sits at a stretch-x, top anchor with a zero size delta, so
        /// its height is zero no matter how many rows the grid lays into it - and a ScrollRect
        /// scrolls a content RECT, not the children inside it. With the fitter left Unconstrained
        /// the list draws every row it is given and can only ever REACH the ones already inside the
        /// viewport: the rest are clipped by the viewport's Mask, which cuts the drawing off and,
        /// being a raycast filter, eats the press too. A card below the fold is therefore invisible
        /// AND dead, which is the exact failure the arcade grid shipped the day it grew to a
        /// thirteenth mode.</para>
        ///
        /// <para>Horizontal scrolling goes off with it. The grid is a fixed three-column count in a
        /// content that stretches to the viewport's width, so there is never anything to reach
        /// sideways - leaving it on only lets a drag slide the whole list off its own column.</para>
        /// </summary>
        int ShapeVariantsList(ScrollRect scroll, bool dryRun)
        {
            if (!scroll || !scroll.content) return 0;

            int changed = 0;

            if (scroll.horizontal)
            {
                _log.Add("Scroll View: vertical only (the grid's column count is fixed).");
                changed++;
                if (!dryRun)
                {
                    Undo.RecordObject(scroll, "variants scroll axis");
                    scroll.horizontal = false;
                }
            }

            if (scroll.content.TryGetComponent(out GridLayoutGroup rows)
                && (rows.cellSize != ToyLayout.VariantCell || rows.spacing != ToyLayout.VariantSpacing
                    || rows.padding.left != ToyLayout.VariantPadding + ToyLayout.GridExtraLeft
                    || rows.childAlignment != ToyLayout.GridAlignment))
            {
                _log.Add($"Scroll View/Content: rows at {ToyLayout.VariantCell.x:0}x{ToyLayout.VariantCell.y:0}, upper-left.");
                changed++;
                if (!dryRun)
                {
                    Undo.RecordObject(rows, "variant rows");
                    rows.cellSize = ToyLayout.VariantCell;
                    rows.spacing = ToyLayout.VariantSpacing;
                    rows.padding = ToyLayout.Padding(ToyLayout.VariantPadding);
                    rows.childAlignment = ToyLayout.GridAlignment;
                }
            }

            var fitter = scroll.content.GetComponent<ContentSizeFitter>();
            if (!fitter)
            {
                _log.Add("Scroll View/Content: add a ContentSizeFitter so the list can scroll.");
                changed++;
                if (!dryRun) fitter = Undo.AddComponent<ContentSizeFitter>(scroll.content.gameObject);
            }

            if (fitter && fitter.verticalFit != ContentSizeFitter.FitMode.PreferredSize)
            {
                _log.Add("Scroll View/Content: fit the HEIGHT to the rows - it was unconstrained, " +
                         "so every row past the viewport was clipped and unpressable.");
                changed++;
                if (!dryRun)
                {
                    Undo.RecordObject(fitter, "variants content fit");
                    fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                }
            }

            return changed;
        }


        // A 275x100 cell, split so the name gets the upper band and the detail line the lower one.
        // Fractions rather than pixels: the cell size is the designer's to change, and a layout
        // authored in pixels stops being a layout the moment they do.
        // The name BOTTOM-LEFT and the detail above it, both inset past the plate's chamfer.
        static readonly Vector2 CardTitleAnchorMin = new(0.06f, 0.08f);
        static readonly Vector2 CardTitleAnchorMax = new(0.74f, 0.50f);
        static readonly Vector2 CardDetailAnchorMin = new(0.06f, 0.50f);
        static readonly Vector2 CardDetailAnchorMax = new(0.94f, 0.88f);

        /// <summary>
        /// The variant card's own layout, type and slot wiring.
        ///
        /// <para>Run on every pass. The template is a duplicate of the ARCADE's game card, so its
        /// title sits in the band that card put a title in - which on a 275x100 cell is an
        /// eleven-unit strip near the top that a 27pt line overflows downward. It reads as a
        /// caption that has slid off its own card, and it is most of why the first pass looked
        /// unfinished.</para>
        ///
        /// <para>The second line is CREATED here rather than demanded from the designer, because
        /// it is the one thing a row says that its name cannot - "current", "flying", a painting's
        /// progress - and a card that cannot say it is a list of names with no state in it.</para>
        /// </summary>
        int ShapeVariantCard(GameObject card, bool dryRun)
        {
            if (!card) return 0;

            int changed = 0;

            var component = card.GetComponent<ToyVariantCard>();
            if (!component)
            {
                _log.Add($"{card.name}: add ToyVariantCard.");
                changed++;
                if (!dryRun) component = Undo.AddComponent<ToyVariantCard>(card);
            }

            changed += ShapePlates(card, dryRun);
            changed += DisableRootMask(card, dryRun);

            var title = FindComponentIn<TMP_Text>(card, "GameTitle");
            if (title)
            {
                changed += SetRect((RectTransform)title.transform,
                                   CardTitleAnchorMin, CardTitleAnchorMax,
                                   Vector2.zero, Vector2.zero,
                                   "the variant name", dryRun);

                // A NAME, not body copy: the floor is high enough that a long one shortens rather
                // than turning into small print, and the ellipsis takes what is left over.
                changed += SizeType(title, ToyLayout.VariantNameMin, ToyLayout.VariantNameMax, "the variant name", dryRun);
                if (!dryRun && (title.overflowMode != TextOverflowModes.Ellipsis
                                || title.alignment != TextAlignmentOptions.BottomLeft))
                {
                    Undo.RecordObject(title, "variant name overflow");
                    title.overflowMode = TextOverflowModes.Ellipsis;
                    title.alignment = TextAlignmentOptions.BottomLeft;
                    title.alignment = TextAlignmentOptions.Left;
                }
            }

            var detail = FindComponentIn<TMP_Text>(card, "GameDetail");
            if (!detail && title)
            {
                _log.Add($"{card.name}: add the GameDetail line (\"current\", \"flying\", progress).");
                changed++;
                if (!dryRun)
                {
                    var go = new GameObject("GameDetail", typeof(RectTransform));
                    Undo.RegisterCreatedObjectUndo(go, "variant detail line");
                    go.transform.SetParent(card.transform, false);

                    detail = Undo.AddComponent<TextMeshProUGUI>(go);
                    // The template's own face, so a card the designer restyled stays restyled.
                    detail.font = title.font;
                    detail.color = new Color(1f, 1f, 1f, 0.62f);
                    detail.alignment = TextAlignmentOptions.TopLeft;
                    detail.raycastTarget = false;
                    detail.text = string.Empty;
                }
            }

            if (detail)
            {
                changed += SetRect((RectTransform)detail.transform,
                                   CardDetailAnchorMin, CardDetailAnchorMax,
                                   Vector2.zero, Vector2.zero,
                                   "the variant detail line", dryRun);
                changed += SizeType(detail, ToyLayout.VariantDetailMin, ToyLayout.VariantDetailMax, "the variant detail line", dryRun);
            }

            if (!component || dryRun) return changed;

            var cardSo = new SerializedObject(component);
            changed += SetRef(cardSo, "background", FindComponentIn<Image>(card, "Background"),
                              "the fill", false);
            changed += SetRef(cardSo, "border", FindComponentIn<Image>(card, "Border"),
                              "the rim", false);
            changed += SetRef(cardSo, "nameText", title, "the variant name", false);
            changed += SetRef(cardSo, "detailText", detail, "the detail line", false);
            cardSo.ApplyModifiedProperties();
            return changed;
        }


        /// <summary>
        /// The Toy Box's layout numbers, in ONE place. <c>Tools/Build/author_toybox_layout.py</c>
        /// writes the same values into the scene from outside the editor (so the layout lands on
        /// the branch rather than in somebody's working tree) and
        /// <c>wire_home_hub_scene.py --check</c> audits them; the three must agree, and this is
        /// the copy the editor reads.
        ///
        /// <para>Every type value is a BAND (autosize min..max), halved from the first pass's
        /// 42-58 / 22-44 / 34-44 / 22-34 / 15-21 - on a window whose whole left column is three
        /// labels those read as a poster. The toy card is 400x250 because the card the arcade
        /// authored was 275x203 and the grid was forcing it into 260x96: the plates overran the
        /// cell and the grid read as a strip of small overlapping tiles.</para>
        /// </summary>
        static class ToyLayout
        {
            public const float TitleMin = 28f, TitleMax = 36f;
            public const float BodyMin = 16f, BodyMax = 22f;
            public const float HeaderMin = 22f, HeaderMax = 28f;
            public const float VariantNameMin = 16f, VariantNameMax = 22f;
            public const float VariantDetailMin = 12f, VariantDetailMax = 14f;
            public const float CardNameMin = 18f, CardNameMax = 26f;
            public const float CardTaglineMin = 11f, CardTaglineMax = 14f;
            public const float CardSectionMin = 10f, CardSectionMax = 13f;

            public static readonly Vector2 ToyCell = new(400f, 250f);
            public static readonly Vector2 ToySpacing = new(20f, 20f);
            public const int ToyPadding = 16;
            public static readonly Vector2 VariantCell = new(275f, 88f);
            public static readonly Vector2 VariantSpacing = new(16f, 14f);
            public const int VariantPadding = 12;

            // Both grids start UPPER-LEFT: centred, a lone row (the Wanderway's one "Wander") sat
            // in the middle of an empty strip and read as a misplaced card. The extra left inset
            // keeps the first column off the window's edge now that nothing centres it.
            public const TextAnchor GridAlignment = TextAnchor.UpperLeft;
            public const int GridExtraLeft = 20;
            public static RectOffset Padding(int all) => new(all + GridExtraLeft, all, all, all);

            // The grid card shows its title and art only; the tagline/category lines stay
            // authored and bound, switched off.
            public const bool CardLabelsActive = false;

            /// <summary>
            /// pixelsPerUnitMultiplier that draws the plates' 9-slice at design scale on the card's
            /// canvas. The sprites are authored at PPU 400 (design x4) and UGUI divides that by the
            /// canvas's referencePixelsPerUnit before slicing - Menu_Main's is 240, so a 20-unit
            /// border came out at 48 and the chamfer read at twice its size. Read off the canvas,
            /// never written down, for the same reason author_toybox_layout.py reads it off the scene.
            /// </summary>
            public static float SliceMultiplier(GameObject card)
            {
                var canvas = card ? card.GetComponentInParent<Canvas>(true) : null;
                return canvas ? canvas.referencePixelsPerUnit / 100f : 1f;
            }

            // card anchors as FRACTIONS of the cell - the cell is the designer's to change
            public static readonly Vector2 PortraitMin = new(0.06f, 0.36f), PortraitMax = new(0.94f, 0.95f);
            public static readonly Vector2 NameMin = new(0.06f, 0.06f), NameMax = new(0.94f, 0.34f);
            public static readonly Vector2 TaglineMin = new(0.06f, 0.05f), TaglineMax = new(0.68f, 0.19f);
            public static readonly Vector2 SectionMin = new(0.68f, 0.05f), SectionMax = new(0.94f, 0.19f);
        }

        /// <summary>
        /// The toy grid's cell, spacing and padding, on EVERY pass (EnsureCardLayout only runs
        /// when the grid has no layout yet), plus the fitter and top anchor that let it scroll -
        /// the same fix the variants list needed (Docs/HomeHub/ARCHITECTURE.md 5.4.3).
        /// </summary>
        int ShapeCardGrid(GameObject grid, bool dryRun)
        {
            if (!grid) return 0;
            int changed = 0;

            if (grid.TryGetComponent(out GridLayoutGroup layout)
                && (layout.cellSize != ToyLayout.ToyCell || layout.spacing != ToyLayout.ToySpacing
                    || layout.padding.left != ToyLayout.ToyPadding + ToyLayout.GridExtraLeft
                    || layout.childAlignment != ToyLayout.GridAlignment))
            {
                _log.Add($"GameGrid: cells at {ToyLayout.ToyCell.x:0}x{ToyLayout.ToyCell.y:0}.");
                changed++;
                if (!dryRun)
                {
                    Undo.RecordObject(layout, "toy grid cells");
                    layout.cellSize = ToyLayout.ToyCell;
                    layout.spacing = ToyLayout.ToySpacing;
                    layout.padding = ToyLayout.Padding(ToyLayout.ToyPadding);
                    layout.childAlignment = ToyLayout.GridAlignment;
                }
            }

            var rect = (RectTransform)grid.transform;
            var top = new Vector2(0.5f, 1f);
            if (rect.anchorMin != new Vector2(0f, 1f) || rect.anchorMax != Vector2.one || rect.pivot != top)
            {
                _log.Add("GameGrid: anchor to the top of the viewport so the fitter can grow it.");
                changed++;
                if (!dryRun)
                {
                    Undo.RecordObject(rect, "toy grid anchor");
                    rect.pivot = top;
                    rect.anchorMin = new Vector2(0f, 1f);
                    rect.anchorMax = Vector2.one;
                    rect.anchoredPosition = Vector2.zero;
                    rect.sizeDelta = Vector2.zero;
                }
            }

            if (!grid.TryGetComponent(out ContentSizeFitter fitter))
            {
                _log.Add("GameGrid: add a ContentSizeFitter so a third row of toys can scroll.");
                changed++;
                if (!dryRun) fitter = Undo.AddComponent<ContentSizeFitter>(grid);
            }
            if (fitter && fitter.verticalFit != ContentSizeFitter.FitMode.PreferredSize)
            {
                changed++;
                if (!dryRun)
                {
                    Undo.RecordObject(fitter, "toy grid fit");
                    fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                }
            }
            return changed;
        }

        /// <summary>
        /// A card's two plates - its Background and Border, plus the rim on its own root - drawn
        /// SLICED and stretched to the card. The sprites carry a 9-slice border since
        /// <c>author_toy_card_sprites.py</c>; drawn Simple they stretch the chamfer, which on a
        /// 275x88 row squashes a 228x170 picture to 3:1 and reads as bent corners.
        /// </summary>
        int ShapePlates(GameObject card, bool dryRun)
        {
            if (!card) return 0;
            int changed = 0;

            var plates = new List<Image>();
            if (card.TryGetComponent(out Image root) && root.sprite) plates.Add(root);
            foreach (var name in new[] { "Background", "Border" })
            {
                var img = FindComponentIn<Image>(card, name);
                if (!img) continue;
                plates.Add(img);
                changed += SetRect((RectTransform)img.transform, Vector2.zero, Vector2.one,
                                   Vector2.zero, Vector2.zero, $"{card.name}/{name} stretched to the card", dryRun);
            }

            float multiplier = ToyLayout.SliceMultiplier(card);
            foreach (var img in plates)
            {
                if (img.type == Image.Type.Sliced && Mathf.Approximately(img.pixelsPerUnitMultiplier, multiplier))
                    continue;
                _log.Add($"{card.name}/{img.name}: draw the plate sliced at x{multiplier:0.##}.");
                changed++;
                if (dryRun) continue;
                Undo.RecordObject(img, "card plate");
                img.type = Image.Type.Sliced;
                img.fillCenter = true;
                img.pixelsPerUnitMultiplier = multiplier;
            }
            return changed;
        }

        /// <summary>
        /// The card templates inherited a root <see cref="Mask"/> from the arcade card, which
        /// clips every child to the rim sprite's alpha - i.e. to the chamfer, so the first letter
        /// of a name lost its corner. Off rather than removed, so the component list is untouched.
        /// </summary>
        int DisableRootMask(GameObject card, bool dryRun)
        {
            if (!card || !card.TryGetComponent(out Mask mask) || !mask.enabled) return 0;
            _log.Add($"{card.name}: switch the root Mask off (it clipped the text to the chamfer).");
            if (!dryRun)
            {
                Undo.RecordObject(mask, "card mask");
                mask.enabled = false;
            }
            return 1;
        }

        /// <summary>
        /// The toy card's own layout: plates, a portrait filling the upper two thirds, the name
        /// under it, and the tagline + category line <see cref="ToyboxCard"/> was already written
        /// to show and had nothing bound to. Run on every pass, like <see cref="ShapeVariantCard"/>,
        /// and for the same reason.
        /// </summary>
        int ShapeToyCard(GameObject card, GameObject variantTemplate, bool dryRun)
        {
            if (!card) return 0;
            int changed = 0;

            changed += ShapePlates(card, dryRun);
            changed += DisableRootMask(card, dryRun);

            var portrait = FindComponentIn<Image>(card, "VesselIcon");
            if (portrait)
            {
                changed += SetRect((RectTransform)portrait.transform, ToyLayout.PortraitMin, ToyLayout.PortraitMax,
                                   Vector2.zero, Vector2.zero, "the toy portrait", dryRun);
                if (!portrait.preserveAspect)
                {
                    changed++;
                    if (!dryRun) { Undo.RecordObject(portrait, "portrait aspect"); portrait.preserveAspect = true; }
                }
            }

            var title = FindComponentIn<TMP_Text>(card, "GameTitle");
            if (title)
            {
                changed += SetRect((RectTransform)title.transform, ToyLayout.NameMin, ToyLayout.NameMax,
                                   Vector2.zero, Vector2.zero, "the toy name", dryRun);
                changed += SizeType(title, ToyLayout.CardNameMin, ToyLayout.CardNameMax, "the toy card name", dryRun);
                if (!dryRun && (title.overflowMode != TextOverflowModes.Ellipsis
                                || title.alignment != TextAlignmentOptions.Left))
                {
                    Undo.RecordObject(title, "toy name overflow");
                    title.overflowMode = TextOverflowModes.Ellipsis;
                    title.alignment = TextAlignmentOptions.Left;
                }
            }

            // The donor for the two new lines: the variant card's own detail line, so the card
            // carries the project's font and material rather than TMP's defaults.
            var donor = variantTemplate ? FindComponentIn<TMP_Text>(variantTemplate, "GameDetail") : title;
            var tagline = EnsureCardLabel(card, "Tagline", donor, ToyLayout.TaglineMin, ToyLayout.TaglineMax,
                                          ToyLayout.CardTaglineMin, ToyLayout.CardTaglineMax,
                                          TextAlignmentOptions.TopLeft, 0.72f, dryRun, ref changed);
            var section = EnsureCardLabel(card, "Section", donor, ToyLayout.SectionMin, ToyLayout.SectionMax,
                                          ToyLayout.CardSectionMin, ToyLayout.CardSectionMax,
                                          TextAlignmentOptions.TopRight, 0.6f, dryRun, ref changed);

            // Bound and SWITCHED OFF: on the grid the title is the whole card (the arcade's cards
            // carry a title and art, nothing else); the sentence lives on the detail window.
            foreach (var extra in new[] { tagline, section })
            {
                if (!extra || extra.gameObject.activeSelf == ToyLayout.CardLabelsActive) continue;
                _log.Add($"{card.name}/{extra.name}: {(ToyLayout.CardLabelsActive ? "shown" : "hidden")} on the grid card.");
                changed++;
                if (!dryRun)
                {
                    Undo.RecordObject(extra.gameObject, "toy card label");
                    extra.gameObject.SetActive(ToyLayout.CardLabelsActive);
                }
            }

            if (dryRun || !card.TryGetComponent(out ToyboxCard toyCard)) return changed;
            var so = new SerializedObject(toyCard);
            changed += SetRef(so, "taglineText", tagline, "the toy tagline", false);
            changed += SetRef(so, "sectionText", section, "the toy category", false);
            so.ApplyModifiedProperties();
            return changed;
        }

        TMP_Text EnsureCardLabel(GameObject card, string name, TMP_Text donor, Vector2 anchorMin, Vector2 anchorMax,
                                 float min, float max, TextAlignmentOptions align, float alpha,
                                 bool dryRun, ref int changed)
        {
            var label = FindComponentIn<TMP_Text>(card, name);
            if (!label)
            {
                _log.Add($"{card.name}: add the {name} line.");
                changed++;
                if (dryRun) return null;

                var go = new GameObject(name, typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(go, "toy card label");
                go.transform.SetParent(card.transform, false);
                label = Undo.AddComponent<TextMeshProUGUI>(go);
                if (donor) label.font = donor.font;
                label.color = new Color(1f, 1f, 1f, alpha);
                label.raycastTarget = false;
                label.text = string.Empty;
                label.overflowMode = TextOverflowModes.Ellipsis;
                label.alignment = align;
            }

            changed += SetRect((RectTransform)label.transform, anchorMin, anchorMax,
                               Vector2.zero, Vector2.zero, $"the {name} line", dryRun);
            changed += SizeType(label, min, max, $"the toy card {name.ToLowerInvariant()}", dryRun);
            return label;
        }

        /// <summary>
        /// Give a label an autosize BAND, and only write when the band actually differs so a
        /// re-run on an already-authored scene reports nothing and dirties nothing.
        ///
        /// <para>A band rather than a size, everywhere: these labels carry toy names and authored
        /// prose of no fixed length, and a fixed size on any of them is a clip waiting for the
        /// first long one. Autosizing off at a fixed size is what the arcade authored, which is
        /// correct for a caption over a card grid and wrong for the whole left column of a
        /// window.</para>
        /// </summary>
        int SizeType(TMP_Text text, float min, float max, string label, bool dryRun)
        {
            if (!text) return 0;

            if (text.enableAutoSizing
                && Mathf.Approximately(text.fontSizeMin, min)
                && Mathf.Approximately(text.fontSizeMax, max)
                && text.textWrappingMode == TextWrappingModes.Normal)
                return 0;

            _log.Add($"{text.name}: size {label} at {min:0}-{max:0}.");
            if (dryRun) return 1;

            Undo.RecordObject(text, "toy window type");
            text.enableAutoSizing = true;
            text.fontSizeMin = min;
            text.fontSizeMax = max;
            // Without somewhere to wrap, a band answers a long line by shrinking it to nothing -
            // which is the failure the band is here to avoid, wearing a different costume.
            text.textWrappingMode = TextWrappingModes.Normal;
            return 1;
        }

        /// <summary>
        /// Anchor a rect, idempotently. Pivot is part of the comparison: the arcade's labels are
        /// authored at a top-left pivot, so a rect that matched on anchors alone would be left
        /// hanging off its own band.
        /// </summary>
        int SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
                    Vector2 offsetMin, Vector2 offsetMax, string label, bool dryRun)
        {
            if (!rect) return 0;

            var pivot = new Vector2(0.5f, 0.5f);
            if (rect.anchorMin == anchorMin && rect.anchorMax == anchorMax
                && rect.offsetMin == offsetMin && rect.offsetMax == offsetMax
                && rect.pivot == pivot)
                return 0;

            _log.Add($"{rect.name}: lay out {label}.");
            if (dryRun) return 1;

            Undo.RecordObject(rect, "toy card layout");
            rect.pivot = pivot;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return 1;
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
