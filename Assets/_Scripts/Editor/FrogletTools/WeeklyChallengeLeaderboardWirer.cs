using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Editor.Froglet
{
    /// <summary>
    /// Wires the weekly challenge leaderboard window from the hierarchy it is sitting in.
    ///
    /// <para><b>Why a tool and not an inspector pass.</b> The window is ~16 serialized references
    /// across two components, and every one of them is a name you can only get wrong silently: a
    /// tab pointed at the wrong button switches the wrong scope, an unwired backdrop leaves a
    /// tooltip that will not close, and a missed <c>rowContainer</c> spawns every row on the modal
    /// root behind the artwork. None of those throw. Resolving them from the hierarchy makes the
    /// wiring reproducible and re-runnable, and turns "did I miss one?" into a report.</para>
    ///
    /// <para><b>It never overwrites a reference you set by hand</b> unless you ask. Re-running is
    /// therefore always safe, which is what makes it a repair tool as well as a bring-up tool.</para>
    /// </summary>
    public class WeeklyChallengeLeaderboardWirer : EditorWindow
    {
        const string ToolName = "Wire Weekly Challenge Leaderboard";

        static readonly FrogletToolShipContext Ship = new(ToolName)
        {
            // PERMANENT, not a one-off: the window is re-wired every time its art is re-laid, and
            // an empty ToolScriptPaths hides the Retire button, which is the correct state here.
            Validate = () => ValidateActive(),
            CommitType = "chore",
            CommitScope = "ui",
        };

        [MenuItem("FrogletTools/Interface/Wire Weekly Challenge Leaderboard", false, 30)]
        [FrogletTool(FrogletToolCategory.Interface, Importance = 3,
            Description = "Resolve every reference on the weekly leaderboard modal and its row " +
                          "panel from the hierarchy, and report what is missing.")]
        public static void Open() =>
            GetWindow<WeeklyChallengeLeaderboardWirer>(false, "Leaderboard Wirer").minSize =
                new Vector2(460f, 420f);

        [SerializeField] WeeklyChallengeLeaderboardModal modal;
        [SerializeField] SO_ProfileIconList profileIcons;
        [SerializeField] bool overwriteExisting;
        [SerializeField] bool movePanelToScrollContent = true;

        Vector2 _scroll;
        readonly List<Line> _report = new();

        readonly struct Line
        {
            public readonly string Field, Target, Note;
            public readonly Status State;
            public Line(Status state, string field, string target, string note = null)
            {
                State = state; Field = field; Target = target; Note = note;
            }
        }

        enum Status { Wired, Kept, Missing, Fixed }

        // ── Window ─────────────────────────────────────────────────────────────

        void OnGUI()
        {
            FrogletEditorPalette.Banner("Weekly Challenge Leaderboard",
                "Resolve the window's references from its own hierarchy",
                FrogletEditorPalette.ColorFor(FrogletToolCategory.Interface));

            EditorGUILayout.Space(4);

            using (var change = new EditorGUI.ChangeCheckScope())
            {
                modal = (WeeklyChallengeLeaderboardModal)EditorGUILayout.ObjectField(
                    new GUIContent("Modal",
                        "The LeaderboardConfigureModal. Leave empty and the open scenes are " +
                        "searched for exactly one."),
                    modal, typeof(WeeklyChallengeLeaderboardModal), true);

                profileIcons = (SO_ProfileIconList)EditorGUILayout.ObjectField(
                    new GUIContent("Profile icons",
                        "Resolves a leaderboard row's avatar id to a sprite. Left empty, the " +
                        "project is searched for exactly one asset."),
                    profileIcons, typeof(SO_ProfileIconList), false);

                if (change.changed) _report.Clear();
            }

            overwriteExisting = EditorGUILayout.ToggleLeft(
                new GUIContent("Overwrite references already set",
                    "OFF by default: a reference you set by hand outranks anything this tool " +
                    "infers from a name. Turn it on to re-resolve everything from scratch."),
                overwriteExisting);

            movePanelToScrollContent = EditorGUILayout.ToggleLeft(
                new GUIContent("Move the row panel onto the scroll Content",
                    "The panel works from anywhere as long as rowContainer points at the scroll " +
                    "Content. Moving it there makes the component sit on the object it draws " +
                    "into, which is where the next person will look for it."),
                movePanelToScrollContent);

            EditorGUILayout.Space(6);

            using (new EditorGUI.DisabledScope(!ResolveModal()))
            {
                if (FrogletEditorPalette.ColorButton("Wire it", FrogletEditorPalette.Ok, 120f, 26f))
                    Run(dryRun: false);

                if (FrogletEditorPalette.ColorButton("Report only", FrogletEditorPalette.Info, 120f, 22f))
                    Run(dryRun: true);
            }

            if (!ResolveModal())
                EditorGUILayout.HelpBox(
                    "No WeeklyChallengeLeaderboardModal in the open scenes. Add the component to " +
                    "LeaderboardConfigureModal first, or drag it in above.", MessageType.Info);

            DrawReport();

            FrogletToolShipPanel.Draw(Ship, this);
        }

        void DrawReport()
        {
            if (_report.Count == 0) return;

            FrogletEditorPalette.HorizontalRule();

            int missing = _report.Count(l => l.State == Status.Missing);
            EditorGUILayout.LabelField(
                missing == 0 ? "Everything resolved." : $"{missing} reference(s) could not be resolved.",
                FrogletEditorPalette.SectionHeader);

            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MinHeight(160f));
            foreach (var line in _report)
            {
                var accent = line.State switch
                {
                    Status.Wired => FrogletEditorPalette.Ok,
                    Status.Fixed => FrogletEditorPalette.Info,
                    Status.Kept => FrogletEditorPalette.Muted,
                    _ => FrogletEditorPalette.Warn,
                };

                using (new EditorGUILayout.HorizontalScope())
                {
                    var pill = GUILayoutUtility.GetRect(60f, 16f, GUILayout.Width(60f));
                    FrogletEditorPalette.StatusPill(pill, line.State.ToString().ToUpperInvariant(), accent);
                    EditorGUILayout.LabelField(line.Field, GUILayout.Width(180f));
                    EditorGUILayout.LabelField(line.Target ?? "—");
                }

                if (!string.IsNullOrEmpty(line.Note))
                    EditorGUILayout.LabelField("     " + line.Note, EditorStyles.miniLabel);
            }
            EditorGUILayout.EndScrollView();
        }

        // ── Wiring ─────────────────────────────────────────────────────────────

        bool ResolveModal()
        {
            if (modal) return true;

            var found = FindObjectsByType<WeeklyChallengeLeaderboardModal>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            // Exactly one, or none. TWO is a real state (a duplicated modal) and picking one
            // silently would wire the wrong window and read as the tool not working.
            if (found.Length == 1) modal = found[0];
            return modal;
        }

        void Run(bool dryRun)
        {
            _report.Clear();
            if (!ResolveModal()) return;

            var root = modal.transform;
            var panel = EnsurePanel(root, dryRun);

            WireModal(root, panel, dryRun);
            if (panel) WirePanel(panel, dryRun);

            if (dryRun) return;

            EditorUtility.SetDirty(modal);
            if (panel) EditorUtility.SetDirty(panel);

            // Recorded in the same pass that wrote it: a path recorded later can be missed by an
            // early return, and the scene IS the deliverable here.
            FrogletToolChangeLedger.RecordOpenScenes(ToolName);
        }

        /// <summary>
        /// Find the row panel, or add it. It is a SEPARATE component from the modal on purpose —
        /// the modal owns the window's decisions and the panel owns the rows — but where it SITS
        /// is free, so this puts it on the scroll Content where the next person will look.
        /// </summary>
        WeeklyChallengeLeaderboardPanel EnsurePanel(Transform root, bool dryRun)
        {
            var panel = root.GetComponentInChildren<WeeklyChallengeLeaderboardPanel>(true);
            var content = FindScrollContent(root);

            if (panel && movePanelToScrollContent && content && panel.transform != content)
            {
                _report.Add(new Line(Status.Kept, "panel location", panel.gameObject.name,
                    "Already on another object. Moving a MonoBehaviour would drop its serialized " +
                    "values, so it is left where it is - rowContainer below points it at the list."));
            }

            if (panel) return panel;

            var host = movePanelToScrollContent && content ? content.gameObject : root.gameObject;
            if (dryRun)
            {
                _report.Add(new Line(Status.Missing, "row panel", host.name,
                    "Would add WeeklyChallengeLeaderboardPanel here."));
                return null;
            }

            panel = Undo.AddComponent<WeeklyChallengeLeaderboardPanel>(host);
            _report.Add(new Line(Status.Fixed, "row panel", host.name, "Component added."));
            return panel;
        }

        void WireModal(Transform root, WeeklyChallengeLeaderboardPanel panel, bool dryRun)
        {
            var so = new SerializedObject(modal);
            var tabs = Find(root, "ButtonTabs");
            var reward = Find(root, "RankRewardPanel");

            Set(so, "panel", panel, dryRun);
            Set(so, "contentRoot", FindRect(root, "Content"), dryRun);
            Set(so, "timeLeftText", Component<TMP_Text>(Find(root, "Time")), dryRun);
            Set(so, "challengeTitleText", Component<TMP_Text>(Find(root, "LeaderboardHeader")), dryRun);

            Set(so, "worldTab", Component<Button>(Find(tabs, "WorldLeaderboard")), dryRun);
            Set(so, "regionalTab", Component<Button>(Find(tabs, "RegionalLeaderboard")), dryRun);
            Set(so, "friendsTab", Component<Button>(Find(tabs, "FriendsLeaderboard")), dryRun);

            Set(so, "rankRewardButton", Component<Button>(Find(root, "RankRewardButton")), dryRun);
            Set(so, "rankRewardPanel", reward ? reward.gameObject : null, dryRun);

            // The backdrop is the FIRST RankBG under the reward panel. There is a second RankBG
            // deeper inside it (the tier table's own background) and a third on the leaderboard
            // ROW - three objects sharing a name, which is exactly why this resolves by PATH from
            // a known parent rather than by a name search over the whole window.
            Set(so, "rankRewardBackdrop", FindRect(reward, "RankBG"), dryRun);

            Set(so, "closeButton", Component<Button>(Find(root, "CloseButton")), dryRun);

            var switcher = FindObjectsByType<ScreenSwitcher>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (switcher.Length == 1)
                Set(so, "screenSwitcher", switcher[0], dryRun);
            else
                _report.Add(new Line(Status.Missing, "screenSwitcher", null,
                    $"{switcher.Length} ScreenSwitchers in the open scenes - wire it by hand."));

            SetEnum(so, "ModalType", (int)ScreenSwitcher.ModalWindows.WEEKLY_CHALLENGE_LEADERBOARD,
                "WEEKLY_CHALLENGE_LEADERBOARD", dryRun);

            if (!dryRun) so.ApplyModifiedProperties();
        }

        void WirePanel(WeeklyChallengeLeaderboardPanel panel, bool dryRun)
        {
            var so = new SerializedObject(panel);
            var content = FindScrollContent(modal.transform);

            Set(so, "rowContainer", content, dryRun);

            var template = ResolveRowTemplate(content);
            Set(so, "rowTemplate", template, dryRun);

            if (template)
            {
                Set(so, "templateRank", FindDeep<TMP_Text>(template, "RankText"), dryRun);
                Set(so, "templateAvatar", FindDeep<Image>(template, "AvatarIcon"), dryRun);
                Set(so, "templateName", FindDeep<TMP_Text>(template, "Username"), dryRun);
                Set(so, "templateScore", FindDeep<TMP_Text>(template, "ScoreText"), dryRun);

                // The row's OWN Image is what the podium tints. Not a child's - tinting the rank
                // badge would leave the row itself un-podiumed.
                Set(so, "templateBackground", template.GetComponent<Image>(), dryRun);
            }

            Set(so, "profileIcons", ResolveProfileIcons(), dryRun);

            if (!dryRun) so.ApplyModifiedProperties();
        }

        /// <summary>
        /// The row template, whether it is still in the scene or has been extracted to a prefab.
        /// A PREFAB ASSET is preferred when one is already assigned, because extracting the row is
        /// the direction this is going and re-pointing it at the leftover scene copy every run
        /// would undo that.
        /// </summary>
        RectTransform ResolveRowTemplate(RectTransform content)
        {
            if (!content) return null;

            // `new SerializedObject(null)` THROWS, so the component is resolved first. There is no
            // panel at all on a first run, which is the normal path rather than an error.
            var existingPanel = content.GetComponentInChildren<WeeklyChallengeLeaderboardPanel>(true)
                             ?? modal.GetComponentInChildren<WeeklyChallengeLeaderboardPanel>(true);

            var assigned = existingPanel
                ? new SerializedObject(existingPanel).FindProperty("rowTemplate")?.objectReferenceValue as RectTransform
                : null;

            if (assigned && !assigned.gameObject.scene.IsValid())
            {
                _report.Add(new Line(Status.Kept, "rowTemplate", assigned.name,
                    "A prefab asset is already assigned - kept, never re-pointed at a scene copy."));
                return assigned;
            }

            // An in-scene copy is a perfectly good template (a prefab INSTANCE left in the list is
            // the normal Unity shape), and it is preferred over a project search because it is the
            // one you can see laid out.
            var child = FindRect(content, "LeaderboardContent");
            if (child) return child;

            var stray = content.childCount > 0 ? content.GetChild(0) as RectTransform : null;
            if (stray) return stray;

            // Nothing in the scene: the row has been EXTRACTED to a prefab and the instance
            // deleted, which is the cleaner shape (no stray object, nothing to hide at Awake). The
            // hierarchy cannot answer this one, so the project is searched - and only an
            // unambiguous answer is accepted, because wiring the wrong prefab draws a row that
            // looks almost right and is very hard to attribute.
            return FindRowTemplateAsset();
        }

        /// <summary>
        /// The row prefab in the project, when there is exactly one plausible candidate.
        ///
        /// <para>Matched on SHAPE, not just on name: a prefab qualifies only if it carries the
        /// parts a row needs (a rank text, a name text, a score text). A name search alone would
        /// happily wire a prefab someone else called <c>LeaderboardContent</c>, and the failure -
        /// rows that draw but stay blank - reads as the fetch being broken rather than as the
        /// wrong prefab.</para>
        /// </summary>
        static RectTransform FindRowTemplateAsset()
        {
            var candidates = new List<RectTransform>();

            foreach (string guid in AssetDatabase.FindAssets("LeaderboardContent t:Prefab"))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (!go) continue;

                var rect = go.transform as RectTransform;
                if (!rect) continue;

                bool looksLikeARow =
                    FindDeep<TMP_Text>(rect, "RankText") &&
                    FindDeep<TMP_Text>(rect, "Username") &&
                    FindDeep<TMP_Text>(rect, "ScoreText");

                if (looksLikeARow) candidates.Add(rect);
            }

            return candidates.Count == 1 ? candidates[0] : null;
        }

        SO_ProfileIconList ResolveProfileIcons()
        {
            if (profileIcons) return profileIcons;

            var guids = AssetDatabase.FindAssets("t:" + nameof(SO_ProfileIconList));
            if (guids.Length != 1) return null;   // ambiguous is not a guess worth making

            profileIcons = AssetDatabase.LoadAssetAtPath<SO_ProfileIconList>(
                AssetDatabase.GUIDToAssetPath(guids[0]));
            return profileIcons;
        }

        // ── Assignment ─────────────────────────────────────────────────────────

        void Set(SerializedObject so, string field, UnityEngine.Object value, bool dryRun)
        {
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                _report.Add(new Line(Status.Missing, field, null, "No such serialized field."));
                return;
            }

            if (prop.objectReferenceValue && !overwriteExisting)
            {
                _report.Add(new Line(Status.Kept, field, prop.objectReferenceValue.name,
                    "Already set - a hand-wired reference outranks an inferred one."));
                return;
            }

            if (!value)
            {
                _report.Add(new Line(Status.Missing, field, null,
                    "Not found in the hierarchy. Optional fields are safe to leave empty."));
                return;
            }

            if (!dryRun) prop.objectReferenceValue = value;
            _report.Add(new Line(dryRun ? Status.Missing : Status.Wired, field, value.name,
                dryRun ? "Would wire this." : null));
        }

        void Set(SerializedObject so, string field, Component value, bool dryRun) =>
            Set(so, field, (UnityEngine.Object)value, dryRun);

        void SetEnum(SerializedObject so, string field, int value, string label, bool dryRun)
        {
            var prop = so.FindProperty(field);
            if (prop == null) return;

            if (prop.intValue == value)
            {
                _report.Add(new Line(Status.Kept, field, label));
                return;
            }

            if (!dryRun) prop.intValue = value;
            _report.Add(new Line(dryRun ? Status.Missing : Status.Wired, field, label));
        }

        // ── Hierarchy lookup ───────────────────────────────────────────────────

        /// <summary>
        /// A direct-or-deep child by exact name, searched from <paramref name="root"/> only. Scoped
        /// rather than global because three objects in this window are called <c>RankBG</c>.
        /// </summary>
        static Transform Find(Transform root, string name)
        {
            if (!root) return null;
            if (root.name == name) return root;

            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name)
                    return t;

            return null;
        }

        /// <summary>
        /// <see cref="Find"/> as a <see cref="RectTransform"/>, for the fields that are typed that
        /// way. Every UI object's transform IS one, so this never loses a hit - but saying so in
        /// the signature is what lets the compiler check it instead of it being true by accident.
        /// </summary>
        static RectTransform FindRect(Transform root, string name) => Find(root, name) as RectTransform;

        static T FindDeep<T>(Transform root, string name) where T : Component
        {
            var t = Find(root, name);
            return t ? t.GetComponent<T>() : null;
        }

        static T Component<T>(Transform t) where T : Component => t ? t.GetComponent<T>() : null;

        static RectTransform FindScrollContent(Transform root)
        {
            var scroll = root.GetComponentInChildren<ScrollRect>(true);
            if (scroll && scroll.content) return scroll.content;

            var view = Find(root, "Viewport");
            return view ? FindRect(view, "Content") : null;
        }

        // ── Validation (the ship panel's gate) ─────────────────────────────────

        static FrogletToolValidation ValidateActive()
        {
            var found = FindObjectsByType<WeeklyChallengeLeaderboardModal>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            if (found.Length == 0)
                return FrogletToolValidation.Fail(
                    "No leaderboard modal in the open scenes.",
                    "Open Menu_Main before pushing - the scene IS this tool's output.");

            var problems = new List<string>();
            foreach (var m in found)
            {
                var so = new SerializedObject(m);

                // Only the pieces WITHOUT which the window is broken rather than merely plainer.
                // Every other field is legitimately optional and must not fail a push.
                Require(so, "panel", "the row list", problems);
                Require(so, "closeButton", "the close button", problems);

                // A panel that cannot produce a ROW draws an empty board forever, and an empty
                // board is exactly what a leaderboard nobody has finished looks like - so this
                // failure is invisible without the check. The panel accepts either an explicit
                // template (scene object or prefab asset) or the container's first child.
                var panelRef = so.FindProperty("panel")?.objectReferenceValue
                    as WeeklyChallengeLeaderboardPanel;
                if (panelRef) RequireRowSource(panelRef, problems);

                var reward = so.FindProperty("rankRewardPanel")?.objectReferenceValue;
                var backdrop = so.FindProperty("rankRewardBackdrop")?.objectReferenceValue;
                if (reward && !backdrop)
                    problems.Add("rankRewardPanel is wired but rankRewardBackdrop is not - the " +
                                 "tooltip would open and never close.");

                if (reward is GameObject rewardGo && rewardGo.activeSelf)
                    problems.Add("RankRewardPanel is ACTIVE in the scene. It must start inactive, " +
                                 "or it is on screen the moment the window opens.");
            }

            return problems.Count == 0
                ? FrogletToolValidation.Pass($"{found.Length} leaderboard modal(s) wired.")
                : FrogletToolValidation.Fail("The leaderboard window is not fully wired.", problems);
        }

        /// <summary>
        /// The panel must have SOMETHING to clone. Mirrors <c>EnsureTemplate</c>'s own fallback
        /// order rather than demanding an explicit reference, because "the container's first
        /// child" is a supported way to author this and failing it would be a false alarm.
        /// </summary>
        static void RequireRowSource(WeeklyChallengeLeaderboardPanel panel, List<string> problems)
        {
            var so = new SerializedObject(panel);

            if (so.FindProperty("rowTemplate")?.objectReferenceValue) return;

            // NOT `?? panel.transform`. A serialized reference whose target was deleted comes back
            // as Unity's FAKE null - a live C# reference that `==` reports as null - so `??` would
            // NOT fall through and this would dereference it. Unity's own operator is the test.
            var container = so.FindProperty("rowContainer")?.objectReferenceValue as RectTransform;
            if (!container) container = panel.transform as RectTransform;

            if (container && container.childCount > 0) return;

            problems.Add("The row panel has no rowTemplate and its container has no children - " +
                         "there is nothing to clone, so the board would draw zero rows. Assign " +
                         "the LeaderboardContent prefab to rowTemplate.");
        }

        static void Require(SerializedObject so, string field, string what, List<string> problems)
        {
            var prop = so.FindProperty(field);
            if (prop == null || !prop.objectReferenceValue)
                problems.Add($"{field} is not wired ({what}).");
        }
    }
}
