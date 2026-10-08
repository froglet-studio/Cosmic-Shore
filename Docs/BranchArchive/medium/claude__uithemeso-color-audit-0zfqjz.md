# Branch archive: `claude/uithemeso-color-audit-0zfqjz`

_Snapshot 2026-10-08. Index: [README](../README.md#index--medium-branches-410-unmerged-commits)_

- **Last commit:** 2026-08-25 by Shombith
- **Unmerged commits:** 6
- **Forked from:** `8b3a76921` (2026-08-25, docs(ui): commit the style foundation and architecture audit the tracker cites)
- **Tip:** `bfc5c3155`
- **Files touched (9):**
  - `Assets/_SO_Assets/UI/UITheme.asset`
  - `Assets/_SO_Assets/UI/UITheme.asset.meta`
  - `Assets/_Scripts/UI/Scoreboard.cs`
  - `Assets/_Scripts/UI/UIThemeHelper.cs`
  - `Assets/_Scripts/UI/UIThemeHelper.cs.meta`
  - `Assets/_Scripts/UI/UIThemeSO.cs`
  - `Assets/_Scripts/UI/UIThemeSO.cs.meta`
  - `Docs/UI_COLOR_TOKEN_MAP.md`
  - `Docs/UI_REDESIGN_TASKS.md`

### `3a23976e4` — feat(ui): add UIThemeSO chrome tokens and map the UI colour literals onto them

_Shombith, 2026-08-24 23:53:33 +0000_

```text
Authors the Style Foundation section 10 field map as a ScriptableObject:
the four surface tones, two border tones, four text tones, four system
hues, the nine-step spacing scale, both chamfers, the two stroke weights,
the four motion durations and the stagger pair. Exactly those 25 fields,
in document order, with the document's values as hardcoded defaults so an
unassigned reference degrades to the shipped look rather than to black.

Team colours are deliberately absent. Jade/Ruby/Gold stay in SO_ColorSet
behind GetDomainUIColor / GetDomainSignalColor / GetDomainUIAccentColor.
That separation is what enforces the team-colour contract: chrome is
neutral, so a coloured pixel is always telling the player something.

Follows the HUDAnimationSettingsSO pattern. Three accessors are added
because two of the fields cannot be read raw: Resolve(theme) returns the
asset or a hidden defaults instance, so a call site never restates a hex
(the way ScoreNumberAnimator already duplicates two of HUDAnimation's
defaults inline); Spacing(step) is 1-based to match the s1..s9 token
names and falls back loudly if the serialized array is resized, since a
silent fallback is indistinguishable from a theme that never applied;
StaggerFor(index) applies the cap, which is meaningless split across two
loose floats and is the exact mistake the hangar grid makes today.

Docs/UI_COLOR_TOKEN_MAP.md classifies all 184 colour-literal occurrences
in Assets/_Scripts/UI. 58 land on a theme field; 22 are team identity;
34 are vessel-HUD gauge states that argue for ElementalBarsConfigSO; 24
are editor-window chrome; 19 carry no authored colour at all; 14 are
console rich text; 13 have no home and are flagged for discussion rather
than answered with invented fields. No call sites changed.

Verified: compiles clean against Unity-shaped stubs, and all 25 token
values assert against the document (colours, spacing, clamp, stagger
cap), with the asset YAML round-tripping to the same 14 hex values.
Not verified in the Editor - no Unity instance is reachable from this
session, so /verify-unity did not run.
```

```text
 Assets/_SO_Assets/UI/UITheme.asset      |  48 +++++++
 Assets/_SO_Assets/UI/UITheme.asset.meta |   8 ++
 Assets/_Scripts/UI/UIThemeSO.cs         | 186 ++++++++++++++++++++++++
 Assets/_Scripts/UI/UIThemeSO.cs.meta    |  11 ++
 Docs/UI_COLOR_TOKEN_MAP.md              | 480 ++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
 5 files changed, 733 insertions(+)
```

<details><summary>Patch (code/doc/text files, first 150 of 678 lines)</summary>

```diff
diff --git a/Assets/_Scripts/UI/UIThemeSO.cs b/Assets/_Scripts/UI/UIThemeSO.cs
new file mode 100644
index 000000000..0a248cbe3
--- /dev/null
+++ b/Assets/_Scripts/UI/UIThemeSO.cs
@@ -0,0 +1,186 @@
+using UnityEngine;
+
+namespace CosmicShore.UI
+{
+    /// <summary>
+    /// THE chrome vocabulary for every menu screen, modal, panel and HUD frame —
+    /// authored verbatim from <c>Docs/STYLE_FOUNDATION.md</c> §10 (the field map).
+    /// Surfaces, borders, text ramp, system hues, the 8px spacing scale, the one
+    /// chamfer, stroke weights, and the motion/stagger tokens.
+    ///
+    /// <para><b>Team colours are deliberately NOT here.</b> Jade / Ruby / Gold live in
+    /// <see cref="CosmicShore.ScriptableObjects.SO_ColorSet"/> and are read through
+    /// <c>GetDomainUIColor</c> / <c>GetDomainSignalColor</c> / <c>GetDomainUIAccentColor</c>.
+    /// That separation is what enforces the team-colour contract in §3: chrome is neutral,
+    /// and a coloured pixel is always telling the player something. A team colour that
+    /// leaked into this asset would make a Ruby player's whole interface read as a
+    /// permanent error state.</para>
+    ///
+    /// <para>Follows the <see cref="HUDAnimationSettingsSO"/> pattern: every value has a
+    /// hardcoded default equal to the shipped token, so an unassigned reference degrades
+    /// to the correct look rather than to black. Call sites read
+    /// <c>theme ? theme.textBody : UIThemeSO.Fallback.textBody</c> — or simply
+    /// <c>UIThemeSO.Resolve(theme).textBody</c>.</para>
+    /// </summary>
+    [CreateAssetMenu(
+        fileName = "UITheme",
+        menuName = "ScriptableObjects/UI/UI Theme")]
+    public class UIThemeSO : ScriptableObject
+    {
+        // ---------------------------------------------------------------- colour
+        // Style Foundation §2. Hex is preserved in the Rgb() literals so these can be
+        // diffed against the document by eye. UI colours are sRGB, so byte/255 is
+        // exactly what ColorUtility.TryParseHtmlString would produce.
+
+        [Header("Surface ramp (§2)")]
+        [Tooltip("#07090F — scrims, modal backdrop, deepest field")]
+        public Color surfaceVoid = Rgb(0x07090F);
+        [Tooltip("#0E131C — default panel surface")]
+        public Color surfaceHull = Rgb(0x0E131C);
+        [Tooltip("#171E2A — raised surface, card, button rest")]
+        public Color surfacePlate = Rgb(0x171E2A);
+        [Tooltip("#212B3A — hover surface, active row")]
+        public Color surfaceRaise = Rgb(0x212B3A);
+
+        [Header("Borders (§2)")]
+        [Tooltip("#2A3444 — hairline border, divider")]
+        public Color borderRule = Rgb(0x2A3444);
+        [Tooltip("#3D4A5E — emphasised border, table head")]
+        public Color borderRuleHigh = Rgb(0x3D4A5E);
+
+        [Header("Text ramp (§2)")]
+        [Tooltip("#E8EDF5 — headings, primary values. Never pure white: it buzzes on a dark HUD over a moving arena")]
+        public Color textSignal = Rgb(0xE8EDF5);
+        [Tooltip("#B9C4D2 — body copy, descriptions")]
+        public Color textBody = Rgb(0xB9C4D2);
+        [Tooltip("#7C8899 — labels, secondary, captions")]
+        public Color textMuted = Rgb(0x7C8899);
+        [Tooltip("#4E5A6B — disabled, placeholder, metadata")]
+        public Color textFaint = Rgb(0x4E5A6B);
+
+        [Header("System and reserved hues (§2)")]
+        [Tooltip("#4FD5E8 — focus, selection, links, chrome accent, ALL pre-team UI. Cyan is the system hue because Domains.Blue is already the codebase's neutral non-playable sentinel")]
+        public Color systemAccent = Rgb(0x4FD5E8);
+        [Tooltip("#2A8A99 — inactive tab, unfilled track")]
+        public Color systemDim = Rgb(0x2A8A99);
+        [Tooltip("#A67CFF — new / unclaimed / CTA badge ONLY. Sits outside the Jade/Ruby/Gold gamut deliberately")]
+        public Color attention = Rgb(0xA67CFF);
+        [Tooltip("#FF5C3A — destructive FILL only, never a tint or a border. Form, not hue, separates it from Ruby")]
+        public Color danger = Rgb(0xFF5C3A);
+
+        // ------------------------------------------------------- space & geometry
+
+        [Header("Spacing scale (§5) — 8px base at the 1920 reference")]
+        [Tooltip("s1..s9. Every margin, padding and gap is a step on this scale. Read it through Spacing(step), which is 1-based to match the s1..s9 token names")]
+        public float[] spacing = { 4f, 8f, 12f, 16f, 24f, 32f, 48f, 64f, 96f };
+
+        [Header("Geometry (§5)")]
+        [Tooltip("14px, top-right only — panels, cards, modals. One 9-slice sprite serves all")]
+        public float chamferLarge = 14f;
+        [Tooltip("10px, top-right only — buttons, chips, badges. Border radius is 0 everywhere; the chamfer is the only corner treatment")]
+        public float chamferSmall = 10f;
+        [Tooltip("1px — all borders and dividers")]
+        public float hairline = 1f;
+        [Tooltip("2px — focus ring, selected state, own-chip border")]
+        public float stroke = 2f;
+
+        // ---------------------------------------------------------------- motion
+
+        [Header("Motion (§8)")]
+        [Tooltip("120ms, OutQuad — hover, tint, focus move")]
+        public float durMicro = 0.12f;
+        [Tooltip("200ms, OutCubic — button press, toggle, tab change")]
+        public float durStd = 0.20f;
+        [Tooltip("320ms, OutQuint — modal in/out, screen slide, toast entry")]
+        public float durPanel = 0.32f;
+        [Tooltip("500ms+, OutBack — quest claim and end-game reveal, nothing else")]
+        public float durCeremony = 0.50f;
+
+        [Header("Stagger (§8)")]
+        [Tooltip("40ms per item")]
+        public float staggerStep = 0.04f;
+        [Tooltip("Capped at 8 items — beyond this the delay stops growing, so a long list can never stall behind its own entrance")]
+        public int staggerCap = 8;
+
+        // ------------------------------------------------------------- accessors
+
+        /// <summary>Number of steps on the spacing scale (s1..s9).</summary>
+        public const int SpacingSteps = 9;
+
+        static readonly float[] DefaultSpacing = { 4f, 8f, 12f, 16f, 24f, 32f, 48f, 64f, 96f };
+
+        bool _warnedSpacing;
+
+        /// <summary>
+        /// The 1-based spacing token: <c>Spacing(1)</c> is <c>s1</c> (4px),
+        /// <c>Spacing(9)</c> is <c>s9</c> (96px). Out-of-range steps clamp to the scale.
+        ///
+        /// <para>Falls back to the shipped scale — loudly, once — if the serialized array
+        /// has been resized in the inspector. A silent fallback here would be
+        /// indistinguishable from a theme that never applied.</para>
+        /// </summary>
+        public float Spacing(int step)
+        {
+            int i = Mathf.Clamp(step, 1, SpacingSteps) - 1;
+
+            if (spacing == null || spacing.Length != SpacingSteps)
+            {
+                if (!_warnedSpacing)
+                {
+                    _warnedSpacing = true;
+                    Debug.LogWarning(
+                        $"[UIThemeSO] '{name}' has {(spacing == null ? 0 : spacing.Length)} spacing " +
+                        $"entries, expected {SpacingSteps} (s1..s9). Falling back to the shipped scale. " +
+                        "Restore the array length in the inspector.", this);
+                }
+                return DefaultSpacing[i];
+            }
+
+            return spacing[i];
+        }
+
+        /// <summary>
+        /// Entrance delay for the <paramref name="index"/>-th item in a staggered list,
+        /// honouring the §8 cap. The two fields are meaningless apart — the current hangar
```

</details>

### `a4b6deb42` — docs(ui): record T4 against the redesign tracker, and close two report gaps

_Shombith, 2026-08-25 00:15:52 +0000_

```text
Verifying T4's acceptance criteria against the working tree turned up two
places the mapping report fell short of what the task asked for, both
cheap to close now:

- 38 of the 180 sites were counted and bucketed but never cited by
  file:line - the console rich-text and editor-window groups were
  summarised in prose with totals only. Both now carry full site tables,
  so every one of the 184 occurrences is cited. Coverage is 180/180.
- The gaps were grouped by symptom rather than against the task's own
  (a) missing token / (b) feature-level SO / (c) never designed
  categories. They are now bucketed explicitly: 10 / 2 / 1.

Tracker: T4 is IN PROGRESS, six criteria of seven verified. The seventh
cannot be satisfied as written - "live asset created and referenced"
requires a [SerializeField] on some consumer, which the next criterion
forbids. Verified 0 references to the asset GUID and 0 scripts declaring
a UIThemeSO field, so the asset is deliberately unreferenced; which
clause wins needs a ruling rather than a guess.

Also recorded: the four non-field members added beyond the 25 (the
serialized surface is exactly 25, names/types/order an exact match
against the spec table), the 184-vs-165 count reconciliation, the two
live palette defects the audit surfaced, and the absence of both
companion docs the tracker names.

Four rows opened in the design feedback queue - negative feedback,
positive feedback, disabled content vs disabled chrome, and affordability
- each a decision the foundation does not currently make.
```

```text
 Docs/UI_COLOR_TOKEN_MAP.md | 56 ++++++++++++++++++++++++++++++++++++++++++++++++++++++++
 Docs/UI_REDESIGN_TASKS.md  | 39 ++++++++++++++++++++++++++++++---------
 2 files changed, 86 insertions(+), 9 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Docs/UI_COLOR_TOKEN_MAP.md b/Docs/UI_COLOR_TOKEN_MAP.md
index 68753f821..cfab932b5 100644
--- a/Docs/UI_COLOR_TOKEN_MAP.md
+++ b/Docs/UI_COLOR_TOKEN_MAP.md
@@ -229,6 +229,14 @@ _No existing literal maps here — greenfield token._
 13 occurrences that the §10 vocabulary cannot express. Listed for discussion; **no fields were
 invented for them.**
 
+Bucketed against T4's three categories:
+
+| Bucket | Count | Which |
+|---|---:|---|
+| **(a) missing token** — the foundation should name it | 10 | negative/urgent ×4, positive ×2, affordability ×2, locked tint ×1, disabled alpha ×1 |
+| **(b) belongs to a feature-level SO** — not chrome | 2 | gauge-at-full ×1, input-denied ×1 (both `ElementalBarsConfigSO`, alongside §5.2's 34) |
+| **(c) never designed** — no intent behind it | 1 | the decorative magenta cycle stop |
+
 
 **Negative / urgent feedback — 4 occurrences.** The biggest gap. §3 pins `danger` to destructive fill *only*, never a tint or a border, so score-loss text, the countdown-urgent tint, and the consent overlay's validation error have nowhere to go. Three of the four are already the *same colour* (`1, 0.3, 0.2` ≈ `#FF4D33`), a hair off `danger`'s `#FF5C3A` — the vocabulary is missing a word the codebase already has a value for. Options: a `warn` token distinct from `danger`; or rule that this is gameplay feedback and belongs in `ElementalBarsConfigSO` beside the existing fire/negative-debuff colour.
 
@@ -436,6 +444,54 @@ Separately worth noting against the project's own logging rule: the five `[FLOW-
 bring-up telemetry of exactly the kind CLAUDE.md says should sit on a `CSLogChannel`. Three of them
 already do; `Debug.LogError` at line 1320 and the three `LogColored` calls do not.
 
+**Console rich text (14)**
+
+| Site | Current | Reading |
+|---|---|---|
+| `Modals/ArcadeGameConfigureModal.cs:1232` | `CSDebug.LogVerbose(CSLogChannel.NetworkFlow, "<color=#FFD700>[FLOW-2] [ArcadeConfigModal…` | FLOW-2 trace |
+| `Modals/ArcadeGameConfigureModal.cs:1288` | `CSDebug.LogVerbose(CSLogChannel.NetworkFlow, "<color=#FFD700>[FLOW-2] [ArcadeConfigModal…` | FLOW-2 trace |
+| `Modals/ArcadeGameConfigureModal.cs:1305` | `CSDebug.LogVerbose(CSLogChannel.NetworkFlow, $"<color=#FFD700>[FLOW-2] [ArcadeConfigModa…` | FLOW-2 trace |
+| `Modals/ArcadeGameConfigureModal.cs:1320` | `Debug.LogError("<color=#FF0000>[FLOW-2] [ArcadeConfigModal] SyncAllGameDataForLaunch - g…` | FLOW-2 error trace |
+| `Modals/ArcadeGameConfigureModal.cs:1335` | `CSDebug.LogVerbose(CSLogChannel.NetworkFlow, $"<color=#FFD700>[FLOW-2] [ArcadeConfigModa…` | FLOW-2 trace |
+| `Screens/PartyInviteNotificationPanel.cs:146` | `Color.green);` | LogColored |
+| `Screens/PartyInviteNotificationPanel.cs:73` | `Color.magenta);` | LogColored |
+| `Screens/PartyInviteNotificationPanel.cs:79` | `Color.red);` | LogErrorColored |
+| `TestMiniGameEvents.cs:25` | `DebugExtensions.LogColored("OnMiniGameRoundStarted", Color.cyan);` | DebugExtensions.LogColored |
+| `TestMiniGameEvents.cs:30` | `DebugExtensions.LogColored("OnMiniGameRoundEnd", Color.cyan);` | DebugExtensions.LogColored |
+| `UniversalStatsProviderEditor.cs:413` | `CSDebug.Log($"<color=cyan><b>  STATS PREVIEW ({stats.Count} total)</b></color>");` | console rich text |
+| `UniversalStatsProviderEditor.cs:419` | `CSDebug.Log($"<color=cyan>[{icon}] <b>{stat.Label}</b>: {stat.Value}</color>");` | console rich text |
+| `WildlifeBlitzHUD.cs:57` | `CSDebug.Log($"<color=cyan>[WildlifeBlitzHUD] Target set to {targetScoreToWin}</color>");` | console rich text |
+| `WildlifeBlitzHUD.cs:93` | `CSDebug.Log("<color=yellow>[WildlifeBlitzHUD] Round ended - clearing displays</color>");` | console rich text |
+
+**Editor-window chrome (24)**
+
+| Site | Current | Reading |
+|---|---|---|
+| `ActiveGameModesWindow.cs:160` | `GUI.backgroundColor = new Color(0.5f, 0.8f, 0.5f);` | editor window |
+| `ActiveGameModesWindow.cs:168` | `GUI.backgroundColor = new Color(0.8f, 0.5f, 0.5f);` | editor window |
+| `LeaderboardConfigSOEditor.cs:145` | `GUI.backgroundColor = new Color(0.5f, 0.8f, 0.5f);` | — |
+| `LeaderboardConfigSOEditor.cs:151` | `GUI.backgroundColor = new Color(0.8f, 0.7f, 0.5f);` | — |
+| `LeaderboardConfigSOEditor.cs:199` | `EditorGUI.DrawRect(dotRect, new Color(0.3f, 0.8f, 0.3f));` | — |
+| `LeaderboardConfigSOEditor.cs:213` | `GUI.backgroundColor = new Color(0.6f, 0.8f, 1f);` | — |
+| `LeaderboardConfigSOEditor.cs:22` | `private static readonly Color headerColor = new Color(0.3f, 0.5f, 0.7f, 0.3f);` | — |
+| `LeaderboardConfigSOEditor.cs:23` | `private static readonly Color activeGameModeColor = new Color(0.4f, 0.7f, 0.4f, 0.15f);` | — |
+| `LeaderboardConfigSOEditor.cs:24` | `private static readonly Color inactiveGameModeColor = new Color(0.5f, 0.5f, 0.5f, 0.1f);` | — |
+| `LeaderboardConfigSOEditor.cs:25` | `private static readonly Color missingMappingColor = new Color(0.8f, 0.6f, 0.2f, 0.2f);` | — |
+| `LeaderboardConfigSOEditor.cs:290` | `GUI.backgroundColor = new Color(1f, 0.5f, 0.5f);` | — |
+| `Model/MinigameHUDInspector.cs:10` | `Color headerColor = new Color(0.09f, 0.24f, 0.48f);` | — |
+| `Model/MinigameHUDInspector.cs:11` | `Color sectionBlue = new Color(0.14f, 0.22f, 0.36f);` | — |
+| `Model/MinigameHUDInspector.cs:12` | `Color sectionGreen = new Color(0.14f, 0.32f, 0.21f);` | — |
+| `Model/MinigameHUDInspector.cs:23` | `MiniGameType.Freestyle => new Color(0.2f, 0.6f, 0.2f),// greenish` | — |
+| `Model/MinigameHUDInspector.cs:24` | `MiniGameType.CellularDuel => new Color(0.6f, 0.2f, 0.6f),// purple` | — |
+| `Model/MinigameHUDInspector.cs:77` | `normal = { textColor = Color.white },` | — |
+| `Model/MinigameHUDInspector.cs:93` | `normal = { textColor = Color.white },` | — |
+| `UniversalStatsProviderEditor.cs:17` | `private static readonly Color TrackerColor = new Color(0.4f, 0.6f, 0.9f);` | — |
+| `UniversalStatsProviderEditor.cs:18` | `private static readonly Color SuccessColor = new Color(0.3f, 0.8f, 0.4f);` | — |
+| `UniversalStatsProviderEditor.cs:19` | `private static readonly Color ErrorColor = new Color(0.9f, 0.35f, 0.35f);` | — |
+| `UniversalStatsProviderEditor.cs:20` | `private static readonly Color ListHeaderColor = new Color(0.5f, 0.5f, 0.5f);` | — |
+| `UniversalStatsProviderEditor.cs:303` | `miniLabelStyle.normal.textColor = Color.gray;` | — |
+| `UniversalStatsProviderEditor.cs:317` | `previewStyle.normal.textColor = new Color(0.6f, 0.8f, 0.6f);` | — |
+
 ---
 
 ## 6. What section 10 does not touch
diff --git a/Docs/UI_REDESIGN_TASKS.md b/Docs/UI_REDESIGN_TASKS.md
index f00055d4d..d99ddb61e 100644
--- a/Docs/UI_REDESIGN_TASKS.md
+++ b/Docs/UI_REDESIGN_TASKS.md
@@ -15,7 +15,7 @@ Maintained by the `ui-redesign-tracker` skill. Do not hand-edit the status table
 | T1 | Safe area component | TODO | — | | | |
 | T2 | Finish canvas resolution migration | TODO | — | | | |
 | T3 | Unify GameCanvas fork | TODO | T2 | | | |
-| T4 | UIThemeSO + literal inventory | TODO | — | | | |
+| T4 | UIThemeSO + literal inventory | IN PROGRESS | — | `claude/uithemeso-color-audit-0zfqjz` | | |
 | T5 | Download & install TMP fonts | TODO | — | | | |
 | T6 | TMP Style Sheet + Aldrich audit | TODO | T5 | | | |
 
@@ -93,17 +93,35 @@ Acceptance criteria:
 **Spec:** Style Foundation §10 · **Audit ref:** §1.4, §5.4
 
 Acceptance criteria:
-- [ ] `UIThemeSO` authored to §10 **verbatim** — 25 fields, no additions
-- [ ] Follows `HUDAnimationSettingsSO` pattern with hardcoded fallbacks
-- [ ] **No team colour fields** — they stay in `SO_ColorSet`
-- [ ] Live asset created and referenced
-- [ ] Mapping report covers all 165 literals in `Assets/_Scripts/UI/`
-- [ ] Unmapped literals bucketed: (a) missing token, (b) feature-level SO, (c) never designed
-- [ ] No call sites changed yet
+- [x] `UIThemeSO` authored to §10 **verbatim** — 25 fields, no additions
+- [x] Follows `HUDAnimationSettingsSO` pattern with hardcoded fallbacks
+- [x] **No team colour fields** — they stay in `SO_ColorSet`
+- [ ] Live asset created and referenced — created, **referenced by nothing** (see Deviations)
+- [x] Mapping report covers all 165 literals in `Assets/_Scripts/UI/`
+- [x] Unmapped literals bucketed: (a) missing token, (b) feature-level SO, (c) never designed
+- [x] No call sites changed yet
 
 **Deliverables:**
+- `Assets/_Scripts/UI/UIThemeSO.cs` — 25 serialized fields; names, types and declaration order verified as an exact match against §10's table. Values are hardcoded defaults, so an unassigned reference degrades to the shipped tokens.
+- `Assets/_SO_Assets/UI/UITheme.asset` (+ `.meta`) — authored instance, sibling to `HUDAnimationSettings.asset`. All 14 colour fields round-trip to §10's hex.
+- `Docs/UI_COLOR_TOKEN_MAP.md` — the mapping report. 184 occurrences classified across 180 sites, every site cited by `file:line`.
+
 **Findings:**
+- The literal count is **184 occurrences on 180 lines**, not 165, under the rule `new Color(` · `new Color32(` · `Color.<named>` · `<color=` over `Assets/_Scripts/UI/**/*.cs`. Same population — the audit's own worst-offender figures run approximate in the same direction (`PrivacyConsentOverlay` cited at 14 vs an actual 15; `SquirrelVesselHUDView` at "7–8" vs 16, counting only the authored `[SerializeField]` tints). The `new Color(` + named-`Color.X` subset alone is 170. All 184 are classified and the buckets sum, so nothing is unaccounted for either way.
+- Only **58** of the 184 are chrome. The rest: 34 vessel-HUD gauge states, 24 editor-window chrome, 22 team identity, 19 that carry no authored colour at all (alpha re-packs, `Color.clear`, captured-rest initialisers), 14 console rich text, 13 gaps.
+- **`danger` and `borderRule` have zero call sites.** Nothing in the UI currently draws a destructive fill or a code-side divider. Both are greenfield rather than consolidations.
+- **`attention` has one accidental match**: `IconRotator`'s violet cycle stop is `#A673FF`, four units off the token. Nothing else in the UI is violet.
+- **Two live defects, independent of any theming work.** `DomainVolumeHexGraphic.cs:84` hardcodes `{ Color.green, Color.red, Color.yellow }` as the three domain colours and never consults `SO_ColorSet`, so a domain re-colour silently misses that gauge. `ObjectiveArrowGraphic`'s three constants are fixed lime regardless of domain, so §3's "objective arrow … existing behaviour — keep" describes an intent the code does not implement.
+- **The census floor is a floor.** `HangarCaptainsView.cs:51-52` carries `"FFF"` / `"888"` as bare 3-digit hex strings, which match no colour-literal pattern. Colours authored on prefabs are invisible to the sweep entirely — and §7 replaces a per-prefab `_pressed`/`_selected`/`_inactive` sprite-swap approach, so the real interactive-state surface is larger than 58.
+- **Do not sweep `Color.white` globally.** 17 of the 64 are `textSignal`; the rest are untint resets, domain fallbacks, gauge rests and field initialisers. A blanket replace would dim invisible raycast targets, break domain fallbacks and change gauge semantics.
+- **`Docs/STYLE_FOUNDATION.md` and `Docs/UI_ARCHITECTURE_AUDIT.md` are not in the repo.** This tracker names both as companion docs at those paths; neither exists on `bleeding-edge`. T4 was implemented from copies supplied directly, and the report cites `STYLE_FOUNDATION.md` without a `Docs/` path for that reason.
+
 **Deviations from spec:**
+- **"Live asset created and referenced" cannot hold while "no call sites changed yet" holds.** An SO asset can only be referenced by a `[SerializeField] UIThemeSO` on a consumer, which is a call-site change. Verified: 0 references to the asset GUID in any prefab/scene/asset, and 0 scripts declaring a `UIThemeSO` field. The asset is created and authored; it is deliberately unreferenced. **Needs a ruling on which clause wins** — the criterion as written is unsatisfiable.
+- **Four non-field members were added beyond the 25 fields.** The serialized surface is exactly 25 and the inspector shows nothing extra, but the class also declares `SpacingSteps` (public const), `DefaultSpacing` (static readonly), `_warnedSpacing` (private bool) and `_fallback` (static), supporting three accessors — `Resolve(theme)`, `Spacing(step)`, `StaggerFor(index)`. Reasons: `spacing[9]` is an array a designer can resize, so a bounds-safe 1-based reader that warns once is needed or a mis-sized asset fails silently; `staggerStep`/`staggerCap` are meaningless apart, and read as two loose floats they reproduce the unbounded-stagger bug the hangar grid already has; and without `Resolve` every call site must restate a hex, which is how `HUDAnimationSettingsSO`'s defaults ended up duplicated inline at `ScoreNumberAnimator.cs:131-132`. **If the criterion is read strictly, all of this moves to a separate static class and `UIThemeSO` becomes 25 fields and nothing else** — a contained change, offered rather than assumed.
+- **Report scope is 184, not 165** (see Findings). Superset, fully reconciled in the report's §2.
+- **`UIThemeSO.cs` adds one `new Color(` to `Assets/_Scripts/UI/`** — the private `Rgb(int hex)` helper the tokens are built from. A strict cross-cutting grep reads this as +1; the count of *hardcoded* literals is unchanged at 184.
+- Report was published as an artifact as well as committed: https://claude.ai/code/artifact/b9c74280-7c9a-4f4d-9b6b-e7e03f65c048 — `Docs/UI_COLOR_TOKEN_MAP.md` is canonical.
 
 ---
 
@@ -155,7 +173,10 @@ Anything found during implementation that needs a design decision. The implement
 
 | # | Raised by | Task | Question | Status |
 |---|---|---|---|---|
-| | | | | |
+| 1 | T4 | T4 | No token for **negative / urgent feedback**. §3 pins `danger` to destructive *fill* only, so score-loss text, the countdown-urgent tint and the consent overlay's validation error have nowhere legal to go. Three of the four sites are already the same value (`1, 0.3, 0.2` ≈ `#FF4D33`), a hair off `danger`'s `#FF5C3A`. Add a `warn` token distinct from `danger`, or rule that this is gameplay feedback and belongs in `ElementalBarsConfigSO`? | OPEN |
+| 2 | T4 | T4 | No token for **positive feedback** (score gain). §2 has no success hue and green is Jade, so a success token collides with team identity the way §1.2 says Ruby collides with danger. §1.2's own answer — form disambiguates before hue — suggests motion or a glyph rather than a colour. Which? | OPEN |
+| 3 | T4 | T4 | **Disabled is defined for chrome controls, not for dimmed content.** §7 gives disabled a transparent surface, `rule` border and `faint` text; `GameCard.lockedTintColor` is a `(0.3,0.3,0.3)` multiply over card art and `DomainInfoData` swaps alpha `1.0`/`0.4`. Add a disabled tint/alpha token, or rule that content dims via `CanvasGroup` alpha? | OPEN |
+| 4 | T4 | T4 | **Requirement met / unmet** (`HangarCaptainsView` crystal and XP affordability) maps cleanly onto `textSignal` / `textMuted`, but "can I afford this" is arguably a status rather than a text weight. Confirm the text-ramp reading, or name it? | OPEN |
 
 ---
 
```

</details>

### `bd9f87a4f` — docs(ui): re-verify T4 against the now-committed style foundation

_Shombith, 2026-08-25 00:26:50 +0000_

```text
Both companion docs landed on bleeding-edge (8b3a7692). T4 was built from
copies supplied directly, so the first thing to establish was whether the
committed spec is the same document: Docs/STYLE_FOUNDATION.md is
byte-identical to the copy used, and section 10 re-verifies against it -
25 fields, names, types and declaration order an exact match, all 14
authored colours matching.

The tracker Finding that recorded both docs as absent is therefore stale
and is corrected in place. Report citations now point at the canonical
Docs/ paths rather than the bare filename they used while the files had
no home in the repo.

No criterion changed state. Six of seven verified; the seventh - "live
asset created and referenced" - still cannot hold while "no call sites
changed yet" holds, and still needs a ruling.
```

```text
 Docs/UI_COLOR_TOKEN_MAP.md | 6 +++---
 Docs/UI_REDESIGN_TASKS.md  | 2 +-
 2 files changed, 4 insertions(+), 4 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Docs/UI_COLOR_TOKEN_MAP.md b/Docs/UI_COLOR_TOKEN_MAP.md
index cfab932b5..780d9d2aa 100644
--- a/Docs/UI_COLOR_TOKEN_MAP.md
+++ b/Docs/UI_COLOR_TOKEN_MAP.md
@@ -1,8 +1,8 @@
 # UI colour-literal → `UIThemeSO` token map
 
 **Status:** audit, no call sites changed · **Scope:** `Assets/_Scripts/UI/**/*.cs`
-**Derives from:** `STYLE_FOUNDATION.md` §10 (field map), §2 (colour), §3 (team-colour contract), §7 (interactive states)
-**Companion to:** `Docs/PALETTE.md`, the UI Architecture Audit §1.4 / §5.4
+**Derives from:** `Docs/STYLE_FOUNDATION.md` §10 (field map), §2 (colour), §3 (team-colour contract), §7 (interactive states)
+**Companion to:** `Docs/PALETTE.md`, `Docs/UI_ARCHITECTURE_AUDIT.md` §1.4 / §5.4
 
 ---
 
@@ -500,7 +500,7 @@ Eleven of the 25 fields are non-colour and therefore have **zero** overlap with
 `spacing[9]`, `chamferLarge`, `chamferSmall`, `hairline`, `stroke`, `durMicro`, `durStd`,
 `durPanel`, `durCeremony`, `staggerStep`, `staggerCap`.
 
-Their call sites are the *other* two piles the UI Architecture Audit §5.4 counts, which this report
+Their call sites are the *other* two piles `Docs/UI_ARCHITECTURE_AUDIT.md` §5.4 counts, which this report
 did not enumerate:
 
 - **~50 hardcoded `sizeDelta` / `anchoredPosition` writes** → `spacing`, `chamfer*`
diff --git a/Docs/UI_REDESIGN_TASKS.md b/Docs/UI_REDESIGN_TASKS.md
index d99ddb61e..2bfd420f9 100644
--- a/Docs/UI_REDESIGN_TASKS.md
+++ b/Docs/UI_REDESIGN_TASKS.md
@@ -114,7 +114,7 @@ Acceptance criteria:
 - **Two live defects, independent of any theming work.** `DomainVolumeHexGraphic.cs:84` hardcodes `{ Color.green, Color.red, Color.yellow }` as the three domain colours and never consults `SO_ColorSet`, so a domain re-colour silently misses that gauge. `ObjectiveArrowGraphic`'s three constants are fixed lime regardless of domain, so §3's "objective arrow … existing behaviour — keep" describes an intent the code does not implement.
 - **The census floor is a floor.** `HangarCaptainsView.cs:51-52` carries `"FFF"` / `"888"` as bare 3-digit hex strings, which match no colour-literal pattern. Colours authored on prefabs are invisible to the sweep entirely — and §7 replaces a per-prefab `_pressed`/`_selected`/`_inactive` sprite-swap approach, so the real interactive-state surface is larger than 58.
 - **Do not sweep `Color.white` globally.** 17 of the 64 are `textSignal`; the rest are untint resets, domain fallbacks, gauge rests and field initialisers. A blanket replace would dim invisible raycast targets, break domain fallbacks and change gauge semantics.
-- **`Docs/STYLE_FOUNDATION.md` and `Docs/UI_ARCHITECTURE_AUDIT.md` are not in the repo.** This tracker names both as companion docs at those paths; neither exists on `bleeding-edge`. T4 was implemented from copies supplied directly, and the report cites `STYLE_FOUNDATION.md` without a `Docs/` path for that reason.
+- **Both companion docs landed mid-task** (`bleeding-edge` `8b3a7692`). T4 was implemented from copies supplied directly, before either was in the repo. The committed `Docs/STYLE_FOUNDATION.md` was diffed against the copy used and is **byte-identical**, and §10 was re-verified against the committed file: 25 fields, names/types/order an exact match, and all 14 authored colours matching. The report's citations now point at the canonical paths.
 
 **Deviations from spec:**
 - **"Live asset created and referenced" cannot hold while "no call sites changed yet" holds.** An SO asset can only be referenced by a `[SerializeField] UIThemeSO` on a consumer, which is a call-site change. Verified: 0 references to the asset GUID in any prefab/scene/asset, and 0 scripts declaring a `UIThemeSO` field. The asset is created and authored; it is deliberately unreferenced. **Needs a ruling on which clause wins** — the criterion as written is unsatisfiable.
```

</details>

### `bfc5c3155` — refactor(ui): make UIThemeSO 25 fields and nothing else, and prove the reference

_Shombith, 2026-08-25 01:09:06 +0000_

```text
Two decisions, both taken on review.

UIThemeSO now carries its 25 serialized fields and no other member - no
constants, no static state, no accessors. That is what makes "authored to
section 10 verbatim" a claim a script can check against the document's
table instead of a judgement call, and it is now checked: names, types
and declaration order are an exact match, and the asset's 14 colours
round-trip to the document's hex.

Everything that is not a token moved to UIThemeHelper: Resolve(),
Spacing(step) and StaggerFor(index) as extension methods, the fallback
instance, and Rgb(0xRRGGBB). The accessors are extension methods so they
are safe on an unassigned reference - an extension method may be invoked
on a null this - and the null path is covered by the value assertions.
Rgb moved with them rather than being inlined, so the field initialisers
still show the document's hex; inlining would also have added 14 colour
literals to Assets/_Scripts/UI, against the cross-cutting check.

Scoreboard gains one [SerializeField] UIThemeSO, declared and never read.
The consumer is not arbitrary: Scoreboard sits on GameCanvas.prefab, so
the eventual inspector assignment is a single prefab edit reaching every
game mode and creating no scene override. The sibling
HUDAnimationSettingsSO is assigned per-scene across 12 scenes - exactly
the pattern T3 exists to unwind - so a per-scene consumer here would have
handed T3 twelve new overrides to clean up.

Criterion 7 is amended from "no call sites changed" to "no call sites
restyled". The old wording made criterion 4 unsatisfiable, since an SO
can only be referenced by a serialized field on a consumer. The ban is on
swapping literals for tokens: verified, 0 colour literals removed from
Assets/_Scripts/UI on this branch.

Criterion 4 is [~], not [x]. The reference is declared but the asset has
0 GUID references until someone assigns it on GameCanvas.prefab, which
needs the editor. T4 stays IN PROGRESS on that one item.

Verified: compiles clean (0 errors, 0 warnings), 25/25 value assertions
pass including the null-reference path, conditional-compilation check OK
over 1740 files. Not Editor-verified; no Unity instance is reachable from
this session.
```

```text
 Assets/_Scripts/UI/Scoreboard.cs         |   9 +++++
 Assets/_Scripts/UI/UIThemeHelper.cs      | 106 ++++++++++++++++++++++++++++++++++++++++++++++++++++++
 Assets/_Scripts/UI/UIThemeHelper.cs.meta |  11 ++++++
 Assets/_Scripts/UI/UIThemeSO.cs          | 119 +++++++++++--------------------------------------------------
 Docs/UI_COLOR_TOKEN_MAP.md               |  48 +++++++++++++++----------
 Docs/UI_REDESIGN_TASKS.md                |  15 ++++----
 6 files changed, 186 insertions(+), 122 deletions(-)
```

<details><summary>Patch (code/doc/text files, first 150 of 398 lines)</summary>

```diff
diff --git a/Assets/_Scripts/UI/Scoreboard.cs b/Assets/_Scripts/UI/Scoreboard.cs
index f103ac79e..d1738cde6 100644
--- a/Assets/_Scripts/UI/Scoreboard.cs
+++ b/Assets/_Scripts/UI/Scoreboard.cs
@@ -91,6 +91,15 @@ namespace CosmicShore.UI
         [Header("Animation (optional)")]
         [SerializeField] private HUDAnimationSettingsSO animSettings;
 
+        [Header("Theme (optional)")]
+        [Tooltip("Chrome tokens from Docs/STYLE_FOUNDATION.md §10 - surfaces, borders, text " +
+                 "ramp, system hues, spacing, chamfers, motion. Team colours are NOT here; " +
+                 "they stay in SO_ColorSet. Read through UIThemeHelper (theme.Resolve(), " +
+                 "theme.Spacing(4), theme.StaggerFor(i)), which falls back to the shipped " +
+                 "tokens when this is unassigned. Assigned on GameCanvas.prefab, so one " +
+                 "reference serves every game mode and no scene override is needed.")]
+        [SerializeField] private UIThemeSO uiTheme;
+
         #endregion
 
         #region Private Fields
diff --git a/Assets/_Scripts/UI/UIThemeHelper.cs b/Assets/_Scripts/UI/UIThemeHelper.cs
new file mode 100644
index 000000000..c9303a806
--- /dev/null
+++ b/Assets/_Scripts/UI/UIThemeHelper.cs
@@ -0,0 +1,106 @@
+using System.Collections.Generic;
+using UnityEngine;
+
+namespace CosmicShore.UI
+{
+    /// <summary>
+    /// Read access for <see cref="UIThemeSO"/>, and the hardcoded fallbacks that make an
+    /// unassigned theme reference degrade to the shipped tokens rather than to black.
+    ///
+    /// <para>This lives beside the asset rather than on it deliberately: <c>UIThemeSO</c> is
+    /// <b>25 serialized fields and nothing else</b>, which is what makes "authored to
+    /// <c>Docs/STYLE_FOUNDATION.md</c> §10 verbatim" a checkable claim rather than a
+    /// judgement call. Anything that is not a token belongs here.</para>
+    ///
+    /// <para>Call sites read <c>theme.Resolve().textBody</c>, <c>theme.Spacing(4)</c>,
+    /// <c>theme.StaggerFor(i)</c> — all safe on an unassigned reference, since an extension
+    /// method may be invoked on a null <c>this</c>.</para>
+    /// </summary>
+    public static class UIThemeHelper
+    {
+        /// <summary>Steps on the §5 spacing scale (<c>s1</c>..<c>s9</c>).</summary>
+        public const int SpacingSteps = 9;
+
+        static readonly float[] DefaultSpacing = { 4f, 8f, 12f, 16f, 24f, 32f, 48f, 64f, 96f };
+
+        static readonly HashSet<int> WarnedSpacing = new HashSet<int>();
+
+        static UIThemeSO _fallback;
+
+        /// <summary>
+        /// A throwaway instance carrying nothing but <see cref="UIThemeSO"/>'s hardcoded
+        /// defaults. Prefer <see cref="Resolve"/>.
+        /// </summary>
+        public static UIThemeSO Fallback
+        {
+            get
+            {
+                if (_fallback == null)
+                {
+                    _fallback = ScriptableObject.CreateInstance<UIThemeSO>();
+                    _fallback.name = "UITheme (defaults)";
+                    _fallback.hideFlags = HideFlags.HideAndDontSave;
+                }
+                return _fallback;
+            }
+        }
+
+        /// <summary>
+        /// The authored asset when one is wired, the hardcoded defaults when it is not.
+        /// Never returns null, so a call site never has to restate a token value —
+        /// which is how <see cref="HUDAnimationSettingsSO"/>'s defaults ended up duplicated
+        /// inline at <c>ScoreNumberAnimator.cs:131-132</c>.
+        /// </summary>
+        public static UIThemeSO Resolve(this UIThemeSO theme) => theme ? theme : Fallback;
+
+        /// <summary>
+        /// The 1-based spacing token: <c>Spacing(1)</c> is <c>s1</c> (4px), <c>Spacing(9)</c>
+        /// is <c>s9</c> (96px). Out-of-range steps clamp to the scale.
+        ///
+        /// <para>Falls back to the shipped scale — loudly, once per asset — if the serialized
+        /// array has been resized in the inspector. A silent fallback here would be
+        /// indistinguishable from a theme that never applied.</para>
+        /// </summary>
+        public static float Spacing(this UIThemeSO theme, int step)
+        {
+            var t = theme.Resolve();
+            int i = Mathf.Clamp(step, 1, SpacingSteps) - 1;
+            var scale = t.spacing;
+
+            if (scale == null || scale.Length != SpacingSteps)
+            {
+                if (WarnedSpacing.Add(t.GetInstanceID()))
+                    Debug.LogWarning(
+                        $"[UIThemeSO] '{t.name}' has {(scale == null ? 0 : scale.Length)} spacing " +
+                        $"entries, expected {SpacingSteps} (s1..s9). Falling back to the shipped " +
+                        "scale. Restore the array length in the inspector.", t);
+
+                return DefaultSpacing[i];
+            }
+
+            return scale[i];
+        }
+
+        /// <summary>
+        /// Entrance delay for the <paramref name="index"/>-th item in a staggered list,
+        /// honouring §8's cap. The two fields are meaningless apart — the current hangar grid
+        /// runs 80ms across an unbounded list, which is exactly what the cap prevents.
+        /// </summary>
+        public static float StaggerFor(this UIThemeSO theme, int index)
+        {
+            var t = theme.Resolve();
+            return Mathf.Min(Mathf.Max(index, 0), Mathf.Max(t.staggerCap, 0)) * t.staggerStep;
+        }
+
+        /// <summary>
+        /// 0xRRGGBB, opaque. Keeps <c>Docs/STYLE_FOUNDATION.md</c> §10's hex visible in
+        /// <see cref="UIThemeSO"/>'s field initialisers, so the defaults can be diffed against
+        /// the document by eye.
+        /// </summary>
+        public static Color Rgb(int hex) => new Color(
+            ((hex >> 16) & 0xFF) / 255f,
+            ((hex >> 8) & 0xFF) / 255f,
+            (hex & 0xFF) / 255f,
+            1f);
+    }
+}
diff --git a/Assets/_Scripts/UI/UIThemeSO.cs b/Assets/_Scripts/UI/UIThemeSO.cs
index 0a248cbe3..414edcb51 100644
--- a/Assets/_Scripts/UI/UIThemeSO.cs
+++ b/Assets/_Scripts/UI/UIThemeSO.cs
@@ -18,9 +18,13 @@ namespace CosmicShore.UI
     ///
     /// <para>Follows the <see cref="HUDAnimationSettingsSO"/> pattern: every value has a
     /// hardcoded default equal to the shipped token, so an unassigned reference degrades
-    /// to the correct look rather than to black. Call sites read
-    /// <c>theme ? theme.textBody : UIThemeSO.Fallback.textBody</c> — or simply
-    /// <c>UIThemeSO.Resolve(theme).textBody</c>.</para>
+    /// to the correct look rather than to black.</para>
+    ///
+    /// <para><b>This type is 25 serialized fields and nothing else</b> — no helpers, no
+    /// constants, no accessors — so "authored to §10 verbatim" stays mechanically checkable
+    /// against the document's table. Read access and the fallbacks live in
+    /// <see cref="UIThemeHelper"/>: <c>theme.Resolve().textBody</c>, <c>theme.Spacing(4)</c>,
+    /// <c>theme.StaggerFor(i)</c>.</para>
```

</details>

_Also contains 2 unmerged merge commit(s) (branch-sync merges; no authored changes of their own)._
